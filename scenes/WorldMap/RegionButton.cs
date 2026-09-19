using Godot;

/// <summary>
/// 羊皮卷地图上的一块区域按钮。
///
/// 挂在"一张图 + Button"的节点上，点一下就把这块区域的编号交给外层的 WorldMap。
/// 以后加新区域：复制一个节点，改一下 RegionId 就行，不用动代码。
/// </summary>
[GlobalClass]
public partial class RegionButton : Button
{
    /// <summary>这块区域的编号，同时也是 resource/data 下面的文件夹名。农场是 1。</summary>
    [Export(PropertyHint.Range, "1,999,1")] public int RegionId { get; set; } = 1;

    public override void _Ready()
    {
        Pressed += OnPressed;
    }

    private void OnPressed()
    {
        WorldMap map = FindWorldMap();
        if (map == null)
        {
            GD.PushError($"[RegionButton] 外层没有 WorldMap，接不上点击：{GetPath()}");
            return;
        }

        map.OnClicked(RegionId);
    }

    private WorldMap FindWorldMap()
    {
        for (Node node = GetParent(); node != null; node = node.GetParent())
        {
            if (node is WorldMap map)
            {
                return map;
            }
        }

        return null;
    }
}
