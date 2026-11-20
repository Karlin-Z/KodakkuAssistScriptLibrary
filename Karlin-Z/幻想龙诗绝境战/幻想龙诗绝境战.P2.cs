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
    #region P2

    #region 一运

    [ScriptMethod(name: "P2 1运记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25555"],
        userControl: false)]
    public void P2_1运记录(Event @event, ScriptAccessory accessory)
    {
        parse = 2.1;
        firstTargetIcon = null;
        if (ParseObjectId(@event["SourceId"], out var id)) tordanId = id;
    }

    [ScriptMethod(name: "P2 1运波勒克兰冲锋", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:3781"])]
    public void P2_1运波勒克兰冲锋(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(16, 52);
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.DestoryAt = 7000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P2 1运伊尼亚斯冲锋", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:3782"])]
    public void P2_1运伊尼亚斯冲锋(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(16, 52);
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.DestoryAt = 7000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P2 1运韦尔吉纳冲锋", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:3783"])]
    public void P2_1运韦尔吉纳冲锋(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(16, 52);
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.DestoryAt = 7000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P2 1运冲锋位置记录", eventType: EventTypeEnum.NpcYell, userControl: false)]
    public void P2_1运冲锋位置记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        var str = @event["Id"];
        if (str != "3781" && str != "3782" && str != "3783") return;
        var sourcePos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var dir = PositionTo8Dir(sourcePos, new Vector3(100, 0, 100));
        if (dir == 0 || dir == 4)
        {
            p2SafeDir[0] = false;
            p2SafeDir[4] = false;
        }

        if (dir == 1 || dir == 5)
        {
            p2SafeDir[1] = false;
            p2SafeDir[5] = false;
        }

        if (dir == 2 || dir == 6)
        {
            p2SafeDir[2] = false;
            p2SafeDir[6] = false;
        }

        if (dir == 3 || dir == 7)
        {
            p2SafeDir[3] = false;
            p2SafeDir[7] = false;
        }
    }

    [ScriptMethod(name: "P2 1运冲锋安全区位置(Imgui)", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:3781"])]
    public void P2_1运冲锋安全区位置(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        Task.Delay(100).ContinueWith(y =>
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2 1运冲锋安全区位置";
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me;
            dp.DestoryAt = 7000;

            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            var cpos = new Vector3(100, 0, 100);
            var npos = new Vector3(100, 0, 82);
            //MT
            if (idIndex == 0 || idIndex == 2 || idIndex == 4 || idIndex == 6)
                dp.TargetPosition = RotatePoint(npos, cpos, p2SafeDir.LastIndexOf(true) * float.Pi / 4);
            else //ST
                dp.TargetPosition = RotatePoint(npos, cpos, p2SafeDir.IndexOf(true) * float.Pi / 4);

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
        });
    }

    [ScriptMethod(name: "P2 1运地震", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25558"])]
    public void P2_1运地震(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Name = "P2 1运地震";
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;

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

    [ScriptMethod(name: "P2 1运穿天记录", eventType: EventTypeEnum.TargetIcon, userControl: false)]
    public void P2_1运穿天记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        if (ParsTargetIcon(@event["Id"]) != 0) return;

        if (ParseObjectId(@event["TargetId"], out var id)) p2BlueCircle.Add(id);
    }

    [ScriptMethod(name: "P2 1运空间破碎", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25564"])]
    public void P2_1运空间破碎(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P2 1运空间破碎";
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;

        dp.Scale = new Vector2(9);
        dp.Delay = 3000;
        dp.DestoryAt = 9000 - dp.Delay;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P2 1运穿天(大圈)", eventType: EventTypeEnum.TargetIcon)]
    public void P2_1运穿天(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        if (ParsTargetIcon(@event["Id"]) != 0) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(24);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(0.5f);
        if (ParseObjectId(@event["TargetId"], out var id)) dp.Owner = id;
        dp.Delay = 6000;
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P2 一运穿天连线(ImGui)", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25562"])]
    public void P2_一运穿天连线(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P2 一运穿天连线";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = p2BlueCircle[0];
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.DestoryAt = 9000;
        for (var i = 1; i < p2BlueCircle.Count; i++)
        {
            dp.TargetObject = p2BlueCircle[i];
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
        }
    }

    [ScriptMethod(name: "P2 1运让勒努冲锋", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:2551"])]
    public void P2_1运让勒努冲锋(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(8, 50);
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        dp.Delay = 3000;
        dp.DestoryAt = 3000;
        dp.ScaleMode |= ScaleMode.YByDistance;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P2 1运阿代尔菲尔冲锋", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:2550"])]
    public void P2_1运阿代尔菲尔冲锋(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(8, 50);
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var sid)) dp.Owner = sid;
        dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        dp.Delay = 3000;
        dp.DestoryAt = 3000;
        dp.ScaleMode |= ScaleMode.YByDistance;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P2 1运骑神位置(Imgui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25563"])]
    public void P2_1运骑神位置(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.1) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P2 1运骑神位置";
        dp.Scale = new Vector2(8, 50);
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.TargetObject = tordanId;
        dp.Owner = accessory.Data.Me;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Delay = 500;
        dp.DestoryAt = 7000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
    }

    #endregion

    #region 二运

    [ScriptMethod(name: "P2 二运记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25569"],
        userControl: false)]
    public void P2_二运记录(Event @event, ScriptAccessory accessory)
    {
        parse = 2.2;
    }

    [ScriptMethod(name: "P2 二运龙眼背对", eventType: EventTypeEnum.EnvControl,
        eventCondition: ["DirectorId:8003759A", "Id:00020001"])]
    public void P2_二运龙眼背对(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        var index = int.Parse(@event["Index"], NumberStyles.HexNumber);
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P2_二运龙眼背对";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = accessory.Data.Me;
        dp.Delay = 4500;
        dp.DestoryAt = 5000;
        if (index == 0) dp.TargetPosition = new Vector3(100, 0, 65);
        if (index == 1) dp.TargetPosition = new Vector3(124.75f, 0, 75.25f);
        if (index == 2) dp.TargetPosition = new Vector3(135, 0, 100);
        if (index == 3) dp.TargetPosition = new Vector3(124.75f, 0, 124.75f);
        if (index == 4) dp.TargetPosition = new Vector3(100, 0, 135);
        if (index == 5) dp.TargetPosition = new Vector3(75.25f, 0, 124.75f);
        if (index == 6) dp.TargetPosition = new Vector3(65, 0, 100);
        if (index == 7) dp.TargetPosition = new Vector3(75.25f, 0, 75.25f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);
    }

    [ScriptMethod(name: "P2 二运骑神背对", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25552"])]
    public void P2_二运骑神背对(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P2_二运骑神背对";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = accessory.Data.Me;
        if (ParseObjectId(@event["TargetId"], out var id)) dp.TargetObject = id;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);
    }

    [ScriptMethod(name: "P2 二运泽菲兰位置记录", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:2549"],
        userControl: false)]
    public void P2_二运泽菲兰位置记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        p2ZPos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    [ScriptMethod(name: "P2 二运劈刀记录", eventType: EventTypeEnum.TargetIcon, userControl: false)]
    public void P2_二运劈刀记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        var tid = ParsTargetIcon(@event["Id"]);
        if (tid != -279 && tid != -280) return;
        if (ParseObjectId(@event["TargetId"], out var id))
        {
            var index = accessory.Data.PartyList.ToList().IndexOf(id);
            if (tid == -280) p2Jump.Item1 = index;
            if (tid == -279) p2Jump.Item2 = index;
        }
    }

    [ScriptMethod(name: "P2 二运阿代尔菲尔位置", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:12601"],
        userControl: false)]
    public void P2_二运阿代尔菲尔位置(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        p2AdelPos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    [ScriptMethod(name: "P2 二运劈刀起跑位置(Imgui)", eventType: EventTypeEnum.TargetIcon)]
    public void P2_二运劈刀起跑位置(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        if (ParsTargetIcon(@event["Id"]) != -279) return;
        Task.Delay(100).ContinueWith(t =>
        {
            List<int> g1Mem = [];
            List<int> g2Mem = [];

            // 2 6
            // 2 1 3 7
            // 6 0 
            g1Mem.Add(p2Jump.Item1);
            g2Mem.Add(p2Jump.Item2);
            if (p2Jump.Item1 != 0 && p2Jump.Item2 != 0) g2Mem.Add(0);
            if (p2Jump.Item1 != 2 && p2Jump.Item2 != 2) g2Mem.Add(2);
            if (p2Jump.Item1 != 6 && p2Jump.Item2 != 6) g2Mem.Add(6);
            if (p2Jump.Item1 != 1 && p2Jump.Item2 != 1) g1Mem.Add(1);
            if (p2Jump.Item1 != 3 && p2Jump.Item2 != 3) g1Mem.Add(3);
            if (p2Jump.Item1 != 7 && p2Jump.Item2 != 7) g1Mem.Add(7);
            if (p2Jump.Item1 != 4 && p2Jump.Item2 != 4)
            {
                if (g2Mem.Count <= 3) g2Mem.Add(4);
                else g1Mem.Add(4);
            }

            if (p2Jump.Item1 != 5 && p2Jump.Item2 != 5)
            {
                if (g1Mem.Count <= 3) g1Mem.Add(5);
                else g2Mem.Add(5);
            }

            var drot = p2AdelPos.X > 100 ? float.Pi / 45 : float.Pi / -45;
            var meIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new Vector2(1.5f, 20);
            dp.Color = accessory.Data.DefaultSafeColor.WithW(3);
            dp.Owner = accessory.Data.Me;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.YByDistance;

            var cpos = new Vector3(100, 0, 100);
            var sPos = (p2ZPos - cpos) / 15 * 19.5f + cpos;
            if (g1Mem.IndexOf(meIndex) != -1)
                dp.TargetPosition = RotatePoint(sPos, cpos, float.Pi + drot * 3);
            else
                dp.TargetPosition = RotatePoint(sPos, cpos, drot * 3);

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);


            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Color = accessory.Data.DefaultSafeColor.WithW(3);
            dp2.Scale = new Vector2(1.5f, 20);
            dp2.ScaleMode |= ScaleMode.YByDistance;
            dp2.DestoryAt = 15000;
            dp2.Position = dp.TargetPosition;
            dp2.TargetPosition = RotatePoint(dp2.Position.Value, cpos, drot * 5);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        });
    }

    [ScriptMethod(name: "P2 二运光球爆炸范围", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:13070"])]
    public void P2_二运光球爆炸范围(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new Vector2(9);
        dp.Color = accessory.Data.DefaultDangerColor;
        var idStr = @event["SourceId"];
        if (ParseObjectId(idStr, out var id)) dp.Owner = id;
        dp.DestoryAt = 2000;
        dp.Name = $"P2二运光球爆炸范围{idStr}";

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P2 光球爆炸范围移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25295"],
        userControl: false)]
    public void P2_光球爆炸范围移除(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        accessory.Method.RemoveDraw($"P2二运光球爆炸范围{@event["SourceId"]}");
    }

    [ScriptMethod(name: "P2 二运陨石记录", eventType: EventTypeEnum.TargetIcon, userControl: false)]
    public void P2_2运陨石记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        if (ParsTargetIcon(@event["Id"]) != -45) return;
        if (ParseObjectId(@event["TargetId"], out var id))
            p2Stone[accessory.Data.PartyList.ToList().IndexOf(id)] = true;
        var s1 = p2Stone.IndexOf(true);
        var s2 = p2Stone.LastIndexOf(true);
        //记录分组
        if (s1 != s2)
        {
            p2StoneMem = (s1, s2);
            //AB mt h2
            if (s1 == 0 && s2 == 3) p2StoneTeam = [0, 4, 5, 1, 3, 7, 6, 2];
            //AB d14
            if (s1 == 4 && s2 == 7) p2StoneTeam = [4, 0, 5, 1, 7, 3, 6, 2];
            //AC 双t
            if (s1 == 0 && s2 == 1) p2StoneTeam = [0, 4, 7, 3, 1, 5, 6, 2];
            //AC d12
            if (s1 == 4 && s2 == 5) p2StoneTeam = [4, 0, 7, 3, 5, 1, 6, 2];
            //AD mt H1
            if (s1 == 0 && s2 == 2) p2StoneTeam = [0, 4, 7, 3, 2, 6, 5, 1];
            //AD d13
            if (s1 == 4 && s2 == 6) p2StoneTeam = [4, 0, 7, 3, 6, 2, 5, 1];
            //BC h2 st
            if (s1 == 1 && s2 == 3) p2StoneTeam = [3, 7, 4, 0, 1, 5, 6, 2];
            //BC d24
            if (s1 == 5 && s2 == 7) p2StoneTeam = [7, 3, 4, 0, 5, 1, 6, 2];
            //BD h12
            if (s1 == 2 && s2 == 3) p2StoneTeam = [4, 0, 3, 7, 5, 1, 2, 6];
            //BD d34
            if (s1 == 6 && s2 == 7) p2StoneTeam = [4, 0, 7, 3, 5, 1, 6, 2];
            //CD st h1
            if (s1 == 1 && s2 == 2) p2StoneTeam = [2, 6, 7, 3, 1, 5, 4, 0];
            //CD d23
            if (s1 == 5 && s2 == 6) p2StoneTeam = [6, 2, 7, 3, 5, 1, 4, 0];
        }
    }

    [ScriptMethod(name: "P2 二运陨石连线(ImGui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25576"])]
    public void P2_2运陨石连线(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        Task.Delay(100).ContinueWith(t =>
        {
            var s1 = p2Stone.IndexOf(true);
            var s2 = p2Stone.LastIndexOf(true);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = accessory.Data.PartyList[s1];
            dp.TargetObject = accessory.Data.PartyList[s2];
            dp.DestoryAt = 12000;
            dp.Name = "P2 2运陨石双人连线(ImGui)";
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
        });
    }

    [ScriptMethod(name: "P2 二运冰分摊位置(ImGui)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25576"])]
    public void P2_2运冰分摊位置(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        Task.Delay(100).ContinueWith(t =>
        {
            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            var dir4 = p2StoneTeam.IndexOf(idIndex) / 2;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2 2运冰分摊位置(ImGui)";
            dp.Scale = new Vector2(3f, 10);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = RotatePoint(new Vector3(100, 0, 88.5f), new Vector3(100, 0, 100), float.Pi / 2 * dir4);
            dp.DestoryAt = 7000;

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        });


        //accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "P2 二运第一轮塔记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:29564"],
        userControl: false)]
    public void P2_二运第一轮塔记录(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        var sourcePos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var cpos = new Vector3(100, 0, 100);
        if ((sourcePos - cpos).Length() > 7)
        {
            var dir = (PositionTo12Dir(sourcePos, cpos) + 1) % 12;
            p2Tower[dir] = true;
        }
        else
        {
            var dir = PositionTo8Dir(sourcePos, cpos) / 2 + 12;
            p2Tower[dir] = true;
        }
    }

    [ScriptMethod(name: "P2 二运第一轮塔位置(ImGui)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:29563"])]
    public void P2_二运第一轮塔位置(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;
        Task.Delay(100).ContinueWith(t =>
        {
            List<int> towerMem = [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1];
            List<int> alternate = [];
            //高优先级
            for (var i = 0; i < 4; i++)
            {
                var MemIndex = p2StoneTeam[i * 2];
                //中
                if (p2Tower[i * 3 + 1])
                {
                    towerMem[i * 3 + 1] = MemIndex;
                    continue;
                }

                //左
                if (p2Tower[i * 3])
                {
                    towerMem[i * 3] = MemIndex;
                    continue;
                }

                //右
                if (p2Tower[i * 3 + 2]) towerMem[i * 3 + 2] = MemIndex;
            }

            //低优先级
            for (var i = 0; i < 4; i++)
            {
                var MemIndex = p2StoneTeam[i * 2 + 1];
                //左
                if (p2Tower[i * 3] && towerMem[i * 3] == -1)
                {
                    towerMem[i * 3] = MemIndex;
                    continue;
                }

                //右
                if (p2Tower[i * 3 + 2] && towerMem[i * 3 + 2] == -1)
                {
                    towerMem[i * 3 + 2] = MemIndex;
                    continue;
                }

                //内左
                if (p2Tower[i + 12] && towerMem[i + 12] == -1)
                {
                    towerMem[i + 12] = MemIndex;
                    continue;
                }

                //补塔
                alternate.Add(MemIndex);
            }

            //补塔
            foreach (var mem in alternate)
                for (var i = 12; i < 16; i++)
                    if (p2Tower[i] && towerMem[i] == -1)
                    {
                        towerMem[i] = mem;
                        break;
                    }

            var idIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            var npos = new Vector3(100, 0, 82);
            var npos2 = new Vector3(100, 0, 94);
            var cpos = new Vector3(100, 0, 100);
            var dp = accessory.Data.GetDefaultDrawProperties();
            var tIndex = towerMem.IndexOf(idIndex);
            if (tIndex >= 0 && tIndex < 12) dp.Position = RotatePoint(npos, cpos, float.Pi / 6 * (tIndex - 1));
            if (tIndex >= 12 && tIndex < 16)
                dp.Position = RotatePoint(npos2, cpos, float.Pi / 2 * (tIndex - 12) + float.Pi / 4);

            dp.Name = "P2 2运第一轮塔位置(ImGui)";
            dp.DestoryAt = 12000;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Scale = new Vector2(3);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "P2 2运第一轮塔位置(ImGui)";
            dp2.Color = accessory.Data.DefaultSafeColor;
            dp2.Owner = accessory.Data.Me;
            dp2.TargetPosition = dp.Position;
            dp2.Scale = new Vector2(3f, 10);
            dp2.ScaleMode |= ScaleMode.YByDistance;
            dp2.Delay = 7500;
            dp2.DestoryAt = 4500;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        });
    }

    [ScriptMethod(name: "P2 二运第二轮塔位置(ImGui)", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:28650"])]
    public void P2_二运第二轮塔位置(Event @event, ScriptAccessory accessory)
    {
        if (parse != 2.2) return;

        var index = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
        var posIndex = p2StoneTeam.IndexOf(index);
        if (index == p2StoneMem.Item1) posIndex = p2StoneTeam.IndexOf(p2StoneMem.Item2);
        if (index == p2StoneMem.Item2) posIndex = p2StoneTeam.IndexOf(p2StoneMem.Item1);

        var npos = new Vector3(100, 0, 82);
        var cpos = new Vector3(100, 0, 100);
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.TargetPosition = RotatePoint(npos, cpos, float.Pi / 4 * posIndex);


        dp.Name = "P2 2运第一轮塔位置(ImGui)";
        dp.DestoryAt = 11000;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Position = dp.TargetPosition;
        dp.Scale = new Vector2(3);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }

    #endregion

    [ScriptMethod(name: "P2 二运结束记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25533"],
        userControl: false)]
    public void P2_二运结束记录(Event @event, ScriptAccessory accessory)
    {
        parse = 2.3;
    }

    [ScriptMethod(name: "P2 骑神奋力一挥（右)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25536"])]
    public void P2_骑神奋力一挥_右(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;

        dp.Scale = new Vector2(40);
        dp.Radian = float.Pi / 180 * 130;
        dp.Rotation = float.Pi / 180 * -65;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        dp.Color = accessory.Data.DefaultSafeColor;
        dp.TargetColor = accessory.Data.DefaultDangerColor;
        dp.Rotation = float.Pi;
        dp.DestoryAt = 6000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "P2 骑神奋力一挥（左)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25537"])]
    public void P2_骑神奋力一挥_左(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Color = accessory.Data.DefaultDangerColor;
        if (ParseObjectId(@event["SourceId"], out var id)) dp.Owner = id;

        dp.Scale = new Vector2(40);
        dp.Radian = float.Pi / 180 * 130;
        dp.Rotation = float.Pi / 180 * 65;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        dp.Color = accessory.Data.DefaultSafeColor;
        dp.TargetColor = accessory.Data.DefaultDangerColor;
        dp.Rotation = float.Pi;
        dp.DestoryAt = 6000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    #endregion
}
