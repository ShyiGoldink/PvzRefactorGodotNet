using Godot;

/// <summary>
/// 植物动画组的基类：把植物状态翻译成图集上的一行。
///
/// 状态只有"待机 / 攻击 / 倒下"这几种，但**表现可以更多**——
/// 向日葵的"害怕待机"、坚果的"残血待机"都不是状态，是同一个待机下的不同表现。
/// 所以子类重写的不是状态机，而是"现在这种状态该挑哪几行"。
///
/// 挑行的时候按顺序试，第一个配过的就用——这样配置里少写一个动作也不会整株画不出来。
/// </summary>
public abstract class PlantAnimationComponent : AnimationGroupComponent
{
    public const string ClipIdle = "idle";
    public const string ClipAttack = "attack";
    public const string ClipDie = "die";

    // 编号和类型名都交给具体植物去定——基类自己不该被装配出来
    public abstract override int Id { get; }
    public abstract override string Type { get; }

    /// <summary>挂在哪株植物上。</summary>
    protected Plant Plant { get; private set; }

    protected override void OnBind(BattleEntity entity, string configId)
    {
        Plant = entity as Plant;
        if (Plant == null)
        {
            GD.PushError($"[{Type}] 只能挂在植物身上。");
            return;
        }

        Plant.Events.Register(BattleEventName.state_changed, new EventResponse(0, OnVisualChanged));
        Plant.Events.Register(BattleEventName.hp_changed, new EventResponse(0, OnVisualChanged));

        // 一上来就把该播的定下来，免得表现层来问的时候是空的
        PlayFirst(ClipsForState(Plant.State));
    }

    private bool OnVisualChanged(object arg)
    {
        PlayFirst(ClipsForState(Plant.State));
        return true;
    }

    /// <summary>这种状态下按顺序试这几行，用第一个配过的。</summary>
    protected virtual string[] ClipsForState(PlantState state)
    {
        switch (state)
        {
            case PlantState.Attack:
                // 没有攻击动作的植物（向日葵这种）退回待机，别空着
                return new[] { ClipAttack, ClipIdle };
            case PlantState.Die:
            case PlantState.Dead:
                return new[] { ClipDie, ClipIdle };
            default:
                return new[] { ClipIdle };
        }
    }

    /// <summary>按顺序找第一个配过的动作切过去。</summary>
    protected void PlayFirst(string[] candidates)
    {
        if (candidates == null || candidates.Length == 0)
        {
            return;
        }

        foreach (string name in candidates)
        {
            if (HasClip(name))
            {
                Play(name);
                return;
            }
        }

        // 一个都没配过：还是记下来，这样日志和表现层能看出是配置漏了，而不是"没动画"
        Play(candidates[0]);
    }
}

/// <summary>
/// 豌豆射手的动画：待机 / 攻击 / 倒下。
/// 它比基类多做的事只有一件——告诉别人"我的子弹收在这张图集的 pea 这一行"，
/// 子弹画自己的时候来问它。
/// </summary>
public sealed class PeashooterAnimation : PlantAnimationComponent
{
    public const string ClipBullet = "pea";

    public override int Id => 5006;
    public override string Type => "look.plant.peashooter";
    public override string DisplayName => "豌豆射手动画";
    public override string Description => "待机 / 攻击 / 倒下，外加 pea 这一行给子弹用。";
}

/// <summary>
/// 向日葵的动画：一般待机 / 害怕待机 / 倒下。
///
/// "害怕"的触发条件（用户定的）：**正在被啃**，或者**种在前半场**。
/// 前半场 = 列号小于总列数的一半——也就是离僵尸最近的那几列。
/// </summary>
public sealed class SunflowerAnimation : PlantAnimationComponent
{
    public const string ClipScared = "idle_scared";

    public override int Id => 5005;
    public override string Type => "look.plant.sunflower";
    public override string DisplayName => "向日葵动画";
    public override string Description => "被啃、或者种在前半场时切害怕待机。";

    protected override string[] ClipsForState(PlantState state)
    {
        if (state == PlantState.Idle && IsScared())
        {
            return new[] { ClipScared, ClipIdle };
        }

        return base.ClipsForState(state);
    }

    private bool IsScared()
    {
        if (Plant == null)
        {
            return false;
        }

        if (Plant.IsBeingEaten)
        {
            return true;
        }

        TilesData tiles = GameManager.Tiles;
        return Plant.Tile != null && tiles != null && Plant.Tile.Column < tiles.Columns / 2;
    }
}

/// <summary>
/// 坚果的动画：满血 / 半血 / 残血 / 倒下。
/// 它不是三个状态，是**一个待机状态按血量比例挑不同的行**——所以重写的是挑行，不是状态。
/// </summary>
public sealed class WallNutAnimation : PlantAnimationComponent
{
    public const string ClipFull = "full";
    public const string ClipHalf = "half";
    public const string ClipLow = "low";

    public override int Id => 5008;
    public override string Type => "look.plant.wallnut";
    public override string DisplayName => "坚果动画";
    public override string Description => "按血量挑满血 / 半血 / 残血待机。";

    protected override string[] ClipsForState(PlantState state)
    {
        if (state != PlantState.Idle && state != PlantState.Attack)
        {
            return base.ClipsForState(state);
        }

        return new[] { PhaseClip(), ClipIdle };
    }

    private string PhaseClip()
    {
        if (Plant == null || Plant.MaxHp <= 0f)
        {
            return ClipFull;
        }

        float ratio = Plant.Hp / Plant.MaxHp;
        if (ratio > 2f / 3f)
        {
            return ClipFull;
        }

        return ratio > 1f / 3f ? ClipHalf : ClipLow;
    }
}

/// <summary>
/// 土豆地雷的动画：待机 / 待发 / 爆炸。
/// "待发"不是植物状态，是那个地雷组件自己记的引信——所以这里去问它。
/// </summary>
public sealed class PotatoMineAnimation : PlantAnimationComponent
{
    public const string ClipArming = "idle";
    public const string ClipArmed = "arm";
    public const string ClipExplode = "explode";

    public override int Id => 5007;
    public override string Type => "look.plant.potatomine";
    public override string DisplayName => "土豆地雷动画";
    public override string Description => "引信好了切待发，炸的时候切爆炸。";

    private PotatoMine _mine;

    protected override void OnBind(BattleEntity entity, string configId)
    {
        _mine = entity.GetComponent<PotatoMine>();
        base.OnBind(entity, configId);
    }

    protected override string[] ClipsForState(PlantState state)
    {
        switch (state)
        {
            case PlantState.Die:
            case PlantState.Dead:
                return new[] { ClipExplode, ClipDie, ClipIdle };
            case PlantState.Idle:
            case PlantState.Attack:
                return new[] { _mine != null && _mine.IsArmed ? ClipArmed : ClipArming, ClipIdle };
            default:
                return base.ClipsForState(state);
        }
    }
}
