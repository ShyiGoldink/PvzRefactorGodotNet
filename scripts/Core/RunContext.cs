using Godot;

/// <summary>
/// 场景之间传参用的临时中转站。
/// 现在只有"从地图选了哪块区域"这一件事，等做存档的时候再换掉。
/// </summary>
public static class RunContext
{
    /// <summary>
    /// 羊皮卷地图上刚被点开的区域编号。
    /// 它同时也是 resource/data 下面的文件夹名——农场是 1，也就是去解析 resource/data/1。
    /// 直接运行选关场景时是 0，那边会兜底成 1。
    /// </summary>
    public static int SelectedRegionId { get; set; }

    /// <summary>
    /// 选关界面里刚点开的关卡编号。它同时也是 resource/data/&lt;区域编号&gt; 下面的文件名。
    /// 直接运行对战场景时是 0，那边会兜底成 1。
    /// </summary>
    public static int SelectedLevelId { get; set; }

    /// <summary>
    /// 羊皮卷是否已经展开过。
    /// 从选关界面回来时据此保持展开状态，不再重播展开动画。
    /// </summary>
    public static bool MapOpened { get; set; }
}
