using System.Collections.Generic;
using Godot;

/// <summary>这一局现在是什么状态。</summary>
public enum BattleResult
{
    /// <summary>还在打。</summary>
    Running,

    /// <summary>所有波次出完、场上清干净了。</summary>
    Won,

    /// <summary>有僵尸走到草坪左边了。</summary>
    Lost,
}

/// <summary>
/// 这一局的运行状态和内部流程：开始一局 → 每帧推进 → 出怪 / 攻击 / 子弹 / 阳光 → 分出胜负。
///
/// 做成静态的（全局一份），因为同时只会开着一局。
/// 它是逻辑层的东西，**不依赖场景树**，所以整局可以在一个节点都没有的情况下跑完——
/// 表现层只是每帧来读它、照着画。
///
/// 逻辑跑**固定步长**：渲染多少帧都不影响结果，同一个输入序列跑出同一局。
/// </summary>
public static class GameManager
{
    /// <summary>草坪固定 5 行 9 列。植物和僵尸都按这套格子对位。</summary>
    public const int LawnRows = 5;
    public const int LawnColumns = 9;

    /// <summary>
    /// 美术画植物 / 僵尸时按这套"基准格子"来画。
    /// 实际大小 = 图的大小 × Scale，所以换个区域、改一下 tile_size，它们会跟着一起变。
    /// </summary>
    public static readonly Vector2 BaseTileSize = new Vector2(170f, 172f);

    /// <summary>逻辑固定步长（秒）。</summary>
    public const float FixedStep = 1f / 60f;

    /// <summary>开场给多少阳光。</summary>
    public const int StartingSun = 50;

    /// <summary>天上掉阳光的间隔（秒）。</summary>
    private const float SkySunInterval = 10f;

    /// <summary>天上第一颗阳光什么时候掉。</summary>
    private const float SkySunFirstDelay = 6f;

    /// <summary>当前这一局的缩放倍率。</summary>
    public static float Scale { get; private set; } = 1f;

    public static RegionConfig Region { get; private set; }
    public static LevelData Level { get; private set; }
    public static TilesData Tiles { get; private set; }
    public static BattleField Field { get; private set; }
    public static ZombieSpawner Spawner { get; private set; }
    public static SaveData Save { get; private set; }

    /// <summary>现在有多少阳光。</summary>
    public static int Sun { get; private set; }

    /// <summary>这一局现在什么状态。</summary>
    public static BattleResult Result { get; private set; } = BattleResult.Running;

    private static readonly List<Zombie> _zombies = new List<Zombie>();
    private static readonly List<Projectile> _projectiles = new List<Projectile>();
    private static readonly List<SunToken> _suns = new List<SunToken>();
    private static readonly List<int> _seeds = new List<int>();
    private static readonly Dictionary<int, float> _cooldowns = new Dictionary<int, float>();

    /// <summary>场上还留着的僵尸（正在倒下的也在，表现层要把它播完）。</summary>
    public static IReadOnlyList<Zombie> Zombies => _zombies;

    /// <summary>场上飞着的子弹。</summary>
    public static IReadOnlyList<Projectile> Projectiles => _projectiles;

    /// <summary>场上没被捡走的阳光。</summary>
    public static IReadOnlyList<SunToken> Suns => _suns;

    /// <summary>这一关带了哪几种植物（选卡选的，或者自动填满的）。</summary>
    public static IReadOnlyList<int> Seeds => _seeds;

    /// <summary>现在开着局没有。</summary>
    public static bool InBattle => Tiles != null;

    private static float _accumulator;
    private static float _skySunTimer;

    /// <summary>
    /// 开一局：读区域和关卡数据、按地形铺格子、算好缩放倍率、准备好出怪。
    /// 返回 false 表示数据有问题，没开起来。
    /// </summary>
    public static bool StartBattle(int regionId, int levelId, SaveData save = null)
    {
        Save = save;
        Region = RegionConfig.Load(regionId);
        Level = LevelData.Load(regionId, levelId);
        if (Region == null || Level == null)
        {
            GD.PushError($"[GameManager] 数据读不到：区域 {regionId} 第 {levelId} 关。");
            return false;
        }

        Tiles = new TilesData(Level.TerrainGrid, Region.TileOrigin, Region.TileSize);
        if (!Tiles.IsValid)
        {
            GD.PushError("[GameManager] 格子配置不成立（tile_origin / tile_size / 地形表有问题）。");
            return false;
        }

        // 植物和僵尸的大小跟着格子走
        Scale = Tiles.CellSize.X / BaseTileSize.X;

        _zombies.Clear();
        _projectiles.Clear();
        _suns.Clear();
        _cooldowns.Clear();
        _accumulator = 0f;
        _skySunTimer = SkySunFirstDelay;
        Sun = StartingSun;
        Result = BattleResult.Running;

        Field = new BattleField(Tiles);
        Spawner = new ZombieSpawner(Region, Tiles, Level, Field);

        GD.Print($"[GameManager] 开局：{Region.DisplayName} 第 {Level.Id} 关，"
            + $"格子 {Tiles.Columns}×{Tiles.Rows}，起点 {Tiles.Origin}，每格 {Tiles.CellSize}，"
            + $"倍率 {Scale:F3}，阳光 {Sun}，种子 {_seeds.Count} 种");
        return true;
    }

