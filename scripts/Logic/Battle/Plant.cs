/// <summary>
/// 植物的基类。
///
/// 它自己不带任何"具体植物"的行为——豌豆射手、向日葵这些不是一个一个的子类，
/// 而是 EntityAssembler 按配置把若干 EntityComponent 装配到它身上拼出来的。
/// 换一种植物 = 换一份配置，加一种植物基本不用写代码。
///
/// 逻辑层的东西，不是 Godot 节点：不放进场景树、没有任何动画，也能跑完一整局。
/// </summary>
public sealed class Plant : BattleEntity
{
    /// <summary>站在哪一格。没种下去的时候是 null，由 Tile.Place / Tile.Take 维护。</summary>
    public Tile Tile { get; internal set; }

    /// <summary>是不是已经种在草坪上了。</summary>
    public bool IsPlanted => Tile != null;

    public Plant(string id, string displayName)
        : base(id, displayName)
    {
    }
}
