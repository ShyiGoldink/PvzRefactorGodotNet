using Godot;

/// <summary>
/// 射手（2002）：豌豆射手这种"站着往右吐东西"的植物。
///
/// 它只负责**吐**：在攻击状态里按间隔造一颗子弹出去。
/// 子弹怎么飞、打谁、在哪爆，全在 <see cref="Projectile"/> 里，
/// 跟射手没关系——所以以后要加三线射手、寒冰射手，改的都不是这里。
///
/// 冷却和状态无关，一直在走：待机的时候也在攒，这样一进攻击状态就能马上吐第一颗。
/// </summary>
public sealed class Shooter : EntityComponent
{
    private const float DefaultInterval = 1.4f;
    private const float DefaultDamage = 20f;
    private const float DefaultSpeed = 600f;
    private const string DefaultBulletClip = "pea";

    public override int Id => 2002;
    public override string Type => "attack.shooter";
    public override string DisplayName => "射手";
    public override string Description => "在攻击状态里按间隔吐子弹。";

    private float _interval = DefaultInterval;
    private float _damage = DefaultDamage;
    private float _speed = DefaultSpeed;
    private string _bulletClip = DefaultBulletClip;
    private float _cooldown;

    private Plant _plant;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        _interval = ReadFloat(parameters, "interval", DefaultInterval);
        _damage = ReadFloat(parameters, "damage", DefaultDamage);
        _speed = ReadFloat(parameters, "speed", DefaultSpeed);

        if (parameters != null && parameters.ContainsKey("bullet_clip"))
        {
            _bulletClip = parameters["bullet_clip"].AsString();
        }
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
        _cooldown -= (float)arg;

        if (_plant.State != PlantState.Attack || !_plant.IsAlive)
        {
            return true;
        }

        if (_cooldown > 0f)
        {
            return true;
        }

        _cooldown = _interval;
        Fire();
        return true;
    }

    private void Fire()
    {
        BattleField field = GameManager.Field;
        TilesData tiles = GameManager.Tiles;
        if (field == null || tiles == null || _plant.Tile == null)
        {
            return;
        }

        int lane = _plant.Tile.Row;
        Vector2 center = tiles.GetTileCenter(_plant.Tile.Column, lane);

        var projectile = new Projectile(field.FindNearestZombieAhead(lane, _plant.CenterX))
        {
            Lane = lane,
            // 从植物嘴边出来：往右一点、往上一点，看着像从嘴里吐的
            Position = new Vector2(center.X + tiles.CellSize.X * 0.22f, center.Y - tiles.CellSize.Y * 0.18f),
            Speed = _speed,
            Damage = _damage,
            // 保底：飞到草坪右边再往外一段就自己消失
            FallbackX = tiles.Origin.X + tiles.Columns * tiles.CellSize.X + 200f,
            AtlasSource = _plant.GetComponent<AnimationGroupComponent>(),
            Clip = _bulletClip,
        };

        GameManager.AddProjectile(projectile);
    }
}
