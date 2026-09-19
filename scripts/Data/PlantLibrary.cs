using System.Collections.Generic;
using Godot;

/// <summary>
/// 植物库：`resource/data/plants/` 下面每个数字文件夹是一种植物。
///
/// 选卡界面、图鉴都来问它"一共有哪些植物、编号是几"。库里的条目带着显示信息
/// （名字、价格、冷却），所以选卡界面不用再自己去读文件。
///
/// 读过的配置会缓存下来——同一株植物的配置读一次就够了。
/// </summary>
public static class PlantLibrary
{
    private static readonly Dictionary<int, PlantData> _cache = new Dictionary<int, PlantData>();
    private static List<int> _ids;

    /// <summary>库根目录。</summary>
    public static string Root => PlantData.DataDirectory;

    /// <summary>清掉缓存（改了配置、或者重新开局时用）。</summary>
    public static void ClearCache()
    {
        _cache.Clear();
        _ids = null;
    }

    /// <summary>库里所有植物编号，升序。扫的是文件夹名，所以加一种植物不用改代码。</summary>
    public static IReadOnlyList<int> AllIds()
    {
        if (_ids != null)
        {
            return _ids;
        }

        _ids = new List<int>();

        using DirAccess dir = DirAccess.Open(Root);
        if (dir == null)
        {
            GD.PushError($"[植物库] 打不开目录：{Root}（{DirAccess.GetOpenError()}）");
            return _ids;
        }

        foreach (string name in dir.GetDirectories())
        {
            if (int.TryParse(name, out int id))
            {
                _ids.Add(id);
            }
        }

        _ids.Sort();
        return _ids;
    }

    /// <summary>取一种植物的配置；没有就返回 null。</summary>
    public static PlantData Get(int plantId)
    {
        if (_cache.TryGetValue(plantId, out PlantData cached))
        {
            return cached;
        }

        PlantData data = PlantData.Load(plantId);
        _cache[plantId] = data;
        return data;
    }

    /// <summary>能当"种子"带上场的植物，按编号升序。</summary>
    public static List<PlantData> AllSeeds()
    {
        var seeds = new List<PlantData>();
        foreach (int id in AllIds())
        {
            PlantData data = Get(id);
            if (data != null && data.Seed)
            {
                seeds.Add(data);
            }
        }
        return seeds;
    }

    /// <summary>已解锁的那些种子，按编号升序。选卡界面只列这些。</summary>
    public static List<PlantData> UnlockedSeeds(SaveData save)
    {
        var list = new List<PlantData>();
        foreach (PlantData data in AllSeeds())
        {
            if (save == null || save.IsPlantUnlocked(data.Id))
            {
                list.Add(data);
            }
        }
        return list;
    }
}
