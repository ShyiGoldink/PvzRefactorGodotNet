using System.Collections.Generic;
using Godot;

/// <summary>
/// 这一局的运行状态和内部流程：开始一局 → 每帧推进 → 出怪。
///
/// 做成静态的（全局一份），因为同时只会开着一局。
/// 它是逻辑层的东西，**不依赖场景树**，所以整局可以在一个节点都没有的情况下跑完——
/// 表现层只是每帧来读它、照着画。
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

    /// <summary>当前这一局的缩放倍率。</summary>
    public static float Scale { get; private set; } = 1f;

    /// <summary>当前这一局用到的东西。</summary>
    public static RegionConfig Region { get; private set; }
    public static LevelData Level { get; private set; }
    public static TilesData Tiles { get; private set; }
    public static ZombieSpawner Spawner { get; private set; }

    private static readonly List<Zombie> _zombies = new List<Zombie>();

    /// <summary>场上活着的僵尸（"活"到彻底没了为止；正在倒下的还留着，表现层要播完）。</summary>
    public static IReadOnlyList<Zombie> Zombies => _zombies;

    /// <summary>现在开着局没有。</summary>
    public static bool InBattle => Tiles != null;

    /// <summary>
    /// 开一局：读区域和关卡数据、按地形铺格子、算好缩放倍率、准备好出怪。
    /// 返回 false 表示数据有问题，没开起来。
    /// </summary>
    public static bool StartBattle(int regionId, int levelId)
    {
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
        Spawner = new ZombieSpawner(Region, Tiles, Level);

        GD.Print($"[GameManager] 开局：{Region.DisplayName} 第 {Level.Id} 关，"
            + $"格子 {Tiles.Columns}×{Tiles.Rows}，起点 {Tiles.Origin}，每格 {Tiles.CellSize}，倍率 {Scale:F3}");
        return true;
    }

    /// <summary>收一局，把状态清干净（换场景的时候用）。</summary>
    public static void EndBattle()
    {
        _zombies.Clear();
        Spawner = null;
        Tiles = null;
        Region = null;
        Level = null;
        Scale = 1f;
    }

    /// <summary>每帧推进：先出怪，再让场上每一只往前走一帧。</summary>
    public static void Tick(float delta)
    {
        if (!InBattle)
        {
            return;
        }

        Spawner?.Tick(delta);

        // 倒着的先留着（表现层还要把倒下那一段播完），彻底没了的才从名单里去掉
        for (int i = _zombies.Count - 1; i >= 0; i--)
        {
            if (_zombies[i].State == ZombieState.Dead)
            {
                _zombies.RemoveAt(i);
                continue;
            }

            _zombies[i].Tick(delta);
        }
    }

    /// <summary>把一只僵尸放到场上。</summary>
    public static Zombie AddZombie(Zombie zombie)
    {
        if (zombie != null)
        {
            _zombies.Add(zombie);
        }

        return zombie;
    }
}
