using System;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using System.Reflection.Metadata;
using System.Net;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using System.Runtime.Intrinsics.Arm;
using System.Collections.Generic;
using System.Timers;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Veever.DawnTrail.Everkeep;

[ScriptType(name: "LV.99 佐拉加歼灭战", territorys: [1200], guid: "7a6d317c-b176-4e94-9fbc-3bc833be1338",
    version: "0.0.1.0", author: "Veever", note: noteStr)]

public class Everkeep
{
    const string noteStr =
    """
    v0.0.1.0:
    1. 现在支持文字横幅/TTS开关
    鸭门。
    2. 现已支持所有利刃冲情况，如果依然遇到画错/漏画的情况，请dc带回放私信我（十分感谢）
    """;

    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = false;

    [UserSetting("Debug开关")]
    public bool isDebug { get; set; } = false;

    public int DoubleEdgedSwordsCount;
    public int BitterReapingCount;
    public int Run0178TTS;
    public int ForgedTrackCount;

    private readonly object BitterReapingLock = new object();
    private readonly object Run0178Lock = new object();
    private readonly object ForgedTrackLock = new object();

    private bool isDRTTS = false;


    public Vector3 midpoint = new Vector3(100.0f, 0.0f, 100.0f);
    public int midpointSum = 100 + 0 + 100;
    private static readonly float platformOffset = 30 / MathF.Sqrt(2);



    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        DoubleEdgedSwordsCount = 0;
        BitterReapingCount = 0;
        Run0178TTS = 0;
        ForgedTrackCount = 0;
    }

    [ScriptMethod(name: "灵魂超载", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3770[78])$"])]
    public void TerrorUnleashed(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4700, true);
        accessory.TTS("AOE", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "双锋合刃", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37714"])]
    public void DoubleEdgedSwords(Event @event, ScriptAccessory accessory)
    {
        if (DoubleEdgedSwordsCount == 0)
        {
            if (isDebug) accessory.Method.SendChat($"/e SourceRotation: {@event.SourceRotation()}");
            if (isText) accessory.Method.TextInfo($"去Boss{((@event.SourceRotation() == 3.14f) ? "后" : "前")}面，然后准备对穿", duration: 4500, true);
            accessory.TTS($"去Boss{((@event.SourceRotation() == 3.14f) ? "后" : "前")}面，然后准备对穿", isTTS, isDRTTS);
        }

        DoubleEdgedSwordsCount++;
        if (DoubleEdgedSwordsCount == 2)
        {
            DoubleEdgedSwordsCount = 0;
        }

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "DoubleEdged Swords";
        dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 0 / 255.0f, 0.5f);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(30);
        dp.Radian = float.Pi / 180 * 180;
        dp.DestoryAt = 4700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
     
    [ScriptMethod(name: "弑父之愤火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37715"])]
    public void TankBuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4700, true);
        accessory.TTS("死刑准备", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "民众的幻影（爆炸）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37709"])]
    public void shadowExplosion1(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "shadowExplosion1";
        dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 0.5f);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(8);
        dp.DestoryAt = 7700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp1 = accessory.Data.GetDefaultDrawProperties();
        dp1.Name = "shadowExplosion1描边";
        dp1.Scale = new(8f);
        dp1.InnerScale = new(7.95f);
        dp1.Radian = float.Pi * 2;
        dp1.Color = new Vector4(178 / 255.0f, 34 / 255.0f, 34 / 255.0f, 9.0f);
        dp1.Position = @event.EffectPosition();
        dp1.DestoryAt = 7700;
        dp1.Radian = 2 * float.Pi;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp1);
    }

    [ScriptMethod(name: "利刃寻迹", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37712"])]
    public void VorpalTrail(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "VorpalTrail";
        //dp.Color = accessory.Data.DefaultDangerColor;
        dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Position = @event.SourcePosition();
        dp.TargetPosition = @event.EffectPosition();
        dp.Scale = new Vector2(4);
        dp.DestoryAt = 2300;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "回旋惩击（钢铁）/（月环）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3773[45])$"])]
    public void SmitingCircuit(Event @event, ScriptAccessory accessory)
    {
        if (@event.ActionId() == 37735)
        {
            if (isText) accessory.Method.TextInfo("钢铁, 远离Boss", duration: 4700, true);
            accessory.TTS("钢铁, 远离Boss", isTTS, isDRTTS);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "SmitingCircuit（钢铁）";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = @event.SourceId();
            dp.Scale = new Vector2(10);
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        } else if (@event.ActionId() == 37734)
        {
            if (isText) accessory.Method.TextInfo("月环, 去Boss脚下", duration: 4700, true);
            accessory.TTS("月环, 去Boss脚下", isTTS, isDRTTS);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "SmitingCircuit（月环）";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = @event.SourceId();
            dp.Scale = new Vector2(10);
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "新曦世纪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3771[68])$"])]
    public void DawnofanAge(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("转场AOE", duration: 6700, true);
        accessory.TTS("转场AOE", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "无敌裂斩", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37720"])]
    public void ChasmofVollok(Event @event, ScriptAccessory accessory)
    {
        bool inArena = true;
        Vector3 spos = @event.SourcePosition();
        var posSum = spos.X + spos.Y + spos.Z;

        // SE
        if (spos.X >= 107.05f && spos.Z >= 107.05f)
        {
            inArena = false;
        }

        // NE
        if (spos.X >= 107.05f && spos.X <= 135.3f && spos.Z <= 92.93f && spos.Z >= 64.77f)
        {
            inArena = false;
        }

        // NW
        if (spos.X >= 64.66f && spos.X <= 92.83 && spos.Z <= 92.8f && spos.Z >= 64.84f)
        {
            inArena = false;
        }

        // WS
        if (spos.X >= 64.66f && spos.X <= 92.89 && spos.Z <= 135.3f && spos.Z >= 107.1f)
        {
            inArena = false;
        }
        if (isDebug) accessory.Method.SendChat($"/e {inArena}");
        if (inArena)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "无敌裂斩";
            dp.Color = accessory.Data.DefaultDangerColor;
            //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 0.5f);
            dp.Owner = @event.SourceId();
            dp.Scale = new Vector2(5, 5);
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        } else
        {
            if (isDebug) accessory.Method.SendChat($"/e In 偏移");
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "无敌裂斩偏移";
            //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 0.5f);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Scale = new Vector2(5, 5);
            dp.DestoryAt = 7000;
            var offset = new Vector3(
            @event.SourcePosition().X > midpoint.X ? -platformOffset : +platformOffset,
            0,
            @event.SourcePosition().Z > midpoint.Z ? -platformOffset : +platformOffset
             );
            dp.Position = @event.SourcePosition() + offset;
            dp.TargetPosition = @event.SourcePosition();
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        }
    }

    [ScriptMethod(name: "愤恨收割", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37754"])]
    public void BitterReaping(Event @event, ScriptAccessory accessory)
    {
        lock (BitterReapingLock)
        {
            if (isDebug) accessory.Method.SendChat($"/e BitterReapingCount: {BitterReapingCount}");
            if (BitterReapingCount == 0 || BitterReapingCount == 2 || BitterReapingCount == 4)
            {
                if (isText) accessory.Method.TextInfo("双T死刑", duration: 4700, true);
                accessory.TTS("双T死刑", isTTS, isDRTTS);
            }
            BitterReapingCount++;
        }
    }

    [ScriptMethod(name: "利刃冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37729"])]
    public void ForgedTrack(Event @event, ScriptAccessory accessory)
    {
        lock (ForgedTrackLock)
        {
            // Always XX
            List<Vector3> NEList = new List<Vector3>
            {
            new Vector3(122.98f, 0.00f, 66.41f),
            new Vector3(130.05f, 0.00f, 73.48f),
            new Vector3(133.59f, 0.00f, 77.02f),
            new Vector3(126.52f, 0.00f, 69.95f)
            };

            // Always Big XX
            List<Vector3> NWList = new List<Vector3>
            {
            new Vector3(69.95f, 0.00f, 73.48f),
            new Vector3(77.02f, 0.00f, 66.41f),
            new Vector3(73.48f, 0.00f, 69.95f),
            new Vector3(66.41f, 0.00f, 77.02f)
            };

            // Always XX
            List<Vector3> SWList = new List<Vector3>
            {
            new Vector3(66.41f, 0.00f, 122.98f),
            new Vector3(73.48f, 0.00f, 130.05f),
            new Vector3(69.95f, 0.00f, 126.52f),
            new Vector3(77.02f, 0.00f, 133.59f)
            };

            // Always Big XX
            List<Vector3> SEList = new List<Vector3>
            {
            new Vector3(133.59f, 0.00f, 122.98f),
            new Vector3(126.52f, 0.00f, 130.05f),
            new Vector3(122.98f, 0.00f, 133.59f),
            new Vector3(130.05f, 0.00f, 126.52f)
            };

            List<Vector3> vectorList = new List<Vector3>
            {
                //From NE (left to Right)  X-- Z++
                new Vector3(101.55f, 0.00f, 87.62f),    //[0]
                new Vector3(105.16f, 0.00f, 91.15f),    //[1]
                new Vector3(108.68f, 0.00f, 94.74f),    //[2]
                new Vector3(112.29f, 0.00f, 98.33f),    //[3]

                //From NW (left to Right)  X-- Z++
                new Vector3(87.60f, 0.00f, 98.29f),    //[4]
                new Vector3(91.15f, 0.00f, 94.71f),    //[5]
                new Vector3(94.78f, 0.00f, 91.14f),    //[6]
                new Vector3(98.31f, 0.00f, 87.61f),    //[7]
            };

            var spos = @event.SourcePosition();

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Duty's Edge";
            dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            dp.Position = @event.SourcePosition();
            var offset = new Vector3(
            @event.SourcePosition().X > midpoint.X ? -platformOffset : +platformOffset,
            0,
            @event.SourcePosition().Z > midpoint.Z ? -platformOffset : +platformOffset
            );
            dp.TargetPosition = @event.SourcePosition() + offset;
            dp.Scale = new Vector2(5, 20);
            dp.DestoryAt = 11599;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

            // handle Method
            // NEList
            #region NEList
            if (@event.SourcePosition() == NEList[0])
            {
                if (isDebug) accessory.Method.SendChat($"/e NEList[0]");
                var dpPos = vectorList[1];
                var tPos = dpPos;
                tPos.X = tPos.X - 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == NEList[1])
            {
                if (isDebug) accessory.Method.SendChat($"/e NEList[1]");
                var dpPos = vectorList[3];
                var tPos = dpPos;
                tPos.X = tPos.X - 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == NEList[2])
            {
                if (isDebug) accessory.Method.SendChat($"/e NEList[2]");
                var dpPos = vectorList[0];
                var tPos = dpPos;
                tPos.X = tPos.X - 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == NEList[3])
            {
                if (isDebug) accessory.Method.SendChat($"/e NEList[3]");
                var dpPos = vectorList[2];
                var tPos = dpPos;
                tPos.X = tPos.X - 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            #endregion
            // NWList
            #region NWList
            if (@event.SourcePosition() == NWList[0])
            {
                if (isDebug) accessory.Method.SendChat($"/e NWList[0]");
                var dpPos = vectorList[7];
                var tPos = dpPos;
                tPos.X = tPos.X + 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == NWList[1])
            {
                if (isDebug) accessory.Method.SendChat($"/e NWList[1]");
                var dpPos = vectorList[5];
                var tPos = dpPos;
                tPos.X = tPos.X + 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == NWList[2])
            {
                if (isDebug) accessory.Method.SendChat($"/e NWList[2]");
                var dpPos = vectorList[4];
                var tPos = dpPos;
                tPos.X = tPos.X + 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == NWList[3])
            {
                if (isDebug) accessory.Method.SendChat($"/e NWList[3]");
                var dpPos = vectorList[6];
                var tPos = dpPos;
                tPos.X = tPos.X + 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            #endregion
            // SWList
            #region SWList
            if (@event.SourcePosition() == SWList[0])
            {
                if (isDebug) accessory.Method.SendChat($"/e SWList[0]");
                var dpPos = vectorList[1];
                var tPos = dpPos;
                tPos.X = tPos.X - 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == SWList[1])
            {
                if (isDebug) accessory.Method.SendChat($"/e SWList[1]");
                var dpPos = vectorList[3];
                var tPos = dpPos;
                tPos.X = tPos.X - 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == SWList[2])
            {
                if (isDebug) accessory.Method.SendChat($"/e SWList[2]");
                var dpPos = vectorList[0];
                var tPos = dpPos;
                tPos.X = tPos.X - 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == SWList[3])
            {
                if (isDebug) accessory.Method.SendChat($"/e SWList[3]");
                var dpPos = vectorList[2];
                var tPos = dpPos;
                tPos.X = tPos.X - 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            #endregion
            // SEList
            #region SEList
            if (@event.SourcePosition() == SEList[0])
            {
                if (isDebug) accessory.Method.SendChat($"/e SEList[0]");
                var dpPos = vectorList[5];
                var tPos = dpPos;
                tPos.X = tPos.X + 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == SEList[1])
            {
                if (isDebug) accessory.Method.SendChat($"/e SEList[1]");
                var dpPos = vectorList[7];
                var tPos = dpPos;
                tPos.X = tPos.X + 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == SEList[2])
            {
                if (isDebug) accessory.Method.SendChat($"/e SEList[2]");
                var dpPos = vectorList[6];
                var tPos = dpPos;
                tPos.X = tPos.X + 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            if (@event.SourcePosition() == SEList[3])
            {
                if (isDebug) accessory.Method.SendChat($"/e SEList[2]");
                var dpPos = vectorList[4];
                var tPos = dpPos;
                tPos.X = tPos.X + 4;
                tPos.Z = tPos.Z + 4;
                Draw(accessory, dpPos, tPos);
            }
            #endregion
            ForgedTrackCount++;
        }
    }

    private void Draw(ScriptAccessory accessory, Vector3 dpPos, Vector3 dpTargetpos)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "ForgedTrack";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = dpPos;
        dp.TargetPosition = dpTargetpos;
        dp.Scale = new Vector2(5, 20);
        dp.Delay = (ForgedTrackCount >= 8) ? 5500 : 0;
        dp.DestoryAt = (ForgedTrackCount >= 8) ? 9099 : 13000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        if (isDebug) accessory.Method.SendChat($"/e ForgedTrackCount: {ForgedTrackCount}");
    }

    [ScriptMethod(name: "半身残", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37738"])]
    public void HalfFull(Event @event, ScriptAccessory accessory)
    {

        if (isDebug) accessory.Method.SendChat($"/e SourceRotation: {@event.SourceRotation()}");
        if (isText) accessory.Method.TextInfo($"去Boss{((@event.SourceRotation() == 1.57f) ? "左" : "右")}侧", duration: 4500, true);
        accessory.TTS($"去Boss{((@event.SourceRotation() == 1.57f) ? "左" : "右")}侧", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "HalfFull";
        dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 0 / 255.0f, 0.5f);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(30);
        dp.Radian = float.Pi / 180 * 180;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "回旋半身残（钢铁/月环）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3774[123])$"])]
    public void HalfCircuitFull(Event @event, ScriptAccessory accessory)
    {

        if (isDebug) accessory.Method.SendChat($"/e SourceRotation: {@event.SourceRotation()}");
        //if (isText) accessory.Method.TextInfo($"去Boss{((@event.SourceRotation() == 1.57f) ? "左" : "右")}侧", duration: 4500, true);
        //accessory.TTS($"去Boss{((@event.SourceRotation() == 1.57f) ? "左" : "右")}侧", isTTS, isDRTTS);

        if (@event.ActionId() == 37741)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "HalfFull with Circuit";
            dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 0 / 255.0f, 0.5f);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = @event.SourceId();
            dp.Scale = new Vector2(30);
            dp.Radian = float.Pi / 180 * 180;
            dp.DestoryAt = 7300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        if (@event.ActionId() == 37742)
        {
            if (isText) accessory.Method.TextInfo("月环, 注意躲避半场刀", duration: 4700, true);
            accessory.TTS("月环, 注意躲避半场刀", isTTS, isDRTTS);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "HalfCircuit（月环）";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = @event.SourceId();
            dp.Scale = new Vector2(10);
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        if (@event.ActionId() == 37743)
        {
            if (isText) accessory.Method.TextInfo("钢铁, 远离Boss, 注意躲避半场刀", duration: 4700, true);
            accessory.TTS("钢铁, 远离Boss, 注意躲避半场刀", isTTS, isDRTTS);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "HalfCircuit（钢铁）";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = @event.SourceId();
            dp.Scale = new Vector2(10);
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "分散", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0178"])]
    public void Run0178(Event @event, ScriptAccessory accessory)
    {
        lock (Run0178Lock)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "0178分散";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = @event.TargetId();
            dp.Scale = new Vector2(5);
            dp.DestoryAt = 5500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            if (Run0178TTS == 0)
            {
                if (isText) accessory.Method.TextInfo("全体分散", duration: 4700, true);
                accessory.TTS("全体分散", isTTS, isDRTTS);
            }
            Run0178TTS++;
        }
    }

    [ScriptMethod(name: "责任之刃", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:35567"])]
    public async void DutysEdge(Event @event, ScriptAccessory accessory)
    {
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";

        if (isText) accessory.Method.TextInfo($"与{tname}多段分摊，注意减伤", duration: 6000, true);
        accessory.TTS($"与{tname}多段分摊，注意减伤", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Duty's Edge";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.SourceId();
        dp.TargetObject = @event.TargetId();
        dp.Scale = new Vector2(6, 20);
        dp.DestoryAt = 8000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
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

    public static uint ActionId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
    }

    public static uint SourceId(this Event @event)
    {
        return ParseHexId(@event["SourceId"], out var id) ? id : 0;
    }


    public static string DurationMilliseconds(this Event @event)
    {
        return JsonConvert.DeserializeObject<string>(@event["DurationMilliseconds"]) ?? string.Empty;
    }

    public static float SourceRotation(this Event @event)
    {
        //return ParseHexId(@event["SourceRotation"], out var sourceRotation) ? sourceRotation : 0;
        return float.TryParse(@event["SourceRotation"], out var rot) ? rot : 0;

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
        return JsonConvert.DeserializeObject<string>(@event["SourceName"]) ?? string.Empty;
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
}


public static class Extensions
{
    public static void TTS(this ScriptAccessory accessory, string text, bool isTTS, bool isDRTTS)
    {
        accessory.Method.TTS(text);
    }
}