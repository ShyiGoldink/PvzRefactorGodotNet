using System.Collections.Generic;
using Godot;

/// <summary>
/// 存档。放 `user://save.json`，整包搬走的时候跟着走，不用另外找。
///
/// 它**不认识任何具体内容**：不知道有哪些植物、哪些关卡。
/// "初始解锁哪些""最多带几个"由调用方决定，存在这里的只是"这个玩家现在是什么状态"。
/// 这样加植物、加区域都不用动这个文件。
///
/// 数据层：只描述数据，不放行为。
/// </summary>
public sealed class SaveData
{
    /// <summary>存档路径。user:// 在 Windows 上是 %APPDATA%/Godot/app_userdata/&lt;项目名&gt;/。</summary>
    public const string FilePath = "user://save.json";

    /// <summary>最多能带几种植物上场（种子包的长度）。</summary>
    public int MaxSeedSlots { get; set; } = 1;

    /// <summary>已经解锁的植物 id。</summary>
    public List<int> UnlockedPlants { get; } = new List<int>();

    /// <summary>已通关的记录，键是 <see cref="LevelKey"/>。</summary>
    public List<int> ClearedLevels { get; } = new List<int>();

    /// <summary>区域 + 关卡 → 一个整数键，用来记"这关通没通"。</summary>
    public static int LevelKey(int regionId, int levelId)
    {
        return regionId * 1000 + levelId;
    }

    /// <summary>
    /// 读存档。读不到（第一次玩）就用传进来的默认值新建一份。
    /// 默认值由调用方给，这里不猜内容。
    /// </summary>
    public static SaveData Load(int defaultMaxSeedSlots = 1, IEnumerable<int> defaultUnlocked = null)
    {
        var save = new SaveData { MaxSeedSlots = defaultMaxSeedSlots };

        if (defaultUnlocked != null)
        {
            foreach (int plantId in defaultUnlocked)
            {
                save.UnlockedPlants.Add(plantId);
            }
        }

        // 先看文件在不在。第一次玩是正常情况，不该由 JsonReader 报一条错出来。
        if (!FileAccess.FileExists(FilePath))
        {
            GD.Print($"[存档] 没有存档，用默认值新建一份：{ProjectSettings.GlobalizePath(FilePath)}");
            save.Save();
            return save;
        }

        JsonReader json = JsonReader.FromFile(FilePath);
        if (!json.IsValid)
        {
            GD.PushError($"[存档] 存档文件读不了，这次按默认值走：{FilePath}");
            return save;
        }

        save.MaxSeedSlots = json["max_seed_slots"].AsInt(defaultMaxSeedSlots);
        save.UnlockedPlants.Clear();
        ReadIntList(json["unlocked_plants"], save.UnlockedPlants);
        save.ClearedLevels.Clear();
        ReadIntList(json["cleared_levels"], save.ClearedLevels);

        GD.Print($"[存档] 已读：种子包 {save.MaxSeedSlots} 格，"
            + $"解锁植物 {save.UnlockedPlants.Count} 种，通关 {save.ClearedLevels.Count} 关");
        return save;
    }

    /// <summary>写回磁盘。目录不存在的话 Godot 会自己建。</summary>
    public void Save()
    {
        var root = new Godot.Collections.Dictionary
        {
            { "max_seed_slots", MaxSeedSlots },
            { "unlocked_plants", ToVariantArray(UnlockedPlants) },
            { "cleared_levels", ToVariantArray(ClearedLevels) },
        };

        using FileAccess file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"[存档] 写不进去：{FilePath}（{FileAccess.GetOpenError()}）");
            return;
        }

        file.StoreString(Json.Stringify(root, "  "));
    }

    /// <summary>这种植物解锁了没。</summary>
    public bool IsPlantUnlocked(int plantId)
    {
        return UnlockedPlants.Contains(plantId);
    }

    /// <summary>解锁一种植物；已经有了就不重复加。</summary>
    public void UnlockPlant(int plantId)
    {
        if (!UnlockedPlants.Contains(plantId))
        {
            UnlockedPlants.Add(plantId);
        }
    }

    /// <summary>这关通了没。</summary>
    public bool IsLevelCleared(int regionId, int levelId)
    {
        return ClearedLevels.Contains(LevelKey(regionId, levelId));
    }

    /// <summary>记一次通关。</summary>
    public void MarkLevelCleared(int regionId, int levelId)
    {
        int key = LevelKey(regionId, levelId);
        if (!ClearedLevels.Contains(key))
        {
            ClearedLevels.Add(key);
        }
    }

    /// <summary>
    /// 已解锁的植物装不装得满种子包。
    /// 装不满就说明没得挑，选卡界面可以直接跳过——这是选卡流程的判据。
    /// </summary>
    public bool NeedsSeedSelection()
    {
        return UnlockedPlants.Count >= MaxSeedSlots;
    }

    private static void ReadIntList(JsonReader array, List<int> target)
    {
        for (int i = 0; i < array.Count; i++)
        {
            target.Add(array[i].AsInt());
        }
    }

    private static Godot.Collections.Array ToVariantArray(List<int> source)
    {
        var array = new Godot.Collections.Array();
        foreach (int value in source)
        {
            array.Add(value);
        }
        return array;
    }
}
