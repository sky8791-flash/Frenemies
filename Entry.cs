using System;
using System.Linq;
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ShadowDuel;

[ModInitializer(nameof(Init))]
public class Entry
{
    public const string HarmonyId = "com.frenemies.mod";

    public static void Init()
    {
        var harmony = new Harmony(HarmonyId);
        PatchEachClassSeparately(harmony);
        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
        Log.Info("[Frenemies] mod initialized");
    }

    /// <summary>
    /// 游戏更新一旦改名补丁目标（方法名、属性名、参数名任一），PatchAll 会整体抛异常，
    /// 整个 mod 直接失效。逐类打补丁，把失败隔离到单个类并留下可定位的错误日志。
    /// </summary>
    private static void PatchEachClassSeparately(Harmony harmony)
    {
        var patchTypes = typeof(Entry).Assembly.GetTypes()
            .Where(t => t.IsDefined(typeof(HarmonyPatch), inherit: false));
        foreach (var type in patchTypes)
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
                Log.Info($"[Frenemies] patched {type.Name}");
            }
            catch (Exception e)
            {
                Log.Error($"[Frenemies] failed to patch {type.FullName} (game update renamed the target?): {e}");
            }
        }
    }
}
