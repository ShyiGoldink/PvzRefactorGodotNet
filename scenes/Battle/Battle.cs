using System.Collections.Generic;
using Godot;

/// <summary>
/// 对战场景。它是**表现层**：自己不算任何玩法，只做三件事——
///
/// 1. 开场：定好这一关带哪几种植物，让 <see cref="GameManager"/> 开一局，
///    再把区域图、草坪、格子线摆到数据说的位置；
/// 2. 每帧：让 GameManager 往前推进，把胜负显示出来；
/// 3. 输入：点阳光收阳光，点草地种植物。
///
/// 坐标约定：Region 节点的原点对齐区域图的左上角、缩放为 1，
/// 所以它的局部坐标就是 region.json 里那些像素坐标。屏幕上点一下，
/// 先用 Region 的变换拉回区域局部坐标，再做后面的事。
/// </summary>
public partial class Battle : Node2D
{
    private const string LevelSelectScene = "res://scenes/LevelSelect/LevelSelect.tscn";

    /// <summary>直接运行本场景时（没有从选关点进来）默认开哪一关。</summary>
    private const int DefaultRegionId = 1;
    private const int DefaultLevelId = 1;

    /// <summary>新存档的初始状态：种子包几格、开局解锁哪几种植物。</summary>
    private const int DefaultSeedSlots = 2;
    private static readonly int[] DefaultUnlocked = { 1000001, 1000002, 1000003, 1000004 };

    private Camera2D _camera;
    private Node2D _region;
    private Sprite2D _background;
    private Sprite2D _lawn;
    private TileGridOverlay _tileGrid;
    private SeedBank _seedBank;
    private Label _message;

    public override void _Ready()
    {
        _camera = GetNode<Camera2D>("Camera");
        _region = GetNode<Node2D>("Region");
        _background = GetNode<Sprite2D>("Region/Background");
        _lawn = GetNode<Sprite2D>("Region/Lawn");
        _tileGrid = GetNode<TileGridOverlay>("Region/TileGrid");
        _seedBank = GetNode<SeedBank>("UI/SeedBank");
        _message = GetNode<Label>("UI/Message");

        int regionId = RunContext.SelectedRegionId > 0 ? RunContext.SelectedRegionId : DefaultRegionId;
        int levelId = RunContext.SelectedLevelId > 0 ? RunContext.SelectedLevelId : DefaultLevelId;

        SaveData save = SaveData.Load(DefaultSeedSlots, DefaultUnlocked);
        GameManager.SetSeeds(ResolveSeeds(save));

        if (!GameManager.StartBattle(regionId, levelId, save))
        {
            return;
        }

        PlaceBackground();
        PlaceLawn();
        _message.Visible = false;
    }

    /// <summary>
    /// 这一关带哪几种植物：选卡界面选过就用它，没选过就按存档自动填满。
    /// （"已解锁的装不满种子包就直接开打"那条规则由选卡界面那边判断。）
    /// </summary>
    private static List<int> ResolveSeeds(SaveData save)
    {
        if (RunContext.SelectedSeeds != null && RunContext.SelectedSeeds.Count > 0)
        {
            return new List<int>(RunContext.SelectedSeeds);
        }

        var auto = new List<int>();
        foreach (PlantData data in PlantLibrary.UnlockedSeeds(save))
        {
            if (auto.Count >= save.MaxSeedSlots)
            {
                break;
            }

            auto.Add(data.Id);
        }

        GD.Print($"[Battle] 没有选卡，自动带 {auto.Count} 种植物上场。");
        return auto;
    }

    /// <summary>
    /// 表现层每帧做的事：先让逻辑往前走，再把胜负显示出来。
    /// 顺序不能反——先画后算的话，画面永远慢一帧。
    /// </summary>
    public override void _Process(double delta)
    {
        if (!GameManager.InBattle)
        {
            return;
        }

        GameManager.Tick((float)delta);
        UpdateMessage();
    }

    public override void _ExitTree()
    {
        // 离开对战场景就把这一局收掉，免得下次进来还带着上次的僵尸
        GameManager.EndBattle();
        RunContext.SelectedSeeds = null;
    }

    private void UpdateMessage()
    {
        switch (GameManager.Result)
        {
            case BattleResult.Won:
                _message.Visible = true;
                _message.Text = "这一关通了！\n按 Esc 回选关";
                break;
            case BattleResult.Lost:
                _message.Visible = true;
                _message.Text = "僵尸进屋了……\n按 Esc 回选关重来";
                break;
            default:
                _message.Visible = false;
                break;
        }
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

    /// <summary>屏幕像素 → 区域局部像素。拉伸、相机、节点变换一次算完。</summary>
    private Vector2 ToRegionPoint(Vector2 screenPosition)
    {
        return _region.GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetTree().ChangeSceneToFile(LevelSelectScene);
            return;
        }

        if (GameManager.Result != BattleResult.Running)
        {
            return;
        }

        if (@event is not InputEventMouseButton mouse
            || !mouse.Pressed
            || mouse.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        Vector2 point = ToRegionPoint(mouse.Position);

        // 先看是不是在收阳光——阳光压在草地上，优先它
        if (GameManager.CollectSunAt(point) > 0)
        {
            return;
        }

        // 再看是不是在种植物
        int selected = _seedBank.SelectedPlantId;
        if (selected == 0)
        {
            return;
        }

        if (!TilePicker.TryPickFromRegion(GameManager.Tiles, point, out int column, out int row))
        {
            return;
        }

        if (GameManager.Plant(PlantLibrary.Get(selected), column, row))
        {
            // 种下去了就取消选中，逼玩家每次重新点卡（原版就是这样）
            _seedBank.ClearSelection();
        }
    }
}
