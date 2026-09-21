/// <summary>
/// 植物的基类。
///
/// 它自己不带任何"具体植物"的行为——豌豆射手、向日葵这些不是一个一个的子类，
/// 而是 EntityAssembler 按配置把若干 EntityComponent 装配到它身上拼出来的。
/// 换一种植物 = 换一份配置，加一种植物基本不用写代码。
///
/// 逻辑层的东西，不是 Godot 节点：不放进场景树、没有任何动画，也能跑完一整局。
/// </summary>
public sealed class Plant : BattleEntity
{
    /// <summary>倒下之后停留多久才真正消失（秒），留给表现层播完倒下那一段。</summary>
    public const float DeathDuration = 0.7f;

    /// <summary>被咬之后，"正在被啃"这个标记再保留多久（秒）。</summary>
    private const float EatenMarkDuration = 0.4f;

    public Plant(string id, string displayName)
        : base(id, displayName)
    {
    }

    /// <summary>站在哪一格。没种下去的时候是 null，由 Tile.Place / Tile.Take 维护。</summary>
    public Tile Tile { get; internal set; }

    /// <summary>是不是已经种在草坪上了。</summary>
    public bool IsPlanted => Tile != null;

    /// <summary>
    /// 身体中心的横坐标（区域像素空间）。和僵尸一样，**判定一律按身体中心**，
    /// 这样"僵尸走过植物"和"豌豆该打到谁"用的是同一把尺子。
    /// </summary>
    public float CenterX { get; internal set; }

    /// <summary>当前状态。外面只能看。</summary>
    public PlantState State => _state;

    private PlantState _state = PlantState.Idle;
    private float _deathLeft;
    private float _eatenLeft;

    /// <summary>这个植物现在是不是"正被啃"——动画组拿它决定要不要切害怕待机。</summary>
    public bool IsBeingEaten => _eatenLeft > 0f;

    /// <summary>还有没有用：没倒下也没消失。</summary>
    public bool IsAlive => _state != PlantState.Die && _state != PlantState.Dead;

    /// <summary>血掉光 → 倒下。</summary>
    protected override void OnHpDepleted()
    {
        ChangeState(PlantState.Die);
    }

    /// <summary>切换状态的唯一入口。</summary>
    public void ChangeState(PlantState next)
    {
        if (next == _state || _state == PlantState.Dead)
        {
            return;
        }

        // 倒下之后不许再回到干活的状态
        if (_state == PlantState.Die && next != PlantState.Dead)
        {
            return;
        }

        _state = next;

        if (next == PlantState.Die)
        {
            _deathLeft = DeathDuration;
        }

        Events.Trigger(BattleEventName.state_changed, next);
    }

    /// <summary>
    /// 告诉这个植物"你该不该干活"。**只有攻击管理会调它**，植物自己不改自己的攻击状态。
    /// 正在倒下的植物不会被叫起来。
    /// </summary>
    public void SetAttacking(bool attacking)
    {
        if (_state != PlantState.Idle && _state != PlantState.Attack)
        {
            return;
        }

        ChangeState(attacking ? PlantState.Attack : PlantState.Idle);
    }

    /// <summary>
    /// 记一笔"刚被咬了"。只给动画用，跟伤害结算无关。
    /// 每咬一口刷新一次计时，所以啃得越勤它就一直亮着。
    /// </summary>
    public void MarkBeingEaten()
    {
        _eatenLeft = EatenMarkDuration;
    }

    public override void Tick(float delta)
    {
        base.Tick(delta);

        if (_eatenLeft > 0f)
        {
            _eatenLeft -= delta;
        }

        if (_state == PlantState.Die)
        {
            _deathLeft -= delta;
            if (_deathLeft <= 0f)
            {
                ChangeState(PlantState.Dead);
            }
        }
    }
}
