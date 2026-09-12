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

[ScriptType(name: "M7sPiPi", territorys: [1261], guid: "6ac641b7-0c94-1e86-803b-b020f772abcc", version: "0.0.0.1",
    author: "Karlin")]
public class M7sPiPi
{
    private uint[] flowers = [];
    private int parse;

    public void Init(ScriptAccessory accessory)
    {
        parse = 1;
        flowers = [0, 0];
    }

    [ScriptMethod(name: "P1_MT拉花", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:18308"])]
    public void P1_MT拉花(Event @event, ScriptAccessory accessory)
    {
        if (parse != 1) return;
        if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) != 0) return;
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        if (pos == default) return;
        if (pos.X < 90)
            Task.Delay(2000).ContinueWith(t =>
            {
                accessory.Method.SendChat(
                    $"/PiPiPlugin Command DoAction {{\"Id\":3624,\"Target\":0x{@event.SourceId:X},\"Delay\":0,\"Force\":false}}");
            });
        if (pos.Z < 90)
            accessory.Method.SendChat(
                $"/PiPiPlugin Command DoAction {{\"Id\":3624,\"Target\":0x{@event.SourceId:X},\"Delay\":0,\"Force\":false}}");
    }

    [ScriptMethod(name: "P2 阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42364"],
        userControl: false)]
    public void P2_PhaseChange(Event @event, ScriptAccessory accessory)
    {
        parse = 2;
    }

    [ScriptMethod(name: "P3 阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42398"],
        userControl: false)]
    public void P3_PhaseChange(Event @event, ScriptAccessory accessory)
    {
        parse = 3;
    }

    [ScriptMethod(name: "P3_MT拉花", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:18308"])]
    public void P3_MT拉花(Event @event, ScriptAccessory accessory)
    {
        if (parse != 3) return;
        if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) != 0) return;
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        if (pos == default) return;
        if (pos.X < 90)
            Task.Delay(2000).ContinueWith(t =>
            {
                accessory.Method.SendChat(
                    $"/PiPiPlugin Command DoAction {{\"Id\":3624,\"Target\":0x{@event.SourceId:X},\"Delay\":0,\"Force\":false}}");
            });
        if (pos.Z < -5)
            accessory.Method.SendChat(
                $"/PiPiPlugin Command DoAction {{\"Id\":3624,\"Target\":0x{@event.SourceId:X},\"Delay\":0,\"Force\":false}}");
    }

    [ScriptMethod(name: "P3 小怪阶段开减伤", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42395"],
        suppress: 1000)]
    public void P3_小怪阶段开减伤(Event @event, ScriptAccessory accessory)
    {
        if (parse != 3) return;
        if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) != 0) return;
        accessory.Method.SendChat(
            "/PiPiPlugin Command DoAction {\"Id\":7531,\"Target\":0xE0000001,\"Delay\":0,\"Force\":false}");
        Task.Delay(5000).ContinueWith(t =>
        {
            accessory.Method.SendChat(
                "/PiPiPlugin Command DoAction {\"Id\":36927,\"Target\":0xE0000001,\"Delay\":0,\"Force\":false}");
        });
    }


    private static bool ParseObjectId(string? idStr, out uint id)
    {
        id = 0;
        if (string.IsNullOrEmpty(idStr)) return false;
        try
        {
            var idStr2 = idStr.Replace("0x", "");
            id = uint.Parse(idStr2, NumberStyles.HexNumber);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }


    private int FloorToIndex(Vector3 pos)
    {
        var centre = new Vector3(100, 0, 100);
        var dv = pos - centre;
        var index = 0;
        if (dv.X > 0)
        {
            if (dv.Z > 0)
                index = 3;
            else
                index = 0;
        }
        else
        {
            if (dv.Z > 0)
                index = 2;
            else
                index = 1;
        }

        return index;
    }

    private Vector3 IndexToFloor(int index)
    {
        switch (index)
        {
            case 0: return new Vector3(105, 0, 95);
            case 1: return new Vector3(95, 0, 95);
            case 2: return new Vector3(95, 0, 105);
            case 3: return new Vector3(105, 0, 105);
        }

        return default;
    }

    private Vector3 RotatePoint(Vector3 point, Vector3 centre, float radian)
    {
        Vector2 v2 = new(point.X - centre.X, point.Z - centre.Z);

        var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
        var lenth = v2.Length();
        return new Vector3(centre.X + MathF.Sin(rot) * lenth, centre.Y, centre.Z - MathF.Cos(rot) * lenth);
    }
}