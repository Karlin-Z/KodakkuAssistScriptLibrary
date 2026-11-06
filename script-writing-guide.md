# KodakkuAssist 脚本编写指南

当单纯的条件判断不够用、需要更复杂的逻辑时，用脚本实现。脚本是触发器功能的完全上位替代，简单功能也推荐用脚本写，方便后续扩展。脚本用 [C#](https://learn.microsoft.com/zh-cn/dotnet/csharp/) 编写，需要最基础的 C# 知识。

- 完整可运行的示例：仓库根目录的 [SimpleScript.cs](./SimpleScript.cs)；实战写法见 `Karlin-Z/` 等作者目录。
- 本文只讲常用部分。**最新、最全的 API 以插件自带的自动化文档为准**，见文末「[自动化文档与开发工具](#自动化文档与开发工具)」。

## 最小结构

一个脚本由三部分组成：引用命名空间、一个带 `[ScriptType]` 的 `public` 类（非抽象、有公开无参构造函数）、类里的属性与方法。

```cs
using System;
using System.Collections.Generic;
using KodakkuAssist.Script;              // ScriptType / ScriptMethod / UserSetting / ScriptData / ScriptAccessory
using KodakkuAssist.Module.GameEvent;    // Event / EventTypeEnum
using KodakkuAssist.Module.Draw;         // DrawModeEnum / DrawTypeEnum / 绘制相关枚举

namespace MyScripts;

[ScriptType(name: "示例脚本", territorys: [179, 979],
            guid: "d3b6a9b4-1e0e-4e0c-b7c7-ff1fce0e6cf2",
            version: "0.0.0.3", author: "Karlin")]
public class MyScript
{
    int count = 0;

    public void Init(ScriptAccessory accessory) => count = 0;

    [ScriptMethod(name: "医济计数", eventType: EventTypeEnum.StartCasting,
                  eventCondition: ["ActionId:133"])]
    public void OnMedica(Event @event, ScriptAccessory accessory)
    {
        count++;
        accessory.Method.SendChat($"/e 第 {count} 次医济");
    }
}
```

- 脚本编译**不带隐式 using**：`List`/`Dictionary` 要 `using System.Collections.Generic;`，LINQ 要 `using System.Linq;`，`Vector2/3/4` 要 `using System.Numerics;`。
- 可引用的程序集：.NET 运行时、WinForms、Dalamud（含 FFXIVClientStructs、Lumina）以及插件自身；允许 `unsafe`。
- `[ScriptMethod]` 方法必须 `public`，签名固定为 `void (Event, ScriptAccessory)`；方法名随意，用户看到的是 `name`。
- 不要写 `async void` 方法——异常不会被插件捕获记录；需要异步就在方法里 `Task.Run` 并自己 `try/catch`。
- 每条游戏事件只匹配同类型的方法，全部 `eventCondition` 满足才执行。
- 方法在**线程池**上执行，多个方法可能并发读写同一字段，注意加锁。
- `Init` 可选，签名必须是 `public void Init(ScriptAccessory accessory)`；调用时机见「[生命周期与线程](#生命周期与线程)」。

## ScriptType 参数

| 参数 | 类型 | 必填 | 默认值 | 说明 |
| --- | --- | --- | --- | --- |
| `guid` | string | ✅ | — | 唯一标识；guid 相同会被视为同一个脚本，请用标准 GUID |
| `name` | string | 否 | `Default Script` | 脚本显示名 |
| `territorys` | uint[] | 否 | 空 | 生效的区域 ID（TerritoryType）；空表示所有区域 |
| `version` | string | 否 | `0.0.0.1` | 版本号，与在线库比较后提示更新 |
| `author` | string | 否 | `Unknown` | 作者 |
| `note` | string | 否 | 空 | 备注，显示给用户 |
| `updateInfo` | string | 否 | 空 | 更新说明 |

参数可以具名、顺序任意。长文本常用 `const` 引用：

```cs
[ScriptType(name: "绝凯夫卡", territorys: [1363], guid: "...", version: "0.0.0.4", updateInfo: UpdateInfo)]
public class 绝凯夫卡
{
    const string UpdateInfo = """
        精修 P1
        新增分摊击退指示
        """;
}
```

## 类成员

| 写法 | 说明 |
| --- | --- |
| `[UserSetting(note)]` | 在插件设置界面生成可编辑项；必须是 `public` 且有 `get; set;` 的属性，类型可被 JSON 序列化（数值 / bool / string / 枚举 / `Vector2/3/4` / `ScriptColor` 等） |
| `[ScriptData]` | `public` 属性，不显示在界面，跨脚本重载、插件重启保留 |
| `[ScriptTimeline(note)]` | `const string` 字段，脚本内嵌时间轴，见「[时间轴](#时间轴)」 |
| 普通字段 | 仅本次运行有效，脚本重载即重置 |

```cs
[UserSetting("爆炸等待时间（毫秒）")]
public int DelayMs { get; set; } = 1000;

[UserSetting("指示颜色")]
public ScriptColor Color { get; set; } = new() { V4 = new(1, 0, 0, 1) };

[ScriptData]
public Dictionary<uint, int> Counter { get; set; } = new();
```

> `ScriptData` 在切换区域与插件保存时写回，不是每次赋值立即落盘。

## ScriptMethod 参数

| 参数 | 类型 | 必填 | 默认值 | 说明 |
| --- | --- | --- | --- | --- |
| `eventType` | `EventTypeEnum` | ✅ | — | 匹配的事件类型 |
| `name` | string | 否 | 方法名 | 用户界面上显示的名称；同一脚本内不要重名（用于保存启用状态） |
| `eventCondition` | string[] | 否 | 全部匹配 | 形如 `"属性:值"` |
| `userControl` | bool | 否 | `true` | `false` 时用户不可见、不可禁用 |
| `suppress` | uint | 否 | `0` | 节流毫秒数；同一方法在此期间只触发一次，团灭重置时清空 |

### eventCondition 匹配

- 按**第一个** `:` 拆成键和值，`"ActionId:133"` 表示整串相等（区分大小写）。
- 值以 `regex:` 开头时按正则匹配：`"ActionId:regex:^(133|134)$"`。不自动加锚点，需要完全匹配请写 `^...$`。
- 值留空（`"Targetable:"`）或没有 `:` 时，该条件**永远满足**。
- 事件没有该属性时视为不匹配。
- 同一方法里键不能重复，否则加载时抛异常。
- **值的格式必须和事件里的一致**：ID 类（`SourceId`、`TargetId` 等）是至少 8 位的大写十六进制，如 `"SourceId:4000ABCD"`；`ActionId`、`StatusID`、`DataId` 等是十进制。不确定时查 `get_event_schema`（见文末）。

## Event 对象

```cs
var raw = @event["ActionId"];                       // string，不存在时返回 ""
if (@event.TryGet("TargetId", out var tid)) { }    // 区分「不存在」与「空」
var sid = @event.SourceId;                          // ulong，已按十六进制解析
var pos = @event.SourcePosition;                    // Vector3
```

| 成员 | 说明 |
| --- | --- |
| `this[name]` / `TryGet(name, out v)` / `Has(name)` | 取原始字符串属性 |
| `PropertiesCopy` | 全部属性的副本，调试时可打印 |
| `Type` / `DateTime` / `ActLog` / `Info` | 事件类型、产生时间、ACT 日志行、说明 |
| `EventTriggerSource` | `GameEvent`（游戏）/ `FakeEvent`（伪造）/ `Script`（脚本 `PostEvent`） |
| `SourceId` / `TargetId` | `ulong`，按十六进制解析，失败为 0 |
| `SourcePosition` / `TargetPosition` / `EffectPosition` | `Vector3`，解析失败为零向量 |
| `SourceRotation` / `TargetRotation` | `float` 弧度 |
| `ActionId` / `StatusId` / `StatusStackCount` / `StatusParam` | `uint`，按十进制解析 |
| `Id` | `uint`，**按十进制解析**；`Tether`、`TetherRemove`、`TargetIcon`、`SetObjPos` 的 `Id` 是十六进制，请用 `Convert.ToUInt32(@event["Id"], 16)` |

想确认某事件有哪些属性：在插件 GameEvent 页面点开具体日志查看，或临时打印
`accessory.Log.Debug(string.Join(",", @event.PropertiesCopy))`。

### 事件类型

多数事件带 `SourceId` / `SourceName` / `SourcePosition` / `SourceRotation`，有目标时带对应的 `Target*`；`*Position` 是 JSON 字符串，形如 `{"X":1.00,"Y":2.00,"Z":3.00}`。下表列出各事件在此之外的主要属性（括号内为值格式，未注明为十进制或原文）：

| 事件 | 说明 | 主要属性 |
| --- | --- | --- |
| `StartCasting` | 开始读条 | `ActionId` `Duration`(秒，两位小数) `DurationMilliseconds` `EffectPosition` `SourceDataId` `TargetDataId` |
| `ActionEffect` | 技能生效（每个目标一条） | `ActionId` `EffectPosition` `TargetIndex` `Damage` `Heal` `Overheal` `HitType` `Sequence` `OwnerId`(X8) |
| `CancelAction` | 读条被取消 / 打断 | `ActionId` `Interrupt` |
| `StatusAdd` / `StatusRemove` | 获得 / 失去状态 | `StatusID` `StackCount` `Param` `Duration` `DurationMilliseconds` |
| `StatusList` | 状态列表刷新 | — |
| `EffectResult` | 效果结算 | `Hp` `HpBefore` `MaxHp` `Shield` `ShieldBefore` `Sequence` |
| `DoT` / `HoT` | 持续伤害 / 治疗跳 | `StatusId`(X4) `Amount` `Hp` `MaxHp` |
| `SimulatedDoT` / `SimulatedHoT` | 合并跳按来源分摊后的模拟跳 | `StatusID` `ActionId` `Amount` `Potency` `Share` `Estimated` |
| `Death` | 单位死亡 | `SourceDataId` `TargetDataId` |
| `AddCombatant` / `RemoveCombatant` | 单位出现 / 消失 | `DataId` |
| `ObjectChanged` | 对象变化 | `Operate` `DataId` `Kind` `Visible` `EventState` `OldEventState` |
| `Targetable` | 可选中状态变化 | `Targetable` `DataId` |
| `TargetIcon` | 头顶点名图标 | `Id`(X4) |
| `Marker` | 攻击 / 锁链 / 禁止等头顶标记 | `Id` `Operate` |
| `Tether` / `TetherRemove` | 连线 / 连线解除 | `Id`(X4)；解除另有 `Slot` |
| `Director` | 导演指令 | `Instance`(X8) `Command`(X8) `CommandName` `Param1`~`Param4` |
| `EnvControl` | 环境控制（mapEffect） | `DirectorId`(X8) `State`(X8) `Id`(X8) `Index` `Flag` `Parm4` |
| `SetObjPos` | 对象位置变化（morelog 106） | `Id`(X4) `MorlogId` |
| `MorelogCompat` | morelog 兼容事件 | `MorlogId` `Id` `Index` `DirectorId` |
| `ObjectEffect` | 场地 / 单位特效 | `Id1` `Id2` |
| `ObjectVfx` | 对象 VFX | `Id` |
| `VfxEvent` | VFX 事件 | `Id` `Handle` `Type` `Rotation` `Scale` `Speed` `EffectPosition` |
| `PlayActionTimeline` | 动作演出 | `Id` |
| `KnockBack` | 击退 | `Distance` `Duration` `DurationMilliseconds` `Rotation` |
| `NpcYell` | NPC 喊话 | `Id` |
| `Chat` | 聊天消息 | `Sender` `Message` `Type` |
| `SystemLog` | 系统日志 | `LogMessageId` `LogParam1`~`LogParam3` |
| `Countdown` | 倒计时 | `Type` `Duration` `DurationMilliseconds` |
| `CombatChanged` | 战斗状态变化 | `Type`（如 `ResetCombat`、`TerritoryChanged`）`InCombat` `Reason` |
| `Waymark` | 地面标点 | `Id` `Operate` `Position` |
| `UpdateHpMp` | 血量 / 魔力更新 | `Hp` `HpBefore` `HpAfter` `MaxHp` `Mp` `Cp` `Gp` |
| `UpdateParty` | 队伍成员更新 | `PartyLength` |
| `Territory` | 切换区域 | `PlaceId` `PlaceName` |
| `ChangeMap` | 地图切换 | `MapId` |
| `ChangePlayer` | 主视角玩家切换 | — |
| `PlayerStats` / `Gauge` | 玩家属性 / 职业量谱 | — |
| `TimeLineEvent` | 时间轴 `event` 行触发 | 时间轴里写的参数，见「[时间轴](#时间轴)」 |

> 这张表是摘要。字段表由插件构建时扫描源码自动生成，完整字段与格式请用 `get_event_schema` 查询。

## ScriptAccessory

`accessory` 分三块：`Data`（数据）、`Method`（操作）、`Log`（日志）。每个脚本拿到的是自己专属的实例，绘图删除、危险区默认按脚本隔离。

### accessory.Data

| 成员 | 说明 |
| --- | --- |
| `Me` | 主视角玩家 EntityId（`uint`；录像回放时为回放主视角） |
| `MyObject` | 主视角玩家对象（`IPlayerCharacter?`） |
| `Objects` | Dalamud `IObjectTable` |
| `PartyList` | 队伍成员 ID（`List<uint>`，按用户设定顺序） |
| `EnmityList` | 仇恨表 `EnemyId → 玩家 Id 列表`（`ulong`，只读） |
| `DefaultDangerColor` / `DefaultSafeColor` | 用户设置的危险 / 安全颜色（`Vector4`） |
| `GetDefaultDrawProperties()` | 新建一份绘制参数 `DrawPropertiesEdit` |

### accessory.Method

| 方法 | 说明 |
| --- | --- |
| `SendChat(text)` | 发送聊天，`\n` 换行 |
| `TextInfo(text, durationMs, isWarning = false, delay = 0)` | 屏幕中央横幅；`isWarning` 为红色 |
| `TTS(text, rate = 0, delay = 0)` | 语音朗读（引擎由用户选择）；`EdgeTTSCache(texts)` 预缓存文本 |
| `SendDraw(mode, type, props[, callback])` | 发送绘图，见「[绘图](#绘图)」 |
| `SendDrawJson(json[, callback])` | 用原生 JSON 发送绘图 |
| `RemoveDraw(nameRegex, allScripts = false)` | 按名称正则删除本脚本的绘图，返回被删名称列表 |
| `SendDangerZone(type, props)` / `RemoveDangerZone(nameRegex)` | 登记 / 删除危险区，见「[安全点](#安全点)」 |
| `GetSafePoint(nameRegex, options)` / `WatchSafePoint(nameRegex, options, onResult)` | 计算 / 持续计算安全点 |
| `RunDelayed(delayMs, action)` | 延迟执行；跟随录像倍速与暂停，录像回跳时取消。**推荐代替 `Task.Delay`** |
| `RunOnMainThreadAsync(action)` | 在游戏主线程执行 |
| `RegistFrameworkUpdateAction(action, onMainThread = false, deactivateExisting = true)` | 注册每帧执行，返回 guid |
| `UnregistFrameworkUpdateAction(guid)` / `ClearFrameworkUpdateAction(this)` | 注销单个 / 本类全部 |
| `UseAction(targetId, actionId, actionType = 1)` | 对目标使用技能 |
| `UseActionLocation(pos 或 targetId, actionId, actionType = 1)` | 使用地面技能 |
| `SelectTarget(targetId)` | 选中目标 |
| `Mark(actorId, markType, local = false)` / `MarkClear([localOnly])` | 设置 / 清除头顶标记 |
| `PostEvent(event)` | 发布一个事件（来源标为 `Script`），谨慎使用 |
| `HttpGet(url)` / `HttpPost(url, data)` | HTTP 请求；`HttpGet` 返回 `Task<string>` |
| `VfxMethod.*` | 原生 VFX，见「[VFX 与临时对象](#vfx-与临时对象)」 |
| `ObjectMethod.*` | 临时对象 |

> `markType` 取值：`Attack1`~`Attack8`、`Bind1`~`Bind3`、`Stop1` `Stop2`、`Square` `Circle` `Cross` `Triangle`、`None`。
> `EdgeTTS`、`Method.RemoveVfx` 已弃用，请分别改用 `TTS`、`VfxMethod.RemoveVfx`。

### accessory.Log

`Debug(text)` / `Error(text)`，输出到 Dalamud 日志，同时记入脚本开发接口（`get_script_log` 可读）。

## 绘图

```cs
var dp = accessory.Data.GetDefaultDrawProperties();
dp.Name = "钢铁";
dp.Owner = @event.SourceId;            // 绑定到对象
dp.Scale = new(10);
dp.Color = accessory.Data.DefaultDangerColor;
dp.DestoryAt = 5000;                   // 毫秒
accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
```

**绘制模式 `DrawModeEnum`**：`Default`（跟随用户设置，推荐）、`Imgui`、`Dx11`（世界直绘）、`Vfx`（原生特效）。

**图形 `DrawTypeEnum`**：

| 类型 | 说明 |
| --- | --- |
| `Circle` | 圆 |
| `Donut` | 月环，`InnerScale` 为内径 |
| `Fan` | 扇形，`Radian` 为角度（弧度） |
| `Rect` | 从中心向前延伸的矩形 |
| `Straight` | 穿过中心的矩形（辣尾） |
| `HotWing` | 双矩形（辣翅） |
| `Line` | 线条 |
| `Arrow` | 箭头 |
| `Displacement` | 位移指示 |
| `SightAvoid` | 背对指示 |
| `Text` | 世界坐标文字，任何模式都按 `Dx11` 绘制 |
| `Polygon` | 多边形（奇偶填充），任何模式都按 `Dx11` 绘制 |

**常用绘制参数 `DrawPropertiesEdit`**：

| 参数 | 默认 | 说明 |
| --- | --- | --- |
| `Name` | `Default` | 名称，供 `RemoveDraw` 使用 |
| `Owner` / `Position` | — | 绘制中心：绑定对象 ID / 固定坐标 |
| `TargetObject` / `TargetPosition` | — | 指向对象 / 坐标（朝向、距离缩放、连线终点） |
| `CentreResolvePattern` + `CentreOrderIndex` | `Normal` | 中心的解析方式，见下 |
| `TargetResolvePattern` + `TargetOrderIndex` | `Normal` | 指向目标的解析方式，见下 |
| `Offset` | — | 偏移（中心点减偏移） |
| `Rotation` / `FixRotation` | 0 / false | 朝向（弧度）；`FixRotation` 为 true 时不跟随 Owner 转向 |
| `Scale` | (5,5) | 大小（米）；`Text` 只用 X 作字号倍率 |
| `InnerScale` | — | 月环内径 |
| `Radian` | π/2 | 扇形角度 |
| `ScaleMode` | `None` | 标志位：`ByTime`/`XByTime`/`YByTime` 随时间填充，`XByDistance`/`YByDistance` 随到 Target 的距离伸缩 |
| `Color` / `TargetColor` | 危险色 / — | 起始颜色 / 随时间渐变到的颜色 |
| `Delay` / `DestoryAt` | 0 / 5000 | 延迟生效 / 持续时间（毫秒） |
| `FadeCentreObject` / `FadeCentrePosition` / `FadeDistance` / `FadeMode` | — | 超出距离后淡出 |
| `Wave` / `Decal` | true / true | 波浪扩散 / 贴地贴花 |
| `Erase` | false | 擦除掩码：图形内只擦不画（如在大圆里挖出安全区）；对危险区表示安全区 |
| `EdgeWall` | 0 | 沿边缘立起的光墙高度（米），仅 Dx11 |
| `ProjectDepth` | 0 | 贴地高度容差 / 贴墙深度（米），0 取默认 |
| `Text` / `TextPlacement` | — / `Screen` | `Text` 类型的内容与摆放：`Screen` `Ground` `Stand` `Billboard` `Wall` |
| `Polygon` / `PolygonSpace` | — / `Local` | 多边形环列表与坐标系（`Local` 相对中心随朝向旋转，`World` 世界坐标）；修改后需重新赋值 |

**解析方式 `PositionResolvePatternEnum`**：`Normal`（直接用 Owner/TargetObject）、`OwnerTarget`（Owner 的当前目标）、`OwnerEnmityOrder`（Owner 仇恨第 n 位）、`PlayerNearestOrder` / `PlayerFarestOrder`（离中心第 n 近 / 远的玩家）、`TetherSource` / `TetherTarget`（连线源 / 目标）。`OrderIndex` 从 1 开始。

需要逐帧变化时传回调（在线程池上执行），参数是本次绘制的实时属性：

```cs
accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp, d => d.Rotation += 0.01f);
```

示例：指向一仇的顺劈扇形、头顶文字：

```cs
var fan = accessory.Data.GetDefaultDrawProperties();
fan.Name = "顺劈";
fan.Owner = @event.SourceId;
fan.TargetResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
fan.TargetOrderIndex = 1;
fan.Scale = new(40);
fan.Radian = MathF.PI / 2;
fan.DestoryAt = 5000;
accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, fan);

var text = accessory.Data.GetDefaultDrawProperties();
text.Owner = accessory.Data.Me;
text.Text = "分摊";
text.Offset = new(0, -2.5f, 0);   // 放到头顶 Y 填负数
text.Scale = new(1.5f);
text.DestoryAt = 5000;
accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Text, text);
```

更复杂的写法可以看本仓库实战脚本，例如 [M1s.cs](./Karlin-Z/M1s.cs)。

## 安全点

插件可以根据你登记的危险区自动求最近的安全点。危险区**只参与计算、不绘制**，需要显示请另行 `SendDraw`。

```cs
var zone = accessory.Data.GetDefaultDrawProperties();
zone.Name = "P1-钢铁";
zone.Owner = @event.SourceId;
zone.Scale = new(10);
zone.DestoryAt = 6000;
accessory.Method.SendDangerZone(DrawTypeEnum.Circle, zone);

var result = accessory.Method.GetSafePoint("^P1-");
```

- 只有面积图形有效（`Circle` `Donut` `Fan` `Rect` `Straight` `HotWing`）；`Erase = true` 表示安全区。
- 生命周期同绘制（`Delay` / `DestoryAt`），换图清空。默认只计入本脚本登记的区域，`SafePointOptions.AllScripts` 可计入全部。
- `GetSafePoint` 对同一请求每帧增量重算并缓存，3 秒不再请求自动回收；`WatchSafePoint` 持续回调，返回句柄 `Dispose()` 停止，换图自动停止。
- `SafePointOptions` / `SafePointResult` 的字段请用 `get_script_api` 查询。

## VFX 与临时对象

`accessory.Method.VfxMethod`：

| 方法 | 说明 |
| --- | --- |
| `CreateOmen(index, scale, pos, rotation, color, destroyAt, setVfxStateFunc)` | 创建地面 Omen（Omen 表行号），返回句柄 |
| `CreateLockOn(index, targetId, color, destroyAt, setVfxStateFunc)` | 创建头顶锁定标记（Lockon 表） |
| `CreateChanneling(index, sourceId, targetId, color, destroyAt, setVfxStateFunc)` | 创建连线（Channeling 表） |
| `RemoveVfx(handle, vfxType)` | 移除，`VfxType`：`Omen` `LockOn` `Channeling` |
| `SetVfxColorRGBA` / `SetVfxColorRGB` / `SetVfxAlpha` / `SetVfxVisible` / `SetVfxScale` / `SetOmennPos` | 修改属性 |
| `SetVfxSpeed(handle, speed)` | 播放速度，需在主线程调用 |

`setVfxStateFunc` 为每帧回调 `(ref Vector4 color, ref Vector4 pos, ref Vector3 scale)`。

`accessory.Method.ObjectMethod`：`CreateEmptyChara(entityId)` 创建空角色对象（返回句柄），`DestoryChara(handle)` 销毁。

## 时间轴

在脚本里用 `const string` 字段内嵌时间轴，战斗中按事件同步推进，用户可在时间轴界面查看：

```cs
[ScriptTimeline("时间轴范例")]
const string SampleTimeline = """
      0.0  info   时间轴开始
      3.0  info   level=important duration=5 tts=true 死刑，注意减伤
     +2.0  info   text=分散站位
     10.0  sync   eventName=StartCasting ActionId=133 window=2.5 timeout=2.5
     12.0  event  kind=adds
     30.0  jump   targetTime=0
    """;

[ScriptMethod(name: "时间轴事件", eventType: EventTypeEnum.TimeLineEvent, eventCondition: ["kind:adds"])]
public void OnTimelineEvent(Event @event, ScriptAccessory accessory) { }
```

每行 `<时间> <类型> <参数...>`，`#` 开头为注释：

| 项 | 说明 |
| --- | --- |
| 时间 | `10.0` 绝对秒；`+2.0` 相对上一条 |
| `info` | 提示。裸文本即正文；参数 `text=` `level=`（`important` / `warning`）`duration=`（秒）`tts=true` |
| `sync` | 同步。`eventName` 必填（事件类型名），其余键是事件条件；`window` / `timeout` 单位秒，默认 2 |
| `event` | 触发脚本：参数写入一个 `TimeLineEvent` 事件，供 `ScriptMethod` 用 `eventCondition` 匹配 |
| `jump` | 跳转到 `targetTime`（秒）；写了 `eventName` 与条件则为条件跳转 |
| 转义 | 正文里的等号写 `\=`，反斜杠写 `\\` |

多语言：给时间轴加 `[ScriptTimelineI18n(ScriptTimelineLang.Xxx)]` 的 `Dictionary<string, string>` 字段，字段名以「时间轴字段名 + `I18n`」开头（字段名以 `Timeline` 结尾时也可去掉它，如 `SampleI18n_English`），键为原文、值为译文。插件按当前语言自动替换，切换语言即时生效。完整写法见 [SimpleScript.cs](./SimpleScript.cs)。

## 生命周期与线程

- **加载**：编译 → 实例化类 → 读取 `UserSetting` / `ScriptData` 与时间轴 → 调用 `Init`。
- **生效范围**：只有 `territorys` 包含当前区域（或为空）且未被用户禁用的脚本会收到事件。
- **重置**：团灭重置（`CombatChanged` 的 `ResetCombat`）和切换区域（`TerritoryChanged`）时，对当前生效的脚本再次调用 `Init`，并清空 `suppress` 计时。请在 `Init` 里重置所有战斗状态。
- **切换区域**时会清空所有 `RegistFrameworkUpdateAction` 注册的每帧方法、危险区与安全点监视。绘图不会随换图清空，靠 `DestoryAt` 到期或 `RemoveDraw` 删除；录像回跳时清空全部绘图。
- **卸载 / 重载**：类实现 `IDisposable` 时调用 `Dispose()`，请在这里释放 `Timer`、事件订阅等资源。
- **线程**：`Init`、`ScriptMethod`、绘图回调、`RunDelayed` 都在线程池执行；读写游戏内存或调用要求主线程的 API（如 `SetVfxSpeed`）请用 `RunOnMainThreadAsync` 或 `onMainThread: true`。
- **录像**：回放时事件照常分发，`Me` 是回放主视角。延时请用 `RunDelayed`，它跟随倍速、暂停，回跳时自动取消；`Task.Delay` / `Thread.Sleep` 不会。

## 自动化文档与开发工具

插件内置「脚本开发接口」（MCP 服务，默认 `http://127.0.0.1:18778/mcp`）。API 说明直接从插件程序集的 XML 注释读取，事件字段表在插件构建时扫描源码自动生成，**始终与当前插件版本一致**。可以接入 Claude Code、Cursor 等 AI 客户端，也方便自己查文档、调条件。

**启用**：插件设置 → IPC → 脚本开发接口 → 勾选「启用脚本开发接口」，点「复制 Claude Code 注册命令」在终端运行一次（其它客户端用「复制通用 JSON 配置」，HTTP 方式，请求头带 `X-KA-Token`）。热重载需另外勾选「允许变更类工具」。

| 工具 | 用途 |
| --- | --- |
| `ka_status` | 插件状态：区域、战斗、已加载 / 生效脚本数 |
| `get_script_api` | 查 API 签名与注释；不传参返回全部公开类型，`type` 可写短名如 `MethodAccessory`、`DrawPropertiesEdit`、`SafePointOptions` |
| `list_event_types` | 全部事件类型及字段名 |
| `get_event_schema` | 某事件的字段与值格式（十六进制 / 小数位 / JSON 坐标），附最近一条真实事件。**写 `eventCondition` 前先查** |
| `search_examples` | 在本地与在线脚本缓存里搜代码片段 |
| `new_script_template` | 生成带新 guid 的脚本骨架 |
| `compile_script` | 用插件同一套编译器编译，返回诊断 |
| `lint_script` | 静态检查：guid 冲突、方法可见性与签名、条件键名 / 格式 / 正则、`UserSetting` 可见性、`async void` 等运行时会静默失效的问题 |
| `list_scripts` | 已加载脚本、方法与用户设置值 |
| `events_poll` | 增量读取实时事件 |
| `test_condition` | 拿最近的真实事件试一组 `eventCondition`，返回命中情况与失败原因 |
| `explain_dispatch` | 解释某个方法为什么触发 / 没触发 |
| `get_script_log` | 读 `accessory.Log` 输出与方法异常（带栈） |
| `reload_script` | 热重载本地脚本（变更类） |

推荐流程：`get_event_schema` 确认字段格式 → 写脚本 → `compile_script` + `lint_script` 修到无错 → 进副本或放录像，`test_condition` 调条件 → `reload_script` 热重载 → `explain_dispatch` / `get_script_log` 确认效果。

不用 MCP 时，也可以在 IDE 里引用插件目录下的 `KodakkuAssist.dll`，同目录的 `KodakkuAssist.xml` 会提供智能提示与注释。

## 提交到本库

- 一个 `.cs` 只放一个脚本类。
- guid 必须唯一；更新脚本时保持 guid 不变、递增 `version`，并写 `updateInfo`。
- 提交前至少用 `compile_script` / `lint_script` 过一遍，或在游戏内本地加载确认无报错。
