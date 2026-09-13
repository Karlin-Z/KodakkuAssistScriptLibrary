using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Common.Lua;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime.Attributes;
using System.Management;
using System.Reflection.Metadata;
using System.Net;
using System.Windows;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.Draw.Vfx.VfxNative;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.GameEvent.Types;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Script;
using Lumina.Data;
using Lumina.Excel.Sheets;
using Lumina.Excel.Sheets.Experimental;
using Newtonsoft.Json;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Linq;
using System.Net.Http;
using System.Net.Http;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Xml.Linq;
using static FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Delegates;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.DataCenterHelper;
using static KodakkuAssist.Script.ScriptAccessory;
using KodaMarkType = KodakkuAssist.Module.GameOperate.MarkType;


namespace Veever.A_Realm_Reborn.Guildhests;


[ScriptType(name: Name,
    territorys: [190, 191, 192, 214, 215, 216, 219, 220, 221, 222, 223, 298, 299, 300],
    guid: "3797d75b-cf0b-4de8-ad06-71f343af697b",
    version: Version, author: "Veever", note: NoteStr, updateInfo: UpdateStr)]
public class Guildhests
{

    const string NoteStr =
    $"""
    v{Version}
    ----- 请在使用前阅读注意事项 以及根据情况修改用户设置 -----
    1. 本脚本为全部行会令的合集，通过当前所在副本ID(CurrentTerritoryId)区分各行会令的机制。
    2. 如果需要某个机制的绘画或者哪里出了问题请在dc@我或者私信我
    3. 标点开关以及本地开关在用户设置里可自行选择
    鸭门
    ----------------------------------
    ----- Please read the notes before use and adjust user settings as needed. -----
    1. This single script covers every guildhest; each encounter's mechanics are gated by the current territory ID (CurrentTerritoryId).
    2. If you need a draw or notice any issues, @ me on DC or DM me.
    3. The toggle for marking and local marking can be adjusted in the user settings.
    4. TTS not support English, you can turn off TTS in user settings.
    Duckmen.
    """;

    const string UpdateStr =
    $"""
    v{Version}

    鸭门
    ----------------------------------

    Duckmen.
    """;

    private const string Name = "行会令合集 [Guildhests]";
    private const string Version = "0.0.0.1";
    private const string DebugVersion = "0.0.1.0";

    private const bool Debugging = true;

    [UserSetting("播报语言(language)")]
    public Language language { get; set; } = Language.Chinese;

    [UserSetting("绘图不透明度，数值越大越显眼(Draw opacity — higher value == more visible)")]
    public static float ColorAlpha { get; set; } = 1f;

    [UserSetting("文字横幅提示开关(Banner text toggle)")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关(TTS toggle)")]
    public bool isTTS { get; set; } = true;

    //[UserSetting("是否自动使用防击退(Auto anti-knockback)")]
    //public bool useAntiKnockBack { get; set; } = false;

    //[UserSetting("指路开关(Guide arrow toggle)")]
    //public bool isLead { get; set; } = true;

    [UserSetting("目标标记开关(Target Marker toggle)")]
    public bool isMark { get; set; } = true;

    [UserSetting("本地目标标记开关(打开则为本地开关，关闭则为小队) - Local target marker toggle (ON = local only, OFF = party shared)")]
    public bool LocalMark { get; set; } = true;

    [UserSetting("Debug开关, 非开发用请关闭 - Debug on/off (don't touch unless you know what you're doing)")]
    public bool isDebug { get; set; } = false;

    public enum Language
    {
        Chinese,
        English
    }

    public void DebugMsg(string str, ScriptAccessory sa)
    {
        if (!isDebug) return;
        sa.Log.Debug($"[DEBUG] {str}");
    }

    private ScriptAccessory _sa = null;
    private bool isDRTTS = false;

    private int attackCount_UTA;
    private int NotifyCount_BTS;
    private int attackCount_BTS;
    private readonly object NotifyLock_BTS = new object();
    private int pollenClusterTTSCount_PPP;
    private readonly object pollenClusterLock_PPP = new object();
    private int showTargetCount_SB;
    private readonly object showTargetLock_SB = new object();
    private int showTargetCount_FSN;
    private readonly object showTargetLock_FSN = new object();
    private int attackCount_ATV;
    private int attackCount_MTF;
    private int attackCount_SAC;
    private int drawEyeDelay_SAC;
    private int SleepCount_LLQ;
    private readonly object SleepLock_LLQ = new object();

    public void Init(ScriptAccessory sa)
    {
        sa.Log.Debug($"脚本 {Name} v{Version}{DebugVersion} 完成初始化.");
        sa.Method.RemoveDraw(".*");

        _sa = sa;

        _ = ScriptVersionChecker.CheckVersionAsync(
            sa,
            "3797d75b-cf0b-4de8-ad06-71f343af697b",
            Version,
            showNotification: true
        );


        sa.Method.ClearFrameworkUpdateAction(this);

        attackCount_UTA = 0;
        NotifyCount_BTS = 0;
        attackCount_BTS = 0;
        pollenClusterTTSCount_PPP = 0;
        showTargetCount_SB = 0;
        showTargetCount_FSN = 0;
        attackCount_ATV = 0;
        attackCount_MTF = 0;
        attackCount_SAC = 0;
        drawEyeDelay_SAC = 0;
        SleepCount_LLQ = 0;

        RefreshParams();
    }

    private void RefreshParams()
    {
    }

#region 各行会令脚本

#region 190 讨伐彷徨死灵 Under the Armor
// v0.0.0.5: 

[ScriptMethod(name: "—————— LV.10 讨伐彷徨死灵 (190) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_UTA(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "指路", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:515"])]
public void Navi_UTA(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 190) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "指路";
    dp.Owner = accessory.Data.Me;
    dp.Color = accessory.Data.DefaultSafeColor;
    dp.ScaleMode |= ScaleMode.YByDistance;
    dp.TargetPosition = @event.SourcePosition();
    dp.Scale = new(1);
    dp.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
}

[ScriptMethod(name: "删除指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:870"])]
public void delNavi_UTA(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 190) return;
    if (attackCount_UTA == 1)
    {
        accessory.Method.RemoveDraw(".*");
    }
    attackCount_UTA++;
}

