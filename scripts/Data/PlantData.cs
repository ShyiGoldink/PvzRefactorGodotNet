using System.Collections.Generic;
using Godot;

/// <summary>
/// 一种植物的静态配置，读 `resource/data/plants/&lt;植物 id&gt;/plant.json`。
///
/// 这里只放"数据"，不放任何行为。行为全部是组件，由 EntityAssembler 按 components 装配。
/// 所以加一种植物通常不用写代码，写一份配置 + 一张图集就行。
///
/// 数据层：只描述数据，不放行为。
/// </summary>
public sealed class PlantData
{
    /// <summary>植物配置目录。每种植物一个文件夹，配置和图集都在里面。</summary>
    public const string DataDirectory = "res://resource/data/plants";

    /// <summary>
    /// 植物编号。**从 1000000 起**，和僵尸的编号分开，免得两边的表撞号。
    /// </summary>
    public int Id { get; private set; }

    /// <summary>显示名，选卡和图鉴上用。</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>种群（豌豆家族 / 向日葵家族……）。以后按种群给加成，现在只是记着。</summary>
    public string Population { get; private set; } = string.Empty;

    /// <summary>种一次要多少阳光。</summary>
    public int Cost { get; private set; }

    /// <summary>卡片冷却（秒）。</summary>
    public float Cooldown { get; private set; }

    /// <summary>血量上限。</summary>
    public float Hp { get; private set; }

    /// <summary>能不能作为"种子"带上场。像瓜子这种由别的植物生出来的，就是 false。</summary>
    public bool Seed { get; private set; } = true;

    /// <summary>组件编号 → 这个组件的参数。</summary>
    public Dictionary<int, Godot.Collections.Dictionary> Components { get; private set; }
        = new Dictionary<int, Godot.Collections.Dictionary>();

    /// <summary>某种植物的配置路径。</summary>
    public static string GetPath(int plantId)
    {
        return $"{DataDirectory}/{plantId}/plant.json";
    }

    /// <summary>某种植物的文件夹（图集在这儿）。</summary>
    public static string GetDirectory(int plantId)
    {
        return $"{DataDirectory}/{plantId}";
    }

    /// <summary>读一份植物配置。读不到就返回 null。</summary>
    public static PlantData Load(int plantId)
    {
        string path = GetPath(plantId);
        JsonReader json = JsonReader.FromFile(path);
        if (!json.IsValid)
        {
            return null;
        }

        var data = new PlantData
        {
            Id = json["id"].AsInt(plantId),
            DisplayName = json["display_name"].AsString(),
            Population = json["population"].AsString(),
            Cost = json["cost"].AsInt(),
            Cooldown = json["cooldown"].AsFloat(),
            Hp = json["hp"].AsFloat(),
            Seed = json["seed"].AsBool(true),
        };

        ComponentParams.ReadInto(json["components"], data.Components, path, "PlantData");
        return data;
    }
}
