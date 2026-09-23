using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShadowDuel.Core;

namespace ShadowDuel.Models;

/// 挂在每名玩家生物上：把你打向影子的伤害从"立刻生效"改成"记到回合末"。
/// 玩家真 HP 全程不动，所以事件里"恢复到进问号前的血量"自动成立。
public sealed class DuelBankPower : PowerModel
{
    /// 诊断计数：只读，不参与任何游戏状态，所以不影响跨端一致性。
    /// 用来区分"钩子根本没被调"和"调到了但条件不匹配"。
    public static int SeenHpLostCalls;
    public static int ShadowTargets;
    public static int DealerMatched;
    public static int Banked;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    private Player? OwnerPlayer => Owner.Player;

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (amount <= 0m || !DuelState.IsActive)
        {
            return amount;
        }

        SeenHpLostCalls++;
        bool isShadow = DuelState.IsShadow(target);
        if (isShadow)
        {
            ShadowTargets++;
        }

        // 只收属于本玩家的输出。这是 fold，先命中的监听者会把数值改成 0，
        // 因此不会重复入账；不匹配时必须原值透传，否则会吞掉别人的伤害。
        if (!isShadow || dealer?.Player != OwnerPlayer)
        {
            return amount;
        }

        DealerMatched++;
        DuelState.AddBank(target, amount);
        Banked++;
        return 0m;
    }

    /// 只能否决、不能放行。这里只挡"本人把自己的影子选成目标"。
    /// 注意 TargetType.AllEnemies 这类无目标选择的不经过这里，仍会波及自己的影子，属有意保留。
    public override bool ShouldAllowTargeting(Creature target)
    {
        var player = OwnerPlayer;
        if (player == null || !DuelState.IsActive || !LocalContext.IsMe(player))
        {
            return true;
        }

        return !ReferenceEquals(DuelState.OwnerOf(target), player);
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        var player = OwnerPlayer;
        if (side != CombatSide.Player || !DuelState.IsActive || player == null)
        {
            return;
        }

        // 有人打额外回合时 participants 可能不含其他玩家，必须早退，否则重复开奖。
        if (!participants.Contains(Owner))
        {
            return;
        }

        // 整个 side 会通知所有监听者，只让第一名玩家的 power 真正执行结算。
        // 用 NetId 比较，不依赖对象同一性。
        if (player.NetId != player.RunState.Players[0].NetId)
        {
            return;
        }

        var combat = Owner.CombatState;
        if (combat == null)
        {
            return;
        }

        await DuelState.SettleAsync(choiceContext, combat);
    }
}
