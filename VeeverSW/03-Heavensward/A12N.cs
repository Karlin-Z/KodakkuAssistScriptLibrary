using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw;
using Dalamud.Utility.Numerics;
using System.Numerics;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Meva.Heavensward.KodakkuAssist.Alexander12;

[ScriptType(name: "LV.60 亚历山大机神城 天动之章4", territorys: [583], guid: "c77e794f-f3bc-37fc-810b-bb153ba399c1", version: "0.0.0.6", author: "Meva & Veever", note:noteStr)]
public class A12N
{
    const string noteStr =
        """
        v0.0.0.6:
        """;
    
    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;
    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = false;

    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "百万神圣", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:6887"])]
    public void 百万神圣(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 2000, true);
        if (isTTS) accessory.Method.TTS("AOE");
    }
    
    // 十字圣礼1（十字激光1）
    [ScriptMethod(name: "十字圣礼1", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:6885"])]
    public void 十字圣礼1(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("躲避十字激光", duration: 2000, true);
        if (isTTS) accessory.Method.TTS("躲避十字激光");
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "十字圣礼1";
        dp.Owner = @event.SourceId();
        dp.Color = accessory.Data.DefaultDangerColor; 
        dp.Scale = new(16.0f, 60.0f); // X=半径16，Y=长度60
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
    }

    // 十字圣礼2（十字激光2，旋转90度）
    [ScriptMethod(name: "十字圣礼2", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:6885"])]
    public void 十字圣礼2(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "十字圣礼2";
        dp.Owner = @event.SourceId();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Scale = new(16.0f, 60.0f);
        dp.Rotation = float.Pi / 2;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
    }

    // 重力异常（扩大黑圈）
    [ScriptMethod(name: "重力异常", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:6891"])]
    public void 重力异常(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离扩大黑圈", duration: 2000, true);
        if (isTTS) accessory.Method.TTS("远离扩大黑圈");
    }

    // 白光之鞭（点名AOE，Buff 562触发）
    [ScriptMethod(name: "白光之鞭", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:562"])]
    public void 白光之鞭(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("分散", duration: 2000, true);
        if (isTTS) accessory.Method.TTS("分散");
        
        var circleDp = accessory.Data.GetDefaultDrawProperties();
        circleDp.Name = "白光之鞭区域";
        circleDp.Owner = @event.TargetId();
        circleDp.Color = accessory.Data.DefaultDangerColor;
        circleDp.Scale = new(4);
        circleDp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, circleDp);
    }

    // 集团罪（分摊，Buff 1122触发）
    [ScriptMethod(name: "集团罪", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:1122"])]
    public void 集团罪(Event @event, ScriptAccessory accessory)
    {
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";
        
        if (isText) accessory.Method.TextInfo($"靠近{tname}分摊", 2000);
        
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "分摊区域";
        dp.Owner = @event.TargetId();
        dp.Color = accessory.Data.DefaultSafeColor; 
        dp.Scale = new(4);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 加重罪（大圈，Buff 1121触发）
    [ScriptMethod(name: "加重罪", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:1121"])]
    public void 加重罪(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("分散", 2000);
        
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "加重罪区域";
        dp.Owner = @event.TargetId();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Scale = new(2.5f);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "圣餐", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:6908"])]
    public void 圣餐(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("放圈连线点名", duration: 4000, true);
        if (isTTS) accessory.Method.TTS("放圈连线点名");
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


