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
using System.Xml.Linq;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Veever.DawnTrail.YuweyawataFieldStation;

[ScriptType(name: "LV.100 废弃据点玉韦亚瓦塔实验站", territorys: [1242], guid: "992e47a8-17d0-4379-891b-0762c0509257",
    version: "0.0.2.2", author: "Veever", note: noteStr)]

public class YuweyawataFieldStation
{
    //^(?!.*((武僧|机工士|龙骑士|学者|舞者|蝰蛇剑士|暗黑骑士|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast|Cancel|Add))).*$
    public int RawElectropeCount, TelltaleTearsTTSCount, LightningStormTTSCount, DarkTwoCount, JaggedEdgeCount, RockBlastNaviCount, RagingClawCount;
    private readonly object LeapingEarthLock = new object();
    private readonly object JaggedEdgeLock = new object();
    public int LeapingEarthResult;

    const string noteStr =
    """
    v0.0.2.2:
    1. 如果有漏画（Boss出现在南侧核爆）错画的情况，请在dc@我，并附上arr文件
    鸭门。
    """;
    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = false;

    [UserSetting("Debug开关, 非开发用请关闭")]
    public bool isDebug { get; set; } = false;

    
    public async void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        LightningStormTTSCount = 0;
        RawElectropeCount = 0;
        TelltaleTearsTTSCount = 0;
        DarkTwoCount = 0;
        JaggedEdgeCount = 0;
        LeapingEarthResult = 0;
        RockBlastNaviCount = 0;
        RagingClawCount = 0;

