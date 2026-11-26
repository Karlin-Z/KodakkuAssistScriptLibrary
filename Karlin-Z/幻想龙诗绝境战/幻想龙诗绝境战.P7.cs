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
    #region P7

    [ScriptMethod(name: "P7 开场记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:29752"],
        userControl: false)]
    public void P7_开场记录(Event @event, ScriptAccessory accessory)
    {
        parse = 7.0;
    }

    [ScriptMethod(name: "P7 阶段累加地火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28059"],
        userControl: false)]
    public void P7_阶段累加地火(Event @event, ScriptAccessory accessory)
    {
        parse = Math.Round(parse + 0.1, 1);
    }

    [ScriptMethod(name: "P7 阶段累加死亡轮回", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28051"],
        userControl: false)]
    public void P7_阶段累加死亡轮回(Event @event, ScriptAccessory accessory)
    {
        parse = Math.Round(parse + 0.1, 1);
    }

    [ScriptMethod(name: "P7 阶段累加陨石", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28057"],
        userControl: false)]
    public void P7_阶段累加陨石(Event @event, ScriptAccessory accessory)
    {
        parse = Math.Round(parse + 0.1, 1);
    }

    [ScriptMethod(name: "P7 钢铁", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:2056", "StackCount:42"])]
    public void P7_钢铁(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P7 钢铁";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Scale = new Vector2(8);
        if (ParseObjectId(@event["TargetId"], out var id)) dp.Owner = id;
        if (parse == 7.3 || parse == 7.6 || parse == 7.9)
            dp.DestoryAt = 8000;
        else
            dp.DestoryAt = 6000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P7 月环", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:2056", "StackCount:43"])]
    public void P7_月环(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P7 月环";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Radian = float.Pi * 2;
        dp.Scale = new Vector2(50);
        dp.InnerScale = new Vector2(8);
        if (ParseObjectId(@event["TargetId"], out var id)) dp.Owner = id;
        if (parse == 7.3 || parse == 7.6 || parse == 7.9)
            dp.DestoryAt = 8000;
        else
            dp.DestoryAt = 6000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    [ScriptMethod(name: "P7 脑死地火点位", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28059"])]
    public void P7_脑死地火点位(Event @event, ScriptAccessory accessory)
    {
        var cpos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var r = float.Parse(@event["SourceRotation"]);

        var pos1 = new Vector3(cpos.X + MathF.Sin(r) * -8, cpos.Y, cpos.Z + MathF.Cos(r) * -8);
        var pos2 = new Vector3(cpos.X + MathF.Sin(r) * -14, cpos.Y, cpos.Z + MathF.Cos(r) * -14);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P7 脑死地火点位1";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Scale = new Vector2(1.5f);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = pos1;
        dp.DestoryAt = 9000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P7 脑死地火点位2";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Scale = new Vector2(1.5f);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = pos2;
        dp.Delay = 9000;
        dp.DestoryAt = 2000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P7 脑死地火点位3";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Scale = new Vector2(1.5f);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Position = pos1;
        dp.TargetPosition = pos2;
        dp.DestoryAt = 9000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "P7 死亡轮回剑分摊处(Imgui)", eventType: EventTypeEnum.StartCasting)]
    public void P7_死亡轮回剑分摊处(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(50).ContinueWith(t =>
        {
            var idstr = @event["ActionId"];
            if (idstr != "29452" && idstr != "29453" && idstr != "29454") return;

            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);

            var isme = false;
            accessory.Log.Debug($"parse:{parse}");
            if (parse == 7.2 || !p7_116)
            {
                if (idstr == "29452" && (idIndex == 3 || idIndex == 5 || idIndex == 7)) isme = true;
                if (idstr == "29453" && (idIndex == 2 || idIndex == 4 || idIndex == 6)) isme = true;
                if (idstr == "29454" && (idIndex == 0 || idIndex == 1)) isme = true;
            }
            else
            {
                if (parse == 7.5)
                {
                    if (idstr == "29452" && idIndex == 0) isme = true;
                    if (idstr == "29453" && idIndex != 0 && idIndex != 1) isme = true;
                    if (idstr == "29454" && idIndex == 1) isme = true;
                }

                if (parse == 7.8)
                {
                    if (idstr == "29452" && idIndex == 1) isme = true;
                    if (idstr == "29453" && idIndex != 0 && idIndex != 1) isme = true;
                    if (idstr == "29454" && idIndex == 0) isme = true;
                }
            }

            if (isme)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P7 死亡轮回剑分摊处";
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Scale = new Vector2(1.5f);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Owner = accessory.Data.Me;
                if (ParseObjectId(@event["SourceId"], out var sid)) dp.TargetObject = sid;
                dp.DestoryAt = 6700;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "P7 死亡轮回剑分摊范围";
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Scale = new Vector2(4);
                if (ParseObjectId(@event["SourceId"], out var sid2)) dp.Owner = sid2;
                dp.DestoryAt = 12000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        });
    }

    [ScriptMethod(name: "P7 一号核爆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28058"])]
    public void P7_一号核爆(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P7 一号核爆";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Scale = new Vector2(21f);
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.DestoryAt = 9000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P7 二号核爆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28114"])]
    public void P7_二号核爆(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P7 二号核爆";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Scale = new Vector2(21f);
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.Delay = 9000;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P7 三号核爆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28115"])]
    public void P7_三号核爆(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P7 二号核爆";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Scale = new Vector2(21f);
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.Delay = 13000;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P7 一号核爆位置收集", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28058"],
        userControl: false)]
    public void P7_一号核爆位置收集(Event @event, ScriptAccessory accessory)
    {
        p7Stone1 = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    [ScriptMethod(name: "P7 二号核爆位置收集", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28114"],
        userControl: false)]
    public void P7_二号核爆位置收集(Event @event, ScriptAccessory accessory)
    {
        p7Stone2 = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    [ScriptMethod(name: "P7 核爆1跑2(Imgui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28114"])]
    public void P7_核爆1跑2(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(50).ContinueWith(t =>
        {
            var cpos = new Vector3(100, 0, 100);
            var dot1 = Vector3.Normalize(cpos - p7Stone1);
            var pos1 = p7Stone1 + dot1 * 21f;
            var stone2pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var dot2 = Vector3.Normalize(cpos - p7Stone2);
            var pos2 = stone2pos + dot2 * 21f;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P7 核爆跑1";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = pos1;
            dp.Scale = new Vector2(1.5f);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.DestoryAt = 9000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "P7 核爆1跑2";
            dp2.Color = accessory.Data.DefaultSafeColor;
            dp2.Position = pos1;
            dp2.TargetPosition = pos2;
            dp2.Scale = new Vector2(1.5f);
            dp2.ScaleMode |= ScaleMode.YByDistance;
            dp2.DestoryAt = 9000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);

            var dp3 = accessory.Data.GetDefaultDrawProperties();
            dp3.Name = "P7 核爆跑2";
            dp3.Color = accessory.Data.DefaultSafeColor;
            dp3.Owner = accessory.Data.Me;
            dp3.TargetPosition = pos2;
            dp3.Scale = new Vector2(1.5f);
            dp3.ScaleMode |= ScaleMode.YByDistance;
            dp3.Delay = 9000;
            dp3.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
        });
    }

    [ScriptMethod(name: "P7 核爆2跑3(Imgui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28115"])]
    public void P7_核爆2跑3(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(50).ContinueWith(t =>
        {
            var cpos = new Vector3(100, 0, 100);
            var dot1 = Vector3.Normalize(cpos - p7Stone2);
            var pos1 = p7Stone2 + dot1 * 21f;
            var stone3pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var dot2 = Vector3.Normalize(cpos - stone3pos);
            var pos2 = stone3pos + dot2 * 21f;


            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "P7 核爆2跑3";
            dp2.Color = accessory.Data.DefaultSafeColor;
            dp2.Position = pos1;
            dp2.TargetPosition = pos2;
            dp2.Scale = new Vector2(1.5f);
            dp2.ScaleMode |= ScaleMode.YByDistance;
            dp2.DestoryAt = 13000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);

            var dp3 = accessory.Data.GetDefaultDrawProperties();
            dp3.Name = "P7 核爆跑3";
            dp3.Color = accessory.Data.DefaultSafeColor;
            dp3.Owner = accessory.Data.Me;
            dp3.TargetPosition = pos2;
            dp3.Scale = new Vector2(1.5f);
            dp3.ScaleMode |= ScaleMode.YByDistance;
            dp3.Delay = 13000;
            dp3.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
        });
    }

    #endregion
}
