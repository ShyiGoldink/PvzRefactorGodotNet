# PvZ 重构解耦方案

> 生成时间：2026-09-20
> 项目根目录：`D:\pvz`
> Godot 4.7（C# .NET 8 / 导出模板 4.7.1.stable.mono.custom_build）
> 当前编译命令：`$env:NUGET_PACKAGES='C:\Users\24807\.nuget\packages'; dotnet build pvz.csproj --no-restore`
> 目标：让逻辑层脱离 Godot，表现层只负责"画"，数据层只负责"描述"

---

## 当前代码的事实（接手者必读）

这份文档的所有阶段都必须先核对这一节。如果阶段要求与事实冲突，以本节为准。

### 文件布局（当前仓库实际）

```
D:\pvz\
├── project.godot          # Godot 4.7 项目，主场景 WorldMap
├── pvz.csproj / pvz.sln   # Godot.NET.Sdk/4.7.1，TargetFramework net8.0
├── .godot/                # gitignore，缓存
├── build/                 # gitignore，导出产物
├── .tools/dotnet10/       # 便携 .NET 10 SDK（导出用）
├── scenes/                # 表现层（Godot 节点脚本）
│   ├── Battle/Battle.cs, UnitDrawer.cs, SeedBank.cs, TileGridOverlay.cs
│   ├── LevelSelect/LevelSelect.cs
│   └── WorldMap/WorldMap.cs, RegionButton.cs
├── scripts/               # 当前所有 C# 代码（无命名空间）
│   ├── Core/RunContext.cs
│   ├── Data/              # CharacterData, ComponentParams, LevelCatalog,
│   │                      # LevelData, LevelWave, PlantData, PlantLibrary,
│   │                      # RegionConfig, SaveData, ZombieData, ZombieLibrary
│   ├── Logic/GameManager.cs, EventBus.cs
│   └── Logic/Battle/      # BattleEntity, EntityComponent, EntityAssembler,
│   │                     # ComponentLibrary, AttackDirector, BattleField,
│   │                     # Projectile, Zombie, Plant, ZombieSpawner,
│   │                     # TilesData, TilePicker, WavePlanner, SunToken,
│   │                     # Tile, TileType, PlantState, ZombieState,
│   │                     # DamageEvent, BattleEventName,
│   │                     └── Components/ (Shooter, NormalMove, NormalAttack,
│   │                         NormalDamage, HeadArmor, PotatoMine,
│   │                         SunProducer, PlantAnimationComponent,
│   │                         NormalZombieAnimation, AnimationGroupComponent)
│   └── tool/JsonReader.cs
└── resource/data/         # JSON 配置 + 图集
```

### 关键事实

1. **仓库没有任何 `namespace` 声明**。所有文件都在全局命名空间。
2. **"逻辑层"并非没有 Godot 依赖**。以下"纯逻辑"文件直接 `using Godot;` 并使用 `Godot.Vector2`、`Godot.Mathf`、`Godot.GD`、`Godot.FileAccess`、`Godot.ProjectSettings`、`Godot.Json`、`Godot.Collections.Dictionary/Array`、`Godot.Variant`：
   - `scripts/Logic/GameManager.cs`
   - `scripts/Logic/Battle/EntityAssembler.cs`
   - `scripts/Logic/Battle/Projectile.cs`
   - `scripts/Logic/Battle/Zombie.cs`（`Vector2 Position`）
   - `scripts/Logic/Battle/ZombieSpawner.cs`（`Vector2`）
   - `scripts/Logic/Battle/TilesData.cs`（`Vector2`）
   - `scripts/Logic/Battle/TilePicker.cs`（`Vector2`, `Mathf`, `GD`）
   - `scripts/Logic/Battle/SunToken.cs`（`Vector2`, `Mathf`）
   - `scripts/Logic/Battle/WavePlanner.cs`（`GD`, `Mathf`）
   - `scripts/Data/RegionConfig.cs`（`Vector2`）
   - `scripts/Data/LevelCatalog.cs`（`DirAccess`）
   - `scripts/Data/LevelData.cs`（`GD`）
   - `scripts/Data/PlantData.cs`（`GD`）
   - `scripts/Data/SaveData.cs`（`FileAccess`, `ProjectSettings`, `Json`, `Godot.Collections.*`）
   - `scripts/Data/ZombieData.cs`（`Godot.Collections.*`）
   - `scripts/Data/ComponentParams.cs`（`GD`）
   - `scripts/Logic/Battle/ComponentLibrary.cs`（`GD`）
   - `scripts/Logic/Battle/Components/*.cs` 大部分 `using Godot;`
   - `scripts/Core/RunContext.cs`（`Godot`）
   - `scripts/tool/JsonReader.cs`（`Godot`, `FileAccess`）
