using System;
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
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Veever.DawnTrail.The_Jade_Stoa_Unreal;

[ScriptType(name: "LV.100 白虎幻巧战", territorys: [1239], guid: "29193d9d-a2c5-4a0d-875b-943a06790b95",
    version: "0.0.0.5", author: "Veever", note: noteStr)]

public class The_Jade_Stoa_Unreal
{
    const string noteStr =
    """
    v0.0.0.5:
    1. 本脚本使用攻略为子言攻略，请在打本之前调整好!可达鸭的小队排序!（很重要，影响指路和机制播报）
    2. 如果懒得调也不想看需要小队位置判定的指路，可以在用户设置里面关闭指路开关
    鸭门。
    """;

    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = false;

    [UserSetting("指路开关")]
    public bool isLead { get; set; } = true;

    [UserSetting("标点开关")]
    public bool isMark { get; set; } = true;

    [UserSetting("本地标点开关(打开则为本地开关，关闭则为小队)")]
    public bool LocalMark { get; set; } = true;

    [UserSetting("Debug开关, 非开发用请关闭")]
    public bool isDebug { get; set; } = false;

    public KodakkuAssist.Data.IGameObject? Boss { get; set; }

    public int HighestStakesCount;
    public int OminousWindMarkerTTSCount;
    public int HakuteiNotifyCount;
    public int ShockStrikeCount;

    public bool isMTGroup;