    /// <summary>这一关带哪几种植物上场。选卡界面选完调它；没选卡就自动填满。</summary>
    public static void SetSeeds(IEnumerable<int> plantIds)
    {
        _seeds.Clear();
        if (plantIds == null)
        {
            return;
        }

        foreach (int id in plantIds)
        {
            if (!_seeds.Contains(id))
            {
                _seeds.Add(id);
            }
        }
    }

    /// <summary>收一局，把状态清干净（换场景的时候用）。</summary>
    public static void EndBattle()
    {
        _zombies.Clear();
        _projectiles.Clear();
        _suns.Clear();
        _cooldowns.Clear();
        Field = null;
        Spawner = null;
        Tiles = null;
        Region = null;
        Level = null;
        Scale = 1f;
        Result = BattleResult.Running;
    }

    /// <summary>
    /// 每帧推进。外面给多少秒都行，里面按固定步长切成整数步走，
    /// 剩下的零头攒着下一帧继续用——这样渲染快慢不影响这一局的结果。
    /// </summary>
    public static void Tick(float delta)
    {
        if (!InBattle)
        {
            return;
        }

        _accumulator += delta;

        // 一帧最多补 5 步，免得某一帧特别长的时候一次跑几百步卡住
        int steps = 0;
        while (_accumulator >= FixedStep && steps < 5)
        {
            _accumulator -= FixedStep;
            steps++;
            Step(FixedStep);
        }
    }

    private static void Step(float delta)
    {
        if (Result != BattleResult.Running)
        {
            return;
        }

        // 整个一步里的通知都合并：这一步结束时每条路最多刷一次
        Field.BeginBatch();

        Spawner?.Tick(delta);
        TickCooldowns(delta);
        TickZombies(delta);
        TickPlants(delta);
        TickProjectiles(delta);
        TickSuns(delta);
        DropSkySun(delta);
        Reap();

        Field.EndBatch();

        CheckResult();
    }

    private static void TickZombies(float delta)
    {
        foreach (Zombie zombie in _zombies)
        {
            zombie.Tick(delta);
        }
    }

    /// <summary>卡片冷却往下走。冷却属于玩法，所以放在逻辑层，不放在界面里。</summary>
    private static void TickCooldowns(float delta)
    {
        if (_cooldowns.Count == 0)
        {
            return;
        }

        var keys = new List<int>(_cooldowns.Keys);
        foreach (int key in keys)
        {
            float left = _cooldowns[key] - delta;
            if (left <= 0f)
            {
                _cooldowns.Remove(key);
            }
            else
            {
                _cooldowns[key] = left;
            }
        }
    }

    /// <summary>植物都站在格子上，所以直接扫格子就行，不用另维护一份名单。</summary>
    private static void TickPlants(float delta)
    {
        for (int row = 0; row < Tiles.Rows; row++)
        {
            for (int column = 0; column < Tiles.Columns; column++)
            {
                Plant plant = Tiles.Get(column, row)?.Plant;
                plant?.Tick(delta);
            }
        }
    }

    private static void TickProjectiles(float delta)
    {
        foreach (Projectile projectile in _projectiles)
        {
            projectile.Tick(delta, Field);
        }
    }

    private static void TickSuns(float delta)
    {
        foreach (SunToken sun in _suns)
        {
            sun.Tick(delta);
        }
    }

    /// <summary>天上定时掉一颗阳光。落点随机挑一列。</summary>
    private static void DropSkySun(float delta)
    {
        _skySunTimer -= delta;
        if (_skySunTimer > 0f)
        {
            return;
        }

        _skySunTimer = SkySunInterval;

        var random = new System.Random();
        int column = random.Next(0, Tiles.Columns);
        int row = random.Next(0, Tiles.Rows);
        Vector2 center = Tiles.GetTileCenter(column, row);

        AddSun(new SunToken
        {
            Position = new Vector2(center.X, Tiles.Origin.Y - Tiles.CellSize.Y),
            RestY = center.Y,
        });
    }

    /// <summary>把死了的东西从场上收走。植物顺手把格子让出来。</summary>
    private static void Reap()
    {
        for (int i = _zombies.Count - 1; i >= 0; i--)
        {
            Zombie zombie = _zombies[i];
            if (zombie.State != ZombieState.Dead)
            {
                continue;
            }

            Field.RemoveZombie(zombie, zombie.Lane);
            _zombies.RemoveAt(i);
            zombie.Events.Trigger(BattleEventName.zombie_removed, zombie);
        }

        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            if (_projectiles[i].Done)
            {
                _projectiles.RemoveAt(i);
            }
        }