[ScriptMethod(name: "钢铁正义", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:356"])]
public async void IronJustice_UTA(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 190) return;
    // 下踢
    if (IbcHelper.GetPlayerRole(accessory, accessory.Data.MyObject) == "Tank")
    {
        accessory.Method.UseAction(accessory.Data.MyObject.TargetObject.EntityId, 7540);
    }

    await Task.Delay(1000);

    // 扫腿
    if (IbcHelper.GetPlayerRole(accessory, accessory.Data.MyObject) == "Tank")
    {
        accessory.Method.UseAction(accessory.Data.MyObject.TargetObject.EntityId, 7863);
    }
        
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "Iron Justice";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(10);
    dp.Radian = float.Pi / 180 * 120;
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

#endregion 190 讨伐彷徨死灵 Under the Armor

#region 214 完成集团战训练 Basic Training: Enemy Parties
// v0.0.0.3: 

[ScriptMethod(name: "—————— LV.10 完成集团战训练！(214) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_BTP(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "飞翼斩", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:1015"])]
public void WingCutter_BTP(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 214) return;
    var dp = accessory.Data.GetDefaultDrawProperties();

    dp.Name = "Wing Cutter";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(7);
    dp.Radian = float.Pi / 180 * 60;
    dp.DestoryAt = 2200;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

[ScriptMethod(name: "发霉喷嚏", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:579"])]
public void MoldySneeze_BTP(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 214) return;
    var dp = accessory.Data.GetDefaultDrawProperties();

    dp.Name = "Moldy Sneeze";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(6);
    dp.Radian = float.Pi / 180 * 90;
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

[ScriptMethod(name: "副本指示", eventType: EventTypeEnum.Director, eventCondition: ["Instance:80032711"])]
public void Notify_BTP(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 214) return;
    if (isDebug) accessory.Method.SendChat($"/e {@event.Command()}");
    if (@event.Command() == 00000000)
    {
        if (isText) accessory.Method.TextInfo("在紫圈内等待敌人出现", duration: 4700, true);
        accessory.TTS("在紫圈内等待敌人出现", isTTS, isDRTTS);
    }
}

#endregion 214 完成集团战训练 Basic Training: Enemy Parties

#region 215 突破所有关门，讨伐最深处的敌人 Basic Training: Enemy Strongholds
// v0.0.0.3: 

[ScriptMethod(name: "—————— LV.15 突破所有关门，讨伐最深处的敌人！(215) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_BTS(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "吃力跳跃", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:1006"])]
public void LaboredLeap_BTS(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 215) return;
    if (isText) accessory.Method.TextInfo("钢铁，远离Boss", duration: 4000, true);
    accessory.TTS("钢铁，远离Boss", isTTS, isDRTTS);
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "吃力跳跃";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(9.5f);
    dp.DestoryAt = 3800;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

[ScriptMethod(name: "操纵杆画图指示", eventType: EventTypeEnum.MorelogCompat, eventCondition: ["MorlogId:101"])]
public async void Notify_BTS(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 215) return;
    await Task.Delay(1000);
    if (HelperExtensions.GetCurrentTerritoryId() != 215) return;
    lock (NotifyLock_BTS)
    {
        if (NotifyCount_BTS == 0)
        {
            List<Vector3> vectorList = new List<Vector3>
            {
            new Vector3(-377.86f, 24.19f, -562.98f),
            new Vector3(-392.50f, 24.94f, -487.56f)
            };

            for (int i = 0; i <= 1; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"操纵杆画图指示{i}";
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Position = vectorList[i];
                dp.ScaleMode = ScaleMode.ByTime;
                dp.Scale = new Vector2(0.8f);
                dp.DestoryAt = long.MaxValue;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }
        NotifyCount_BTS++;
    }
}

[ScriptMethod(name: "删除绘制", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:870"])]
public void delDraw_BTS(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 215) return;
    if (attackCount_BTS == 1)
    {
        accessory.Method.RemoveDraw(".*");
    }
    attackCount_BTS++;
}

#endregion 215 突破所有关门，讨伐最深处的敌人 Basic Training: Enemy Strongholds

#region 216 捕获金币龟 Hero on the Half Shell
// v0.0.0.3: 

[ScriptMethod(name: "—————— LV.15 捕获金币龟！(216) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_HHS(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "龟足踏", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:417"])]
public void TortoiseStomp_HHS(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 216) return;
    if (isText) accessory.Method.TextInfo("钢铁，远离Boss", duration: 3800, true);
    accessory.TTS("钢铁，远离Boss", isTTS, isDRTTS);

    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "龟足踏";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(11f);
    dp.DestoryAt = 3800;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

[ScriptMethod(name: "钢铁正义", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:448"])]
public void IronJustice_HHS(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 216) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "Stagnant Spray";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(8);
    dp.Radian = float.Pi / 180 * 120;
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

[ScriptMethod(name: "火元精指路", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:1130"])]
public void Navi_HHS(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 216) return;
    if (isText) accessory.Method.TextInfo("集中攻击火元精", duration: 4000, true);
    accessory.TTS("集中攻击火元精", isTTS, isDRTTS);

    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "指路";
    dp.Owner = accessory.Data.Me;
    dp.Color = accessory.Data.DefaultSafeColor;
    dp.ScaleMode |= ScaleMode.YByDistance;
    dp.TargetObject = @event.SourceId();
    dp.Scale = new(1);
    dp.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
}

[ScriptMethod(name: "删除绘制+成堆的香草指路", eventType: EventTypeEnum.Chat, eventCondition: ["Message:regex:^(用火元精核心在我这里点燃香草！| Use the core to light the herb patch lying before me. | ファイアスプライトの核で、\nアタイの目の前の香草に着火するんだ！)$"])]
public async void delDraw_HHS(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 216) return;
    accessory.Method.RemoveDraw(".*");
    await Task.Delay(50);

    if (isText) accessory.Method.TextInfo("点燃成堆的香草, 并将金币龟带进黄色催眠范围内", duration: 5000, true);
    accessory.TTS("点燃成堆的香草, 并将金币龟带进黄色催眠范围内", isTTS, isDRTTS);
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "成堆的香草指路";
    dp.Owner = accessory.Data.Me;
    dp.Color = accessory.Data.DefaultSafeColor;
    dp.ScaleMode |= ScaleMode.YByDistance;
    dp.TargetPosition = new Vector3(-167.92f, -29.31f, 84.42f);
    dp.Scale = new(1);
    dp.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
}

[ScriptMethod(name: "删除绘制+成堆的香草范围", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id2:1"])]
public async void delDraw2_HHS(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 216) return;
    if (isDebug) accessory.Method.SendChat("/e Im in");
    accessory.Method.RemoveDraw(".*");
    await Task.Delay(50);

    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"成堆的香草范围";
    dp.Color = new Vector4(255 / 255.0f, 255 / 255.0f, 0 / 255.0f, 0.8f);
    dp.Position = new Vector3(-167.92f, -29.31f, 84.42f);
    dp.Scale = new Vector2(5f);
    dp.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

[ScriptMethod(name: "删除绘制", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:832"])]
public async void delDraw3_HHS(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 216) return;
    accessory.Method.RemoveDraw(".*");
}

#endregion 216 捕获金币龟 Hero on the Half Shell

#region 191 驱除剧毒妖花 Pulling Poison Posies
// v0.0.0.3: 

[ScriptMethod(name: "—————— LV.20 驱除剧毒妖花！(191) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_PPP(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "花粉块", eventType: EventTypeEnum.MorelogCompat, eventCondition: ["MorlogId:101"])]
public async void pollenCluster_PPP(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 191) return;
    await Task.Delay(1000);
    if (HelperExtensions.GetCurrentTerritoryId() != 191) return;
    lock (pollenClusterLock_PPP)
    {
        if (pollenClusterTTSCount_PPP == 0)
        {
            if (isText) accessory.Method.TextInfo("不要踩在毒圈范围内, 后续紫圈同理，头铁可以开浴血类技能站在圈内猛猛输出", duration: 6000, true);
            accessory.TTS("不要踩在毒圈范围内, 后续紫圈同理", isTTS, isDRTTS);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "花粉块";
            dp.Color = new Vector4(138 / 255.0f, 43 / 255.0f, 251 / 226.0f, 0.5f);
            dp.Position = new Vector3(-154.83f, -0.63f, 171.10f);
            dp.Scale = new Vector2(11f);
            dp.DestoryAt = long.MaxValue;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        pollenClusterTTSCount_PPP++;
    }
}

[ScriptMethod(name: "删除绘制", eventType: EventTypeEnum.Director, eventCondition: ["Command:80000002", "Instance:00110002"])]
public async void delDraw_PPP(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 191) return;
    accessory.Method.RemoveDraw(".*");
}

[ScriptMethod(name: "闪雷直击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:325"])]
public void Thunderstrike_PPP(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 191) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "闪雷直击";
    dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(3, 11.2f);
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
}

[ScriptMethod(name: "嚎叫", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:336"])]
public async void delDraw2_PPP(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 191) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"嚎叫";
    dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(4.8f);
    dp.DestoryAt = 3000;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

#endregion 191 驱除剧毒妖花 Pulling Poison Posies

#region 192 消灭恶徒团伙寄生蜂团 Stinging Back
// v0.0.0.3: 标点开关以及本地开关在用户设置里可自行选择

[ScriptMethod(name: "—————— LV.20 消灭恶徒团伙寄生蜂团！(192) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_SB(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "目标显示", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(595|145|148|519|146|144|147)$"])]
public async void showTarget_SB(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 192) return;
    await Task.Delay(2000);
    if (HelperExtensions.GetCurrentTerritoryId() != 192) return;
    lock (showTargetLock_SB)
    {
        if (showTargetCount_SB == 0)
        {
            if (isText) accessory.Method.TextInfo("优先攻击红色医疗兵", duration: 5000, true);
            accessory.TTS("优先攻击红色医疗兵", isTTS, isDRTTS);
        }
        if (@event.DataId() == 595)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"{@event.SourceId()}";
            dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 0 / 255.0f, 1.0f);
            dp.Owner = @event.SourceId();
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Scale = new Vector2(0.5f);
            dp.DestoryAt = long.MaxValue;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
        else if (@event.DataId() == 144)
        {
            if (isText) accessory.Method.TextInfo("集中攻击红腹群点蜂兵", duration: 5000, true);
            accessory.TTS("集中攻击红腹群点蜂兵", isTTS, isDRTTS);
            if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"{@event.SourceId()}";
            dp.Color = new Vector4(255 / 255.0f, 255 / 255.0f, 0 / 226.0f, 1f);
            dp.Owner = @event.SourceId();
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Scale = new Vector2(0.5f);
            dp.DestoryAt = long.MaxValue;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
        else
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = $"{@event.SourceId()}";
            dp1.Color = accessory.Data.DefaultSafeColor;
            dp1.Owner = @event.SourceId();
            dp1.ScaleMode = ScaleMode.ByTime;
            dp1.Scale = new Vector2(0.5f);
            dp1.DestoryAt = long.MaxValue;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp1);
        }
        showTargetCount_SB++;
    }
}

[ScriptMethod(name: "删除绘制", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:regex:^(595|145|148|519|146|144|147)$"])]
public async void delDraw_SB(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 192) return;
    accessory.Method.RemoveDraw($"{@event.SourceId()}");
}

[ScriptMethod(name: "强突", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:866"])]
public void Heartstopper_SB(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 192) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "强突";
    dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(3, 3.4f);
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
}

[ScriptMethod(name: "删除绘制1", eventType: EventTypeEnum.Director, eventCondition: ["Command:00000001", "Instance:00180001"])]
public async void delDraw1_SB(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 192) return;
    accessory.Method.RemoveDraw(".*");
}

#endregion 192 消灭恶徒团伙寄生蜂团 Stinging Back

#region 220 讨伐梦幻之布拉奇希奥 All's Well that Ends in the Well
// v0.0.0.3: 

[ScriptMethod(name: "—————— LV.25 讨伐梦幻之布拉奇希奥！(220) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_ASW(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "恐怖视线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:513"])]
public void DreadGaze_ASW(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 220) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "恐怖视线";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(7);
    dp.Radian = float.Pi / 180 * 90;
    dp.DestoryAt = 3000;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

[ScriptMethod(name: "石化吐息", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:505"])]
public void Petribreath_ASW(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 220) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "石化吐息";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(7.6f);
    dp.Radian = float.Pi / 180 * 120;
    dp.DestoryAt = 2800;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

[ScriptMethod(name: "notify", eventType: EventTypeEnum.Chat, eventCondition: ["Message:regex:^(发现梦幻之布拉奇希奥了！\n不要慌，按一直以来的战术就好！| Without an ally to draw his magicks away from the others, Briaxio will unleash his bane on all of you! | 夢幻のブラキシオを確認した！\n盾役はその務めを果たし、味方を守り抜け！)$"])]
public async void notify_ASW(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 220) return;
    if (isText) accessory.Method.TextInfo("优先攻击 梦幻 布拉奇希奥", duration: 5000, true);
    accessory.TTS("优先攻击 梦幻 布拉奇希奥", isTTS, isDRTTS);

    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"notify";
    dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 0 / 255.0f, 1.0f);
    dp.Owner = 0x400050A2;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(0.5f);
    dp.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
}

[ScriptMethod(name: "麻痹", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:308"])]
public void Paralyze_ASW(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 220) return;
    if (isText) accessory.Method.TextInfo("注意打断 梦幻 布拉奇希奥", duration: 3700, true);
    accessory.TTS("注意打断 梦幻 布拉奇希奥", isTTS, isDRTTS);
}

[ScriptMethod(name: "删除绘制", eventType: EventTypeEnum.Director, eventCondition: ["Command:80000022", "Instance:80032717"])]
public async void delDraw_ASW(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 220) return;
    accessory.Method.RemoveDraw(".*");
}

#endregion 220 讨伐梦幻之布拉奇希奥 All's Well that Ends in the Well

#region 219 击溃哥布林炸弹军团 Flicking Sticks and Taking Names
// v0.0.0.3: 

[ScriptMethod(name: "—————— LV.25 击溃哥布林炸弹军团！(219) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_FSN(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "目标显示", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(531|530|587|533)$"])]
public async void showTarget_FSN(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 219) return;
    await Task.Delay(2000);
    if (HelperExtensions.GetCurrentTerritoryId() != 219) return;
    lock (showTargetLock_FSN)
    {
        if (@event.DataId() == 587)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"{@event.SourceId()}";
            dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 0 / 255.0f, 1.0f);
            dp.Owner = @event.SourceId();
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Scale = new Vector2(0.5f);
            dp.DestoryAt = long.MaxValue;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
        else if (@event.DataId() == 533)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"{@event.SourceId()}";
            dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 0 / 255.0f, 1.0f);
            dp.Owner = @event.SourceId();
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Scale = new Vector2(1f);
            dp.DestoryAt = long.MaxValue;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
        else
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = $"{@event.SourceId()}";
            dp1.Color = accessory.Data.DefaultSafeColor;
            dp1.Owner = @event.SourceId();
            dp1.ScaleMode = ScaleMode.ByTime;
            dp1.Scale = new Vector2(0.5f);
            dp1.DestoryAt = long.MaxValue;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp1);
        }
        showTargetCount_FSN++;

    }
}

