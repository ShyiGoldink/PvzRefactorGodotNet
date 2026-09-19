using Godot;

/// <summary>
/// 子弹（现在只有豌豆）。逻辑层的东西，不依赖节点。
///
/// 流程是用户定的：豌豆**自己飞**，不每帧去问"我该打到谁"。
/// 出膛时锁一次目标，之后每帧只读那只僵尸当前的位置——
/// 所以僵尸被减速了，豌豆就跟着飞得更久，不会提前在空中爆掉。
///
/// 目标没了（死了 / 被移走）才**重新锁一次**；实在没得打，
/// 就飞到保底位置自己消失。这就是"追踪为主、原目标点保底"。
/// </summary>
public sealed class Projectile
{
    /// <summary>打哪一路。</summary>
    public int Lane { get; set; }

    /// <summary>当前位置（区域像素空间），指子弹的中心。</summary>
    public Vector2 Position { get; set; }

    /// <summary>每秒往前飞多少像素。</summary>
    public float Speed { get; set; } = 600f;

    /// <summary>打中了扣多少血。</summary>
    public float Damage { get; set; } = 20f;

    /// <summary>飞到这个 x 还没打到人就自己消失（保底）。</summary>
    public float FallbackX { get; set; }

    /// <summary>锁定的目标；没有就是 null。</summary>
    public Zombie Target { get; private set; }

    /// <summary>已经打中、或者飞过头了，该从场上拿掉了。</summary>
    public bool Done { get; private set; }

    /// <summary>画它用哪本图集——子弹的帧就收在它主人的图集里。</summary>
    public AnimationGroupComponent AtlasSource { get; set; }

    /// <summary>子弹在图集上是哪个动作（豌豆射手是 "pea"）。</summary>
    public string Clip { get; set; } = string.Empty;

    private bool _relocked;

    public Projectile(Zombie target)
    {
        Target = target;
    }

    /// <summary>往前走一帧。</summary>
    public void Tick(float delta, BattleField field)
    {
        if (Done)
        {
            return;
        }

        if (!BattleField.CanBeTargeted(Target))
        {
            Target = null;
        }

        // 只在"手上没目标"的时候去问一次，问不到就不再反复问
        if (Target == null && !_relocked && field != null)
        {
            _relocked = true;
            Target = field.FindNearestZombieAhead(Lane, Position.X);
        }

        Position = new Vector2(Position.X + Speed * delta, Position.Y);

        if (BattleField.CanBeTargeted(Target))
        {
            if (Position.X >= Target.BodyCenterX)
            {
                Target.TakeDamage(new DamageEvent(null, Damage));
                Done = true;
            }
            return;
        }

        // 没目标可打：飞到保底位置就消失
        if (Position.X >= FallbackX)
        {
            Done = true;
        }
    }
}
