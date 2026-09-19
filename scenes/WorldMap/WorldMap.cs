using Godot;

/// <summary>
/// 启动场景：羊皮卷世界地图。
/// 一开始是合上的（中间一条竖卷），点击后向左右展开，露出羊皮卷上的各个区域。
/// 点某个区域就进入它的选关界面。
///
/// 区域不是这里生成的——它们是场景里自己摆的"图 + Button"节点，
/// 每个按钮挂 RegionButton.cs，点一下就调用下面的 OnClicked。
/// </summary>
public partial class WorldMap : Control
{
    private const string LevelSelectScene = "res://scenes/LevelSelect/LevelSelect.tscn";

    /// <summary>羊皮卷展开后的完整尺寸，要和场景里 ScrollRoot 的大小保持一致（占位）。</summary>
    private static readonly Vector2 ScrollSize = new Vector2(1780f, 950f);

    /// <summary>合上时中间那条竖卷有多宽（占位）。</summary>
    private const float RolledWidth = 70f;

    /// <summary>卷轴左右两根竖轴的厚度（占位）。</summary>
    private const float RollerThickness = 32f;

    /// <summary>展开动画时长（秒）。</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float UnrollDuration { get; set; } = 0.8f;

    private Control _viewport;
    private ColorRect _rollerLeft;
    private ColorRect _rollerRight;
    private Label _hint;
    private Button _clickCatcher;

    private bool _isOpen;
    private bool _isUnrolling;

    public override void _Ready()
    {
        _viewport = GetNode<Control>("%Viewport");
        _rollerLeft = GetNode<ColorRect>("%RollerLeft");
        _rollerRight = GetNode<ColorRect>("%RollerRight");
        _hint = GetNode<Label>("%Hint");
        _clickCatcher = GetNode<Button>("%ClickCatcher");

        _clickCatcher.Pressed += OnScrollClicked;

        // 从选关界面返回时保持展开，不再重播一次展开动画。
        _isOpen = RunContext.MapOpened;
        _hint.Visible = !_isOpen;
        _clickCatcher.Visible = !_isOpen;
        SetUnroll(_isOpen ? 1f : 0f);
    }

    /// <summary>
    /// 地图上某块区域被点开。
    /// 参数是区域编号，同时也是 resource/data 下面的文件夹名——农场是 1，
    /// 也就是去解析 resource/data/1，小关有几关由那个目录里的内容决定。
    /// </summary>
    public void OnClicked(int regionId)
    {
        if (_isUnrolling)
        {
            return;
        }

        RunContext.SelectedRegionId = regionId;
        GetTree().ChangeSceneToFile(LevelSelectScene);
    }

    /// <summary>
    /// 设置羊皮卷的展开程度：0 = 完全合上，1 = 完全展开。
    /// 羊皮卷是横向展开的，所以改的是可视区域的宽度和横向位置，
    /// 里面的内容本身不动，文字不会被拉变形。
    /// </summary>
    private void SetUnroll(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);

        float width = Mathf.Lerp(RolledWidth, ScrollSize.X, t);
        float left = (ScrollSize.X - width) * 0.5f;

        _viewport.Position = new Vector2(left, 0f);
        _viewport.Size = new Vector2(width, ScrollSize.Y);

        _rollerLeft.Position = new Vector2(left, 0f);
        _rollerRight.Position = new Vector2(left + width - RollerThickness, 0f);
    }

    private void OnScrollClicked()
    {
        if (_isOpen || _isUnrolling)
        {
            return;
        }

        OpenScroll();
    }

    private void OpenScroll()
    {
        _isUnrolling = true;
        _hint.Visible = false;

        Tween tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenMethod(Callable.From<float>(SetUnroll), 0f, 1f, UnrollDuration);
        tween.TweenCallback(Callable.From(() =>
        {
            _isUnrolling = false;
            _isOpen = true;
            RunContext.MapOpened = true;

            // 展开之前由它吃掉点击，展开之后让位给区域按钮。
            _clickCatcher.Visible = false;
        }));
    }
}