[ScriptMethod(name: "删除绘制", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:regex:^(531|530|587)$"])]
public async void delDraw_FSN(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 219) return;
    accessory.Method.RemoveDraw($"{@event.SourceId()}");
}

[ScriptMethod(name: "bomb", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:596"])]
public async void bomb_FSN(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 219) return;
    var dp1 = accessory.Data.GetDefaultDrawProperties();
    dp1.Name = $"{@event.SourceId()}";
    dp1.Color = accessory.Data.DefaultDangerColor;
    dp1.Owner = @event.SourceId();
    dp1.ScaleMode = ScaleMode.ByTime;
    dp1.Scale = new Vector2(3f);
    dp1.DestoryAt = 5000;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp1);
}

[ScriptMethod(name: "删除绘制1", eventType: EventTypeEnum.Director, eventCondition: ["Command:40000002", "Instance:80032718"])]
public async void delDraw1_FSN(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 219) return;
    accessory.Method.RemoveDraw(".*");
}

#endregion 219 击溃哥布林炸弹军团 Flicking Sticks and Taking Names

#region 222 讨伐坑道中出现的妖异 Annoy the Void
// v0.0.0.3: 

[ScriptMethod(name: "—————— LV.30 讨伐坑道中出现的妖异！(222) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_ATV(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "定罪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:912"])]
public void Condemnation_ATV(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 222) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "Condemnation";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(7.3f);
    dp.Radian = float.Pi / 180 * 90;
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

[ScriptMethod(name: "notify", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:870", "SourceId:40011DA3"])]
public void notify_ATV(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 222) return;
    if (isDebug) accessory.Method.SendChat($"/e notifycount: {attackCount_ATV}");
    if (attackCount_ATV == 1)
    {
        if (isText) accessory.Method.TextInfo("集中攻击 布索, 注意交互蓝色火焰削弱BOSS", duration: 6000, true);
        accessory.TTS("集中攻击 布索, 注意交互蓝色火焰削弱BOSS", isTTS, isDRTTS);
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
    }
    attackCount_ATV++;
}

[ScriptMethod(name: "哀叫", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:602"])]
public async void OvertoneShriek_ATV(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 222) return;
    await Task.Delay(50);

    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"哀叫范围";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(4.7f);
    dp.DestoryAt = 2800;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

#endregion 222 讨伐坑道中出现的妖异 Annoy the Void

#region 221 讨伐污染源头魔界花 More than a Feeler
// v0.0.0.3: 

[ScriptMethod(name: "—————— LV.30 讨伐污染源头魔界花！(221) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_MTF(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "指路", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:1126"])]
public async void Navi_MTF(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 221) return;
    if (isDebug) accessory.Method.SendChat("/e im in Navi");
    await Task.Delay(1500);
    if (HelperExtensions.GetCurrentTerritoryId() != 221) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "指路";
    dp.Owner = accessory.Data.Me;
    dp.Color = accessory.Data.DefaultSafeColor;
    dp.ScaleMode |= ScaleMode.YByDistance;
    dp.TargetObject = @event.SourceId();
    dp.Scale = new(1);
    dp.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
}

[ScriptMethod(name: "删除指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:872"])]
public void delNavi_MTF(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 221) return;
    if (isDebug) accessory.Method.SendChat("/e im in delNavi");
    if (attackCount_MTF == 1)
    {
        accessory.Method.RemoveDraw("指路");
        if (isText) accessory.Method.TextInfo("集中攻击 剧毒魔花谭琳", duration: 6000, true);
        accessory.TTS("集中攻击 剧毒魔花谭琳", isTTS, isDRTTS);
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
    }
    attackCount_MTF++;
}

[ScriptMethod(name: "臭气", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:604"])]
public void BadBreath_MTF(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 221) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "BadBreath";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(15);
    dp.Radian = float.Pi / 180 * 120;
    dp.DestoryAt = 3000;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

[ScriptMethod(name: "腐坏泡泡", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:1027"])]
public void stalebubble_MTF(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 221) return;
    var dp1 = accessory.Data.GetDefaultDrawProperties();
    dp1.Name = "stalebubble";
    dp1.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 0 / 225.0f, 1f);
    dp1.Owner = @event.SourceId();
    dp1.ScaleMode = ScaleMode.ByTime;
    dp1.Scale = new Vector2(0.5f);
    dp1.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp1);
}

[ScriptMethod(name: "腐坏泡泡销毁", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:1027"])]
public void Delstalebubble_MTF(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 221) return;
    accessory.Method.RemoveDraw($"{@event.SourceId()}");
}

#endregion 221 讨伐污染源头魔界花 More than a Feeler

#region 223 注意无敌的眷属，讨伐大型妖异 Shadow and Claw
// v0.0.0.4: 

[ScriptMethod(name: "—————— LV.35 注意无敌的眷属，讨伐大型妖异！(223) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_SAC(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "定罪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:912"])]
public void Condemnation_SAC(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 223) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "Condemnation";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(7.3f);
    dp.Radian = float.Pi / 180 * 90;
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

[ScriptMethod(name: "notify", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:870", "SourceId:400111A2"])]
public void notify_SAC(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 223) return;
    if (isDebug) accessory.Method.SendChat($"/e notifycount: {attackCount_SAC}");
    if (attackCount_SAC == 0)
    {
        if (isText) accessory.Method.TextInfo("集中攻击 暗影魔爪", duration: 2000, true);
        accessory.TTS("集中攻击 暗影魔爪", isTTS, isDRTTS);
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
    }
    attackCount_SAC++;
}

[ScriptMethod(name: "暗影之眼范围", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:1802"])]
public async void drawEye_SAC(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 223) return;
    if (isText) accessory.Method.TextInfo("不要攻击 暗影之眼, 注意远离钢铁", duration: 5000, true);
    accessory.TTS("不要攻击 暗影之眼, 注意远离钢铁", isTTS, isDRTTS);

    for (int i = 0; i < 5; i++)
    {
        switch (i)
        {
            case 0:
                drawEyeDelay_SAC = 7900;
                break;
            case 1:
                drawEyeDelay_SAC = 18100;
                break;
            case 2:
                drawEyeDelay_SAC = 28400;
                break;
            case 3:
                drawEyeDelay_SAC = 38700;
                break;
            case 4:
                drawEyeDelay_SAC = 49000;
                break;
        }

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"暗影之眼范围{i}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(14f);
        dp.Delay = drawEyeDelay_SAC;
        dp.DestoryAt = 2500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp1 = accessory.Data.GetDefaultDrawProperties();
        dp1.Name = $"暗影之眼范围{i}描边";
        dp1.Scale = new(14f);
        dp1.InnerScale = new(13.9f);
        dp1.Radian = float.Pi * 2;
        dp1.Color = new Vector4(178 / 255.0f, 34 / 255.0f, 34 / 255.0f, 10.0f);
        dp1.Owner = @event.SourceId();
        dp1.Delay = drawEyeDelay_SAC;
        dp1.DestoryAt = 2500;
        dp1.Radian = 2 * float.Pi;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp1);
    }
    if (!LocalMark) await Task.Delay(1000);         // 拟人
    if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Stop1, LocalMark);
}

[ScriptMethod(name: "三连爪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:932"])]
public void Triclip_SAC(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 223) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "三连爪";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(4f, 5f);
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
}

[ScriptMethod(name: "咒言", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(585|586)$"])]
public async void Curse_SAC(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 223) return;
    await Task.Delay(5);

    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"咒言范围";
    dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(3.8f);
    dp.DestoryAt = 2400;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

[ScriptMethod(name: "删除绘制", eventType: EventTypeEnum.Director, eventCondition: ["Command:40000002", "Instance:8003271B"])]
public async void delDraw_SAC(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 223) return;
    accessory.Method.RemoveDraw(".*");
}

[ScriptMethod(name: "骇人嚎叫", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:933"])]
public async void FrightfulRoar_SAC(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 223) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"骇人嚎叫";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(8f);
    dp.DestoryAt = 2700;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

[ScriptMethod(name: "删除骇人嚎叫", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:933"])]
public async void delFrightfulRoar_SAC(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 223) return;
    accessory.Method.RemoveDraw("骇人嚎叫");
}

#endregion 223 注意无敌的眷属，讨伐大型妖异 Shadow and Claw

#region 298 讨伐爆弹怪的女王 Long Live the Queen
// v0.0.0.3: 

[ScriptMethod(name: "—————— LV.35 讨伐爆弹怪的女王！(298) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_LLQ(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "自爆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:337"])]
public async void SelfDestruct_LLQ(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 298) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"自爆";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(6.5f);
    dp.DestoryAt = 2900;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

[ScriptMethod(name: "催眠", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:145"])]
public async void Sleep_LLQ(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 298) return;
    lock (SleepLock_LLQ)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"催眠";
        dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 251 / 255.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(2f);
        dp.DestoryAt = 2500;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

        switch (SleepCount_LLQ)
        {
            case 0:
                if (isText) accessory.Method.TextInfo("催眠 注意打断", duration: 2000, true);
                accessory.TTS("催眠 注意打断", isTTS, isDRTTS);
                if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Bind1, LocalMark);
                break;
            case 1:
                if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Bind2, LocalMark);
                break;
            case 2:
                if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Bind3, LocalMark);
                break;
        }
        SleepCount_LLQ++;
    }
}

[ScriptMethod(name: "notify", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:1805"])]
public void notify_LLQ(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 298) return;
    if (isText) accessory.Method.TextInfo("集中攻击 爆弹女王", duration: 5000, true);
    accessory.TTS("集中攻击 爆弹女王", isTTS, isDRTTS);
    if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
}

[ScriptMethod(name: "大爆炸", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:1007"])]
public async void bigExplosion_LLQ(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 298) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"大爆炸";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(8.3f);
    dp.DestoryAt = 5100;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

    if (isText) accessory.Method.TextInfo("钢铁, 注意远离", duration: 5000, true);
    accessory.TTS("钢铁, 注意远离", isTTS, isDRTTS);
}

#endregion 298 讨伐爆弹怪的女王 Long Live the Queen

#region 299 歼灭特殊阵型的妖异 Ward Up
// v0.0.0.4: 

[ScriptMethod(name: "—————— LV.40 歼灭特殊阵型的妖异！(299) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_WU(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "无敌提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:325"])]
public void GOD_WU(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 299) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"无敌提示";
    dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    dp.Owner = @event.TargetId();
    dp.Scale = new Vector2(1.5f);
    dp.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

    if (isText) accessory.Method.TextInfo("攻击非无敌的小怪", duration: 5000, true);
    accessory.TTS("攻击非无敌的小怪", isTTS, isDRTTS);
    if (isMark) accessory.Method.Mark(@event.TargetId(), KodakkuAssist.Module.GameOperate.MarkType.Stop1, LocalMark);
}

[ScriptMethod(name: "无敌提示删除", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:325"])]
public void delGod_WU(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 299) return;
    accessory.Method.RemoveDraw("无敌提示");
    accessory.Method.MarkClear();
}

[ScriptMethod(name: "恐怖眼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:740"])]
public void bigExplosion_WU(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 299) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"恐怖眼";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Position = @event.EffectPosition();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(6f);
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

#endregion 299 歼灭特殊阵型的妖异 Ward Up

#region 300 制止三方混战的巨人族，守住遗物 Solemn Trinity
// v0.0.0.3: 

[ScriptMethod(name: "—————— LV.40 制止三方混战的巨人族，守住遗物！(300) ——————", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:"])]
public void Divider_ST(Event @event, ScriptAccessory accessory) { }

[ScriptMethod(name: "超压斧", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:720"])]
public void Overpower_ST(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 300) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "Overpower";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(7.9f);
    dp.Radian = float.Pi / 180 * 90;
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

[ScriptMethod(name: "Boss指示0", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:1816"])]
public async void BossNotify_ST(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 300) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"Boss指示0";
    dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(2f);
    dp.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

    if (isText) accessory.Method.TextInfo("攻击 长房克利俄斯", duration: 5000, true);
    accessory.TTS("攻击 长房克利俄斯", isTTS, isDRTTS);
    if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
}

[ScriptMethod(name: "Boss指示删除0", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:1816"])]
public async void delBossNotify_ST(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 300) return;
    accessory.Method.RemoveDraw("Boss指示0");
    accessory.Method.MarkClear();
}

[ScriptMethod(name: "Boss指示1", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:1824"])]
public async void BossNotify1_ST(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 300) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"Boss指示1";
    dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(2f);
    dp.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

    if (isText) accessory.Method.TextInfo("优先攻击 被标记Boss", duration: 5000, true);
    accessory.TTS("优先攻击 被标记Boss", isTTS, isDRTTS);
    if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
}

[ScriptMethod(name: "Boss指示2", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:1819"])]
public async void BossNotify2_ST(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 300) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"Boss指示2";
    dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(2f);
    dp.DestoryAt = long.MaxValue;
    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack2, LocalMark);
}

[ScriptMethod(name: "野蛮咆哮", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:596"])]
public async void BarbarousScream_ST(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 300) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"野蛮咆哮";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Owner = @event.SourceId();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(5.3f);
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

[ScriptMethod(name: "巨大抨击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:692"])]
public void ColossalSlam_ST(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 300) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = "巨大抨击";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Owner = @event.SourceId();
    dp.Scale = new Vector2(9f);
    dp.Radian = float.Pi / 180 * 120;
    dp.DestoryAt = 4000;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
}

[ScriptMethod(name: "骨灰", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:600"])]
public async void BonePowder_ST(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 300) return;
    var dp = accessory.Data.GetDefaultDrawProperties();
    dp.Name = $"骨灰";
    dp.Color = accessory.Data.DefaultDangerColor;
    dp.Position = @event.EffectPosition();
    dp.ScaleMode = ScaleMode.ByTime;
    dp.Scale = new Vector2(3f);
    dp.DestoryAt = 2500;
    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
}

[ScriptMethod(name: "删除绘制", eventType: EventTypeEnum.Director, eventCondition: ["Command:40000002", "Instance:8003271E"])]
public async void delDraw_ST(Event @event, ScriptAccessory accessory)
{
    if (HelperExtensions.GetCurrentTerritoryId() != 300) return;
    accessory.Method.RemoveDraw(".*");
}

#endregion 300 制止三方混战的巨人族，守住遗物 Solemn Trinity

#endregion 各行会令脚本

}



#region 函数集
public static class EventExtensions
{
    private static bool ParseHexId(string? idStr, out uint id)
    {
        id = 0;
        if (string.IsNullOrEmpty(idStr)) return false;
        try
        {
            var idStr2 = idStr.Replace("0x", "");
            id = uint.Parse(idStr2, System.Globalization.NumberStyles.HexNumber);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static uint Id0(this Event @event)
    {
        return ParseHexId(@event["Id"], out var id) ? id : 0;
    }

    public static uint Index(this Event ev)
    {
        return ParseHexId(ev["Index"], out var index) ? index : 0;
    }

    public static uint DataId(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["DataId"]);
    }

    public static uint ActionId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
    }

    public static uint SourceId(this Event @event)
    {
        return ParseHexId(@event["SourceId"], out var id) ? id : 0;
    }

    public static uint SourceDataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["SourceDataId"]);
    }

    public static uint Command(this Event @event)
    {
        return ParseHexId(@event["Command"], out var cid) ? cid : 0;
    }

    public static string DurationMilliseconds(this Event @event)
    {
        return JsonConvert.DeserializeObject<string>(@event["DurationMilliseconds"]) ?? string.Empty;
    }

    public static float SourceRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
    }

    public static float TargetRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["TargetRotation"]);
    }

    public static uint State(this Event @event)
    {
        return ParseHexId(@event["State"], out var state) ? state : 0;
    }

    public static string SourceName(this Event @event)
    {
        return @event["SourceName"];
    }

    public static string TargetName(this Event @event)
    {
        return @event["TargetName"];
    }

    public static uint TargetId(this Event @event)
    {
        return ParseHexId(@event["TargetId"], out var id) ? id : 0;
    }

    public static Vector3 SourcePosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    public static Vector3 TargetPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
    }

    public static Vector3 EffectPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
    }

    public static uint DirectorId(this Event @event)
    {
        return ParseHexId(@event["DirectorId"], out var id) ? id : 0;
    }

    public static uint StatusId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StatusId"]);
    }

    public static uint StackCount(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StackCount"]);
    }

    public static uint Param(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["Param"]);
    }
}


public static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
    }

    public static IGameObject? GetMe(this ScriptAccessory sa)
    {
        return sa.Data.Objects.LocalPlayer;
    }

    public static IEnumerable<IGameObject?> GetByDataId(this ScriptAccessory sa, uint dataId)
    {
        return sa.Data.Objects.Where(x => x.DataId == dataId);
    }

    public static string GetPlayerJob(this ScriptAccessory sa, IPlayerCharacter? playerObject, bool fullName = false)
    {
        if (playerObject == null) return "None";
        return fullName ? playerObject.ClassJob.Value.Name.ToString() : playerObject.ClassJob.Value.Abbreviation.ToString();
    }

    /// <summary>
    /// 获取玩家的职能
    /// Return: "Tank"(坦克) / "Healer"(治疗) / "Melee DPS"(近战物理) / "Ranged Physical DPS"(远程物理) / "Ranged Magical DPS"(远程魔法) / "Unknown" / "None"
    /// 大版本更新需要维护
    /// </summary>
    public static string GetPlayerRole(this ScriptAccessory sa, IPlayerCharacter? playerObject)
    {
        if (playerObject == null) return "None";

        return playerObject.ClassJob.RowId switch
        {
            // Tank: 骑士、战士、暗黑骑士、绝枪战士
            19 or 21 or 32 or 37 => "Tank",
            // Healer: 白魔法师、学者、占星术士、贤者
            24 or 28 or 33 or 40 => "Healer",
            // Melee DPS: 武僧、龙骑士、忍者、武士、钐镰客、蝰蛇剑士
            20 or 22 or 30 or 34 or 39 or 41 => "Melee DPS",
            // Ranged Physical DPS: 吟游诗人、机工士、舞者
            23 or 31 or 38 => "Ranged Physical DPS",
            // Ranged Magical DPS: 黑魔法师、召唤师、赤魔法师、青魔法师、绘灵法师
            25 or 27 or 35 or 36 or 42 => "Ranged Magical DPS",
            _ => "Unknown"
        };
    }

    public static float GetStatusRemainingTime(this ScriptAccessory sa, IBattleChara? battleChara, uint statusId)
    {
        if (battleChara == null || !battleChara.IsValid()) return 0;
        unsafe
        {
            BattleChara* charaStruct = (BattleChara*)battleChara.Address;
            var statusIdx = charaStruct->GetStatusManager()->GetStatusIndex(statusId);
            return charaStruct->GetStatusManager()->GetRemainingTime(statusIdx);
        }
    }

    public static bool HasStatus(this ScriptAccessory sa, IBattleChara? battleChara, uint statusId)
    {
        if (battleChara == null || !battleChara.IsValid()) return false;
        unsafe
        {
            BattleChara* charaStruct = (BattleChara*)battleChara.Address;
            var statusIdx = charaStruct->GetStatusManager()->GetStatusIndex(statusId);
            return statusIdx != -1;
        }
    }

    public static float GetHitboxRadius(IGameObject obj)
    {
        if (obj == null || !obj.IsValid()) return -1;
        return obj.HitboxRadius;
    }

    public static string GetObjectName(IGameObject obj)
    {
        if (obj == null || !obj.IsValid()) return "None";
        return obj.Name.ToString();
    }

    /// <summary>
    /// 获取指定标记索引的对象EntityId
    /// </summary>
    public static unsafe ulong GetMarkerEntityId(uint markerIndex)
    {
        var markingController = MarkingController.Instance();
        if (markingController == null) return 0;
        if (markerIndex >= 17) return 0;

        return markingController->Markers[(int)markerIndex];
    }

    /// <summary>
    /// 获取对象身上的标记
    /// </summary>
    /// <returns>MarkType</returns>
    public static MarkType GetObjectMarker(IGameObject? obj)
    {
        if (obj == null || !obj.IsValid()) return MarkType.None;

        ulong targetEntityId = obj.EntityId;

        for (uint i = 0; i < 17; i++)
        {
            var markerEntityId = GetMarkerEntityId(i);
            if (markerEntityId == targetEntityId)
            {
                return (MarkType)i;
            }
        }

        return MarkType.None;
    }

    /// <summary>
    /// 检查对象是否有指定的标记
    /// </summary>
    public static bool HasMarker(IGameObject? obj, MarkType markType)
    {
        return GetObjectMarker(obj) == markType;
    }

    /// <summary>
    /// 检查对象是否有任何标记
    /// </summary>
    public static bool HasAnyMarker(IGameObject? obj)
    {
        return GetObjectMarker(obj) != MarkType.None;
    }

    private static ulong GetMarkerForObject(IGameObject? obj)
    {
        if (obj == null) return 0;
        unsafe
        {
            for (uint i = 0; i < 17; i++)
            {
                var markerEntityId = GetMarkerEntityId(i);
                if (markerEntityId == obj.EntityId)
                {
                    return markerEntityId;
                }
            }
        }
        return 0;
    }

    private static MarkType GetMarkerTypeForObject(IGameObject? obj)
    {
        if (obj == null) return MarkType.None;
        unsafe
        {
            for (uint i = 0; i < 17; i++)
            {
                var markerEntityId = GetMarkerEntityId(i);
                if (markerEntityId == obj.EntityId)
                {
                    return (MarkType)i;
                }
            }
        }
        return MarkType.None;
    }

    /// <summary>
    /// 获取标记的名称
    /// </summary>
    public static string GetMarkerName(MarkType markType)
    {
        return markType switch
        {
            MarkType.Attack1 => "攻击1",
            MarkType.Attack2 => "攻击2",
            MarkType.Attack3 => "攻击3",
            MarkType.Attack4 => "攻击4",
            MarkType.Attack5 => "攻击5",
            MarkType.Bind1 => "止步1",
            MarkType.Bind2 => "止步2",
            MarkType.Bind3 => "止步3",
            MarkType.Ignore1 => "禁止1",
            MarkType.Ignore2 => "禁止2",
            MarkType.Square => "方块",
            MarkType.Circle => "圆圈",
            MarkType.Cross => "十字",
            MarkType.Triangle => "三角",
            MarkType.Attack6 => "攻击6",
            MarkType.Attack7 => "攻击7",
            MarkType.Attack8 => "攻击8",
            _ => "无标记"
        };
    }
}

public enum MarkType
{
    None = -1,
    Attack1 = 0,
    Attack2 = 1,
    Attack3 = 2,
    Attack4 = 3,
    Attack5 = 4,
    Bind1 = 5,
    Bind2 = 6,
    Bind3 = 7,
    Ignore1 = 8,
    Ignore2 = 9,
    Square = 10,
    Circle = 11,
    Cross = 12,
    Triangle = 13,
    Attack6 = 14,
    Attack7 = 15,
    Attack8 = 16,
    Count = 17
}

public static class ActionExt
{
    public static unsafe bool IsReadyWithCanCast(uint actionId, ActionType actionType)
    {
        var am = ActionManager.Instance();
        if (am == null) return false;

        var adjustedId = am->GetAdjustedActionId(actionId);

        // 0 = Ready）
        if (am->GetActionStatus(actionType, adjustedId) != 0)
            return false;

        ulong targetId = 0;
        var ts = TargetSystem.Instance();
        if (ts != null && ts->GetTargetObject() != null)
            targetId = ts->GetTargetObject()->GetGameObjectId();

        return am->GetActionStatus(actionType, adjustedId, targetId) == 0;
    }

    public static bool IsSpellReady(this uint spellId) => IsReadyWithCanCast(spellId, ActionType.Action);
    public static bool IsAbilityReady(this uint abilityId) => IsReadyWithCanCast(abilityId, ActionType.EventAction);
}

#region 计算函数

public static class MathTools
{
    public static float DegToRad(this float deg) => (deg + 360f) % 360f / 180f * float.Pi;
    public static float RadToDeg(this float rad) => (rad + 2 * float.Pi) % (2 * float.Pi) / float.Pi * 180f;

    /// <summary>
    /// 将弧度值规范化到 -π 到 π 范围
    /// </summary>
    public static float NormalizeRadian(this float rad)
    {
        rad = (rad + 2 * float.Pi) % (2 * float.Pi); // 先转到 0-2π
        if (rad > float.Pi) rad -= 2 * float.Pi; // 如果大于 π，转到负数范围
        return rad;
    }

    /// <summary>
    /// 获得任意点与中心点的弧度值，以(0, 0, 1)方向为0，以(1, 0, 0)方向为pi/2。
    /// 即，逆时针方向增加。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetRadian(this Vector3 point, Vector3 center)
        => MathF.Atan2(point.X - center.X, point.Z - center.Z);

    /// <summary>
    /// 获得任意点与中心点的长度。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetLength(this Vector3 point, Vector3 center)
        => new Vector2(point.X - center.X, point.Z - center.Z).Length();

    /// <summary>
    /// 将任意点以中心点为圆心，逆时针旋转并延长。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <param name="radian">旋转弧度</param>
    /// <param name="length">基于该点延伸长度</param>
    /// <returns></returns>
    public static Vector3 RotateAndExtend(this Vector3 point, Vector3 center, float radian, float length)
    {
        var baseRad = point.GetRadian(center);
        var baseLength = point.GetLength(center);
        var rotRad = baseRad + radian;
        return new Vector3(
            center.X + MathF.Sin(rotRad) * (length + baseLength),
            center.Y,
            center.Z + MathF.Cos(rotRad) * (length + baseLength)
        );
    }

    /// <summary>
    /// 获得某角度所在划分区域
    /// </summary>
    /// <param name="radian">输入弧度</param>
    /// <param name="regionNum">区域划分数量</param>
    /// <param name="baseRegionIdx">0度所在区域的初始Idx</param>>
    /// <param name="isDiagDiv">是否为斜分割，默认为false</param>
    /// <param name="isCw">是否顺时针增加，默认为false</param>
    /// <returns></returns>
    public static int RadianToRegion(this float radian, int regionNum, int baseRegionIdx = 0, bool isDiagDiv = false, bool isCw = false)
    {
        var sepRad = float.Pi * 2 / regionNum;
        var inputAngle = radian * (isCw ? -1 : 1) + (isDiagDiv ? sepRad / 2 : 0);
        var rad = (inputAngle + 4 * float.Pi) % (2 * float.Pi);
        return ((int)Math.Floor(rad / sepRad) + baseRegionIdx + regionNum) % regionNum;
    }

    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerX">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
        => point with { X = 2 * centerX - point.X };

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerZ">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
        => point with { Z = 2 * centerZ - point.Z };

    /// <summary>
    /// 将输入点中心对称
    /// </summary>
    /// <param name="point">输入点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static Vector3 PointCenterSymmetry(this Vector3 point, Vector3 center)
        => point.RotateAndExtend(center, float.Pi, 0);

    /// <summary>
    /// 获取给定数的指定位数
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="x">对应位数，个位为1</param>
    /// <returns></returns>
    public static int GetDecimalDigit(this int val, int x)
    {
        var valStr = val.ToString();
        var length = valStr.Length;
        if (x < 1 || x > length) return -1;
        var digitChar = valStr[length - x]; // 从右往左取第x位
        return int.Parse(digitChar.ToString());
    }

    /// <summary>
    /// 根据角度和距离计算目标位置
    /// </summary>
    public static Vector3 GetPositionByAngle(Vector3 origin, float angleInDegrees, float distance)
    {
        float radian = angleInDegrees * MathF.PI / 180f;

        return new Vector3(
            origin.X + distance * MathF.Cos(radian),
            origin.Y,
            origin.Z + distance * MathF.Sin(radian)
        );
    }
}

#endregion 计算函数

#region 位置序列函数
public static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataId，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="sa"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory sa, uint pid)
    {
        // 获得玩家 IDX
        return sa.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="sa"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int GetMyIndex(this ScriptAccessory sa)
    {
        return sa.Data.PartyList.IndexOf(sa.Data.Me);
    }

    /// <summary>
    /// 输入玩家dataId，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="sa"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string GetPlayerJobById(this ScriptAccessory sa, uint pid)
    {
        // 获得玩家职能简称，无用处，仅作DEBUG输出
        var idx = sa.Data.PartyList.IndexOf(pid);
        var str = sa.GetPlayerJobByIndex(idx);
        return str;
    }

    /// <summary>
    /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="idx">位置index</param>
    /// <param name="fourPeople">是否为四人迷宫</param>
    /// <param name="sa"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory sa, int idx, bool fourPeople = false)
    {
        List<string> role8 = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
        List<string> role4 = ["T", "H", "D1", "D2"];
        if (idx < 0 || idx >= 8 || (fourPeople && idx >= 4))
            return "Unknown";
        return fourPeople ? role4[idx] : role8[idx];
    }

    /// <summary>
    /// 将List内信息转换为字符串。
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="myList"></param>
    /// <param name="isJob">是职业，在转为字符串前调用转职业函数</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string BuildListStr<T>(this ScriptAccessory sa, List<T> myList, bool isJob = false)
    {
        return string.Join(", ", myList.Select(item =>
        {
            if (isJob && item != null && item is int i)
                return sa.GetPlayerJobByIndex(i);
            return item?.ToString() ?? "";
        }));
    }
}
#endregion 位置序列函数

#region 绘图函数

public static class DrawTools
{
    /// <summary>
    /// 返回绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">绘图基准，可为UID或位置</param>
    /// <param name="targetObj">绘图指向目标，可为UID或位置</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="radian">绘制图形弧度范围</param>
    /// <param name="rotation">绘制图形旋转弧度，以owner面前为基准，逆时针增加</param>
    /// <param name="width">绘制图形宽度，部分图形可保持与长度一致</param>
    /// <param name="length">绘制图形长度，部分图形可保持与宽度一致</param>
    /// <param name="innerWidth">绘制图形内宽，部分图形可保持与长度一致</param>
    /// <param name="innerLength">绘制图形内长，部分图形可保持与宽度一致</param>
    /// <param name="drawModeEnum">绘图方式</param>
    /// <param name="drawTypeEnum">绘图类型</param>
    /// <param name="isSafe">是否使用安全色</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="byY">动画效果随距离变更</param>
    /// <param name="draw">是否直接绘图</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnerBase(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        float radian, float rotation, float width, float length, float innerWidth, float innerLength,
        DrawModeEnum drawModeEnum, DrawTypeEnum drawTypeEnum, bool isSafe = false,
        bool byTime = false, bool byY = false, bool draw = true)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.InnerScale = new Vector2(innerWidth, innerLength);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Color = isSafe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        dp.ScaleMode |= byY ? ScaleMode.YByDistance : ScaleMode.None;

        switch (ownerObj)
        {
            case uint u:
                dp.Owner = u;
                break;
            case ulong ul:
                dp.Owner = ul;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
            default:
                throw new ArgumentException($"ownerObj {ownerObj} 的目标类型 {ownerObj.GetType()} 输入错误");
        }

        switch (targetObj)
        {
            case 0:
            case 0u:
                break;
            case uint u:
                dp.TargetObject = u;
                break;
            case ulong ul:
                dp.TargetObject = ul;
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos;
                break;
            default:
                throw new ArgumentException($"targetObj {targetObj} 的目标类型 {targetObj.GetType()} 输入错误");
        }

        if (draw)
            sa.Method.SendDraw(drawModeEnum, drawTypeEnum, dp);
        return dp;
    }

    /// <summary>
    /// 返回指路绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">出发点</param>
    /// <param name="targetObj">结束点</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">箭头旋转角度</param>
    /// <param name="width">箭头宽度</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        float rotation = 0, float width = 1f, bool isSafe = true, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width,
            width, 0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Displacement, isSafe, false, true, draw);

    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float rotation = 0, float width = 1f, bool isSafe = true,
        bool draw = true)
        => sa.DrawGuidance((ulong)sa.Data.Me, targetObj, delay, destroy, name, rotation, width, isSafe, draw);

    /// <summary>
    /// 返回圆形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="scale">圆形径长</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float scale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, scale, scale,
            0, 0, DrawModeEnum.Default, DrawTypeEnum.Circle, isSafe, byTime, false, draw);

    /// <summary>
    /// 返回环形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="outScale">外径</param>
    /// <param name="innerScale">内径</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Donut, isSafe, byTime, false, draw);

    /// <summary>
    /// 返回扇形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="radian">弧度</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="outScale">外径</param>
    /// <param name="innerScale">内径</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, radian, rotation, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Fan, isSafe, byTime, false, draw);

    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawFan(ownerObj, 0, delay, destroy, name, radian, rotation, outScale, innerScale, isSafe, byTime, draw);

    /// <summary>
    /// 返回矩形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">矩形起始</param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">矩形宽度</param>
    /// <param name="length">矩形长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Rect, isSafe, byTime, byY, draw);

    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawRect(ownerObj, 0, delay, destroy, name, rotation, width, length, isSafe, byTime, byY, draw);

    /// <summary>
    /// 返回背对绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, bool isSafe = true, bool draw = true)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, 0, 0, 0, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.SightAvoid, isSafe, false, false, draw);

    /// <summary>
    /// 返回击退绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="targetObj">击退源</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="width">箭头宽</param>
    /// <param name="length">箭头长</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float width, float length,
        bool isSafe = false, bool draw = true)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, float.Pi, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Displacement, isSafe, false, false, draw);

    /// <summary>
    /// 返回线型绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">线条起始</param>
    /// <param name="targetObj">线条目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">线条宽度</param>
    /// <param name="length">线条长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLine(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Line, isSafe, byTime, byY, draw);

    /// <summary>
    /// 返回两对象间连线绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">起始源</param>
    /// <param name="targetObj">目标源</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="width">线宽</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnection(this ScriptAccessory sa, object ownerObj, object targetObj,
        int delay, int destroy, string name, float width = 1f, bool isSafe = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, 0, width, width,
            0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Line, isSafe, false, true, draw);

    /// <summary>
    /// 赋予输入的dp以ownerId为源的远近目标绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="isNearOrder">从owner计算，近顺序或远顺序</param>
    /// <param name="orderIdx">从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersDistanceOrder(this DrawPropertiesEdit self, bool isNearOrder,
        uint orderIdx)
    {
        self.CentreResolvePattern = isNearOrder
            ? PositionResolvePatternEnum.PlayerNearestOrder
            : PositionResolvePatternEnum.PlayerFarestOrder;
        self.CentreOrderIndex = orderIdx;
        return self;
    }

    /// <summary>
    /// 赋予输入的dp以ownerId为源的仇恨顺序绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="orderIdx">仇恨顺序，从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersEnmityOrder(this DrawPropertiesEdit self, uint orderIdx)
    {
        self.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        self.CentreOrderIndex = orderIdx;
        return self;
    }

    /// <summary>
    /// 赋予输入的dp以position为源的远近目标绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="isNearOrder">从owner计算，近顺序或远顺序</param>
    /// <param name="orderIdx">从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetPositionDistanceOrder(this DrawPropertiesEdit self, bool isNearOrder,
        uint orderIdx)
    {
        self.TargetResolvePattern = isNearOrder
            ? PositionResolvePatternEnum.PlayerNearestOrder
            : PositionResolvePatternEnum.PlayerFarestOrder;
        self.TargetOrderIndex = orderIdx;
        return self;
    }

    /// <summary>
    /// 赋予输入的dp以ownerId施法目标为源的绘图
    /// </summary>
    /// <param name="self"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersTarget(this DrawPropertiesEdit self)
    {
        self.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        return self;
    }
}

