using System.Linq;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShadowDuel.Core;
using ShadowDuel.Models;

namespace ShadowDuel.ConsoleCommands;

public class DuelDebugCmd : AbstractConsoleCmd
{
    public override string CmdName => "dueldebug";

    public override string Args => "";

    public override string Description => "打印决斗 mod 的注册与运行状态";

    /// 故意不联网：这份输出就是"我这台机器看到了什么"，是判断分叉的原始材料。
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        var all = ModelDb.AllEvents;
        var duel = all.FirstOrDefault(e => e.Id.Entry == "DUEL_OF_SHADOWS");

        // 两个实例共用同一份 godot.log、行会互相穿插，所以把自己的 NetId 打出来。
        var me = issuingPlayer?.NetId ?? 0;
        var hasPower = issuingPlayer?.Creature.Powers.Any(p => p is DuelBankPower) ?? false;
        var sb = $"me={me} duelRegistered={duel != null} inRun={RunManager.Instance.IsInProgress} bankPowerOn={hasPower}";

        if (!DuelState.IsActive)
        {
            return new CmdResult(true, sb + " duel=inactive");
        }

        // 两端逐字比对这一串，即可判断是否分叉，不需要截图。
        // hooks 那四个数是诊断用：seen=0 说明钩子没被调到；seen>0 而 banked=0 说明条件不匹配。
        var hooks = $"hooks {DuelBankPower.SeenHpLostCalls}/{DuelBankPower.ShadowTargets}/{DuelBankPower.DealerMatched}/{DuelBankPower.Banked}";
        return new CmdResult(true,
            $"{sb} duel=active round={DuelState.Round}/{DuelState.TotalRounds} ranked={DuelState.Ranking.Count} {hooks} {DuelState.SnapshotForDebug()}");
    }
}
