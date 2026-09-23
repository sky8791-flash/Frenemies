using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShadowDuel.Core;

/// 决斗的临时账本。只在决斗战斗内有效，由事件在进/出战斗时 Begin/End。
/// 所有集合都按 CombatState.Players 的顺序读写，该顺序在各 peer 上一致。
public static class DuelState
{
    public const int TotalRounds = 7;

    /// 影子的兜底外观：角色没有对应 visuals 场景（第三方角色、Character 缺失）时使用。
    public const string FallbackVisualsPath = "res://scenes/creature_visuals/battle_friend_v1.tscn";

    private static readonly Dictionary<Creature, Player> OwnerByShadow = new();

    private static readonly Dictionary<Creature, decimal> Bank = new();

    private static readonly Dictionary<Player, Creature> ShadowByOwner = new();

    public static bool IsActive { get; private set; }

    public static int Round { get; private set; }

    /// 名次，第一名在前。每回合开奖后都会刷新，所以战斗因任何原因结束时它都是最新的。
    public static IReadOnlyList<Player> Ranking { get; private set; } = new List<Player>();

    /// 决斗参赛名单，顺序与 CombatState.Players 一致。
    /// 存在这里是因为影子的 VisualsPath 在 creature 绑定前就会被读，拿不到 CombatState。
    public static IReadOnlyList<Player> Participants { get; private set; } = new List<Player>();

    public static void Begin(IReadOnlyList<Player> players)
    {
        // 共享事件的 handler 会在每个 peer 上按玩家实例各跑一次，所以这里会被调 N 次。
        // 同一场决斗内的重复调用必须是无操作，否则后一次会把前一次已建立的影子绑定清掉。
        if (IsActive && Participants.Count == players.Count && Participants.SequenceEqual(players))
        {
            return;
        }

        IsActive = true;
        Round = 0;
        Participants = players;
        Ranking = new List<Player>();
        OwnerByShadow.Clear();
        ShadowByOwner.Clear();
        Bank.Clear();
    }

    public static void End()
    {
        IsActive = false;
        Participants = new List<Player>();
        OwnerByShadow.Clear();
        ShadowByOwner.Clear();
        Bank.Clear();
    }

    public static void Bind(Player player, Creature shadow)
    {
        OwnerByShadow[shadow] = player;
        ShadowByOwner[player] = shadow;
        Bank[shadow] = 0m;
    }

    public static bool IsShadow(Creature creature) => OwnerByShadow.ContainsKey(creature);

    public static Player? OwnerOf(Creature shadow) => OwnerByShadow.GetValueOrDefault(shadow);

    public static Creature? ShadowOf(Player player) => ShadowByOwner.GetValueOrDefault(player);

    /// <summary>
    /// 把玩家的格挡镜像到他的影子身上，让开奖伤害走原生 DamageBlockInternal 吸收。
    /// 收益不只是显示：格挡碎裂的表现和 AfterBlockBroken 钩子都回来了，
    /// 而且影子的 block 会在敌方回合开始时被原生 AfterTurnStart 清掉，天然充当回合重置。
    ///
    /// 只能在开奖时调，绝不能在当回合中途镜像给影子挂上格挡：
    /// CreatureCmd.Damage 是先吃格挡（:287）再轮到我们的记账钩子（:291），
    /// 影子中途有格挡的话，回合内那一击会先吃掉一层、我们只记到余额，
    /// 开奖再镜像一次又吃掉一层 —— 同一份护盾减免两遍。
    /// </summary>
    public static void MirrorBlockToShadow(Player player)
    {
        Creature? shadow = ShadowOf(player);
        if (shadow == null)
        {
            return;
        }

        int current = shadow.Block;
        if (current > 0)
        {
            shadow.LoseBlockInternal(current);
        }

        int target = player.Creature.Block;
        if (target > 0)
        {
            shadow.GainBlockInternal(target);
        }
    }

