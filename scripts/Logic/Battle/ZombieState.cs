/// <summary>
/// 僵尸的状态。定死，不开放给数据自定义——
/// 所有组件都靠"现在是什么状态"来决定自己该不该干活。
/// </summary>
public enum ZombieState
{
    /// <summary>刚上场，还没开始走。等有登场动画了，这一段就是播登场。</summary>
    Spawn,

    /// <summary>走路：往左推。移动组件只在这个状态里动。</summary>
    Walk,

    /// <summary>啃植物：停下来咬前面那株。攻击组件只在这个状态里咬。</summary>
    Eat,

    /// <summary>正在倒下（动画还没播完，还没从场上拿掉）。</summary>
    Die,

    /// <summary>彻底没了，终点，不再接受任何状态切换。</summary>
    Dead,
}
