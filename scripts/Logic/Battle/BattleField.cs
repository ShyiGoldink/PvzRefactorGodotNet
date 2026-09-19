using System.Collections.Generic;

/// <summary>
/// 战场：按**路**把植物和僵尸摆好，并在"有变化"的时候让攻击管理刷新。
///
/// 为什么按路分：这一路的植物要不要动手，只跟这条路有没有僵尸有关，
/// 跟别的路一点关系没有。分开放，刷新的时候就只用碰被通知到的那几条路。
///
/// **合批**：BeginBatch 之后的通知只把路标脏，EndBatch 才真正刷新一遍。
/// 一帧里"生成僵尸 + 打死僵尸 + 种植物"加起来可能几十条通知，
/// 合并之后每条路最多刷一次。这就是"加个锁"。
///
/// 逻辑层的东西，不依赖节点。
/// </summary>
public sealed class BattleField
{
    private readonly List<Plant>[] _plantsByLane;
    private readonly List<Zombie>[] _zombiesByLane;
    private readonly bool[] _dirty;
    private readonly AttackDirector _attack;

    private bool _batching;

    public BattleField(TilesData tiles)
    {
        int lanes = tiles != null && tiles.Rows > 0 ? tiles.Rows : GameManager.LawnRows;

        _plantsByLane = new List<Plant>[lanes];
        _zombiesByLane = new List<Zombie>[lanes];
        _dirty = new bool[lanes];
        for (int lane = 0; lane < lanes; lane++)
        {
            _plantsByLane[lane] = new List<Plant>();
            _zombiesByLane[lane] = new List<Zombie>();
        }

        _attack = new AttackDirector(this);
    }

    /// <summary>一共几条路。</summary>
    public int LaneCount => _plantsByLane.Length;

    /// <summary>攻击管理。种植物、出怪之后由它统一决定谁该动手。</summary>
    public AttackDirector Attack => _attack;

    /// <summary>这一路上的植物。</summary>
    public IReadOnlyList<Plant> PlantsInLane(int lane)
    {
        return lane >= 0 && lane < LaneCount ? _plantsByLane[lane] : EmptyPlants;
    }

    /// <summary>这一路上的僵尸。</summary>
    public IReadOnlyList<Zombie> ZombiesInLane(int lane)
    {
        return lane >= 0 && lane < LaneCount ? _zombiesByLane[lane] : EmptyZombies;
    }

    private static readonly List<Plant> EmptyPlants = new List<Plant>();
    private static readonly List<Zombie> EmptyZombies = new List<Zombie>();

    // ---------- 上场 / 下场 ----------

    public void AddPlant(Plant plant, int lane)
    {
        if (plant == null || lane < 0 || lane >= LaneCount)
        {
            return;
        }

        _plantsByLane[lane].Add(plant);
        Notify(lane);
    }

    public void RemovePlant(Plant plant, int lane)
    {
        if (plant == null || lane < 0 || lane >= LaneCount)
        {
            return;
        }

        if (_plantsByLane[lane].Remove(plant))
        {
            Notify(lane);
        }
    }

    public void AddZombie(Zombie zombie, int lane)
    {
        if (zombie == null || lane < 0 || lane >= LaneCount)
        {
            return;
        }

        _zombiesByLane[lane].Add(zombie);
        Notify(lane);
    }

    public void RemoveZombie(Zombie zombie, int lane)
    {
        if (zombie == null || lane < 0 || lane >= LaneCount)
        {
            return;
        }

        if (_zombiesByLane[lane].Remove(zombie))
        {
            Notify(lane);
        }
    }

    // ---------- 查询 ----------

    /// <summary>一只僵尸还能不能被瞄准（正在倒下的不算）。</summary>
    public static bool CanBeTargeted(Zombie zombie)
    {
        return zombie != null && zombie.State != ZombieState.Die && zombie.State != ZombieState.Dead;
    }

    /// <summary>
    /// 这一路上、身体中心在 fromX **右边**的、最近的那只僵尸。
    /// 用身体中心而不是嘴：嘴离得远，判定会偏早半个身位。
    /// </summary>
    public Zombie FindNearestZombieAhead(int lane, float fromX)
    {
        if (lane < 0 || lane >= LaneCount)
        {
            return null;
        }

        Zombie nearest = null;
        float nearestX = float.MaxValue;

        foreach (Zombie zombie in _zombiesByLane[lane])
        {
            if (!CanBeTargeted(zombie))
            {
                continue;
            }

            float center = zombie.BodyCenterX;
            if (center > fromX && center < nearestX)
            {
                nearest = zombie;
                nearestX = center;
            }
        }

        return nearest;
    }

    /// <summary>这一路上还有没有僵尸的身体中心在 fromX 右边。植物"该不该攻击"就看它。</summary>
    public bool HasZombieAhead(int lane, float fromX)
    {
        return FindNearestZombieAhead(lane, fromX) != null;
    }

    /// <summary>这条路上最靠左的僵尸身体中心；没僵尸就返回正的极大值。</summary>
    public float LeftmostZombieCenter(int lane)
    {
        if (lane < 0 || lane >= LaneCount)
        {
            return float.MaxValue;
        }

        float leftmost = float.MaxValue;
        foreach (Zombie zombie in _zombiesByLane[lane])
        {
            if (!CanBeTargeted(zombie))
            {
                continue;
            }

            float center = zombie.BodyCenterX;
            if (center < leftmost)
            {
                leftmost = center;
            }
        }

        return leftmost;
    }

    /// <summary>场上现在还有没有活着的僵尸。</summary>
    public bool HasAnyZombie()
    {
        for (int lane = 0; lane < LaneCount; lane++)
        {
            foreach (Zombie zombie in _zombiesByLane[lane])
            {
                if (CanBeTargeted(zombie))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // ---------- 合批 ----------

    /// <summary>有东西变了。合批期间只把这条目标脏，不合批就立刻刷一次。</summary>
    public void Notify(int lane)
    {
        if (lane < 0 || lane >= LaneCount)
        {
            return;
        }

        _dirty[lane] = true;
        if (!_batching)
        {
            Flush();
        }
    }

    /// <summary>开始合批：这期间的通知先攒着。</summary>
    public void BeginBatch()
    {
        _batching = true;
    }

    /// <summary>结束合批：把攒下来的路一次刷完。</summary>
    public void EndBatch()
    {
        _batching = false;
        Flush();
    }

    /// <summary>把所有标脏的路各刷一次，然后清干净。</summary>
    public void Flush()
    {
        for (int lane = 0; lane < LaneCount; lane++)
        {
            if (!_dirty[lane])
            {
                continue;
            }

            _dirty[lane] = false;
            _attack.RefreshLane(lane);
        }
    }
}