3. **Godot 通过 `pvz.csproj` 发现 C# 脚本**。把文件移出 `scripts/` 而不同步改项目文件，Godot 编译会丢脚本。
4. **Godot 4.7 编辑器**在 `D:\Lya\bin\梨娅CSharp.exe`（用户自编译 4.7.1.stable.mono）。
5. **项目当前能编译**，`dotnet build pvz.csproj --no-restore` 通过。
6. **无头模式**：`$env:APPDATA='D:\pvz\.godot\_userdata'; $env:LOCALAPPDATA='D:\pvz\.godot\_userdata'` 必须设，否则崩溃。
7. **视觉验证**：`--write-movie` 录帧 + 采样像素，不靠肉眼。
8. **导出**：`build/pvz.exe` + `build/data_pvz_windows_x86_64/` 两个都要。

---

## 路线图（两条路线，必须先选）

| 期次 | 名称 | 适用路线 A（物理重组） | 适用路线 B（真解耦） |
|------|------|------|------|
| P0 | 物理重组 | 把 `scripts/` 挪到 `PvZ.Core/`，仍是同一 `.csproj` | 新建 `PvZ.Core.csproj`，引入类型适配器 |
| P1 | 组件注册表 | 组件枚举 + 静态注册表 | 同 A + 编译期依赖图 |
| P2 | 事件总线 | 泛型 `IHandle<T>` | 同 A + 零分配 |
| P3 | 数据 + ViewModel | FlatBuffers + Command Buffer | 同 A |

**路线 A**：改动小、风险低、Godot 无缝兼容；但逻辑层仍然引用 Godot 类型，不是真正"换引擎"。
**路线 B**：真正解耦，但 P0 就要重写所有 `Godot.Vector2/Mathf/GD` 的用法，工作量大。

**建议**：先做 A 跑通流程，再择机做 B。如果用户希望"下一步 AI 来直接开干"，选 A。

你的建议（选 A / 选 B / 自定义路线，请在此填写）：
>我选择B，但还是你来做。因为我们现在还在做实验性质的demo，所以没关系，慢慢做。

---

## P0：物理重组（路线 A）

### 是否批准 ☐  ☐  暂缓

**重构思路**  
把 `scripts/` 整体搬到 `PvZ.Core/`，**保留在同一个 `pvz.csproj` 编译**（不新建 `.csproj`，避免 Godot 丢脚本）。目录只是重组，代码零改动。

这是安全的起点：Godot 仍然从同一个项目文件编译，逻辑层文件虽然搬了位置，但仍在同一个 assembly 里，`using Godot;` 不需要动。

**具体指导**

1. 新建目录结构（先建空目录，不动文件）：
   ```powershell
   New-Item -ItemType Directory -Force D:\pvz\PvZ.Core\Core | Out-Null
   New-Item -ItemType Directory -Force D:\pvz\PvZ.Core\Data | Out-Null
   New-Item -ItemType Directory -Force D:\pvz\PvZ.Core\Logic\Battle\Components | Out-Null
   New-Item -ItemType Directory -Force D:\pvz\PvZ.Core\Tool | Out-Null
   ```

2. **移动文件**（保留 `*.uid` 配对文件一起移动）：
   ```powershell
   $moves = @(
     'scripts\Core\*',          'PvZ.Core\Core\'
     'scripts\Data\*',          'PvZ.Core\Data\'
     'scripts\Logic\Battle\Components\*', 'PvZ.Core\Logic\Battle\Components\'
     'scripts\Logic\Battle\*',  'PvZ.Core\Logic\Battle\'
     'scripts\Logic\*',         'PvZ.Core\Logic\'
     'scripts\tool\*',          'PvZ.Core\Tool\'
   )
   # 执行前先预览：
   $moves | ForEach-Object { Write-Host "MOVE $_" }
   # 确认后执行（逐对）：
   Move-Item -LiteralPath 'D:\pvz\scripts\Core\*' -Destination 'D:\pvz\PvZ.Core\Core\'
   # ...其余类似
   ```

3. **清空原目录**（只清空，保留 `scripts/` 目录本身以防回滚）：
   ```powershell
   Get-ChildItem D:\pvz\scripts -Recurse -File | Remove-Item -Force
   ```

