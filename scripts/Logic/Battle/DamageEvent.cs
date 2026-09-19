/// <summary>
/// 一次伤害。
///
/// 为什么不是直接传一个数字：一次伤害会被好几件事影响——谁打的、是不是真伤、
/// 要不要被护盾吃掉一部分……这些信息以后都要挂在同一个对象上往后传，
/// 所以从一开始就用对象，别等要加字段的时候再改签名。
/// </summary>
public sealed class DamageEvent
{
    /// <summary>谁打的。可能是 null（比如中毒、踩踏这种没有明确来源的）。</summary>
    public BattleEntity Source { get; }

    /// <summary>这一次的伤害值。</summary>
    public float Amount { get; set; }

    public DamageEvent(BattleEntity source, float amount)
    {
        Source = source;
        Amount = amount;
    }
}
