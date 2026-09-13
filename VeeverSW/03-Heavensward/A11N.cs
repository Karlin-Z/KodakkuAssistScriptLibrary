using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw;
using Dalamud.Utility.Numerics;
using System.Numerics;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;

namespace Meva.Heavensward.KodakkuAssist.Alexander11;

[ScriptType(name: "LV.60 亚历山大机神城 天动之章3", territorys: [582], guid: "c9446d8e-118d-ec98-fcbc-732213b0a265", version: "0.0.0.6", author: "Meva & Veever", note:noteStr)]
public class A11N
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

    #region 小怪
    [ScriptMethod(name: "汽油弹钢铁", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:6850"])]
    public void 汽油弹钢铁(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("钢铁", duration: 2000, true);
        if (isTTS) accessory.Method.TTS("钢铁");
        
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "汽油弹钢铁区域";
        dp.Owner = @event.SourceId();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Scale = new Vector2(8.5f);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "汽油弹月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:6851"])]
    public void 汽油弹月环(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("月环", duration: 2000, true);
        if (isTTS) accessory.Method.TTS("月环");
        
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "汽油弹月环";
        dp.Owner = @event.SourceId();
        dp.Color = accessory.Data.DefaultDangerColor; 
        dp.Scale = new Vector2(12);
        dp.InnerScale = new Vector2(3);
        dp.Radian = float.Pi * 2;
        dp.DestoryAt = 5000;
        dp.Radian = 2 * float.Pi;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }
    #endregion

    #region Boss
    [ScriptMethod(name: "百式聚能炮", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:6745"])]
    public void 百式聚能炮(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("核爆点名", duration: 4000, true);
        if (isTTS) accessory.Method.TTS("核爆点名");
        
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "百式聚能炮";
        dp.Owner = @event.TargetId();
    }
    
    [ScriptMethod(name: "黑暗命运", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:6681"])]
    public void 黑暗命运(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("流血AOE", duration: 4000, true);
        if (isTTS) accessory.Method.TTS("流血AOE");
    }
    
    // 摧毁者冲击（直线AOE，Action 6751/6787）
    [ScriptMethod(name: "摧毁者冲击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(6751|6787)$"])]
    public void 摧毁者冲击(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离直线点名", duration: 2000, true);
        if (isTTS) accessory.Method.TTS("远离直线点名");
        
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "摧毁者冲击";
        dp.Scale = new(6,50);
        dp.TargetObject = @event.TargetId();
        dp.Owner = @event.SourceId();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 螺旋桨强风（需要找掩体）
    [ScriptMethod(name: "螺旋桨强风", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(6744|6773)$"])]
    public void 螺旋桨强风(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("躲在装置后", duration: 3000);
        if (isTTS) accessory.Method.TTS("躲在装置后");
    }
    
    // 等离子护盾提示（NPC 3647）
    [ScriptMethod(name: "护盾提示", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataID:6101"])]
    public void 护盾提示(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.TextInfo("从护盾正面击杀护盾", duration: 4000);
        if (isTTS) accessory.Method.TTS("从护盾正面击杀护盾");
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

