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

namespace Veever.EndWalker.theMinstrelsBalladEndsingersAria;

[ScriptType(name: "LV.90 终极之战(解限版)", territorys: [998], guid: "100df6f8-d8ce-44f7-9fb0-431eca0f2825",
    version: "0.0.0.7", author: "Veever", note: noteStr)]

public class the_Minstrels_Ballad_Endsingers_Aria
{
    const string noteStr =
    """
    v0.0.0.7:
    1. 现在支持文字横幅/TTS开关
    2. 以前的这几个脚本的底层扩展目前懒得重构（就能加啥随便加了）
    鸭门。 
    """;
    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = false;

    public int connectNotify = 0;

    public void Init(ScriptAccessory accessory)
    {
       accessory.Method.RemoveDraw(".*");   
    }

    [ScriptMethod(name: "绝望的锁链", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28701"])]
    public void GripofDespair(Event @event, ScriptAccessory accessory)
    {
        List<Vector3> vectorList = new List<Vector3>
        {
            new Vector3(92.89f, 0.00f, 87.07f),
            new Vector3(92.96f, 0.00f, 95.95f),
            new Vector3(92.97f, 0.00f, 104.98f),
            new Vector3(93.06f, 0.00f, 113.93f),
            new Vector3(107.00f, 0.00f, 86.94f),
            new Vector3(107.13f, 0.00f, 96.03f),
            new Vector3(107.28f, 0.00f, 105.11f),
            new Vector3(107.03f, 0.00f, 114.14f),
        };

        var index = accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        if (index == -1)
        {
            return;
        }

        bool isLeftSide = index >= 0 && index <= 3;
        bool isRightSide = index >= 4 && index <= 7;

        if (isLeftSide || isRightSide)
        {
            string sideText = isLeftSide ? "左侧" : "右侧";
            if (isText) accessory.Method.TextInfo($"场中集合准备向{sideText}拉线", duration: 4700, true);
            if (isTTS)
            {
                accessory.Method.TTS($"场中集合准备向{sideText}拉线");
            }

            Vector3 pos = isLeftSide
                ? new Vector3(81.38f, 0.00f, 102.54f)
                : new Vector3(118.63f, 0.00f, 99.46f);

            Vector3 pos1 = vectorList[index];

            DrawDisplacement(accessory, "连线踩塔指路1", pos, 4700, 5000);
            DrawDisplacement(accessory, "连线踩塔指路2", pos1, 9700, 5000);
        }
    }

    private void DrawDisplacement(ScriptAccessory accessory, string name, Vector3 targetPosition, int delay, int destroyAt)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = accessory.Data.Me;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.TargetPosition = targetPosition;
        dp.Scale = new Vector2(2);
        dp.Delay = delay;
        dp.DestoryAt = destroyAt;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "反诘内侧危险", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28706"])]
    public void ElenchosInside(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Elenchos";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Position = new Vector3(100f, 0f, 80f);
        dp.Owner = @event.SourceId();
        dp.TargetPosition = new Vector3(100f, 0f, 100f);
        //dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(14, 40);
        dp.DestoryAt = 7700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "反诘外侧危险", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28705"])]
    public void ElenchosOutside(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Elenchos";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Position = new Vector3(100f, 0f, 80f);
        dp.TargetPosition = new Vector3(100f, 0f, 100f);
        dp.Scale = new Vector2(14, 40);
        dp.DestoryAt = 6000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "傲慢", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28717"])]
    public void Hubris(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("双T死刑", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("双T死刑");
    }

    [ScriptMethod(name: "AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(28718|28662)$"])]
    public void AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("AOE");
    }

    [ScriptMethod(name: "反讽", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28720"])]
    public void Eironeia(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo($"分组分摊", duration: 4700, true);
        if (isTTS) accessory.Method.TTS($"分组分摊");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "反讽(分摊)";
        dp.Color = new Vector4(0 / 255.0f, 255 / 255.0f, 255 / 255.0f, 1.0f);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 4700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "分离", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28668"])]
    public void Diairesis(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Sweeping Gouge";
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(20);
        dp.Radian = float.Pi / 180 * 180;
        dp.DestoryAt = 4700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "蓝色天体撞击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(286[67]7)$"])]
    public void BlueStar(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("击退到安全位置", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("击退到安全位置");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "蓝色天体撞击";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 6700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp1 = accessory.Data.GetDefaultDrawProperties();
        dp1.Name = $"蓝色天体击退";
        dp1.Scale = new(1.5f, 23);
        dp1.Color = accessory.Data.DefaultSafeColor;
        dp1.Owner = accessory.Data.Me;
        dp1.TargetPosition = @event.TargetPosition();
        dp1.Rotation = float.Pi;
        dp1.DestoryAt = 4700;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
    }

    [ScriptMethod(name: "红色天体撞击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28666"])]
    public void RedStar(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "红色天体撞击";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(30);
        dp.DestoryAt = 7000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
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

    public static uint SourceRotation(this Event @event)
    {
        return ParseHexId(@event["SourceRotation"], out var sourceRotation) ? sourceRotation : 0;
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

