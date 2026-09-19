using System.Collections.Generic;
using Godot;

/// <summary>
/// 一种僵尸的静态配置，读 `resource/data/zombies/&lt;僵尸 id&gt;/zombie.json`。
///
/// 跟植物一样：这里只放"数据"，不放任何行为。行为全部是组件，
/// 由 EntityAssembler 按 components 装配。加一种僵尸通常不用写代码。
///
/// 数据层：只描述数据，不放行为。
/// </summary>
public sealed class ZombieData
{
    /// <summary>僵尸配置目录。每种僵尸一个文件夹，配置和图集都在里面。</summary>
    public const string DataDirectory = "res://resource/data/zombies";

    /// <summary>
    /// 僵尸编号。**关卡数据里 `type` 字段写的就是这个数字**，所以它必须是整数。
    /// </summary>
    public int Id { get; private set; }

    /// <summary>显示名。</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>血量上限（不含防具）。</summary>
    public float Hp { get; private set; }

    /// <summary>
    /// 占用价值。出怪时按价值分配预算要用它——越硬的僵尸占的预算越多。
    /// </summary>
    public float Value { get; private set; } = 1f;

    /// <summary>
    /// 是不是"旗帜僵尸"。一大波僵尸时必刷一只、**不算进价值**，
    /// 而且要有开关——有些关卡不该刷。
    /// </summary>
    public bool Flag { get; private set; }

    /// <summary>组件编号 → 这个组件的参数。</summary>
    public Dictionary<int, Godot.Collections.Dictionary> Components { get; private set; }
        = new Dictionary<int, Godot.Collections.Dictionary>();

    /// <summary>某只僵尸的配置路径。</summary>
    public static string GetPath(int zombieId)
    {
        return $"{DataDirectory}/{zombieId}/zombie.json";
    }

    /// <summary>某只僵尸的文件夹（图集在这儿）。</summary>
    public static string GetDirectory(int zombieId)
    {
        return $"{DataDirectory}/{zombieId}";
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
            Value = json["value"].AsFloat(1f),
            Flag = json["flag"].AsBool(),
        };

        ComponentParams.ReadInto(json["components"], data.Components, path, "ZombieData");
        return data;
    }
}