        await Task.Delay(50);
    }
    private bool isDRTTS = false;

    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    #region 小怪
    [ScriptMethod(name: "Sweeping Gouge", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40668"])]
    public void SweepingGouge(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = $"SweepingGouge-{@event.SourceId()}";
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(9);
        dp.Radian = float.Pi / 180 * 90;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Sweeping Gouge Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:40668"], userControl: false)]
    public void SweepingGougeClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"SweepingGouge-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Thunderball", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40666"])]
    public void Thunderball(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = $"Thunderball-{@event.SourceId()}";
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(8);
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "Thunderball Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:40666"], userControl: false)]
    public void ThunderballClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"Thunderball-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Catapult", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40672"])]
    public void Catapult(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = $"Catapult-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 3750;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "Catapult Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:40672"], userControl: false)]
    public void CatapultClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"Catapult-{@event.SourceId()}");
    }
    
    [ScriptMethod(name: "Glass Punch", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40671"])]
    public void GlassPunch(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = $"GlassPunch-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(7);
        dp.Radian = float.Pi / 180 * 120;
        dp.DestoryAt = 3750;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "Glass Punch Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:40671"], userControl: false)]
    public void GlassPunchClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"GlassPunch-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Landslip", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:41118"])]
    public void Landslip(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = $"Landslip-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(12);
        dp.Radian = float.Pi / 180 * 120;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "Landslip Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:41118"], userControl: false)]
    public void LandslipClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"Landslip-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Plummet", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40676"])]
    public void Plummet(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = $"Plummet-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(10);
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "Plummet Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:40676"], userControl: false)]
    public void PlummetClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"Plummet-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Wild Horn", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40675"])]
    public void WildHorn(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = $"WildHorn-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(15);
        dp.Radian = float.Pi / 180 * 120;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "Wild Horn Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:40675"], userControl: false)]
    public void WildHornClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"WildHorn-{@event.SourceId()}");
    }

    #endregion


    #region Boss1
    [ScriptMethod(name: "Electrical Overload", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40635"])]
    public void ElectricalOverload(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4700, true);
        accessory.TTS("AOE", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "CellShock", eventType: EventTypeEnum.EnvControl, eventCondition: ["State:regex:^(00020001|00200010)$"])]
    public void CellShock(Event @event, ScriptAccessory accessory)
    {
        var EffectPositions = new Dictionary<byte, Vector3>
        {
            { 0x0D, new Vector3(81.132f, -0.75f, 268.868f) },
            { 0x0E, new Vector3(81.132f, -0.75f, 285.132f) },
            { 0x0F, new Vector3(64.868f, -0.75f, 268.868f) },
            { 0x10, new Vector3(64.868f, -0.75f, 285.132f) }
        };
        var EffectPositionsPairs = new Dictionary<byte, byte>
        {
            { 0x0D, 0x10 },
            { 0x0E, 0x0F },
            { 0x0F, 0x0E },
            { 0x10, 0x0D }
        };

        var index = @event.Index();
        var state = @event.State();

        if (state == 0x00200010 && EffectPositionsPairs.TryGetValue(index, out var remappedIndex))
            index = remappedIndex;

        if (EffectPositions.TryGetValue(index, out var position))
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "CellShock";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Position = position;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Scale = new Vector2(26);
            dp.DestoryAt = 8000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "Lightning Storm", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40637"])]
    public async void LightningStorm(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(50);
        if (LightningStormTTSCount == 0 || LightningStormTTSCount == 4 || LightningStormTTSCount == 7 || LightningStormTTSCount == 12)
        {
            if (isText) accessory.Method.TextInfo("全体分散", duration: 4700, true);
            accessory.TTS("全体分散", isTTS, isDRTTS);
        }
        LightningStormTTSCount++;
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Lightning Storm";
        dp.Color = new Vector4(138 / 255.0f, 43 / 255.0f, 251 / 226.0f, 1.0f);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 4800;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Raw Electrope", eventType: EventTypeEnum.Targetable, eventCondition: ["SourceId:40002641"])]
    public void RawElectrope(Event @event, ScriptAccessory accessory)
    {
        if (RawElectropeCount == 0)
        {
            if (isText) accessory.Method.TextInfo("优先攻击小怪", duration: 4700, true);
            accessory.TTS("优先攻击小怪", isTTS, isDRTTS);
            RawElectropeCount++;
        }
    }


    [ScriptMethod(name: "Sparking Fissure", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:41258"])]
    public async void SparkingFissure(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(8700);
        if (isText) accessory.Method.TextInfo("AOE", duration: 3700, true);
        accessory.TTS("AOE", isTTS, isDRTTS);
    }


    [ScriptMethod(name: "Clear", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:17998"])]
    public void Boss1Clear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
    }
    #endregion


    #region Boss2
    [ScriptMethod(name: "Boss2死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40658"])]
    public void Boss2Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4700, true);
        accessory.TTS("死刑准备", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "Phantom Flood", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40644"])]
    public void PhantomFlood(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("去Boss脚下", duration: 4700, true);
        accessory.TTS("去Boss脚下", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Phantom Flood";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Dark II", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4065[67])$"])]
    public void DarkTwo(Event @event, ScriptAccessory accessory)
    {
        var isFirstAction = @event.ActionId() == 40656;
        var duration = @event.DurationMilliseconds();
        var delay = isFirstAction ? 0 : 4700;
        var destroyAt = isFirstAction ? 4700 : 3800;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Phantom Flood";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.TargetId();
        dp.Delay = delay;
        dp.Scale = DarkTwoCount < 12 ? new Vector2(5) : new Vector2(35);
        dp.Radian = float.Pi / 180 * 30;
        dp.Rotation = float.Pi / 180 * @event.SourceRotation() + float.Pi / 180 * 30;
        dp.DestoryAt = destroyAt;

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        DarkTwoCount++;
    }


    [ScriptMethod(name: "Telltale Tears", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40649"])]
    public void TelltaleTears(Event @event, ScriptAccessory accessory)
    {
        if (TelltaleTearsTTSCount == 0)
        {
            if (isText) accessory.Method.TextInfo("分散, 不要重叠", duration: 4700, true);
            accessory.TTS("分散, 不要重叠", isTTS, isDRTTS);

            TelltaleTearsTTSCount++;
        }
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Telltale Tears";
        dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 251 / 255.0f, 1.0f);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 4800;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Necrohazard", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40646"])]
    public void Necrohazard(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离场中, 注意目压", duration: 4700, true);
        accessory.TTS("远离场中, 注意目压", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Telltale Tears";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Position = new Vector3(115.94f, 12.50f, -66.04f);
        dp.Scale = new Vector2(18);
        dp.DestoryAt = 14700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Bloodburst", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40647"])]
    public async void Bloodburst(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4700, true);
        accessory.TTS("AOE", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "Soul Douse", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40651"])]
    public void SoulDouse(Event @event, ScriptAccessory accessory)
    {
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";
        if (isText) accessory.Method.TextInfo($"与{tname}分摊", duration: 4700, true);
        accessory.TTS($"与{tname}分摊", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Telltale Tears";
        dp.Color = new Vector4(0 / 255.0f, 255 / 255.0f, 255 / 255.0f, 1.0f);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 4700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }



    #endregion


    #region Boss3
    [ScriptMethod(name: "Raging Claw", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40613"])]
    public void RagingClaw(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("去Boss身后", duration: 4700, true);
        accessory.TTS($"去Boss身后", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Raging Claw";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(45);
        dp.Radian = float.Pi / 180 * 180;
        dp.DestoryAt = 5400;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        if (RagingClawCount != 0)
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "Raging Claw Navi";
            dp1.Owner = accessory.Data.Me;
            dp1.Color = accessory.Data.DefaultSafeColor;
            dp1.ScaleMode |= ScaleMode.YByDistance;
            dp1.TargetPosition = @event.TargetPosition();
            dp1.Scale = new Vector2(2f);
            dp1.DestoryAt = 4500;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        }
        RagingClawCount++;
    }

    [ScriptMethod(name: "Boulder Dance", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4060[78])$"])]
    public void BoulderDance(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Boulder Dance";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.ScaleMode = ScaleMode.ByTime;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(7);
        dp.DestoryAt = 7300;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }


    //[ScriptMethod(name: "LeapingEarth", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40662"])]
    //public void LeapingEarth(Event @event, ScriptAccessory accessory)
    //{
    //    List<uint> LeapingEarthCheck0 = new List<uint> { 0x400029F4, 0x400029F5, 0x400029F6, 0x400029F7 };
    //    List<uint> LeapingEarthCheck1 = new List<uint> { 0x400029F0, 0x400029F1, 0x400029F2, 0x400029F3 };
    //    lock (LeapingEarthLock)
    //    {
    //        if (LeapingEarthTargetList.Count == 4)
    //        {
    //            if (LeapingEarthTargetList.All(value => LeapingEarthCheck0.Contains(value)))
    //            {
    //                LeapingEarthResult = 0;
    //            }
    //            if (LeapingEarthTargetList.All(value => LeapingEarthCheck1.Contains(value)))
    //            {
    //                LeapingEarthResult = 1;
    //            }

    //            if (LeapingEarthResult == 0)
    //            {
    //                var dp = accessory.Data.GetDefaultDrawProperties();
    //                dp.Name = $"{LeapingEarthResult}LeapingEarth";
    //                dp.Owner = accessory.Data.Me;
    //                dp.Color = accessory.Data.DefaultSafeColor;
    //                dp.ScaleMode |= ScaleMode.YByDistance;
    //                dp.TargetPosition = new Vector3(41.77f, -87.90f, -707.98f);
    //                dp.Scale = new(2);
    //                dp.DestoryAt = 4000;
    //                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    //            }
    //            if (LeapingEarthResult == 1)
    //            {
    //                var dp = accessory.Data.GetDefaultDrawProperties();
    //                dp.Name = $"{LeapingEarthResult}LeapingEarth";
    //                dp.Owner = accessory.Data.Me;
    //                dp.Color = accessory.Data.DefaultSafeColor;
    //                dp.ScaleMode |= ScaleMode.YByDistance;
    //                dp.TargetPosition = new Vector3(34.28f, -87.90f, -716.22f);
    //                dp.Scale = new(2);
    //                dp.DestoryAt = 4000;
    //                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    //            }
    //            LeapingEarthTargetList.Clear();
    //        }

    //        LeapingEarthTargetList.Add(@event.TargetId());
    //    }
    //}
    //[ScriptMethod(name: "LeapingEarth", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40662"])]
    //public void LeapingEarth(Event @event, ScriptAccessory accessory)
    //{
    //    lock (LeapingEarthLock)
    //    {

    //        if (LeapingEarthResult == 0)
    //        {
    //            var dp = accessory.Data.GetDefaultDrawProperties();
    //            dp.Name = $"{LeapingEarthResult}LeapingEarth";
    //            dp.Owner = accessory.Data.Me;
    //            dp.Color = accessory.Data.DefaultSafeColor;
    //            dp.ScaleMode |= ScaleMode.YByDistance;
    //            dp.TargetPosition = new Vector3(41.77f, -87.90f, -707.98f);
    //            dp.Scale = new(2);
    //            dp.DestoryAt = 4000;
    //            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    //        }
    //        if (LeapingEarthResult == 4)
    //        {
    //            var dp = accessory.Data.GetDefaultDrawProperties();
    //            dp.Name = $"{LeapingEarthResult}LeapingEarth";
    //            dp.Owner = accessory.Data.Me;
    //            dp.Color = accessory.Data.DefaultSafeColor;
    //            dp.ScaleMode |= ScaleMode.YByDistance;
    //            dp.TargetPosition = new Vector3(34.28f, -87.90f, -716.22f);
    //            dp.Scale = new(2);
    //            dp.DestoryAt = 4000;
    //            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    //        }
    //        LeapingEarthResult++;
    //    }
    //}

    [ScriptMethod(name: "Jagged Edge", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40615"])]
    public void JaggedEdge(Event @event, ScriptAccessory accessory)
    {
        lock (JaggedEdgeLock)
        {
            if (@event.TargetId() == accessory.Data.Me)
            {
                if (isText) accessory.Method.TextInfo("分散, 不要重叠", duration: 4700, true);
                accessory.TTS("分散, 不要重叠", isTTS, isDRTTS);
            }
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "Jagged Edge";
            dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 251 / 255.0f, 1.0f);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = @event.TargetId();
            dp.Scale = new Vector2(6);
            dp.DestoryAt = 4700;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "Crater Carve", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40605"])]
    public void CraterCarve(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Crater Carve";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(11);
        dp.DestoryAt = 9200;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Leaping Earth", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40606"])]
    public void LeapingEarth(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Leaping Earth";
        dp.Color = new Vector4(1, 1, 0, 1);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 1200;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Beastly Roar", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40610"])]
    public void BeastlyRoar(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离Boss", duration: 7000, true);
        accessory.TTS("远离Boss", isTTS, isDRTTS);


        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Beastly Roar";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(25);
        dp.DestoryAt = 8000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Turali Stone IV", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40616"])]
    public void TuraliStoneIV(Event @event, ScriptAccessory accessory)
    {
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";
        if (isText) accessory.Method.TextInfo($"与{tname}分摊", duration: 4700, true);
        accessory.TTS($"与{tname}分摊", isTTS, isDRTTS);


        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Turali Stone IV";
        dp.Color = new Vector4(0 / 255.0f, 255 / 255.0f, 255 / 255.0f, 1.0f);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 4700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40618|40603)$"])]
    public void BOSS3AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4700, true);
        accessory.TTS($"AOE", isTTS, isDRTTS);

    }

    [ScriptMethod(name: "Boss3死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40619"])]
    public void Boss3Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4700, true);
        accessory.TTS($"死刑准备", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "岩石冲击-画图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40611"])]
    public void RockBlast(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Rock Blast";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(5f);
        dp.DestoryAt = 700;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "岩石冲击-引导", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40611"])]
    public void RockBlastNavi(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(50);
        DebugMsg($"In Navi", accessory);
        DebugMsg($"Rotation: {@event.SourceRotation()}", accessory);
        DebugMsg($"RockBlastNaviCount: {RockBlastNaviCount}", accessory);
        if (RockBlastNaviCount == 0) 
        {
            WaitToRemove(accessory);
            DebugMsg($"RockBlastNavi: IN", accessory);
            switch (@event.SourceRotation())
            {
                // North ClockWise
                case 1.57f:
                    DebugMsg($"In 1.57",accessory);
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "Rock BlastNavi rect right Half 1.57";
                    dp.Color = new Vector4(1, 1, 0, 1);
                    dp.Position = new Vector3(33.52f, -87.90f, -709.22f);
                    dp.Radian = float.Pi;
                    dp.Rotation = float.Pi / 2;
                    dp.Scale = new Vector2(40f);
                    dp.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
                    var Npos1 = new Vector3(27.94f, -87.90f, -698.07f); 
                    var NTpos1 = new Vector3(20.98f, -87.90f, -710.16f);
                    DrawDisplacement(accessory, Npos1, NTpos1, 14f, 10000, "1.57 Arrow1");

                    var Npos2 = new Vector3(20.98f, -87.90f, -710.16f);
                    var NTpos2 = new Vector3(28.00f, -87.90f, -721.33f);
                    DrawDisplacement(accessory, Npos2, NTpos2, 14f, 10000, "1.57 Arrow2");

                    var Npos3 = new Vector3(28.47f, -87.90f, -721.85f);
                    var NTpos3 = new Vector3(39.54f, -87.90f, -721.92f);
                    DrawDisplacement(accessory, Npos3, NTpos3, 11f, 10000, "1.57 Arrow3");

                    return;

                // North AntiClockWise
                case -1.57f:
                    DebugMsg($"In M-1.57", accessory);
                    DebugMsg($"Rotation: {@event.SourceRotation()}", accessory);
                    var dp1 = accessory.Data.GetDefaultDrawProperties();
                    dp1.Name = "Rock BlastNavi rect right Half -1.57";
                    dp1.Color = new Vector4(1, 1, 0, 1);
                    dp1.Position = new Vector3(33.52f, -87.90f, -709.22f);
                    dp1.Radian = float.Pi;
                    dp1.Rotation = -float.Pi / 2;
                    dp1.Scale = new Vector2(40f);
                    dp1.DestoryAt = 5000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp1);

                    var Npos1_1 = new Vector3(37.41f, -87.90f, -697.52f);
                    var NTpos1_1 = new Vector3(50.98f, -87.90f, -710.16f);
                    DrawDisplacement(accessory, Npos1_1, NTpos1_1, 14f, 10000, "-1.57 Arrow1");

                    var Npos2_2 = new Vector3(47.62f, -87.90f, -706.97f);
                    var NTpos2_2 = new Vector3(42.00f, -87.90f, -721.33f);
                    DrawDisplacement(accessory, Npos2_2, NTpos2_2, 14f, 10000, "-1.57 Arrow2");

                    var Npos3_3 = new Vector3(42.51f, -87.90f, -719.83f);
                    var NTpos3_3 = new Vector3(29.74f, -87.90f, -722.10f);
                    DrawDisplacement(accessory, Npos3_3, NTpos3_3, 11f, 10000, "-1.57 Arrow3");

                    return;

                // West and East ClockWise
                case 3.14f:
                    var Spos = @event.SourcePosition();
                    if (Spos.X >= 45 && Spos.Z <= -700 && Spos.Z >= -720)
                    {
                        var dp2 = accessory.Data.GetDefaultDrawProperties();
                        dp2.Name = "Rock BlastNavi rect right Half 3.14";
                        dp2.Color = new Vector4(1, 1, 0, 1);
                        dp2.Position = new Vector3(33.52f, -87.90f, -709.22f);
                        dp2.Radian = float.Pi;
                        dp2.Rotation = float.Pi;
                        dp2.Scale = new Vector2(40f);
                        dp2.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp2);

                        var Epos1 = new Vector3(22.58f, -87.90f, -702.92f);
                        var ETpos1 = new Vector3(33.42f, -87.90f, -696.70f);
                        DrawDisplacement(accessory, Epos1, ETpos1, 13f, 10000, "3.14E Arrow1");

                        var Epos2 = new Vector3(33.83f, -87.90f, -696.47f);
                        var ETpos2 = new Vector3(45.85f, -87.90f, -703.79f);
                        DrawDisplacement(accessory, Epos2, ETpos2, 14f, 10000, "3.14E Arrow2");

                        var Epos3 = new Vector3(45.71f, -87.90f, -703.78f);
                        var ETpos3 = new Vector3(46.45f, -87.90f, -715.07f);
                        DrawDisplacement(accessory, Epos3, ETpos3, 11f, 10000, "3.14E Arrow3");
                    } else
                    {
                        var dp2 = accessory.Data.GetDefaultDrawProperties();
                        dp2.Name = "Rock BlastNavi rect right Half 3.14";
                        dp2.Color = new Vector4(1, 1, 0, 1);
                        dp2.Position = new Vector3(33.52f, -87.90f, -709.22f);
                        dp2.Radian = float.Pi;
                        dp2.Rotation = float.Pi;
                        dp2.Scale = new Vector2(40f);
                        dp2.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp2);

                        var Wpos1 = new Vector3(45.78f, -87.90f, -704.25f);
                        var WTpos1 = new Vector3(33.73f, -87.90f, -695.72f);
                        DrawDisplacement(accessory, Wpos1, WTpos1, 14f, 10000, "3.14W Arrow1");

                        var Wpos2 = new Vector3(34.41f, -87.90f, -696.13f);
                        var WTpos2 = new Vector3(21.66f, -87.90f, -703.56f);
                        DrawDisplacement(accessory, Wpos2, WTpos2, 14f, 10000, "3.14W Arrow2");

                        var Wpos3 = new Vector3(22.33f, -87.90f, -703.18f);
                        var WTpos3 = new Vector3(21.61f, -87.90f, -714.67f);
                        DrawDisplacement(accessory, Wpos3, WTpos3, 11f, 10000, "3.14W Arrow3");
                    }

                    return;

                // West and East AntiClockWise
                case -0f:
                    var Spos0 = @event.SourcePosition();
                    if (Spos0.X >= 45 && Spos0.Z <= -700 && Spos0.Z >= -720)
                    {
                        var dp4 = accessory.Data.GetDefaultDrawProperties();
                        dp4.Name = "Rock BlastNavi rect right Half 3.14";
                        dp4.Color = new Vector4(1, 1, 0, 1);
                        dp4.Position = new Vector3(33.52f, -87.90f, -709.22f);
                        dp4.Radian = float.Pi;
                        dp4.Rotation = float.Pi / 180;
                        dp4.Scale = new Vector2(40f);
                        dp4.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp4);

                        var Epos1_1 = new Vector3(22.79f, -87.90f, -717.67f);
                        var ETpos1_1 = new Vector3(34.76f, -87.90f, -723.53f);
                        DrawDisplacement(accessory, Epos1_1, ETpos1_1, 14f, 10000, "-0E Arrow1");

                        var Epos2_2 = new Vector3(35.25f, -87.90f, -723.81f);
                        var ETpos2_2 = new Vector3(46.42f, -87.90f, -715.65f);
                        DrawDisplacement(accessory, Epos2_2, ETpos2_2, 13f, 10000, "-0E Arrow2");

                        var Epos3_3 = new Vector3(45.62f, -87.90f, -716.17f);
                        var ETpos3_3 = new Vector3(46.61f, -87.90f, -706.54f);
                        DrawDisplacement(accessory, Epos3_3, ETpos3_3, 11f, 10000, "-0E Arrow3");
                    } else
                    {
                        var dp3 = accessory.Data.GetDefaultDrawProperties();
                        dp3.Name = "Rock BlastNavi rect right Half 3.14";
                        dp3.Color = new Vector4(1, 1, 0, 1);
                        dp3.Position = new Vector3(33.52f, -87.90f, -709.22f);
                        dp3.Radian = float.Pi;
                        dp3.Rotation = float.Pi / 180;
                        dp3.Scale = new Vector2(40f);
                        dp3.DestoryAt = 5000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp3);

                        var Wpos1_1 = new Vector3(46.51f, -87.90f, -714.42f);
                        var WTpos1_1 = new Vector3(35.83f, -87.90f, -724.43f);
                        DrawDisplacement(accessory, Wpos1_1, WTpos1_1, 14f, 10000, "-0W Arrow1");

                        var Wpos2_2 = new Vector3(36.30f, -87.90f, -724.04f);
                        var WTpos2_2 = new Vector3(22.96f, -87.90f, -717.39f);
                        DrawDisplacement(accessory, Wpos2_2, WTpos2_2, 15f, 10000, "-0W Arrow2");

                        var Wpos3_3 = new Vector3(22.86f, -87.90f, -717.37f);
                        var WTpos3_3 = new Vector3(21.23f, -87.90f, -706.96f);
                        DrawDisplacement(accessory, Wpos3_3, WTpos3_3, 11f, 10000, "-0W Arrow3");
                    }
                    return;

            }
        }
        RockBlastNaviCount++;
    }


    private void DrawDisplacement(ScriptAccessory accessory, Vector3 pos, Vector3 target, float scale, int duration, string name, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        //dp.Owner = accessory.Data.Me;
        dp.Color = accessory.Data.DefaultSafeColor;
        //dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Position = pos;
        dp.TargetPosition = target;
        dp.Scale = new Vector2(2f, scale);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private async void WaitToRemove(ScriptAccessory accessory)
    {
        await Task.Delay(20000);
        RockBlastNaviCount = 0;
        DebugMsg("Successfully RockBlastNaviCount = 0", accessory);
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
    public static IBattleChara? GetById(ScriptAccessory accessory, uint id)
    {
        return (IBattleChara?)accessory.Data.Objects.SearchByEntityId(id);
    }

    public static IBattleChara? GetMe(ScriptAccessory accessory)
    {
        return accessory.Data.Objects.SearchByEntityId(accessory.Data.Me) as IBattleChara;
    }

    public static KodakkuAssist.Data.IGameObject? GetFirstByDataId(ScriptAccessory accessory, uint dataId)
    {
        return accessory.Data.Objects.Where(x => x.DataId == dataId).FirstOrDefault();
    }

    public static IEnumerable<KodakkuAssist.Data.IGameObject> GetByDataId(ScriptAccessory accessory, uint dataId)
    {
        return accessory.Data.Objects.Where(x => x.DataId == dataId);
    }
}

public static unsafe class IBattleCharaExtensions
{
    public static bool HasStatus(this IBattleChara ibc, uint statusId, float remaining = -1)
    {
        return ibc.StatusList.Any(s => s.StatusId == statusId && s.RemainingTime > remaining);
    }

    public static unsafe uint Tethering(this IBattleChara ibc, int index = 0)
    {
        return ((BattleChara*)ibc.Address)->Vfx.Tethers[index].TargetId.ObjectId;
    }

}
