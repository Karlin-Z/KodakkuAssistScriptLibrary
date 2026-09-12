using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw;
using Dalamud.Utility.Numerics;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using Dalamud.Memory.Exceptions;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using KodakkuAssist.Module.GameOperate;
using System.Security.Cryptography;
using System.ComponentModel;
using System.DirectoryServices.ActiveDirectory;
using System.Collections;
using System.Text;
using System.Threading;
using System.Globalization;

namespace KarlinScriptNamespace;

[ScriptType(name: "M6sPiPi", territorys: [1259], guid: "7adfd63a-2c0a-12ef-689f-5cb8301ca3b8", version: "0.0.0.1",
    author: "Karlin")]
public class M6sPiPi
{
    public enum zoo2FishEnum
    {
        None,
        Left,
        Right
    }

    private List<int> StickyMousseTarget = [];

    private int parse;
    private CancellationTokenSource sandChargeToken = new();

    [UserSetting("动物园第二轮引导鱼")] public zoo2FishEnum zoo2Fish { get; set; }

    public void Init(ScriptAccessory accessory)
    {
        parse = 0;
        sandChargeToken.Cancel();
    }

    [ScriptMethod(name: "P1A 双手涂鸦突进", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(426(39|40))$"], suppress: 1000)]
    public void 双手涂鸦突进(Event @event, ScriptAccessory accessory)
    {
        if (accessory.Data.Objects.LocalPlayer?.ClassJob.RowId == 32)
            accessory.Method.HttpPost("http://127.0.0.1:9909/DoAction",
                "{\"Id\":36926,\"Target\":0xE0000001,\"Delay\":0,\"Force\":true}");
    }


    [ScriptMethod(name: "P2 沙漠 阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42600"],
        userControl: false)]
    public void P2_PhaseChange(Event @event, ScriptAccessory accessory)
    {
        parse = 21;
    }

    [ScriptMethod(name: "P2 沙漠 大圈结束突进", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:4454"])]
    public void P2_沙漠_大圈结束突进(Event @event, ScriptAccessory accessory)
    {
        if (parse != 21) return;
        if (@event.TargetId != accessory.Data.Me) return;
        if (accessory.Data.Objects.LocalPlayer?.ClassJob.RowId == 32)
            accessory.Method.HttpPost("http://127.0.0.1:9909/DoAction",
                "{\"Id\":36926,\"Target\":0xE0000001,\"Delay\":0,\"Force\":true}");
    }

    [ScriptMethod(name: "P2 沙漠_第一轮大圈传送", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:4454"])]
    public void P2_沙漠_第一轮大圈传送(Event @event, ScriptAccessory accessory)
    {
        if (parse != 21) return;
        if (@event.TargetId != accessory.Data.Me) return;
        var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        if (myindex != 0) return;

        Vector3 pos = new(112, 0, 105);
        var PosStr = JsonConvert.SerializeObject(pos);
        sandChargeToken = new CancellationTokenSource();
        ThreadPool.QueueUserWorkItem(async c =>
        {
            try
            {
                await Task.Delay(42500, sandChargeToken.Token);
                accessory.Method.SendChat($"/PiPiPlugin TP {PosStr}");
            }
            catch (OperationCanceledException)
            {
            }
        });
    }


    [ScriptMethod(name: "P3 动物园 阶段转换", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(4266[12345])$"], userControl: false)]
    public void P3_PhaseChange(Event @event, ScriptAccessory accessory)
    {
        parse = (int)@event.ActionId - 42661 + 30;
    }

    [ScriptMethod(name: "P3 动物园 第一轮关爆发", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(42662)$"])]
    public void P3_动物园_第一轮关爆发(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.SendChat("/PiPiPlugin BurstOff");
        if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) == 0)
        {
            Vector3 pos = new(110, 0, 100);
            accessory.Method.SendChat($"/PiPiPlugin Move {pos}");
        }
    }

    [ScriptMethod(name: "P3 动物园 第一轮MT选中老鼠", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:18343"])]
    public void P3_动物园_第一轮MT选中老鼠(Event @event, ScriptAccessory accessory)
    {
        if (parse != 31) return;
        if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) != 0) return;
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        if (pos.X < 107)
        {
            accessory.Method.SelectTarget((uint)@event.SourceId);
            //暗影波动
            accessory.Method.SendChat(
                "/PiPiPlugin Command DoAction {\"Id\":16469,\"Target\":0xE0000001,\"Delay\":0.1,\"Force\":false}");
        }
    }

    [ScriptMethod(name: "P3 动物园 第二轮MT减伤", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:18343"], suppress: 1000)]
    public void P3_动物园_第二轮MT减伤(Event @event, ScriptAccessory accessory)
    {
        if (parse != 32) return;
        if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) != 0) return;
        //铁壁
        accessory.Method.SendChat(
            "/PiPiPlugin Command DoAction {\"Id\":7531,\"Target\":0xE0000001,\"Delay\":0,\"Force\":false}");
    }

    [ScriptMethod(name: "P3 动物园 第二轮MT选中鱼", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:18346"])]
    public void P3_动物园_第二轮MT选中鱼(Event @event, ScriptAccessory accessory)
    {
        if (parse != 32) return;
        if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) != 0) return;
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        if (pos == default) return;
        if (pos.X > 100)
            Task.Delay(2000).ContinueWith(t => { accessory.Method.SelectTarget((uint)@event.SourceId); });
    }

    [ScriptMethod(name: "P3 动物园 第二轮关群攻", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(42663)$"])]
    public void P3_动物园_第二轮关群攻(Event @event, ScriptAccessory accessory)
    {
        var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        if (myindex == 0) return;
        accessory.Method.SendChat("/PiPiPlugin AttackMode NoAoe");
    }

    [ScriptMethod(name: "P3 动物园 第二轮鱼出现开爆发", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:18346"])]
    public void P3_动物园_第二轮鱼出现开爆发(Event @event, ScriptAccessory accessory)
    {
        if (parse != 32) return;
        Task.Delay(1000).ContinueWith(t =>
        {
            accessory.Method.SendChat("/PiPiPlugin AttackMode Normal");
            accessory.Method.SendChat("/PiPiPlugin BurstOn");
        });
    }

    [ScriptMethod(name: "P3 动物园 第三轮马出现单点", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:18345"])]
    public void P3_动物园_第三轮选中马(Event @event, ScriptAccessory accessory)
    {
        if (parse != 33) return;
        if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) == 1) return;
        accessory.Method.SelectTarget((uint)@event.SourceId);
        accessory.Method.SendChat("/PiPiPlugin AttackMode NoAoe");
    }

    [ScriptMethod(name: "P3 动物园 第四轮马出现单点", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:18345"])]
    public void P3_动物园_第四轮选中马(Event @event, ScriptAccessory accessory)
    {
        if (parse != 34) return;
        if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) == 0) return;
        accessory.Method.SelectTarget((uint)@event.SourceId);
        accessory.Method.SendChat("/PiPiPlugin AttackMode NoAoe");
    }

    [ScriptMethod(name: "P3 动物园 马死正常打", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:3625"])]
    public void P3_动物园_马死正常打(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.SendChat("/PiPiPlugin AttackMode Normal");
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