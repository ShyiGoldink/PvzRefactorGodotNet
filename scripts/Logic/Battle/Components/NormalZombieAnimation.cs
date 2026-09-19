using Godot;

/// <summary>
/// 普通僵尸的动画组（5001）：走路 / 啃 / 倒下 三段。
///
/// 它只把**状态翻译成动画名**，具体每段是什么资源、怎么播，由表现层拿这张表去解决。
/// 换一套美术只要改配置里的地址，这个类不用动。
/// </summary>
public sealed class NormalZombieAnimation : AnimationGroupComponent
{
    /// <summary>走路那一段的名字。</summary>
    public const string ClipWalk = "walk";

    /// <summary>啃东西那一段的名字。</summary>
    public const string ClipEat = "eat";

    /// <summary>倒下那一段的名字。</summary>
    public const string ClipDie = "die";

    public override int Id => 5001;
    public override string Type => "look.zombie.normal";
    public override string DisplayName => "普通僵尸动画";
    public override string Description => "把僵尸状态翻译成 walk / eat / die 三段。";

    private Zombie _zombie;

    protected override void OnBind(BattleEntity entity, string configId)
    {
        _zombie = entity as Zombie;
        if (_zombie == null)
        {
            GD.PushError($"[{Type}] 只能挂在僵尸身上。");
            return;
        }

        _zombie.Events.Register(BattleEventName.state_changed, new EventResponse(0, OnStateChanged));

        // 一上来就把当前该播的定下来，免得表现层来问的时候是空的
        Play(ClipForState(_zombie.State));
    }

    private bool OnStateChanged(object arg)
    {
        Play(ClipForState(_zombie.State));
        return true;
    }

    private static string ClipForState(ZombieState state)
    {
        switch (state)
        {
            case ZombieState.Eat:
                return ClipEat;
            case ZombieState.Die:
            case ZombieState.Dead:
                return ClipDie;
            default:
                // 登场和走路现在共用一段，等有登场动画了再拆开
                return ClipWalk;
        }
    }
}
