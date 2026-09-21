/// <summary>
/// 草坪上的一格。
///
/// 逻辑层的东西，不是 Godot 节点：没有图、没有任何动画也能跑。
/// 一格就两件事——自己是什么地形（Type），以及这一格上站着的植物（Plant 占位）。
///
/// 一格最多站一个植物。种下去 / 拔走都必须走 Place / Take，
/// 这样"植物知道自己站在哪"和"格子知道自己被占了"永远是同步的。
/// </summary>
public sealed class Tile
{
    /// <summary>第几列，从 0 开始往右。</summary>
    public int Column { get; }

    /// <summary>第几行，从 0 开始往下。</summary>
    public int Row { get; }

    /// <summary>这一格的地形。</summary>
    public TileType Type { get; set; }

    /// <summary>
    /// 这一格上的植物占位。空格就是 null。
    /// 类型是基类 Plant——具体是豌豆射手还是向日葵，靠装配出来的组件区分，不靠子类。
    /// </summary>
    public Plant Plant { get; private set; }

    /// <summary>这一格是不是空的。</summary>
    public bool IsEmpty => Plant == null;

    /// <summary>这一格现在能不能种东西。规则以后会长（水池要荷叶之类），先只判空格。</summary>
    public bool CanPlant => IsEmpty;

    public Tile(int column, int row, TileType type = TileType.Grass)
    {
        Column = column;
        Row = row;
        Type = type;
    }

    /// <summary>
    /// 把植物种到这一格。已经被占了、或者植物是空的，就种不下去，返回 false。
    /// 种成功后植物的 Tile 会指回这一格。
    /// </summary>
    public bool Place(Plant plant)
    {
        if (plant == null || !CanPlant)
        {
            return false;
        }

        Plant = plant;
        plant.Tile = this;
        return true;
    }

    /// <summary>
    /// 把这一格上的植物拿走（被吃掉、被铲掉都走这里），返回原来的植物；空格返回 null。
    /// </summary>
    public Plant Take()
    {
        Plant plant = Plant;
        Plant = null;

        if (plant != null)
        {
            plant.Tile = null;
        }

        return plant;
    }
}
