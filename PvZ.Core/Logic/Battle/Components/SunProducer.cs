using Godot;

/// <summary>
/// 产阳光（7001）：向日葵每隔一阵子往地上丢一颗阳光。
///
/// 它不碰"阳光值多少、点了有什么反应"——那是 GameManager 的账。
/// 它只负责"到点了，在我这儿生成一颗"。
/// </summary>
public sealed class SunProducer : EntityComponent
{
    private const float DefaultInterval = 24f;
    private const float DefaultFirstDelay = 7f;
    private const int DefaultAmount = 25;

    public override int Id => 7001;
    public override string Type => "resource.sun";
    public override string DisplayName => "产阳光";
    public override string Description => "每过一段时间在脚下生成一颗阳光。";

    private float _interval = DefaultInterval;
    private float _firstDelay = DefaultFirstDelay;
    private int _amount = DefaultAmount;
    private float _timer;

    private Plant _plant;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        _interval = ReadFloat(parameters, "interval", DefaultInterval);
        _firstDelay = ReadFloat(parameters, "first_delay", DefaultFirstDelay);
        _amount = Mathf.RoundToInt(ReadFloat(parameters, "amount", DefaultAmount));
    }

    public override void Bind(BattleEntity entity, string configId)
    {
        _plant = entity as Plant;
        if (_plant == null)
        {
            GD.PushError($"[{Type}] 只能挂在植物身上。");
            return;
        }

        // 第一颗不用等满一整个周期，不然向日葵前 20 秒跟没有一样
        _timer = _firstDelay;
        _plant.Events.Register(BattleEventName.tick, new EventResponse(0, OnTick));
    }

    private bool OnTick(object arg)
    {
        if (!_plant.IsAlive)
        {
            return true;
        }

        _timer -= (float)arg;
        if (_timer > 0f)
        {
            return true;
        }

        _timer = _interval;
        Drop();
        return true;
    }

    private void Drop()
    {
        TilesData tiles = GameManager.Tiles;
        if (tiles == null || _plant.Tile == null)
        {
            return;
        }

        Vector2 center = tiles.GetTileCenter(_plant.Tile.Column, _plant.Tile.Row);

        GameManager.AddSun(new SunToken
        {
            Value = _amount,
            // 从植物头顶往下掉，落在它脚下
            Position = new Vector2(center.X, center.Y - tiles.CellSize.Y * 0.6f),
            RestY = center.Y + tiles.CellSize.Y * 0.25f,
        });
    }
}
