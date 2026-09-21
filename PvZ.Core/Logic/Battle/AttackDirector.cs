/// <summary>
/// 攻击管理：一条路上"植物该不该动手"由它一个人说了算。
///
/// 植物自己**不改**自己的攻击状态——它只提供一个入口（Plant.SetAttacking）给这里调。
/// 这样规则只有一份：想改"什么时候算该打"，只改这一个文件。
///
/// 判定规则（用户定的）：
/// - 用**僵尸的身体中心**比，不用嘴；
/// - 僵尸的身体中心在植物右边 → 该打；
/// - 僵尸已经走过植物（身体中心跑到左边去了）→ 不打；
/// - 但只要这条路上**还有别的**僵尸在它右边，就还是要打。
///
/// 最后一条其实已经被概括了：这里判的是"这条路上有没有任何一只僵尸的身体中心在我右边"，
/// 而不是"离我最近的那只在哪边"。
/// </summary>
public sealed class AttackDirector
{
    private readonly BattleField _field;

    public AttackDirector(BattleField field)
    {
        _field = field;
    }

    /// <summary>重新算一遍这一路上所有植物该不该动手。</summary>
    public void RefreshLane(int lane)
    {
        foreach (Plant plant in _field.PlantsInLane(lane))
        {
            if (plant == null || !plant.IsAlive)
            {
                continue;
            }

            plant.SetAttacking(_field.HasZombieAhead(lane, plant.CenterX));
        }
    }

    /// <summary>某株植物现在该打哪只僵尸：离它最近的、身体中心在它右边的那只。</summary>
    public Zombie TargetFor(Plant plant, int lane)
    {
        return plant == null ? null : _field.FindNearestZombieAhead(lane, plant.CenterX);
    }
}