#endregion 绘图函数

#region 标点函数

public static class MarkerHelper
{
    public static void LocalMarkClear(this ScriptAccessory sa)
    {
        sa.Log.Debug($"删除本地标点。");
        sa.Method.Mark(0xE000000, KodaMarkType.Attack1, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Attack2, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Attack3, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Attack4, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Attack5, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Attack6, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Attack7, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Attack8, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Bind1, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Bind2, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Bind3, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Stop1, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Stop2, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Square, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Circle, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Cross, true);
        sa.Method.Mark(0xE000000, KodaMarkType.Triangle, true);
    }

    public static void MarkClear(this ScriptAccessory sa,
        bool enable = true, bool local = false, bool localString = false)
    {
        if (!enable) return;
        sa.Log.Debug($"接收命令：删除标点");

        if (local)
        {
            if (localString)
                sa.Log.Debug($"[字符模拟] 删除本地标点。");
            else
                sa.LocalMarkClear();
        }
        else
            sa.Method.MarkClear();
    }

    public static void MarkPlayerByIdx(this ScriptAccessory sa, int idx, KodaMarkType marker,
        bool enable = true, bool local = false, bool localString = false)
    {
        if (!enable) return;
        if (localString)
            sa.Log.Debug($"[本地字符模拟] 为{idx}({sa.GetPlayerJobByIndex(idx)})标上{marker}。");
        else
            sa.Method.Mark(sa.Data.PartyList[idx], marker, local);
    }

