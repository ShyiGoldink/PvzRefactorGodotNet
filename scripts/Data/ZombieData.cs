using System.Collections.Generic;
using Godot;

/// <summary>
/// 一种僵尸的静态配置，读 resource/data/zombies/&lt;僵尸 id&gt;.json。
///
/// 跟植物一样：这里只放"数据"，不放任何行为。行为全部是组件，
/// 由 EntityAssembler 按 components 装配。加一种僵尸通常不用写代码。
///
/// 数据层：只描述数据，不放行为。
/// </summary>
public sealed class ZombieData
{
    /// <summary>僵尸配置目录。</summary>
    public const string DataDirectory = "res://resource/data/zombies";

    /// <summary>
    /// 僵尸编号，就是配置文件名（不带扩展名）。
    /// **关卡数据里 `type` 字段写的就是这个数字**，所以它必须是整数。
    /// </summary>
    public int Id { get; private set; }

    /// <summary>显示名。</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// 血量上限。放在顶层而不是塞进组件参数里，是因为
    /// "有没有血"是所有实体共有的概念，不是某个组件独有的。
    /// </summary>
    public float Hp { get; private set; }

    /// <summary>组件编号 → 这个组件的参数。</summary>
    public Dictionary<int, Godot.Collections.Dictionary> Components { get; private set; }
        = new Dictionary<int, Godot.Collections.Dictionary>();

    /// <summary>某只僵尸的配置路径。</summary>
    public static string GetPath(int zombieId)
    {
        return $"{DataDirectory}/{zombieId}.json";
    }

    /// <summary>读一份僵尸配置。读不到就返回 null。</summary>
    public static ZombieData Load(int zombieId)
    {
        string path = GetPath(zombieId);
        JsonReader json = JsonReader.FromFile(path);
        if (!json.IsValid)
        {
            return null;
        }

        var data = new ZombieData
        {
            Id = json["id"].AsInt(zombieId),
            DisplayName = json["display_name"].AsString(),
            Hp = json["hp"].AsFloat(),
        };

        Godot.Collections.Dictionary components = json["components"].AsObject();
        foreach (Variant key in components.Keys)
        {
            string keyText = key.AsString();
            if (!int.TryParse(keyText, out int componentId))
            {
                GD.PushError($"[ZombieData] 组件编号看不懂：{keyText}（{path}）");
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
