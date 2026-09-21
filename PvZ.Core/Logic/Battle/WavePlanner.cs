using System.Collections.Generic;
using Godot;

/// <summary>
/// 小阶段的出怪分配：把"价值"换成一张具体的僵尸清单。
///
/// 规则是用户定的：
/// - 总价值 = base_value × 难度系数；
/// - 预算拆两份，**渐进 60% / 一大波 40%**；
/// - 在允许的种类里买僵尸，**优先买占用价值高的**；
/// - 某一份的零头买不起最便宜的一只时，**零头并进另一份**。
///
/// "优先买贵的"如果理解成"能买得起就一直买最贵的"，一个 12 点的阶段会变成 6 只铁桶，
/// 那不是关卡，那是处刑。所以这里按**轮**买：一轮里每种最多买一只、贵的先买，
/// 买得起就再来一轮。这样既保持"贵的优先"，又能出杂牌。
///
/// 逻辑层的东西，不依赖节点，也不认识任何具体僵尸——它只认"编号 + 占用价值"。
/// </summary>
public static class WavePlanner
{
    /// <summary>渐进部分占总预算的比例。</summary>
    public const float TrickleRatio = 0.6f;

    /// <summary>一个"小阶段"算出来的出怪清单。</summary>
    public sealed class Plan
    {
        /// <summary>慢慢放出来的那批。</summary>
        public readonly List<int> Trickle = new List<int>();

        /// <summary>最后一大波一口气放出来的那批。</summary>
        public readonly List<int> HugeWave = new List<int>();

        /// <summary>这一阶段一共几只。</summary>
        public int TotalCount => Trickle.Count + HugeWave.Count;
    }

    /// <summary>
    /// 把一个小阶段算成具体清单。
    /// difficultyScale 是难度系数（简单 ×1 / 普通 ×3 / 困难 ×5 / 噩梦 ×10）。
    /// </summary>
    public static Plan Build(int baseValue, float difficultyScale, IReadOnlyList<int> allowedTypes)
    {
        var plan = new Plan();

        // 把允许的种类换算成"编号 + 占用价值"，按价值从高到低排
        var candidates = new List<(int TypeId, int Cost)>();
        if (allowedTypes != null)
        {
            foreach (int typeId in allowedTypes)
            {
                ZombieData data = ZombieLibrary.Get(typeId);
                if (data == null)
                {
                    continue;
                }

                candidates.Add((typeId, Mathf.Max(1, (int)data.Value)));
            }
        }

        if (candidates.Count == 0)
        {
            return plan;
        }

        candidates.Sort((a, b) => b.Cost.CompareTo(a.Cost));

        int total = (int)(baseValue * difficultyScale);
        int trickleBudget = (int)(total * TrickleRatio);
        int hugeBudget = total - trickleBudget;

        // 渐进那份先买
        int leftover = Buy(trickleBudget, candidates, plan.Trickle);

        // 零头并进一大波那份
        Buy(hugeBudget + leftover, candidates, plan.HugeWave);

        return plan;
    }

    /// <summary>
    /// 花钱买僵尸，返回剩下买不动的那点零头。
    /// 一轮里每种最多买一只（贵的先买），买得起就再来一轮。
    /// </summary>
    private static int Buy(int budget, List<(int TypeId, int Cost)> candidates, List<int> into)
    {
        if (budget <= 0)
        {
            return 0;
        }

        while (true)
        {
            bool boughtInThisRound = false;

            foreach ((int typeId, int cost) in candidates)
            {
                if (budget < cost)
                {
                    continue;
                }

                into.Add(typeId);
                budget -= cost;
                boughtInThisRound = true;
            }

            if (!boughtInThisRound)
            {
                break; // 最便宜的也买不起了
            }
        }

        return budget;
    }
}
