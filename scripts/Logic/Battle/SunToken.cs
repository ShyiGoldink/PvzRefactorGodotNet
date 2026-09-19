using Godot;

/// <summary>
/// 一颗阳光。逻辑层的东西——它在哪儿、值多少、还在不在，全是数据；
/// 表现层照着画一个黄圈，玩家点到就收走。
///
/// 它有两种来路：天上掉下来的，和向日葵产出来的。两者只有"从哪儿开始掉"不一样，
/// 所以用同一个类。
/// </summary>
public sealed class SunToken
{
    /// <summary>一颗值多少阳光。</summary>
    public int Value { get; set; } = 25;

    /// <summary>当前位置（区域像素空间）。</summary>
    public Vector2 Position { get; set; }

    /// <summary>落到这个高度就停住。</summary>
    public float RestY { get; set; }

    /// <summary>往下掉的速度。</summary>
    public float FallSpeed { get; set; } = 90f;

    /// <summary>落地之后停留多久没人捡就消失（秒）。</summary>
    public float Life { get; set; } = 14f;

    /// <summary>被收走了没。</summary>
    public bool Collected { get; private set; }

    /// <summary>已经落地了（不再往下掉）。</summary>
    public bool Landed => Position.Y >= RestY;

    /// <summary>点上去能不能收到——判定半径（像素）。</summary>
    public float Radius { get; set; } = 60f;

    public void Tick(float delta)
    {
        if (Collected)
        {
            return;
        }

        if (!Landed)
        {
            float y = Position.Y + FallSpeed * delta;
            Position = new Vector2(Position.X, Mathf.Min(y, RestY));
        }
        else
        {
            Life -= delta;
        }
    }

    /// <summary>这一颗是不是没了（被收了、或者超时了）。</summary>
    public bool Expired => Collected || (Landed && Life <= 0f);

    /// <summary>点 p 能不能收到它。</summary>
    public bool HitTest(Vector2 point)
    {
        return !Collected && point.DistanceTo(Position) <= Radius;
    }

    /// <summary>收走，返回它值多少。</summary>
    public int Collect()
    {
        if (Collected)
        {
            return 0;
        }

        Collected = true;
        return Value;
    }
}
