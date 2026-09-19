using Godot;

/// <summary>
/// 普通受伤（3001）：被打的时候扣血。
///
/// 它只是受伤链上的一环，而且是**优先级最低的那一环**：
/// 防具（3002）排在它前面，防具没碎的时候会返回 false 把它堵住。
/// 谁想再加一层（真伤、免伤），往同一个事件上加个优先级更高的处理函数就行，
/// 不用动这个类。
///
/// 植物和僵尸共用它——"会掉血"本来就是两边共有的事。
/// </summary>
public sealed class NormalDamage : EntityComponent
{
    public override int Id => 3001;
    public override string Type => "defense.hurt";
    public override string DisplayName => "普通受伤";
    public override string Description => "受到伤害时扣血；血掉光由实体自己决定接下来怎样。";

    /// <summary>
    /// 血量掉到这个比例以下就把胳膊打断（0 表示不打断）。
    /// 断手不是状态，是损伤——动画组会切到无手的那几段。
    /// </summary>
    private float _armLossAt;

    private BattleEntity _entity;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        _armLossAt = ReadFloat(parameters, "arm_loss_at", 0f);
    }

    public override void Bind(BattleEntity entity, string configId)
    {
        _entity = entity;
        if (_entity == null)
        {
            return;
        }

        _entity.Events.Register(BattleEventName.take_damage, new EventResponse(0, OnHurt));
    }

    private bool OnHurt(object arg)
    {
        if (_entity is Zombie zombie && (zombie.State == ZombieState.Die || zombie.State == ZombieState.Dead))
        {
            return true; // 已经没了，白打
        }

        if (arg is not DamageEvent hit)
        {
            return true;
        }

        _entity.Hp -= hit.Amount;
        TryLoseArm();
        return true;
    }

    private void TryLoseArm()
    {
        if (_armLossAt <= 0f || _entity is not Zombie zombie || zombie.ArmLost || _entity.MaxHp <= 0f)
        {
            return;
        }

        if (_entity.Hp / _entity.MaxHp <= _armLossAt)
        {
            zombie.LoseArm();
        }
    }
}
