using System.Collections.Generic;

/// <summary>
/// 场上会动的东西的公共基类：植物、僵尸都是它。
///
/// 它提供所有实体都需要的那几样：id / 显示名 / 自己的事件中心 / 身上挂的组件 / 每帧推进。
/// 具体是什么东西（站着的还是走着的、有没有血量、站在哪一格）交给子类。
///
/// 逻辑层的东西，不是 Godot 节点：不放进场景树、没有任何动画，也能跑完一整局。
/// 表现层只是读它的状态去画图。
/// </summary>
public abstract class BattleEntity
{
    /// <summary>实体 id，就是数据文件夹里的配置名。</summary>
    public string Id { get; }

    /// <summary>显示名，装配时从配置里填。</summary>
    public string DisplayName { get; set; }

    /// <summary>这个实体自己的事件中心。组件在 Bind 里往它上面注册。</summary>
    public EventBus Events { get; } = new EventBus();

    private readonly List<EntityComponent> _components = new List<EntityComponent>();

    /// <summary>身上装着的组件，按装配顺序。</summary>
    public IReadOnlyList<EntityComponent> Components => _components;

    protected BattleEntity(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    /// <summary>
    /// 装配器专用：把一个组件挂上来。
    /// 这里不做任何检查（前置组件、重复之类），检查是装配器的事。
    /// </summary>
    public void AddComponent(EntityComponent component)
    {
        if (component != null)
        {
            _components.Add(component);
        }
    }

    /// <summary>按组件的 Type 名找组件，找不到返回 null。</summary>
    public EntityComponent GetComponent(string type)
    {
        foreach (EntityComponent component in _components)
        {
            if (component.Type == type)
            {
                return component;
            }
        }

        return null;
    }

    /// <summary>
    /// 按组件的 C# 类型找组件，找不到返回 null。
    /// 表现层用它找"这本动画收在哪儿"——它不认识具体是哪种动画，只认识动画组的基类。
    /// </summary>
    public T GetComponent<T>() where T : EntityComponent
    {
        foreach (EntityComponent component in _components)
        {
            if (component is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    /// <summary>身上有没有这种类型的组件。</summary>
    public bool HasComponent(string type)
    {
        return GetComponent(type) != null;
    }

    /// <summary>
    /// 往前走一帧。载荷是这一帧的秒数。
    /// 逻辑层自己决定谁调用它（正式跑的时候由战斗场景驱动，测试的时候可以直接循环调），
    /// 所以这里不依赖任何 Godot 的东西。
    /// </summary>
    public virtual void Tick(float delta)
    {
        Events.Trigger(BattleEventName.tick, delta);
    }
}
