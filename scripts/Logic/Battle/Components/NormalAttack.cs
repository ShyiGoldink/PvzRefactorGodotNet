using Godot;

/// <summary>
/// 普通啃食（2001）：僵尸啃植物。只在"啃"状态里干活。
///
/// 前面那格没植物了就回去走路；有的话按间隔一口一口咬。
/// 咬的时候顺手给植物记一笔"我正在被啃"——**只给动画用**，跟伤害结算无关。
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
            // 嘴前面的植物没了（被啃光了、被炸了），回去走路
            _zombie.ChangeState(ZombieState.Walk);
            return true;
        }

        _cooldown -= (float)arg;
        if (_cooldown > 0f)
        {
            return true;
        }

        _cooldown = _interval;
        target.MarkBeingEaten();
        target.Events.Trigger(BattleEventName.take_damage, new DamageEvent(_zombie, _damage));
        return true;
    }

    /// <summary>嘴巴底下那一格里的、还活着的植物。</summary>
    private Plant FindPlantAhead()
    {
        TilesData lawn = _zombie.Lawn;
        if (lawn == null || !TilePicker.TryPickFromRegion(lawn, _zombie.Position, out int column, out int row))
        {
            return null;
        }

        Plant plant = lawn.Get(column, row)?.Plant;
        return plant != null && plant.IsAlive ? plant : null;
    }
}