4. **更新 Godot 的 `.csproj` 文件扫描路径**：Godot 4 默认只扫描 `scripts/`。需要改 `project.godot` 或让 Godot 重新导入。最稳妥的方式是在 `pvz.csproj` 里保留 `<Compile Include="..\PvZ.Core\**\*.cs" />` 或直接用 `<Compile Include="PvZ.Core\**\*.cs" />`。

   实际上 Godot .NET SDK 会自动包含项目目录下所有 `.cs` 文件——只要 `PvZ.Core/` 是 `pvz.csproj` 同目录下的子目录就会被发现。确认方法：`dotnet build` 后看 `PvZ.Core\` 下的 `.cs` 是否出现在编译列表。

5. **验证**：
   ```powershell
   $env:NUGET_PACKAGES='C:\Users\24807\.nuget\packages'
   dotnet build pvz.csproj --no-restore
   ```
   编译通过即 P0 完成。

6. **Godot 编辑器验证**：
   ```powershell
   $env:APPDATA='D:\pvz\.godot\_userdata'; $env:LOCALAPPDATA='D:\pvz\.godot\_userdata'
   & 'D:\Lya\bin\梨娅CSharp.exe' --headless --path 'D:\pvz' --quit-after 30
   ```

**回滚点**：如果 Godot 编译报错"脚本丢失"，把 `PvZ.Core/` 移回 `scripts/` 即可。`.uid` 文件不用管，Godot 不依赖它。

**本期的坑**
- `scripts/` 目录不能删（Godot 缓存可能还引用路径）。
- 如果 `dotnet build` 报"找不到文件"，检查 `PvZ.Core/` 下是否有 `.uid` 文件被误当成 C# 编译——实际上 `.uid` 不是 `.cs`，不会有这个问题。
- **不要新建 `PvZ.Core.csproj`**，否则 Godot 不会自动编译它，脚本会丢。

**你的建议**（请在此填写）：
>

---

## P1：组件注册表

### 是否批准 ■  ☐  暂缓

**重构思路**  
当前 `ComponentLibrary.Create(int id)` 用 `switch` 手工分发，`EntityComponent.Requirements` 用字符串做依赖检查。P1 的目标：

- 组件 ID 统一为 `enum ComponentId`（消灭魔法数字）
- 依赖检查改为类型名枚举或编译期注册
- 装配器仍然"不认识具体组件"，但 ID 校验在编译期

**具体指导**

1. 新建 `PvZ.Core/Logic/ComponentId.cs`：
   ```csharp
   // 编号段位沿用项目约定：1xxx 移动 / 2xxx 攻击 / 3xxx 防御与受击 /
   // 4xxx 行为与状态 / 5xxx 形态与动画 / 6xxx 控制与增益 / 7xxx 资源
   public enum ComponentId
   {
       AnimationGroup = 4001,
       NormalMove     = 1001,
       NormalAttack   = 2001,
       Shooter        = 2002,
       SunProducer    = 7001,
       PotatoMine     = 2003,
       NormalDamage   = 3001,
       HeadArmor      = 3002,
       NormalZombieAnimation = 5001,
       PeashooterAnimation  = 5006,
       SunflowerAnimation   = 5005,
       WallNutAnimation     = 5008,
       PotatoMineAnimation  = 5007,
   }
   ```

2. 改造 `EntityComponent`：`Id` 属性从 `int` 改成 `ComponentId`。
   注意：所有 `ComponentLibrary.Create(id)` 调用点也要同步改。

3. `ComponentLibrary` 从 `switch(int)` 改成 `switch(ComponentId)`。

4. **过渡期兼容**：如果配置 JSON 里组件编号仍是 `int`，`JsonReader` 读出来后强转 `(ComponentId)id`，保留数据格式不变。

**验证**：
```powershell
dotnet build pvz.csproj --no-restore
# 运行游戏，开一局，确认装配日志里有"组件 N 个"
```

**你的建议**（请在此填写）：
>

---

## P2：事件总线升级

### 是否批准 ■  ☐  暂缓

**重构思路**  
当前 `EventBus` 用 `Dictionary<string, List<EventResponse>>`，`Trigger` 时 `ToArray()` 快照、参数 `object` 装箱。P2 的目标：

- 事件名改成 `const string` 仍是字符串（保留兼容），但处理函数签名收紧
- 引入泛型 `IHandle<T>` 接口，事件载荷用结构体
- 优先级用 `EventOrderAttribute` 替代 `int priority` 字段

**具体指导**

1. 定义事件载荷结构体（示例）：
   ```csharp
   public readonly struct TickEvent { public float Delta; }
   public readonly struct HpChangedEvent { public float Delta; }
   public readonly struct StateChangedEvent { public object NewState; }
   public readonly struct AnimationChangedEvent { public string ClipName; }
   ```

2. 事件总线骨架：
   ```csharp
   public sealed class EventBus
   {
       private readonly List<IHandle>[] _handlers;

       public void Register<T>(IHandle<T> handler) where T : struct { ... }
       public void Trigger<T>(ref T evt) where T : struct { ... }
   }

   public interface IHandle<T> where T : struct
   {
       bool Handle(ref T evt);
   }
   ```

3. **兼容性**：保留旧的 `Register(string eventName, EventResponse handler)` 方法作为重载，内部仍然走字符串分发。这样表现层和逻辑层可以逐步迁移。

4. `BattleEventName` 常量保留（避免大量字符串替换引入错误）。

**验证**：
- 编译通过
- 写一个最小测试：触发 `TickEvent`，确认 `Handle` 按优先级执行
- 对比 GC 次数（可选，P2 后期再做）

**你的建议**（请在此填写）：
>

---

## P3：数据层 FlatBuffers + 表现层 ViewModel

### 是否批准 ■  ☐  暂缓

**重构思路**  
P3 是终极形态：

- 数据文件从 JSON 换成 FlatBuffers（或 MessagePack），零拷贝读取
- `PlantData`、`LevelData`、`RegionConfig` 变成不可变只读结构
- 表现层不直接读 `GameManager` 单例，而是通过 `IBattleView` 接口消费 `DrawCommand[]`
- 逻辑层产出指令，表现层执行指令，完全双向解耦

**具体指导**

1. **FlatBuffers schema**（`PvZ.Core/Data/pvz.fbs`）：
   ```flatbuffers
   table PlantData {
     id:int, display_name:string, population:string,
     cost:int, cooldown:float, hp:float, seed:bool,
     components:[ComponentParam]
   }
   // ...
   root_type LevelCatalog;
   ```

2. **构建时生成 C# 代码**：`flatc --csharp pvz.fbs`，输出并入 `PvZ.Core/Data/Generated/`

3. **运行时读取**：
   ```csharp
   var data = LevelDataParser.Parse(File.ReadAllBytes(path));
   ```

4. **ViewModel / Command Buffer**：
   ```csharp
   public readonly struct DrawUnitCommand
   {
       public object Entity;
       public Vector2 Position;
       public string AtlasPath;
       public Vector2 CellSize;
       public int Row, Frame;
       public Vector2 Anchor;
       public float Scale;
   }

   public interface IBattleView
   {
       void OnFrame(DrawUnitCommand[] units, SunToken[] suns, Projectile[] projectiles);
       void OnResult(BattleResult result);
   }
   ```

5. `UnitDrawer` 实现 `IBattleView`，逻辑层 `GameManager.Step()` 结束后调用 `_view.OnFrame(...)`。

**验证**：
- FlatBuffers 加载速度对比 JSON
- 逻辑层在无 Godot 环境跑完整一局（需要先完成路线 B 的类型适配器）
- 表现层热重载：改 `UnitDrawer` 的绘制逻辑不重新编译逻辑层

**你的建议**（请在此填写）：
>

---

## 附录 A：当前项目关键文件清单（重构前基线）

| 文件 | 行数 | 角色 | Godot 依赖 |
|------|------|------|------|
| `scripts/Logic/GameManager.cs` | ~230 | 战斗主循环 | `Vector2`, `Mathf`, `GD` ⚠️ |
| `scripts/Logic/Battle/BattleField.cs` | ~170 | 战场管理 | 无 |
| `scripts/Logic/Battle/AttackDirector.cs` | ~50 | 攻击调度 | 无 |
| `scripts/Logic/Battle/EntityAssembler.cs` | ~100 | 实体装配 | `GD` ⚠️ |
| `scripts/Logic/Battle/BattleEntity.cs` | ~110 | 实体基类 | 无 |
| `scripts/Logic/Battle/Plant.cs` | ~80 | 植物基类 | 无 |
| `scripts/Logic/Battle/Zombie.cs` | ~90 | 僵尸基类 | `Vector2` ⚠️ |
| `scripts/Logic/Battle/Projectile.cs` | ~60 | 子弹 | `Vector2` ⚠️ |
| `scripts/Logic/Battle/ZombieSpawner.cs` | ~170 | 出怪 | `Vector2`, `GD` ⚠️ |
| `scripts/Logic/Battle/TilePicker.cs` | ~50 | 格子定位 | `Vector2`, `Mathf`, `GD` ⚠️ |
| `scripts/Logic/Battle/TilesData.cs` | ~80 | 格子数据 | `Vector2`, `GD` ⚠️ |
| `scripts/Logic/Battle/ComponentLibrary.cs` | ~40 | 组件工厂 | `GD` ⚠️ |
| `scripts/Logic/EventBus.cs` | ~70 | 事件总线 | 无 |
| `scenes/Battle/Battle.cs` | ~130 | 对战场景 | ✅ Godot 节点 |
| `scenes/Battle/UnitDrawer.cs` | ~170 | 单位渲染 | ✅ Godot 节点 |
| `scenes/Battle/SeedBank.cs` | ~120 | 种子栏 | ✅ Godot 节点 |
| `scenes/Battle/TileGridOverlay.cs` | ~40 | 格子线 | ✅ Godot 节点 |
| `scripts/Data/PlantData.cs` | ~50 | 植物数据 | `GD` ⚠️ |
| `scripts/Data/LevelData.cs` | ~140 | 关卡数据 | `GD` ⚠️ |
| `scripts/Data/RegionConfig.cs` | ~70 | 区域配置 | `Vector2` ⚠️ |
| `scripts/Data/ZombieData.cs` | ~60 | 僵尸数据 | `Godot.Collections.*` ⚠️ |
| `scripts/Data/CharacterData.cs` | ~50 | 角色数据 | 无 |
| `scripts/tool/JsonReader.cs` | ~130 | JSON 解析 | `Godot`, `FileAccess` ⚠️ |

⚠️ 标 `Godot` 的文件在搬去 Core 时需要把 `Godot.*` 类型替换为自定义类型（如 `Vector2` → `struct Vec2 { public float X, Y; }`），或者留在主项目。

---

## 附录 B：编译与运行命令

```powershell
# 编译
$env:NUGET_PACKAGES='C:\Users\24807\.nuget\packages'
dotnet build pvz.csproj --no-restore

