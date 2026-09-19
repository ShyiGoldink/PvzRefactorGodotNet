using System.Collections.Generic;
using Godot;

/// <summary>
/// 动画组组件的基类。
///
/// 它只管一件事：**"现在该播哪一段"**——一张 动画名 → 帧序列 的表，
/// 外加一个"当前动画名"。它**不加载、不播放、不碰任何渲染**：
/// 表现层来问它现在播什么、有哪几帧，自己去播。这样逻辑层不放进场景也能跑完一整局。
///
/// 每个植物、每种僵尸的动画组都不一样（状态不一样、片段也不一样），所以各写各的子类；
/// 表本身从配置里读，换资源不用改代码。
/// </summary>
public abstract class AnimationGroupComponent : EntityComponent
{
    /// <summary>配置里写给帧率用的键。其它键一律当动画名。</summary>
    public const string FpsKey = "fps";

    private const float DefaultFps = 8f;

    private readonly Dictionary<string, string[]> _clips = new Dictionary<string, string[]>();

    /// <summary>当前该播哪一段（动画名）。没有就是空串。</summary>
    public string CurrentClip { get; private set; } = string.Empty;

    /// <summary>当前这一段一共几张图，按顺序播。没配就是空数组。</summary>
    public string[] CurrentFrames => GetFrames(CurrentClip);

    /// <summary>这几张图每秒翻几张。</summary>
    public float FramesPerSecond { get; private set; } = DefaultFps;

    /// <summary>挂在哪个实体上。子类在 OnBind 里用。</summary>
    protected BattleEntity Host { get; private set; }

    /// <summary>按动画名取帧序列；没配过返回空数组。</summary>
    public string[] GetFrames(string clipName)
    {
        return !string.IsNullOrEmpty(clipName) && _clips.TryGetValue(clipName, out string[] frames)
            ? frames
            : System.Array.Empty<string>();
    }

    /// <summary>
    /// 动画表从配置里平铺读进来：每个键是一个动画名，值可以是一张图，
    /// 也可以是一串图（数组）。`fps` 那个键单独当帧率用。
    /// </summary>
    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        _clips.Clear();
        FramesPerSecond = DefaultFps;

        if (parameters == null)
        {
            return;
        }

        foreach (Variant key in parameters.Keys)
        {
            string name = key.AsString();
            if (name == FpsKey)
            {
                FramesPerSecond = ReadFloat(parameters, FpsKey, DefaultFps);
                continue;
            }

            _clips[name] = ToFrames(parameters[key]);
        }
    }

    /// <summary>
    /// 锁死装配流程：先把 Host 记下来，再交给子类接线。
    /// 这样子类不会漏掉 Host，也不会有人忘了调 base。
    /// </summary>
    public sealed override void Bind(BattleEntity entity, string configId)
    {
        Host = entity;
        OnBind(entity, configId);
    }

    /// <summary>子类在这里注册事件、决定一开始播哪一段。</summary>
    protected abstract void OnBind(BattleEntity entity, string configId);

    /// <summary>切到某一段，并喊一声让表现层跟上。名字没变就不重复喊。</summary>
    protected void Play(string clipName)
    {
        if (CurrentClip == clipName)
        {
            return;
        }

        CurrentClip = clipName;
        Host?.Events.Trigger(BattleEventName.animation_changed, CurrentClip);
    }

    /// <summary>
    /// 把配置里的一个值读成帧序列。
    /// 写一个字符串就是单张图；写数组就是一串图。空串会被丢掉。
    /// </summary>
    private static string[] ToFrames(Variant value)
    {
        if (value.VariantType == Variant.Type.String)
        {
            string single = value.AsString();
            return string.IsNullOrEmpty(single) ? System.Array.Empty<string>() : new[] { single };
        }

        if (value.VariantType != Variant.Type.Array)
        {
            return System.Array.Empty<string>();
        }

        var frames = new List<string>();
        foreach (Variant item in value.AsGodotArray())
        {
            string path = item.AsString();
            if (!string.IsNullOrEmpty(path))
            {
                frames.Add(path);
            }
        }

        return frames.ToArray();
    }
}
