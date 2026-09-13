using System;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using System.Reflection.Metadata;
using System.Net;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using System.Runtime.Intrinsics.Arm;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using FFXIVClientStructs;
using System;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Veever.DawnTrail.Cenote_Ja_Ja_Gural;

[ScriptType(name: "加加财富天坑", territorys: [1209], guid: "64620549-e269-49c0-b0ae-76edec024864",
    version: "0.0.0.4", author: "Veever", note: noteStr)]

public class Cenote_Ja_Ja_Gural
{
    const string noteStr =
    """
    v0.0.0.4:
    1. 加加财富天坑只根据自己下底的arr进行绘制，没有后面天箭绘画的情况
    2. 如果你有此方面的素材可以dc私信提供给我，我会加入到绘制里
    鸭门。
    """;

    [UserSetting("Debug开关, 非开发用请关闭")]
    public bool isDebug { get; set; } = false;

    public int AetherialLightCount;

    private readonly object AetherialLightLock = new object();

    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        AetherialLightCount = 0;
    }

    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    [ScriptMethod(name: "保护色", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3997"])]
    public void Crypsis(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "保护色";
        dp.Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(5f);
        dp.InnerScale = new Vector2(4.9f);
        dp.DestoryAt = 10000;
        dp.Radian = 2 * float.Pi;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
    }

    [ScriptMethod(name: "黄金飞溅", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:38267"])]
    public void GoldenGall(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "黄金飞溅";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(40f);
        dp.Radian = float.Pi;
        dp.DestoryAt = 6700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "以太光", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:38271"])]
    public async void AetherialLight(Event @event, ScriptAccessory accessory)
    {
        lock (AetherialLightLock)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"以太光";
            dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            dp.Owner = @event.SourceId();
            dp.Scale = new Vector2(40f);
            dp.Radian = float.Pi / 180 * 60;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = $"以太光安全";
            dp1.Color = accessory.Data.DefaultSafeColor;
            dp1.Owner = @event.SourceId();
            dp1.Scale = new Vector2(40f);
            dp1.Radian = float.Pi / 180 * 60;
            dp1.Delay = 5000;
            dp1.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp1);
        }
        if (AetherialLightCount >= 5)
        {
            await Task.Delay(5000);
            accessory.Method.RemoveDraw(".*");
        }
        DebugMsg($"{AetherialLightCount}", accessory);
        AetherialLightCount++;
    }

    [ScriptMethod(name: "火焰剑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^3825[15]$"])]
    public void FlameBlade(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Flame Blade";
        dp.Owner = @event.SourceId();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Scale = new(5f, 40f);
        dp.DestoryAt = 2200;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
    }
}

public static class EventExtensions
{
    private static bool ParseHexId(string? idStr, out uint id)
    {
        id = 0;
        if (string.IsNullOrEmpty(idStr)) return false;
        try
        {
            var idStr2 = idStr.Replace("0x", "");
            id = uint.Parse(idStr2, System.Globalization.NumberStyles.HexNumber);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static uint Id(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["Id"]);
    }

    public static uint ActionId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
    }

    public static uint SourceId(this Event @event)
    {
        return ParseHexId(@event["SourceId"], out var id) ? id : 0;
    }

    public static uint SourceDataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["SourceDataId"]);
    }

    public static uint DataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DataId"]);
    }

    public static uint Command(this Event @event)
    {
        return ParseHexId(@event["Command"], out var cid) ? cid : 0;
    }

    public static string DurationMilliseconds(this Event @event)
    {
        return JsonConvert.DeserializeObject<string>(@event["DurationMilliseconds"]) ?? string.Empty;
    }

    public static float SourceRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
    }

    public static float TargetRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["TargetRotation"]);
    }

    public static byte Index(this Event @event)
    {
        return (byte)(ParseHexId(@event["Index"], out var index) ? index : 0);
    }

    public static uint State(this Event @event)
    {
        return ParseHexId(@event["State"], out var state) ? state : 0;
    }

    public static string SourceName(this Event @event)
    {
        return @event["SourceName"];
    }

    public static string TargetName(this Event @event)
    {
        return @event["TargetName"];
    }

    public static uint TargetId(this Event @event)
    {
        return ParseHexId(@event["TargetId"], out var id) ? id : 0;
    }

    public static Vector3 SourcePosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    public static Vector3 TargetPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
    }

    public static Vector3 EffectPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
    }

    public static uint DirectorId(this Event @event)
    {
        return ParseHexId(@event["DirectorId"], out var id) ? id : 0;
    }

    public static uint StatusId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StatusId"]);
    }

    public static uint StackCount(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StackCount"]);
    }

    public static uint Param(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["Param"]);
    }

    public static string Operate(this Event @event)
    {
        return @event["Operate"];
    }
}


public static class Extensions
{
    public static void TTS(this ScriptAccessory accessory, string text, bool isTTS, bool isDRTTS)
    {
        if (isDRTTS)
        {
            accessory.Method.SendChat($"/pdr tts {text}");
        }
        else if (isTTS)
        {
            accessory.Method.TTS(text);
        }
    }
}

public static class IbcHelper
{
    public static IBattleChara? GetById(ScriptAccessory accessory, uint id)
    {
        return (IBattleChara?)accessory.Data.Objects.SearchByEntityId(id);
    }

    public static IBattleChara? GetMe(ScriptAccessory accessory)
    {
        return accessory.Data.Objects.SearchByEntityId(accessory.Data.Me) as IBattleChara;
    }

    public static KodakkuAssist.Data.IGameObject? GetFirstByDataId(ScriptAccessory accessory, uint dataId)
    {
        return accessory.Data.Objects.Where(x => x.DataId == dataId).FirstOrDefault();
    }

    public static IEnumerable<KodakkuAssist.Data.IGameObject> GetByDataId(ScriptAccessory accessory, uint dataId)
    {
        return accessory.Data.Objects.Where(x => x.DataId == dataId);
    }
}

public static unsafe class IBattleCharaExtensions
{
    public static bool HasStatus(this IBattleChara ibc, uint statusId, float remaining = -1)
    {
        return ibc.StatusList.Any(s => s.StatusId == statusId && s.RemainingTime > remaining);
    }

    public static unsafe uint Tethering(this IBattleChara ibc, int index = 0)
    {
        return ((BattleChara*)ibc.Address)->Vfx.Tethers[index].TargetId.ObjectId;
    }

}

