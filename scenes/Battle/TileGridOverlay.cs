using Godot;

/// <summary>
/// 格子可视化：把逻辑上切出来的 n×m 格用细线画出来，用来检查格子切得对不对。
///
/// 这是**调试用**的东西，跟玩法无关——以后正式跑图的时候，把它隐藏掉或者直接删掉都行。
/// 线画在"区域像素空间"里，所以要挂成 Region 的子节点，跟草坪图共用一套坐标。
/// </summary>
public partial class TileGridOverlay : Node2D
{
    /// <summary>线的颜色。默认半透明红，压在绿色草坪上看得清。</summary>
    [Export] public Color LineColor { get; set; } = new Color(1f, 0.1f, 0.1f, 0.75f);

    /// <summary>线的粗细（像素）。</summary>
    [Export(PropertyHint.Range, "1,8,1")] public float LineWidth { get; set; } = 2f;

    private TilesData _tiles;

    /// <summary>把要画的格子交进来，立刻重画一次。</summary>
    public void Setup(TilesData tiles)
    {
        _tiles = tiles;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_tiles == null || !_tiles.IsValid)
        {
            return;
        }

        Vector2 origin = _tiles.Origin;
        float fullWidth = _tiles.Columns * _tiles.CellSize.X;
        float fullHeight = _tiles.Rows * _tiles.CellSize.Y;

        // 竖线：列数 + 1 根（最右边那条外框也算一根）
        for (int column = 0; column <= _tiles.Columns; column++)
        {
            float x = origin.X + column * _tiles.CellSize.X;
            DrawLine(new Vector2(x, origin.Y), new Vector2(x, origin.Y + fullHeight), LineColor, LineWidth);
        }

        // 横线：行数 + 1 根
        for (int row = 0; row <= _tiles.Rows; row++)
        {
            float y = origin.Y + row * _tiles.CellSize.Y;
            DrawLine(new Vector2(origin.X, y), new Vector2(origin.X + fullWidth, y), LineColor, LineWidth);
        }
    }
}
