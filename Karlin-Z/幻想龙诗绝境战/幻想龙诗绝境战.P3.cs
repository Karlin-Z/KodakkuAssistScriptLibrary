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
    #region P3

    [ScriptMethod(name: "P3 记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26376"],
        userControl: false)]
    public void P3_记录(Event @event, ScriptAccessory accessory)
    {
        parse = 3;
        if (ParseObjectId(@event["SourceId"], out var id)) p3BossId = id;
    }

    [ScriptMethod(name: "P3 牙尾连旋(钢铁月环)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26386"])]
    public void P3_牙尾连旋(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;

        dp.Scale = new Vector2(8);
        dp.Radian = float.Pi * 2;
        dp.DestoryAt = 11500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        dp.Scale = new Vector2(40);
        dp.InnerScale = new Vector2(8);
        dp.Delay = 11500;
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    [ScriptMethod(name: "P3 尾牙连旋(月环钢铁)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26387"])]
    public void P3_尾牙连旋(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;

        dp.Scale = new Vector2(40);
        dp.InnerScale = new Vector2(8);
        dp.Radian = float.Pi * 2;
        dp.DestoryAt = 11500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

        dp.Delay = 11500;
        dp.Scale = new Vector2(8);
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P3 原地塔预测", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26382"])]
    public void P3_原地塔预测(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultSafeColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P3 上箭头塔预测", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26383"])]
    public void P3_上箭头塔预测(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultSafeColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Offset = new Vector3(0, 0, -14);
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P3 下箭头塔预测", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26384"])]
    public void P3_下箭头塔预测(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultSafeColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Offset = new Vector3(0, 0, -14);
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P3 塔位置确定", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26385"])]
    public void P3_塔位置确定(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultSafeColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 2500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P3 麻将武神枪引导", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26385"])]
    public void P3_麻将武神枪引导(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(8, 62);
        dp.DestoryAt = 2500;
        dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P3 四塔武神枪引导", eventType: EventTypeEnum.StartCasting)]
    public void P3_四塔武神枪引导(Event @event, ScriptAccessory accessory)
    {
        if (parse != 3) return;
        var aid = @event["ActionId"];
        if (aid != "26391" && aid != "26392" && aid != "26393" && aid != "26394") return;
        var str = @event["SourceId"];
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Name = $"P3_四塔武神枪引导{str}";
        if (ParseObjectId(str, out var id)) dp.Owner = id;
        dp.Scale = new Vector2(8, 62);
        dp.Delay = 5000;
        dp.DestoryAt = 2500;
        dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P3 四塔武神枪移除", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0054"], userControl: false)]
    public void P3_四塔武神枪移除(Event @event, ScriptAccessory accessory)
    {
        if (parse != 3) return;
        accessory.Method.RemoveDraw($"P3_四塔武神枪引导{@event["SourceId"]}");
    }

    [ScriptMethod(name: "P3 武神枪确定", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26378"])]
    public void P3_武神枪确定(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(8, 62);
        dp.DestoryAt = 4500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P3 同组麻将连线(ImGui)", eventType: EventTypeEnum.StatusAdd)]
    public void P3_同组麻将连线(Event @event, ScriptAccessory accessory)
    {
        if (parse != 3) return;
        var stasusid = @event["StatusID"];
        if (stasusid != "3004" && stasusid != "3005" && stasusid != "3006") return;
        if (ParseObjectId(@event["TargetId"], out var id))
        {
            if (p3majong.ContainsKey(stasusid))
            {
                p3majong[stasusid].Add(id);
            }
            else
            {
                p3majong.Add(stasusid, []);
                p3majong[stasusid].Add(id);
            }
        }


        if (id == accessory.Data.Me)
            Task.Delay(100).ContinueWith(o =>
            {
                foreach (var tid in p3majong[stasusid])
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "P3 同组麻将连线";
                    dp.Owner = id;
                    dp.TargetObject = tid;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 6000;
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
                }
            });
    }

    [ScriptMethod(name: "P3 腾龙枪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26380"])]
    public void P3_腾龙枪(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;
        dp.Scale = new Vector2(13);
        dp.Radian = float.Pi / 2;
        dp.DestoryAt = 3500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "P3 四塔记录", eventType: EventTypeEnum.StartCasting, userControl: false)]
    public void P3_四塔记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 3) return;
        var aid = @event["ActionId"];
        if (aid != "26391" && aid != "26392" && aid != "26393" && aid != "26394") return;
        var num = int.Parse(aid) - 26390;
        var sourcePos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var dir = PositionTo8Dir(sourcePos, new Vector3(100, 0, 100)) / 2;
        p3Tower[dir] = num;
    }

    [ScriptMethod(name: "P3 四塔站位(ImGui)", eventType: EventTypeEnum.StartCasting)]
    public void P3_四塔站位(Event @event, ScriptAccessory accessory)
    {
        if (parse != 3 || p3TowerDeal) return;
        var aid = @event["ActionId"];
        if (aid != "26391" && aid != "26392" && aid != "26393" && aid != "26394") return;
        p3TowerDeal = true;

        Task.Delay(100).ContinueWith(t =>
        {
            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            var myTower = -1;
            //D4
            if (idIndex == 7) myTower = 0;
            //H2
            if (idIndex == 3) myTower = 1;
            //H1
            if (idIndex == 2) myTower = 2;
            //D3
            if (idIndex == 6) myTower = 3;
            //St
            if (idIndex == 1)
            {
                if (p3Tower[0] >= 2)
                {
                    myTower = 0;
                }
                else
                {
                    if (p3Tower[1] > 2)
                        myTower = 1;
                    else if (p3Tower[3] > 2)
                        myTower = 3;
                    else if (p3Tower[2] > 2) myTower = 2;
                }
            }

            //D2
            if (idIndex == 5)
            {
                if (p3Tower[1] >= 2)
                {
                    myTower = 1;
                }
                else
                {
                    if (p3Tower[2] > 2)
                        myTower = 2;
                    else if (p3Tower[0] > 2)
                        myTower = 0;
                    else if (p3Tower[3] > 2) myTower = 3;
                }
            }

            //D1
            if (idIndex == 4)
            {
                if (p3Tower[2] >= 2)
                {
                    myTower = 2;
                }
                else
                {
                    if (p3Tower[3] > 2)
                        myTower = 3;
                    else if (p3Tower[1] > 2)
                        myTower = 1;
                    else if (p3Tower[0] > 2) myTower = 0;
                }
            }

            //Mt
            if (idIndex == 0)
            {
                if (p3Tower[3] >= 2)
                {
                    myTower = 3;
                }
                else
                {
                    if (p3Tower[0] > 2)
                        myTower = 0;
                    else if (p3Tower[2] > 2)
                        myTower = 2;
                    else if (p3Tower[1] > 2) myTower = 1;
                }
            }

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Position = RotatePoint(new Vector3(108, 0, 92), new Vector3(100, 0, 100), float.Pi / 2 * myTower);
            dp.Scale = new Vector2(5);
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        });
    }

    [ScriptMethod(name: "P3 追魂炮T辅助(ImGui)", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0054"])]
    public void P3_追魂T炮辅助(Event @event, ScriptAccessory accessory)
    {
        if (parse != 3) return;
        if (!ParseObjectId(@event["SourceId"], out var id)) return;
        var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
        if ((idIndex == 0 && id == p3BossId && !p3Boom[0]) || (idIndex == 1 && id != p3BossId && !p3Boom[1]))
        {
            p3Boom[idIndex] = true;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"P3 追魂炮{(idIndex == 0 ? "M" : "S")}T辅助";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = id;
            dp.Scale = new Vector2(10);
            dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
        }
    }

    [ScriptMethod(name: "P3 追魂炮范围", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0054"])]
    public void P3_追魂炮范围(Event @event, ScriptAccessory accessory)
    {
        if (parse != 3) return;
        if (!ParseObjectId(@event["SourceId"], out var id)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P3_追魂炮范围";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = id;
        dp.Scale = new Vector2(5);
        dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        dp.DestoryAt = 7000;
        if (id == p3BossId && !p3Boom[2])
        {
            p3Boom[2] = true;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        if (id != p3BossId && !p3Boom[3])
        {
            p3Boom[3] = true;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    #endregion
}
