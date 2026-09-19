using Godot;

/// <summary>
/// 普通攻击（2001）：僵尸啃植物。只在"啃"状态里干活。
///
/// 前面那格没植物了就回去走路；有的话按间隔一口一口咬。
///
/// 注意：植物的血量还没做，所以咬下去只是把 take_damage 事件**发给那株植物**，
/// 植物那边现在没人接。等做植物血量组件的时候，它会自己去订这个事件，这边一行都不用改。
/// </summary>
public sealed class NormalAttack : EntityComponent
{
    private const float DefaultDamage = 25f;
    private const float DefaultInterval = 1f;

    public override int Id => 2001;
    public override string Type => "attack.eat";
    public override string DisplayName => "普通啃食";
    public override string Description => "在啃状态里按间隔咬前面那株植物。";

    private float _damage = DefaultDamage;
    private float _interval = DefaultInterval;
    private float _cooldown;
    private Zombie _zombie;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        _damage = ReadFloat(parameters, "damage", DefaultDamage);
        _interval = ReadFloat(parameters, "interval", DefaultInterval);
    }

    public override void Bind(BattleEntity entity, string configId)
    {
        _zombie = entity as Zombie;
        if (_zombie == null)
        {
            GD.PushError($"[{Type}] 只能挂在僵尸身上。");
            return;
        }

        _zombie.Events.Register(BattleEventName.tick, new EventResponse(0, OnTick));
    }

    private bool OnTick(object arg)
    {
        if (_zombie.State != ZombieState.Eat)
        {
            _cooldown = 0f; // 下次一开啃就立刻咬第一口
            return true;
        }

        Plant target = FindPlantAhead();
        if (target == null)
        {
            // 嘴前面的植物没了（被铲了、被打掉了），回去走路
            _zombie.ChangeState(ZombieState.Walk);
            return true;
        }

        _cooldown -= (float)arg;
        if (_cooldown > 0f)
        {
            return true;
        }

        _cooldown = _interval;
        target.Events.Trigger(BattleEventName.take_damage, new DamageEvent(_zombie, _damage));
        return true;
    }

    /// <summary>嘴巴底下那一格里的植物。</summary>
    private Plant FindPlantAhead()
    {
        TilesData lawn = _zombie.Lawn;
        if (lawn == null || !TilePicker.TryPickFromRegion(lawn, _zombie.Position, out int column, out int row))
        {
            return null;
        }

        return lawn.Get(column, row)?.Plant;
    }
}
