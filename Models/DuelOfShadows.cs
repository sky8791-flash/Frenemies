using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ShadowDuel.Core;

namespace ShadowDuel.Models;

public sealed class DuelOfShadows : EventModel
{
    /// EnterCombatWithoutExitingEvent 在非共享事件上会直接抛，且决斗语义本身就要求全员同场。
    public override bool IsShared => true;

    public override bool IsAllowed(IRunState runState) => runState.Players.Count >= 2;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new List<EventOption>
    {
        new EventOption(this, Accept, "DUEL_OF_SHADOWS.pages.INITIAL.options.ACCEPT"),
    };

    private Task Accept()
    {
        if (Owner?.RunState is not { } runState)
        {
            Log.Error("[Frenemies] Accept ran without an owner, aborting to avoid a stuck room");
            return Task.CompletedTask;
        }

        DuelState.Begin(runState.Players);
        Log.Info($"[Frenemies] duel starting, players={runState.Players.Count}");
        EnterCombatWithoutExitingEvent(ModelDb.Encounter<DuelEncounter>(), new List<Reward>(), shouldResumeAfterCombat: true);
        return Task.CompletedTask;
    }

    public override async Task Resume(AbstractRoom room)
    {
        Log.Info($"[Frenemies] ended, ranking={string.Join(" > ", DuelState.Ranking.Select(p => p.NetId))}");

        // Resume 在每个 peer 上都会对每名玩家实例各跑一次。只认 Players[0] 那一次，
        // 才是"每个 peer 恰好执行一遍"；链内部的跨玩家选择由 choice synchronizer 路由。
        // 用 NetId 而不是 ReferenceEquals：不假设事件实例的 Owner 与 RunState.Players 是同一对象。
        var owner = Owner;
        var players = owner?.RunState.Players;
        try
        {
            if (owner != null && players != null && players.Count > 0 && owner.NetId == players[0].NetId)
            {
                await TributeChain.RunAsync(DuelState.Ranking);
            }
        }
        finally
        {
            // 进贡链若被玩家断线、选择取消等异常打断，外层 RunSafely 会吞掉异常，
            // 不 finally 的话这个实例永远到不了 SetEventFinished，事件房间就卡死了。
            DuelState.End();
            SetEventFinished(L10NLookup("DUEL_OF_SHADOWS.pages.RESULT.description"));
        }
    }
}
