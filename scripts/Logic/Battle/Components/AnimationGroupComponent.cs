using System.Collections.Generic;
using Godot;

/// <summary>
/// 动画组组件的基类。
///
/// 一个单位**一张图集**：横向是帧、纵向是动作，**一行一个动作**。所以它要管的就是
/// "用哪张图、一格多大、一行几帧、现在该播哪一行"这四件事。
///
/// 它**不加载、不播放、不碰任何渲染**：表现层来问它"现在该播哪一行、图在哪"，
/// 自己去图上抠那一格画出来。这样逻辑层不放进场景也能跑完一整局。
///
/// 每个植物、每种僵尸的动画组都不一样（状态不一样、动作也不一样），所以各写各的子类；
/// 图集地址和行号本身从配置里读，换资源不用改代码。
/// </summary>
public abstract class AnimationGroupComponent : EntityComponent
{
    /// <summary>配置里写图集地址的键。</summary>
    public const string AtlasKey = "atlas";

    /// <summary>配置里写"一格多大"的键，写成 [宽, 高]。</summary>
    public const string CellKey = "cell";

    /// <summary>配置里写"一行几帧"的键。</summary>
    public const string ColumnsKey = "columns";

    /// <summary>
    /// 配置里写给帧率用的键。
    /// **除了这四个键，其余每个键都是一个动作**，值是这个动作在图集上的行号。
    /// </summary>
    public const string FpsKey = "fps";

    private const float DefaultFps = 8f;

    /// <summary>一行几帧没配时的兜底值，也是美术画图集时的约定。</summary>
    private const int DefaultColumns = 24;

    /// <summary>图集里一行几个格子。约定一行一个动作、横向排开。</summary>
    public int Columns { get; private set; } = DefaultColumns;

    /// <summary>格子的像素大小，图集按这个尺寸切。</summary>
    public Vector2 Cell { get; private set; } = Vector2.Zero;

    /// <summary>这个单位的全部动作都装在下面这一张图上。</summary>
    public string Atlas { get; private set; } = string.Empty;

    /// <summary>图集上的格子每秒翻几个。</summary>
    public float FramesPerSecond { get; private set; } = DefaultFps;

    /// <summary>当前该播哪个动作。没有就是空串。</summary>
    public string CurrentClip { get; private set; } = string.Empty;

    /// <summary>当前动作在图集上是第几行；没在播、或者这个动作没配过，就是 -1。</summary>
    public int CurrentRow => RowOf(CurrentClip);

    /// <summary>现在的配置够不够画：图、格子、行号都得有。表现层拿它决定要不要动手。</summary>
    public bool CanDraw =>
        !string.IsNullOrEmpty(Atlas) && Cell.X > 0f && Cell.Y > 0f && Columns > 0 && CurrentRow >= 0;

    /// <summary>动作名 → 图集上的行号。</summary>
    private readonly Dictionary<string, int> _rows = new Dictionary<string, int>();

    /// <summary>挂在哪个实体上。子类在 OnBind 里用。</summary>
    protected BattleEntity Host { get; private set; }

    /// <summary>这个动作在图集上第几行；没配过返回 -1。</summary>
    public int RowOf(string clipName)
    {
        return !string.IsNullOrEmpty(clipName) && _rows.TryGetValue(clipName, out int row) ? row : -1;
    }

    /// <summary>这个动作配过没有。子类挑动画时用它做兜底：没有的动作别硬播。</summary>
    public bool HasClip(string clipName)
    {
        return RowOf(clipName) >= 0;
    }

    /// <summary>
    /// 动画表从配置里读进来：`atlas` / `cell` / `columns` / `fps` 是这本动画自己的参数，
    /// **其余每个键都是一个动作**，值是这个动作在图集上的行号（第一行是 0）。
    /// </summary>
    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        _rows.Clear();
        Atlas = string.Empty;
        Cell = Vector2.Zero;
        Columns = DefaultColumns;
        FramesPerSecond = DefaultFps;

        if (parameters == null)
        {
            return;
        }

        foreach (Variant key in parameters.Keys)
        {
            string name = key.AsString();

            if (name == AtlasKey)
            {
                Atlas = parameters[key].AsString();
                continue;
            }

            if (name == CellKey)
            {
                Cell = ReadCell(parameters[key]);
                continue;
            }

            if (name == ColumnsKey)
            {
                Columns = Mathf.Max(1, ReadRow(parameters[key], DefaultColumns));
                continue;
            }

            if (name == FpsKey)
            {
                FramesPerSecond = ReadFloat(parameters, FpsKey, DefaultFps);
                continue;
            }

            // 剩下的键全是动作名、值是行号。
            // 写成数组或者字符串是旧约定（一帧一个文件），这里直接报错挡住，
            // 免得"改到一半的配置"悄悄按第 0 行播出去。
            int row = ReadRow(parameters[key], int.MinValue);
            if (row == int.MinValue)
            {
                GD.PushError($"[动画组] 动作 {name} 的值应该是图集上的行号（整数），"
                    + $"现在写的是 {parameters[key].VariantType}。");
                continue;
            }

            _rows[name] = row;
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
    /// 读一个"行号"式的整数。读不出来返回 fallback。
    ///
    /// JSON 里的整数到了这边一律是 Float（Godot 的 JSON 解析就这么给的），
    /// 所以整数和浮点都得认；后面转 protobuf 时给的是真整数，也照样过。
    /// </summary>
    private static int ReadRow(Variant value, int fallback)
    {
        switch (value.VariantType)
        {
            case Variant.Type.Int:
                return (int)value.AsInt64();
            case Variant.Type.Float:
                return Mathf.RoundToInt((float)value.AsDouble());
            default:
                return fallback;
        }
    }

    /// <summary>把配置里的 [宽, 高] 读成 Vector2；写歪了报错并返回零，CanDraw 会挡住。</summary>
    private static Vector2 ReadCell(Variant value)
    {
        if (value.VariantType != Variant.Type.Array)
        {
            GD.PushError("[动画组] cell 应该写成 [宽, 高]。");
            return Vector2.Zero;
        }

        Godot.Collections.Array array = value.AsGodotArray();
        if (array.Count < 2)
        {
            GD.PushError("[动画组] cell 应该写成 [宽, 高] 两个数。");
            return Vector2.Zero;
        }

        return new Vector2((float)array[0].AsDouble(), (float)array[1].AsDouble());
    }
}
