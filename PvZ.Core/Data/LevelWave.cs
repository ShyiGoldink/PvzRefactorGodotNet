using System.Collections.Generic;

/// <summary>
/// 关卡数据里的一次出怪（`start` 阶段 `waves` 里的一项）。
///
/// 现在只有开始阶段用它。以后中间阶段的小阶段会生成同样的结构，
/// 出怪器就不用改——它只认"这一波要放哪些僵尸"。
/// </summary>
public sealed class LevelWave
{
    /// <summary>第几次出怪，从 1 开始。</summary>
    public int Index { get; set; }

    /// <summary>这一波出哪些僵尸、各几只。</summary>
    public List<LevelZombieEntry> Zombies { get; } = new List<LevelZombieEntry>();

    /// <summary>这一波一共几只。</summary>
    public int TotalCount
    {
        get
        {
            int total = 0;
            foreach (LevelZombieEntry entry in Zombies)
            {
                total += entry.Count;
            }
            return total;
        }
    }
}

/// <summary>一次出怪里的"某种僵尸出几只"。</summary>
public sealed class LevelZombieEntry
{
    /// <summary>僵尸编号，就是 zombie 配置文件名那个数字。</summary>
    public int TypeId { get; set; }

    /// <summary>出几只。</summary>
    public int Count { get; set; }
}

/// <summary>
/// 中间阶段里的一个小阶段。
///
/// 它**不写死出哪些僵尸**：只给一个基础价值和允许的种类，
/// 具体出什么由 WavePlanner 按预算算出来——所以调难度只要改数字，不用重配一遍。
/// </summary>
public sealed class LevelSubStage
{
    /// <summary>第几个小阶段，从 1 开始。</summary>
    public int Index { get; set; }

    /// <summary>基础价值（还要乘难度系数才是总预算）。</summary>
    public int BaseValue { get; set; }

    /// <summary>这一个阶段允许出现的僵尸种类。</summary>
    public List<int> ZombieTypes { get; } = new List<int>();
}
