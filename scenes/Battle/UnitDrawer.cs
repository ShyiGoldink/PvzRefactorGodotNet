using System.Collections.Generic;
using Godot;

/// <summary>
/// 战场绘制器：**一个 _Draw 把场上所有单位画完**。
///
/// 一个单位只有一张图集（横向是帧、纵向是动作），所以画一只单位就是四步：
/// 问逻辑层现在播哪个动作、从图集上抠出那一格、按格子大小缩放、贴到它的位置上。
/// 同种单位共用同一张纹理，Godot 会自动把它们的绘制合批。
///
/// 它**只读逻辑层的状态**：播哪个动作问 AnimationGroupComponent，站在哪儿问实体自己。
/// 逻辑层一个节点都没有也能照常跑，这边画的只是它的一层影子。
///
/// 阳光不是图集里的东西（它就是个圈），所以直接画圆。
/// </summary>
public partial class UnitDrawer : Node2D
{
    /// <summary>
    /// 一颗子弹画多大（像素，区域空间）。它和植物共用一张图集、格子太大，
    /// 所以按一个固定的小尺寸画——这是表现层的选择，不影响任何判定。
    /// </summary>
    private const float BulletSize = 44f;

    /// <summary>阳光的半径。</summary>
    private const float SunRadius = 34f;

    /// <summary>一只单位翻到第几格了。**表现层的东西**，不进逻辑层。</summary>
    private sealed class Cursor
    {
        public string Clip = string.Empty;
        public int Frame;
        public float Elapsed;
    }

    private readonly Dictionary<object, Cursor> _cursors = new Dictionary<object, Cursor>();

    /// <summary>图集路径 → 纹理。每一帧都从同一张图上抠，不用反复加载。</summary>
    private readonly Dictionary<string, Texture2D> _atlases = new Dictionary<string, Texture2D>();

    public override void _Process(double delta)
    {
        if (!GameManager.InBattle)
        {
            return;
        }

        Advance((float)delta);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!GameManager.InBattle)
        {
            return;
        }

