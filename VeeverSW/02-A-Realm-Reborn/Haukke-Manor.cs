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
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Runtime.Intrinsics.Arm;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Veever.A_Realm_Reborn.Haukke_Manor;

[ScriptType(name: "LV.28 名门府邸静语庄园", territorys: [1040], guid: "964f1a0a-5b2a-4473-b41b-a170bc823f67",
    version: "0.0.0.5", author: "Veever", note: noteStr)]

public class Haukke_Manor
{
    const string noteStr =
    """
    v0.0.0.5:
    1. 绿色标记为需要捡的钥匙，红色代表不捡
    2. 如果需要某个机制的绘画或者哪里出了问题请在dc@我或者私信我
    3. 如果想要鲶鱼精标记请确保你打开了ACT并且安装了鲶鱼精插件
    鸭门。
    """;

    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = true;

    [UserSetting("指路开关")]
    public bool isLead { get; set; } = true;

    [UserSetting("目标标记开关")]
    public bool isMark { get; set; } = true;

    [UserSetting("本地目标标记开关(打开则为本地开关，关闭则为小队)")]
    public bool LocalMark { get; set; } = true;

    [UserSetting("是否进行场地标点引导")]
    public bool PostNamazuPrint { get; set; } = true;

    [UserSetting("鲶鱼精邮差端口设置")]
    public int PostNamazuPort { get; set; } = 2019;

    [UserSetting("场地标点是否为本地标点(如果选择非本地标点，脚本只会在非战斗状态下进行标点)")]
    public bool PostNamazuisLocal { get; set; } = true;

    [UserSetting("Debug开关, 非开发用请关闭")]
    public bool isDebug { get; set; } = false;


