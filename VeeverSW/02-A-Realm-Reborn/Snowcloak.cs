using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Common.Lua;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.GameEvent.Types;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Script;
using Lumina.Data;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Xml.Linq;
using KodaMarkType = KodakkuAssist.Module.GameOperate.MarkType;

namespace Veever.A_Realm_Reborn.Snowcloak;

[ScriptType(name: Name, territorys: [1062], guid: "e64621a3-5ff4-40e2-9070-88e69191a1c0",
    version: Version, author: "Veever", note: NoteStr, updateInfo: UpdateStr)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$
// ^\[\w+\|[^|]+\|E\]\s\w+ 

public class Snowcloak
{
    const string NoteStr =
    $"""
    v{Version}
    ----- 请在使用前阅读注意事项 以及根据情况修改用户设置 -----
    1. 如果需要某个机制的绘画或者哪里出了问题请在dc@我或者私信我
    鸭门
    ----------------------------------
    ----- Please read the notes before use and adjust user settings as needed. -----
    1. If you need a draw or notice any issues, @ me on DC or DM me.
    Duckmen.
    """;

    const string UpdateStr =
    $"""
    v{Version}
    修复了TTS
    鸭门
    ----------------------------------
    Fixed TTS
    Duckmen.
    """;

    private const string Name = "LV.50 凛冽洞天披雪大冰壁 [Snowcloak]";
    private const string Version = "0.0.0.3";
    private const string DebugVersion = "a";

    private const bool Debugging = false;


    [UserSetting("播报语言(language)")]
    public Language language { get; set; } = Language.Chinese;

    [UserSetting("绘图不透明度，数值越大越显眼(Draw opacity — higher value = more visible)")]
    public static float ColorAlpha { get; set; } = 1f;

    [UserSetting("文字横幅提示开关(Banner text toggle)")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关(TTS toggle)")]
    public bool isTTS { get; set; } = true;

    //[UserSetting("是否自动使用防击退(Auto anti-knockback)")]
    //public bool useAntiKnockBack { get; set; } = false;

    //[UserSetting("指路开关(Guide arrow toggle)")]
    //public bool isLead { get; set; } = true;

    //[UserSetting("目标标记开关(Target Marker toggle)")]
    //public bool isMark { get; set; } = true;

    //[UserSetting("本地目标标记开关(打开则为本地开关，关闭则为小队) - Local target marker toggle (ON = local only, OFF = party shared)")]
    //public bool LocalMark { get; set; } = true;

    [UserSetting("Debug开关, 非开发用请关闭 - Debug on/off (don't touch unless you know what you're doing)")]
    public bool isDebug { get; set; } = false;

    public enum Language
    {
        Chinese,
        English
    }


    private readonly object CountLock = new object();
    private bool isEdgeTTS = false;

    public void DebugMsg(string str, ScriptAccessory sa)
    {
        if (!isDebug) return;
        sa.Log.Debug($"[DEBUG] {str}");
    }

    private ScriptAccessory _sa = null;

    public void Init(ScriptAccessory sa)
    {
        sa.Log.Debug($"脚本 {Name} v{Version}{DebugVersion} 完成初始化.");
        sa.Method.RemoveDraw(".*");

        _sa = sa;

        _ = ScriptVersionChecker.CheckVersionAsync(
            sa,
            "e64621a3-5ff4-40e2-9070-88e69191a1c0",
            Version,
            showNotification: true
        );


        sa.Method.ClearFrameworkUpdateAction(this);

        RefreshParams();
    }

    private void RefreshParams()
    {

    }

    #region Mobs
    [ScriptMethod(name: "---- 小怪-Mobs ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void mobs(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "伤头&插言 打断销毁", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^75(38|51)$"], userControl: false)]
    public void destoryCancelAction(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($".*{ev.TargetId}");
    }

    [ScriptMethod(name: "崩落 - Pillar Impact", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:3160"])]
    public void PillarImpact(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(5.9f), 2700, $"Pillar Impact-{ev.SourceId}", color: sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Pillar Impact Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:3160"], userControl: false)]
    public void PillarImpactClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Pillar Impact-{ev.SourceId}");
    }

    [ScriptMethod(name: "飞散 - Shatter", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:3200"])]
    public void Shatter(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(8f), 2700, $"Shatter-{ev.SourceId}", color: sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Shatter Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:3200"], userControl: false)]
    public void ShatterClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Shatter-{ev.SourceId}");
    }

    [ScriptMethod(name: "患病喷嚏 - Sickly Sneeze", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31264"])]
    public void SicklySneeze(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(7.9f), 90, 2200, $"Sickly Sneeze-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Sickly Sneeze Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:31264"], userControl: false)]
    public void SicklySneezeClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Sickly Sneeze-{ev.SourceId}");
    }

    [ScriptMethod(name: "强击 - Heavy Attack", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:3098"])]
    public void HeavyAttack(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(9.8f), 120, 2200, $"Heavy Attack-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Heavy Attack Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:3098"], userControl: false)]
    public void HeavyAttackClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Heavy Attack-{ev.SourceId}");
    }

    [ScriptMethod(name: "双重碎击 - Double Smash", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:3259"])]
    public void DoubleSmash(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(7.6f), 120, 2200, $"Double Smash-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Double Smash Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:3259"], userControl: false)]
    public void DoubleSmashClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Double Smash-{ev.SourceId}");
    }

    [ScriptMethod(name: "飞翼斩 - Wing Cutter", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:331"])]
    public void WingCutter(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(6.9f), 60, 2200, $"Wing Cutter-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Wing Cutter Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:331"], userControl: false)]
    public void WingCutterClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Wing Cutter-{ev.SourceId}");
    }
    #endregion


    #region Boss1
    [ScriptMethod(name: "---- Boss1 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void Boss1(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "风雪 - Snow Drift", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:3080"])]
    public void Boss1SnowDrift(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "AOE" : "AOE";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if(isTTS) sa.TTS($"{msg}", isEdgeTTS);
    }


    [ScriptMethod(name: "寒潮 - Cold Wave", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:3111"])]
    public void Boss1ColdWave(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(8f), 3700, $"Cold Wave-{ev.SourceId}", color: sa.Data.DefaultDangerColor);
    }
    #endregion


    #region Boss2
    [ScriptMethod(name: "---- Boss2 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void Boss2(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "殴打 - Buffet", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29585"])]
    public void Boss2Buffet(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(12f), 120, 2700, $"Buffet-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Buffet Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:29585"], userControl: false)]
    public void Boss2BuffetClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Buffet-{ev.SourceId}");
    }

    [ScriptMethod(name: "狂风暴雪 - Northerlies", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29582"])]
    public void Boss2Northerlies(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "AOE" : "AOE";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.TTS($"{msg}", isEdgeTTS);
    }

    [ScriptMethod(name: "大雪球 - Heavy Snow", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29589"])]
    public void Boss2HeavySnow(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(15f), 6700, $"Heavy Snow-{ev.SourceId}", color: sa.Data.DefaultDangerColor, scaleByTime: false);
    }

    [ScriptMethod(name: "小雪球 - Light Snow", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29590"])]
    public void Boss2LightSnow(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(2f), 6700, $"Light Snow-{ev.SourceId}", color: sa.Data.DefaultDangerColor, scaleByTime: false);
    }

    [ScriptMethod(name: "回转 - Spin", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29586"])]
    public void Boss2Spin(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircleObject(sa, ev.SourceId, new Vector2(11f), 4700, $"Spin-{ev.SourceId}", color: new Vector4(1, 0, 0, ColorAlpha), scaleByTime: true);
    }

    [ScriptMethod(name: "冻环 - Frozen Circle", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29591"])]
    public void Boss2FrozenCircle(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircleObject(sa, ev.SourceId, new Vector2(10f), 3700, $"Frozen Circle-{ev.SourceId}", color: sa.Data.DefaultSafeColor, scaleByTime: false, delay: 1000);
    }

    [ScriptMethod(name: "冻环(分散） - Frozen Spike(Spread)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29592"])]
    public void Boss2FrozenSpike_Ch(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircleObject(sa, ev.TargetId, new Vector2(6f), 4700, $"Frozen Circle-{ev.SourceId}", color: new Vector4(0, 1, 1, ColorAlpha), scaleByTime: false);
    }
    #endregion

    #region Boss3
    [ScriptMethod(name: "---- Boss3 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void Boss3(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "月下咆哮 - Lunar Cry", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29599"])]
    public void Boss3LunarCry(Event ev, ScriptAccessory sa)
    {
        var iceList = IbcHelper.GetByDataId(sa, 14714);

        foreach(var ice in iceList)
        {
            if (ice == null || !ice.IsValid()) continue;
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"Lunar Cry-{ice.EntityId}";
            dp.Color = sa.Data.DefaultSafeColor;
            dp.Owner = ice.EntityId;
            dp.TargetObject = ev.SourceId;
            dp.Rotation = -float.Pi;
            dp.Scale = new Vector2(80);
            dp.Radian = 30 * (float.Pi / 180);
            dp.DestoryAt = 7700;
            dp.FixRotation = false;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        string msg = language == Language.Chinese ? "躲在完整的冰柱后面随后准备远离" : "Get behind a full ice pillar, then move out";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 5000, true);
        if (isTTS) sa.TTS($"{msg}", isEdgeTTS);
    }

    [ScriptMethod(name: "Pillar Remove", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29648"], userControl:false)]
    public void Boss3PillarRemove(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Lunar Cry-{ev.SourceId}");
    }

    [ScriptMethod(name: "飞散 - Pillar Shatter", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29601"])]
    public void Boss3PillarShatter(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircleObject(sa, ev.SourceId, new Vector2(8f), 1700, $"Pillar Shatter-{ev.SourceId}", color: sa.Data.DefaultDangerColor, scaleByTime: true);
    }

    [ScriptMethod(name: "死刑 - Tankbuster", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29596"])]
    public void Boss3Tankbuster(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? $"坦克死刑" : $"Tank Buster";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4300, true);
        if (isTTS) sa.TTS($"{msg}", isEdgeTTS);
    }

    [ScriptMethod(name: "苍穹咆哮 - Heavensward Roar", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29593"])]
    public void HeavenswardRoar(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(50f), 60, 4700, $"Heavensward Roar-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "千年风雪 - Thousand-year Storm", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29595"])]
    public void Boss3Thousand_yearStorm(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "AOE" : "AOE";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.TTS($"{msg}", isEdgeTTS);
    }
    #endregion
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
    /// Return: "Tank"(坦克) / "Healer"(治疗) / "Melee DPS"(近战) / "Ranged DPS"(远程) / "Unknown" / "None"
    /// </summary>
    public static string GetPlayerRole(this ScriptAccessory sa, IPlayerCharacter? playerObject)
    {
        if (playerObject == null) return "None";
        return playerObject.ClassJob.Value.Role switch
        {
            1 => "Tank",        // 坦克
            4 => "Healer",      // 治疗
            2 => "Melee DPS",   // 近战DPS
            3 => "Ranged DPS",  // 远程DPS
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
    public static void TTS(this ScriptAccessory accessory, string text, bool isEdgeTTS)
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
