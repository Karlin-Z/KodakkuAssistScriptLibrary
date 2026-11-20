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
    #region P5

    #region 一运

    [ScriptMethod(name: "P5 一运记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27529"],
        userControl: false)]
    public void P5_一运记录(Event @event, ScriptAccessory accessory)
    {
        parse = 5.1;
    }

    [ScriptMethod(name: "P5 一运旋风冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27531"])]
    public void P5_一运旋风冲(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;

        dp.Scale = new Vector2(10, 60);
        dp.DestoryAt = 6000;
        dp.Name = "P5一运旋风冲";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P5 一运白龙位置连线(ImGui)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:27531"])]
    public void P5_一运白龙位置连线(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.TargetObject = id;
        dp.Owner = accessory.Data.Me;
        dp.Scale = new Vector2(5);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.DestoryAt = 6000;
        dp.Name = "P5一运白龙位置连线";

        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
    }

    [ScriptMethod(name: "P5 一运双骑士螺旋枪", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0005"])]
    public void P5_一运双骑士连线(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        if (ParseObjectId(@event["TargetId"], out var tid)) dp.TargetObject = tid;

        dp.Scale = new Vector2(16, 60);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Delay = p5TetherCrashDelay;
        dp.DestoryAt = 6000 - p5TetherCrashDelay;
        dp.Name = "P5一运双骑士连线冲锋";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P5 一运雷翼", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2833"])]
    public void P5_一运雷翼(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["TargetId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 5000;
        dp.Delay = int.TryParse(@event["DurationMilliseconds"], out var d) ? d - dp.DestoryAt : 8000;
        dp.Name = $"P5一运雷翼{id:X8}";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P5 一运穿天", eventType: EventTypeEnum.TargetIcon)]
    public void P5_一运穿天(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1 || ParsTargetIcon(@event["Id"]) != -316) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor.WithW(0.5f);
        if (ParseObjectId(@event["TargetId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(24);
        dp.Delay = 2000;
        dp.DestoryAt = 4000;
        dp.Name = $"P5一运穿天{id:X8}";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P5 阿斯卡隆之仁・揭示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25546"])]
    public void P5_阿斯卡隆之仁揭示(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;

        dp.Scale = new Vector2(50);
        dp.Radian = float.Pi / 180 * 30;
        dp.DestoryAt = 4000;
        foreach (var tid in accessory.Data.PartyList)
        {
            dp.Name = $"P5 阿斯卡隆之仁・揭示 {tid:X8}";
            dp.TargetObject = tid;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }

    [ScriptMethod(name: "P5 一运双龙俯冲处理位置记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:12603"],
        userControl: false)]
    public void P5_一运双龙俯冲处理位置记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1) return;
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        p5DivePos = new Vector3((pos.X - 100) / 9 * 19 + 100, pos.Y, (pos.Z - 100) / 9 * 19 + 100);
    }

    [ScriptMethod(name: "P5 一运双龙俯冲处理位置", eventType: EventTypeEnum.TargetIcon)]
    public void P5_一运双龙俯冲处理位置(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1 || ParsTargetIcon(@event["Id"]) != -310) return;
        if (!ParseObjectId(@event["TargetId"], out var id) || id != accessory.Data.Me) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultSafeColor.WithW(2f);
        dp.Owner = id;
        dp.TargetPosition = p5DivePos;
        dp.Scale = new Vector2(1, 60);
        dp.DestoryAt = 5000;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Name = "P5一运双龙俯冲处理位置";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "P5 一运白龙俯冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27534"])]
    public void P5_一运白龙俯冲(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(20, 48);
        dp.DestoryAt = 6000;
        dp.Name = "P5一运白龙俯冲";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P5 一运黑龙俯冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27533"])]
    public void P5_一运黑龙俯冲(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(20, 48);
        dp.DestoryAt = 6000;
        dp.Name = "P5一运黑龙俯冲";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P5 一运格里诺位置记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:12602"],
        userControl: false)]
    public void P5_一运格里诺位置记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1) return;
        p5GrenoPos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    [ScriptMethod(name: "P5 一运连线格里诺(ImGui)", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25546"])]
    public void P5_一运连线格里诺(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P5 一运连线格里诺";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = p5GrenoPos;
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 8000;
        dp.ScaleMode |= ScaleMode.YByDistance;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
    }

    #endregion

    #region 二运

    [ScriptMethod(name: "P5 二运记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27538"],
        userControl: false)]
    public void P5_二运记录(Event @event, ScriptAccessory accessory)
    {
        parse = 5.2;
        p5sony = [0, 0, 0, 0, 0, 0, 0, 0];
        if (ParseObjectId(@event["SourceId"], out var id)) tordanId = id;
    }

    [ScriptMethod(name: "P5 二运黑龙俯冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27533"])]
    public void P5_二运黑龙俯冲(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(20, 48);
        dp.DestoryAt = 6000;
        dp.Name = "P5一运黑龙俯冲";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P5 二运旋风冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27531"])]
    public void P5_二运旋风冲(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;

        dp.Scale = new Vector2(10, 60);
        dp.DestoryAt = 6000;
        dp.Name = "P5二运旋风冲";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P5 二运战女神之枪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27539"])]
    public void P5_二运战女神之枪(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(10, 50);
        dp.DestoryAt = 6000;
        dp.Name = "P5二运战女神之枪";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P5 二运地震", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25558"])]
    public void P5_二运地震(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.Name = "P5_二运地震";

        dp.Scale = new Vector2(6);
        dp.DestoryAt = 6000;
        dp.Radian = float.Pi * 2;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        dp.Scale = new Vector2(12);
        dp.InnerScale = new Vector2(6);
        dp.Delay = 4000;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

        dp.Scale = new Vector2(18);
        dp.InnerScale = new Vector2(12);
        dp.Delay = 6000;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

        dp.Scale = new Vector2(24);
        dp.InnerScale = new Vector2(18);
        dp.Delay = 8000;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

        dp.Scale = new Vector2(30);
        dp.InnerScale = new Vector2(24);
        dp.Delay = 10000;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    [ScriptMethod(name: "P5 二运龙眼背对", eventType: EventTypeEnum.EnvControl,
        eventCondition: ["DirectorId:8003759A", "Id:00020001"])]
    public void P5_二运龙眼背对(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;
        var index = int.Parse(@event["Index"], NumberStyles.HexNumber);
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = accessory.Data.Me;
        dp.Delay = 16000;
        dp.DestoryAt = 7000;
        if (index == 0) dp.TargetPosition = new Vector3(100, 0, 65);
        if (index == 1) dp.TargetPosition = new Vector3(124.75f, 0, 75.25f);
        if (index == 2) dp.TargetPosition = new Vector3(135, 0, 100);
        if (index == 3) dp.TargetPosition = new Vector3(124.75f, 0, 124.75f);
        if (index == 4) dp.TargetPosition = new Vector3(100, 0, 135);
        if (index == 5) dp.TargetPosition = new Vector3(75.25f, 0, 124.75f);
        if (index == 6) dp.TargetPosition = new Vector3(65, 0, 100);
        if (index == 7) dp.TargetPosition = new Vector3(75.25f, 0, 75.25f);
        dp.Name = "P5 二运龙眼背对";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);
    }

    [ScriptMethod(name: "P5 二运骑神背对", eventType: EventTypeEnum.EnvControl,
        eventCondition: ["DirectorId:8003759A", "Id:00020001"])]
    public void P5_二运骑神背对(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = accessory.Data.Me;
        dp.TargetObject = tordanId;
        dp.Delay = 16000;
        dp.DestoryAt = 7000;

        dp.Name = "P5 二运骑神背对";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);
    }

    [ScriptMethod(name: "P5 二运索尼记录", eventType: EventTypeEnum.TargetIcon, userControl: false)]
    public void P5_二运索尼记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;
        var sony = ParsTargetIcon(@event["Id"]) + 49;
        if (sony < 0 || sony > 3) return;
        if (ParseObjectId(@event["TargetId"], out var id))
        {
            var index = accessory.Data.PartyList.ToList().IndexOf(id);
            p5sony[index] += sony;
        }
    }

    [ScriptMethod(name: "P5 二运死宣记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2976"],
        userControl: false)]
    public void P5_二运死宣记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;

        if (ParseObjectId(@event["TargetId"], out var id))
        {
            var index = accessory.Data.PartyList.ToList().IndexOf(id);
            p5sony[index] += 10;
        }
    }

    [ScriptMethod(name: "P5 二运盖里克位置记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:12637"],
        userControl: false)]
    public void P5_二运盖里克位置记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;
        p5GreekPos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    [ScriptMethod(name: "P5 二运死宣六方站位(ImGui)", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2976"])]
    public void P5_二运死宣六方站位(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;
        Task.Delay(100).ContinueWith(t =>
        {
            if (p5Deal) return;
            var count = p5sony.Where(s => s > 5).Count();
            if (count != 4) return;
            p5Deal = true;
            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            var sony = p5sony[idIndex];
            var posid = sony > 0 ? 4 : 0;
            for (var i = 0; i < idIndex; i++)
                if (sony == p5sony[i])
                    posid++;

            var cpos = new Vector3(100, 0, 100);
            var npos = 19.5f * Vector3.Normalize(new Vector3(p5GreekPos.X - 100, p5GreekPos.Y, p5GreekPos.Z - 100)) +
                       cpos;
            if (posid == 4 || posid == 7)
                npos = 13 * Vector3.Normalize(new Vector3(p5GreekPos.X - 100, p5GreekPos.Y, p5GreekPos.Z - 100)) + cpos;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me;
            dp.Scale = new Vector2(1.5f, 60);
            dp.DestoryAt = 7000;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Name = $"P5二运死宣引导站位{sony}";

            var d = float.Pi / 180f;
            if (posid == 0) dp.TargetPosition = RotatePoint(npos, cpos, d * -90);
            if (posid == 1) dp.TargetPosition = RotatePoint(npos, cpos, d * -142.5f);
            if (posid == 2) dp.TargetPosition = RotatePoint(npos, cpos, d * 142.5f);
            if (posid == 3) dp.TargetPosition = RotatePoint(npos, cpos, d * 90);
            if (posid == 4) dp.TargetPosition = RotatePoint(npos, cpos, d * -90);
            if (posid == 5) dp.TargetPosition = RotatePoint(npos, cpos, d * -37.5f);
            if (posid == 6) dp.TargetPosition = RotatePoint(npos, cpos, d * 37.5f);
            if (posid == 7) dp.TargetPosition = RotatePoint(npos, cpos, d * 90);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        });
    }

    [ScriptMethod(name: "P5 二运索尼引导站位(ImGui)", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:27533"])]
    public void P5_二运索尼引导站位(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;

        var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
        var sony = p5sony[idIndex];
        var posid = sony > 0 ? 4 : 0;
        for (var i = 0; i < idIndex; i++)
            if (sony == p5sony[i])
                posid++;

        var cpos = new Vector3(100, 0, 100);
        var npos = 10 * Vector3.Normalize(new Vector3(p5GreekPos.X - 100, p5GreekPos.Y, p5GreekPos.Z - 100)) + cpos;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = accessory.Data.Me;
        dp.Scale = new Vector2(1.5f, 60);
        dp.DestoryAt = 8000;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Name = $"P5二运索尼引导站位{sony}";

        var d = float.Pi / 180f;
        dp.TargetPosition = cpos;
        if (posid == 4) dp.TargetPosition = RotatePoint(npos, cpos, d * -90);
        if (posid == 7) dp.TargetPosition = RotatePoint(npos, cpos, d * 90);


        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "P5 二运索尼处理位置(横排法)(ImGui)", eventType: EventTypeEnum.TargetIcon)]
    public void P5_二运索尼处理位置_横排法(Event @event, ScriptAccessory accessory)
    {
        if (parse != 5.2) return;
        if (!ParseObjectId(@event["TargetId"], out var id) || id != accessory.Data.Me) return;
        Task.Delay(100).ContinueWith(ca =>
        {
            var index = accessory.Data.PartyList.ToList().IndexOf(id);
            var sony = p5sony[index];
            var priority = p5sony.IndexOf(sony) == index;
            var cpos = new Vector3(100, 0, 100);
            var npos = 4 * Vector3.Normalize(new Vector3(p5GreekPos.X - 100, p5GreekPos.Y, p5GreekPos.Z - 100)) + cpos;
            var npos2 = 20f * Vector3.Normalize(new Vector3(p5GreekPos.X - 100, p5GreekPos.Y, p5GreekPos.Z - 100)) +
                        cpos;


            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = id;
            dp.Scale = new Vector2(3, 60);
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Name = $"P5二运索尼{sony}处理位置";

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Color = accessory.Data.DefaultSafeColor;
            dp2.Scale = new Vector2(1f);
            dp2.DestoryAt = 5000;
            dp2.Name = $"P5二运索尼{sony}击退终点";
            //死宣○
            if (sony == 10)
            {
                if (priority)
                {
                    dp.TargetPosition = RotatePoint(npos, cpos, float.Pi / -2);
                    dp2.Position = RotatePoint(npos2, cpos, float.Pi / -2);
                }
                else
                {
                    dp.TargetPosition = RotatePoint(npos, cpos, float.Pi / 2);
                    dp2.Position = RotatePoint(npos2, cpos, float.Pi / 2);
                }
            }

            //死宣▽
            if (sony == 11)
            {
                dp.TargetPosition = RotatePoint(npos, cpos, float.Pi * 0.75f);
                dp2.Position = RotatePoint(npos2, cpos, float.Pi * 0.75f);
            }

            //死宣□
            if (sony == 12)
            {
                dp.TargetPosition = RotatePoint(npos, cpos, float.Pi * -0.75f);
                dp2.Position = RotatePoint(npos2, cpos, float.Pi * -0.75f);
            }

            //▽
            if (sony == 1)
            {
                dp.TargetPosition = RotatePoint(npos, cpos, float.Pi * -0.25f);
                dp2.Position = RotatePoint(npos2, cpos, float.Pi * -0.25f);
            }

            //□
            if (sony == 2)
            {
                dp.TargetPosition = RotatePoint(npos, cpos, float.Pi * 0.25f);
                dp2.Position = RotatePoint(npos2, cpos, float.Pi * 0.25f);
            }

            //×
            if (sony == 3)
            {
                if (priority)
                {
                    dp.TargetPosition = npos;
                    dp2.Position = npos2;
                }
                else
                {
                    dp.TargetPosition = RotatePoint(npos, cpos, float.Pi);
                    dp2.Position = RotatePoint(npos2, cpos, float.Pi);
                }
            }


            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp2);
        });
    }

    #endregion

    #endregion
}
