# 交接文档（给新的 Codex 会话）

这份文档是给"重启之后接手这个项目的新会话"看的。读完它 + 下面列的三份设计文档，
就能接着干活，不需要用户重新解释一遍。

**先读这三份：**

1. `assets/docs/design-answers.md` —— 用户对问卷的回答 + 本次要做完的目标清单（最重要）
2. `assets/docs/level-data.md` —— 关卡/区域/地形的数据约定
3. `assets/docs/character-data.md` —— 角色数据约定

---

## 1. 项目和仓库

- 路径：`D:\pvz`，Godot 4.7.1 .NET 项目，C#。
- 远端：`https://github.com/ShyiGoldink/PvzRefactorGodotNet.git`，分支 `master`。
- 用户是中文交流，代码注释和文档都用中文。
- 这是一个 PvZ 同人游戏，**逻辑 / 数据 / 表现完全分离**是硬要求：
  不放进场景树、没有任何动画，也能在数据上跑完一整局；表现层只是把数据画出来。

---

## 2. 这台机器上的坑（很重要，能省一小时）

### 2.1 编译

```powershell
$env:NUGET_PACKAGES='C:\Users\24807\.nuget\packages'; dotnet build pvz.csproj -v quiet
```

**必须设 `NUGET_PACKAGES`**。不设的话 NuGet 会把全局包目录解析成一个相对路径，
找不到缓存的 `Godot.NET.Sdk 4.7.1`，直接报 "无法解析 SDK"。

### 2.2 跑起来

Godot 编辑器在 `D:\Lya\bin\梨娅CSharp.exe`（4.7.1.stable.mono.custom_build，用户自编译版）。

```powershell
$env:APPDATA='D:\pvz\.godot\_userdata'; $env:LOCALAPPDATA='D:\pvz\.godot\_userdata'
& 'D:\Lya\bin\梨娅CSharp.exe' --headless --path 'D:\pvz' --quit-after 120
```

**必须把 `APPDATA` / `LOCALAPPDATA` 指到项目目录里**，否则 Godot 要往真实
`%APPDATA%` 写 `user://logs`，被沙箱挡住后会**直接崩溃**。
跑完记得删掉 `D:\pvz\.godot\_userdata`（它在 `.godot/` 里，不影响仓库）。

其它常用形式：

- 只跑某个场景：在 `--path` 后面加上 `res://scenes/Battle/Battle.tscn`
- 单独跑选关：`res://scenes/LevelSelect/LevelSelect.tscn`
- 编辑器导入扫描（**新加的图片资源必须先跑这个，否则运行时 `GD.Load` 拿到 null**）：
  `--headless --editor --quit-after 300`

### 2.3 视觉验证（这个项目一直在用，很有用）

用户看不到你的屏幕、你也看不到图，所以"到底画出来没有"要靠**录帧 + 采样像素**：

```powershell
& godot --path 'D:\pvz' <场景> --write-movie 'D:\pvz\.godot\_shot\a.png' --quit-after 600
```

会在 `_shot` 下生成 `a00000000.png` 起的一串帧（**1920×1080，和设计分辨率 1:1**）。
然后用 `System.Drawing` 读像素：查某个坐标的颜色、按颜色扫出包围盒、数某一行里有几条线。

这套办法验证过"草坪位置对不对""格子线画没画出来""僵尸有没有走进画面""动画帧在不在翻"，
比只看日志可靠得多。`shoot` 之前记得先建目录。

### 2.4 沙箱不稳

有一次事故：命令行大约五次成一次，报 `helper_unknown_error`；
`apply_patch` 的**修改和删除**直接失败（读文件时就报错），只有**新增文件**能用；
`Remove-Item` 和 `[System.IO.File]::Delete` 都不行。新会话如果又遇到，就是老毛病：
**命令多试几次**，改文件前先试一次小改动探路。

---

## 3. 现在的代码结构

