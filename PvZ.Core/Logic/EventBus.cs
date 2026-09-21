using System;
using System.Collections.Generic;

/// <summary>
/// 一个事件处理函数。priority 大的先跑；返回 false 就挡住排在后面的。
/// </summary>
public struct EventResponse
{
    public int Priority;
    public Func<object, bool> Action;

    public EventResponse(int priority, Func<object, bool> action)
    {
        Priority = priority;
        Action = action;
    }
}

/// <summary>
/// 逻辑层用的小事件中心：同一个事件按优先级从大到小依次执行，处理函数可以中途阻塞后面的。
///
/// 跟闪球那边的 BallEvent 是同一套思路，但这边放在逻辑层共用——植物、僵尸、
/// 以后的任何东西各挂一个自己的，所以不叫 BallEvent。
///
/// 这个类不依赖 Godot 的任何节点，所以逻辑层能脱离场景树单独跑。
/// </summary>
public sealed class EventBus
{
    private readonly Dictionary<string, List<EventResponse>> _table = new Dictionary<string, List<EventResponse>>();

    /// <summary>注册一个处理函数。同一个处理函数注册多次就执行多次。</summary>
    public void Register(string eventName, EventResponse handler)
    {
        if (string.IsNullOrEmpty(eventName) || handler.Action == null)
        {
            return;
        }

        if (!_table.TryGetValue(eventName, out List<EventResponse> handlers))
        {
            handlers = new List<EventResponse>();
            _table[eventName] = handlers;
        }

        // 按 priority 从大到小插进去。用插入而不是每次 Sort：Sort 不稳定，
        // 插入能让同优先级的处理函数保持注册顺序，行为可预期。
        int index = handlers.Count;
        while (index > 0 && handlers[index - 1].Priority < handler.Priority)
        {
            index--;
        }
        handlers.Insert(index, handler);
    }

    /// <summary>
    /// 注销一个处理函数。必须传"注册时用的那个委托"——
    /// 现场重新写的 lambda 每次都是新对象，比对不上（方法组是可以的）。
    /// </summary>
    public bool Unregister(string eventName, EventResponse handler)
    {
        if (string.IsNullOrEmpty(eventName) || handler.Action == null)
        {
            return false;
        }

        if (!_table.TryGetValue(eventName, out List<EventResponse> handlers))
        {
            return false;
        }

        for (int i = 0; i < handlers.Count; i++)
        {
            if (handlers[i].Action != handler.Action)
            {
                continue;
            }

            handlers.RemoveAt(i);
            if (handlers.Count == 0)
            {
                _table.Remove(eventName);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// 触发事件。载荷可以是任意对象（数值、字符串、事件对象、甚至委托）。
    /// 返回 false 表示被某个处理函数挡住了，后面的不再执行。
    /// </summary>
    public bool Trigger(string eventName, object arg)
    {
        if (!_table.TryGetValue(eventName, out List<EventResponse> handlers))
        {
            return true;
        }

        // 先取快照再跑：处理函数如果在执行过程中又注册/注销了事件，
        // 本次执行也不会错位（漏跑或重复跑）。监听者很少，这点分配可以忽略。
        EventResponse[] snapshot = handlers.ToArray();
        foreach (EventResponse handler in snapshot)
        {
            if (!handler.Action(arg))
            {
                return false;
            }
        }

        return true;
    }
}
