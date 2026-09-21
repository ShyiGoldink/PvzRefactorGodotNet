using Godot;

/// <summary>
/// 普通僵尸的动画组（5001）：走路 / 啃 / 断手之后的走路和啃 / 倒下。
///
/// "断手"不是状态，是一种损伤——掉了之后**动作没变，换的是行**。
/// 这正是用动画组管这件事的意义：走路那一套逻辑不用知道手还在不在。
///
/// 每一段都按顺序找第一个配过的行，所以配置里少写一个动作也不会整只僵尸消失。
/// </summary>
public sealed class NormalZombieAnimation : AnimationGroupComponent
{
    public const string ClipWalk = "walk";
    public const string ClipEat = "eat";
    public const string ClipWalkArmless = "walk_armless";
    public const string ClipEatArmless = "eat_armless";
    public const string ClipDie = "die";

    public override int Id => 5001;
    public override string Type => "look.zombie.normal";
    public override string DisplayName => "普通僵尸动画";
    public override string Description => "按状态 + 有没有胳膊挑图集上的一行。";

    private Zombie _zombie;

    protected override void OnBind(BattleEntity entity, string configId)
    {
        _zombie = entity as Zombie;
        if (_zombie == null)
        {
            GD.PushError($"[{Type}] 只能挂在僵尸身上。");
            return;
        }

        _zombie.Events.Register(BattleEventName.state_changed, new EventResponse(0, OnVisualChanged));
        _zombie.Events.Register(BattleEventName.arm_lost, new EventResponse(0, OnVisualChanged));

        PlayFirst(ClipsForCurrentState());
    }

    private bool OnVisualChanged(object arg)
    {
        PlayFirst(ClipsForCurrentState());
        return true;
    }

    private string[] ClipsForCurrentState()
    {
        switch (_zombie.State)
        {
            case ZombieState.Eat:
                return _zombie.ArmLost
                    ? new[] { ClipEatArmless, ClipEat, ClipWalkArmless, ClipWalk }
                    : new[] { ClipEat, ClipWalk };

            case ZombieState.Die:
            case ZombieState.Dead:
                return new[] { ClipDie, ClipWalk };

            default:
                // 登场和走路现在共用一段，等有登场动画了再拆开
                return _zombie.ArmLost
                    ? new[] { ClipWalkArmless, ClipWalk }
                    : new[] { ClipWalk };
        }
    }

    /// <summary>按顺序找第一个配过的动作切过去。</summary>
    private void PlayFirst(string[] candidates)
    {
        foreach (string name in candidates)
        {
            if (HasClip(name))
            {
                Play(name);
                return;
            }
        }

        if (candidates.Length > 0)
        {
            Play(candidates[0]);
        }
    }
}
