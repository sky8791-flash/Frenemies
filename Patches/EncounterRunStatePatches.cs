using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShadowDuel.Core;
using ShadowDuel.Models;

namespace ShadowDuel.Patches;

/// 在 encounter 真正生成怪物之前，用权威的 runState 补一次参赛名单。
///
/// 为什么要补：影子数量由 GenerateMonsters() 读 DuelState.Participants 决定，
/// 而那份名单原本只由事件的 Accept() 写入。于是它有两处脆弱点：
/// 1) 决斗中途存退出再续档，静态状态丢失，Participants 为空 → 只生成一只影子、
///    且 Begin() 再没人调 → IsActive 为假 → 记账整个失效，决斗静默变成普通战斗。
/// 2) 任何绕过我们事件的进入方式（控制台强开、别的 mod 直接用这个 encounter）同理。
/// GenerateMonstersWithSlots 是两个进入点（CombatRoom.cs:201、EventCombatSynchronizer.cs:76）
/// 都会经过的地方，且它拿到的是真正的 IRunState。
[HarmonyPatch]
public static class EncounterRunStatePatches
{
    public static MethodBase? TargetMethod()
        => AccessTools.Method(typeof(EncounterModel), nameof(EncounterModel.GenerateMonstersWithSlots));

    public static void Prefix(EncounterModel __instance, IRunState runState)
    {
        if (__instance is not DuelEncounter || runState is NullRunState)
        {
            return;
        }

        DuelState.Begin(runState.Players);
    }
}
