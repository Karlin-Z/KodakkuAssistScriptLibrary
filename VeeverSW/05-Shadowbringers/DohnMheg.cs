using System;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using System.Reflection.Metadata;

namespace Veever.Shadowbringers.DohnMheg;

[ScriptType(name: "LV.73 水妖幻园多恩美格禁园", territorys: [821], guid: "d8fbc4be-b2c3-43a5-93f7-2901b40d0921",
    version: "0.0.0.8", author: "Veever", note: noteStr)]

public class DohnMheg
{
    const string noteStr =
    """
    v0.0.0.8:
    1. 现在支持文字横幅/TTS开关
    2. 以前的这几个脚本的底层扩展目前懒得重构（就能加啥随便加了）
    鸭门。
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
    [ScriptMethod(name: "浇水", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:15786"])]
    public void WateringWheel(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("打断禁园水妖", duration: 5000, true);
        if (isTTS) accessory.Method.TTS("打断禁园水妖");
    }


    [ScriptMethod(name: "未终针", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:15794"])]
    public void UnfinalSting(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "未终针";
        dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(3,8);
        dp.DestoryAt = 2500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "碎骨拳", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:15787"])]
    public void StraightPunch(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("小死刑", duration: 2700, true);
        if (isTTS) accessory.Method.TTS("小死刑");
    }

    #endregion


    #region Boss1

    [ScriptMethod(name: "Boss1死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:8857"])]
    public void Boss1Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4000, true);
        if (isTTS) accessory.Method.TTS("死刑准备");
    }

    [ScriptMethod(name: "Boss1AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:15813"])]
    public void Boss1AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4000, true);
        if (isTTS) accessory.Method.TTS("AOE");
    }



    [ScriptMethod(name: "Boss1水脉乱打(太难摆烂)", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:8800"])]
    public void Boss1Landsblood(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "水脉乱打";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = @event.SourcePosition();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 1000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }


    [ScriptMethod(name: "Boss1分摊", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:003E"])]
    public void Boss1Stack(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Boss1分摊";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 5500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (isText) accessory.Method.TextInfo($"与{tname}分摊", duration: 4000, true);
        if (isTTS) accessory.Method.TTS($"与{tname}分摊");
    }
    #endregion

    #region Boss2
    [ScriptMethod(name: "Boss2AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:8915"])]
    public void Boss2AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4000, true);
        if (isTTS) accessory.Method.TTS("AOE");
    }

    [ScriptMethod(name: "Boss2召唤养分", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:8897"])]
    public void Boss2Fodder(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("接一根连在Boss上的线", duration: 8000, true);
        if (isTTS) accessory.Method.TTS("接一根连在Boss上的线");
    }

    #endregion

    #region Boss3
    [ScriptMethod(name: "Boss3死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:13732"])]
    public void Boss3Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4000, true);
        if (isTTS) accessory.Method.TTS("死刑准备");
    }

    [ScriptMethod(name: "Boss3AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:13708"])]
    public void Boss3AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("AOE");
    }

    [ScriptMethod(name: "Boss3河童歌唱队", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:13552"])]
    public void Boss3ImpChoir(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("背对Boss", duration: 3700, true);
        if (isTTS) accessory.Method.TTS("背对Boss");
    }

    [ScriptMethod(name: "Boss3青蛙歌唱队", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:13551"])]
    public void Boss3ToadChoir(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("去Boss身后", duration: 3700, true);
        if (isTTS) accessory.Method.TTS("去Boss身后");

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Boss3青蛙歌唱队";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(21);
        dp.Radian = float.Pi / 180 * 150;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Boss3终章", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:15723"])]
    public void Boss3Finale(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("过完独木桥后，进入圈内攻击梦幻的弦乐器", duration: 15000, true);
        if (isTTS) accessory.Method.TTS("过完独木桥后，进入圈内攻击乐器");
    }

    [ScriptMethod(name: "Boss3腐蚀咬", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:13547"])]
    public void Boss3CorrosiveBile(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离Boss正面", duration: 3700, true);
        if (isTTS) accessory.Method.TTS("远离Boss正面");

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Boss3腐蚀咬";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(20);
        dp.Radian = float.Pi / 180 * 90;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Boss3触手轰击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:13952"])]
    public void Boss3FlailingTentacles(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("远离Boss四角", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("远离Boss四角");

        float iAng = 45;
        float aIncrement = 90;

        for (var i = 0; i < 4; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Boss3腐蚀咬:{i}";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = @event.TargetId();
            dp.Scale = new Vector2(20);
            dp.Radian = float.Pi / 180 * 50;
            dp.Rotation = float.Pi / 180 * (iAng + i * aIncrement);
            dp.DestoryAt = 4700;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
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

    public static uint ActionId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
    }

    public static uint SourceId(this Event @event)
    {
        return ParseHexId(@event["SourceId"], out var id) ? id : 0;
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


