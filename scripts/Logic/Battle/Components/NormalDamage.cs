using Godot;

/// <summary>
/// 普通受伤（3001）：僵尸被打的时候扣血。
///
/// 它只是受伤链上的一环。谁想挡伤害、改伤害（无敌、护盾、真伤），
/// 往同一个事件上加个优先级更高的处理函数就行，不用动这个类。
/// </summary>
public sealed class NormalDamage : EntityComponent
{
    public override int Id => 3001;
    public override string Type => "defense.hurt";
    public override string DisplayName => "普通受伤";
    public override string Description => "受到伤害时扣血，血掉光交给实体自己进倒下状态。";

    private Zombie _zombie;

    public override void Bind(BattleEntity entity, string configId)
    {
        _zombie = entity as Zombie;
        if (_zombie == null)
        {
            GD.PushError($"[{Type}] 只能挂在僵尸身上。");
            return;
        }

        _zombie.Events.Register(BattleEventName.take_damage, new EventResponse(0, OnHurt));
    }

    private bool OnHurt(object arg)
    {
        if (_zombie.State == ZombieState.Dead)
        {
            return true; // 已经没了，白打
        }

        if (arg is DamageEvent hit)
        {
            // 掉光血这件事由 Hp 的 setter 负责（进倒下状态 + 喊 hp_changed）
            _zombie.Hp -= hit.Amount;
        }

        return true;
    }
}
