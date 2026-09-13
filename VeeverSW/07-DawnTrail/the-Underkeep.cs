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

namespace Veever.DawnTrail.the_Underkeep;

[ScriptType(name: "LV.100 王城古迹永护塔底", territorys: [1266], guid: "9b381347-ddbf-4f52-98a9-a63d6e0d69bd",
    version: "0.0.0.5", author: "Veever & Cyf5119", note: noteStr)]

public class the_Underkeep
{
    const string noteStr =
    """
    v0.0.0.5:
    1. 持续更新中
    2. Boss3的十字炸弹画了但是没画完，先不开放使用
    3. 如果需要某个机制的绘画或者哪里出了问题请在dc@我或者私信我
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

    [UserSetting("Debug开关, 非开发用请关闭")]
    public bool isDebug { get; set; } = false;

    public KodakkuAssist.Data.IGameObject? Boss { get; set; }

    public int StaticForceCount; 
    public int ConcurrentFieldCount;

    public Vector3 Boss1Center = new Vector3(-248.00f, -70.00f, 122.00f);

    private readonly object StaticForceLock = new object();
    private readonly object ConcurrentFieldLock = new object();

    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        StaticForceCount = 0;
        ConcurrentFieldCount = 0;
        补充初始化(accessory);
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

    #region 小怪
    [ScriptMethod(name: "Sandstorm", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42904"])]
    public void Sandstorm(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Sandstorm-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(15);
        dp.Radian = float.Pi / 180 * 90;
        dp.DestoryAt = 3700;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Sandstorm Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:42904"], userControl: false)]
    public void SandstormClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"Sandstorm-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Ultravibration", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42907"])]
    public void Ultravibration(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Ultravibration-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 3700;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Ultravibration Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:42907"], userControl: false)]
    public void UltravibrationClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"Ultravibration-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Sand Crusher", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42905"])]
    public void SandCrusher(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"SandCrusher-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(8);
        dp.DestoryAt = 3700;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Sand Crusher Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:42905"], userControl: false)]
    public void SandCrusherClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"SandCrusher-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Earthquake", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42911"])]
    public void Earthquake(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Earthquake-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(10);
        dp.DestoryAt = 3700;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Earthquake Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:42911"], userControl: false)]
    public void EarthquakeClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"Earthquake-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Piercing Joust", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42917"])]
    public void PiercingJoust(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"PiercingJoust-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 3700;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Piercing Joust Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:42917"], userControl: false)]
    public void PiercingJoustClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"PiercingJoust-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Blazing Torch", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42921"])]
    public void BlazingTorch(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"BlazingTorch-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(10);
        dp.Radian = float.Pi / 180 * 120;
        dp.DestoryAt = 3700;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Blazing Torch Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:42921"], userControl: false)]
    public void BlazingTorchClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"BlazingTorch-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Run Amok", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42924"])]
    public void RunAmok(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Run Amok-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(6, 18);
        dp.DestoryAt = 3700;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "Run Amok Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:42924"], userControl: false)]
    public void RunAmokClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"Run Amok-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Wheeling Shot", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42910"])]
    public void WheelingShot(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo($"去{@event.TargetName()}身后或打断", duration: 3500, true);
        if (isTTS) accessory.Method.TTS($"去{@event.TargetName()}身后或打断");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Wheeling Shot-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(40);
        dp.Radian = float.Pi;
        dp.DestoryAt = 3700;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Wheeling Shot Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:42910"], userControl: false)]
    public void WheelingShotClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"Wheeling Shot-{@event.SourceId()}");
    }

    [ScriptMethod(name: "Electrostrike", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42926"])]
    public void Electrostrike(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Electrostrike-{@event.SourceId()}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(8);
        dp.DestoryAt = 3700;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Electrostrike Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:42926"], userControl: false)]
    public void ElectrostrikeClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"Electrostrike-{@event.SourceId()}");
    }

    #endregion

    #region Boss 1
    [ScriptMethod(name: "Boss1 AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42547|42544)$"])]
    public void Boss1AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4700, true);
        if (isTTS)  accessory.Method.TTS($"AOE");
    }

    [ScriptMethod(name: "Almighty Racket", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42546"])]
    public void AlmightyRacket(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("去Boss身后", duration: 3500, true);
        if (isTTS) accessory.Method.TTS($"去Boss身后");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Almighty Racket";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(30);
        dp.Radian = float.Pi;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Aerial Ambush", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42543"])]
    public void AerialAmbush(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离直线冲击", duration: 4000, true);
        if (isTTS) accessory.Method.TTS($"远离直线冲击");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Aerial Ambush";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = @event.EffectPosition();
        dp.TargetPosition = Boss1Center;
        dp.Scale = new Vector2(15, 30);
        dp.DestoryAt = 3500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "Sedimentary Debris", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43160"])]
    public void SedimentaryDebris(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            if (isText) accessory.Method.TextInfo("分散, 不要重叠", duration: 4000, true);
            if (isTTS) accessory.Method.TTS($"分散, 不要重叠");
        }
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Sedimentary Debris";
        dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 251 / 255.0f, 1.0f);
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 4800;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Boss1死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42548"])]
    public void Boss1Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4000, true);
        if (isTTS) accessory.Method.TTS($"死刑准备");
    }

    [ScriptMethod(name: "Sphere Shatter", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(43135|42545)"])]
    public void SphereShatter(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Sphere Shatter";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 1800;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    #endregion

    #region Boss 2
    [ScriptMethod(name: "Boss2 AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42579)$"])]
    public void Boss2AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4500, true);
        if (isTTS) accessory.Method.TTS($"AOE");
    }

    [ScriptMethod(name: "Boss2死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43136"])]
    public void Boss2Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4000, true);
        if (isTTS) accessory.Method.TTS($"死刑准备");
    }

    public bool isL = true;

    [ScriptMethod(name: "Sector Bisector", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4256[23])$"])]
    public void SectorBisector(Event @event, ScriptAccessory accessory)
    {
        // 42562 left
        // 42563 right
        if (@event.ActionId() != 42562) 
        { 
            isL = false; 
        } else {
            isL = true;
        }

        if (isText) accessory.Method.TextInfo($"{(isL ? "去Boss最后一个分身的右侧" : "去Boss最后一个分身的左侧")}", duration: 3500, true);
        if (isTTS) accessory.Method.TTS($"{(isL ? "去Boss最后一个分身的右侧" : "去Boss最后一个分身的左侧")}");

        //var dp = accessory.Data.GetDefaultDrawProperties();
        //dp.Name = $"Sector Bisector";
        //dp.Color = new Vector4(0.0f, 0.749f, 1.0f, 1.0f);
        //dp.Owner = @event.TargetId();
        //dp.Scale = new Vector2(45);
        //dp.Radian = float.Pi;
        //dp.Rotation = isL ? float.Pi / 2 : -float.Pi / 2;
        //DebugMsg($"isL: {isL}", accessory);
        //DebugMsg($"Rotation: {dp.Rotation}", accessory);
        //dp.DestoryAt = 4000;
        //accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    //     [ScriptMethod(name: "Sector Bisector Shadow", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^()$"])]
    // public void SectorBisectorShadow(Event @event, ScriptAccessory accessory)
    // {
    //     // 42562 left
    //     // 42563 right
    //     if (@event.ActionId() != 42562) 
    //     { 
    //         isL = false; 
    //     } else {
    //         isL = true;
    //     }

    //     if (isText) accessory.Method.TextInfo($"{(isL ? "去Boss右侧" : "去Boss左侧")}", duration: 3500, true);
    //     if (isTTS) accessory.Method.TTS($"{(isL ? "去Boss右侧" : "去Boss左侧")}");

    //     var dp = accessory.Data.GetDefaultDrawProperties();
    //     dp.Name = $"Sector Bisector";
    //     dp.Color = accessory.Data.DefaultDangerColor;
    //     dp.Owner = @event.TargetId();
    //     dp.Scale = new Vector2(45);
    //     dp.Radian = float.Pi;
    //     dp.Rotation = isL ? float.Pi / 2 : -float.Pi / 2;
    //     DebugMsg($"isL: {isL}", accessory);
    //     DebugMsg($"Rotation: {dp.Rotation}", accessory);
    //     dp.DestoryAt = 4000;
    //     accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    // }

    [ScriptMethod(name: "Ordered Fire", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42573"])]
    public void OrderedFire(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Ordered Fire";
        dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 251 / 255.0f, 1.0f);
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(8, 55);
        if (@event.EffectPosition().Z < -200) 
        {
            dp.Rotation = 0;
        } else {
            dp.Rotation = float.Pi / 2;
        }
        dp.DestoryAt = 3500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "Static Force", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:024F"])]
    public async void StaticForce(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(1000);
        lock (StaticForceLock)
        {
            if (StaticForceCount == 0)
            {
                DebugMsg($"{StaticForceCount}", accessory);
                if (isText) accessory.Method.TextInfo("分散, 不要重叠", duration: 4000, true);
                if (isTTS) accessory.Method.TTS($"分散, 不要重叠");
                for (var i = 0; i < accessory.Data.PartyList.Count; i++)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "Static Force";
                    dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
                    dp.Owner = @event.TargetId();
                    dp.TargetObject = accessory.Data.PartyList[i];
                    dp.Scale = new Vector2(40);
                    dp.Radian = float.Pi / 180 * 30;
                    dp.DestoryAt = 4800;
                    accessory.Method.SendDraw(DrawModeEnum.Vfx, DrawTypeEnum.Fan, dp);
                }
            }
            StaticForceCount++;
            if (StaticForceCount > 3)
            {
                StaticForceCount = 0;
            }
            DebugMsg($"StaticForceCount: {StaticForceCount}", accessory);
        } 
    }

    [ScriptMethod(name: "Electric Excess", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43139"])]
    public void ElectricExcess(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            if (isText) accessory.Method.TextInfo("分散, 不要重叠", duration: 4000, true);
            if (isTTS) accessory.Method.TTS($"分散, 不要重叠");
        }
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Electric Excess";
        dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 251 / 255.0f, 1.0f);
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 4800;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    #endregion

    #region Boss 3
    [ScriptMethod(name: "Boss3 AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42525)$"])]
    public void Boss3AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4700, true);
        if (isTTS) accessory.Method.TTS($"AOE");
    }

    //[ScriptMethod(name: "Enforcement Ray", eventType: EventTypeEnum.SetObjPos, eventCondition: ["Id:0197"])]
    //public void EnforcementRay(Event @event, ScriptAccessory accessory)
    //{
    //    var dp = accessory.Data.GetDefaultDrawProperties();
    //    dp.Name = $"Enforcement Ray1-{@event.SourceId()}";
    //    dp.Color = accessory.Data.DefaultDangerColor;
    //    dp.Position = @event.SourcePosition();
    //    dp.Scale = new Vector2(60, 4.5f);
    //    dp.DestoryAt = 8000;
    //    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp);

    //    var dp1 = accessory.Data.GetDefaultDrawProperties();
    //    dp1.Name = $"Enforcement Ray2-{@event.SourceId()}";
    //    dp1.Color = accessory.Data.DefaultDangerColor;
    //    dp1.Position = @event.SourcePosition();
    //    dp1.Scale = new Vector2(60, 4.5f);
    //    dp1.Rotation = float.Pi;
    //    dp1.DestoryAt = 8000;
    //    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp1);

    //    var dp2 = accessory.Data.GetDefaultDrawProperties();
    //    dp2.Name = $"Enforcement Ray3-{@event.SourceId()}";
    //    dp2.Color = accessory.Data.DefaultDangerColor;
    //    dp2.Position = @event.SourcePosition();
    //    dp2.Scale = new Vector2(9f, 60f);
    //    //dp2.Rotation = float.Pi;
    //    dp2.DestoryAt = 8000;
    //    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp2);

    //    var dp3 = accessory.Data.GetDefaultDrawProperties();
    //    dp3.Name = $"Enforcement Ray4-{@event.SourceId()}";
    //    dp3.Color = accessory.Data.DefaultDangerColor;
    //    dp3.Position = @event.SourcePosition();
    //    dp3.Scale = new Vector2(9f, 60f);
    //    dp3.Rotation = float.Pi;
    //    dp3.DestoryAt = 8000;
    //    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp3);
    //}

    [ScriptMethod(name: "Electray", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43130"])]
    public void Electray(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"Electray";
        dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 251 / 255.0f, 1.0f);
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(9, 40);
        if (@event.EffectPosition().X > 13)
        {
            dp.Rotation = -float.Pi / 2;
        }
        else
        {
            dp.Rotation = float.Pi / 2;
        }
        dp.DestoryAt = 3500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "Hypercharged Light", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42524"])]
    public void HyperchargedLight(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            if (isText) accessory.Method.TextInfo("分散, 不要重叠", duration: 4000, true);
            if (isTTS) accessory.Method.TTS($"分散, 不要重叠");
        }
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Hypercharged Light";
        dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 251 / 255.0f, 1.0f);
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 4800;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Neutralize Front Lines", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42738"])]
    public void NeutralizeFrontLines(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Hypercharged Light";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(30);
        dp.Radian = float.Pi;
        dp.DestoryAt = 4800;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }


    [ScriptMethod(name: "Deterrent Pulse", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42540"])]
    public async void DeterrentPulse(Event @event, ScriptAccessory accessory)
    {
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";

        if (isText) accessory.Method.TextInfo($"与{tname}分摊", duration: 4700, true);
        if (isTTS) accessory.Method.TTS($"与{tname}分摊");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Deterrent Pulse";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.SourceId();
        dp.TargetObject = @event.TargetId();
        dp.Scale = new Vector2(8, 40);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "Concurrent Field", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:024A"])]
    public async void ConcurrentField(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(1000);
        lock (ConcurrentFieldLock)
        {
            if (ConcurrentFieldCount == 0)
            {
                DebugMsg($"{ConcurrentFieldCount}", accessory);
                if (isText) accessory.Method.TextInfo("分散, 不要重叠", duration: 4000, true);
                if (isTTS) accessory.Method.TTS($"分散, 不要重叠");
                for (var i = 0; i < accessory.Data.PartyList.Count; i++)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "Concurrent Field";
                    dp.Color = new Vector4(0.0f, 0.749f, 1.0f, 1.0f);
                    dp.Owner = @event.TargetId();
                    dp.TargetObject = accessory.Data.PartyList[i];
                    dp.Scale = new Vector2(40);
                    dp.Radian = float.Pi / 180 * 50;
                    dp.DestoryAt = 4800;
                    accessory.Method.SendDraw(DrawModeEnum.Vfx, DrawTypeEnum.Fan, dp);
                }
            }
            ConcurrentFieldCount++;
            if (ConcurrentFieldCount > 3) 
            {
                ConcurrentFieldCount = 0;
            }
            DebugMsg($"StaticForceCount: {ConcurrentFieldCount}", accessory);
        }
    }
    #endregion

    #region 补充

    private Dictionary<uint, uint> _boss2ClonesDict = new();

    private void 补充初始化(ScriptAccessory sa)
    {
        _boss2ClonesDict.Clear();
    }

    [ScriptMethod(name: "补充-Boss2分身半场刀连线记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0147"], userControl: false)]
    public void Boss2ClonesTetherRecord(Event evt, ScriptAccessory sa)
    {
        _boss2ClonesDict[evt.SourceId()] = evt.TargetId();
    }

    [ScriptMethod(name: "补充-Boss2分身半场刀", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4316[34])$"], suppress: 8000)]
    public void Boss2ClonesCleave(Event evt, ScriptAccessory sa)
    {
        if (!_boss2ClonesDict.ContainsKey(evt.SourceId()))
        {
            _boss2ClonesDict.Clear();
            return;
        }
        var lastClone = _boss2ClonesDict[evt.SourceId()];
        var clonesNum = _boss2ClonesDict.Count;
        _boss2ClonesDict.Clear();

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "Boss2ClonesCleave";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Scale = new Vector2(45);
        dp.Radian = float.Pi;
        dp.DestoryAt = (clonesNum - 1) * 900 + 500;
        dp.Owner = lastClone;
        dp.Rotation = evt.ActionId > 43163 ? -float.Pi / 2 : float.Pi / 2;

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
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
