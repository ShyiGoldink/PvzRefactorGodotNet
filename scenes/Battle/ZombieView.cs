using Godot;

/// <summary>
/// 一只僵尸的表现：按逻辑那边"现在该播哪一段"翻帧。
///
/// 它**只读不写**——播哪一段由逻辑层的动画组组件决定，这里只负责把当前那几张图翻出来、
/// 把位置抄过来。僵尸走没走、啃没啃、死没死，全是逻辑层说了算。
/// </summary>
public partial class ZombieView : Node2D
{
    private Zombie _zombie;
    private AnimationGroupComponent _animation;
    private Sprite2D _sprite;
    private string _clip = string.Empty;
    private int _frame;
    private float _elapsed;

    /// <summary>把一只僵尸交给它显示。挂成 Region 的子节点，坐标就是区域像素空间。</summary>
    public void Setup(Zombie zombie)
    {
        _zombie = zombie;
        _animation = zombie.GetComponent("look.zombie.normal") as AnimationGroupComponent;

        _sprite = new Sprite2D { Centered = false };
        AddChild(_sprite);

        // 植物和僵尸的大小跟着格子走
        Scale = new Vector2(GameManager.Scale, GameManager.Scale);

        _clip = _animation?.CurrentClip ?? string.Empty;
        ApplyFrame();
        Position = zombie.Position;
    }

    public override void _Process(double delta)
    {
        if (_zombie == null || _animation == null)
        {
            return;
        }

        Position = _zombie.Position;

        if (_animation.CurrentClip != _clip)
        {
            _clip = _animation.CurrentClip;
            _frame = 0;
            _elapsed = 0f;
            ApplyFrame();
        }

        string[] frames = _animation.CurrentFrames;
        if (frames.Length <= 1)
        {
            return;
        }

        float step = 1f / Mathf.Max(0.01f, _animation.FramesPerSecond);
        _elapsed += (float)delta;
        while (_elapsed >= step)
        {
            _elapsed -= step;
            _frame = (_frame + 1) % frames.Length;
            ApplyFrame();
        }
    }

    /// <summary>
    /// 换成当前这一帧的图，并对齐锚点：
    /// 僵尸的 Position 是"嘴"，在图的左边缘中间——所以整个图往右画，左边缘贴着嘴。
    /// </summary>
    private void ApplyFrame()
    {
        string[] frames = _animation?.CurrentFrames;
        if (_sprite == null || frames == null || frames.Length == 0)
        {
            return;
        }

        string path = frames[Mathf.Clamp(_frame, 0, frames.Length - 1)];
        _sprite.Texture = GD.Load<Texture2D>(path);

        if (_sprite.Texture != null)
        {
            _sprite.Position = new Vector2(0f, -_sprite.Texture.GetHeight() * 0.5f);
        }
    }
}
