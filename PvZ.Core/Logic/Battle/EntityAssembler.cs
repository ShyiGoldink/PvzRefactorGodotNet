using System.Collections.Generic;
using Godot;

/// <summary>
/// 装配中心：把"数据"变成"场上的一株植物 / 一只僵尸"。这里只做两件事，
/// 而且**不认识任何具体组件**：
///
/// 1. 建出基类对象（Plant / Zombie），填好配置里那几个公共字段；
/// 2. AttachComponents —— 按编号建组件、填参数、检查前置、挂上去、调组件的 Bind。
///
/// "编号 → 组件"的翻译表在 ComponentLibrary，所以**加组件不用动这个文件**。
///
/// 这里**不建节点、不碰外观**：植物和僵尸都是纯 C# 对象，
/// 表现层自己按它们的状态去画。
/// </summary>
public static class EntityAssembler
{
    /// <summary>按配置装配一株植物。装配完还没种到格子上，种哪由调用方决定。</summary>
    public static Plant BuildPlant(PlantData data)
    {
        if (data == null)
        {
            return null;
        }

        var plant = new Plant(data.Id.ToString(), data.DisplayName);
        plant.InitHp(data.Hp);

        int count = AttachComponents(plant, data.Components, data.Id.ToString());

        GD.Print($"[装配] 植物 {plant.Id}（{plant.DisplayName}）血量 {data.Hp}，组件 {count} 个");
        return plant;
    }

    /// <summary>
    /// 按配置装配一只僵尸，摆在 position（区域像素空间），站在 lawn 的第 lane 路上。
    /// position 指的是僵尸的嘴，不是身体中心。
    /// </summary>
    public static Zombie BuildZombie(ZombieData data, Vector2 position, TilesData lawn, int lane)
    {
        if (data == null)
        {
            return null;
        }

        var zombie = new Zombie(data.Id.ToString(), data.DisplayName)
        {
            Position = position,
            Lawn = lawn,
            Lane = lane,
        };
        zombie.InitHp(data.Hp);

        int count = AttachComponents(zombie, data.Components, data.Id.ToString());

        // 身体中心的宽度按图集格子算，和画出来的一样宽——判定和画面用同一把尺子
        AnimationGroupComponent animation = zombie.GetComponent<AnimationGroupComponent>();
        if (animation != null && animation.Cell.X > 0f)
        {
            zombie.BodyWidth = animation.Cell.X * GameManager.Scale;
        }

        GD.Print($"[装配] 僵尸 {zombie.Id}（{zombie.DisplayName}）血量 {data.Hp}，"
            + $"组件 {count} 个，宽 {zombie.BodyWidth:F0}");
        return zombie;
    }

    /// <summary>
    /// 把一组组件挂到实体上。键是组件编号，值是参数；返回挂上了几个。
    ///
    /// 分三步做：先全建出来填参数 → 检查前置组件并挂上去 → **都挂完了**才挨个调 Bind。
    /// 这样"谁写在前面"不影响结果：依赖是依赖，不是顺序。
    /// </summary>
    public static int AttachComponents(
        BattleEntity entity,
        Dictionary<int, Godot.Collections.Dictionary> components,
        string configId = null)
    {
        if (entity == null || components == null)
        {
            return 0;
        }

        var batch = new List<EntityComponent>();

        // 第一步：全部建出来、读好参数（还没挂上去，所以还没有任何副作用）
        foreach (KeyValuePair<int, Godot.Collections.Dictionary> entry in components)
        {
            EntityComponent component = ComponentLibrary.Create(entry.Key);
            if (component == null)
            {
                continue;
            }

            component.ApplyParams(entry.Value ?? new Godot.Collections.Dictionary());
            batch.Add(component);
        }

        // 第二步：前置组件齐了的才挂上去（先挂，不接线）
        var accepted = new List<EntityComponent>();
        foreach (EntityComponent component in batch)
        {
            if (!RequirementsMet(entity, component, batch))
            {
                continue; // 缺前置组件：不挂（报错在检查里打过了）
            }

            entity.AddComponent(component);
            accepted.Add(component);
        }

        // 第三步：身上挂齐了才接线。组件在 Bind 里可以放心找兄弟组件。
        foreach (EntityComponent component in accepted)
        {
            component.Bind(entity, configId);
        }

        return accepted.Count;
    }

    /// <summary>
    /// 前置组件齐了没有。Requirements 里写的是组件的 Type 名：
    /// 实体上已经挂着的算，**这一批里正准备挂的**也算（所以跟书写顺序无关）。
    /// </summary>
    private static bool RequirementsMet(BattleEntity entity, EntityComponent component, List<EntityComponent> batch)
    {
        string[] requirements = component.Requirements;
        if (requirements == null || requirements.Length == 0)
        {
            return true;
        }

        foreach (string requirement in requirements)
        {
            if (entity.HasComponent(requirement) || HasComponentType(batch, requirement))
            {
                continue;
            }

            GD.PushError($"[装配] {component.Type} 需要前置组件 {requirement}，"
                + $"{entity.Id} 上没有，这个组件不挂。");
            return false;
        }

        return true;
    }

    /// <summary>这一批里有没有这种类型的组件（还没挂上去的那些也算）。</summary>
    private static bool HasComponentType(List<EntityComponent> batch, string type)
    {
        foreach (EntityComponent component in batch)
        {
            if (component.Type == type)
            {
                return true;
            }
        }

        return false;
    }
}
