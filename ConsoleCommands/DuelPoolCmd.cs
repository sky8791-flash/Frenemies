using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using ShadowDuel.Patches;

namespace ShadowDuel.ConsoleCommands;

/// 开发用：切换"事件池里只有决斗"。
public class DuelPoolCmd : AbstractConsoleCmd
{
    public override string CmdName => "duelpool";

    public override string Args => "";

    public override string Description => "切换调试模式：下一次抽问号事件必定是决斗";

    /// 必须联网。两边事件池内容不同的话，抽到的问号事件会不一致，直接造成状态分叉。
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        ForceDuelEventPatches.DevOnlyDuel = !ForceDuelEventPatches.DevOnlyDuel;
        return new CmdResult(true, $"duelOnlyPool={ForceDuelEventPatches.DevOnlyDuel}");
    }
}