    //private readonly object OminousWindMarkerTTSLock = new object();
    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        if (PostNamazuPrint) PostWaymark(accessory);
    }

    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    [ScriptMethod(name: "debug", eventType: EventTypeEnum.Chat, eventCondition: ["Message:debug"])]
    public void debug(Event @event, ScriptAccessory accessory)
    {

    }

    #region Waymark
    private static readonly Vector3 posA = new Vector3(48.76f, 0.00f, 36.00f);
    private static readonly Vector3 posB = new Vector3(16.94f, 0.00f, 70.82f);
    private static readonly Vector3 posC = new Vector3(-36.62f, 0.00f, 16.15f);

    public void PostWaymark(ScriptAccessory accessory)
    {
        var waymark = new NamazuHelper.Waymark(accessory);
        waymark.AddWaymarkType("A", posA); 
        waymark.AddWaymarkType("B", posB);
        waymark.AddWaymarkType("C", posC);

        waymark.SetJsonPayload(LocalMark, PostNamazuisLocal);
        waymark.PostWaymarkCommand(PostNamazuPort);
    }

    private static readonly Vector3 newposA = new Vector3(-46.52f, -0.00f, 0.01f);
    private static readonly Vector3 newposB = new Vector3(-31.85f, -18.80f, -0.03f);
    private static readonly Vector3 targetC = new Vector3(-31.85f, -18.80f, -24f);
    private static readonly Vector3 newposC = new Vector3(-2.16f, -18.80f, 40.35f);

    private static readonly Vector3 newposD = new Vector3(-16.24f, -15.69f, 27.75f);

    public void PostWaymark1(ScriptAccessory accessory)
    {
        var waymark1 = new NamazuHelper.Waymark(accessory);
        waymark1.AddWaymarkType("A", newposA);
        waymark1.AddWaymarkType("B", newposB);
        waymark1.AddWaymarkType("C", newposC);
        waymark1.AddWaymarkType("D", newposD);

        waymark1.SetJsonPayload(LocalMark, PostNamazuisLocal);
        waymark1.PostWaymarkCommand(PostNamazuPort);
    }


    private static readonly Vector3 newposAA = new Vector3(49.11f, 8.37f, 0.00f);
    private static readonly Vector3 newposBB = new Vector3(25.25f, 17.00f, 0.02f);

    public void PostWaymark2(ScriptAccessory accessory)
    {
        var waymark1 = new NamazuHelper.Waymark(accessory);
        waymark1.AddWaymarkType("A", newposAA);
        waymark1.AddWaymarkType("B", newposBB);

        waymark1.SetJsonPayload(LocalMark, PostNamazuisLocal);
        waymark1.PostWaymarkCommand(PostNamazuPort);
    }

    [ScriptMethod(name: "钥匙mark", eventType: EventTypeEnum.ObjectChanged, eventCondition: [])]
    public async void keymark(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(1000);

        var keyId0 = 2000302;
        if (@event.DataId() == keyId0)
        {
            DebugMsg("key0", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultSafeColor, scaleByTime: false);
        }
        else if (@event.DataId() == keyId0 + 1)
        {
            DebugMsg("key1", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultSafeColor, scaleByTime: false);
        }
        else if (@event.DataId() == keyId0 + 2)
        {
            DebugMsg("key2", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultDangerColor, scaleByTime: false);
        } else if (@event.DataId() == 2000324)
        {
            DebugMsg("key2000324", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultSafeColor, scaleByTime: false);
            Vector3 pos = new Vector3(-46.49f, 0.00f, 0.05f);
            DrawHelper.DrawDisplacement(accessory, pos, new Vector2(1.5f), 999999999, $"displacement-{@event.DataId()}", accessory.Data.DefaultSafeColor);
            PostWaymark1(accessory);
            DrawHelper.DrawArrow(accessory, newposB, targetC, 1f, 10f, 999999999, "arrow", accessory.Data.DefaultSafeColor);
            
        } else if (@event.DataId() == 2000305)
        {
            DebugMsg("key2000305", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultDangerColor, scaleByTime: false);
        } else if (@event.DataId() == 2000325)
        {
            DebugMsg("key2000325", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultSafeColor, scaleByTime: false);
        } else if (@event.DataId() == 2001235)
        {
            DebugMsg("key2001235", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultSafeColor, scaleByTime: false);
        } else if (@event.DataId() == 2000327)
        {
            DebugMsg("key2000327", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultDangerColor, scaleByTime: false);

        }

        if (@event.Operate() == "Remove")
        {
            accessory.Method.RemoveDraw($"标记-{@event.DataId()}");
            if (@event.DataId() == 2000324)
            {
                await Task.Delay(5000);
                accessory.Method.RemoveDraw($"displacement-{@event.DataId()}");
            }
        }

    }

    [ScriptMethod(name: "钥匙mark初始", eventType: EventTypeEnum.ObjectChanged, eventCondition: [])]
    public async void keymarkInit(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(6000);

        var keyId0 = 2000302;
        if (@event.DataId() == keyId0)
        {
            DebugMsg("key0", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultSafeColor, scaleByTime: false);
        }
        else if (@event.DataId() == keyId0 + 1)
        {
            DebugMsg("key1", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultSafeColor, scaleByTime: false);
        }
        else if (@event.DataId() == keyId0 + 2)
        {
            DebugMsg("key2", accessory);
            DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(0.7f), 999999999, $"标记-{@event.DataId()}", accessory.Data.DefaultDangerColor, scaleByTime: false);
        } 

        if (@event.Operate() == "Remove")
        {
            accessory.Method.RemoveDraw($"标记-{@event.DataId()}");
        }

    }

    #endregion





    #region 小怪
    [ScriptMethod(name: "暗黑雾", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29776"])]
    public void DarkMist(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("注意远离或打断", duration: 3700, true);
        if (isTTS) accessory.Method.TTS("注意远离或打断");
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
        DrawHelper.DrawCircleObject(accessory, @event.SourceId(), new Vector2(8f), 3700, $"DarkMist-{@event.SourceId()}", accessory.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "DarkMist Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:29776"], userControl: false)]
    public void DarkMistClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"DarkMist-{@event.SourceId()}");
    }


    [ScriptMethod(name: "恐怖视线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:513"])]
    public void DreadGaze(Event @event, ScriptAccessory accessory)
    {
        DrawHelper.DrawFanOwner(accessory, @event.SourceId(), 0, new Vector2(7.35f), 90, 2700, $"DarkMist-{@event.SourceId()}", accessory.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "DarkMist Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:513"], userControl: false)]
    public void DreadGazeClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"DreadGaze-{@event.SourceId()}");
    }
     
    #endregion


    #region Boss1
    [ScriptMethod(name: "虚空烈炎", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:855"])]
    public void VoidFireII(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("注意远离或打断", duration: 3700, true);
        if (isTTS) accessory.Method.TTS("注意远离或打断");
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
        DrawHelper.DrawCircle(accessory, @event.EffectPosition(), new Vector2(5f), 2700, $"VoidFireII-{@event.SourceId()}", accessory.Data.DefaultDangerColor);
    }
    
    [ScriptMethod(name: "VoidFireII Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:855"], userControl: false)]
    public void VoidFireIIClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"VoidFireII-{@event.SourceId()}");
    }

    [ScriptMethod(name: "暗黑雾", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:705"])]
    public void DarkMist1(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("注意远离", duration: 3700, true);
        if (isTTS) accessory.Method.TTS("注意远离");
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
        DrawHelper.DrawCircle(accessory, @event.EffectPosition(), new Vector2(9.4f), 3700, $"DarkMist1-{@event.SourceId()}", accessory.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "DarkMist1 Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:705"], userControl: false)]
    public void DarkMist1Clear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"DarkMist1-{@event.SourceId()}");
    }




    #endregion



    #region Boss2
    [ScriptMethod(name: "冰棘屏障", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:859"])]
    public async void IceSpikes(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("打断庄园的小丑", duration: 3000, true);
        if (isTTS) accessory.Method.TTS("打断庄园的小丑");
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Bind1, LocalMark);
        await Task.Delay(4000);
        accessory.Method.MarkClear();
    }

    [ScriptMethod(name: "吸魂", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:860"])]
    public async void SoulDrain(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("眩晕庄园的总管", duration: 3700, true);
        if (isTTS) accessory.Method.TTS("眩晕庄园的总管");
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Bind1, LocalMark);
        await Task.Delay(4000);
        accessory.Method.MarkClear();
    }
    #endregion


    #region Boss3
    [ScriptMethod(name: "石化眼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28648"])]
    public void PetrifyingEye(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        if (isText) accessory.Method.TextInfo("背对眼睛", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("背对眼睛");
    }

    [ScriptMethod(name: "女仆标记", eventType: EventTypeEnum.Targetable, eventCondition: ["DataId:14506", "Targetable:True"])]
    public void TargetMark(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("优先攻击随从女仆", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("优先攻击随从女仆");
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);

    }

    [ScriptMethod(name: "Boss3暗黑雾", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28646"])]
    public void Boss3DarkMist(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("注意远离或下踢打断", duration: 3700, true);
        if (isTTS) accessory.Method.TTS("注意远离或下踢打断");
        DrawHelper.DrawCircle(accessory, @event.EffectPosition(), new Vector2(9f), 3700, $"DarkMist-{@event.SourceId()}", accessory.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Boss3DarkMist Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:28646"], userControl: false)]
    public void Boss3DarkMistClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"DarkMist-{@event.SourceId()}");
    }


    [ScriptMethod(name: "Draw Clear", eventType: EventTypeEnum.Death, eventCondition: ["TargetDataId:14504"], userControl: false)]
    public void DrawClear(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
    }

    #endregion




    #region Helpers

    public unsafe static float GetStatusRemainingTime(ScriptAccessory sa, IBattleChara? battleChara, uint statusId)
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

    public static void DrawFan(ScriptAccessory accessory, Vector3 position, float rotation, Vector2 scale, float angle, int duration, string name, Vector4? color = null, int delay = 0, bool fix = false)
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
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    public static void DrawFanOwner(ScriptAccessory accessory, ulong owner, float rotation, Vector2 scale, float angle, int duration, string name, Vector4? color = null, int delay = 0, bool scaleByTime = true, bool fix = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.Rotation = rotation;
        dp.Scale = scale;
        dp.Radian = angle* (float.Pi / 180);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.FixRotation = fix;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
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
    #endregion
}