    public static void MarkPlayerById(ScriptAccessory sa, uint id, KodaMarkType marker,
        bool enable = true, bool local = false, bool localString = false)
    {
        if (!enable) return;
        if (localString)
            sa.Log.Debug($"[本地字符模拟] 为{sa.GetPlayerIdIndex(id)}({sa.GetPlayerJobById(id)})标上{marker}。");
        else
            sa.Method.Mark(id, marker, local);
    }

    public static int GetMarkedPlayerIndex(this ScriptAccessory sa, List<KodaMarkType> markerList, KodaMarkType marker)
    {
        return markerList.IndexOf(marker);
    }
}

#endregion

#region 特殊函数

public static class SpecialFunction
{
    public static void SetTargetable(this ScriptAccessory sa, IGameObject? obj, bool targetable)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            if (targetable)
            {
                if (obj.IsDead || obj.IsTargetable) return;
                charaStruct->TargetableStatus |= ObjectTargetableFlags.IsTargetable;
            }
            else
            {
                if (!obj.IsTargetable) return;
                charaStruct->TargetableStatus &= ~ObjectTargetableFlags.IsTargetable;
            }
        }
        sa.Log.Debug($"SetTargetable {targetable} => {obj.Name} {obj}");
    }

    public static unsafe void SetModelScale(ScriptAccessory sa, uint dataId, float scale, float VfxScale)
    {
        sa.Method.RunOnMainThreadAsync(() =>
        {
            var obj = sa.Data.Objects.FirstOrDefault(o => o.DataId == dataId);
            if (obj == null) return;

            var gameObj = (GameObject*)obj.Address;
            if (gameObj == null || !gameObj->IsReadyToDraw()) return;

            gameObj->Scale = scale;
            gameObj->VfxScale = VfxScale;

            if (gameObj->IsCharacter())
            {
                var chara = (BattleChara*)gameObj;
                chara->Character.CharacterData.ModelScale = scale;
            }

            gameObj->DisableDraw();
            gameObj->EnableDraw();
        });
    }

    public static void SetRotation(this ScriptAccessory sa, IGameObject? obj, float radian, bool show = false)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->SetRotation(radian);
        }
        sa.Log.Debug($"改变面向 {obj.Name.TextValue} | {obj.EntityId} => {radian.RadToDeg()}");

        if (!show) return;
        var ownerObj = sa.GetById(obj.EntityId);
        if (ownerObj == null) return;
        var dp = sa.DrawGuidance(ownerObj, 0, 0, 2000, $"改变面向 {obj.Name.TextValue}", radian, draw: false);
        dp.FixRotation = true;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);

    }

    public static void SetPosition(this ScriptAccessory sa, IGameObject? obj, Vector3 position, bool show = false)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->SetPosition(position.X, position.Y, position.Z);
        }
        sa.Log.Debug($"改变位置 => {obj.Name.TextValue} | {obj.EntityId} => {position}");

        if (!show) return;
        var dp = sa.DrawCircle(position, 0, 2000, $"传送点 {obj.Name.TextValue}", 0.5f, true, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

    }

    [Flags]
    public enum DrawState : uint
    {
        Invisibility = 0x00_00_00_02,
        IsLoading = 0x00_00_08_00,
        SomeNpcFlag = 0x00_00_01_00,
        MaybeCulled = 0x00_00_04_00,
        MaybeHiddenMinion = 0x00_00_80_00,
        MaybeHiddenSummon = 0x00_80_00_00,
    }

    public static unsafe DrawState* ActorDrawState(IGameObject actor)
        => (DrawState*)(&((GameObject*)actor.Address)->RenderFlags);

    /// <summary>
    /// 检查对象可见性（Read）
    /// </summary>
    /// <param name="sa">ScriptAccessory</param>
    /// <param name="obj">Obj need check</param>
    /// <param name="checkVisible">true=检查可见，false=检查不可见</param>
    /// <returns>如果符合检查条件返回 True</returns>
    public static unsafe bool IsActorVisible(this ScriptAccessory sa, IGameObject? obj, bool checkVisible = true)
    {
        if (obj == null) return false;

        try
        {
            var state = *ActorDrawState(obj);
            bool isVisible = (state & DrawState.Invisibility) == 0;

            return checkVisible ? isVisible : !isVisible;
        }
        catch (Exception e)
        {
            sa.Log.Error($" {e} ");
            return false;
        }
    }

    /// <summary>
    /// 设置对象可见性（Write）
    /// </summary>
    public static unsafe void WriteVisible(this ScriptAccessory sa, IGameObject? actor, bool visible)
    {
        if (actor == null) return;

        try
        {
            var statePtr = ActorDrawState(actor);
            if (visible)
                *statePtr &= ~DrawState.Invisibility;
            else
                *statePtr |= DrawState.Invisibility;
        }
        catch (Exception e)
        {
            sa.Log.Error($" {e} ");
            throw;
        }
    }

    /// <summary>
    /// Check DrawState
    /// </summary>
    public static unsafe bool HasDrawState(this ScriptAccessory sa, IGameObject? actor, DrawState state)
    {
        if (actor == null) return false;

        try
        {
            return (*ActorDrawState(actor) & state) != 0;
        }
        catch (Exception e)
        {
            sa.Log.Error($" {e} ");
            return false;
        }
    }
}