    private readonly object OminousWindMarkerTTSLock = new object();
    private bool isDRTTS = false;

    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        HighestStakesCount = 0;
        OminousWindMarkerTTSCount = 0;
        HakuteiNotifyCount = 0;
        ShockStrikeCount = 0;
    }

    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    [ScriptMethod(name: "debug", eventType: EventTypeEnum.Chat, eventCondition: ["Message:debug"])]
    public async void debug(Event @event, ScriptAccessory accessory)
    {
        //DebugMsg($"Me:{IbcHelper.GetMe().Name}", accessory);
        //DebugMsg($"job:{IbcHelper.GetMe().ClassJob.Value.Name}", accessory);
    }

    #region Phase 1
    [ScriptMethod(name: "风雷波动", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:39933"])]
    public void StormPulse(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 3000, true);
        accessory.TTS("AOE", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "天雷掌", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:39931"])]
    public void Tankbuster(Event @event, ScriptAccessory accessory)
    {
        var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        if (index == 0)
        {
            if (isText) accessory.Method.TextInfo("范围死刑, 注意减伤", duration: 2500, true);
            accessory.TTS("范围死刑, 注意减伤", isTTS, isDRTTS);
        } else
        {
            if (isText) accessory.Method.TextInfo("范围死刑, 远离MT", duration: 2500, true);
            accessory.TTS("范围死刑, 远离MT", isTTS, isDRTTS);
        }
    }

    [ScriptMethod(name: "乾坤一掷位置记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:003E"])]
    public void HighestStakesPosRecord(Event @event, ScriptAccessory accessory)
    {
        uint id = @event.TargetId();
        var boss = IbcHelper.GetById(accessory, id);
        if (boss == null)
        {
            DebugMsg("未找到对应的对象", accessory);
            return;
        }

        Boss = boss;
    }

    [ScriptMethod(name: "乾坤一掷", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:39939"])]
    public void HighestStakes(Event @event, ScriptAccessory accessory)
    {
        var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        isMTGroup = (HighestStakesCount % 2 == 0) ? true : false;
        if (Boss == null)
        {
            return;
        }

        if (isMTGroup == true)
        {
            if (index == 1)
            {
                if (isText) accessory.Method.TextInfo("挑衅Boss", duration: 3700, true);
                accessory.TTS("挑衅Boss", isTTS, isDRTTS);
            } else
            {
                if (isText) accessory.Method.TextInfo("MT组前往分摊", duration: 3700, true);
                accessory.TTS("MT组前往分摊", isTTS, isDRTTS);

                var dp = accessory.Data.GetDefaultDrawProperties();

                dp.Name = "乾坤一掷";
                dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
                dp.Position = Boss.Position;
                dp.Scale = new Vector2(6);
                dp.DestoryAt = 5000;
                dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                DebugMsg($"Me:{index}", accessory);
                if (index == 2 || index == 4 || index == 5)
                {
                    DebugMsg($"Me:MT组", accessory);
                    var dp1 = accessory.Data.GetDefaultDrawProperties();
                    dp1.Name = $"乾坤一掷指路MT组";
                    dp1.Owner = accessory.Data.Me;
                    dp1.Color = accessory.Data.DefaultSafeColor;
                    dp1.ScaleMode |= ScaleMode.YByDistance;
                    dp1.TargetPosition = dp.Position;
                    dp1.Scale = new(2);
                    dp1.DestoryAt = 4500;
                    if (isLead) accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
                }
            }
        }

        if (isMTGroup == false)
        {
            if (isText) accessory.Method.TextInfo("ST组前往分摊", duration: 3700, true);
            accessory.TTS("ST组前往分摊", isTTS, isDRTTS);

            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "乾坤一掷";
            dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            dp.Position = Boss.Position;
            dp.Scale = new Vector2(6);
            dp.DestoryAt = 5000;
            dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            DebugMsg($"Me:{index}", accessory);
            if (index == 3 || index == 6 || index == 7)
            {
                DebugMsg($"Me:ST组", accessory);
                var dp1 = accessory.Data.GetDefaultDrawProperties();
                dp1.Name = $"乾坤一掷指路ST组";
                dp1.Owner = accessory.Data.Me;
                dp1.Color = accessory.Data.DefaultSafeColor;
                dp1.ScaleMode |= ScaleMode.YByDistance;
                dp1.TargetPosition = dp.Position;
                dp1.Scale = new(2);
                dp1.DestoryAt = 4500;
                if (isLead) accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
            }

        //if (Math.Abs(@event.SourceRotation()) > 1)
        //{
        //    DebugMsg($"rotation:{@event.SourceRotation()}", accessory);
        //    DebugMsg($"ABSrotation:{Math.Abs(@event.SourceRotation())}", accessory);

        //} else
        //{
        //    DebugMsg($"rotation:{@event.SourceRotation()}", accessory);

        //    DebugMsg($"ABSrotation:{Math.Abs(@event.SourceRotation())}", accessory);

 
        //    }
        }
        HighestStakesCount++;
    }

    [ScriptMethod(name: "荒弹标记", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:17801"])]
    public void AratamaMarker(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "荒弹标记";
        dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 0.784f);
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(1.5f);
        dp.InnerScale = new Vector2(1.48f);
        dp.DestoryAt = 10000;
        dp.Radian = 2 * float.Pi;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
    }

    [ScriptMethod(name: "空中东方躲球", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:39952"])]
    public void EasternBall(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "空中东方躲球标记";
        dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1f);
        var pos = @event.EffectPosition();
        pos.Y = 0;
        dp.Position = pos;
        dp.Scale = new Vector2(2f);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "妖风标记", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:1481"])]
    public void OminousWindMarker(Event @event, ScriptAccessory accessory)
    {
        lock(OminousWindMarkerTTSLock)
        {
            if (OminousWindMarkerTTSCount == 0)
            {
                if (isText) accessory.Method.TextInfo("被点名者互相远离", duration: 2500, true);
                accessory.TTS("被点名者互相远离", isTTS, isDRTTS);
            }
            OminousWindMarkerTTSCount++;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "妖风标记";
            dp.Color = new Vector4(1.0f, 0.0f, 1.0f, 1.0f);
            dp.Owner = @event.TargetId();
            dp.Scale = new Vector2(5f);
            dp.InnerScale = new Vector2(4.95f);
            dp.DestoryAt = 10000;
            dp.Radian = 2 * float.Pi;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
        }
    }

    [ScriptMethod(name: "雷火一闪(白虎-半-39930)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:39930"])]
    public void FireandLightning_39930(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离Boss正面", duration: 2500, true);
        accessory.TTS("远离Boss正面", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "雷火一闪39930";
        dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(20f, 50f);
        //dp.Radian = float.Pi;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "ST白帝拉怪提示", eventType: EventTypeEnum.Targetable, eventCondition: ["SourceName:regex:^(白帝|Hakutei)$"])]
    public void HakuteiNotify(Event @event, ScriptAccessory accessory)
    {
        var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        if (HakuteiNotifyCount == 0)
        {
            if (index == 0)
            {
                if (isText) accessory.Method.TextInfo("MT将Boss带到场地北侧", duration: 3000, true);
                accessory.TTS("MT将Boss带到场地北侧", isTTS, isDRTTS);
            }

            if (index == 1)
            {
                if (isText) accessory.Method.TextInfo("白帝出现, ST注意拉仇前往场地南侧", duration: 3000, true);
                accessory.TTS("白帝出现, ST注意拉仇前往场地南侧", isTTS, isDRTTS);
            }
        }
        HakuteiNotifyCount++;
    }

    [ScriptMethod(name: "荒弹", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0004"])]
    public void Aratama(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);

            if (index == 3 || index == 2)
            {
                if (isText) accessory.Method.TextInfo("奶妈前往场地东侧引导三次荒弹", duration: 2000, true);
                accessory.TTS("奶妈前往场地东侧引导三次荒弹", isTTS, isDRTTS);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"荒弹奶妈指路";
                dp.Owner = accessory.Data.Me;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.TargetPosition = new Vector3(18, 0, 0);
                dp.Scale = new(2);
                dp.DestoryAt = 4500;
                if (isLead) accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }

            if (index > 3)
            {
                if (isText) accessory.Method.TextInfo("DPS前往场地西侧引导三次荒弹", duration: 2000, true);
                accessory.TTS("DPS前往场地西侧引导三次荒弹", isTTS, isDRTTS);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"荒弹DPS指路";
                dp.Owner = accessory.Data.Me;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.TargetPosition = new Vector3(-18, 0, 0);
                dp.Scale = new(2);
                dp.DestoryAt = 4500;
                if (isLead) accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }
    }

    [ScriptMethod(name: "ST白帝冲提示", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0057"])]
    public void WhiteHeraldNotify(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            if (isText) accessory.Method.TextInfo("ST前往场地南侧引导白帝冲", duration: 3700, true);
            accessory.TTS("ST前往场地南侧引导白帝冲", isTTS, isDRTTS);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"ST白帝冲提示指路";
            dp.Owner = accessory.Data.Me;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.TargetPosition = new Vector3(2.13f, -0.00f, 19.39f);
            dp.Scale = new(2);
            dp.DestoryAt = 5000;
            if (isLead) accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
    }

    [ScriptMethod(name: "远雷提示(除ST)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:39934"])]
    public void DistantClap(Event @event, ScriptAccessory accessory)
    {
        var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        if (index != 1)
        {
            if (isText) accessory.Method.TextInfo("月环, 前往Boss脚下", duration: 2000, true);
            accessory.TTS("月环, 前往Boss脚下", isTTS, isDRTTS);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "远雷提示";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = @event.SourceId();
            dp.Scale = new Vector2(4f);
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = $"远雷指路";
            dp1.Owner = accessory.Data.Me;
            dp1.Color = accessory.Data.DefaultSafeColor;
            dp1.ScaleMode |= ScaleMode.YByDistance;
            dp1.TargetPosition = @event.SourcePosition();
            dp1.Scale = new(2);
            dp1.DestoryAt = 5000;
            if (isLead) accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        }
    }

    [ScriptMethod(name: "雷火一闪(白帝-全-39935)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:39935"])]
    public void FireandLightning_39935(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离直线AOE", duration: 2000, true);
        accessory.TTS("远离直线AOE", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "雷火一闪39935";
        dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(20f, 50f);
        //dp.Radian = float.Pi;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "雷轰", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:39961"])]
    public async void ShockStrike(Event @event, ScriptAccessory accessory)
    {
        if (ShockStrikeCount == 0)
        {
            if (isText) accessory.Method.TextInfo("集火白帝, 注意撞球", duration: 2500, true);
            accessory.TTS("集火白帝, 注意撞球", isTTS, isDRTTS);
        } else
        {
            if (isText) accessory.Method.TextInfo("集火白虎", duration: 2500, true);
            accessory.TTS("集火白虎", isTTS, isDRTTS);
            await Task.Delay(14700);
            if (isText) accessory.Method.TextInfo("坦克LB", duration: 2500, true);
            accessory.TTS("坦克LB", isTTS, isDRTTS);
        }

        ShockStrikeCount++;
    }
    #endregion
    #region Phase 2
    [ScriptMethod(name: "P2-1旋体脚", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:39957"])]
    public void SweeptheLeg(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("场中月环", duration: 2000, true);
        accessory.TTS("场中月环", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P2-1旋体脚标记";
        dp.Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);
        dp.Position = new Vector3(0, 0, 0);
        dp.Scale = new Vector2(25f);
        dp.InnerScale = new Vector2(5f);
        dp.DestoryAt = 4800;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Radian = 2 * float.Pi;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
    }

    [ScriptMethod(name: "p2荒弹", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:39953"])]
    public void phase2Aratama(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "p2荒弹";
        dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(2f);
        dp.DestoryAt = 4800;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P2转场大AOE", eventType: EventTypeEnum.Chat, eventCondition: ["Message:regex:^(化为灰烬吧！| To ashes with you! | 塵芥と消えるがよい！)$"])]
    public void phase2AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("大AOE", duration: 2500, true);
        accessory.TTS("大AOE", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "P2-2旋体脚", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:39932"])]
    public void P2SweeptheLeg(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("去Boss背后", duration: 3700, true);
        accessory.TTS("去Boss背后", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P2-2旋体脚";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(24f);
        dp.Radian = float.Pi / 180 * 270;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "百雷缭乱", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:39942"])]
    public void HundredfoldHavoc(Event @event, ScriptAccessory accessory)
    {
        var basePos = @event.EffectPosition();

        List<Vector3> HundredfoldHavoclistNSEW = new List<Vector3>
        {
            new Vector3(-0.02f, -0.02f, -5.02f),      //N[0]    z-5 0
            new Vector3(-5.02f, -0.02f, -0.02f),      //W[0]    x-5 1
            new Vector3(4.99f, -0.02f, -0.02f),       //E[0]    x+5 2
            new Vector3(-0.02f, -0.02f, 4.99f),       //S[0]    z+5 3

            new Vector3(-0.02f, 0f, -10.02f),      //N[1]    z-5 4
            new Vector3(-10.02f, 0f, -0.02f),      //W[1]    x-5 5
            new Vector3(9.99f, 0f, -0.02f),       //E[1]    x+5 6
            new Vector3(-0.02f, 0f, 9.99f),       //S[1]    z+5 7
        };

        List<Vector3> HundredfoldHavoclistCorner = new List<Vector3>
        {
            new Vector3(-3.56f, -0.02f, -3.56f),      //NW[0]    z-5 0
            new Vector3(-3.56f, -0.02f, 3.52f),      //SW[0]    x-5 1   707
            new Vector3(3.52f, -0.02f, 3.52f),       //SE[0]    x+5 2
            new Vector3(3.52f, -0.02f, -3.56f),       //NE[0]    z+5 3

            new Vector3(-7.12f, 0f, -7.12f),      //N[1]    z-5 4
            new Vector3(-7.12f, 0f, 7.04f),      //W[1]    x-5 5
            new Vector3(7.04f, 0f, 7.04f),       //E[1]    x+5 6
            new Vector3(7.04f, 0f, -7.12f),       //S[1]    z+5 7
        };

        var dp0 = accessory.Data.GetDefaultDrawProperties();
        dp0.Name = "百雷缭乱Base";
        dp0.Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        dp0.Position = basePos;
        dp0.Scale = new Vector2(5f);
        dp0.DestoryAt = 4700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);

        DebugMsg($"{@event.EffectPosition()}", accessory);

        if (basePos == HundredfoldHavoclistNSEW[0])
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "百雷缭乱Base";
            dp1.Color = new Vector4(1.0f, 1f, 0.0f, 1.0f);
            dp1.Position = HundredfoldHavoclistNSEW[4];
            dp1.Scale = new Vector2(5f);
            dp1.InnerScale = new Vector2(4.95f);
            dp1.Delay = 4700;
            dp1.DestoryAt = 1000;
            dp1.Radian = 2 * float.Pi;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp1);
        }
        if (basePos == HundredfoldHavoclistNSEW[1])
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "百雷缭乱Base";
            dp1.Color = new Vector4(1.0f, 1f, 0.0f, 1.0f);
            dp1.Position = HundredfoldHavoclistNSEW[5];
            dp1.Scale = new Vector2(5f);
            dp1.InnerScale = new Vector2(4.95f);
            dp1.Delay = 4700;
            dp1.DestoryAt = 1000;
            dp1.Radian = 2 * float.Pi;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp1);
        }
        if (basePos == HundredfoldHavoclistNSEW[2])
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "百雷缭乱Base";
            dp1.Color = new Vector4(1.0f, 1f, 0.0f, 1.0f);
            dp1.Position = HundredfoldHavoclistNSEW[6];
            dp1.Scale = new Vector2(5f);
            dp1.InnerScale = new Vector2(4.95f);
            dp1.Delay = 4700;
            dp1.DestoryAt = 1000;
            dp1.Radian = 2 * float.Pi;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp1);
        }
        if (basePos == HundredfoldHavoclistNSEW[3])
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "百雷缭乱Base";
            dp1.Color = new Vector4(1.0f, 1f, 0.0f, 1.0f);
            dp1.Position = HundredfoldHavoclistNSEW[7];
            dp1.Scale = new Vector2(5f);
            dp1.InnerScale = new Vector2(4.95f);
            dp1.Delay = 4700;
            dp1.DestoryAt = 1000;
            dp1.Radian = 2 * float.Pi;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp1);
        }
        //------

        if (basePos == HundredfoldHavoclistCorner[0])
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "百雷缭乱";
            dp1.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            dp1.Position = HundredfoldHavoclistCorner[4];
            dp1.Scale = new Vector2(5f);
            dp1.InnerScale = new Vector2(4.95f);
            dp1.Delay = 4700;
            dp1.DestoryAt = 1000;
            dp1.Radian = 2 * float.Pi;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp1);
        }
        if (basePos == HundredfoldHavoclistCorner[1])
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "百雷缭乱";
            dp1.Color = new Vector4(1.0f, 1f, 0.0f, 1.0f);
            dp1.Position = HundredfoldHavoclistCorner[5];
            dp1.Scale = new Vector2(5f);
            dp1.InnerScale = new Vector2(4.95f);
            dp1.Delay = 4700;
            dp1.DestoryAt = 1000;
            dp1.Radian = 2 * float.Pi;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp1);
        }
        if (basePos == HundredfoldHavoclistCorner[2])
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "百雷缭乱";
            dp1.Color = new Vector4(1.0f, 1f, 0.0f, 1.0f);
            dp1.Position = HundredfoldHavoclistCorner[6];
            dp1.Scale = new Vector2(5f);
            dp1.InnerScale = new Vector2(4.95f);
            dp1.Delay = 4700;
            dp1.DestoryAt = 1000;
            dp1.Radian = 2 * float.Pi;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp1);
        }
        if (basePos == HundredfoldHavoclistCorner[3])
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "百雷缭乱";
            dp1.Color = new Vector4(1.0f, 1f, 0.0f, 1.0f);
            dp1.Position = HundredfoldHavoclistCorner[7];
            dp1.Scale = new Vector2(5f);
            dp1.InnerScale = new Vector2(4.95f);
            dp1.Delay = 4700;
            dp1.DestoryAt = 1000;
            dp1.Radian = 2 * float.Pi;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp1);
        }
    }

    [ScriptMethod(name: "炸弹低气压", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0065"])]
    public void Bombogenesis(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);

            if (index == 0 || index == 1)
            {
                if (isText) accessory.Method.TextInfo("坦克去A点引导炸弹低气压", duration: 2500, true);
                accessory.TTS("坦克去A点引导炸弹低气压", isTTS, isDRTTS);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"炸弹低气压坦克指路";
                dp.Owner = accessory.Data.Me;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.TargetPosition = new Vector3(-0.72f, -0.00f, -18.46f);
                dp.Scale = new(2);
                dp.DestoryAt = 4500;
                if (isLead) accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }

            if (index == 2 || index == 3)
            {
                if (isText) accessory.Method.TextInfo("奶妈去B点引导炸弹低气压", duration: 2500, true);
                accessory.TTS("奶妈去B点引导炸弹低气压", isTTS, isDRTTS);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"炸弹低气压奶妈指路";
                dp.Owner = accessory.Data.Me;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.TargetPosition = new Vector3(16.48f, -0.00f, 10.22f);
                dp.Scale = new(2);
                dp.DestoryAt = 4500;
                if (isLead) accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }

            if (index > 3)
            {
                if (isText) accessory.Method.TextInfo("DPS去C点引导炸弹低气压", duration: 2500, true);
                accessory.TTS("DPS去C点引导炸弹低气压", isTTS, isDRTTS);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"炸弹低气压Dps指路";
                dp.Owner = accessory.Data.Me;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.TargetPosition = new Vector3(-17.41f, 0.00f, 7.78f);
                dp.Scale = new(2);
                dp.DestoryAt = 4500;
                if (isLead) accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }
    }

    [ScriptMethod(name: "炸弹低气压扩散", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2009431"])]
    public void BombogenesisExpend(Event @event, ScriptAccessory accessory)
    {
        DebugMsg($"Operate: {@event.Operate()}", accessory);
        if (@event.Operate() == "Add")
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "炸弹低气压扩散";
            dp.Color = new Vector4(1.0f, 0.0f, 1.0f, 1.0f);
            dp.Position = @event.SourcePosition();
            dp.Scale = new Vector2(12f);
            dp.DestoryAt = 20000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            DebugMsg($"{@event.SourcePosition()}", accessory);
        } else
        {
            accessory.Method.RemoveDraw("炸弹低气压扩散");
        }

    }

    #endregion
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


public static class Extensions
{
    public static void TTS(this ScriptAccessory accessory, string text, bool isTTS, bool isDRTTS)
    {
        if (isTTS)
        {
            accessory.Method.TTS(text);
        }
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
