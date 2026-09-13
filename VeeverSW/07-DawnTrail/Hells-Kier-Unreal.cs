﻿using System;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using System.Reflection.Metadata;
using System.Net;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using FFXIVClientStructs;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Runtime.Intrinsics.Arm;
using FFXIVClientStructs.FFXIV.Client.Game.Character;


namespace Veever.DawnTrail.Hells_Kier_Unreal;

[ScriptType(name: "LV.100 朱雀幻巧战", territorys: [1272], guid: "60468283-702c-4ddb-95db-fd81409d5630",
    version: "0.0.0.8", author: "Veever", note: noteStr)]

public class Hells_Kier_Unreal
{
    const string noteStr =
    """
    v0.0.0.8:
    1. 本脚本使用攻略为菓子攻略，请在打本之前调整好! 可达鸭的小队排序!!（很重要，影响指路和机制播报）
    2. 如果懒得调也不想看需要小队位置判定的指路，可以在用户设置里面关闭指路开关
    3. 用户设置里面新加入场景标点设置(开局放置ABCD标点)(需要ACT鲶鱼精), 可能在未来弄一个不需要鲶鱼精的方法
    4. 如果需要某个机制的绘画或者哪里出了问题请在dc@我或者私信我
    鸭门。
    """;

    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = true;

    [UserSetting("指路开关")]
    public bool isLead { get; set; } = true;

    [UserSetting("标点开关")]
    public bool isMark { get; set; } = true;

    [UserSetting("本地标点开关(打开则为本地开关，关闭则为小队)")]
    public bool LocalMark { get; set; } = true;

    [UserSetting("是否进行场地标点")]
    public bool PostNamazuPrint { get; set; } = true;

    [UserSetting("鲶鱼精邮差端口设置")]
    public int PostNamazuPort { get; set; } = 2019;

    [UserSetting("场地标点是否为本地标点")]
    public bool PostNamazuisLocal { get; set; } = true;

    [UserSetting("Debug开关, 非开发用请关闭")]
    public bool isDebug { get; set; } = false;


    private static readonly Vector3 posA = new Vector3(100f, 0.00f, 82.70f);
    private static readonly Vector3 posB = new Vector3(118.36f, 0.00f, 100f);
    private static readonly Vector3 posC = new Vector3(100f, 0.00f, 118.21f);
    private static readonly Vector3 posD = new Vector3(81.5f, 0.00f, 100f);


    public int isX;
    public int isIncandescent = 0;
    public int isMesmerizingMelody = 0;
    public int isRuthlessRefrain = 0;
    public int IncandescentCount = 0;

