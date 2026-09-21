using System.Collections.Generic;
using Godot;

/// <summary>
/// 僵尸库：`resource/data/zombies/` 下面每个数字文件夹是一种僵尸。
///
/// 出怪器拿它按编号查僵尸的占用价值（算预算要用）；旗帜僵尸也是在这儿找的——
/// 谁身上写了 `"flag": true`，谁就是"一大波"里必刷的那只。
/// </summary>
public static class ZombieLibrary
{
    private static readonly Dictionary<int, ZombieData> _cache = new Dictionary<int, ZombieData>();
    private static List<int> _ids;
    private static int _flagZombieId;
    private static bool _flagSearched;

    /// <summary>清缓存（改了配置、重新开局时用）。</summary>
    public static void ClearCache()
    {
        _cache.Clear();
        _ids = null;
        _flagSearched = false;
        _flagZombieId = 0;
    }

    /// <summary>库里所有僵尸编号，升序。扫的是文件夹名。</summary>
    public static IReadOnlyList<int> AllIds()
    {
        if (_ids != null)
        {
            return _ids;
        }

        _ids = new List<int>();

        using DirAccess dir = DirAccess.Open(ZombieData.DataDirectory);
        if (dir == null)
        {
            GD.PushError($"[僵尸库] 打不开目录：{ZombieData.DataDirectory}");
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

    /// <summary>取一只僵尸的配置；没有就返回 null。</summary>
    public static ZombieData Get(int zombieId)
    {
        if (_cache.TryGetValue(zombieId, out ZombieData cached))
        {
            return cached;
        }

        ZombieData data = ZombieData.Load(zombieId);
        _cache[zombieId] = data;
        return data;
    }

    /// <summary>旗帜僵尸的编号；配置里没标就返回 0。</summary>
    public static int FlagZombieId()
    {
        if (_flagSearched)
        {
            return _flagZombieId;
        }

        _flagSearched = true;
        foreach (int id in AllIds())
        {
            ZombieData data = Get(id);
            if (data != null && data.Flag)
            {
                _flagZombieId = id;
                break;
            }
        }

        if (_flagZombieId == 0)
        {
            GD.PushWarning("[僵尸库] 没有任何僵尸标了 flag=true，一大波不会出旗帜僵尸。");
        }

        return _flagZombieId;
    }
}
