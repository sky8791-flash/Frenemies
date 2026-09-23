using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShadowDuel.Core;

/// 决斗落地的赌注：名次环形链上每对相邻玩家「一进一出」换一张牌。
/// 牌库规模不变，所以胜者不会被稀释、败者也不会白赚一次删牌。
public static class TributeChain
{
    public static async Task RunAsync(IReadOnlyList<Player> ranking)
    {
        int n = ranking.Count;
        if (n < 2)
        {
            return;
        }

        // 两人局只做一步（否则同一个配对会被走两遍）；三人及以上才是 1↔2、2↔3、末↔1 的环。
        int steps = (n == 2) ? 1 : n;
        for (int i = 0; i < steps; i++)
        {
            await SwapAsync(ranking[i], ranking[(i + 1) % n]);
        }
    }

    private static async Task SwapAsync(Player taker, Player from)
    {
        List<CardModel> fromDeck = PileType.Deck.GetPile(from).Cards.ToList();
        List<CardModel> takerDeck = PileType.Deck.GetPile(taker).Cards.ToList();
        if (fromDeck.Count == 0 || takerDeck.Count == 0)
        {
            Log.Info($"[Frenemies] tribute skipped, empty deck (taker={taker.NetId} from={from.NetId})");
            return;
        }

        // 两次选择都要在转移之前取完，否则第一笔转移会改变第二个人的可选列表。
        CardModel? taken = await PickAsync(fromDeck, taker, "SHADOW_DUEL_TAKE");
        CardModel? given = await PickAsync(takerDeck, taker, "SHADOW_DUEL_GIVE");
        if (taken == null || given == null)
        {
            Log.Info($"[Frenemies] tribute aborted before transfer, taken={taken?.Id.Entry ?? "none"} given={given?.Id.Entry ?? "none"}");
            return;
        }

        await CardPileCmd.GiveToAnotherPlayer(taken, taker, PileType.Deck);
        await CardPileCmd.GiveToAnotherPlayer(given, from, PileType.Deck);
        Log.Info($"[Frenemies] tribute {taker.NetId} <- {taken.Id.Entry} / -> {from.NetId} {given.Id.Entry}");

        // GiveToAnotherPlayer 只返回 Task，内部的 ShouldAddToDeck 否决不会反馈给我们，
        // 而否决前源牌堆已经把牌移走了 —— 那种情况下牌会变成孤儿。所以事后自查并显式报错。
        VerifyLanded(taken, taker);
        VerifyLanded(given, from);
    }

    private static void VerifyLanded(CardModel card, Player expectedOwner)
    {
        if (!PileType.Deck.GetPile(expectedOwner).Cards.Contains(card))
        {
            Log.Error($"[Frenemies] card {card.Id.Entry} did not land in {expectedOwner.NetId}'s deck after transfer");
        }
    }

    private static async Task<CardModel?> PickAsync(
        IReadOnlyList<CardModel> cards, Player chooser, string promptKey)
    {
        CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("card_selection", promptKey), 1);
        IEnumerable<CardModel> picked =
            await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), cards, chooser, prefs);
        return picked.FirstOrDefault();
    }
}
