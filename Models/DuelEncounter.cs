using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using ShadowDuel.Core;

namespace ShadowDuel.Models;

public sealed class DuelEncounter : EncounterModel
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool ShouldGiveRewards => false;

    public override IEnumerable<MonsterModel> AllPossibleMonsters => new List<MonsterModel>
    {
        ModelDb.Monster<ShadowMonster>(),
    };

    /// 影子数量在这里就定下来，不在 AfterAddedToRoom 里动态加：
    /// CreatureCmd.Add 要求 CombatManager.IsInProgress 已经为真，而那个时点还没有。
    /// Participants 由事件在 EnterCombat 之前写入，所以这里拿得到人数。
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        int count = DuelState.Participants.Count;
        if (count < 1)
        {
            count = 1;
        }

        var monsters = new List<(MonsterModel, string?)>(count);
        for (int i = 0; i < count; i++)
        {
            ShadowMonster shadow = (ShadowMonster)ModelDb.Monster<ShadowMonster>().ToMutable();
            shadow.DuelIndex = i;
            monsters.Add((shadow, null));
        }

        return monsters;
    }
}
