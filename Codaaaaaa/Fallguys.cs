using System;
using System.Numerics;
using System.Collections.Generic;
using Newtonsoft.Json;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Extensions;

namespace Codaaaaaa.Fallguys;

[ScriptType(
    guid: "c706caf6-827b-4880-b212-fb4e6931aab3",
    name: Name,
    territorys: [1165],
    version: Version,
    author: "Codaaaaaa",
    note: Note,
    updateInfo: UpdateInfo)]
public class Fallguys
{
    #region 元信息
    private const string Name = "糖豆人简易画图";
    private const string Version = "0.0.0.2";

    private const string Note =
        $"""
        v{Version}
        糖豆人简易画图，画着玩。
        """;

    private const string UpdateInfo =
        $"""
        {Version}
        初版
        """;
    #endregion

    #region 调试
    private const bool Debugging = false;
    private static uint GuidanceOwner(ScriptAccessory sa, int idx)
        => idx >= 0 && idx < sa.Data.PartyList.Count ? sa.Data.PartyList[idx] : sa.Data.Me;
    #endregion

    #region 机关预测
    private sealed class 机关记录
    {
        public readonly List<long> 判定 = [];
        public readonly List<Vector3> 位置 = [];
        public int 序号;
        public int 画到;
        public long 画到时刻;
        public Vector3? 画到位置;
        public long 预测到;
    }

    private const int 移动机关预测提前Ms = 1000;
    private const int 原地AOE预测提前Ms = 2000;

    private const long 连打接续上限Ms = 1000;
    private const int 位置记录上限 = 32;
    private const float 位置容差 = 1;

    private readonly Dictionary<uint, 机关记录> _机关记录 = [];
    private readonly Dictionary<uint, List<(Vector3 起点, Vector3 终点)>> _冲锋路线 = [];
    private readonly object _机关锁 = new();

    public void Init(ScriptAccessory sa)
    {
        lock (_机关锁)
        {
            _机关记录.Clear();
            _冲锋路线.Clear();
            _方块.Clear();
        }
        sa.Method.RemoveDraw(".*");
    }

    private List<(int 序号, long 等待, Vector3? 位置, long 接续)> 记录并预测后续判定(uint sid, int 周期次数, long 默认周期Ms, Vector3? 这次位置)
    {
        var now = Environment.TickCount64;
        var 结果 = new List<(int 序号, long 等待, Vector3? 位置, long 接续)>();
        lock (_机关锁)
        {
            if (!_机关记录.TryGetValue(sid, out var r)) _机关记录[sid] = r = new();
            var n = ++r.序号;
            var t = r.判定;
            t.Add(now);
            if (t.Count > 周期次数 + 1) t.RemoveAt(0);

            int? 位置循环 = null;
            if (这次位置 is { } pos)
            {
                记一次位置(r.位置, pos);
                位置循环 = 找位置循环(r.位置);
            }

            var 周期 = t.Count == 周期次数 + 1 ? now - t[0]
                : 默认周期Ms > 0 ? 默认周期Ms
                : 周期次数 == 2 && t.Count == 2 ? 2 * (now - t[0])
                : 0;
            r.预测到 = 0;
            if (周期 <= 0 || 这次位置 is not null && 位置循环 is null) return 结果;

            var 首个序号 = n - (t.Count - 1);   // t[0] 是第几下
            for (var s = Math.Max(r.画到 + 1, n + 1); s <= n + 周期次数; s++)
            {
                var i = s - 周期次数 - 首个序号;
                if (i < 0) continue;
                var 时刻 = t[i] + 周期;
                if (时刻 - now < 200) continue;   // 漏收过判定会算出离谱的值，跳过，之后自动对上
                Vector3? 位置 = null;
                if (位置循环 is { } p)
                {
                    // 往前推整数个位置循环，落到已记录的那一下
                    var d = s - n;
                    位置 = r.位置[^(1 + (d + p - 1) / p * p - d)];
                }
                var 接续 = r.画到 == s - 1 && 时刻 > r.画到时刻 && 时刻 - r.画到时刻 <= 连打接续上限Ms && 同一位置(位置, r.画到位置)
                    ? 时刻 - r.画到时刻 : 0;
                结果.Add((s, 时刻 - now, 位置, 接续));
                (r.画到, r.画到时刻, r.画到位置) = (s, 时刻, 位置);
            }

            // 下一下已经画了预测时记下它的时刻，读条画图据此不再重复画
            var 下一下 = n + 1 - 周期次数 - 首个序号;
            if (r.画到 > n && 下一下 >= 0) r.预测到 = t[下一下] + 周期;
            return 结果;
        }
    }

