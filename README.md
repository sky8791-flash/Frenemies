# 该死的敌人竟敢伪装成队友 (Frenemies)

《杀戮尖塔 2》(Slay the Spire 2) 联机 mod：一个新的共享问号事件「阴影决斗 / Duel of Shadows」。
队友在同一张地图上互相角力，回合末统一开奖。打的是彼此的影子，掉不了真血——输的人只输牌。

- Mod 版本：0.2.0
- 游戏最低版本：0.111.0
- 平台：Windows（构建脚本按 Windows 路径写死）

## 玩法

- 队伍里会出现一个新的共享问号事件「阴影决斗」，需要至少 2 名玩家，单人局不会出现。
- 走上台座后全员进入决斗：每个玩家对应一只 100 点生命的影子。你无法把自己的影子选为目标，只能打别人的影子。
- 对影子造成的伤害不会立即生效，而是全部记账，**回合末一次性开奖结算**，共 7 个回合。开奖时会把你当前的格挡镜像给影子参与吸收，开奖后你的格挡清零。
- 每回合开奖后按影子剩余血量排定名次。
- 决斗结束后进入「进贡链」：名次相邻的玩家两两换牌——胜者从败者的牌组里挑一张拿走，再从自己的牌里挑一张交给对方。牌组规模不变。

### 注意事项

- 影子不会行动，决斗场上也没有其他敌人。「不流血」只针对影子——自伤类卡牌仍然会掉真血。
- `TargetType.AllEnemies` 这类无目标选择牌会波及自己的影子，伤害记在自己账上，慎重使用 AOE。
- 若所有影子提前死亡，决斗提前结束并直接结算名次。
- 决斗中途存档退出再读档，决斗进度（轮数与记账）会重置。
- 影子外观沿用对应角色的原版模型；第三方角色没有对应模型时回退为通用小人。

## 安装

游戏从 `<游戏目录>/mods/Frenemies/` 读取 mod，需要三个产物：

| 文件 | 作用 |
| --- | --- |
| `Frenemies.dll` | 逻辑与 Harmony 补丁 |
| `Frenemies.json` | mod 清单（id、版本、`min_game_version`） |
| `Frenemies.pck` | 本地化文本与事件图片等 Godot 资源 |

本仓库不含构建产物，需要自行编译：

1. 装好 .NET 9 SDK、Godot 4.5.1 mono 版，以及已安装的本体游戏。
2. 编辑 `Frenemies.csproj` 里的 `Sts2Dir` 和 `GodotExe`，改成你本机的实际路径。
3. `dotnet build` — 编译并把 dll 与 json 复制到 `mods/Frenemies/`。
4. `dotnet build -t:ExportPck` — 用 headless Godot 导出 pck 到同一目录。

## 调试命令

需要在本体中开启调试命令：

- `dueldebug` — 查看本机记账与钩子状态，用于排查联机不同步（两端逐字比对快照）。
- `duelpool` — 强制下一个问号事件为阴影决斗，仅开发验证用。

## 目录结构

```
Entry.cs                mod 入口，逐类打补丁以隔离游戏更新导致的改名
Core/                   决斗账本（DuelState）与进贡链（TributeChain）
Models/                 事件、战斗、影子生物、记账 power
Patches/                Harmony 补丁：共享事件池、参赛名单兜底
ConsoleCommands/        dueldebug / duelpool
Frenemies/              打包进 pck 的资源：本地化(eng、zhs)、mod 图标
images/                 事件立绘
workshop/               Steam 创意工坊预览图
```

## 兼容性与联机

基于 Harmony 实现，未修改任何原版数据表，理论上与其他 mod 兼容。补丁失败会被隔离到单个类并写入错误日志，而不是让整个 mod 失效。

本 mod **影响玩法**（`affects_gameplay: true`）：进房前请确认房内所有玩家都已安装并启用。

## 版本

- **0.2.0** — 决斗延长为 7 回合；多项稳定性修复。
- **0.1.0** — 首发。

## English

A shared "?" event for co-op runs (2+ players): **Duel of Shadows**. Every player owns a 100 HP shadow — you can't target your own, only everyone else's. Damage dealt to shadows is banked and paid out in one lump sum at the end of each round, for 7 rounds. Afterwards players are ranked and adjacent ranks exchange cards: the winner takes one card from the loser's deck and gives one back in return. Deck sizes stay the same. No real HP is lost here (self-damage still hurts). All players must install the mod. Based on Harmony, no vanilla data tables touched.
