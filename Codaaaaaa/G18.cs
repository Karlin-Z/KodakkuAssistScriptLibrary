using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Newtonsoft.Json;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;

namespace Codaaaaaa.G18;

[ScriptType(
    guid: "8f2a1d6d-4d3b-4c61-8b9a-6a4b8d0c8b2a",
    name: "g18神秘剧透小脚本",
    territorys: [1279],
    version: "0.0.0.3",
    author: "Codaaaaaa",
    note: "剧透")]
public class G18
{
    #region User Settings
    [UserSetting("Debug")]
    public bool ChatOutput { get; set; } = false;
    #endregion

    private readonly List<string> _historyRound = new();

    private readonly List<RoundItem> _roundBuf = new();

    private static readonly TimeSpan RoundResetGap = TimeSpan.FromSeconds(3);

    private long _seq = 0; // 用于稳定排序（同index时按到达顺序）

    private sealed class RoundItem
    {
        public int Index { get; }
        public string Text { get; }
        public long Seq { get; }

        public RoundItem(int index, string text, long seq)
        {
            Index = index;
            Text = text;
            Seq = seq;
        }
    }

    public void Init(ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(".*");
        _historyRound.Clear();
        // _roundBuf.Clear();
        // _lastRecordTime = DateTime.MinValue;
        _seq = 0;
    }

    [ScriptMethod(name: "记录", eventType: EventTypeEnum.EnvControl, eventCondition: ["Flag:regex:^(1|2)$"], userControl: false)]
    public void 记录(Event evt, ScriptAccessory sa)
    {
        if (!TryGetInt(evt, "Index", out var index)) return;

        // 允许：0-20 + 27-31
        if (!IsValidIndex(index)) return;

        // 隔太久就清空，认为新一轮
        // var now = DateTime.UtcNow;
        // if (_lastRecordTime != DateTime.MinValue && (now - _lastRecordTime) > RoundResetGap)
        // {
        //     DebugPrint(sa, $"[G18] Round reset by gap. buf cleared. gap={(now - _lastRecordTime).TotalMilliseconds:0}ms");
        //     _roundBuf.Clear();
        // }
        // _lastRecordTime = now;

        var txt = MapIndexToText(index);

        _roundBuf.Add(new RoundItem(index, txt, ++_seq));
        DebugPrint(sa, $"[G18] Record index={index} => {txt}, bufCount={_roundBuf.Count}");

        // 收满3个就结算（按index排序）
        if (_roundBuf.Count >= 3)
        {
            var ordered = _roundBuf
                .OrderBy(x => x.Index)   // index 越大越靠后
                .ThenBy(x => x.Seq)      // 同index时按到达顺序
                .Take(3)
                .Select(x => x.Text)
                .ToList();

            var line = string.Join(" / ", ordered);
            // _historyRound.Add(line);

            DebugPrint(sa, $"[G18] Round sealed => {line}");

            // _roundBuf.Clear();
        }
    }

    [ScriptMethod(name: "变动播报", eventType: EventTypeEnum.EnvControl, eventCondition: ["Flag:regex:^(64|128|256|512|1024|2048|4096|8192|16384)$"])]
    public void 变动播报(Event evt, ScriptAccessory sa)
    {
        if (!TryGetInt(evt, "Index", out var index)) return;
        if (index >= 21 && index <= 26) return;

        DebugPrint(sa, $"变动");
        sa.Method.TTS("变动");
        sa.Method.TextInfo("变动", 5500, true);
    }

    [ScriptMethod(name: "剧透报", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["SourceName:潜网巡梦"])]
    public void 剧透报(Event evt, ScriptAccessory sa)
    {
        if (TryGetInt(evt, "Id1", out var id1) && id1 != 1) return;

        DebugPrint(sa, "[G18] 1.");
        // 1) 优先播报当前缓冲（即使没满3个也可以发）
        if (_roundBuf.Count > 0)
        {
            var ordered = _roundBuf
                .OrderBy(x => x.Index)
                .ThenBy(x => x.Seq)
                .Select(x => x.Text)
                .ToList();

            var msg = $"[G18]：{string.Join(" / ", ordered)}";
            Output(sa, msg);
            _roundBuf.Clear();
            return;
        }

        // 2) 否则播报最后一条封存历史
        // if (_historyRound.Count > 0)
        // {
        //     var last = _historyRound.Last();
        //     Output(sa, $"[G18]：{last}");
        //     return;
        // }

        DebugPrint(sa, "[G18] No records yet.");
    }

    // ---------------- Core Mapping ----------------

    private static bool IsValidIndex(int index)
        => (index >= 0 && index <= 20) || (index >= 27 && index <= 31);

    private static string MapIndexToText(int index)
    {
        // 27-31：固定映射
        if (index >= 27 && index <= 31)
        {
            return index switch
            {
                27 => "心碎",
                28 => "终钻",
                29 => "三钻",
                30 => "终钻",
                31 => "宝箱",
                _ => "?"
            };
        }

        // 0-20：循环规律
        var m = index % 7;
        return m switch
        {
            0 => "心碎",
            1 => "小钻",
            2 => "三钻",
            3 => "终钻",
            4 => "宝箱",
            5 => "彩",
            6 => "小钻",
            _ => "?"
        };
    }

    // ---------------- Output Helpers ----------------

    private void Output(ScriptAccessory sa, string msg)
    {
        if (ChatOutput)
            DebugPrint(sa, msg);

        sa.Method.TextInfo(msg, 3000, false);
    }

    private void DebugPrint(ScriptAccessory sa, string msg)
    {
        if (ChatOutput)
            sa.Method.SendChat($"/e {msg}");
    }

    private static bool TryGetInt(Event evt, string key, out int value)
    {
        value = 0;
        try
        {
            var s = evt[key];
            if (string.IsNullOrEmpty(s)) return false;

            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(s.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value);

            return int.TryParse(s, out value);
        }
        catch
        {
            return false;
        }
    }
}

#region Helpers

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
        catch
        {
            return false;
        }
    }

    public static uint ActionId(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["ActionId"]);
    public static uint SourceId(this Event evt) => ParseHexId(evt["SourceId"], out var id) ? id : 0;
    public static uint TargetId(this Event evt) => ParseHexId(evt["TargetId"], out var id) ? id : 0;
    public static Vector3 SourcePosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["SourcePosition"]);
    public static Vector3 EffectPosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["EffectPosition"]);
    public static uint DirectorId(this Event evt) => ParseHexId(evt["DirectorId"], out var id) ? id : 0;
}

public static class ScriptAccessoryExtensions
{
    public static int MyIndex(this ScriptAccessory sa) => sa.Data.PartyList.IndexOf(sa.Data.Me);

    public static DrawPropertiesEdit FastDp(this ScriptAccessory sa, string name, Vector3 pos, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.DestoryAt = duration;
        dp.Scale = scale;
        return dp;
    }

    public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, Vector3 pos, uint duration, uint delay = 0, string name = "Waypoint")
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.TargetPosition = pos;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }
}

#endregion