    private long? 记录并预测下次判定(uint sid, int 周期次数, long 默认周期Ms)
    {
        var now = Environment.TickCount64;
        lock (_机关锁)
        {
            if (!_机关记录.TryGetValue(sid, out var r)) _机关记录[sid] = r = new();
            var t = r.判定;
            t.Add(now);
            if (t.Count > 周期次数 + 1) t.RemoveAt(0);

            long 下次 = 0;
            if (t.Count == 周期次数 + 1) 下次 = t[1] + (now - t[0]);
            else if (t.Count == 周期次数 && 默认周期Ms > 0) 下次 = t[0] + 默认周期Ms;
            else if (t.Count == 2 && 周期次数 == 2) 下次 = 2 * now - t[0];

            // 漏收过判定会算出离谱的值，跳过，之后自动对上
            r.预测到 = 下次 - now >= 200 ? 下次 : 0;
            return r.预测到 != 0 ? 下次 - now : null;
        }
    }

    private static void 记一次位置(List<Vector3> ps, Vector3 pos)
    {
        ps.Add(pos);
        if (ps.Count > 位置记录上限) ps.RemoveAt(0);
    }

    /// <summary>找最小的循环 p：最近几次（最多 p+1 次）都满足 第 i 次 ≈ 第 i-p 次，下一次就和 p 次前那次一样；看不出循环返回 null</summary>
    private static int? 找循环<T>(List<T> xs, Func<T, T, bool> 相同)
    {
        var n = xs.Count;
        for (var p = 1; p < n; p++)
        {
            var 核对次数 = Math.Min(n - p, p + 1);
            var 对上 = true;
            for (var i = n - 1; i >= n - 核对次数 && 对上; i--)
                对上 = 相同(xs[i], xs[i - p]);
            if (对上) return p;
        }
        return null;
    }

    private static int? 找位置循环(List<Vector3> ps) => 找循环(ps, (a, b) => Vector3.Distance(a, b) < 位置容差);

    private static Vector3? 按位置循环预测(List<Vector3> ps, Vector3 pos, bool 一次就当原地 = false)
    {
        记一次位置(ps, pos);
        if (ps.Count == 1) return 一次就当原地 ? pos : null;
        return 找位置循环(ps) is { } p ? ps[^p] : null;
    }

    private static bool 同一位置(Vector3? a, Vector3? b)
        => a is { } x ? b is { } y && Vector3.Distance(x, y) < 位置容差 : b is null;

    private static Vector3 判定位置(Event evt)
    {
        var pos = evt.EffectPosition();
        return pos.LengthSquared() > 0.01f ? pos : evt.SourcePosition();
    }

    private bool 下次判定已预测(uint sid)
    {
        lock (_机关锁)
            return _机关记录.TryGetValue(sid, out var r) && r.预测到 > Environment.TickCount64;
    }

