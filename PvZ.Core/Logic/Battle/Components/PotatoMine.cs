using Godot;

/// <summary>
/// 土豆地雷（2003）。
///
/// 用户对这个单位的定义是："它本身不只是植物，也是子弹"——
/// 所以它是**自爆型**的：埋下去先等引信（这段时间是待机），引信好了变成待发，
/// 有僵尸贴到跟前就炸，炸完自己也没了。
///
/// 它不吐任何东西，伤害就在原地结算：结算完把自己血量清零，正常走"倒下"那一套。
/// </summary>
public sealed class PotatoMine : EntityComponent
{
    private const float DefaultArmTime = 15f;
    private const float DefaultDamage = 1800f;
    private const float DefaultTriggerRange = 90f;
    private const float DefaultBlastRange = 130f;

    public override int Id => 2003;
    public override string Type => "attack.mine";
    public override string DisplayName => "土豆地雷";
    public override string Description => "引信好了之后，僵尸靠近就自爆。";

    /// <summary>引信走完了没——动画组拿它决定播"待机"还是"待发"。</summary>
    public bool IsArmed { get; private set; }

    private float _armTime = DefaultArmTime;
    private float _damage = DefaultDamage;
    private float _triggerRange = DefaultTriggerRange;
    private float _blastRange = DefaultBlastRange;
    private float _timer;

    private Plant _plant;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        _armTime = ReadFloat(parameters, "arm_time", DefaultArmTime);
        _damage = ReadFloat(parameters, "damage", DefaultDamage);
        _triggerRange = ReadFloat(parameters, "trigger_range", DefaultTriggerRange);
        _blastRange = ReadFloat(parameters, "blast_range", DefaultBlastRange);
        _timer = _armTime;
    }

    public override void Bind(BattleEntity entity, string configId)
    {
        _plant = entity as Plant;
        if (_plant == null)
        {
            GD.PushError($"[{Type}] 只能挂在植物身上。");
            return;
        }

        _plant.Events.Register(BattleEventName.tick, new EventResponse(0, OnTick));
    }

    private bool OnTick(object arg)
    {
        if (!_plant.IsAlive || _plant.Tile == null)
        {
            return true;
        }

        float delta = (float)arg;

        if (!IsArmed)
        {
            _timer -= delta;
            if (_timer <= 0f)
            {
                IsArmed = true;
                // 引信好了：切一次表现（动画组听 hp_changed，这里借它把画面催一下）
                _plant.Events.Trigger(BattleEventName.hp_changed, 0f);
            }
            return true;
        }

        Zombie trigger = FindTrigger();
        if (trigger != null)
        {
            Explode();
        }

        return true;
    }

    /// <summary>有没有僵尸的身体中心贴到触发范围内。</summary>
    private Zombie FindTrigger()
    {
        BattleField field = GameManager.Field;
        if (field == null)
        {
            return null;
        }

        Zombie nearest = field.FindNearestZombieAhead(_plant.Tile.Row, _plant.CenterX - _triggerRange);
        if (nearest == null)
        {
            return null;
        }

        return nearest.BodyCenterX - _plant.CenterX <= _triggerRange ? nearest : null;
    }

    /// <summary>
    /// 炸：把这一路上、爆炸范围内的僵尸全打一遍，然后自己血清零。
    /// 用身体中心的距离算范围，和别的判定保持一致。
    /// </summary>
    private void Explode()
    {
        BattleField field = GameManager.Field;
        if (field == null)
        {
            return;
        }

        int lane = _plant.Tile.Row;
        foreach (Zombie zombie in field.ZombiesInLane(lane))
        {
            if (!BattleField.CanBeTargeted(zombie))
            {
                continue;
            }

            if (Mathf.Abs(zombie.BodyCenterX - _plant.CenterX) <= _blastRange)
            {
                zombie.TakeDamage(new DamageEvent(_plant, _damage));
            }
        }

        // 炸完自己也没了：走正常的血量归零那一套（会切到爆炸动画）
        _plant.Hp = 0f;
    }
}
