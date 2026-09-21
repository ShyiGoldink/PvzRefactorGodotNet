/// <summary>
/// 逻辑层的事件名。跟闪球那边一样，事件名是定死的常量，不开放给数据自定义——
/// 所有组件都靠"现在发生的是哪个事件"决定自己要不要干活。
///
/// 植物、僵尸共用同一套名字：它们都挂在 BattleEntity 上，事件本来就是一回事。
/// </summary>
public static class BattleEventName
{
    /// <summary>
    /// 每帧一次。载荷：float（这一帧的秒数）。
    /// 谁调用 Tick 由逻辑层决定——没有场景、没有节点也能照常往前走。
    /// </summary>
    public const string tick = "TICK";

    /// <summary>
    /// 状态变了。载荷：object（新的状态，比如 ZombieState）。
    /// 通知类事件，处理函数一律返回 true。
    /// </summary>
    public const string state_changed = "STATECHANGED";

    /// <summary>
    /// 血量变了。载荷：float（**这次变了多少**：正数回血、负数掉血；当前血量读实体自己的 Hp）。
    /// 想"只在受伤 / 回血的时候做点什么"就订这个，别每帧去比血量。
    /// </summary>
    public const string hp_changed = "HPCHANGED";

    /// <summary>
    /// 受到伤害。载荷：DamageEvent。按优先级依次过一遍，处理函数返回 false 就挡住后面的
    /// （无敌、护盾、真伤这些就是这条链上的不同优先级）。
    /// </summary>
    public const string take_damage = "TAKEDAMAGE";

    /// <summary>
    /// 该换动画了。载荷：string（动画名，可能是空串）。
    /// **只有表现层会听**——逻辑层喊完就完事，自己不认识任何动画资源。
    /// </summary>
    public const string animation_changed = "ANIMATIONCHANGED";

    /// <summary>
    /// 僵尸的胳膊被打断了。载荷：null。
    /// 动画组听它去切"无手"的那几段；别的系统想借这个机会做点什么也行。
    /// </summary>
    public const string arm_lost = "ARMLOST";

    /// <summary>
    /// 防具（路障 / 铁桶）被打碎。载荷：null。
    /// 表现层可以借它播个碎裂效果，玩法上暂时没人听。
    /// </summary>
    public const string armor_broken = "ARMORBROKEN";

    /// <summary>
    /// 一株植物从场上没了（被啃掉、或者被打掉）。载荷：Plant。
    /// 给"路上还有没有东西挡着"之类的判断用。
    /// </summary>
    public const string plant_removed = "PLANTREMOVED";

    /// <summary>
    /// 一只僵尸从场上没了。载荷：Zombie。
    /// </summary>
    public const string zombie_removed = "ZOMBIEREMOVED";
}
