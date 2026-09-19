using System.Collections.Generic;
using Godot;

/// <summary>
/// 出怪。
///
/// 现在只做一件事：把僵尸放到**某条车道最右侧、地图外面**，让它自己走进画面。
///
/// - **谁出、出几只**：直接用关卡数据里 `start` 阶段那三次出怪，这里不另配一份。
/// - **从多远的地方进**：读地图大小算出来——草坪右边缘再往右 region 配的 `spawn_margin`。
///   那个边距要大到让出场点在画面外，僵尸才会"走进来"而不是凭空出现。
/// - **走哪条路**：数据里没有，按 PvZ 的做法随机挑一行。
///
/// 节奏是一波一波来的：这一波放完、并且场上清干净了，才放下一波。
///
/// 逻辑层的东西，不依赖节点。
/// </summary>
public sealed class ZombieSpawner
{
    /// <summary>同一波里两只僵尸之间隔多久（秒）。</summary>
    private const float PerZombieDelay = 0.6f;

    /// <summary>开场到第一只出场之间留一点时间，别一进来就出怪。</summary>
    private const float FirstDelay = 1.5f;

    /// <summary>场上还没清干净时，多久查一次。</summary>
    private const float WaitPoll = 0.25f;

    private readonly RegionConfig _region;
    private readonly TilesData _tiles;
    private readonly List<LevelWave> _waves;
    private readonly System.Random _random = new System.Random();
    private readonly Queue<int> _queue = new Queue<int>();

    private int _waveIndex = -1;
    private float _timer = FirstDelay;

    public ZombieSpawner(RegionConfig region, TilesData tiles, LevelData level)
    {
        _region = region;
        _tiles = tiles;
        _waves = level?.StartWaves ?? new List<LevelWave>();
    }

    /// <summary>要出的怪都出完了。</summary>
    public bool Finished => _waveIndex >= _waves.Count;

    /// <summary>
    /// 某条车道的出场点（区域像素空间）：草坪最右边再往外 `spawn_margin`，
    /// 纵向取那条车道的中线。
    /// </summary>
    public Vector2 GetSpawnPosition(int lane)
    {
        float x = _tiles.Origin.X + _tiles.Columns * _tiles.CellSize.X + _region.SpawnMargin;
        float y = _tiles.Origin.Y + (lane + 0.5f) * _tiles.CellSize.Y;
        return new Vector2(x, y);
    }

    public void Tick(float delta)
    {
        if (Finished)
        {
            return;
        }

        _timer -= delta;
        if (_timer > 0f)
        {
            return;
        }

        if (_queue.Count > 0)
        {
            _timer = PerZombieDelay;
            Spawn(_queue.Dequeue());
            return;
        }

        // 这一波已经放完了：等场上清干净再放下一波
        if (GameManager.Zombies.Count > 0)
        {
            _timer = WaitPoll;
            return;
        }

        _waveIndex++;
        if (_waveIndex >= _waves.Count)
        {
            GD.Print("[出怪] 全部出完了");
            return;
        }

        FillQueue(_waves[_waveIndex]);
        _timer = 0f;
        GD.Print($"[出怪] 第 {_waves[_waveIndex].Index} 波，共 {_queue.Count} 只");
    }

    /// <summary>把一波摊平成一串僵尸编号，按顺序一只一只放出去。</summary>
    private void FillQueue(LevelWave wave)
    {
        foreach (LevelZombieEntry entry in wave.Zombies)
        {
            for (int i = 0; i < entry.Count; i++)
            {
                _queue.Enqueue(entry.TypeId);
            }
        }
    }

    private void Spawn(int zombieTypeId)
    {
        int lane = _random.Next(0, _tiles.Rows);
        Vector2 position = GetSpawnPosition(lane);

        ZombieData data = ZombieData.Load(zombieTypeId);
        Zombie zombie = EntityAssembler.BuildZombie(data, position, _tiles);
        if (zombie == null)
        {
            GD.PushError($"[出怪] 装配不出编号 {zombieTypeId} 的僵尸。");
            return;
        }

        GameManager.AddZombie(zombie);

        float lawnRight = _tiles.Origin.X + _tiles.Columns * _tiles.CellSize.X;
        GD.Print($"[出怪] 僵尸 {zombieTypeId} 走第 {lane} 路，出场点 x={position.X:F0}"
            + $"（草坪右边缘 {lawnRight:F0}，再往外 {_region.SpawnMargin:F0}）");
    }
}
