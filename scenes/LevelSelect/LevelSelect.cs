using Godot;

/// <summary>
/// 区域内的选关界面：一页 4x4 共 16 个小关，左右翻页。
///
/// 小关数量完全由数据决定：扫 resource/data/&lt;区域编号&gt;/ 下面的 json，
/// 有几个 json 就有几关，代码和场景里都不写死。
/// 翻页效果和格子样式还是占位实现。
/// </summary>
public partial class LevelSelect : Control
{
    private const string WorldMapScene = "res://scenes/WorldMap/WorldMap.tscn";
    private const string BattleScene = "res://scenes/Battle/Battle.tscn";

    /// <summary>直接运行本场景时（没有从地图点进来）默认看哪个区域。</summary>
    private const int DefaultRegionId = 1;

    /// <summary>一页有几列、几行，默认 4x4。</summary>
    [Export(PropertyHint.Range, "1,8,1")] public int Columns { get; set; } = 4;
    [Export(PropertyHint.Range, "1,8,1")] public int Rows { get; set; } = 4;

    /// <summary>翻页动画单侧的时长（秒）。</summary>
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float FlipDuration { get; set; } = 0.18f;

    private GridContainer _grid;
    private Control _pageRoot;
    private Label _title;
    private Label _pageLabel;
    private Label _status;
    private Button _prevButton;
    private Button _nextButton;

    private int _regionId;
    private Godot.Collections.Array<int> _levelIds = new Godot.Collections.Array<int>();
    private int _page;
    private int _pageCount;
    private bool _isFlipping;

    private int LevelsPerPage => Mathf.Max(1, Columns * Rows);

    public override void _Ready()
    {
        _grid = GetNode<GridContainer>("%Grid");
        _pageRoot = GetNode<Control>("%PageRoot");
        _title = GetNode<Label>("%Title");
        _pageLabel = GetNode<Label>("%PageLabel");
        _status = GetNode<Label>("%Status");
        _prevButton = GetNode<Button>("%PrevButton");
        _nextButton = GetNode<Button>("%NextButton");

        _grid.Columns = Columns;

        _prevButton.Pressed += () => GoToPage(_page - 1);
        _nextButton.Pressed += () => GoToPage(_page + 1);
        GetNode<Button>("%BackButton").Pressed += () => GetTree().ChangeSceneToFile(WorldMapScene);

        _pageRoot.Resized += UpdateFlipPivot;
        UpdateFlipPivot();

        // 区域编号由地图那边点出来；单独运行本场景时兜底成农场。
        _regionId = RunContext.SelectedRegionId > 0 ? RunContext.SelectedRegionId : DefaultRegionId;
        _levelIds = LevelCatalog.GetLevelIds(_regionId);
        _pageCount = Mathf.Max(1, Mathf.CeilToInt(_levelIds.Count / (float)LevelsPerPage));
        _page = 0;

        // 区域名也走数据：读 resource/data/<区域编号>/region.json。
        RegionConfig region = RegionConfig.Load(_regionId);
        _title.Text = region != null && !string.IsNullOrEmpty(region.DisplayName)
            ? region.DisplayName
            : $"区域 {_regionId}";

        RebuildGrid();
        UpdatePager();

        if (_levelIds.Count == 0)
        {
            _status.Text = $"这个区域还没有关卡数据：{LevelCatalog.GetRegionDirectory(_regionId)}";
        }
    }

    /// <summary>翻页时以中轴为轴心收拢，所以轴心要跟着控件大小走。</summary>
    private void UpdateFlipPivot()
    {
        _pageRoot.PivotOffset = _pageRoot.Size * 0.5f;
    }

    /// <summary>按当前页重新摆一遍小关格子。关卡本身来自数据，不在这里写死。</summary>
    private void RebuildGrid()
    {
        foreach (Node child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }

        int first = _page * LevelsPerPage;
        int last = Mathf.Min(first + LevelsPerPage, _levelIds.Count);

        for (int i = first; i < last; i++)
        {
            int levelId = _levelIds[i];

            var button = new Button
            {
                Text = levelId.ToString(),
                // 固定大小。不加 ExpandFill，所以一页只有几个小关时也不会被拉高拉宽。
                CustomMinimumSize = new Vector2(300f, 140f),
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand,
            };

            button.AddThemeFontSizeOverride("font_size", 40);
            button.Pressed += () => OnLevelPressed(levelId);

            _grid.AddChild(button);
        }
    }

    private void UpdatePager()
    {
        _pageLabel.Text = _levelIds.Count == 0
            ? "没有关卡数据"
            : $"第 {_page + 1} / {_pageCount} 页";

        bool singlePage = _pageCount <= 1;
        _prevButton.Disabled = singlePage || _page <= 0;
        _nextButton.Disabled = singlePage || _page >= _pageCount - 1;
    }

    /// <summary>
    /// 翻页：先把当前页横向收拢成一条线，换完内容再展开。
    /// 这是占位效果，以后可以换成真正的翻书动画。
    /// </summary>
    private void GoToPage(int page)
    {
        if (_isFlipping)
        {
            return;
        }

        page = Mathf.Clamp(page, 0, _pageCount - 1);
        if (page == _page)
        {
            return;
        }

        int target = page;
        _isFlipping = true;

        Tween tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_pageRoot, "scale:x", 0f, FlipDuration);
        tween.TweenCallback(Callable.From(() =>
        {
            _page = target;
            RebuildGrid();
            UpdatePager();
        }));
        tween.TweenProperty(_pageRoot, "scale:x", 1f, FlipDuration);
        tween.TweenCallback(Callable.From(() => _isFlipping = false));
    }

    private void OnLevelPressed(int levelId)
    {
        RunContext.SelectedLevelId = levelId;
        GetTree().ChangeSceneToFile(BattleScene);
    }
}
