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

[ScriptType(name: "M8s绘图", territorys: [1263], guid: "50723590-fd25-5934-36ce-5f7e35097cb6", version: "0.0.0.4",
    author: "Karlin", note: noteStr, updateInfo: updateInfoStr)]
public class M8sDraw
{
    private const string noteStr =
        """

        """;

    private const string updateInfoStr =
        """

        """;

    private int ballCount;
    private ulong bossId = 0;
    private ulong greenWolfId;


    private int parse;
    private int[] wolfBallBuff = [0, 0, 0, 0, 0, 0, 0, 0];
    private int[] wolfDefenceYellow = [0, 0, 0, 0, 0, 0, 0, 0];
    private ulong yellowWolfId;

    public void Init(ScriptAccessory accessory)
    {
        parse = 0;
        wolfDefenceYellow = [0, 0, 0, 0, 0, 0, 0, 0];
        wolfBallBuff = [0, 0, 0, 0, 0, 0, 0, 0];
        ballCount = 0;
    }


    [ScriptMethod(name: "双狼 防火墙记录", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(4390|4389)$"], userControl: false)]
    public void 双狼_防火墙记录(Event @event, ScriptAccessory accessory)
    {
        var yellow = @event.StatusId == 4390;
        var tindex = accessory.Data.PartyList.IndexOf((uint)@event.TargetId);
        if (tindex == -1) return;
        wolfDefenceYellow[tindex] = yellow ? 1 : 0;
    }

    [ScriptMethod(name: "双狼 撞球buff记录", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(439[12])$"], userControl: false)]
    public void 双狼_撞球buff记录(Event @event, ScriptAccessory accessory)
    {
        var yellow = @event.StatusId == 4391;
        var tindex = accessory.Data.PartyList.IndexOf((uint)@event.TargetId);
        if (tindex == -1) return;
        var id = yellow ? 4 : 0;
        if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
        if (dur > 2000 && dur < 3000) id += 1;
        if (dur > 3000 && dur < 4000) id += 2;
        if (dur > 5000 && dur < 6000) id += 3;
        wolfBallBuff[tindex] = id;
    }

    [ScriptMethod(name: "双狼 狼头Id记录", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:regex:^(18219|18225])$"], userControl: false)]
    public void 双狼_狼头Id记录(Event @event, ScriptAccessory accessory)
    {
        if (@event["DataId"] == "18219")
            greenWolfId = @event.SourceId;
        else
            yellowWolfId = @event.SourceId;
    }

    [ScriptMethod(name: "双狼 风头出现选中", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:18219"])]
    public void 双狼_风头出现选中(Event @event, ScriptAccessory accessory)
    {
        var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        if (myindex == -1) return;
        if (wolfDefenceYellow[myindex] == 1)
            Task.Delay(50).ContinueWith(t => { accessory.Method.SelectTarget((uint)@event.SourceId); });
    }

    [ScriptMethod(name: "双狼 土头出现选中", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:18225"])]
    public void 双狼_土头出现选中(Event @event, ScriptAccessory accessory)
    {
        var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        if (myindex == -1) return;
        if (wolfDefenceYellow[myindex] == 0)
            Task.Delay(50).ContinueWith(t => { accessory.Method.SelectTarget((uint)@event.SourceId); });
    }

    [ScriptMethod(name: "双狼 防火墙切换选中狼头", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(4390|4389)$"])]
    public void 双狼_防火墙切换选中狼头(Event @event, ScriptAccessory accessory)
    {
        var yellow = @event.StatusId == 4390;
        if (@event.TargetId != accessory.Data.Me) return;
        var selectId = yellow ? greenWolfId : yellowWolfId;

        Task.Delay(50).ContinueWith(t => { accessory.Method.SelectTarget((uint)selectId); });
    }

    [ScriptMethod(name: "双狼 风buff撞球提醒", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:41932"],
        suppress: 1000)]
    public void 双狼_风buff撞球提醒(Event @event, ScriptAccessory accessory)
    {
        ballCount++;
        accessory.Log.Debug($"绿{ballCount}撞球");
        var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        var isme = false;
        //绿小黄大
        if (ballCount == wolfBallBuff[myindex]) isme = true;
        if (isme)
        {
            accessory.Method.TTS("撞球");
            accessory.Method.TextInfo("撞球", 2000);
        }
    }

    [ScriptMethod(name: "双狼 土buff撞球提醒", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:41965"],
        suppress: 1000)]
    public void 双狼_土buff撞球提醒(Event @event, ScriptAccessory accessory)
    {
        accessory.Log.Debug($"土{ballCount}撞球");
        var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        var isme = false;
        //绿小黄大
        if (ballCount == wolfBallBuff[myindex] + 4) isme = true;
        if (isme)
            Task.Delay(4000).ContinueWith(t =>
            {
                accessory.Method.TTS("撞球");
                accessory.Method.TextInfo("撞球", 2000);
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