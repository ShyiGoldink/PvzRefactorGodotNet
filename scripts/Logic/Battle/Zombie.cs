using Godot;

/// <summary>
/// 僵尸的基类。
///
/// 跟植物一样：它自己不带任何"具体僵尸"的行为。普通僵尸、路障僵尸这些不是一个一个的子类，
/// 而是 EntityAssembler 按配置把若干 EntityComponent 装配上去拼出来的。
///
/// 逻辑层的东西，不是 Godot 节点。位置在"区域像素空间"里，和草坪共用一套坐标，
/// 所以 TilePicker 可以直接拿它算出"现在站在哪一格"。
/// </summary>
public sealed class Zombie : BattleEntity
{
    /// <summary>倒下之后停留多久才真正消失（秒），留给表现层播完倒下那一段。</summary>
    public const float DeathDuration = 1.5f;

    public Zombie(string id, string displayName)
        : base(id, displayName)
    {
    }

    /// <summary>
    /// 位置（区域像素空间）。这是僵尸的**嘴**——往前走、啃东西都按这个点算。
    /// 判定用的"身体中心"在 <see cref="BodyCenterX"/>。
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>身体的宽度（像素）。装配时按图集格子填，用来从嘴推出身体中心。</summary>
    public float BodyWidth { get; set; } = 140f;

    /// <summary>
    /// 身体中心的横坐标。**攻击判定一律用它**，不用嘴——
    /// 不然僵尸刚把嘴伸到植物边上就算"已经走过去了"，判定会偏早半个身位。
    /// </summary>
    public float BodyCenterX => Position.X + BodyWidth * 0.5f;

    /// <summary>站在哪片草坪上。出怪的时候由生成方塞进来。</summary>
    public TilesData Lawn { get; set; }

    /// <summary>在哪一路。只在横向移动，所以定下来就不变了。</summary>
    public int Lane { get; set; }

    /// <summary>当前状态。外面只能看，改只能走 ChangeState。</summary>
    public ZombieState State => _state;

    private ZombieState _state = ZombieState.Spawn;
    private float _deathLeft;

    /// <summary>
    /// 胳膊还在不在。掉了之后动画组会切到"无手"的那几段。
    /// 它不是状态，是一种损伤：掉了胳膊照样走路、照样啃。
    /// </summary>
    public bool ArmLost { get; private set; }

    /// <summary>血掉光 → 开始倒下。</summary>
    protected override void OnHpDepleted()
    {
        ChangeState(ZombieState.Die);
    }

    /// <summary>把胳膊打断。掉过一次就不会再触发第二次。</summary>
    public void LoseArm()
    {
        if (ArmLost || _state == ZombieState.Dead)
        {
            return;
        }

        ArmLost = true;
        Events.Trigger(BattleEventName.arm_lost, null);
    }

    /// <summary>切换状态的唯一入口：改状态 + 发事件，所有切换都得走这里。</summary>
    public void ChangeState(ZombieState next)
    {
        if (next == _state)
        {
            return;
        }

        // 死亡是终点：谁也别想把死人拉回别的状态
        if (_state == ZombieState.Dead)
        {
            return;
        }

        _state = next;

        if (next == ZombieState.Die)
        {
            _deathLeft = DeathDuration;
        }

        Events.Trigger(BattleEventName.state_changed, next);
    }

    /// <summary>
    /// 往前走一帧。除了让组件干活，还管"倒下多久之后真正消失"这件事——
    /// 它属于实体的生命周期，不是某个组件的行为。
    /// </summary>
    public override void Tick(float delta)
    {
        base.Tick(delta);

        if (_state == ZombieState.Die)
        {
            _deathLeft -= delta;
            if (_deathLeft <= 0f)
            {
                ChangeState(ZombieState.Dead);
            }
        }
    }

    /// <summary>
    /// 走一遍受伤链。返回 true = 走完了（该扣的血已经扣了），
    /// false = 被某个处理函数挡下来了（比如防具还没碎）。
    /// </summary>
    public bool TakeDamage(DamageEvent hit)
    {
        return hit != null && Events.Trigger(BattleEventName.take_damage, hit);
    }

    /// <summary>不带来源的写法。</summary>
    public bool TakeDamage(float damage)
    {
        return TakeDamage(new DamageEvent(null, damage));
    }
}
