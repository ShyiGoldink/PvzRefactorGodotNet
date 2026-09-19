using Godot;

/// <summary>
/// 按编号创建组件。配置里 components 的键就是这个编号，加新组件时在这里补一行。
///
/// 编号段位：1xxx 移动 / 2xxx 攻击 / 3xxx 防御与受击 / 4xxx 行为与状态 /
/// 5xxx 形态与动画 / 6xxx 控制与增益 / 7xxx 资源。
///
/// 植物和僵尸共用一个表——它们挂的是同一套 EntityComponent。
/// 同一个类可以登记在好几个编号下：路障、旗帜、铁桶三种僵尸的动作和普通僵尸一模一样，
/// 差别只在图集，所以它们各占一个动画编号、类却是同一个。
/// </summary>
public static class ComponentLibrary
{
    public static EntityComponent Create(int id)
    {
        switch (id)
        {
            // ---- 移动 ----
            case 1001:
                return new NormalMove();

            // ---- 攻击 ----
            case 2001:
                return new NormalAttack();
            case 2002:
                return new Shooter();
            case 2003:
                return new PotatoMine();

            // ---- 防御与受击 ----
            case 3001:
                return new NormalDamage();
            case 3002:
                return new HeadArmor();

            // ---- 形态与动画 ----
            case 5001: // 普通僵尸
            case 5002: // 路障僵尸
            case 5003: // 旗帜僵尸
            case 5004: // 铁桶僵尸
                return new NormalZombieAnimation();
            case 5005:
                return new SunflowerAnimation();
            case 5006:
                return new PeashooterAnimation();
            case 5007:
                return new PotatoMineAnimation();
            case 5008:
                return new WallNutAnimation();

            // ---- 资源 ----
            case 7001:
                return new SunProducer();

            default:
                GD.PushError($"[装配] 没有编号为 {id} 的组件。");
                return null;
        }
    }
}