    public static string VisualsFor(Player player)
    {
        // 第三方角色在原版里没有同名 visuals 场景时直接拼路径会加载失败，
        // 宁可退化成通用小人也不让战斗创建炸掉。
        if (player.Character == null)
        {
            return FallbackVisualsPath;
        }

        string scenePath = $"res://scenes/creature_visuals/{player.Character.Id.Entry.ToLowerInvariant()}.tscn";
        return ResourceLoader.Exists(scenePath) ? scenePath : FallbackVisualsPath;
    }

    public static void AddBank(Creature shadow, decimal amount) => Bank[shadow] += amount;

    /// 排序后的账本快照，供两端逐字比对判断是否分叉。
    /// 必须用 InvariantCulture：两端区域设置不同的话，小数点会被格式化成逗号，造成假分叉报警。
    public static string SnapshotForDebug()
    {
        var parts = OwnerByShadow
            .Select(kv => $"{kv.Value.NetId}:{kv.Key.CurrentHp}+{Bank.GetValueOrDefault(kv.Key).ToString("0.##", CultureInfo.InvariantCulture)}")
            .OrderBy(s => s, StringComparer.Ordinal);
        return string.Join(" ", parts);
    }

    /// 回合末统一开奖。由第一名玩家身上的 DuelBankPower 唯一执行。
    public static async Task SettleAsync(PlayerChoiceContext ctx, ICombatState combat)
    {
        // 目标与伤害量全部先快照，之后不再读实时状态，否则先结算的人会把后结算的人的判定改掉。
        var snapshot = combat.Enemies.Where(IsShadow).ToList();
        if (snapshot.Count == 0)
        {
            return;
        }

        Round++;

        var pending = new Dictionary<Creature, decimal>(snapshot.Count);
        foreach (var shadow in snapshot)
        {
            if (!Bank.TryGetValue(shadow, out decimal banked))
            {
                // IsShadow 为真却没入账，说明这只影子的 Bind 被跳过了，记账已不可信；按 0 处理并留证。
                Log.Error($"[Frenemies] settle found an unbound shadow (owner={OwnerOf(shadow)?.NetId ?? 0}), treating bank as 0");
                banked = 0m;
            }

            pending[shadow] = banked;
            Bank[shadow] = 0m;
        }

        // 先把格挡同步到影子身上，之后完全交给原生吸收，避免我们和引擎各算一套减免。
        foreach (Player player in combat.Players)
        {
            MirrorBlockToShadow(player);
        }

        foreach (var shadow in snapshot)
        {
            decimal incoming = pending[shadow];
            if (incoming <= 0m)
            {
                continue;
            }

            // 影子上镜像来的格挡在这里被真实消耗（Unblocked 才不吃格挡，Unpowered 只是不叠力量/易伤）。
            await CreatureCmd.Damage(ctx, shadow, incoming, ValueProp.Unpowered, null, null, null);
        }

        // 开奖即作废本回合的防御。原生清挡在下回合开始，若不在此清零，
        // 没被打到的人会留着一个"看起来还有用"的格挡数字，下一回合误以为它还在保护自己。
        foreach (Player player in combat.Players)
        {
            int block = player.Creature.Block;
            if (block > 0)
            {
                player.Creature.LoseBlockInternal(block);
            }
        }

        // 名次覆盖"曾经绑定过的所有影子"，不只是本轮还挂在敌人列表上的那些：
        // 提前归零的影子若被移出 Enemies，它的主人仍必须留在名次里，否则进贡链会静默少配对一人。
        // 平局用 NetId 做第二键，不依赖 Dictionary 的枚举顺序（那不是契约）。
        Ranking = OwnerByShadow
            .OrderByDescending(kv => kv.Key.CurrentHp)
            .ThenBy(kv => kv.Value.NetId)
            .Select(kv => kv.Value)
            .ToList();

        if (Round >= TotalRounds)
        {
            // 清场以自然结束战斗；此时不再关心伤害归属。
            foreach (var shadow in combat.Enemies.Where(IsShadow).ToList())
            {
                if (shadow.IsAlive)
                {
                    await CreatureCmd.Damage(ctx, shadow, shadow.CurrentHp + 1m, ValueProp.Unpowered, null, null, null);
                }
            }
        }
    }
}