```
resource/data/          数据（纯数据，不放行为）
  1/region.json         区域：背景、tile_origin、tile_size、spawn_margin、地形表
  1/1.json              关卡：terrain_grid、stages（剧情/开始/中间/结束）
  plants/<id>/          每种植物一个文件夹（新约定，正在迁移）
  zombies/<id>/         每种僵尸一个文件夹（新约定，正在迁移）
assets/docs/            设计文档
scenes/                 表现层
  WorldMap/             羊皮卷地图（点击展开 → 区域按钮）
  LevelSelect/          选关（扫数据目录，一页 4x4 翻页）
  Battle/               对战：Background / Lawn / TileGrid + ZombieView
scripts/
  tool/JsonReader.cs    通用 JSON 读字段工具（不认识任何业务概念）
  Data/                 数据层：把 JSON 读成纯 C# 对象
  Logic/                逻辑层：纯 C#，不继承 Godot 节点
    GameManager.cs      开局 / 每帧推进 / 缩放倍率
    EventBus.cs         事件中心：按优先级执行，处理函数返回 false 可以阻塞后面的
    Battle/             BattleEntity、EntityComponent、EntityAssembler、
                        ComponentLibrary、Tile、TilesData、TilePicker、
                        Zombie、ZombieState、ZombieSpawner、DamageEvent、
                        BattleEventName、Components/
  Core/RunContext.cs    场景之间传参（选了哪个区域 / 哪一关）
```

### 已经定死的几条

- **组件化装配**：具体植物/僵尸不是子类，是"基类 + 若干组件"，由 `EntityAssembler`
  按配置装配。装配器**不认识任何具体组件**，加组件只改 `ComponentLibrary` 一行。
- **组件编号段位**：1xxx 移动 / 2xxx 攻击 / 3xxx 防御与受击 / 4xxx 行为与状态 /
  5xxx 形态与动画 / 6xxx 控制与增益 / 7xxx 资源。
- **坐标**：区域像素空间，原点在区域图左上角，向右 x 变大、向下 y 变大。
  `TilePicker` 负责屏幕像素 → 格子，且**屏幕怎么缩放都准**（先拉回区域局部坐标再算）。
- **僵尸的 `Position` 是"嘴"**，不是身体中心。

---

## 4. 目前做到哪了

已经能跑通的：

- 羊皮卷地图：合上 → 点击展开 → 区域按钮（农场 id=1 → `resource/data/1/`）
- 选关：扫目录里有哪些 json 就有几关，一页 4x4，翻页
- 对战场景：区域背景 + 一整块草坪按 `tile_origin`/`tile_size` 摆好，2px 格子线可视化
- 逻辑层：普通僵尸 = 移动 + 啃食 + 受伤 + 动画组四个组件，**零节点**下跑通
  走 → 撞到植物转啃 → 咬 → 打死它 → 进倒下状态
- 出怪：读关卡 `start` 阶段那三波，在草坪右边缘再往外 `spawn_margin` 出场，随机挑车道，走进画面
- 占位动画：每单位一张图集的方案**还没做**，现在是老的"一帧一个文件"

**没做的**（这次目标，见 `design-answers.md` 第 6 节）：
4 种僵尸、4 种植物、选卡、种子包、存档、望远镜预览、对象池、绘制器、种群。

---

## 5. 下一步按这个顺序做（用户已经确认过方向）

**每一步都先编译 + 无头跑一遍再往下走。**

1. **动画改成"图集 + 行=动作"**
   - 一个单位一张图集，一行一个动作、横向 24 帧，含它自己的子弹
   - 组件参数形如
     `"5001": { "atlas": "...", "cell": [160,200], "columns": 24, "fps": 12, "walk": 0, "eat": 1, ... }`
   - 表现层改成**一个绘制器**：一个 `_Draw` 里用 `DrawTextureRectRegion` 把场上所有单位画完，
     同种单位共用一张纹理 → Godot 自动合批
2. **单位库 + 图鉴**
   - 植物/僵尸配置改成每单位一个文件夹（`resource/data/plants/<id>/`）
   - 库里存 id → 组件 id；库要注册到图鉴（选卡界面从图鉴取显示信息）
   - 植物 id 从 **1000000** 起，僵尸从 **1** 起
