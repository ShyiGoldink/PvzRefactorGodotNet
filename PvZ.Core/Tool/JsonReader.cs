using System.Globalization;
using Godot;

/// <summary>
/// 读 JSON 字段的工具。
///
/// 它只负责"把 JSON 文件读成能按字段取值的对象"，不认识关卡、僵尸、剧情这些概念，
/// 具体读哪些字段、怎么解释，全部由调用方决定。
///
/// 用法：
/// <code>
/// JsonReader level = JsonReader.FromFile("res://resource/data/1/1.json");
/// if (level.IsValid)
/// {
///     int id = level["id"].AsInt();
///     JsonReader stages = level["stages"];
///     for (int i = 0; i < stages.Count; i++)
///     {
///         string type = stages[i]["type"].AsString();
///     }
/// }
/// </code>
///
/// 取不到字段时不会抛异常，而是返回一个"无效"的 JsonReader，
/// 由 AsXxx(fallback) 给出兜底值，IsValid / Has 用来判断到底有没有取到。
/// </summary>
public sealed class JsonReader
{
    private readonly Variant _value;

    /// <summary>是否取到了东西。false 表示字段不存在、越界，或者文件读失败。</summary>
    public bool IsValid { get; }

    private JsonReader(Variant value, bool isValid)
    {
        _value = value;
        IsValid = isValid;
    }

    private static JsonReader Invalid() => new JsonReader(default, false);

    /// <summary>从 res:// 或 user:// 路径读一个 JSON 文件。</summary>
    public static JsonReader FromFile(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            GD.PushError($"[JsonReader] 文件不存在：{path}");
            return Invalid();
        }

        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"[JsonReader] 打不开文件：{path}（{FileAccess.GetOpenError()}）");
            return Invalid();
        }

        return FromText(file.GetAsText(), path);
    }

    /// <summary>直接解析一段 JSON 文本。</summary>
    public static JsonReader FromText(string json, string source = "")
    {
        Variant parsed = Json.ParseString(json);
        if (parsed.VariantType == Variant.Type.Nil)
        {
            GD.PushError($"[JsonReader] 解析失败：{source}");
            return Invalid();
        }

        return new JsonReader(parsed, true);
    }

    /// <summary>取对象的一个字段。</summary>
    public JsonReader this[string key]
    {
        get
        {
            if (_value.VariantType != Variant.Type.Dictionary)
            {
                return Invalid();
            }

            Godot.Collections.Dictionary dict = _value.AsGodotDictionary();
            return dict.TryGetValue(key, out Variant found) ? new JsonReader(found, true) : Invalid();
        }
    }

    /// <summary>取数组的一个元素。</summary>
    public JsonReader this[int index]
    {
        get
        {
            if (_value.VariantType != Variant.Type.Array)
            {
                return Invalid();
            }

            Godot.Collections.Array array = _value.AsGodotArray();
            return index >= 0 && index < array.Count ? new JsonReader(array[index], true) : Invalid();
        }
    }

    /// <summary>数组的元素个数，或对象的字段个数。都不是就返回 0。</summary>
    public int Count => _value.VariantType switch
    {
        Variant.Type.Array => _value.AsGodotArray().Count,
        Variant.Type.Dictionary => _value.AsGodotDictionary().Count,
        _ => 0,
    };

    /// <summary>对象里有没有这个字段（有字段但值是 null 也算有）。</summary>
    public bool Has(string key)
    {
        return _value.VariantType == Variant.Type.Dictionary && _value.AsGodotDictionary().ContainsKey(key);
    }

    /// <summary>当前值是对象。</summary>
    public bool IsObject => _value.VariantType == Variant.Type.Dictionary;

    /// <summary>当前值是数组。</summary>
    public bool IsArray => _value.VariantType == Variant.Type.Array;

    /// <summary>把当前值当数组取出来，用来做 foreach。取不到就是空数组。</summary>
    public Godot.Collections.Array AsArray()
    {
        return _value.VariantType == Variant.Type.Array
            ? _value.AsGodotArray()
            : new Godot.Collections.Array();
    }

    /// <summary>把当前值当对象取出来，用来遍历所有字段。取不到就是空字典。</summary>
    public Godot.Collections.Dictionary AsObject()
    {
        return _value.VariantType == Variant.Type.Dictionary
            ? _value.AsGodotDictionary()
            : new Godot.Collections.Dictionary();
    }

    public int AsInt(int fallback = 0)
    {
        return _value.VariantType switch
        {
            Variant.Type.Int => (int)_value.AsInt64(),
            Variant.Type.Float => (int)_value.AsDouble(),
            Variant.Type.Bool => _value.AsBool() ? 1 : 0,
            Variant.Type.String => int.TryParse(_value.AsString(), out int parsed) ? parsed : fallback,
            _ => fallback,
        };
    }

    public float AsFloat(float fallback = 0f)
    {
        return _value.VariantType switch
        {
            Variant.Type.Float => (float)_value.AsDouble(),
            Variant.Type.Int => _value.AsInt64(),
            Variant.Type.String => float.TryParse(_value.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallback,
            _ => fallback,
        };
    }

    public bool AsBool(bool fallback = false)
    {
        return _value.VariantType switch
        {
            Variant.Type.Bool => _value.AsBool(),
            Variant.Type.Int => _value.AsInt64() != 0,
            Variant.Type.String => bool.TryParse(_value.AsString(), out bool parsed) ? parsed : fallback,
            _ => fallback,
        };
    }

    public string AsString(string fallback = "")
    {
        return _value.VariantType == Variant.Type.String ? _value.AsString() : fallback;
    }
}
