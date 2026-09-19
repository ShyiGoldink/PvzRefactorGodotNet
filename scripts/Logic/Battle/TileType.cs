/// <summary>
/// 一格地形的种类。定死，不开放给数据自定义——
/// 植物的"能不能种在这"、僵尸的"能不能走这"都要靠它判断。
///
/// 现在只有农场用得上的几种，后面加地形类型就往这里加。
/// </summary>
public enum TileType
{
    /// <summary>没有地形：地图外，或者还没铺。</summary>
    None = 0,

    /// <summary>普通草地。农场那关就是这种。</summary>
    Grass = 1,

    /// <summary>水池。</summary>
    Water = 2,

    /// <summary>屋顶。</summary>
    Roof = 3,
}
