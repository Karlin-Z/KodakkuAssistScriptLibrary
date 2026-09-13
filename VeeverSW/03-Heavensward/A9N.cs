using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw;
using Dalamud.Utility.Numerics;
using System.Numerics;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Meva.Heavensward.KodakkuAssist.Alexander9;

[ScriptType(name: "LV.60 亚历山大机神城 天动之章1", territorys: [580], guid: "7cb80e0d-e693-6ca9-c46e-c96b0ec5d109", version: "0.0.0.6", author: "Meva & Veever", note: noteStr)]
public class A9N
{
    const string noteStr =
        """
        v0.0.0.6
        """;
    
    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;
    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = false;

    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
    }

    // 废料爆发提示（躲在石头后）
    [ScriptMethod(name: "废料爆发", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:6926"])]
    public void 废料爆发(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("躲在石头后！", duration: 2000);
        if (isTTS) accessory.Method.TTS("躲在石头后");
    }
    

    // 击杀小怪提示（拉进发光地板）
    [ScriptMethod(name: "击杀小怪提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["DataId:6922"])]
    public void 击杀小怪提示(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("拉进发光地板击杀", duration: 2000);
    }

    // 击杀大怪提醒（对角击杀）
    [ScriptMethod(name: "大怪击杀提示", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:6354"])]
    public void 大怪击杀提示(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("在发光地板对角击杀", duration: 4000);
        if (isTTS) accessory.Method.TTS("在发光地板对角击杀");
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

