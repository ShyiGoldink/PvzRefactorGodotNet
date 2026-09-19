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