        for (int i = _suns.Count - 1; i >= 0; i--)
        {
            if (_suns[i].Expired)
            {
                _suns.RemoveAt(i);
            }
        }

        for (int row = 0; row < Tiles.Rows; row++)
        {
            for (int column = 0; column < Tiles.Columns; column++)
            {
                Tile tile = Tiles.Get(column, row);
                Plant plant = tile?.Plant;
                if (plant == null || plant.State != PlantState.Dead)
                {
                    continue;
                }

                tile.Take();
                Field.RemovePlant(plant, row);
                plant.Events.Trigger(BattleEventName.plant_removed, plant);
            }
        }
    }

    private static void CheckResult()
    {
        if (Result != BattleResult.Running)
        {
            return;
        }

        // 僵尸走到草坪左边 → 输了（原版这儿有割草机，先不做）
        for (int lane = 0; lane < Field.LaneCount; lane++)
        {
            if (Field.LeftmostZombieCenter(lane) < Tiles.Origin.X)
            {
                Result = BattleResult.Lost;
                GD.Print($"[GameManager] 第 {lane} 路被突破了，这一局结束。");
                return;
            }
        }

        // 该出的都出完了，场上也清干净了 → 赢了
        if (Spawner != null && Spawner.Finished && !Field.HasAnyZombie())
        {
            Result = BattleResult.Won;
            GD.Print("[GameManager] 僵尸清完了，这一关通了。");
        }
    }

    /// <summary>
    /// 把一株植物种到某一格上。阳光不够、格子被占、格子不在草坪里，都会种不上。
    /// 种成功会扣阳光，并把植物交给战场和攻击管理。
    /// </summary>
    public static bool Plant(PlantData data, int column, int row)
    {
        if (!InBattle || Result != BattleResult.Running || data == null)
        {
            return false;
        }

        if (Sun < data.Cost)
        {
            GD.Print($"[GameManager] 阳光不够：{data.DisplayName} 要 {data.Cost}，现在 {Sun}。");
            return false;
        }

        if (CooldownLeft(data.Id) > 0f)
        {
            return false;
        }

        Tile tile = Tiles.Get(column, row);
        if (tile == null || !tile.CanPlant)
        {
            return false;
        }

        Plant plant = EntityAssembler.BuildPlant(data);
        if (plant == null || !tile.Place(plant))
        {
            return false;
        }

        // 身体中心：判定和画面都用它
        plant.CenterX = Tiles.GetTileCenter(column, row).X;

        Sun -= data.Cost;
        _cooldowns[data.Id] = data.Cooldown;

        Field.BeginBatch();
        Field.AddPlant(plant, row);
        Field.EndBatch();

        GD.Print($"[GameManager] 种下 {plant.DisplayName} 在 ({column},{row})，"
            + $"阳光 {Sun}，血 {plant.Hp:F0}");
        return true;
    }

    /// <summary>把一只僵尸放到场上。</summary>
    public static Zombie AddZombie(Zombie zombie)
    {
        if (zombie != null)
        {
            _zombies.Add(zombie);
            Field?.AddZombie(zombie, zombie.Lane);
        }

        return zombie;
    }

    /// <summary>把一颗子弹放到场上。</summary>
    public static void AddProjectile(Projectile projectile)
    {
        if (projectile != null)
        {
            _projectiles.Add(projectile);
        }
    }

    /// <summary>把一颗阳光放到场上。</summary>
    public static void AddSun(SunToken sun)
    {
        if (sun != null)
        {
            _suns.Add(sun);
        }
    }

    /// <summary>
    /// 在一个点上收阳光（区域像素空间）。收到返回它值多少，没收到返回 0。
    /// 表现层把鼠标位置换算成区域像素坐标再传进来。
    /// </summary>
    public static int CollectSunAt(Vector2 regionPoint)
    {
        foreach (SunToken sun in _suns)
        {
            if (sun.HitTest(regionPoint))
            {
                int value = sun.Collect();
                Sun += value;
                return value;
            }
        }

        return 0;
    }

    /// <summary>某种植物现在种不种得起——选卡界面拿它决定卡片亮不亮。</summary>
    public static bool CanAfford(PlantData data)
    {
        return data != null && Sun >= data.Cost;
    }

    /// <summary>
    /// 直接给阳光。给关卡奖励、初始阳光配置、以及调试用——
    /// 正常玩法里阳光只能靠捡。
    /// </summary>
    public static void GrantSun(int amount)
    {
        Sun += amount;
    }

    /// <summary>这张卡片还要冷却多久；0 就是能点。</summary>
    public static float CooldownLeft(int plantId)
    {
        return _cooldowns.TryGetValue(plantId, out float left) ? Mathf.Max(0f, left) : 0f;
    }

    /// <summary>这张卡片现在能不能点：阳光够、而且不在冷却里。</summary>
    public static bool IsSeedReady(PlantData data)
    {
        return data != null && Sun >= data.Cost && CooldownLeft(data.Id) <= 0f;
    }
}
