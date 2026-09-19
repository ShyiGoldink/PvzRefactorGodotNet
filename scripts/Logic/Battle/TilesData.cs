using System;
using Godot;

/// <summary>
/// 一整块草坪的格子集合。
///
/// **草坪是一整张图**，不是一格一格的小图拼出来的（拼出来不好看）——
/// 美术在整张图上把格子线画好，这边只按"格子区从哪个像素开始 + 每格多少像素"，
/// 在逻辑上把它切成 n×m 格。所以这个类只管逻辑格子，跟图长什么样完全无关。
///
/// 逻辑层的东西，不继承 Godot 节点。里面用的 Vector2 只是 Godot 的值类型，
/// 不牵涉场景树和渲染——逻辑层不碰的是节点，不是这几个结构体。
/// </summary>
public sealed class TilesData
{
    /// <summary>
    /// 格子区左上角在"区域像素空间"里的位置，对应 region.json 的 tile_origin。
    /// 区域像素空间以区域图的左上角为原点，向右 x 变大，向下 y 变大。
    /// </summary>
    public Vector2 Origin { get; }

    /// <summary>每一格的像素大小，对应 region.json 的 tile_size。</summary>
    public Vector2 CellSize { get; }

    /// <summary>列数（x 方向）。</summary>
    public int Columns { get; }

    /// <summary>行数（y 方向）。</summary>
    public int Rows { get; }

    /// <summary>
    /// 这套格子能不能用：行列得有，每格大小得是正的。
    /// 配置写错（比如 tile_size 是 0）的时候，换算工具会直接拒绝，而不是除以 0。
    /// </summary>
    public bool IsValid => Columns > 0 && Rows > 0 && CellSize.X > 0f && CellSize.Y > 0f;

    private readonly Tile[,] _tiles;

    /// <summary>
    /// 按地形表建出一整套格子。
    /// terrainIds 的第一维是行、第二维是列，跟关卡里的 terrain_grid 一一对应（第一行在最上面）。
    /// 格子里的数值是地形编号，直接对应 TileType。
    /// </summary>
    public TilesData(int[,] terrainIds, Vector2 origin, Vector2 cellSize)
    {
        Origin = origin;
        CellSize = cellSize;
        Rows = terrainIds?.GetLength(0) ?? 0;
        Columns = terrainIds?.GetLength(1) ?? 0;
        _tiles = new Tile[Rows, Columns];

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                int terrainId = terrainIds[row, column];
                if (!Enum.IsDefined(typeof(TileType), terrainId))
                {
                    // 编号在 region.json 的 terrain 里有、但 TileType 里没定义：当普通草地放着，别炸。
                    GD.PushWarning($"[TilesData] 地形编号 {terrainId} 没有对应的 TileType，"
                        + $"({column},{row}) 先按 Grass 处理。");
                }

                _tiles[row, column] = new Tile(column, row, (TileType)terrainId);
            }
        }
    }

    /// <summary>取一格；越界返回 null。</summary>
    public Tile Get(int column, int row)
    {
        return InBounds(column, row) ? _tiles[row, column] : null;
    }

    /// <summary>取一格；越界返回 null。</summary>
    public Tile this[int column, int row] => Get(column, row);

    /// <summary>这个坐标在不在草坪里。</summary>
    public bool InBounds(int column, int row)
    {
        return column >= 0 && column < Columns && row >= 0 && row < Rows;
    }

    /// <summary>某一格左上角的像素坐标（区域像素空间）。</summary>
    public Vector2 GetTileOrigin(int column, int row)
    {
        return Origin + new Vector2(column * CellSize.X, row * CellSize.Y);
    }

    /// <summary>某一格中心的像素坐标。摆植物、放僵尸、做表现都用它。</summary>
    public Vector2 GetTileCenter(int column, int row)
    {
        return GetTileOrigin(column, row) + CellSize * 0.5f;
    }
}
