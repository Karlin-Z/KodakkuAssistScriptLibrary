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

[ScriptType(name: "M7s绘图", territorys: [1261], guid: "37ea4922-dee4-b998-f23f-e2a1cd1b1bcd", version: "0.0.0.5",
    author: "Karlin", note: noteStr, updateInfo: updateInfoStr)]
public class M7sDraw
{
    public enum zoo2FishEnum
    {
        None,
        Left,
        Right
    }

    private const string noteStr =
        """

        """;

    private const string updateInfoStr =
        """

        """;

    private ulong bossId;


    private int parse;

    public void Init(ScriptAccessory accessory)
    {
        parse = 0;
    }

    [ScriptMethod(name: "奶妈分摊 分摊范围", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:00A1"])]
    public void 奶妈分摊分摊范围(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "奶妈分摊 分摊范围";
        dp.Scale = new Vector2(6);
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.TargetId;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "BossId记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:42330"])]
    public void BossId记录(Event @event, ScriptAccessory accessory)
    {
        bossId = @event.SourceId;
    }

    [ScriptMethod(name: "奶妈分摊 八方站位预指", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:00A1"])]
    public void 奶妈分摊八方站位预指(Event @event, ScriptAccessory accessory)
    {
        var tpos = @event.TargetPosition;
        var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        var bossPos = accessory.Data.Objects.SearchById(bossId)?.Position ?? Vector3.Zero;
        var drot = myindex switch
        {
            1 => 4,
            2 => 2,
            3 => 6,
            4 => 3,
            5 => 5,
            6 => 1,
            7 => 7,
            _ => 0
        };
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "奶妈分摊 八方站位预指";
        dp.Owner = @event.TargetId;
        dp.TargetPosition = bossPos;
        dp.Rotation = float.Pi + float.Pi / 4 * drot;
        dp.Scale = new Vector2(2, 8);
        dp.DestoryAt = 5200;
        dp.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "奶妈分摊 八方站位", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42362"])]
    public void 奶妈分摊八方站位(Event @event, ScriptAccessory accessory)
    {
        var spos = @event.SourcePosition;
        var srot = @event.SourceRotation;
        var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        var drot = myindex switch
        {
            1 => 4,
            2 => 2,
            3 => 6,
            4 => 3,
            5 => 5,
            6 => 1,
            7 => 7,
            _ => 0
        };
        Vector3 tpos = new(spos.X + MathF.Sin(srot + float.Pi / 4 * drot) * 8, spos.Y,
            spos.Z + MathF.Cos(srot + float.Pi / 4 * drot) * 8);
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "奶妈分摊 八方站位";
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = tpos;
        dp.Scale = new Vector2(2);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.DestoryAt = 2000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "奶妈分摊 中心危险区", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42362"])]
    public void 奶妈分摊中心危险区(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "奶妈分摊 中心危险区";
        dp.Owner = @event.SourceId;
        dp.Scale = new Vector2(6);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.DestoryAt = 2000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P3花粉安全区指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42347"])]
    public void P3花粉安全区指路(Event @event, ScriptAccessory accessory)
    {
        var pos = @event.SourcePosition;
        if (pos.Y > -190) return;
        if (pos.Z > -5) return;
        var dur = 4000;
        var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        if (pos.X > 115)
        {
            if (myindex == 6 || myindex == 2)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "M7s P3花粉安全区指路";
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = new Vector3(83.5f, -200, -11.5f);
                dp.Scale = new Vector2(3);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.DestoryAt = dur;
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }

            if (myindex == 7 || myindex == 3)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "M7s P3花粉安全区指路";
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = new Vector3(116.5f, -200, 22.0f);
                dp.Scale = new Vector2(3);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.DestoryAt = dur;
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }

        if (pos.X < 85)
        {
            if (myindex == 6 || myindex == 2)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "M7s P3花粉安全区指路";
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = new Vector3(116.5f, -200, -11.5f);
                dp.Scale = new Vector2(3);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.DestoryAt = dur;
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }

            if (myindex == 7 || myindex == 3)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "M7s P3花粉安全区指路";
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = new Vector3(83.5f, -200, 22.0f);
                dp.Scale = new Vector2(3);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.DestoryAt = dur;
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }
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