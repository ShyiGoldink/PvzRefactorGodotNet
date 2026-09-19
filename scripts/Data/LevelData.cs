using Godot;
using System.Collections.Generic;

/// <summary>
/// 一关的配置，读 resource/data/&lt;区域编号&gt;/&lt;关卡编号&gt;.json。
///
/// 现在只读对战场景当场要用的那部分（格子类型表），
/// 剧情、阶段、出怪那些等做到那儿再接。
///
/// 数据层：只描述数据，不放行为。
/// </summary>
public sealed class LevelData
{
    /// <summary>关卡编号。</summary>
    public int Id { get; private set; }

    /// <summary>所属区域编号。</summary>
    public int RegionId { get; private set; }

    /// <summary>显示名。</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>难度。</summary>
    public string Difficulty { get; private set; } = string.Empty;

    /// <summary>
    /// 难度系数，中间阶段的预算要乘它。简单 ×1 / 普通 ×3 / 困难 ×5 / 噩梦 ×10。
    /// 读不懂的难度按简单算。
    /// </summary>
    public float DifficultyScale { get; private set; } = 1f;

    /// <summary>
    /// 格子类型表：[行, 列]。
    /// 第一维是行（y，从上往下），第二维是列（x，从左往右），跟 Godot 的坐标一致。
    /// 值是在本区域 region.json 的 terrain 里查图片用的地形编号。
    /// </summary>
    public int[,] TerrainGrid { get; private set; } = new int[0, 0];

    /// <summary>
    /// 开始阶段那几次出怪，按顺序。
    /// 中间阶段的小阶段以后会生成同样的结构，出怪器不用改。
    /// </summary>
    public List<LevelWave> StartWaves { get; private set; } = new List<LevelWave>();

    /// <summary>
    /// 中间阶段的小阶段，按顺序。每个小阶段以"一大波被清干净"为终点。
    /// </summary>
    public List<LevelSubStage> MiddleStages { get; private set; } = new List<LevelSubStage>();

    /// <summary>
    /// 一大波要不要必刷一只旗帜僵尸（不算进价值）。
    /// 有些关卡不该刷，就在关卡数据里写 `"flag_zombie": false`。
    /// </summary>
    public bool FlagZombie { get; private set; } = true;

    /// <summary>某一关配置的路径。</summary>
    public static string GetPath(int regionId, int levelId)
    {
        return $"{LevelCatalog.GetRegionDirectory(regionId)}/{levelId}.json";
    }

    /// <summary>读一关的配置。读不到就返回 null。</summary>
    public static LevelData Load(int regionId, int levelId)
    {
        string path = GetPath(regionId, levelId);
        JsonReader json = JsonReader.FromFile(path);
        if (!json.IsValid)
        {
            return null;
        }

        var data = new LevelData
        {
            Id = json["id"].AsInt(levelId),
            RegionId = json["region_id"].AsInt(regionId),
            DisplayName = json["display_name"].AsString(),
            Difficulty = json["difficulty"].AsString(),
        };

        data.DifficultyScale = data.Difficulty switch
        {
            "normal" => 3f,
            "hard" => 5f,
            "nightmare" => 10f,
            _ => 1f,
        };

        data.TerrainGrid = ReadTerrainGrid(json["terrain_grid"], path);
        data.StartWaves = ReadStartWaves(json["stages"], path);
        data.MiddleStages = ReadMiddleStages(json["stages"], path);
        data.FlagZombie = json["flag_zombie"].AsBool(true);
        return data;
    }

    /// <summary>只在 stages 里找 type == "middle" 那一段，把它的 wave 小阶段读出来。</summary>
    private static List<LevelSubStage> ReadMiddleStages(JsonReader stages, string path)
    {
        var list = new List<LevelSubStage>();

        for (int i = 0; i < stages.Count; i++)
        {
            JsonReader stage = stages[i];
            if (stage["type"].AsString() != "middle")
            {
                continue;
            }

            JsonReader subStages = stage["stages"];
            for (int s = 0; s < subStages.Count; s++)
            {
                JsonReader sub = subStages[s];

                // 中间阶段里也能插剧情，那不是小阶段，跳过
                if (sub["type"].AsString() != "wave")
                {
                    continue;
                }

                var item = new LevelSubStage
                {
                    Index = sub["index"].AsInt(s + 1),
                    BaseValue = sub["base_value"].AsInt(),
                };

                JsonReader types = sub["zombie_types"];
                for (int t = 0; t < types.Count; t++)
                {
                    item.ZombieTypes.Add(types[t].AsInt());
                }

                list.Add(item);
            }
        }

        if (list.Count == 0)
        {
            GD.PushWarning($"[LevelData] 这一关没有中间小阶段，打完开始阶段就结束了：{path}");
        }

        return list;
    }

    /// <summary>只在 stages 里找 type == "start" 那一段，把它的 waves 读出来。</summary>
    private static List<LevelWave> ReadStartWaves(JsonReader stages, string path)
    {
        var waves = new List<LevelWave>();

        for (int i = 0; i < stages.Count; i++)
        {
            JsonReader stage = stages[i];
            if (stage["type"].AsString() != "start")
            {
                continue;
            }

            JsonReader waveList = stage["waves"];
            for (int w = 0; w < waveList.Count; w++)
            {
                JsonReader waveJson = waveList[w];
                var wave = new LevelWave { Index = waveJson["index"].AsInt(w + 1) };

                JsonReader zombies = waveJson["zombies"];
                for (int z = 0; z < zombies.Count; z++)
                {
                    wave.Zombies.Add(new LevelZombieEntry
                    {
                        TypeId = zombies[z]["type"].AsInt(),
                        Count = zombies[z]["count"].AsInt(),
                    });
                }

                waves.Add(wave);
            }
        }

        if (waves.Count == 0)
        {
            GD.PushWarning($"[LevelData] 这一关没有 start 阶段，不会出怪：{path}");
        }

        return waves;
    }

    /// <summary>
    /// 把 Json 里的 [[...], [...]] 读成 [行, 列]。
    /// 行的长度不一致时按最短的那行算列数，并报错——宁可少几格，也别越界。
    /// </summary>
    private static int[,] ReadTerrainGrid(JsonReader rows, string path)
    {
        int rowCount = rows.Count;
        if (rowCount == 0)
        {
            GD.PushError($"[LevelData] terrain_grid 是空的：{path}");
            return new int[0, 0];
        }

        int columnCount = rows[0].Count;
        for (int row = 0; row < rowCount; row++)
        {
            if (rows[row].Count != columnCount)
            {
                GD.PushError($"[LevelData] terrain_grid 第 {row} 行有 {rows[row].Count} 格，"
                    + $"和第一行的 {columnCount} 格对不上：{path}");
            }
        }

        var grid = new int[rowCount, columnCount];
        for (int row = 0; row < rowCount; row++)
        {
            JsonReader line = rows[row];
            int cells = Mathf.Min(columnCount, line.Count);
            for (int column = 0; column < cells; column++)
            {
                grid[row, column] = line[column].AsInt();
            }
        }

        return grid;
    }
}
