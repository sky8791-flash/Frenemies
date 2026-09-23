using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using ShadowDuel.Core;

namespace ShadowDuel.Models;

/// 决斗的记分牌：血量代表这名玩家在决斗中的处境，归零即落败。永不主动行动。
public sealed class ShadowMonster : MonsterModel
{
    public const int DuelHp = 100;

    /// 对应 CombatState.Players 中的下标，即这只影子代表谁。
    public int DuelIndex { get; set; }

    public override int MinInitialHp => DuelHp;

    public override int MaxInitialHp => DuelHp;

    public override bool ShouldShowInCompendium => false;

    /// <summary>
    /// 两只影子是同一个类的两个实例，默认的 Title 取自同一个本地化键，
    /// 于是名字牌上都写着"你的影子"，完全分不出谁是谁。这里按所代表的玩家改名。
    /// 用带转义的 GetPlayerName：Steam 显示名是不可信输入，含方括号会打坏 BBCode。
    /// </summary>
    public override LocString Title
    {
        get
        {
            var run = RunManager.Instance;
            var participants = DuelState.Participants;
            if (!run.IsInProgress || DuelIndex < 0 || DuelIndex >= participants.Count)
            {
                return base.Title;
            }

            string ownerName = PlatformUtil.GetPlayerName(run.NetService.Platform, participants[DuelIndex].NetId);
            LocString title = new LocString("monsters", "SHADOW_MONSTER.name_of");
            title.Add("Owner", ownerName);
            return title;
        }
    }

    /// <summary>
    /// 影子长得像它代表的那个人。AssetPaths 会在 creature 绑定之前就读这里，
    /// 而 MonsterModel.Creature 是抛异常的 getter，所以只能走 DuelState 里存的名单。
    /// </summary>
    protected override string VisualsPath
    {
        get
        {
            var participants = DuelState.Participants;
            if (DuelIndex < 0 || DuelIndex >= participants.Count)
            {
                return DuelState.FallbackVisualsPath;
            }

            return DuelState.VisualsFor(participants[DuelIndex]);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState idle = new MoveState("SHADOW_IDLE", (IReadOnlyList<Creature> _) => Task.CompletedTask);
        idle.FollowUpState = idle;
        return new MonsterMoveStateMachine(new List<MonsterState> { idle }, idle);
    }

    public override async Task AfterAddedToRoom()
    {
        IReadOnlyList<Player>? players = Creature.CombatState?.Players;
        if (players == null)
        {
            return;
        }

        if (DuelIndex >= 0 && DuelIndex < players.Count)
        {
            DuelState.Bind(players[DuelIndex], Creature);
        }

        // 每只影子都会进一次这里，只让 0 号负责上 power，否则每名玩家会叠 N 份。
        if (DuelIndex != 0)
        {
            return;
        }

        foreach (Player player in players)
        {
            // 必须走这个泛型重载：它内部用无参 ToMutable()，在 power 绑上 creature 之后才设层数。
            // 直接 ToMutable(1) 会在绑定前触发 SetAmount，空引用。
            await PowerCmd.Apply<DuelBankPower>(
                new ThrowingPlayerChoiceContext(), player.Creature, 1m, Creature, null);
        }

        // 上 power 是整套机制的前提，而且它静默失败时症状和"没生效"完全一样，所以当场回读。
        int withPower = players.Count(p => p.Creature.Powers.Any(x => x is DuelBankPower));
        Log.Info($"[Frenemies] shadows ready: players={players.Count} bankPowerOn={withPower}");
    }
}
