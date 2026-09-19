using Godot;

/// <summary>
/// 普通移动（1001）：僵尸在"走路"状态里以固定速度往左推。
/// 嘴巴进到有植物的那一格，就转成"啃"。
///
/// 移动和"该不该啃"放在同一个组件里，是因为这两件事本来就是一步判断：
/// 走到哪儿、前面有没有东西挡路。
/// </summary>
public sealed class NormalMove : EntityComponent
{
    private const float DefaultSpeed = 40f;

    public override int Id => 1001;
    public override string Type => "move.walk";
    public override string DisplayName => "普通移动";
    public override string Description => "以固定速度往左走；嘴伸进有植物的格子就转成啃。";

    private float _speed = DefaultSpeed;
    private Zombie _zombie;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        _speed = ReadFloat(parameters, "speed", DefaultSpeed);
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
        // 登场状态还没有对应的表现，先直接当作开始走路
        if (_zombie.State == ZombieState.Spawn)
        {
            _zombie.ChangeState(ZombieState.Walk);
        }

        if (_zombie.State != ZombieState.Walk)
        {
            return true;
        }

        _zombie.Position -= new Vector2(_speed * (float)arg, 0f);

        if (HasPlantAhead())
        {
            _zombie.ChangeState(ZombieState.Eat);
        }

        return true;
    }

    /// <summary>嘴巴底下那一格有没有植物。</summary>
    private bool HasPlantAhead()
    {
        TilesData lawn = _zombie.Lawn;
        if (lawn == null || !TilePicker.TryPickFromRegion(lawn, _zombie.Position, out int column, out int row))
        {
            return false; // 没有草坪，或者还没走进草坪里
        }

        Tile tile = lawn.Get(column, row);
        return tile != null && !tile.IsEmpty;
    }
}
