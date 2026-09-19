using Godot;

/// <summary>
/// 对战场景。它是**表现层**：自己不算任何玩法，只做三件事——
///
/// 1. 开场：让 <see cref="GameManager"/> 开一局，然后把区域图、草坪、格子线摆到数据说的位置；
/// 2. 每帧：让 GameManager 往前推进；
/// 3. 画面：交给 <see cref="UnitDrawer"/>，它用一个 _Draw 把场上所有单位画完。
///
/// 坐标约定：Region 节点的原点对齐区域图的左上角、缩放为 1，
/// 所以它的局部坐标就是 region.json 里那些像素坐标，tile_origin / tile_size 可以直接用。
/// 相机摆在区域正中间——区域图比屏幕大，先让画面看到中段。
/// </summary>
public partial class Battle : Node2D
{
    private const string LevelSelectScene = "res://scenes/LevelSelect/LevelSelect.tscn";

    /// <summary>直接运行本场景时（没有从选关点进来）默认开哪一关。</summary>
    private const int DefaultRegionId = 1;
    private const int DefaultLevelId = 1;

    private Camera2D _camera;
    private Sprite2D _background;
    private Sprite2D _lawn;
    private TileGridOverlay _tileGrid;

    public override void _Ready()
    {
        _camera = GetNode<Camera2D>("Camera");
        _background = GetNode<Sprite2D>("Region/Background");
        _lawn = GetNode<Sprite2D>("Region/Lawn");
        _tileGrid = GetNode<TileGridOverlay>("Region/TileGrid");

        int regionId = RunContext.SelectedRegionId > 0 ? RunContext.SelectedRegionId : DefaultRegionId;
        int levelId = RunContext.SelectedLevelId > 0 ? RunContext.SelectedLevelId : DefaultLevelId;

        if (!GameManager.StartBattle(regionId, levelId))
        {
            return;
        }

        PlaceBackground();
        PlaceLawn();
    }

    /// <summary>
    /// 表现层每帧做的事：先让逻辑往前走，再重画。
    /// 顺序不能反——先画后算的话，画面永远慢一帧。
    /// 单位本身由 UnitDrawer 自己每帧重画，这里只管推逻辑。
    /// </summary>
    public override void _Process(double delta)
    {
        if (!GameManager.InBattle)
        {
            return;
        }

        GameManager.Tick((float)delta);
    }

    public override void _ExitTree()
    {
        // 离开对战场景就把这一局收掉，免得下次进来还带着上次的僵尸
        GameManager.EndBattle();
    }

    /// <summary>区域图摆在区域像素空间的原点上，相机对准整张图的正中间。</summary>
    private void PlaceBackground()
    {
        Texture2D texture = LoadTexture(GameManager.Region.Background);
        if (texture == null)
        {
            return;
        }

        _background.Texture = texture;
        _background.Position = Vector2.Zero;
        _camera.Position = texture.GetSize() * 0.5f;
    }

    /// <summary>摆草坪、把格子交给可视化工具画线。</summary>
    private void PlaceLawn()
    {
        TilesData tiles = GameManager.Tiles;
        RegionConfig region = GameManager.Region;

        int terrainId = GameManager.Level.TerrainGrid[0, 0];
        Texture2D texture = LoadTexture(region.GetTerrainTexture(terrainId));
        if (texture != null)
        {
            _lawn.Texture = texture;
            // 草坪图的左上角 = 格子区的左上角
            _lawn.Position = region.TileOrigin;
        }

        _tileGrid.Setup(tiles);
    }

    private static Texture2D LoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            GD.PushError("[Battle] 图片路径是空的。");
            return null;
        }

        var texture = GD.Load<Texture2D>(path);
        if (texture == null)
        {
            GD.PushError($"[Battle] 图片加载不了：{path}");
        }

        return texture;
    }

    /// <summary>Esc 退回选关。战斗里还没有别的操作，先留一条退路。</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetTree().ChangeSceneToFile(LevelSelectScene);
        }
    }
}