#endregion 特殊函数


public static class NamazuHelper
{
    public class NamazuCommand(ScriptAccessory accessory, string url, string command, string param)
    {
        private ScriptAccessory accessory { get; set; } = accessory;
        private string _url = url;

        public void PostCommand()
        {
            var url = $"{_url}/{command}";
            accessory.Log.Debug($"向{url}发送{param}");
            accessory.Method.HttpPost(url, param);
        }
    }

    public class Waymark
    {
        public ScriptAccessory accessory { get; set; }
        private Dictionary<string, object> _jsonObj = new();
        private string? _jsonPayload;

        public Waymark(ScriptAccessory _accessory)
        {
            accessory = _accessory;
        }

        public void AddWaymarkType(string type, Vector3 pos, bool active = true)
        {
            string[] validTypes = ["A", "B", "C", "D", "One", "Two", "Three", "Four"];
            var waymarkType = type;
            if (!validTypes.Contains(type)) return;
            _jsonObj[waymarkType] = new Dictionary<string, object>
            {
                { "X", pos.X },
                { "Y", pos.Y },
                { "Z", pos.Z },
                { "Active", active }
            };
        }

        public void SetJsonPayload(bool local = true, bool log = true)
        {
            _jsonObj["LocalOnly"] = local;
            _jsonObj["Log"] = log;
            _jsonPayload = JsonConvert.SerializeObject(_jsonObj);
        }

