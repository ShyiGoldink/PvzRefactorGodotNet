using System.Collections.Generic;
using Godot;

/// <summary>
/// 一种植物的静态配置，读 resource/data/plants/&lt;植物 id&gt;.json。
///
/// 这里只放"数据"，不放任何行为。行为全部是组件，由 PlantAssembler 按 components 装配。
/// 所以加一种植物通常不用写代码，写一份配置就行。
///
/// 数据层：只描述数据，不放行为。
/// </summary>
public sealed class PlantData
{
    /// <summary>植物配置目录。</summary>
    public const string DataDirectory = "res://resource/data/plants";

    /// <summary>植物 id，就是配置文件名（不带扩展名）。</summary>
    public string Id { get; private set; } = string.Empty;

    /// <summary>显示名。</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// 组件编号 → 这个组件的参数。
    /// 参数先原样留着（Json 里写的那一份），由组件自己的 ApplyParams 解释，
    /// 数据层不解释它。
    /// </summary>
    public Dictionary<int, Godot.Collections.Dictionary> Components { get; private set; }
        = new Dictionary<int, Godot.Collections.Dictionary>();

    /// <summary>某个植物的配置路径。</summary>
    public static string GetPath(string plantId)
    {
        return $"{DataDirectory}/{plantId}.json";
    }

    /// <summary>读一份植物配置。读不到就返回 null。</summary>
    public static PlantData Load(string plantId)
    {
        string path = GetPath(plantId);
        JsonReader json = JsonReader.FromFile(path);
        if (!json.IsValid)
        {
            return null;
        }

        var data = new PlantData
        {
            Id = json["id"].AsString(plantId),
            DisplayName = json["display_name"].AsString(),
        };

        Godot.Collections.Dictionary components = json["components"].AsObject();
        foreach (Variant key in components.Keys)
        {
            string keyText = key.AsString();
            if (!int.TryParse(keyText, out int componentId))
            {
                GD.PushError($"[PlantData] 组件编号看不懂：{keyText}（{path}）");
                continue;
            }

            Variant parameters = components[key];
            data.Components[componentId] = parameters.VariantType == Variant.Type.Dictionary
                ? parameters.AsGodotDictionary()
                : new Godot.Collections.Dictionary();
        }

        return data;
    }
}
