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
    #region P6

    [ScriptMethod(name: "P6 开场记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26215"],
        userControl: false)]
    public void P6_开场记录(Event @event, ScriptAccessory accessory)
    {
        parse = 6.1;
        p6FireBallCount = 0;
        p6FireBallCount2 = 0;
    }

    [ScriptMethod(name: "P6 阶段累加", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27969"],
        userControl: false)]
    public void P6_阶段累加(Event @event, ScriptAccessory accessory)
    {
        parse = Math.Round(parse + 0.1, 1);
        if (ParseObjectId(@event["SourceId"], out var id)) whiteDragonId = id;
    }

    [ScriptMethod(name: "P6 黑龙ID", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27971"],
        userControl: false)]
    public void P6_黑龙ID(Event @event, ScriptAccessory accessory)
    {
        if (ParseObjectId(@event["SourceId"], out var id)) darkDragonId = id;
    }

    [ScriptMethod(name: "P6 白龙位置id记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:12613"],
        userControl: false)]
    public void P6_白龙位置id记录(Event @event, ScriptAccessory accessory)
    {
        p6WhitePos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        if (ParseObjectId(@event["SourceId"], out var sid)) whiteDragonId = sid;
    }

    [ScriptMethod(name: "P6 开场冰火线收集", eventType: EventTypeEnum.Tether, userControl: false)]
    public void P6_开场冰火线收集(Event @event, ScriptAccessory accessory)
    {
        if (parse != 6.1) return;

        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        p6tether[accessory.Data.PartyList.ToList().IndexOf(sid)] = tid == whiteDragonId ? 2 : 1;
    }

    [ScriptMethod(name: "P6 第一次冰火线站位(Imgui)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:27960"])]
    public void P6_第一次冰火线站位(Event @event, ScriptAccessory accessory)
    {
        if (parse != 6.1) return;

        List<Vector3> postions = [new(100, 0, 109.33f), new(95.7f, 0, 119), new(104.3f, 0, 119)];
        //45 26 37
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P6 第一次冰火线站位";
        dp.Owner = accessory.Data.Me;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Scale = new Vector2(1.5f);
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.DestoryAt = 7000;
        var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
        //D1
        if (idIndex == 4) dp.TargetPosition = postions[0];
        if (idIndex == 2) dp.TargetPosition = postions[1];
        if (idIndex == 3) dp.TargetPosition = postions[2];
        //D2
        if (idIndex == 5)
        {
            if (p6tether[4] != p6tether[5])
            {
                dp.TargetPosition = postions[0];
            }
            else
            {
                if (p6tether[2] == p6tether[6])
                    dp.TargetPosition = postions[1];
                else
                    dp.TargetPosition = postions[2];
            }
        }

        //D3
        if (idIndex == 6)
        {
            if (p6tether[2] != p6tether[6])
            {
                dp.TargetPosition = postions[1];
            }
            else
            {
                if (p6tether[4] == p6tether[5])
                    dp.TargetPosition = postions[0];
                else
                    dp.TargetPosition = postions[2];
            }
        }

        //D4
        if (idIndex == 7)
        {
            if (p6tether[3] != p6tether[7])
            {
                dp.TargetPosition = postions[2];
            }
            else
            {
                if (p6tether[4] == p6tether[5])
                    dp.TargetPosition = postions[0];
                else
                    dp.TargetPosition = postions[1];
            }
        }

        if (idIndex > 1) accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "P6 第一次冰火线黑龙扇形", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27955"])]
    public void P6_第一次冰火线黑龙扇形(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(50);
        dp.Radian = float.Pi / 6f;
        dp.DestoryAt = 7000;
        dp.Name = "P6 第一次冰火线黑龙扇形";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "P6 第一次冰火线白龙扇形", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27957"])]
    public void P6_第一次冰火线白龙扇形(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(50);
        dp.Radian = float.Pi / 6f;
        dp.DestoryAt = 7000;
        dp.Name = "P6 第一次冰火线白龙扇形";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "P6 无尽轮回", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27969"],
        userControl: false)]
    public void P6_无尽轮回(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P6 无尽轮回";
        dp.Scale = new Vector2(4);
        dp.DestoryAt = 8300;
        var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
        if (idIndex == 0 || idIndex == 2 || idIndex == 4 || idIndex == 6)
        {
            dp.Owner = accessory.Data.PartyList[2];
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp.Owner = accessory.Data.PartyList[3];
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        else
        {
            dp.Owner = accessory.Data.PartyList[3];
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp.Owner = accessory.Data.PartyList[2];
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "P6 灭杀誓言分散", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27960"],
        userControl: false)]
    public void P6_灭杀誓言分散(Event @event, ScriptAccessory accessory)
    {
        if (parse != 6.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.DestoryAt = 7300;
        for (var i = 4; i < accessory.Data.PartyList.Count; i++)
        {
            dp.Name = $"P6 灭杀誓言分散 D{i - 3}";
            dp.Owner = accessory.Data.PartyList[i];
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "P6 灭杀誓言范围", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2896"])]
    public void P6_灭杀誓言范围(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["TargetId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 5000;
        dp.Delay = int.TryParse(@event["DurationMilliseconds"], out var d) ? d - 5000 : 0;

        dp.Name = "P6 灭杀誓言";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P6 第一次冰火线安全点(ImGui)", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:26215"])]
    public void P6_第一次冰火线安全点(Event @event, ScriptAccessory accessory)
    {
        if (parse != 6.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultSafeColor;

        dp.Scale = new Vector2(0.2f);
        dp.Delay = 20000;
        dp.DestoryAt = 15000;
        dp.Name = "P6 第一次冰火线安全点";

        dp.Position = new Vector3(95.7f, 0, 119);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        dp.Position = new Vector3(104.3f, 0, 119);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        dp.Position = new Vector3(100, 0, 109.33f);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P6 神圣之翼(左近)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27939"])]
    public void P6_神圣之翼左近(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(10);
        dp.DestoryAt = 8000;
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.CentreOrderIndex = 1;
        dp.Name = "P6_神圣之翼近1";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.CentreOrderIndex = 2;
        dp.Name = "P6_神圣之翼近2";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Color = accessory.Data.DefaultDangerColor;
        dp2.Scale = new Vector2(22, 50);
        dp2.Owner = id;
        dp2.DestoryAt = 8000;
        dp2.Offset = new Vector3(-11, 0, 0);
        dp2.Name = "P6_神圣之翼左";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
    }

    [ScriptMethod(name: "P6 神圣之翼(左远)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27940"])]
    public void P6_神圣之翼左远(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(10);
        dp.DestoryAt = 8000;
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
        dp.CentreOrderIndex = 1;
        dp.Name = "P6_神圣之翼远1";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.CentreOrderIndex = 2;
        dp.Name = "P6_神圣之翼远2";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Color = accessory.Data.DefaultDangerColor;
        dp2.Scale = new Vector2(22, 50);
        dp2.Owner = id;
        dp2.DestoryAt = 8000;
        dp2.Offset = new Vector3(-11, 0, 0);
        dp2.Name = "P6_神圣之翼左";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
    }

    [ScriptMethod(name: "P6 神圣之翼(右近)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27942"])]
    public void P6_神圣之翼右近(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(10);
        dp.DestoryAt = 8000;
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.CentreOrderIndex = 1;
        dp.Name = "P6_神圣之翼近1";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.CentreOrderIndex = 2;
        dp.Name = "P6_神圣之翼近2";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Color = accessory.Data.DefaultDangerColor;
        dp2.Scale = new Vector2(22, 50);
        dp2.Owner = id;
        dp2.DestoryAt = 8000;
        dp2.Offset = new Vector3(11, 0, 0);
        dp2.Name = "P6_神圣之翼右";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
    }

    [ScriptMethod(name: "P6 神圣之翼(右远)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27943"])]
    public void P6_神圣之翼右远(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(10);
        dp.DestoryAt = 8000;
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
        dp.CentreOrderIndex = 1;
        dp.Name = "P6_神圣之翼远1";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        dp.CentreOrderIndex = 2;
        dp.Name = "P6_神圣之翼远2";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Color = accessory.Data.DefaultDangerColor;
        dp2.Scale = new Vector2(22, 50);
        dp2.Owner = id;
        dp2.DestoryAt = 8000;
        dp2.Offset = new Vector3(11, 0, 0);
        dp2.Name = "P6_神圣之翼右";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
    }

    [ScriptMethod(name: "P6 第一次黑龙俯冲", eventType: EventTypeEnum.StartCasting)]
    public void P6_第一次黑龙俯冲(Event @event, ScriptAccessory accessory)
    {
        if (parse != 6.2) return;
        if (!uint.TryParse(@event["ActionId"], out var actionId)) return;
        if (actionId != 27939 && actionId != 27940 && actionId != 27942 && actionId != 27943) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Scale = new Vector2(22, 80);
        dp.Owner = darkDragonId;
        dp.DestoryAt = 7500;
        dp.Name = "P6_第一次黑龙俯冲";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P6 燃烧之翼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27948"])]
    public void P6_燃烧之翼(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(21, 50);
        dp.DestoryAt = 6500;
        dp.Name = "P6 燃烧之翼";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P6 燃烧之尾", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27950"])]
    public void P6_燃烧之尾(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(18, 50);
        dp.DestoryAt = 6500;
        dp.Name = "P6 燃烧之尾";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P6 火球范围", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:13238"])]
    public void P6_火球范围(Event @event, ScriptAccessory accessory)
    {
        lock (lockObj)
        {
            p6FireBallCount++;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            //第一轮
            if (p6FireBallCount == 3)
            {
                dp.Name = "P6 火球范围1";
                dp.Scale = new Vector2(18, 44);
                dp.Position = new Vector3(100, 0, 100);
                dp.DestoryAt = 12000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                dp.Rotation = float.Pi / 2;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
            }

            //第二轮
            if (p6FireBallCount == 6)
            {
                dp.Name = "P6 火球范围2";
                dp.Scale = new Vector2(18, 70);
                var ipos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                var pos = new Vector3(100, 0, 100);
                if (ipos.X < 93.5f) pos.X = 87;
                if (ipos.X > 106.5f) pos.X = 113;
                if (ipos.Z < 93.5f) pos.Z = 87;
                if (ipos.Z > 106.5f) pos.Z = 113;
                dp.Position = pos;
                dp.Delay = 6000;
                dp.DestoryAt = 12000 - dp.Delay;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                dp.Rotation = float.Pi / 2;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
            }

            if (p6FireBallCount == 9)
            {
                dp.Name = "P6 火球范围3";
                dp.Scale = new Vector2(18, 70);
                var ipos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                var pos = new Vector3(100, 0, 100);
                if (ipos.X < 93.5f) pos.X = 87;
                if (ipos.X > 106.5f) pos.X = 113;
                if (ipos.Z < 93.5f) pos.Z = 87;
                if (ipos.Z > 106.5f) pos.Z = 113;
                dp.Position = pos;
                dp.Delay = 8000;
                dp.DestoryAt = 12000 - dp.Delay;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                dp.Rotation = float.Pi / 2;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
            }
        }
    }

    [ScriptMethod(name: "P6 十字火白龙俯冲", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27973"])]
    public void P6_十字火白龙俯冲(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = whiteDragonId;
        dp.Scale = new Vector2(22, 80);
        dp.Delay = 1500;
        dp.DestoryAt = 11000 - dp.Delay;
        dp.Name = "P6 十字火白龙俯冲";
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P6 十字火起跑位置(ImGui)", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:13238"])]
    public void P6_十字火起跑位置(Event @event, ScriptAccessory accessory)
    {
        lock (lockObj)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            p6FireBallCount2++;
            if (p6FireBallCount2 == 6)
            {
                dp.Name = "P6 十字火起跑位置";
                dp.Scale = new Vector2(1.5f);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Owner = accessory.Data.Me;

                var ipos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                var pos = new Vector3(100, 0, 100);
                if (ipos.Z < 93.5f) pos.Z = 109.5f;
                if (ipos.Z > 106.5f) pos.Z = 90.5f;
                if (p6WhitePos.X < 99) pos.X = 121.5f;
                else pos.X = 78.5f;

                dp.TargetPosition = pos;
                dp.DestoryAt = 6000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }
    }

    [ScriptMethod(name: "P6 第二次冰火线ND站位(ImGui)", eventType: EventTypeEnum.StartCasting)]
    public void P6_第二次冰火线ND站位(Event @event, ScriptAccessory accessory)
    {
        if (parse != 6.3) return;
        var aidStr = @event["ActionId"];
        if (aidStr != "27956" && aidStr != "27957") return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P6 第二次冰火线ND站位";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = accessory.Data.Me;
        dp.Scale = new Vector2(1.5f);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.DestoryAt = 7000;

        var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);

        if (idIndex == 2) dp.TargetPosition = new Vector3(100, 0, 80.5f);
        if (idIndex == 3) dp.TargetPosition = new Vector3(100, 0, 119.7f);
        if (idIndex == 4) dp.TargetPosition = new Vector3(103.7f, 0, 89.2f);
        if (idIndex == 5) dp.TargetPosition = new Vector3(97, 0, 110.2f);
        if (idIndex == 6) dp.TargetPosition = new Vector3(107.2f, 0, 81.7f);
        if (idIndex == 7) dp.TargetPosition = new Vector3(92.5f, 0, 118);

        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "P6 双龙冰火俯冲", eventType: EventTypeEnum.StatusAdd)]
    public void P6_双龙冰火俯冲(Event @event, ScriptAccessory accessory)
    {
        if (parse != 6.3) return;
        var StatusIDStr = @event["StatusID"];
        if (StatusIDStr != "2898" && StatusIDStr != "2899") return;
        if (!ParseObjectId(@event["TargetId"], out var id) || id != accessory.Data.Me) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(22, 56);
        dp.Delay = 6500;
        dp.DestoryAt = 12500 - dp.Delay;
        if (StatusIDStr == "2898")
        {
            dp.Name = "P6 双龙冰火俯冲 黑龙 火 危险";
            dp.Owner = darkDragonId;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

            dp.Name = "P6 双龙冰火俯冲 白龙 火 安全";
            dp.Owner = whiteDragonId;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        else
        {
            dp.Name = "P6 双龙冰火俯冲 黑龙 冰 安全";
            dp.Owner = darkDragonId;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

            dp.Name = "P6 双龙冰火俯冲 白龙 冰 危险";
            dp.Owner = whiteDragonId;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    [ScriptMethod(name: "P6 双龙冰火俯冲 T黑龙", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27966"])]
    public void P6_双龙冰火俯冲_T黑龙(Event @event, ScriptAccessory accessory)
    {
        if (parse != 6.3) return;
        var index = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
        if (index == 0)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new Vector2(22, 56);
            dp.DestoryAt = 5000;
            dp.Name = "P6 双龙冰火俯冲 MT黑龙 危险";
            dp.Owner = darkDragonId;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        if (index == 1)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new Vector2(22, 56);
            dp.DestoryAt = 5000;
            dp.Name = "P6 双龙冰火俯冲 ST黑龙 安全";
            dp.Owner = darkDragonId;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    [ScriptMethod(name: "P6 双龙冰火俯冲 T白龙", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27966"])]
    public void P6_双龙冰火俯冲_T白龙(Event @event, ScriptAccessory accessory)
    {
        if (parse != 6.3) return;
        var index = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
        if (index == 0)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new Vector2(22, 56);
            dp.DestoryAt = 5000;
            dp.Name = "P6 双龙冰火俯冲 MT白龙 安全";
            dp.Owner = whiteDragonId;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        if (index == 1)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new Vector2(22, 56);
            dp.DestoryAt = 5000;
            dp.Name = "P6 双龙冰火俯冲 ST白龙 危险";
            dp.Owner = whiteDragonId;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    [ScriptMethod(name: "P6 暗buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2758"],
        userControl: false)]
    public void P6_暗buff记录(Event @event, ScriptAccessory accessory)
    {
        if (ParseObjectId(@event["TargetId"], out var id))
            p6lightDark[accessory.Data.PartyList.ToList().IndexOf(id)] = 1;
    }

    [ScriptMethod(name: "P6 光buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2759"],
        userControl: false)]
    public void P6_光buff记录(Event @event, ScriptAccessory accessory)
    {
        if (ParseObjectId(@event["TargetId"], out var id))
            p6lightDark[accessory.Data.PartyList.ToList().IndexOf(id)] = 2;
    }

    [ScriptMethod(name: "P6 邪念之炎/同归于尽之炎", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27974"])]
    public void P6_邪念之炎(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(18000).ContinueWith(t =>
        {
            var plist = accessory.Data.PartyList.ToList();
            var idIndex = plist.IndexOf(accessory.Data.Me);
            for (var i = 0; i < p6lightDark.Count; i++)
            {
                if (p6lightDark[i] == 0) continue;
                if (p6lightDark[i] == 1)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Owner = plist[i];
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Scale = new Vector2(5);
                    dp.DestoryAt = 5000;
                    dp.Name = "P6 邪念之炎";
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }

                if (p6lightDark[i] == 2)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Owner = plist[i];
                    dp.Scale = new Vector2(4);
                    dp.DestoryAt = 5000;
                    dp.Name = "P6 同归于尽之炎";
                    if (i == idIndex || (p6lightDark.IndexOf(2) == i && p6lightDark.IndexOf(0) == idIndex) ||
                        (p6lightDark.LastIndexOf(2) == i && p6lightDark.LastIndexOf(0) == idIndex))
                        dp.Color = accessory.Data.DefaultSafeColor;
                    else
                        dp.Color = accessory.Data.DefaultDangerColor;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
            }
        });
    }

    [ScriptMethod(name: "P6 邪念之炎/同归于尽之炎标记", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27974"],
        userControl: false)]
    public void P6_邪念之炎标记(Event @event, ScriptAccessory accessory)
    {
        if (!p6Mark) return;
        accessory.Method.MarkClear();
        Task.Delay(50).ContinueWith(t =>
        {
            var plist = accessory.Data.PartyList.ToList();
            var attack = 0;
            var stop = 8;
            var bind = 5;
            for (var i = 0; i < p6lightDark.Count; i++)
            {
                if (p6lightDark[i] == 0)
                {
                    //无
                    stop++;
                    accessory.Method.Mark(plist[i], (MarkType)stop);
                }

                if (p6lightDark[i] == 1)
                {
                    //分散
                    attack++;
                    accessory.Method.Mark(plist[i], (MarkType)attack);
                }

                if (p6lightDark[i] == 2)
                {
                    //分摊
                    bind++;
                    accessory.Method.Mark(plist[i], (MarkType)bind);
                }
            }
        });
        Task.Delay(23000).ContinueWith(t => { accessory.Method.MarkClear(); });
    }

    #endregion
}