# 无头运行（测逻辑）
$env:APPDATA='D:\pvz\.godot\_userdata'; $env:LOCALAPPDATA='D:\pvz\.godot\_userdata'
& 'D:\Lya\bin\梨娅CSharp.exe' --headless --path 'D:\pvz' --quit-after 120

# 开真实窗口测输入
& 'D:\Lya\bin\梨娅CSharp.exe' --path 'D:\pvz' res://scenes/Battle/Battle.tscn --quit-after 120

# 导出 exe
$env:PATH = 'D:\pvz\.tools\dotnet10;' + $env:PATH
$env:DOTNET_ROOT = 'D:\pvz\.tools\dotnet10'
$env:NUGET_PACKAGES = 'C:\Users\24807\.nuget\packages'
New-Item -ItemType Directory -Force -Path 'D:\pvz\build' | Out-Null
& 'D:\Lya\bin\梨娅CSharp.exe' --headless --path 'D:\pvz' `
  --export-release 'Windows Desktop' 'D:\pvz\build\pvz.exe'
```

---

## 附录 C：Git 分支策略

```
main (当前)
├── refactor/p0-physical-reorg      ← P0 路线 A
├── refactor/p1-component-registry  ← P1
├── refactor/p2-struct-eventbus     ← P2
└── refactor/p3-flatbuffers-viewmodel ← P3
```

