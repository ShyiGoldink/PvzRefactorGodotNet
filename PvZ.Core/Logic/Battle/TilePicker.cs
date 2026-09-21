using Godot;

/// <summary>
/// 屏幕像素 → 落在哪一格。
///
/// 草坪是一整张图、逻辑上切成 n×m 格，所以"点在哪一格"本质就是一次坐标换算：
///
///     格子列 = (区域局部像素.x - 格子区起点.x) / 每格宽
///     格子行 = (区域局部像素.y - 格子区起点.y) / 每格高
///
/// **屏幕缩放怎么变都准**，因为换算之前先把屏幕坐标用节点自己的变换拉回"区域局部像素空间"：
/// 那一步把窗口拉伸（window/stretch）、相机、节点自身的位移缩放全都算进去了，
/// 剩下的就是一次和屏幕无关的整数除法。
///
/// 约定：调用时传的 regionNode 是"区域根节点"，它的原点和区域图的左上角对齐、缩放为 1，
/// 这样它的局部坐标就等于区域图的像素坐标，region.json 里的 tile_origin / tile_size 可以直接用。
/// </summary>
public static class TilePicker
{
    /// <summary>
    /// 从屏幕（视口）像素坐标换算。鼠标点击就传事件的 Position，或者传
    /// <c>regionNode.GetViewport().GetMousePosition()</c>。
    /// 返回 false 表示这一点不在草坪里面。
    /// </summary>
    public static bool TryPickFromScreen(
        TilesData tiles,
        CanvasItem regionNode,
        Vector2 screenPosition,
        out int column,
        out int row)
    {
        column = 0;
        row = 0;

        if (tiles == null || regionNode == null || !tiles.IsValid)
        {
            return false;
        }

        // 关键的一步：屏幕像素 → 区域局部像素。拉伸、相机、节点变换一次算完。
        Vector2 regionPosition = regionNode.GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
        return TryPickFromRegion(tiles, regionPosition, out column, out row);
    }

    /// <summary>
    /// 已经从"区域局部像素坐标"拿到的用这个（纯数学，没有屏幕什么事）。
    /// </summary>
    public static bool TryPickFromRegion(
        TilesData tiles,
        Vector2 regionPosition,
        out int column,
        out int row)
    {
        column = 0;
        row = 0;

        if (tiles == null || !tiles.IsValid)
        {
            return false;
        }

        Vector2 offset = regionPosition - tiles.Origin;
        if (offset.X < 0f || offset.Y < 0f)
        {
            return false; // 在格子区左上方
        }

        column = Mathf.FloorToInt(offset.X / tiles.CellSize.X);
        row = Mathf.FloorToInt(offset.Y / tiles.CellSize.Y);

        // 右下越界也在这里被挡掉
        return tiles.InBounds(column, row);
    }
}
