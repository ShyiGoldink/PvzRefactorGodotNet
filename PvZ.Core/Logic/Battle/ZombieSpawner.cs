using System.Collections.Generic;
using Godot;

/// <summary>
/// 出怪。整关按关卡数据里的三个阶段走：
///
/// 1. **开始阶段**：三次手写的出怪（数据写死出什么、几只），清干净一次再放下一次；
/// 2. **中间阶段**：一个个小阶段。每个小阶段先用预算算出一张僵尸清单
///    （<see cref="WavePlanner"/>），**60% 慢慢放出来、40% 作为最后的一大波一口气放完**；
///    一大波里还会必刷一只旗帜僵尸（不算进价值，可以用关卡数据里的开关关掉）。
///    这个阶段以"一大波被清干净"为终点，然后进下一个小阶段；
/// 3. **结束阶段**：数据里 `end` 那一段，现在是空的，到了就直接收工。
///
/// 谁从多远的地方进场：草坪最右边再往外 region 配的 `spawn_margin`，
/// 大到让出场点在画面外，僵尸才会"走进来"而不是凭空出现。走哪条路是随机的。
///
/// 逻辑层的东西，不依赖节点。
/// </summary>
public sealed class ZombieSpawner
{
    private enum Phase
    {
        Start,
        Middle,
        End,
        Done,
    }

    /// <summary>同一波里两只僵尸之间隔多久（秒）。</summary>
    private const float PerZombieDelay = 0.6f;

    /// <summary>开场到第一只出场之间留一点时间。</summary>
    private const float FirstDelay = 3f;

    /// <summary>中间阶段：渐进部分两只之间隔多久。</summary>
    private const float TrickleInterval = 3.2f;

    /// <summary>渐进部分放完之后，隔多久来一大波。</summary>
    private const float HugeWaveLead = 4f;

    /// <summary>一大波里两只之间几乎贴着放，看着才像"一大波"。</summary>
    private const float HugeWaveGap = 0.22f;

    /// <summary>场上还没清干净时，多久查一次。</summary>
    private const float WaitPoll = 0.25f;

    private readonly RegionConfig _region;
    private readonly TilesData _tiles;
    private readonly BattleField _field;
    private readonly List<LevelWave> _startWaves;
    private readonly List<LevelSubStage> _subStages;
    private readonly float _difficultyScale;
    private readonly bool _flagZombie;
    private readonly System.Random _random = new System.Random();
    private readonly Queue<int> _queue = new Queue<int>();

    private Phase _phase = Phase.Start;
    private int _waveIndex = -1;
    private int _subIndex = -1;
    private float _timer = FirstDelay;

    private WavePlanner.Plan _plan;
    private int _trickleLeft;
    private int _hugeLeft;
    private bool _flagSpawned;

    /// <summary>这一关一共会出多少只——给小阶段的日志和"还有多少"用。</summary>
    public int PlannedTotal { get; private set; }

    public ZombieSpawner(RegionConfig region, TilesData tiles, LevelData level, BattleField field)
    {
        _region = region;
        _tiles = tiles;
        _field = field;
        _startWaves = level?.StartWaves ?? new List<LevelWave>();
        _subStages = level?.MiddleStages ?? new List<LevelSubStage>();
        _difficultyScale = level?.DifficultyScale ?? 1f;
        _flagZombie = level?.FlagZombie ?? true;
    }

    /// <summary>要出的怪都出完了（结束阶段也走完了）。</summary>
    public bool Finished => _phase == Phase.Done;

    /// <summary>
    /// 某条车道的出场点（区域像素空间）：草坪最右边再往外 spawn_margin，
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
        switch (_phase)
        {
            case Phase.Start:
                TickStart(delta);
                break;
            case Phase.Middle:
                TickMiddle(delta);
                break;
            case Phase.End:
                EnterDone();
                break;
        }
    }

    // ---------- 开始阶段 ----------

    private void TickStart(float delta)
    {
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

        // 这一波放完了：等场上清干净再放下一波
        if (_field != null && _field.HasAnyZombie())
        {
            _timer = WaitPoll;
            return;
        }

        _waveIndex++;
        if (_waveIndex < _startWaves.Count)
        {
            FillQueue(_startWaves[_waveIndex]);
            _timer = 0f;
            GD.Print($"[出怪] 开始阶段 第 {_startWaves[_waveIndex].Index} 次，共 {_queue.Count} 只");
            return;
        }

        // 开始阶段走完，进中间阶段
        _phase = Phase.Middle;
        _subIndex = -1;
        _timer = HugeWaveLead;
        GD.Print($"[出怪] 开始阶段结束，进中间阶段（{_subStages.Count} 个小阶段）");
    }

    // ---------- 中间阶段 ----------

    private void TickMiddle(float delta)
    {
        _timer -= delta;
        if (_timer > 0f)
        {
            return;
        }

        // 还在放渐进部分
        if (_trickleLeft > 0)
        {
            _timer = TrickleInterval;
            Spawn(_plan.Trickle[_plan.Trickle.Count - _trickleLeft]);
            _trickleLeft--;
            return;
        }

        // 渐进放完了，接着放一大波（含旗帜）
        if (_hugeLeft > 0 || (_flagZombie && !_flagSpawned))
        {
            _timer = HugeWaveGap;

            if (_flagZombie && !_flagSpawned)
            {
                _flagSpawned = true;
                int flagId = ZombieLibrary.FlagZombieId();
                if (flagId != 0)
                {
                    Spawn(flagId);
                    GD.Print("[出怪] 一大波：旗帜僵尸登场");
                    return;
                }
            }

            if (_hugeLeft > 0)
            {
                Spawn(_plan.HugeWave[_plan.HugeWave.Count - _hugeLeft]);
                _hugeLeft--;
            }

            return;
        }

        // 一大波放完了：等它被清干净，这个小阶段才算结束
        if (_field != null && _field.HasAnyZombie())
        {
            _timer = WaitPoll;
            return;
        }

        _subIndex++;
        if (_subIndex >= _subStages.Count)
        {
            _phase = Phase.End;
            GD.Print("[出怪] 中间阶段结束");
            return;
        }

        BeginSubStage(_subStages[_subIndex]);
    }

    private void BeginSubStage(LevelSubStage sub)
    {
        _plan = WavePlanner.Build(sub.BaseValue, _difficultyScale, sub.ZombieTypes);
        _trickleLeft = _plan.Trickle.Count;
        _hugeLeft = _plan.HugeWave.Count;
        _flagSpawned = false;
        _timer = 0f;

        PlannedTotal += _plan.TotalCount;

        GD.Print($"[出怪] 中间阶段 第 {sub.Index} 个小阶段："
            + $"基础价值 {sub.BaseValue}×{_difficultyScale:F0}，"
            + $"渐进 {_plan.Trickle.Count} 只 + 一大波 {_plan.HugeWave.Count} 只"
            + (sub.ZombieTypes.Count > 0 ? $"（种类 {string.Join(",", sub.ZombieTypes)}）" : string.Empty));
    }

    // ---------- 结束 ----------

    private void EnterDone()
    {
        _phase = Phase.Done;
        GD.Print($"[出怪] 全部出完，一共 {PlannedTotal} 只");
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

        ZombieData data = ZombieLibrary.Get(zombieTypeId);
        Zombie zombie = EntityAssembler.BuildZombie(data, position, _tiles, lane);
        if (zombie == null)
        {
            GD.PushError($"[出怪] 装配不出编号 {zombieTypeId} 的僵尸。");
            return;
        }

        GameManager.AddZombie(zombie);
    }
}
