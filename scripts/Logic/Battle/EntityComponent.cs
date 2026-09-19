using System;
using Godot;

/// <summary>
/// 所有实体组件的基类。植物和僵尸的组件都是它。
///
/// 组件只做两件事：
/// 1. **自描述**：Id / Type / Description / Requirements / ApplyParams；
/// 2. **在 Bind 里接线**：往实体的事件上注册处理函数、填自己的字段。
///
/// 装配器只管"按编号建出来 → 填参数 → 检查前置 → 挂上去 → 调 Bind"，
/// 它**不认识任何具体组件**——这就是"加组件不用改装配器"的原因。
///
/// 跟闪球的 BallComponent 有一处不同：**它不是 Node**。
/// 这边逻辑和表现是分开的，组件属于逻辑层，要能脱离场景树跑，
/// 所以不许在这里碰节点、贴图、动画——那些是表现层的事，读状态就行。
/// </summary>
public abstract class EntityComponent
{
    /// <summary>
    /// 唯一编号。约定：1xxx 移动，2xxx 攻击，3xxx 防御与受击，4xxx 行为与状态，
    /// 5xxx 形态与动画，6xxx 控制与增益，7xxx 资源。
    /// </summary>
    public abstract int Id { get; }

    /// <summary>类型名，形如 "move.walk"。配置和文档里用它指代这个组件。</summary>
    public abstract string Type { get; }

    /// <summary>显示名，给文档和调试输出用。</summary>
    public virtual string DisplayName => Type;

    /// <summary>这个组件是做什么的，一句话说清。</summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// 前置组件：没装配它，这个组件就跑不起来。写组件的 Type 名，可以写好几个。
    /// 装配时检查：实体上没有、这一批里也没有，就不挂这个组件并报错。
    ///
    /// 写的是**依赖**，不是处理顺序——配置里谁写在前面都一样。
    /// </summary>
    public virtual string[] Requirements => Array.Empty<string>();

    /// <summary>
    /// 装配时把配置里这个组件的参数读进自己的字段。
    /// 没有参数（比如普通受击）就不用管它；缺的参数一律用组件里的默认值。
    /// </summary>
    public virtual void ApplyParams(Godot.Collections.Dictionary parameters)
    {
    }

    /// <summary>
    /// 装配时接线：注册事件、填自己的字段。默认什么都不做（纯参数型的组件不用管它）。
    /// 这时候所有组件都已经挂到实体身上了，所以可以放心找兄弟组件，
    /// 找得到与否跟配置里的书写顺序无关。
    ///
    /// entity 是挂了它的那个东西（植物或僵尸），configId 是"哪份配置配出了这些组件"。
    /// </summary>
    public virtual void Bind(BattleEntity entity, string configId)
    {
    }

    /// <summary>读一个数值参数；配置里没写就用兜底值。</summary>
    protected static float ReadFloat(Godot.Collections.Dictionary parameters, string key, float fallback)
    {
        if (parameters == null || !parameters.ContainsKey(key))
        {
            return fallback;
        }

        return (float)parameters[key].AsDouble();
    }
}