    private readonly object IncandescentCountLock = new object();


    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        PostWaymark(accessory);
        isIncandescent = 0;
        isMesmerizingMelody = 0;
        isRuthlessRefrain = 0;
        IncandescentCount = 0;
    }

    public void PostWaymark(ScriptAccessory accessory)
    {
        var waymark = new NamazuHelper.Waymark(accessory);
        waymark.AddWaymarkType("A", posA);
        waymark.AddWaymarkType("B", posB);
        waymark.AddWaymarkType("C", posC);
        waymark.AddWaymarkType("D", posD);
        waymark.SetJsonPayload(LocalMark, PostNamazuisLocal);
        waymark.PostWaymarkCommand(PostNamazuPort);
    }

    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    [ScriptMethod(name: "debug", eventType: EventTypeEnum.Chat, eventCondition: ["Message:debug"])]
    public async void debug(Event @event, ScriptAccessory accessory)
    {
        DebugMsg($"isIncandescent: {isIncandescent}", accessory);
    }


    #region P1
    [ScriptMethod(name: "AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(43004|43015)$"])]
    public void Boss1AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 2700, true);
        if (isTTS) accessory.Method.TTS($"AOE");
    }

    [ScriptMethod(name: "Rout-突进", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43027"])]
    public void SedimentaryDebris(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Rout";
        dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(6f, 55);
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "008B-分散", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:008B"])]
    public void ElectricExcess(Event @event, ScriptAccessory accessory)
    {
        lock (IncandescentCountLock)
        {
            DebugMsg($"isIncandescent: {isIncandescent}", accessory);
            var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);

            var posN = new Vector3(100.01f, 0.00f, 93.97f);
            var posNE = new Vector3(104.16f, 0.00f, 95.96f);
            var posE = new Vector3(106.35f, 0.00f, 100.04f);
            var posSE = new Vector3(104.70f, -0.00f, 104.14f);
            var posS = new Vector3(100.12f, -0.00f, 106.30f);
            var posSW = new Vector3(95.76f, 0.00f, 104.66f);
            var posW = new Vector3(93.72f, 0.00f, 100.07f);
            var posNW = new Vector3(96.21f, 0.00f, 95.33f);

            if (isIncandescent == 1)
            {
                DebugMsg($"IncandescentCount:{IncandescentCount}",accessory);
                if (@event.TargetId() == accessory.Data.Me)
                {
                    switch (index)
                    {
                        case 0:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posNE, new Vector2(1f, 1f), 5000, "TH_NE", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 1:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posSE, new Vector2(1f, 1f), 5000, "TH_SE", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 2:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posNW, new Vector2(1f, 1f), 5000, "TH_NW", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 3:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posSW, new Vector2(1f, 1f), 5000, "TH_SW", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 4:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posNE, new Vector2(1f, 1f), 5000, "DPS_NE", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 5:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posSE, new Vector2(1f, 1f), 5000, "DPS_SE", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 6:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posNW, new Vector2(1f, 1f), 5000, "DPS_NW", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 7:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posSW, new Vector2(1f, 1f), 5000, "DPS_SW", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                    }
                }
                else
                {
                    IncandescentCount++;
                }

                if (IncandescentCount > 3)
                {
                    switch (index)
                    {
                        case 0:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posN, new Vector2(1f, 1f), 5000, "TH_N", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 1:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posE, new Vector2(1f, 1f), 5000, "TH_E", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 2:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posW, new Vector2(1f, 1f), 5000, "TH_W", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 3:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posS, new Vector2(1f, 1f), 5000, "TH_S", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 4:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posN, new Vector2(1f, 1f), 5000, "DPS_N", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 5:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posE, new Vector2(1f, 1f), 5000, "DPS_E", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 6:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posW, new Vector2(1f, 1f), 5000, "DPS_W", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                        case 7:
                            if (isLead) DrawHelper.DrawDisplacement(accessory, posS, new Vector2(1f, 1f), 5000, "DPS_S", color: new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                            break;
                    }

                    IncandescentCount = 0;
                }

            }
            else
            {
                if (@event.TargetId() == accessory.Data.Me)
                {
                    if (isText) accessory.Method.TextInfo("分散, 不要重叠", duration: 4000, true);
                    if (isTTS) accessory.Method.TTS($"分散, 不要重叠");
                }
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "008B-分散";
                dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 251 / 255.0f, 1.0f);
                dp.Owner = @event.TargetId();
                dp.Scale = new Vector2(6);
                dp.DestoryAt = 4800;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }

        }
    }

    [ScriptMethod(name: "翼宿击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43005"])]
    public void FleetingSummer(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离Boss正面", duration: 2700, true);
        if (isTTS) accessory.Method.TTS($"远离Boss正面");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Fleeting Summer";
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(40);
        dp.Radian = float.Pi / 2;
        dp.DestoryAt = 2700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "赤热击-死刑播报", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43003"])]
    public void Cremate(Event @event, ScriptAccessory accessory)
    {
        var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        if (@event.TargetId == accessory.Data.Me)
        {
            if (isText) accessory.Method.TextInfo("死刑点名, 注意减伤", duration: 2500, true);
            if (isTTS)  accessory.Method.TTS($"死刑点名, 注意减伤");
        }
        else if (index == 0 || index == 1 || index == 2 || index == 3)
        {
            if (isText) accessory.Method.TextInfo($"死刑点名{@event.TargetName()}", duration: 2500, true);
            if (isTTS) accessory.Method.TTS($"死刑点名{@event.TargetName()}");
        }
    }

    [ScriptMethod(name: "羽毛检测", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(18388|18387)$"])]
    public async void FeatherCheck(Event @event, ScriptAccessory accessory)
    {
        // 18388 大羽
        // 18387 小羽
        var posN = new Vector3(100.00f, 0.00f, 85.00f);
        var posNE = new Vector3(110.60f, 0.00f, 89.40f);
        var posE = new Vector3(115.00f, 0.00f, 100.00f);
        var posSE = new Vector3(110.60f, 0.00f, 110.60f);
        var posS = new Vector3(100.00f, 0.00f, 115.00f);
        var posSW = new Vector3(89.40f, 0.00f, 110.60f);
        var posW = new Vector3(85.00f, 0.00f, 100.00f);
        var posNW = new Vector3(89.40f, 0.00f, 89.40f);


        var posN_away = new Vector3(99.51f, 0.00f, 82.63f);
        var posNE_away = new Vector3(112.55f, 0.00f, 87.18f);
        var posE_away = new Vector3(118.26f, 0.00f, 99.71f);
        var posSE_away = new Vector3(112.78f, 0.00f, 112.55f);
        var posS_away = new Vector3(99.94f, 0.00f, 118.17f);
        var posSW_away = new Vector3(86.91f, 0.00f, 113.00f);
        var posW_away = new Vector3(81.54f, 0.00f, 100.19f);
        var posNW_away = new Vector3(86.65f, 0.00f, 87.23f);


        var posSW_DPSX = new Vector3(93.19f, 0.00f, 106.86f);
        var posSE_DPSX = new Vector3(106.59f, 0.00f, 107.08f);
        var posNW_DPSX = new Vector3(93.13f, 0.00f, 92.92f);
        var posNE_DPSX = new Vector3(106.87f, 0.00f, 93.15f);

        var checkPos = posN;

        if (@event.SourcePosition() != checkPos)
        {
            return;
        }


        if (@event.DataId() == 18387)
        {
            isX = 0;
        } else
        {
            isX = 1;
        }

        DebugMsg($"isx: {isX}", accessory);

        await Task.Delay(1000);
        // MT: 0
        // ST: 1
        // H1: 2
        // H2: 3
        // D1: 4
        // D2: 5
        // D3: 6
        // D4: 7
        var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        switch (index)
        {
            case 0:
            case 4:
                if (isX == 1)
                {
                    // NW
                    DebugMsg("is NW", accessory);
                    if (isText) accessory.Method.TextInfo("攻击指定羽毛，不要攻击尾羽", duration: 4500, true);
                    if (isTTS) accessory.Method.TTS($"攻击指定羽毛，不要攻击尾羽");
                    if (isLead) DrawHelper.DrawDisplacement(accessory, posNW, new Vector2(2f, 2f), 6000, "NW——Red", color: accessory.Data.DefaultDangerColor);

                    if (index == 4)
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posNW_DPSX, new Vector2(1f, 1f), 5000, "DPS_NW", delay: 7000);
                    }
                    else
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posNW, new Vector2(1f, 1f), 5000, "NW", delay: 7000);
                    }

                    if (isLead) DrawHelper.DrawDisplacement(accessory, posNW_away, new Vector2(1f, 1f), 11000, "NW_away", delay: 13000);
                    await Task.Delay(13000);
                    if (index == 4)
                    {
                        if (isText) accessory.Method.TextInfo("引导火焰鸟至指定地点, 并迅速击杀", duration: 4500, true);
                        if (isTTS) accessory.Method.TTS($"引导火焰鸟至指定地点, 并迅速击杀");
                    }
                } 
                else
                {
                    // N
                    DebugMsg("is N", accessory);
                    if (isText) accessory.Method.TextInfo("攻击指定羽毛，不要攻击尾羽", duration: 4500, true);
                    if (isTTS) accessory.Method.TTS($"攻击指定羽毛，不要攻击尾羽");
                    if (isLead) DrawHelper.DrawDisplacement(accessory, posN, new Vector2(2f, 2f), 6000, "N——Red", color: accessory.Data.DefaultDangerColor);
                    
                    if (index == 4)
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posNW_DPSX, new Vector2(1f, 1f), 5000, "DPS_NW", delay: 7000);
                    }
                    else
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posN, new Vector2(1f, 1f), 5000, "N", delay: 7000);
                    }

                    if (isLead) DrawHelper.DrawDisplacement(accessory, posN_away, new Vector2(1f, 1f), 11000, "N_away", delay: 13000);
                    await Task.Delay(13000);
                    if (index == 4)
                    {
                        if (isText) accessory.Method.TextInfo("引导火焰鸟至指定地点, 并迅速击杀", duration: 4500, true);
                        if (isTTS) accessory.Method.TTS($"引导火焰鸟至指定地点, 并迅速击杀");
                    }
                }

                return;


            case 1:
            case 5:
                if (isX == 1)
                {
                    // NE
                    DebugMsg("is NE", accessory);
                    if (isText) accessory.Method.TextInfo("攻击指定羽毛，不要攻击尾羽", duration: 4500, true);
                    if (isTTS) accessory.Method.TTS($"攻击指定羽毛，不要攻击尾羽");
                    if (isLead) DrawHelper.DrawDisplacement(accessory, posNE, new Vector2(2f, 2f), 6000, "NE——Red", color: accessory.Data.DefaultDangerColor);
                    
                    if (index == 5)
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posNE_DPSX, new Vector2(1f, 1f), 5000, "DPS_NE", delay: 7000);
                    }
                    else
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posNE, new Vector2(1f, 1f), 5000, "NE", delay: 7000);
                    }

                    if (isLead) DrawHelper.DrawDisplacement(accessory, posNE_away, new Vector2(1f, 1f), 11000, "NE_away", delay: 13000);
                    await Task.Delay(13000);
                    if (index == 5)
                    {
                        if (isText) accessory.Method.TextInfo("引导火焰鸟至指定地点, 并迅速击杀", duration: 4500, true);
                        if (isTTS) accessory.Method.TTS($"引导火焰鸟至指定地点, 并迅速击杀");
                    }
                }
                else
                {
                    // E
                    DebugMsg("is E", accessory);
                    if (isText) accessory.Method.TextInfo("攻击指定羽毛，不要攻击尾羽", duration: 4500, true);
                    if (isTTS) accessory.Method.TTS($"攻击指定羽毛，不要攻击尾羽");
                    if (isLead) DrawHelper.DrawDisplacement(accessory, posE, new Vector2(2f, 2f), 6000, "E——Red", color: accessory.Data.DefaultDangerColor);
                    
                    if (index == 5)
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posNE_DPSX, new Vector2(1f, 1f), 5000, "DPS_NE", delay: 7000);
                    }
                    else
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posE, new Vector2(1f, 1f), 5000, "E", delay: 7000);
                    }

                    if (isLead) DrawHelper.DrawDisplacement(accessory, posE_away, new Vector2(1f, 1f), 11000, "E_away", delay: 13000);
                    await Task.Delay(13000);
                    if (index == 5)
                    {
                        if (isText) accessory.Method.TextInfo("引导火焰鸟至指定地点, 并迅速击杀", duration: 4500, true);
                        if (isTTS) accessory.Method.TTS($"引导火焰鸟至指定地点, 并迅速击杀");
                    }
                }

                return;

            case 2:
            case 6:
                if (isX == 1)
                {
                    // SW
                    DebugMsg("is SW", accessory);
                    if (isText) accessory.Method.TextInfo("攻击指定羽毛，不要攻击尾羽", duration: 4500, true);
                    if (isTTS) accessory.Method.TTS($"攻击指定羽毛，不要攻击尾羽");
                    if (isLead) DrawHelper.DrawDisplacement(accessory, posSW, new Vector2(2f, 2f), 6000, "SW——Red", color: accessory.Data.DefaultDangerColor);

                    if (index == 6)
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posSW_DPSX, new Vector2(1f, 1f), 5000, "DPS_SW", delay: 7000);
                    }
                    else
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posSW, new Vector2(1f, 1f), 5000, "SW", delay: 7000);
                    }

                    if (isLead) DrawHelper.DrawDisplacement(accessory, posSW_away, new Vector2(1f, 1f), 11000, "SW_away", delay: 13000);
                    await Task.Delay(13000);
                    if (index == 6)
                    {
                        if (isText) accessory.Method.TextInfo("引导火焰鸟至指定地点, 并迅速击杀", duration: 4500, true);
                        if (isTTS) accessory.Method.TTS($"引导火焰鸟至指定地点, 并迅速击杀");
                    }
                }
                else
                {
                    // W
                    DebugMsg("is W", accessory);

                    if (isText) accessory.Method.TextInfo("攻击指定羽毛，不要攻击尾羽", duration: 4500, true);
                    if (isTTS) accessory.Method.TTS($"攻击指定羽毛，不要攻击尾羽");
                    if (isLead) DrawHelper.DrawDisplacement(accessory, posW, new Vector2(2f, 2f), 6000, "W——Red", color: accessory.Data.DefaultDangerColor);

                    if (index == 6)
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posSW_DPSX, new Vector2(1f, 1f), 5000, "DPS_SW", delay: 7000);
                    }
                    else
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posW, new Vector2(1f, 1f), 5000, "W", delay: 7000);
                    }

                    if (isLead) DrawHelper.DrawDisplacement(accessory, posW_away, new Vector2(1f, 1f), 11000, "W_away", delay: 13000);
                    await Task.Delay(13000);
                    if (index == 6)
                    {
                        if (isText) accessory.Method.TextInfo("引导火焰鸟至指定地点, 并迅速击杀", duration: 4500, true);
                        if (isTTS) accessory.Method.TTS($"引导火焰鸟至指定地点, 并迅速击杀");
                    }
                }

                return;


            case 3:
            case 7:
                if (isX == 1)
                {
                    // SE
                    DebugMsg("is SE", accessory);
                    if (isText) accessory.Method.TextInfo("攻击指定羽毛，不要攻击尾羽", duration: 4500, true);
                    if (isTTS) accessory.Method.TTS($"攻击指定羽毛，不要攻击尾羽");
                    if (isLead) DrawHelper.DrawDisplacement(accessory, posSE, new Vector2(2f, 2f), 6000, "SE——Red", color: accessory.Data.DefaultDangerColor);


                    if (index == 7)
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posSE_DPSX, new Vector2(1f, 1f), 5000, "DPS_SE", delay: 7000);
                    }
                    else
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posSE, new Vector2(1f, 1f), 5000, "SE", delay: 7000);
                    }

                    if (isLead) DrawHelper.DrawDisplacement(accessory, posSE_away, new Vector2(1f, 1f), 11000, "SE_away", delay: 13000);
                    await Task.Delay(13000);
                    if (index == 7)
                    {
                        if (isText) accessory.Method.TextInfo("引导火焰鸟至指定地点, 并迅速击杀", duration: 4500, true);
                        if (isTTS) accessory.Method.TTS($"引导火焰鸟至指定地点, 并迅速击杀");
                    }
                }
                else
                {
                    // S
                    DebugMsg("is S", accessory);

                    if (isText) accessory.Method.TextInfo("攻击指定羽毛，不要攻击尾羽", duration: 4500, true);
                    if (isTTS) accessory.Method.TTS($"攻击指定羽毛，不要攻击尾羽");
                    if (isLead) DrawHelper.DrawDisplacement(accessory, posS, new Vector2(2f, 2f), 6000, "S——Red", color: accessory.Data.DefaultDangerColor);

                    if (index == 7)
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posSE_DPSX, new Vector2(1f, 1f), 5000, "DPS_SE", delay: 7000);
                    }
                    else
                    {
                        if (isLead) DrawHelper.DrawDisplacement(accessory, posS, new Vector2(1f, 1f), 5000, "S", delay: 7000);
                    }

                    if (isLead) DrawHelper.DrawDisplacement(accessory, posS_away, new Vector2(1f, 1f), 11000, "S_away", delay: 13000);
                    await Task.Delay(13000);
                    if (index == 7)
                    {
                        if (isText) accessory.Method.TextInfo("引导火焰鸟至指定地点, 并迅速击杀", duration: 4500, true);
                        if (isTTS) accessory.Method.TTS($"引导火焰鸟至指定地点, 并迅速击杀");
                    }
                }


                return;
        }
    }
    #endregion

    #region Switch
    // ObjectEffect
    // Down: Id1: 64; Id2: 32
    // Up:   Id1: 8 ; Id2: 4
    // Right: Id1: 512; Id2: 256
    // Left: Id1: 4096; Id2: 2048

    // var posTopLeft = new Vector3(94.30f, 0.00f, 94.30f);    
    // var posLeft = new Vector3(92.00f, 0.00f, 100.00f);      
    // var posBottomLeft = new Vector3(94.30f, 0.00f, 105.70f); 
    // var posBottom = new Vector3(100.00f, 0.00f, 108.00f);   
    // var posBottomRight = new Vector3(105.70f, 0.00f, 105.70f); 
    // var posRight = new Vector3(108.00f, 0.00f, 100.00f);   
    // var posTopRight = new Vector3(105.70f, 0.00f, 94.30f); 
    // var posTop = new Vector3(100.00f, 0.00f, 92.00f);
    [ScriptMethod(name: "转场AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(43009)$"])]
    public void HeavyAOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("大AOE", duration: 6700, true);
        if (isTTS) accessory.Method.TTS($"大AOE");
    }
    #endregion

    #region Main

    [ScriptMethod(name: "引诱旋律", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43010"])]
    public void MesmerizingMelody(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离中心吸引", duration: 6700, true);
        if (isTTS) accessory.Method.TTS($"远离中心吸引");
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "MesmerizingMelody";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(15);
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

    }

    [ScriptMethod(name: "拒绝旋律", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43011"])]
    public void RuthlessRefrain(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("靠近中心击退", duration: 6700, true);
        if (isTTS) accessory.Method.TTS($"靠近中心击退");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "MesmerizingMelody";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = @event.TargetPosition();
        dp.Rotation = float.Pi;
        dp.Scale = new Vector2(2,11);
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
 

    [ScriptMethod(name: "井宿焰", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43017"])]
    public void WellofFlame(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离Boss正面", duration: 3700, true);
        if (isTTS) accessory.Method.TTS($"远离Boss正面");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Well of Flame";
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(20, 41);
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        isIncandescent = 0;
    }

    [ScriptMethod(name: "00A1-素质三连分摊", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:00A1"])]
    public void zerozeroA1(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"00A1-素质三连分摊";
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(6f);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        var pos = new Vector3(100.01f, 0.00f, 106.51f);
        var posMT = new Vector3(100.12f, 0.00f, 92.58f);
        var bossid = IbcHelper.GetFirstByDataId(accessory, 18385);
        if (index != 0 || index != 1)
        {
            if (isText) accessory.Method.TextInfo($"与{@event.TargetName()}分摊", duration: 3700, true);
            if (isTTS) accessory.Method.TTS($"与{@event.TargetName()}分摊");
            if (isLead) DrawHelper.DrawDisplacement(accessory, pos, new Vector2(1, 1), 6000, "分摊闲人引导");
        } else
        {
            if (index == 0 && bossid?.TargetObjectId == accessory.Data.Me)
            {
                if (isLead) DrawHelper.DrawDisplacement(accessory, posMT, new Vector2(1, 1), 5000, "分摊MT引导");
            } else if (index == 1 && bossid?.TargetObjectId == accessory.Data.Me)
            {
                if (isLead) DrawHelper.DrawDisplacement(accessory, posMT, new Vector2(1, 1), 5000, "分摊ST引导");
            }
            
        }
        
    }

    [ScriptMethod(name: "鬼宿脚", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(43012|43014)$"])]
    public async void PhantomFlurry(Event @event, ScriptAccessory accessory)
    {
        if (@event.ActionId() == 43012) 
        {
            var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            if (index == 0 || index == 1)
            {
                if (isText) accessory.Method.TextInfo($"鬼宿脚，准备减伤并换T，随后远离Boss正面", duration: 4000, true);
                if (isTTS) accessory.Method.TTS($"鬼宿脚，准备减伤并换T，随后远离Boss正面");
            } else 
            {
                if (isText) accessory.Method.TextInfo($"远离Boss正面", duration: 4000, true);
                if (isTTS) accessory.Method.TTS($"远离Boss正面");
            }
        } else 
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"鬼宿脚";
            dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            dp.Owner = @event.SourceId();
            dp.Scale = new Vector2(41f);
            dp.Radian = float.Pi;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }


    [ScriptMethod(name: "东西南北炎", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(18394|18393|18392|18391)$"])]
    public unsafe void Fire(Event @event, ScriptAccessory accessory)
    {
        // 18391    North
        // 18392    East
        // 18393    South
        // 18394    West
        if (accessory.Data.Objects == null)
        {
            DebugMsg("Objects is null", accessory);
            return;
        }

        var battleCharas = accessory.Data.Objects.OfType<IBattleChara>();

        uint[] playersNorth = ScanTether(@event, accessory, 18391u);
        foreach (var player in playersNorth)
        {
            if (player != accessory.Data.Me) continue;
            DebugMsg($"{player}", accessory);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "playersNorth";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = player;
            dp.Scale = new Vector2(2, 25);
            dp.Rotation = float.Pi;
            dp.DestoryAt = 10000;
            dp.FixRotation = true;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        uint[] playersEast = ScanTether(@event, accessory, 18392u);
        foreach (var player in playersEast)
        {
            if (player != accessory.Data.Me) continue;
            DebugMsg($"{player}", accessory);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "playersEast";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = player;
            dp.Scale = new Vector2(2, 25);
            dp.Rotation = float.Pi / 2;
            dp.DestoryAt = 10000;
            dp.FixRotation = true;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }


        uint[] playersSouth = ScanTether(@event, accessory, 18393u);
        foreach (var player in playersSouth) 
        {
            if (player != accessory.Data.Me) continue;
            DebugMsg($"{player}", accessory);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "playersSouth";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = player;
            dp.Scale = new Vector2(2, 25);
            dp.Rotation = 0;
            dp.DestoryAt = 10000;
            dp.FixRotation = true;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }



        uint[] playersWest = ScanTether(@event, accessory, 18394u);
        foreach (var player in playersWest)
        {
            if (player != accessory.Data.Me) continue;
            DebugMsg($"{player}", accessory);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "playersWest";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = player;
            dp.Scale = new Vector2(2, 25);
            dp.Rotation = -float.Pi / 2;
            dp.DestoryAt = 10000;
            dp.FixRotation = true;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

    }
    
    [ScriptMethod(name: "红莲炎-临时", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43018"])]
    public void Hotspot(Event @event, ScriptAccessory accessory) 
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "红莲炎";
        dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(21);
        dp.Rotation = @event.SourceRotation();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.DestoryAt = 1000;
        dp.FixRotation = true;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    // [ScriptMethod(name: "红莲炎-小鸟面向", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:18390"])]
    // public void HotspotBird(Event @event, ScriptAccessory accessory) 
    // {
    //     var dp = accessory.Data.GetDefaultDrawProperties();
    //     dp.Name = "红莲炎-小鸟面向";
    //     dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    //     dp.Owner = @event.SourceId();
    //     dp.Scale = new Vector2(5);
    //     dp.Rotation = @event.SourceRotation() + float.Pi;
    //     dp.ScaleMode = ScaleMode.ByTime;
    //     dp.DestoryAt = 120000;
    //     accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
    // }
 

    [ScriptMethod(name: "灼热旋律check", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42998"])]
    public void IncandescentInterlude(Event @event, ScriptAccessory accessory)
    {
        isIncandescent = 1;
        DebugMsg("IncandescentInterlude Check", accessory);
    }

    //[ScriptMethod(name: "灼热旋律结束check", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2009599"])]
    //public void IncandescentInterludeFinish(Event @event, ScriptAccessory accessory)
    //{
    //    isIncandescent = 0;
    //    DebugMsg("IncandescentInterlude Finish", accessory);
    //}






    #endregion

    public static float GetStatusRemainingTime(ScriptAccessory sa, IBattleChara? battleChara, uint statusId)
    {
        if (battleChara == null || !battleChara.IsValid()) return 0;
        unsafe
        {
            BattleChara* charaStruct = (BattleChara*)battleChara.Address;
            var statusIdx = charaStruct->GetStatusManager()->GetStatusIndex(statusId);
            return charaStruct->GetStatusManager()->GetRemainingTime(statusIdx);
        }
    }


    private unsafe uint[] ScanTether(Event evt, ScriptAccessory sa, uint id)
    {
        if (sa?.Data?.Objects == null) return Array.Empty<uint>();
        List<uint> dataId = [id];
        List<uint> players = [];
        foreach (var fire in sa.Data.Objects.Where(x => dataId.Contains(x.DataId)))
        {
            if (fire?.Address == null) continue;
            var targetId = ((BattleChara*)fire.Address)->Vfx.Tethers[0].TargetId.ObjectId;
            players.Add(targetId);
        }
        DebugMsg($"players: {string.Join(", ", players)}", sa);
        return players.ToArray();
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

        public static void DrawCircle(ScriptAccessory accessory, Vector3 position, Vector2 scale, int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Position = position;
            dp.Scale = scale;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        public static void DrawDisplacement(ScriptAccessory accessory, Vector3 target, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = accessory.Data.Me;
            dp.Color = color ?? accessory.Data.DefaultSafeColor;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.TargetPosition = target;
            dp.Scale = scale;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
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

        public static void DrawFan(ScriptAccessory accessory, Vector3 position, float rotation, Vector2 scale, float angle, int duration, string name, Vector4? color = null, int delay = 0)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Position = position;
            dp.Rotation = rotation;
            dp.Scale = scale;
            dp.Radian = angle;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
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

        public static void DrawArrow(ScriptAccessory accessory, Vector3 startPosition, Vector3 endPosition, float width, int duration, string name, Vector4? color = null, int delay = 0)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Position = startPosition;
            dp.TargetPosition = endPosition;
            dp.Scale = new Vector2(width, 1);
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
    }

}


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

    public static uint Id(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["Id"]);
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

    public static uint DataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DataId"]);
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

    public static byte Index(this Event @event)
    {
        return (byte)(ParseHexId(@event["Index"], out var index) ? index : 0);
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

    public static string Operate(this Event @event)
    {
        return @event["Operate"];
    }
}

public static class IbcHelper
{
    public static KodakkuAssist.Data.IGameObject? GetById(ScriptAccessory accessory, uint id)
    {
        return accessory.Data.Objects.SearchByEntityId(id);
    }

    public static KodakkuAssist.Data.IGameObject? GetMe(ScriptAccessory accessory)
    {
        return accessory.Data.Objects.SearchByEntityId(accessory.Data.Me);
    }

    public static KodakkuAssist.Data.IGameObject? GetFirstByDataId(ScriptAccessory accessory, uint dataId)
    {
        return accessory.Data.Objects.Where(x => x.DataId == dataId).FirstOrDefault();
    }

    public static IEnumerable<KodakkuAssist.Data.IGameObject> GetByDataId(ScriptAccessory accessory, uint dataId)
    {
        return accessory.Data.Objects.Where(x => x.DataId == dataId);
    }

    public static IEnumerable<KodakkuAssist.Data.IGameObject> GetParty(ScriptAccessory accessory)
    {
        foreach (var pid in accessory.Data.PartyList)
        {
            var obj = accessory.Data.Objects.SearchByEntityId(pid);
            if (obj != null) yield return obj;
        }
    }

    public static IEnumerable<KodakkuAssist.Data.IGameObject> GetPartyEntities(ScriptAccessory accessory)
    {
        return accessory.Data.Objects.Where(obj => accessory.Data.PartyList.Contains(obj.EntityId));
    }

    public static bool HasStatus(this IBattleChara ibc, uint statusId)
    {
        return ibc.StatusList.Any(x => x.StatusId == statusId);
    }

    public static bool HasStatusAny(this IBattleChara ibc, uint[] statusIds)
    {
        return ibc.StatusList.Any(x => statusIds.Contains(x.StatusId));
    }

    public static unsafe uint Tethering(this IBattleChara ibc, int index = 0)
    {
        return ((BattleChara*)ibc.Address)->Vfx.Tethers[index].TargetId.ObjectId;
    }
}

public static class NamazuHelper
{
    public class NamazuCommand(ScriptAccessory accessory, string url, string command, string param)
    {
        private ScriptAccessory accessory { get; set; } = accessory;
        private string _url = url;

        public void PostCommand()
        {
            var url = $"{_url}/{command}";
            //accessory.Method.SendChat($"/e 向{url}发送{param}");
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