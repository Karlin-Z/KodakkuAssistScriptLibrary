using System.Globalization;
using System.Numerics;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw.Manager;
using Newtonsoft.Json;
using System;
using System.Runtime.Intrinsics.Arm;
using Dalamud.Memory.Exceptions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Dalamud.Bindings.ImGui;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Extensions;

namespace KarlinScriptNamespace;

public partial class DragongSingDraw
{
    #region P1

    [ScriptMethod(name: "P1 BossId", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:28532"],
        userControl: false)]
    public void P1_BossId(Event @event, ScriptAccessory accessory)
    {
        if (ParseObjectId(@event["SourceId"], out var sid)) p1GrenoId = sid;
    }

    [ScriptMethod(name: "P1 阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25300"],
        userControl: false)]
    public void P1_阶段记录(Event @event, ScriptAccessory accessory)
    {
        if (parse == 0) parse = 1;
        parse = Math.Round(parse + 0.1, 1);
        if (ParseObjectId(@event["SourceId"], out var sid)) p1AdelId = sid;
    }

    [ScriptMethod(name: "P1 钢铁", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25307"])]
    public void P1_钢铁(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(6);
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P1 月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25306"])]
    public void P1_月环(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(70);
        dp.InnerScale = new Vector2(6);
        dp.Radian = float.Pi * 2;
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    [ScriptMethod(name: "P1 苍穹炽焰", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25309"])]
    public void P1_苍穹炽焰(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P1_苍穹炽焰";
        dp.Scale = new Vector2(4);
        dp.Color = accessory.Data.DefaultSafeColor;
        if (ParseObjectId(@event["TargetId"], out var tid)) dp.Owner = tid;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P1 直线多维空间斩", eventType: EventTypeEnum.TargetIcon)]
    public void P1_直线多维空间斩(Event @event, ScriptAccessory accessory)
    {
        if (parse < 1 || parse >= 2) return;
        if (ParsTargetIcon(@event["Id"]) != 0) return;

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Scale = new Vector2(8, 70);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = p1GrenoId;
        if (ParseObjectId(@event["TargetId"], out var tid)) dp.TargetObject = tid;
        dp.DestoryAt = 6000;
        dp.Name = $"P1 直线多维空间斩{tid:X}";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P1 次元裂缝危险区", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:13071"])]
    public void P1_次元裂缝危险区(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(9);
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.DestoryAt = 60000;
        dp.Name = $"P1 次元裂缝危险区{id:X}";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P1 次元裂缝危险区移除", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:13071"],
        userControl: false)]
    public void P1_次元裂缝危险区移除(Event @event, ScriptAccessory accessory)
    {
        if (ParseObjectId(@event["SourceId"], out var id)) accessory.Method.RemoveDraw($"P1 次元裂缝危险区{id:X}");
    }

    [ScriptMethod(name: "P1 光芒剑阿代尔斐尔位置(ImGui)", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True"])]
    public void P1_光芒剑阿代尔斐尔位置(Event @event, ScriptAccessory accessory)
    {
        if (parse != 1.1) return;
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        if (sid != p1AdelId) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P1_光芒剑阿代尔斐尔位置";
        dp.TargetObject = sid;
        dp.Owner = accessory.Data.Me;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.DestoryAt = 7000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
    }

    [ScriptMethod(name: "P1 光芒剑(火神冲)", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25294"])]
    public void P1_光芒剑(Event @event, ScriptAccessory accessory)
    {
        if (p1Charge) return;
        p1Charge = true;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(9);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.DestoryAt = 7000;

        if (float.TryParse(@event["SourceRotation"], out var r))
        {
            if (MathF.Abs(r + float.Pi / 4) < 0.1 || MathF.Abs(r - float.Pi * 0.75f) < 0.1)
            {
                dp.Name = "P1_光芒剑(111.00,111.00)";
                dp.Position = new Vector3(111, 0, 111);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                dp.Name = "P1_光芒剑(89.00,89.00)";
                dp.Position = new Vector3(89, 0, 89);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }

            if (MathF.Abs(r - float.Pi / 4) < 0.1 || MathF.Abs(r + float.Pi * 0.75f) < 0.1)
            {
                dp.Name = "P1_光芒剑(111.00,89.00)";
                dp.Position = new Vector3(111, 0, 89);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                dp.Name = "P1_光芒剑(89.00,111.00)";
                dp.Position = new Vector3(89, 0, 111);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }

        dp.Name = "P1_光芒剑(78.00,100.00)";
        dp.Position = new Vector3(78, 0, 100);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.Name = "P1_光芒剑(92.52,100.00)";
        dp.Position = new Vector3(92.52f, 0, 100);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.Name = "P1_光芒剑(107.48,100.00)";
        dp.Position = new Vector3(107.48f, 0, 100);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.Name = "P1_光芒剑(122.00,100.00)";
        dp.Position = new Vector3(122, 0, 100);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.Name = "P1_光芒剑(100.00,78.00)";
        dp.Position = new Vector3(100, 0, 78);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.Name = "P1_光芒剑(100,92.52.00)";
        dp.Position = new Vector3(100, 0, 92.52f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.Name = "P1_光芒剑(100.00,107.48)";
        dp.Position = new Vector3(100, 0, 107.48f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.Name = "P1_光芒剑(100.00,122.00)";
        dp.Position = new Vector3(100, 0, 122);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P1 光球爆炸范围移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25295"],
        userControl: false)]
    public void P1_光球爆炸范围移除(Event @event, ScriptAccessory accessory)
    {
        if (parse > 2) return;
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var name = $"P1_光芒剑\\({pos.X:f2},{pos.Z:f2}\\)";
        accessory.Method.RemoveDraw(name);
    }

    [ScriptMethod(name: "P1 击退预测", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25308"])]
    public void P1_击退预测(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(1.5f, 16);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(2);
        dp.Owner = accessory.Data.Me;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.TargetObject = sid;
        dp.Rotation = float.Pi;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "P1 索尼记录", eventType: EventTypeEnum.TargetIcon, userControl: false)]
    public void P1_索尼记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 1.2) return;
        var sony = ParsTargetIcon(@event["Id"]) - 47;
        if (sony < 0 || sony > 3) return;
        if (ParseObjectId(@event["TargetId"], out var id))
        {
            var index = accessory.Data.PartyList.ToList().IndexOf(id);
            p1sony[index] = sony;
        }
    }

    [ScriptMethod(name: "P1 索尼击退位置(ImGui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25308"])]
    public void P1_索尼击退位置(Event @event, ScriptAccessory accessory)
    {
        accessory.Log.Debug($"parse{parse}");
        if (parse != 1.2) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Scale = new Vector2(1);
        dp.Owner = accessory.Data.Me;
        dp.DestoryAt = 4000;
        dp.ScaleMode |= ScaleMode.YByDistance;

        var index = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);


        var cpos = new Vector3(100, 0, 100);
        var npos = new Vector3(100, 0, 96);

        //○
        if (p1sony[index] == 0)
        {
            var p1 = RotatePoint(npos, cpos, float.Pi / 2);
            var p2 = RotatePoint(npos, cpos, float.Pi / -2);

            dp.Name = "P1索尼○1";
            dp.TargetPosition = p1;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            dp.Name = "P1索尼○2";
            dp.TargetPosition = p2;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        //▽
        if (p1sony[index] == 1)
        {
            if (index == 2 || index == 3)
            {
                var p = RotatePoint(npos, cpos, float.Pi / -4);
                dp.Name = "P1索尼▽奶";
                dp.TargetPosition = p;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            else
            {
                var p = RotatePoint(npos, cpos, float.Pi / 4 * 3);
                dp.Name = "P1索尼▽D";
                dp.TargetPosition = p;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }

        //□
        if (p1sony[index] == 2)
        {
            if (index == 0 || index == 1)
            {
                var p = RotatePoint(npos, cpos, float.Pi / 4);
                dp.Name = "P1索尼□T";
                dp.TargetPosition = p;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            else
            {
                var p = RotatePoint(npos, cpos, float.Pi / -4 * 3);
                dp.Name = "P1索尼□D";
                dp.TargetPosition = p;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }

        //×
        if (p1sony[index] == 3)
        {
            if (index == 0 || index == 1)
            {
                var p = npos;
                dp.Name = "P1索尼×T";
                dp.TargetPosition = p;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            else
            {
                var p = RotatePoint(npos, cpos, float.Pi);
                dp.Name = "P1索尼×D";
                dp.TargetPosition = p;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }
    }

    [ScriptMethod(name: "P1 光翼闪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25316"])]
    public void P1_光翼闪(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(18);
        dp.Radian = float.Pi / 6;
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.Delay = 10000;
        dp.DestoryAt = 20000;
        dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        dp.TargetOrderIndex = 2;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "P1 苍穹刻印玩家", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2661"])]
    public void P1_苍穹刻印玩家(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(3);
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["TargetId"], out var tid)) dp.Owner = tid;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P1 苍穹刻印落地", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25370"])]
    public void P1_苍穹刻印落地(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P1_苍穹刻印落地";
        dp.Scale = new Vector2(3);
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.DestoryAt = 2500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion
}
