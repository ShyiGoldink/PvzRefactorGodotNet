using Godot;

/// <summary>
/// 按编号创建组件。配置里 components 的键就是这个编号，加新组件时在这里补一行。
///
/// 编号段位：1xxx 移动 / 2xxx 攻击 / 3xxx 防御与受击 / 4xxx 行为与状态 /
/// 5xxx 形态与动画 / 6xxx 控制与增益 / 7xxx 资源。
///
/// 植物和僵尸共用一个表——毕竟它们挂的是同一套 EntityComponent。
/// </summary>
public static class ComponentLibrary
{
    public static EntityComponent Create(int id)
    {
        switch (id)
        {
            case 1001:
                return new NormalMove();
            case 2001:
                return new NormalAttack();
            case 3001:
                return new NormalDamage();
            case 5001:
                return new NormalZombieAnimation();
            default:
                GD.PushError($"[装配] 没有编号为 {id} 的组件。");
                return null;
        }
    }
}