3. **战场与攻击管理**（问卷 A1/A2/B1/B2）
   - 拆成"攻击管理 / 移动管理"两个小类；伤害也归攻击管理管
   - 合批：加锁，一次处理期间的通知合并成一次刷新；锁要能在事件层选粒度（以后有"三线"）
   - 植物该不该攻击：**同路、僵尸身体中心 x 比自己大**；走过了就不打，除非路上还有别的僵尸
   - 植物的攻击状态**统一由管理器改**，植物自己不改
4. **子弹**（A3/A4）
   - 豌豆自己飞，不每帧问；锁定目标后直接读它的位置
   - 目标死了/没了：重新锁；再没有就照原目标点爆（保底）
5. **内容**：4 僵尸（普通 / 路障 / 旗帜 / 铁桶）+ 4 植物（向日葵 / 豌豆射手 / 土豆地雷 / 坚果）
   - 路障、铁桶 = `takeDamage` 链上优先级更高的防具，**没掉完之前 return false 阻塞后面的扣血**
   - 旗帜僵尸：一大波时必刷一个、不算价值，要有开关
   - 土豆地雷本身就是子弹
   - 向日葵每 30 秒在本格放一颗 1000 血的瓜子；被啃或在前半场切"害怕待机"
6. **GameManager 固定步长** + 阳光经济
7. **选卡 / 种子包 / 存档 / 望远镜预览**
   - 存档放 `user://`
   - 已解锁植物数不满种子包上限 → 不加载选卡界面直接开打
   - 种子包在屏幕上方
   - 预览僵尸：黑底 + 一个圆可见，拖动圆看这一关会出什么，另有退出按钮
8. **对象池**：按种类，组件在入池时统一挂好；容量随难度（简单 10 起）
9. 编译、录帧采样验证、`git commit` + `git push`

---

## 6. 已知遗留

- `resource/data/plants/1000002/plant.json` 是个半成品（引用的图集还没生成），
  目前**没有任何代码读它，不影响运行**。它是在沙箱坏掉、删不掉文件的时候被一起提交的，
  环境正常后第一件事就是删掉它。
- `resource/data/zombies/1.json` 和 `resource/data/plants/placeholder.json`
  是旧约定的平铺配置文件，迁移到"每单位一个文件夹"之后应该删掉。
- 植物的血量、被啃、死亡还没做（僵尸咬下去只是把 `take_damage` 事件发给植物，
  植物那边还没人接）。第 5 步会补上。

---

## 7. 和用户协作的方式

- 用户要求：**没说的不要做**；有逻辑问题或不清楚的地方**先问**。
- 他喜欢看到"真的验证过"的结果，所以每次改完都要编译 + 跑 + 报告证据。
- 他对自己想要什么很明确，会直接指出做错的地方。猜错了不要辩解，改。
- 提交用中文写 commit message。他自己有一套 git 习惯（分支 master）。

---

## 8. 进度更新：农场第一关已经能完整跑完

（这一节比前面的"下一步"更新，冲突时以本节为准。）

### 现在能跑通的完整流程

在地图上点农场 → 选关点第 1 关 → 进对战：上方是种子包（图标从图集抠、
买不起的卡片变暗、冷却时压一层黑），点卡片再点草地就把植物种下去，
阳光会从天上掉、向日葵也会产，点一下就收。僵尸按关卡数据一波波从画面外走进来，
豌豆射手往右吐豌豆、打中扣血、僵尸倒下；僵尸走到植物跟前会停下来啃；
四波清完判胜，让僵尸走到草坪左边判负。Esc 回选关。

### 逻辑层现在长这样

- `BattleEntity` 里放了**血量**（植物僵尸共用），血掉光走 `OnHpDepleted`，
  子类各自决定"倒下之后怎样"。
- `Plant` 有状态机（待机 / 攻击 / 倒下 / 没了）；`AttackDirector` 是**唯一**
  能改"该不该攻击"的地方，判定用**僵尸的身体中心**，和画面同一把尺子。
- `BattleField` 按**路**管植物和僵尸，`BeginBatch` / `EndBatch` 是那把"锁"——
  一步之内的通知合并，每条路最多刷一次。
