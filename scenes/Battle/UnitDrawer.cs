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
/// 谁先画谁后画由节点顺序决定，所以它挂在草坪和格子线后面 —— 单位压在草坪上面。
/// </summary>
public partial class UnitDrawer : Node2D
{
    /// <summary>一只单位翻到第几格了。**表现层的东西**，不进逻辑层。</summary>
    private sealed class Cursor
    {
        public string Clip = string.Empty;
        public int Frame;
        public float Elapsed;
    }

    private readonly Dictionary<Zombie, Cursor> _cursors = new Dictionary<Zombie, Cursor>();

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

        foreach (Zombie zombie in GameManager.Zombies)
        {
            DrawZombie(zombie);
        }
    }

    /// <summary>
    /// 把每只单位的帧号往前推。动作换了就从第一格重新开始；
    /// 走到这一行末尾就绕回第一格 —— 图集横向排开，一行正好转一圈。
    /// </summary>
    private void Advance(float delta)
    {
        IReadOnlyList<Zombie> zombies = GameManager.Zombies;

        // 场上没了的单位，游标一起扔掉
        var alive = new HashSet<Zombie>(zombies);
        var stale = new List<Zombie>();
        foreach (Zombie zombie in _cursors.Keys)
        {
            if (!alive.Contains(zombie))
            {
                stale.Add(zombie);
            }
        }

        foreach (Zombie zombie in stale)
        {
            _cursors.Remove(zombie);
        }

        foreach (Zombie zombie in zombies)
        {
            AnimationGroupComponent animation = zombie.GetComponent<AnimationGroupComponent>();
            if (animation == null || !animation.CanDraw)
            {
                continue;
            }

            if (!_cursors.TryGetValue(zombie, out Cursor cursor))
            {
                cursor = new Cursor();
                _cursors[zombie] = cursor;
            }

            if (cursor.Clip != animation.CurrentClip)
            {
                cursor.Clip = animation.CurrentClip;
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
    }

    /// <summary>
    /// 画一只僵尸：从它的图集上抠出"当前动作那一行、当前这一格"。
    /// 僵尸的位置是**嘴**（图的左边缘、竖直居中），所以图从那儿往右铺开。
    /// </summary>
    private void DrawZombie(Zombie zombie)
    {
        AnimationGroupComponent animation = zombie.GetComponent<AnimationGroupComponent>();
        if (animation == null || !animation.CanDraw)
        {
            return;
        }

        Texture2D atlas = GetAtlas(animation.Atlas);
        if (atlas == null)
        {
            return;
        }

        int frame = _cursors.TryGetValue(zombie, out Cursor cursor) ? cursor.Frame : 0;
        frame = Mathf.Clamp(frame, 0, animation.Columns - 1);

        Vector2 cell = animation.Cell;
        var source = new Rect2(frame * cell.X, animation.CurrentRow * cell.Y, cell.X, cell.Y);

        Vector2 size = cell * GameManager.Scale;
        var target = new Rect2(new Vector2(zombie.Position.X, zombie.Position.Y - size.Y * 0.5f), size);

        DrawTextureRectRegion(atlas, target, source);
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