每一期独立 Merge Request，每一期都可以 `git revert` 回退。

**当前仓库状态**：`git status` 显示只有 `docs/` 和 `na'sh.zip` 是未跟踪的，`scripts/` 下的改动都未提交。P0 开始前建议先 `git add -A && git commit -m "重构前基线"`，方便回滚。

---

## 附录 D：给接手 AI 的快速上手

1. 读本文件，从 **P0** 开始
2. `$env:NUGET_PACKAGES='C:\Users\24807\.nuget\packages'; dotnet build pvz.csproj --no-restore` 确认编译通过
3. Godot 编辑器打开项目，确认对战场景能正常运行
4. 每完成一期，更新本文件的"是否批准"状态和"你的建议"栏
5. 遇到问题先查 `docs/` 目录，本文件是最权威的架构文档
6. **先看附录 A 的事实清单**：如果阶段要求与事实冲突，以附录 A 为准

---

## 审批记录

| 日期 | 期次 | 路线 | 批准人 | 备注 |
|------|------|------|--------|------|
| 2026-09-20 | P0 | A/B 待选 | 待填 | |
| | P1 | A/B 待选 | 待填 | |
| | P2 | A/B 待选 | 待填 | |
| | P3 | A/B 待选 | 待填 | |

---

## 你的建议汇总区

> 在上方每个"你的建议"栏里填写。所有建议会在最终确认时汇总。

### P0 建议：
>

### P1 建议：
>

### P2 建议：
>

### P3 建议：
>

---

*本文档是项目架构的唯一真相源（Single Source of Truth）。每次重构完成后必须更新。*