- `Projectile` 自己飞，出膛锁一次目标，目标没了才重新锁一次，再没有就飞到保底位置消失。
- `GameManager` 跑**固定步长**（1/60），一帧最多补 5 步。阳光、卡片冷却、胜负都在它这儿。

### 内容

4 僵尸（普通 / 路障 / 旗帜 / 铁桶）、4 植物（向日葵 / 豌豆射手 / 土豆地雷 / 坚果）。
每种单位一个文件夹：`resource/data/plants/<id>/plant.json` + `atlas.png`，
僵尸同理。图集是**一行一个动作、横向 24 帧**，配置里写 `"walk": 0` 这样的行号。

防线（路障 370、铁桶 1100）挂在受伤链上，优先级更高，没碎之前 `return false`
把后面的扣血堵住。

### 还没做

- **选卡界面**。现在是"按存档自动填满种子包"，还没有让玩家挑的界面。
  规则要按用户说的：已解锁植物数不满种子包上限时**直接开打**，不弹这个界面。
- **旗帜僵尸的"一大波必刷一只"**。数据里的 `flag: true` 已经记着了，
  但出怪器还没用它。
- **望远镜预览僵尸**。
- **对象池**（按种类，组件入池时统一挂好，容量随难度涨）。
- **种群加成**（豌豆附近概率连射之类），先留了 `population` 字段。
- **割草机**——现在僵尸走到草坪左边直接判负。
- 向日葵的**瓜子**（每 30 秒在本格放一颗 1000 血的瓜子）还没做，
  需要在 `Tile` 上加第二个占位（防守位），僵尸啃的时候优先啃它。

---

## 9. 怎么导出 exe

### 前提：要 .NET 10 SDK

Godot 的 `DotNetFinder` 只认**和编辑器运行时同主版本**的 SDK：
`Environment.Version` 是 10.0.9，所以它要 10.x 的 SDK，9.x 不行。
（装了 .NET 10 的**运行时**没用，SDK 和运行时是两回事。）

项目下已经放了一份便携的：`D:\pvz\.tools\dotnet10`（`.gitignore` 里忽略了）。
运行导出前把它的路径放到 PATH 最前面就行：

```powershell
$env:PATH = 'D:\pvz\.tools\dotnet10;' + $env:PATH
$env:DOTNET_ROOT = 'D:\pvz\.tools\dotnet10'
$env:NUGET_PACKAGES = 'C:\Users\24807\.nuget\packages'
```

（`NUGET_PACKAGES` 是因为这台机器上 NuGet 会把全局包目录解析成相对路径。）

### 导出

```powershell
New-Item -ItemType Directory -Force -Path 'D:\pvz\build' | Out-Null
& 'D:\Lya\bin\梨娅CSharp.exe' --headless --path 'D:\pvz' `
  --export-release 'Windows Desktop' 'D:\pvz\build\pvz.exe'
```

**`build` 目录必须先建好**，Godot 不会自己建，否则报"给定的导出路径不存在"。

产物两个：

| 文件 | 说明 |
|---|---|
| `build/pvz.exe` | 模板 + 内嵌的 pck（69.6 MB） |
| `build/data_pvz_windows_x86_64/` | .NET 自包含运行时 + `pvz.dll`（187 个文件） |

**两个都要带着**，少一个跑不起来。

### 注意

- 导出的 release 模板**禁用了命令行场景覆盖**，不能 `pvz.exe <场景路径>`，只能进主场景。
- 换过主场景、或者改了 C# 代码，都要重新导一次。
- 导出用的是自己编译引擎的模板，在 `%APPDATA%\Liya\export_templates\4.7.1.stable\`。

### 无头模式测不了鼠标输入

`--headless` 用的假 DisplayServer 会把鼠标坐标放大（往 (74,80) 塞点击，收到的
是 (2220,2400)），所以**验证"点卡片 → 点草地"这类输入必须开真实窗口**：

```powershell
& 'D:\Lya\bin\梨娅CSharp.exe' --path 'D:\pvz' <场景> --quit-after 120
```

配合 `Input.ParseInputEvent(...)` 往输入系统里塞事件，可以自动化整条输入链路。