        public string? GetJsonPayload()
        {
            if (_jsonPayload == null)
                SetJsonPayload();
            return _jsonPayload;
        }

        public void PostWaymarkCommand(int port)
        {
            var param = GetJsonPayload();
            if (param == null) return;
            var post = new NamazuCommand(accessory, $"http://127.0.0.1:{port}", "place", param);
            post.PostCommand();
        }

        public void ClearWaymarks(int port)
        {
            var post = new NamazuCommand(accessory, $"http://127.0.0.1:{port}", "place", "clear");
            post.PostCommand();
        }
    }
}

public static class DrawHelper
{
    public static void DrawBeam(ScriptAccessory accessory, Vector3 sourcePosition, Vector3 targetPosition, string name = "Light's Course", int duration = 6700, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = sourcePosition;
        dp.TargetPosition = targetPosition;
        dp.Scale = new Vector2(10, 50);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    public static void DrawCircle(ScriptAccessory accessory, Vector3 position, Vector2 scale, int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0, DrawModeEnum drawmode = DrawModeEnum.Default, Vector3? offset = null)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Circle, dp);
    }

    public static void DrawDisplacement(ScriptAccessory accessory, Vector3 targetPos, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0, DrawModeEnum drawmode = DrawModeEnum.Imgui)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = accessory.Data.Me;
        dp.Color = color ?? accessory.Data.DefaultSafeColor;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.TargetPosition = targetPos;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Displacement, dp);
    }

    public static void DrawDisplacementby2points(ScriptAccessory accessory, Vector3 origin, Vector3 target, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Position = origin;
        dp.Color = color ?? accessory.Data.DefaultSafeColor;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.TargetPosition = target;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    public static void DrawDisplacementObject(ScriptAccessory accessory, ulong target, Vector2 scale, int duration, string name, float? rotation = null, Vector4? color = null, int delay = 0, bool fix = false, DrawModeEnum drawmode = DrawModeEnum.Imgui)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = accessory.Data.Me;
        dp.Color = color ?? accessory.Data.DefaultSafeColor;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.TargetObject = target;
        dp.Scale = scale;
        if (rotation.HasValue) dp.Rotation = rotation.Value;
        dp.Delay = delay;
        dp.FixRotation = fix;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Displacement, dp);
    }

    public static void DrawRect(ScriptAccessory accessory, Vector3 position, Vector3 targetPos, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.TargetPosition = targetPos;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    public static void DrawRectObjectNoTarget(ScriptAccessory accessory, ulong owner, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0, ScaleMode scalemode = ScaleMode.None, Vector3? offset = null)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scalemode;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    public static void DrawRectObjectNoTargetWithRot(ScriptAccessory accessory, ulong owner, Vector2 scale, float rotation, int duration, string name, Vector4? color = null, int delay = 0, ScaleMode scalemode = ScaleMode.None, Vector3? offset = null)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.Scale = scale;
        dp.Rotation = rotation;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scalemode;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    public static void DrawRectPosNoTarget(ScriptAccessory accessory, Vector3 pos, Vector2 scale, float rotation, int duration, string name, Vector4? color = null, int delay = 0, ScaleMode scalemode = ScaleMode.None, Vector3? offset = null, DrawModeEnum drawMode = DrawModeEnum.Default)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.Rotation = rotation;
        dp.DestoryAt = duration;
        dp.ScaleMode = scalemode;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        accessory.Method.SendDraw(drawMode, DrawTypeEnum.Rect, dp);
    }

    public static void DrawRectPosTarget(ScriptAccessory accessory, Vector3 pos, Vector3 targetpos, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0, ScaleMode scalemode = ScaleMode.None, Vector3? offset = null, DrawModeEnum drawMode = DrawModeEnum.Default)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.TargetPosition = targetpos;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scalemode;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        accessory.Method.SendDraw(drawMode, DrawTypeEnum.Rect, dp);
    }

    public static void DrawRectObjectTarget(ScriptAccessory accessory, ulong owner, ulong target, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0, ScaleMode scalemode = ScaleMode.None)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.TargetObject = target;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scalemode;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    public static void DrawRectObjectTargetPos(ScriptAccessory accessory, ulong owner, Vector3 targetPos, Vector2 scale, int duration, string name,
                                                Vector4? color = null, int delay = 0, ScaleMode scalemode = ScaleMode.None)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.TargetPosition = targetPos;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scalemode;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    public static void DrawFan(ScriptAccessory accessory, Vector3 position, float rotation, Vector2 scale, float angle,
                                int duration, string name, Vector4? color = null, int delay = 0,
                                bool fix = false, Vector3? offset = null, DrawModeEnum drawmode = DrawModeEnum.Default, bool scaleByTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.Rotation = rotation;
        dp.Scale = scale;
        dp.Radian = angle * (float.Pi / 180);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.FixRotation = fix;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Fan, dp);
    }

    public static void DrawFanNoRot(ScriptAccessory accessory, Vector3 position, Vector2 scale, float angle, int duration, string name, Vector4? color = null, int delay = 0, bool fix = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.Scale = scale;
        dp.Radian = angle * (float.Pi / 180);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.FixRotation = fix;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    public static void DrawFanObjectNoRot(ScriptAccessory accessory, ulong owner, Vector2 scale, float angle, int duration, string name, Vector4? color = null, int delay = 0, bool fix = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.Scale = scale;
        dp.Radian = angle * (float.Pi / 180);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.FixRotation = fix;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    public static void DrawFanObject(ScriptAccessory accessory, ulong owner, float rotation,
        Vector2 scale, float angle, int duration, string name, Vector4? color = null,
        int delay = 0, bool scaleByTime = true, bool fix = false, Vector3? offset = null, DrawModeEnum drawmode = DrawModeEnum.Default)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.Rotation = rotation;
        dp.Scale = scale;
        dp.Radian = angle * (float.Pi / 180);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.FixRotation = fix;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Fan, dp);
    }

    public static void DrawFanPos(ScriptAccessory accessory, Vector3 position, Vector3 targetPosition, float rotation,
        Vector2 scale, float angle, int duration, string name, Vector4? color = null,
        int delay = 0, bool scaleByTime = true, bool fix = false, Vector3? offset = null, DrawModeEnum drawmode = DrawModeEnum.Default)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.TargetPosition = targetPosition;
        dp.Rotation = rotation;
        dp.Scale = scale;
        dp.Radian = angle * (float.Pi / 180);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.FixRotation = fix;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Fan, dp);
    }

    public static void DrawLine(ScriptAccessory accessory, Vector3 startPosition, Vector3 endPosition, float width, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = startPosition;
        dp.TargetPosition = endPosition;
        dp.Scale = new Vector2(width, 1);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
    }

    public static void DrawArrow(ScriptAccessory accessory, Vector3 startPosition, Vector3 endPosition, float x, float y, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = startPosition;
        dp.TargetPosition = endPosition;
        dp.Scale = new Vector2(x, y);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Arrow, dp);
    }

    public static void DrawCircleObject(ScriptAccessory accessory, ulong? ob, Vector2 scale, int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0)
    {
        if (ob == null) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = ob.Value;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    public static void DrawDount(ScriptAccessory accessory, Vector3 position, Vector2 scale, Vector2 innerscale, int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0, DrawModeEnum drawmode = DrawModeEnum.Default)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.Radian = 2 * float.Pi;
        dp.Scale = scale;
        dp.InnerScale = innerscale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Donut, dp);
    }

    public static void DrawDountPos(ScriptAccessory accessory, Vector3 position, Vector3 targetPosition, float rotation, Vector2 scale, float radian, Vector2 innerscale,
        int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0, Vector3? offset = null, DrawModeEnum drawmode = DrawModeEnum.Default)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.TargetPosition = targetPosition;
        dp.Rotation = rotation;
        dp.Radian = radian;
        dp.Scale = scale;
        dp.InnerScale = innerscale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Donut, dp);
    }

    public static void DrawDountObjectPos(ScriptAccessory accessory, ulong obj, Vector3 targetPosition, float rotation, Vector2 scale, float radian, Vector2 innerscale,
        int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0, Vector3? offset = null, DrawModeEnum drawmode = DrawModeEnum.Default)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = obj;
        dp.TargetPosition = targetPosition;
        dp.Rotation = rotation;
        dp.Radian = radian;
        dp.Scale = scale;
        dp.InnerScale = innerscale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Donut, dp);
    }

    public static void DrawDountObject(ScriptAccessory accessory, ulong? ob, Vector2 scale, Vector2 innerscale, int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0, DrawModeEnum drawmode = DrawModeEnum.Default)
    {
        if (ob == null) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = ob.Value;
        dp.Radian = 2 * float.Pi;
        dp.Scale = scale;
        dp.InnerScale = innerscale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Donut, dp);
    }
}