        // 顺序就是绘制顺序：植物在底，僵尸压上去，子弹和阳光在最上面
        DrawPlants();
        DrawZombies();
        DrawProjectiles();
        DrawSuns();
    }

    // ---------- 帧号推进 ----------

    private void Advance(float delta)
    {
        var alive = new HashSet<object>();

        foreach (Zombie zombie in GameManager.Zombies)
        {
            alive.Add(zombie);
            AdvanceClipOnly(zombie, zombie.GetComponent<AnimationGroupComponent>(), delta);
        }

        foreach (Plant plant in AllPlants())
        {
            alive.Add(plant);
            AdvanceClipOnly(plant, plant.GetComponent<AnimationGroupComponent>(), delta);
        }

        foreach (Projectile projectile in GameManager.Projectiles)
        {
            alive.Add(projectile);
            AdvanceClipOnly(projectile, projectile.AtlasSource, delta, projectile.Clip);
        }

        // 场上没了的东西，游标一起扔掉
        var stale = new List<object>();
        foreach (object key in _cursors.Keys)
        {
            if (!alive.Contains(key))
            {
                stale.Add(key);
            }
        }

        foreach (object key in stale)
        {
            _cursors.Remove(key);
        }
    }

    /// <summary>
    /// 把某本动画的帧号往前推。动作换了就从第一格重新开始；
    /// 走到这一行末尾就绕回第一格——图集横向排开，一行正好转一圈。
    /// </summary>
    private void AdvanceClipOnly(object key, AnimationGroupComponent animation, float delta, string clipOverride = null)
    {
        string clip = clipOverride ?? animation?.CurrentClip;
        if (animation == null || !animation.HasClip(clip))
        {
            return;
        }

        if (!_cursors.TryGetValue(key, out Cursor cursor))
        {
            cursor = new Cursor();
            _cursors[key] = cursor;
        }

        if (cursor.Clip != clip)
        {
            cursor.Clip = clip;
            cursor.Frame = 0;
            cursor.Elapsed = 0f;
        }

        cursor.Elapsed += delta;

        float step = 1f / Mathf.Max(0.01f, animation.FramesPerSecond);
        while (cursor.Elapsed >= step)
        {
            cursor.Elapsed -= step;
            cursor.Frame = (cursor.Frame + 1) % animation.Columns;
        }
    }

    // ---------- 画 ----------

    private void DrawPlants()
    {
        TilesData tiles = GameManager.Tiles;
        if (tiles == null)
        {
            return;
        }

        for (int row = 0; row < tiles.Rows; row++)
        {
            for (int column = 0; column < tiles.Columns; column++)
            {
                Plant plant = tiles.Get(column, row)?.Plant;
                if (plant == null)
                {
                    continue;
                }

                Vector2 center = tiles.GetTileCenter(column, row);
                // 植物画在格子正中
                DrawUnit(plant, plant.GetComponent<AnimationGroupComponent>(), center, Vector2.Zero, null);
            }
        }
    }

    private void DrawZombies()
    {
        foreach (Zombie zombie in GameManager.Zombies)
        {
            // 僵尸的位置是**嘴**（图的左边缘、竖直居中），所以图从那儿往右铺开
            DrawUnit(zombie, zombie.GetComponent<AnimationGroupComponent>(), zombie.Position, new Vector2(0f, -0.5f), null);
        }
    }

    private void DrawProjectiles()
    {
        foreach (Projectile projectile in GameManager.Projectiles)
        {
            AnimationGroupComponent animation = projectile.AtlasSource;
            if (animation == null || !animation.HasClip(projectile.Clip))
            {
                continue;
            }

            Texture2D atlas = GetAtlas(animation.Atlas);
            if (atlas == null)
            {
                continue;
            }

            int frame = _cursors.TryGetValue(projectile, out Cursor cursor) ? cursor.Frame : 0;
            frame = Mathf.Clamp(frame, 0, animation.Columns - 1);

            Vector2 cell = animation.Cell;
            var source = new Rect2(frame * cell.X, animation.RowOf(projectile.Clip) * cell.Y, cell.X, cell.Y);

            float size = BulletSize * GameManager.Scale;
            var target = new Rect2(projectile.Position - new Vector2(size, size) * 0.5f, new Vector2(size, size));
            DrawTextureRectRegion(atlas, target, source);
        }
    }

    private void DrawSuns()
    {
        foreach (SunToken sun in GameManager.Suns)
        {
            float radius = SunRadius * GameManager.Scale;
            DrawCircle(sun.Position, radius, new Color(1f, 0.85f, 0.15f, 0.95f));
            DrawArc(sun.Position, radius, 0f, Mathf.Tau, 32, new Color(1f, 1f, 0.6f), 3f);
        }
    }

    /// <summary>
    /// 画一只单位。anchor 是"图上的哪个点对齐 position"：
    /// (0, -0.5) 表示左边缘竖直居中（僵尸的嘴），(0, 0) 表示正中（植物）。
    /// </summary>
    private void DrawUnit(object key, AnimationGroupComponent animation, Vector2 position, Vector2 anchor, string clipOverride)
    {
        string clip = clipOverride ?? animation?.CurrentClip;
        if (animation == null || !animation.CanDraw || !animation.HasClip(clip))
        {
            return;
        }

        Texture2D atlas = GetAtlas(animation.Atlas);
        if (atlas == null)
        {
            return;
        }

        int frame = _cursors.TryGetValue(key, out Cursor cursor) ? cursor.Frame : 0;
        frame = Mathf.Clamp(frame, 0, animation.Columns - 1);

        Vector2 cell = animation.Cell;
        var source = new Rect2(frame * cell.X, animation.RowOf(clip) * cell.Y, cell.X, cell.Y);

        Vector2 size = cell * GameManager.Scale;
        var target = new Rect2(position - size * anchor, size);
        DrawTextureRectRegion(atlas, target, source);
    }

    /// <summary>场上所有植物。植物站在格子上，所以直接扫格子。</summary>
    private static IEnumerable<Plant> AllPlants()
    {
        TilesData tiles = GameManager.Tiles;
        if (tiles == null)
        {
            yield break;
        }

        for (int row = 0; row < tiles.Rows; row++)
        {
            for (int column = 0; column < tiles.Columns; column++)
            {
                Plant plant = tiles.Get(column, row)?.Plant;
                if (plant != null)
                {
                    yield return plant;
                }
            }
        }
    }

    /// <summary>图集只加载一次。加载不了也记下来，免得每帧都去重试、每帧都报一次错。</summary>
    private Texture2D GetAtlas(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (_atlases.TryGetValue(path, out Texture2D cached))
        {
            return cached;
        }

        var texture = GD.Load<Texture2D>(path);
        if (texture == null)
        {
            GD.PushError($"[绘制器] 图集加载不了：{path}。新加的图片要先让编辑器导入一遍。");
        }

        _atlases[path] = texture;
        return texture;
    }
}
