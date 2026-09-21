using System.Collections.Generic;
using Godot;

/// <summary>
/// 一块区域自己的配置，读 resource/data/&lt;区域编号&gt;/region.json。
///
/// 关卡内部的数据在 &lt;关卡编号&gt;.json 里，这个文件只放"这块区域整体"的属性：
/// 背景、格子区从哪开始、每格多大、有哪些地形。以后要加解锁条件、可用植物之类的也往这加。
///
/// 数据层：只描述数据，不放行为。
/// </summary>
public sealed class RegionConfig
{
    public const string FileName = "region.json";

    /// <summary>区域编号，和目录名一致。</summary>
    public int Id { get; private set; }

    /// <summary>区域显示名，选关界面的标题栏用它。</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>区域背景图的资源路径。一个区域一张，固定不变。</summary>
    public string Background { get; private set; } = string.Empty;

    /// <summary>
    /// 格子区左上角在"区域像素空间"里的位置。
    /// 区域像素空间以区域图左上角为原点，向右 x 变大，向下 y 变大。
    /// </summary>
    public Vector2 TileOrigin { get; private set; }

    /// <summary>每一格的像素大小。</summary>
    public Vector2 TileSize { get; private set; }

    /// <summary>
    /// 出怪点在草坪右边缘再往右多少像素。
    /// 大过"草坪右边缘到画面右边缘"的距离，僵尸才会在画面外出场、再走进来。
    /// </summary>
    public float SpawnMargin { get; private set; }

    private readonly Dictionary<int, string> _terrainTextures = new Dictionary<int, string>();

    /// <summary>地形编号 → 图片路径。这块区域一共配了几种地形。</summary>
    public IReadOnlyDictionary<int, string> TerrainTextures => _terrainTextures;

    /// <summary>region.json 的完整路径。</summary>
    public static string GetPath(int regionId)
    {
        return $"{LevelCatalog.GetRegionDirectory(regionId)}/{FileName}";
    }

    /// <summary>某个地形编号用的图片路径；没配过就返回空串。</summary>
    public string GetTerrainTexture(int terrainId)
    {
        return _terrainTextures.TryGetValue(terrainId, out string path) ? path : string.Empty;
    }

    /// <summary>读一块区域的配置。读不到就返回 null。</summary>
    public static RegionConfig Load(int regionId)
    {
        string path = GetPath(regionId);
        JsonReader json = JsonReader.FromFile(path);
        if (!json.IsValid)
        {
            return null;
        }

        var config = new RegionConfig
        {
            // 文件里的 id 优先；没写就用目录编号兜底。
            Id = json["id"].AsInt(regionId),
            DisplayName = json["display_name"].AsString(),
            Background = json["background"].AsString(),
            TileOrigin = ReadVector2(json, "tile_origin"),
            TileSize = ReadVector2(json, "tile_size"),
            SpawnMargin = json["spawn_margin"].AsFloat(),
        };

        JsonReader terrain = json["terrain"];
        foreach (Variant key in terrain.AsObject().Keys)
        {
            string keyText = key.AsString();
            if (!int.TryParse(keyText, out int terrainId))
            {
                GD.PushError($"[RegionConfig] 地形编号看不懂：{keyText}（{path}）");
                continue;
            }

            config._terrainTextures[terrainId] = terrain[keyText]["texture"].AsString();
        }

        return config;
    }

    /// <summary>把 Json 里的 [x, y] 读成 Vector2；格式不对就返回零向量。</summary>
    private static Vector2 ReadVector2(JsonReader parent, string key)
    {
        JsonReader value = parent[key];
        if (!value.IsArray || value.Count < 2)
        {
            GD.PushError($"[RegionConfig] {key} 应该写成 [x, y]。");
            return Vector2.Zero;
        }

        return new Vector2(value[0].AsFloat(), value[1].AsFloat());
    }
}
