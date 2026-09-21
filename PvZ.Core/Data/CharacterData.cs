/// <summary>
/// 一个角色的配置，读 resource/data/characters/&lt;角色 id&gt;.json。
///
/// 剧情里的 speaker_id 就是这个 id——"谁在说"要靠它查出名字和立绘。
///
/// 数据层：只描述数据，不放行为。
/// </summary>
public sealed class CharacterData
{
    public const string DataDirectory = "res://resource/data/characters";

    /// <summary>角色编号。</summary>
    public int Id { get; private set; }

    /// <summary>显示名。</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>立绘 / 头像的资源路径。</summary>
    public string Portrait { get; private set; } = string.Empty;

    /// <summary>某个角色配置的路径。</summary>
    public static string GetPath(int characterId)
    {
        return $"{DataDirectory}/{characterId}.json";
    }

    /// <summary>读一个角色的配置。读不到就返回 null。</summary>
    public static CharacterData Load(int characterId)
    {
        JsonReader json = JsonReader.FromFile(GetPath(characterId));
        if (!json.IsValid)
        {
            return null;
        }

        return new CharacterData
        {
            Id = json["id"].AsInt(characterId),
            DisplayName = json["display_name"].AsString(),
            Portrait = json["portrait"].AsString(),
        };
    }
}
