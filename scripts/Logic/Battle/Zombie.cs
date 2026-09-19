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
    public Zombie(string id, string displayName)
        : base(id, displayName)
    {
    }

    /// <summary>
    /// 位置（区域像素空间）。这是僵尸的**嘴**——往前走、啃东西都按这个点算，
    /// 不是身体中心。这样"嘴伸进哪一格就开始啃哪一格"就是同一件事。
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>站在哪片草坪上。出怪的时候由生成方塞进来。</summary>
    public TilesData Lawn { get; set; }

    /// <summary>
    /// 倒下之后停留多久才真正消失（秒）。
    /// 留这段时间是给表现层把"倒下"那一段播完。等动画做好了，可以改成按动画长度算。
    /// </summary>
    public const float DeathDuration = 1.5f;

    /// <summary>当前状态。外面只能看，改只能走 ChangeState。</summary>
    public ZombieState State => _state;

    private ZombieState _state = ZombieState.Spawn;
    private float _deathLeft;

    /// <summary>血量上限。</summary>
    public float MaxHp { get; private set; }

    private float _hp;

    /// <summary>
    /// 当前血量。改它就会：血量掉光自动进倒下状态，并且喊一声血量变了。
    /// 想"只在受伤 / 回血的时候做点什么"的组件订 hp_changed，别每帧去比血量。
    /// </summary>
    public float Hp
    {
        get => _hp;
        set
        {
            float before = _hp;
            _hp = value;

            if (_hp <= 0f)
            {
                ChangeState(ZombieState.Die);
            }

            if (_hp != before)
            {
                Events.Trigger(BattleEventName.hp_changed, _hp - before);
            }
        }
    }

    /// <summary>
    /// 装配时用：设上限和初始血量。
    /// 不走 Hp 的 setter——那时候还没人听得见 hp_changed，也没必要报一次"满血变化"。
    /// </summary>
    public void InitHp(float maxHp)
    {
        MaxHp = maxHp;
        _hp = maxHp;
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
    /// false = 被某个处理函数挡下来了（比如无敌、护盾吃满）。
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
