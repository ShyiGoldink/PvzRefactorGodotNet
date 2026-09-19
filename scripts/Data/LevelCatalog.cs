using Godot;

/// <summary>
/// 一个区域下面有哪些小关。
///
/// 完全由数据决定：扫资源目录下的 json 文件，文件名（去掉扩展名）就是关卡编号。
/// 小关数量不写在代码里，也不写在场景里，加一个 json 就多一关。
/// </summary>
public static class LevelCatalog
{
    /// <summary>资源根目录。</summary>
    private const string DataRoot = "res://resource/data";

    /// <summary>取得某个区域的资源目录，例如 res://resource/data/1。</summary>
    public static string GetRegionDirectory(int regionId)
    {
        return $"{DataRoot}/{regionId}";
    }

    /// <summary>
    /// 取得某个区域的全部关卡编号，升序。
    /// 目录不存在、或者文件名不是数字，都会跳过并报错，不会抛异常。
    /// </summary>
    public static Godot.Collections.Array<int> GetLevelIds(int regionId)
    {
        var levelIds = new Godot.Collections.Array<int>();
        string directory = GetRegionDirectory(regionId);

        using DirAccess dir = DirAccess.Open(directory);
        if (dir == null)
        {
            GD.PushError($"[LevelCatalog] 打不开区域目录：{directory}（{DirAccess.GetOpenError()}）");
            return levelIds;
        }

        foreach (string file in dir.GetFiles())
        {
            if (!file.EndsWith(".json"))
            {
                continue;
            }

            // region.json 是这块区域自己的配置，不是关卡，跳过。
            if (file == RegionConfig.FileName)
            {
                continue;
            }

            string fileName = file.Substring(0, file.Length - ".json".Length);
            if (int.TryParse(fileName, out int levelId))
            {
                levelIds.Add(levelId);
            }
            else
            {
                GD.PushWarning($"[LevelCatalog] 文件名不是关卡编号，已跳过：{directory}/{file}");
            }
        }

        levelIds.Sort();
        return levelIds;
    }
}
