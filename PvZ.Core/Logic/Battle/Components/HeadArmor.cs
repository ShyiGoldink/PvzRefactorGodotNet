using Godot;

/// <summary>
/// 头部防具（3002）：路障、铁桶。
///
/// 做法就是在受伤链上占一个**更高的优先级**：伤害先到它这儿，
/// 防具没掉完就全吃掉、返回 false 把后面的扣血堵住；
/// 防具刚好在这一下碎掉时，把剩下的伤害放过去，让僵尸本体接着挨。
///
/// 这就是用户说的"防具血量掉完之前阻塞后续的扣血"——
/// 用事件链的阻塞来实现，不用在扣血的地方写 if。
/// </summary>
public sealed class HeadArmor : EntityComponent
{
    /// <summary>比普通扣血（优先级 0）高就行了，数值本身不重要。</summary>
    private const int ArmorPriority = 10;

    public override int Id => 3002;
    public override string Type => "defense.armor.head";
    public override string DisplayName => "头部防具";
    public override string Description => "防具没碎之前，伤害全被它吃掉，后面的扣血被阻塞。";

    /// <summary>防具还剩多少。</summary>
    public float Armor { get; private set; }

    /// <summary>防具的上限，表现层拿来算"还剩百分之几"。</summary>
    public float MaxArmor { get; private set; }

    /// <summary>防具还在不在。</summary>
    public bool HasArmor => Armor > 0f;

    /// <summary>防具叫什么（路障 / 铁桶），给日志和以后的图鉴用。</summary>
    public string ArmorName { get; private set; } = string.Empty;

    private BattleEntity _entity;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        MaxArmor = ReadFloat(parameters, "amount", 0f);
        Armor = MaxArmor;

        if (parameters != null && parameters.ContainsKey("name"))
        {
            ArmorName = parameters["name"].AsString();
        }
    }

    public override void Bind(BattleEntity entity, string configId)
    {
        _entity = entity;
        if (_entity == null)
        {
            return;
        }

        _entity.Events.Register(BattleEventName.take_damage, new EventResponse(ArmorPriority, OnHurt));
    }

    private bool OnHurt(object arg)
    {
        if (!HasArmor)
        {
            return true; // 防具没了，放行给后面的扣血
        }

        if (arg is not DamageEvent hit)
        {
            return true;
        }

        float absorbed = Mathf.Min(Armor, hit.Amount);
        Armor -= absorbed;

        if (absorbed >= hit.Amount)
        {
            return false; // 这一下全被防具吃了，后面的别扣血了
        }

        // 防具刚好碎：剩下的伤害继续往下走
        hit.Amount -= absorbed;
        _entity.Events.Trigger(BattleEventName.armor_broken, null);
        return true;
    }
}
