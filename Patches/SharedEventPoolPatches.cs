using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using ShadowDuel.Models;

namespace ShadowDuel.Patches;

/// 共享事件池是硬编码数组，这里追加我们的事件。ActModel 组池时只读 AllSharedEvents 一处，
/// 所以挂它即可覆盖所有 act，不必逐 act 打补丁。
[HarmonyPatch]
public static class SharedEventPoolPatches
{
    public static MethodBase? TargetMethod() => AccessTools.PropertyGetter(typeof(ModelDb), nameof(ModelDb.AllSharedEvents));

    public static void Postfix(ref IEnumerable<EventModel> __result)
    {
        __result = __result.Append(ModelDb.Event<DuelOfShadows>());
    }
}

/// 仅开发用：把"下一次抽到的问号事件"强制成决斗。
///
/// 为什么不靠改事件池实现：ActModel.GenerateRooms 的池子是
/// AllEvents.Concat(ModelDb.AllSharedEvents)，前半是该 act 自己的事件表，
/// 只替换共享表的话 act 事件仍在竞争，抽不抽得到全看运气。
/// 而且池子在进层时就生成了，之后再切开关对本层无效。
[HarmonyPatch]
public static class ForceDuelEventPatches
{
    /// duelpool 命令切换。只影响本地开发验证，正式版应保持 false。
    public static bool DevOnlyDuel;

    public static MethodBase? TargetMethod() => AccessTools.Method(typeof(ActModel), nameof(ActModel.PullNextEvent));

    public static void Postfix(ref EventModel __result)
    {
        if (DevOnlyDuel)
        {
            __result = ModelDb.Event<DuelOfShadows>();
        }
    }
}