    private static DrawPropertiesEdit 计时Dp(ScriptAccessory sa, string name, Vector2 尺寸, long 出现, long 持续)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Scale = 尺寸;
        dp.Delay = (uint)出现;
        dp.DestoryAt = (uint)持续;
        return dp;
    }

    private static DrawPropertiesEdit? 预测Dp(ScriptAccessory sa, string name, long 等待, int 提前Ms, Vector2 尺寸)
    {
        var 提前 = Math.Clamp(提前Ms, 0, 等待);
        if (提前 == 0) return null;
        var dp = 计时Dp(sa, name, 尺寸, 等待 - 提前, 提前);
        dp.ScaleMode |= ScaleMode.ByTime;
        return dp;
    }

    private static DrawPropertiesEdit 接续Dp(ScriptAccessory sa, string name, long 等待, long 接续, Vector2 尺寸)
        => 计时Dp(sa, name, 尺寸, Math.Max(0, 等待 - 接续), 接续);

    private void 预测机关下次判定(Event evt, ScriptAccessory sa, DrawTypeEnum 形状, Vector2 尺寸, int 提前Ms,
        int 周期次数 = 2, long 默认周期Ms = 0, Vector3? 偏移 = null, bool 跟踪位置 = false)
    {
        var sid = evt.SourceId();
        var 朝向 = evt.SourceRotation();
        Vector3? 这次位置 = 跟踪位置 ? 判定位置(evt) : null;
        if (这次位置 is { } 记) sa.Log.Debug($"机关 {sid:X} 技能 {evt.ActionId()} 判定位置 {记} 朝向 {朝向:F2}");

        foreach (var (序号, 等待, 位置, 接续) in 记录并预测后续判定(sid, 周期次数, 默认周期Ms, 这次位置))
        {
            var name = $"机关预测-{sid:X}-{序号}";
            var dp = 接续 > 0 ? 接续Dp(sa, name, 等待, 接续, 尺寸) : 预测Dp(sa, name, 等待, 提前Ms, 尺寸);
            if (dp is null) continue;
            if (位置 is { } pos)
            {
                dp.Position = 偏移 is { } o ? pos - 按朝向旋转(o, 朝向) : pos;
                dp.Rotation = 朝向;
            }
            else
            {
                dp.Owner = sid;
                dp.Offset = 偏移;
            }
            sa.Method.SendDraw(DrawModeEnum.Default, 形状, dp);
        }
    }

    private static Vector3 矩形居中(float 长) => new(0, 0, 长 / 2);

    private static float 距离XZ(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));

    private static Vector3 朝向向量(float rot) => new(MathF.Sin(rot), 0, MathF.Cos(rot));

    private static Vector3 按朝向旋转(Vector3 o, float rot)
    {
        var (sin, cos) = MathF.SinCos(rot);
        return new(o.X * cos + o.Z * sin, o.Y, -o.X * sin + o.Z * cos);
    }

    [ScriptMethod(name: "机关移除清理", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:regex:^(16338|16339|16350)$"], userControl: false)]
    public void 机关移除清理(Event evt, ScriptAccessory sa)
    {
        var sid = evt.SourceId();
        lock (_机关锁)
        {
            _机关记录.Remove(sid);
            _冲锋路线.Remove(sid);
            _方块.Remove(sid);
        }
        sa.Method.RemoveDraw($"^机关预测(余波\\d+)?-{sid:X}(-\\d+)?$");
    }
    #endregion

    #region 金碟狂热
    [ScriptMethod(name: "金碟狂热 - 圆形AOE画图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:34717"])]
    public void 金碟狂热圆形AOE画图(Event evt, ScriptAccessory sa)
    {
        if (下次判定已预测(evt.SourceId())) return;
        if (!uint.TryParse(evt["DurationMilliseconds"], out var dur)) return;
        var dp = sa.FastDp($"金碟狂热圆形AOE-{evt.SourceId():X}", evt.TargetId(), dur, new Vector2(3));
        dp.ScaleMode |= ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "金碟狂热 - 圆形AOE预测画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:34717", "TargetIndex:1"])]
    public void 金碟狂热圆形AOE预测画图(Event evt, ScriptAccessory sa)
        => 预测机关下次判定(evt, sa, DrawTypeEnum.Circle, new Vector2(3), 原地AOE预测提前Ms);

    // 34716 移动挡板：瞬发，ct=12 矩形 长 3 × 宽 2，以机关为中心。12 条门道各 3 个机关（间隔 3m），EnvControl 0~11 每 2.1s 同时翻转挡板方向；
    private const long 挡板周期Ms = 4220;

    [ScriptMethod(name: "金碟狂热/攻城 - 移动挡板预测画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:34716", "TargetIndex:1"])]
    public void 移动挡板预测画图(Event evt, ScriptAccessory sa)
        => 预测机关下次判定(evt, sa, DrawTypeEnum.Rect, new Vector2(2, 3), 移动机关预测提前Ms, 默认周期Ms: 挡板周期Ms, 偏移: 矩形居中(3));

    // 34718 移动大墙：瞬发，ct=12 矩形 长 4 × 宽 10，以机关为中心。2 排各 4 个机关（间隔 4m），EnvControl 12/13 每 4.1s 翻转方向；
    private const long 大墙周期Ms = 8225;

    [ScriptMethod(name: "金碟狂热 - 移动大墙预测画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:34718", "TargetIndex:1"])]
    public void 金碟狂热移动大墙预测画图(Event evt, ScriptAccessory sa)
        => 预测机关下次判定(evt, sa, DrawTypeEnum.Rect, new Vector2(10, 4), 移动机关预测提前Ms, 默认周期Ms: 大墙周期Ms, 偏移: 矩形居中(4));
    #endregion

    #region 金碟狂欢
    // 34774 冲锋（16350 号怪物，坡上 3 只 1.60s 一次、后半段 2 只 2.10s 一次）同金碟攻城，由「金碟攻城/狂欢 - 冲锋预测画图」处理

    // 34775 移动挡板：瞬发，ct=12 矩形 3×3，以机关为中心。6 排各 3 个机关（间隔 3m，Z = -328 ~ -281），
    private const long 狂欢挡板周期Ms = 4280;

    [ScriptMethod(name: "金碟狂欢 - 移动挡板预测画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:34775", "TargetIndex:1"])]
    public void 金碟狂欢移动挡板预测画图(Event evt, ScriptAccessory sa)
        => 预测机关下次判定(evt, sa, DrawTypeEnum.Rect, new Vector2(3, 3), 移动机关预测提前Ms,
            默认周期Ms: 狂欢挡板周期Ms, 偏移: 矩形居中(3), 跟踪位置: true);

    // 34773 大圆：瞬发，ct=2 圆 r=5。13 个机关铺在 X = -10/0/10、Z = -242 ~ -202 的格子上（间隔 10m），分两组交替：
    // 每个机关间隔 0.4s 连打 8 下再停 3.3s，一轮 6.10s，两组错开约 3s。
    private const long 狂欢大圆周期Ms = 6100;

    [ScriptMethod(name: "金碟狂欢 - 大圆预测画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:34773", "TargetIndex:1"])]
    public void 金碟狂欢大圆预测画图(Event evt, ScriptAccessory sa)
        => 预测机关下次判定(evt, sa, DrawTypeEnum.Circle, new Vector2(5), 原地AOE预测提前Ms, 8, 狂欢大圆周期Ms, 跟踪位置: true);

    // 34776 小圆：瞬发，ct=2 圆 r=2。坡顶 4 个机关（X = ±5.5 / ±1.83，Z = -366.8），每个间隔 0.4s 连打 14 下再停 5s，一轮 10.19s。

    private const float 滚球半径 = 2;
    private const uint 滚球画图上限Ms = 30000;   // 兜底：正常在球移除时就删了

    [ScriptMethod(name: "金碟狂欢 - 滚球碰撞范围画图", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:16351"])]
    public void 金碟狂欢滚球碰撞范围画图(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp($"金碟狂欢滚球-{evt.SourceId():X}", evt.SourceId(), 滚球画图上限Ms, new Vector2(滚球半径));
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "金碟狂欢 - 滚球移除", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:16351"], userControl: false)]
    public void 金碟狂欢滚球移除(Event evt, ScriptAccessory sa)
        => sa.Method.RemoveDraw($"^金碟狂欢滚球-{evt.SourceId():X}$");
    #endregion

    #region 金碟攻城
    // 34800：3.2s，ct=12 矩形 长 31 × 宽 12，命中击退（Omen m0532_hukitobashi）；16352 号可见怪物（-200, 6, 212）每 5.6s 读一次
    [ScriptMethod(name: "金碟攻城 - 击退直线画图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:34800"])]
    public void 金碟攻城击退直线画图(Event evt, ScriptAccessory sa)
    {
        if (!uint.TryParse(evt["DurationMilliseconds"], out var dur)) return;
        var dp = sa.FastDp($"金碟攻城击退直线-{evt.SourceId():X}", evt.EffectPosition(), dur, new Vector2(12, 31));
        dp.Rotation = evt.SourceRotation();
        dp.ScaleMode |= ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 34799 方形机关：瞬发，ct=12 矩形 长 4 × 宽 4，以机关为中心；16338 号机关 8 个，间隔恒定：
    // 击退怪物两侧两个（-196/-204, 216.5）3.70s 一次；另两条斜排各 3 个（间隔 4m）4.10s 一次，一排里 1、3 号同时打，2 号错开 1.94s
    [ScriptMethod(name: "金碟攻城 - 方形机关预测画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:34799", "TargetIndex:1"])]
    public void 金碟攻城方形机关预测画图(Event evt, ScriptAccessory sa)
        => 预测机关下次判定(evt, sa, DrawTypeEnum.Rect, new Vector2(4, 4), 移动机关预测提前Ms, 偏移: 矩形居中(4));

    // 34774 冲锋：瞬发，ct=8，从怪物当前位置（SourcePosition）冲到地面点（EffectPosition），宽 3；16350 号可见怪物，间隔恒定。
    private const float 冲锋宽 = 3;
    private const float 冲锋路线容差 = 2;

    private static bool 同一路线((Vector3 起点, Vector3 终点) a, (Vector3 起点, Vector3 终点) b)
        => 距离XZ(a.起点, b.起点) < 冲锋路线容差 && 距离XZ(a.终点, b.终点) < 冲锋路线容差;

    [ScriptMethod(name: "金碟攻城/狂欢 - 冲锋预测画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:34774", "TargetIndex:1"])]
    public void 冲锋预测画图(Event evt, ScriptAccessory sa)
    {
        var sid = evt.SourceId();
        var 这次 = (起点: evt.SourcePosition(), 终点: evt.EffectPosition());
        (Vector3 起点, Vector3 终点)? 下次 = null;
        int? 循环 = null;
        lock (_机关锁)
        {
            if (!_冲锋路线.TryGetValue(sid, out var 路线)) _冲锋路线[sid] = 路线 = [];
            路线.Add(这次);
            if (路线.Count > 位置记录上限) 路线.RemoveAt(0);
            循环 = 找循环(路线, 同一路线);
            if (循环 is { } p) 下次 = 路线[^p];
        }
        sa.Log.Debug($"冲锋 {sid:X} 起点 {这次.起点} 终点 {这次.终点} 路线循环 {循环}");
        if (记录并预测下次判定(sid, 2, 0) is not { } 等待 || 下次 is not { } 路) return;

        var (从, 到) = 路;
        var 长 = 距离XZ(从, 到);
        if (长 < 1) return;
        // 长度直接写进 Scale.Y、朝向自己算：Vfx 绘图模式下矩形外框不吃 YByDistance
        if (预测Dp(sa, $"机关预测-{sid:X}", 等待, 移动机关预测提前Ms, new Vector2(冲锋宽, 长)) is not { } dp) return;
        dp.Position = 从;
        dp.Rotation = MathF.Atan2(到.X - 从.X, 到.Z - 从.Z);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }
    #endregion

    #region 金碟登山
    private const uint 登山方块第一下 = 34812;
    private const long 登山方块周期Ms = 5980;
    private const float 登山方块边长 = 6;
    private const int 登山方块余波段数 = 4;
    private const long 登山方块余波最晚Ms = 3000;
    private const int 登山方块预测提前Ms = 2000;    // 超过 4000 会和上一轮还没打完的余波同时出现

    private sealed class 方块记录
    {
        public Vector3? 判定中心;
        public float 朝向;
        public long 第一下;
        public Vector3 第一下机关位置;
        public long 下一轮第一下;
        public int 余波段;
        public readonly List<Vector3>?[] 余波位移 = new List<Vector3>?[登山方块余波段数];
    }

    private readonly Dictionary<uint, 方块记录> _方块 = [];

    private 方块记录 方块(uint sid)
    {
        if (!_方块.TryGetValue(sid, out var b)) _方块[sid] = b = new();
        return b;
    }

    private static void 画登山方块预测(ScriptAccessory sa, string name, long 等待, Vector3 中心, float 朝向)
    {
        if (预测Dp(sa, name, 等待, 登山方块预测提前Ms, new Vector2(登山方块边长)) is not { } dp) return;
        dp.Position = 中心 - 登山方块边长 / 2 * 朝向向量(朝向);
        dp.Rotation = 朝向;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "金碟登山 - 方形AOE画图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:34812"])]
    public void 金碟登山方形AOE画图(Event evt, ScriptAccessory sa)
    {
        var sid = evt.SourceId();
        var 原点 = evt.EffectPosition();
        var 朝向 = evt.SourceRotation();
        if (原点.LengthSquared() > 0.01f)
        {
            lock (_机关锁)
            {
                var b = 方块(sid);
                b.判定中心 = 原点 + 登山方块边长 / 2 * 朝向向量(朝向);
                b.朝向 = 朝向;
            }
        }
        if (下次判定已预测(sid)) return;
        if (!uint.TryParse(evt["DurationMilliseconds"], out var dur)) return;
        var dp = sa.FastDp($"金碟登山方形AOE-{sid:X}", 原点, dur, new Vector2(登山方块边长));
        dp.Rotation = 朝向;
        dp.ScaleMode |= ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "金碟登山 - 方形AOE预测画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(34812|34804)$", "TargetIndex:1"])]
    public void 金碟登山方形AOE预测画图(Event evt, ScriptAccessory sa)
    {
        var sid = evt.SourceId();
        var now = Environment.TickCount64;
        var 机关位置 = evt.SourcePosition();
        var 朝向 = evt.SourceRotation();

        if (evt.ActionId() == 登山方块第一下)
        {
            var 等待 = 记录并预测下次判定(sid, 2, 登山方块周期Ms);
            Vector3 中心;
            float 画朝向;
            lock (_机关锁)
            {
                var b = 方块(sid);
                b.第一下 = now;
                b.第一下机关位置 = 机关位置;
                b.余波段 = 0;
                b.下一轮第一下 = 等待 is { } w ? now + w : 0;
                (中心, 画朝向) = b.判定中心 is { } c ? (c, b.朝向) : (机关位置, 朝向);
            }
            if (等待 is { } 等) 画登山方块预测(sa, $"机关预测-{sid:X}", 等, 中心, 画朝向);
            return;
        }

        // 34804 余波
        int 段;
        long 等待余波 = 0;
        Vector3 余波中心 = default;
        lock (_机关锁)
        {
            if (!_方块.TryGetValue(sid, out var b) || b.第一下 == 0 || now - b.第一下 > 登山方块余波最晚Ms) return;
            段 = ++b.余波段;
            if (段 > 登山方块余波段数) return;
            var 下轮位移 = 按位置循环预测(b.余波位移[段 - 1] ??= [], 机关位置 - b.第一下机关位置, 一次就当原地: true);
            if (下轮位移 is { } d && b.下一轮第一下 != 0)
            {
                等待余波 = b.下一轮第一下 - b.第一下;   // 下一轮同一段正好晚一个周期
                余波中心 = (b.判定中心 ?? b.第一下机关位置) + d;
            }
        }
        if (等待余波 > 0) 画登山方块预测(sa, $"机关预测余波{段}-{sid:X}", 等待余波, 余波中心, 朝向);
    }

    // 29795 r=3（2 个，2.54s）、34796 r=5（4 个，2.54s）、34802 r=5（5 个，3.25s 或 2.54s）、
    // 34801 r=6（X=-12 一列 4 个：两个恒 1.38s，另两个 1.38/1.38/1.38/1.87 四拍一循环 → 按四拍预测，打满五下才开始画）。
    private const uint 登山小圆 = 29795;
    private const uint 登山大圆 = 34801;

    [ScriptMethod(name: "金碟登山 - 圆形AOE预测画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(29795|34796|34801|34802)$", "TargetIndex:1"])]
    public void 金碟登山圆形AOE预测画图(Event evt, ScriptAccessory sa)
    {
        var (半径, 周期次数) = evt.ActionId() switch
        {
            登山小圆 => (3f, 2),
            登山大圆 => (6f, 4),
            _ => (5f, 2),
        };
        预测机关下次判定(evt, sa, DrawTypeEnum.Circle, new Vector2(半径), 原地AOE预测提前Ms, 周期次数, 跟踪位置: true);
    }
    #endregion

    #region 其他关卡

    // 34797/34798：1.2s，ct=13 扇形 r=8，Omen gl_fan180 → 180° 半圆，从施法者沿面向
    [ScriptMethod(name: "其他关卡 - 半圆扇形画图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34797|34798)$"])]
    public void 其他关卡半圆扇形画图(Event evt, ScriptAccessory sa)
    {
        if (!uint.TryParse(evt["DurationMilliseconds"], out var dur)) return;
        var dp = sa.FastDp($"其他关卡半圆扇形-{evt.SourceId():X}", evt.SourcePosition(), dur, new Vector2(8));
        dp.Rotation = evt.SourceRotation();
        dp.Radian = MathF.PI;
        dp.ScaleMode |= ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    // 34805：2.7s，ct=2 圆 r=6（Omen general_1bzt），圆心 = 施法目标
    [ScriptMethod(name: "其他关卡 - 圆形AOE画图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:34805"])]
    public void 其他关卡圆形AOE画图(Event evt, ScriptAccessory sa)
    {
        if (!uint.TryParse(evt["DurationMilliseconds"], out var dur)) return;
        var dp = sa.FastDp($"其他关卡圆形AOE-{evt.SourceId():X}", evt.TargetId(), dur, new Vector2(6));
        dp.ScaleMode |= ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    #endregion
}

#region Helpers

public static class EventExtensions
{
    public static float SourceRotation(this Event evt)
        => JsonConvert.DeserializeObject<float>(evt["SourceRotation"]);
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

    public static DrawPropertiesEdit FastDp(this ScriptAccessory sa, string name, uint owner, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.DestoryAt = duration;
        dp.Scale = scale;
        return dp;
    }
}

#endregion