#endregion 函数集

#region 扩展方法
public static class Extensions
{
    public static void antiKnockBack(this MethodAccessory method, ScriptAccessory sa)
    {
        // 7559 沉稳咏唱
        // 7548 亲疏自行
        var myobj = sa.Data.MyObject;

        if (IbcHelper.GetPlayerRole(sa, myobj) == "Tank" || IbcHelper.GetPlayerRole(sa, myobj) == "Melee DPS" || IbcHelper.GetPlayerRole(sa, myobj) == "Ranged Physical DPS")
        {
            method.UseAction(myobj.EntityId, 7548);
        }

        if (IbcHelper.GetPlayerRole(sa, myobj) == "Healer" || IbcHelper.GetPlayerRole(sa, myobj) == "Ranged Magical DPS")
        {
            method.UseAction(myobj.EntityId, 7559);
        }
    }
    public static float hitboxRadius(this MethodAccessory method, IPlayerCharacter obj)
    {
        return obj.HitboxRadius;
    }

    public static void TTS(this ScriptAccessory accessory, string text, bool isTTS, bool isDRTTS)
    {
        accessory.Method.TTS(text);
    }
}

public static class ExtensionMethods
{
    public static float Round(this float value, float precision) => MathF.Round(value / precision) * precision;

    public static Vector3 ToDirection(this float rotation)
    {
        return new Vector3(
            MathF.Sin(rotation),
            0f,
            MathF.Cos(rotation)
        );
    }

    public static Vector3 Quantized(this Vector3 position, float gridSize = 1f)
    {
        return new Vector3(
            MathF.Round(position.X / gridSize) * gridSize,
            MathF.Round(position.Y / gridSize) * gridSize,
            MathF.Round(position.Z / gridSize) * gridSize
        );
    }

    /// <summary>
    /// 获取玩家的职业名称或简称
    /// </summary>
    public static string GetPlayerJob(
        this ScriptAccessory sa,
        IPlayerCharacter? playerObject,
        bool fullName = false
    )
    {
        if (playerObject == null) return "None";
        return fullName
            ? playerObject.ClassJob.Value.Name.ToString()
            : playerObject.ClassJob.Value.Abbreviation.ToString();
    }

    /// <summary>
    /// 判断玩家是否是坦克职业
    /// </summary>
    public static bool IsTank(IPlayerCharacter? playerObject)
    {
        if (playerObject == null) return false;
        return playerObject.ClassJob.Value.Role == 1;
    }
}

public unsafe static class ExtensionVisibleMethod
{
    public static bool IsCharacterVisible(this ICharacter chr)
    {
        var v = (IntPtr)(((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)chr.Address)->GameObject.DrawObject);
        if (v == IntPtr.Zero) return false;
        return Bitmask.IsBitSet(*(byte*)(v + 136), 0);
    }

    public static class Bitmask
    {
        public static bool IsBitSet(ulong b, int pos)
        {
            return (b & (1UL << pos)) != 0;
        }

        public static void SetBit(ref ulong b, int pos)
        {
            b |= 1UL << pos;
        }

        public static void ResetBit(ref ulong b, int pos)
        {
            b &= ~(1UL << pos);
        }

        public static bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }

        public static bool IsBitSet(short b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }
    }
}
#endregion

#region 脚本版本检查
public static class ScriptVersionChecker
{
    private const string OnlineRepoUrl = "https://raw.githubusercontent.com/VeeverSW/Kodakku-Script/refs/heads/main/OnlineRepo.json";
    private static readonly HttpClient _httpClient = new HttpClient();

    /// <summary>
    /// 在线仓库脚本信息
    /// </summary>
    public class OnlineScriptInfo
    {
        public string Name { get; set; } = "";
        public string Guid { get; set; } = "";
        public string Version { get; set; } = "";
        public string Author { get; set; } = "";
        public string Repo { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Note { get; set; } = "";
        public string UpdateInfo { get; set; } = "";
        public int[] TerritoryIds { get; set; } = Array.Empty<int>();
    }

    /// <summary>
    /// 版本
    /// </summary>
    public enum VersionCompareResult
    {
        /// <summary>当前版本较新或相同</summary>
        UpToDate,
        /// <summary>有新版本可用</summary>
        UpdateAvailable,
        /// <summary>未找到匹配的脚本</summary>
        NotFound,
        /// <summary>检查失败</summary>
        Error
    }

    /// <summary>
    /// 检查脚本版本
    /// </summary>
    /// <param name="sa">ScriptAccessory</param>
    /// <param name="guid">脚本GUID</param>
    /// <param name="currentVersion">当前版本号</param>
    /// <param name="showNotification">是否显示通知</param>
    /// <returns>版本比较结果</returns>
    public static async Task<(VersionCompareResult result, OnlineScriptInfo? onlineInfo)> CheckVersionAsync(
        ScriptAccessory sa,
        string guid,
        string currentVersion,
        bool showNotification = true)
    {
        try
        {
            sa.Log.Debug($"开始检查脚本版本 (GUID: {guid}, 当前版本: {currentVersion})");

            var response = await _httpClient.GetStringAsync(OnlineRepoUrl);
            var onlineScripts = JsonConvert.DeserializeObject<List<OnlineScriptInfo>>(response);

            if (onlineScripts == null || onlineScripts.Count == 0)
            {
                sa.Log.Error("无法解析在线仓库数据");
                return (VersionCompareResult.Error, null);
            }

            var onlineScript = onlineScripts.FirstOrDefault(s =>
                s.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase));

            if (onlineScript == null)
            {
                //sa.Log.Debug($"在线仓库中未找到 GUID 为 {guid} 的脚本");
                //if (showNotification)
                //{
                //    sa.Method.TextInfo("该脚本未在在线仓库中注册", 3000);
                //}
                return (VersionCompareResult.NotFound, null);
            }

            sa.Log.Debug($"{onlineScript.Name} by {onlineScript.Author}: 找到在线脚本: {onlineScript.Name}, 在线版本: {onlineScript.Version}");

            var compareResult = CompareVersions(currentVersion, onlineScript.Version);

            if (compareResult < 0)
            {
                sa.Log.Debug($"{onlineScript.Name} by {onlineScript.Author}: 发现新版本: {onlineScript.Version} 请及时更新 (当前: {currentVersion})");
                if (showNotification)
                {
                    sa.Method.TextInfo(
                        $"{onlineScript.Name} by {onlineScript.Author}: 发现新版本: {onlineScript.Version} 请及时更新\n当前版本: {currentVersion}",
                        5000,
                        true);
                }
                return (VersionCompareResult.UpdateAvailable, onlineScript);
            }
            else
            {
                sa.Log.Debug($"{onlineScript.Name} by {onlineScript.Author}: 当前版本已是最新 (当前: {currentVersion}, 在线: {onlineScript.Version})");

                return (VersionCompareResult.UpToDate, onlineScript);
            }
        }
        catch (HttpRequestException ex)
        {
            //sa.Log.Error($"网络请求失败: {ex.Message}");
            //if (showNotification)
            //{
            //    sa.Method.TextInfo("版本检查失败: 网络错误", 3000, true);
            //}
            return (VersionCompareResult.Error, null);
        }
        catch (Exception ex)
        {
            //sa.Log.Error($"版本检查失败: {ex.Message}");
            //if (showNotification)
            //{
            //    sa.Method.TextInfo("版本检查失败", 3000, true);
            //}
            return (VersionCompareResult.Error, null);
        }
    }

    /// <summary>
    /// 比较版本号
    /// </summary>
    /// <param name="version1">版本1 (例如: "0.0.0.3")</param>
    /// <param name="version2">版本2 (例如: "0.0.0.5")</param>
    /// <returns>负数: version1 < version2, 0: 相等, 正数: version1 > version2</returns>
    private static int CompareVersions(string version1, string version2)
    {
        var v1Parts = version1.Split('.').Select(p => int.TryParse(p, out var num) ? num : 0).ToArray();
        var v2Parts = version2.Split('.').Select(p => int.TryParse(p, out var num) ? num : 0).ToArray();

        int maxLength = Math.Max(v1Parts.Length, v2Parts.Length);

        for (int i = 0; i < maxLength; i++)
        {
            int v1Part = i < v1Parts.Length ? v1Parts[i] : 0;
            int v2Part = i < v2Parts.Length ? v2Parts[i] : 0;

            if (v1Part < v2Part) return -1;
            if (v1Part > v2Part) return 1;
        }

        return 0;
    }
}
#endregion

public static class HelperExtensions
{
    public static unsafe uint GetCurrentTerritoryId()
    {
        return AgentMap.Instance()->CurrentTerritoryId;
    }
}
