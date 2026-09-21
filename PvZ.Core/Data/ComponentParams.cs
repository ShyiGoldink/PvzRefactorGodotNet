using System.Collections.Generic;
using Godot;

/// <summary>
/// 把配置里 `components` 那一段读成"组件编号 → 参数表"。
///
/// 植物和僵尸的读法一模一样，所以抽出来一份，免得两边各写一遍、改一处忘一处。
/// 它**不解释参数内容**，只负责把编号认出来、把参数原样递下去。
/// </summary>
public static class ComponentParams
{
    public static void ReadInto(
        JsonReader components,
        Dictionary<int, Godot.Collections.Dictionary> target,
        string sourcePath,
        string owner)
    {
        if (target == null)
        {
            return;
        }

        Godot.Collections.Dictionary raw = components.AsObject();
        foreach (Variant key in raw.Keys)
        {
            string keyText = key.AsString();
            if (!int.TryParse(keyText, out int componentId))
            {
                GD.PushError($"[{owner}] 组件编号看不懂：{keyText}（{sourcePath}）");
                continue;
            }

            Variant parameters = raw[key];
            target[componentId] = parameters.VariantType == Variant.Type.Dictionary
                ? parameters.AsGodotDictionary()
                : new Godot.Collections.Dictionary();
        }
    }
}
