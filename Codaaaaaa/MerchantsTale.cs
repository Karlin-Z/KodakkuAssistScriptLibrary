using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;

namespace Codaaaaaa.MerchantsTale;

[ScriptType(
    guid: "a2c7f9d1-4b6e-4f3a-9c18-7d2e6a5b8c41",
    name: "多变迷宫 异闻商客奇谭 指路+画图",
    territorys: [1317],
    version: "0.0.0.4",
    author: "Codaaaaaa",
    note: "目前只完成了剑术大师，老一老三可以用Ryou佬的画图\n感谢Tou_uTou佬和Angelways佬的arr\n攻略使用的是mmw: https://mmw-ffxiv.feishu.cn/wiki/KvdJwQqfziIab3kPBAAcbCYvn5r")]
public class MerchantsTale
{
    #region 用户设置
    [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    [UserSetting("TTS播报方式")] public static TTS播报方式 TTSMode { get; set; } = TTS播报方式.原生TTS;
    // [UserSetting("剑术大师-P3打法")] public static 剑术大师P3打法 剑术大师_P3打法 { get; set; } = 剑术大师P3打法.MMW;
    [UserSetting("Debug")] public static bool 是否展示调试信息 { get; set; } = false;
    #endregion

    #region 设置enum
    public enum TTS播报方式
    {
        原生TTS,
        EdgeTTS,
        DrTTS
    }
    // public enum 剑术大师P3打法
    // {
    //     MMW,
    //     美式
    // }
    #endregion

    // 换P
    private int _phase = 1;

    #region 剑术大师param
    // ====== 天界交叉斩：点名记录 ======
    private readonly object _tjLock = new();
    private uint _tjArrowTarget = 0; // 028C
    private uint _tjMj1Target = 0;   // 014C
    private uint _tjMj2Target = 0;   // 014D

    private static readonly Vector3 TJ_ArrowPos = new(170.04f, -16.00f, -822.50f);

    // 单：没点名
    private static readonly Vector3 TJ_NoBuff_Single = new(162.39f, -16.00f, -814.90f);
    // 双：没点名
    private static readonly Vector3 TJ_NoBuff_Double = new(170.04f, -16.00f, -822.50f);

    // 圆/环：麻将站位
    private static readonly Vector3 TJ_Pos_A = new(170.02f, -16.00f, -807.20f);
    private static readonly Vector3 TJ_Pos_B = new(170.00f, -16.00f, -814.48f);

    private readonly object _xzLock = new();

    // 4785 / 4786 -> 同buff的人（一般4人/组，但我们只需要找“同组另一个人”）
    private readonly Dictionary<uint, List<uint>> _xzStatusGroups = new()
    {
        [4785u] = new List<uint>(),
        [4786u] = new List<uint>(),
        [4779u] = new List<uint>(),
        [4783u] = new List<uint>()

    };

    // 灵击波点名的两个人（满足过滤条件后才记录）
    private readonly List<uint> _xzLingjiTargets = new();

    // 画图名，用于 remove
    private const string XZCircleNamePrefix = "XZ_LinkCircle_";

    // 2m 绿色圈
    private static readonly Vector4 XZGreen = new(0f, 1f, 0f, 0.55f);
    private readonly object _p3O3Lock = new();
    private P3O3Dir? _p3O3FourthDanger = null;
    private readonly Dictionary<P3O3Dir, Vector3> _p3O3WavePos = new();
    private long _p3O3LastExecMs = 0;
    private bool _p3O3TaskScheduled = false;
    private long _p3O3FourthTicks = long.MinValue;

    private const float P3O3MatchEps = 0.6f;
    
    private bool _四方凶兆3收尾 = false;

    // 四向分类
    private enum P3O3Dir { West, East, South, North }
    private enum P3O3Quadrant { Unknown, NW, NE, SW, SE }

    private static bool Near(float v, float target, float eps) => MathF.Abs(v - target) <= eps;

    private readonly object _p4RockLock = new();

    // 记录两颗陨石（DataId=19229）的落点（XZ）
    private readonly List<Vector3> _p4RockPositions = new();

    // 去重容差
    private const float P4RockEps = 0.6f;

    private static readonly Vector3 P4RockRef = new(175.50f, -16.00f, -809.50f);

    // 集合点
    private static readonly Vector3 P4Gather = new(170.00f, -16.00f, -815.00f);

    // 分组指路点
    private static readonly Vector3 P4_A_02 = new(183.18f, -16.00f, -828.14f);
    private static readonly Vector3 P4_A_13 = new(156.93f, -16.00f, -801.79f);

    private static readonly Vector3 P4_B_02 = new(156.93f, -16.00f, -828.14f);
    private static readonly Vector3 P4_B_13 = new(183.18f, -16.00f, -801.79f);
    private readonly object _p4O4Lock = new();
    private readonly Dictionary<P3O3Dir, Vector3> _p4O4WavePos = new();
    private bool _p4O4TaskScheduled = false;

    private const float P4O4MatchEps = 0.8f;

    #endregion

    // ----------------------------
    // Phase helper
    // ----------------------------
    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa) => _phase = 1;

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2;

    [ScriptMethod(name: "Set Phase 3", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP3"], userControl: false)]
    public void SetP3(Event evt, ScriptAccessory sa) => _phase = 3;
    [ScriptMethod(name: "Set Phase 4", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP4"], userControl: false)]
    public void SetP4(Event evt, ScriptAccessory sa) => _phase = 4;
    [ScriptMethod(name: "Set Phase 5", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP5"], userControl: false)]
    public void SetP5(Event evt, ScriptAccessory sa) => _phase = 5;

    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:phase"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Method.SendChat($"/e Current Phase: {_phase}");

    [ScriptMethod(name: "初始化", eventType: EventTypeEnum.Chat, eventCondition: ["Type:NPCDialogueAnnouncements", "Message:regex:.*放马过来吧.*"], userControl: false)]
    public void 初始化(Event evt, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(".*");
        // sa.Method.SendChat($"/e Current Phase: {_phase}");
        ResetAll();
    }

    [ScriptMethod(name: "初始化兜底", eventType: EventTypeEnum.Chat, eventCondition: ["Type:SystemMessage", "Message:战斗开始！"], userControl: false)]
    public void 初始化兜底(Event evt, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(".*");
        // sa.Method.SendChat($"/e Current Phase: {_phase}");
        ResetAll();
    }

    public void Init(ScriptAccessory sa)
    {
        ResetAll();
        sa.Method.RemoveDraw(".*");
    }

    private void ResetAll()
    {
        _phase = 1;

        lock (_tjLock)
        {
            _tjArrowTarget = 0;
            _tjMj1Target = 0;
            _tjMj2Target = 0;
        }
        lock (_xzLock)
        {
            _xzStatusGroups[4785u].Clear();
            _xzStatusGroups[4786u].Clear();
            _xzStatusGroups[4779u].Clear();
            _xzStatusGroups[4783u].Clear();
            _xzLingjiTargets.Clear();
        }
        ResetP3();
        ResetP4Rock();
        ResetP4O4();
    }
    private readonly object _p3Lock = new();

    // 记录凶兆分组
    private readonly List<uint> _p3SafeEW = new(); // 4784 东西安全
    private readonly List<uint> _p3SafeNS = new(); // 4775 南北安全

    // 记录灵击波 tether 边（source -> target）
    private readonly List<P3TetherEdge> _p3Tethers = new();

    private bool _p3Resolved = false;
    private bool _p3GuideIssued = false;
    private int _p3Seq = 0;
    private long _p3LastMs = 0;

    private readonly Dictionary<uint, P3MemberLink> _p3MemberToBottom = new(); // memberId -> bottom info

    // 固定四点
    private static readonly Vector3 P3_Right = new(190.00f, -16.00f, -810.00f);
    private static readonly Vector3 P3_Left  = new(150.00f, -16.00f, -820.00f);
    private static readonly Vector3 P3_Down  = new(165.00f, -16.00f, -795.00f);
    private static readonly Vector3 P3_Up    = new(175.00f, -16.00f, -835.00f);

    private const float P3PointEps = 6.0f; 
    private const float P3AssignEps = 5.0f;

    private enum P3BottomKey { Unknown, Right, Left, Down, Up }

    private readonly record struct P3TetherEdge(
        uint SourceId,
        uint TargetId,
        Vector3 SourcePos,
        Vector3 TargetPos,
        long TimeMs
    );

    private readonly record struct P3MemberLink(
        uint MemberId,
        uint BottomOrbId,
        Vector3 BottomPos,
        P3BottomKey Key
    );

    private void ResetP3()
    {
        lock (_p3Lock)
        {
            _p3Seq++;
            _p3LastMs = 0;

            _p3SafeEW.Clear();
            _p3SafeNS.Clear();

            _p3Tethers.Clear();
            _p3MemberToBottom.Clear();

            _p3Resolved = false;
            _p3GuideIssued = false;
        }

        _四方凶兆3收尾 = false;
        lock (_p3O3Lock)
        {
            _p3O3FourthDanger = null;
            _p3O3FourthTicks = long.MinValue;
        }
    }

    private static float DistXZ2(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }

    private static P3BottomKey MatchBottomKey(Vector3 p)
    {
        // 只看 XZ
        float eps2 = P3PointEps * P3PointEps;
        if (DistXZ2(p, P3_Right) <= eps2) return P3BottomKey.Right;
        if (DistXZ2(p, P3_Left)  <= eps2) return P3BottomKey.Left;
        if (DistXZ2(p, P3_Down)  <= eps2) return P3BottomKey.Down;
        if (DistXZ2(p, P3_Up)    <= eps2) return P3BottomKey.Up;
        return P3BottomKey.Unknown;
    }

    private static bool IsPartyMember(ScriptAccessory sa, uint id)
        => id != 0 && sa.Data.PartyList.Contains(id);

    private static Vector3 GetObjPos(ScriptAccessory sa, uint id)
        => sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == id)?.Position ?? Vector3.Zero;

    private static uint FindAnother(List<uint> list, uint me)
        => list.FirstOrDefault(x => x != 0 && x != me);
    private static bool TryClassifyWaveDir(Vector3 p, out P3O3Dir dir)
    {
        // X 150左右=西，190左右=东；Z -795左右=南，-835左右=北
        if (Near(p.X, 150f, P3O3MatchEps)) { dir = P3O3Dir.West; return true; }
        if (Near(p.X, 190f, P3O3MatchEps)) { dir = P3O3Dir.East; return true; }
        if (Near(p.Z, -795f, P3O3MatchEps)) { dir = P3O3Dir.South; return true; }
        if (Near(p.Z, -835f, P3O3MatchEps)) { dir = P3O3Dir.North; return true; }

        dir = default;
        return false;
    }

    private static Vector3 ComposeCenter(Vector3 ewPos, Vector3 nsPos)
    {
        return new Vector3(nsPos.X, -16f, ewPos.Z);
    }

    private static IEnumerable<P3O3Dir> AllDirs()
    {
        yield return P3O3Dir.West;
        yield return P3O3Dir.East;
        yield return P3O3Dir.North;
        yield return P3O3Dir.South;
    }

    // 象限危险 -> 危险方向集合
    private static void AddQuadrantDanger(HashSet<P3O3Dir> danger, bool se, bool sw, bool ne, bool nw)
    {
        if (se) { danger.Add(P3O3Dir.East);  danger.Add(P3O3Dir.South); }
        if (sw) { danger.Add(P3O3Dir.West);  danger.Add(P3O3Dir.South); }
        if (ne) { danger.Add(P3O3Dir.East);  danger.Add(P3O3Dir.North); }
        if (nw) { danger.Add(P3O3Dir.West);  danger.Add(P3O3Dir.North); }
    }
    private void ClearP3O3()
    {
        lock (_p3O3Lock)
        {
            _p3O3WavePos.Clear();
            _p3O3TaskScheduled = false;
        }
    }
    private static float DistXZ2P4(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }

    private static bool NearXZ(Vector3 a, Vector3 b, float eps)
        => DistXZ2P4(a, b) <= eps * eps;

    private void ResetP4Rock()
    {
        lock (_p4RockLock)
            _p4RockPositions.Clear();
    }
    private void ResetP4O4()
    {
        lock (_p4O4Lock)
        {
            _p4O4WavePos.Clear();
            _p4O4TaskScheduled = false;
        }
    }
    private static bool TryClassifyP4O4Dir(Vector3 p, out P3O3Dir dir)
    {
        if (Near(p.X, 150f, P4O4MatchEps)) { dir = P3O3Dir.West; return true; }
        if (Near(p.X, 190f, P4O4MatchEps)) { dir = P3O3Dir.East; return true; }
        if (Near(p.Z, -835f, P4O4MatchEps)) { dir = P3O3Dir.North; return true; }
        if (Near(p.Z, -795f, P4O4MatchEps)) { dir = P3O3Dir.South; return true; }

        dir = default;
        return false;
    }

    // 4786: 东安全 / 4779: 北安全 / 4785: 西安全 / 4783: 南安全
    private static bool TryGetP4O4SafeDir(List<uint> myStatuses, out P3O3Dir safe)
    {
        if (myStatuses.Contains(4786u)) { safe = P3O3Dir.East; return true; }
        if (myStatuses.Contains(4779u)) { safe = P3O3Dir.North; return true; }
        if (myStatuses.Contains(4785u)) { safe = P3O3Dir.West; return true; }
        if (myStatuses.Contains(4783u)) { safe = P3O3Dir.South; return true; }

        safe = default;
        return false;
    }

    private static float Abs(float x) => x < 0 ? -x : x;

    // 在两颗落石里选“指定轴更接近 innerCoord”的那一颗
    private static Vector3 PickRockByAxis(List<Vector3> rocks, bool compareZ, float innerCoord)
    {
        if (rocks.Count == 0) return Vector3.Zero;

        Vector3 best = Vector3.Zero;
        float bestD = float.MaxValue;

        foreach (var r in rocks)
        {
            float v = compareZ ? r.Z : r.X;
            float d = Abs(v - innerCoord);
            if (d < bestD)
            {
                bestD = d;
                best = r;
            }
        }
        return best;
    }

    #region 通用
    [ScriptMethod(name: "aoe播报", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45870|46686)$"])]
    public void BossAoe(Event evt, ScriptAccessory sa)
    {
        sa.tts("超大AOE", TTSMode, TTSOpen);
    }
    private uint GetDataIdByObjectId(ScriptAccessory sa, uint objectId)
    {
        if (objectId == 0) return 0;
        var obj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == objectId);
        if (obj == null) return 0;

        return obj.DataId;
    }
    private void DrawWaypointToMe(ScriptAccessory sa, Vector3 wpos, int durMs, string name)
    {
        var dpWp = sa.WaypointDp(wpos, (uint)durMs, 0, name);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
    }
    #endregion

    #region 剑术大师

    // 47566: 圆 单 猜的
    // 47567: 环 单
    // 47568: 圆 双
    // 47569: 环 双 猜的
    [ScriptMethod(name: "剑术大师-P1-天界交叉斩", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47566|47567|47568|47569)$"])]
    public void 剑术大师_P1_天界交叉斩(Event evt, ScriptAccessory sa)
    {
        uint actionId = evt.ActionId();

        bool isCircle = actionId is 47566u or 47568u;
        bool isDouble = actionId is 47568u or 47569u;

        int durMs = 0;
        _ = int.TryParse(evt["DurationMilliseconds"], out durMs);
        if (durMs <= 0) durMs = 6000;

        _ = Task.Run(async () =>
        {
            await Task.Delay(600);

            uint myId = sa.Data.Me;
            // sa.Method.SendChat($"/e Myid: {myId}");

            uint arrow, mj1, mj2;
            lock (_tjLock)
            {
                arrow = _tjArrowTarget;
                mj1 = _tjMj1Target;
                mj2 = _tjMj2Target;
            }
            uint arrowIdLocal = arrow;
            uint mj1IdLocal = mj1;
            uint mj2IdLocal = mj2;

            Vector3 mj1Target = isCircle ? TJ_Pos_A : TJ_Pos_B;
            Vector3 mj2Target = isCircle ? TJ_Pos_B : TJ_Pos_A;

            bool IsCirclePos(Vector3 p) => p == TJ_Pos_A;
            DrawTypeEnum TypeForMj(Vector3 mjTarget) => IsCirclePos(mjTarget) ? DrawTypeEnum.Circle : DrawTypeEnum.Donut;


            // 画图
            void DrawOnTarget(uint targetId, string namePrefix, DrawTypeEnum type, float radius, Vector4 color, int dur)
            {
                if (targetId == 0) return;
                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"{namePrefix}_{targetId:X}_{Environment.TickCount64}";
                dp.Owner = targetId;
                dp.DestoryAt = (uint)Math.Max(500, dur);
                dp.Color = color;
                dp.ScaleMode = ScaleMode.ByTime;
                if (type == DrawTypeEnum.Circle)
                {
                    dp.Scale = new Vector2(radius);
                }
                else
                {
                    dp.Scale = new Vector2(60f);
                    dp.InnerScale = new Vector2(radius);
                    dp.Radian = float.Pi * 2;
                }
                sa.Method.SendDraw(DrawModeEnum.Default, type, dp);
            }
            // 1) 箭头：circle
            if (arrowIdLocal != 0)
            {
                DrawOnTarget(arrowIdLocal, "TJ_Arrow", DrawTypeEnum.Circle, 5f, sa.Data.DefaultSafeColor, durMs+2000);
            }
            // 2) 麻将1
            if (mj1IdLocal != 0)
            {
                var t = TypeForMj(mj1Target);
                var durMs_mj1 = isCircle ? durMs : durMs+2000;
                DrawOnTarget(mj1IdLocal, "TJ_MJ1", t, 8f, sa.Data.DefaultDangerColor, durMs_mj1);
            }

            // 3) 麻将2
            if (mj2IdLocal != 0)
            {
                var t = TypeForMj(mj2Target);
                var durMs_mj2 = isCircle ? durMs+2000 : durMs;
                DrawOnTarget(mj2IdLocal, "TJ_MJ2", t, 8f, sa.Data.DefaultDangerColor, durMs_mj2);
            }

            // 指路
            Vector3 target = Vector3.Zero;
            string label = "";

            if (myId == arrow && arrow != 0)
            {
                target = TJ_ArrowPos;
                label = "天界交叉斩_箭头";
                sa.tts("箭头位 去A", TTSMode, TTSOpen);
            }
            else if (myId == mj1 && mj1 != 0)
            {
                // 圆：014C->A, 014D->B
                // 环：014C->B, 014D->A
                target = isCircle ? TJ_Pos_A : TJ_Pos_B;
                label = "天界交叉斩_麻将1";
                sa.tts(isCircle ? "麻将一 去C" : "麻将一 去中间", TTSMode, TTSOpen);
            }
            else if (myId == mj2 && mj2 != 0)
            {
                target = isCircle ? TJ_Pos_B : TJ_Pos_A;
                label = "天界交叉斩_麻将2";
                sa.tts(isCircle ? "麻将二 去中间" : "麻将二 去C", TTSMode, TTSOpen);
            }
            else
            {
                // 没点名的人
                target = isDouble ? TJ_NoBuff_Double : TJ_NoBuff_Single;
                label = isDouble ? "天界交叉斩_无点名_双" : "天界交叉斩_无点名_单";
                sa.tts(isDouble ? "无点名 去A分摊" : "无点名 靠边", TTSMode, TTSOpen);
            }

            if (target == Vector3.Zero) return;

            var dp = sa.WaypointDp(
                target: target,
                duration: (uint)Math.Max(1000, durMs),
                delay: 0,
                name: $"{label}_{Environment.TickCount64}",
                color: sa.Data.DefaultSafeColor
            );
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        });
    }

    [ScriptMethod(name: "剑术大师-P1-天界交叉斩-麻将记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(028C|014C|014D)$"], userControl: false)]
    public void 剑术大师_P1_天界交叉斩_麻将记录(Event evt, ScriptAccessory sa)
    {
        uint tid = evt.TargetId();
        if (tid == 0) return;

        string iconId = evt["Id"] ?? "";

        lock (_tjLock)
        {
            if (iconId.Equals("028C", StringComparison.OrdinalIgnoreCase))
            {
                _tjArrowTarget = tid;
            }
            else if (iconId.Equals("014C", StringComparison.OrdinalIgnoreCase))
            {
                _tjMj1Target = tid;
            }
            else if (iconId.Equals("014D", StringComparison.OrdinalIgnoreCase))
            {
                _tjMj2Target = tid;
            }
        }
    }

    [ScriptMethod(name: "剑术大师-P1-四方凶兆1-凶兆记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(4785|4786|4783|4779)$"], userControl: false)]
    public void 剑术大师_P1_四方凶兆1_凶兆记录(Event evt, ScriptAccessory sa)
    {
        // 4783 东西北
        // 4779 东西南
        uint tid = evt.TargetId();
        if (tid == 0) return;

        var sid = evt.StatusId;
        // sa.Method.SendChat($"/e StatusId{sid}");

        lock (_xzLock)
        {
            if (!_xzStatusGroups.TryGetValue(sid, out var list)) return;
            if (!list.Contains(tid))
                list.Add(tid);
        }
    }

    [ScriptMethod(name: "剑术大师-P1-四方凶兆1-灵击波记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0173)$"], userControl: false)]
    public void 剑术大师_P1_四方凶兆1_灵击波记录(Event evt, ScriptAccessory sa)
    {

        uint tid = evt.TargetId();
        if (tid == 0) return;
        // sa.Method.SendChat($"/e tid{tid}");
        uint dataId = GetDataIdByObjectId(sa, tid);
        // sa.Method.SendChat($"/e dataId{dataId}");

        // 过滤：DataId 是 19230 或 19228 的不记录
        if (dataId == 19230u || dataId == 19228u) return;

        lock (_xzLock)
        {
            if (_xzLingjiTargets.Count >= 2) return;
            if (_xzLingjiTargets.Contains(tid)) return;

            _xzLingjiTargets.Add(tid);
            // sa.Method.SendChat($"/e [四方凶兆1] 灵击波点名 {_xzLingjiTargets.Count}/2 => tid=0x{tid:X} dataId={dataId}");
        }
    }

    [ScriptMethod(name: "剑术大师-P1-四方凶兆1-接线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46698)$"])]
    public async void 剑术大师_P1_四方凶兆1_接线(Event evt, ScriptAccessory sa)
    {
        if (_phase == 1)
        {
            await Task.Delay(1000);
            uint myId = sa.Data.Me;
            // sa.Method.SendChat($"/e myId{myId}");
            // sa.Method.SendChat($"/e _xzLingjiTargets{_xzLingjiTargets}");

            uint partnerId = 0;
            bool iAmLingji = false;

            lock (_xzLock)
            {
                iAmLingji = _xzLingjiTargets.Contains(myId);
                // sa.Method.SendChat($"/e iAmLingji{iAmLingji}");
                if (iAmLingji) return; // 自己被灵击波点了，不接线，不画

                // 找自己属于哪个凶兆组，然后找同组另一个人
                foreach (var kv in _xzStatusGroups)
                {
                    var list = kv.Value;
                    if (!list.Contains(myId)) continue;

                    partnerId = list.FirstOrDefault(x => x != myId);
                    break;
                }
            }

            if (partnerId == 0)
            {
                sa.tts("未找到搭档ID", TTSMode, TTSOpen);
                return;
            }
            

            // 画 2m 绿圈（绑定对方脚下）
            sa.tts("接同组的线", TTSMode, TTSOpen);

            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"{XZCircleNamePrefix}{partnerId:X}_{Environment.TickCount64}";
            dp.Owner = partnerId;
            dp.DestoryAt = 8000; // 给个够用时长，后面 ActionEffect 再 remove
            dp.Scale = new Vector2(2f);
            dp.ScaleMode = ScaleMode.None;
            dp.Color = XZGreen;

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }

        if (_phase == 4)
        {
            _四方凶兆3收尾 = true;
        }
    }

    [ScriptMethod(name: "剑术大师-P1-天界交叉斩-清除绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(46693)$"], userControl:false)]
    public void 剑术大师_P1_天界交叉斩_清除绘图(Event evt, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "剑术大师-P1-四方凶兆1-接线清除绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(46698)$"], userControl:false)]
    public void 剑术大师_P1_四方凶兆1_接线清除绘图(Event evt, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"^{XZCircleNamePrefix}.*$");

        lock (_xzLock)
        {
            _xzStatusGroups[4785u].Clear();
            _xzStatusGroups[4786u].Clear();
            _xzStatusGroups[4779u].Clear();
            _xzStatusGroups[4783u].Clear();
            _xzLingjiTargets.Clear();
        }
    }
    
    [ScriptMethod(name: "剑术大师-P2-八叶转轮残响回响组合技", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46707|46704)$"])]
    public void 剑术大师_P2_八叶转轮残响回响组合技(Event evt, ScriptAccessory sa)
    {
        
        uint actionId = evt.ActionId();

        int durMs = 0;
        _ = int.TryParse(evt["DurationMilliseconds"], out durMs);
        if (durMs <= 0) durMs = 6000;

        if (_phase == 1)
        {
            sa.tts("场中集合", TTSMode, TTSOpen);
            var dpWp = sa.WaypointDp(
                target: new Vector3(170f, -16f, -815f),
                duration: (uint)Math.Max(1000, durMs),
                delay: 0,
                name: $"P2-八叶转轮-场中集合_{actionId}",
                color: sa.Data.DefaultSafeColor
            );
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
        }
        
        int delayMs = Math.Max(0, durMs);

        // 46707 用 8向 rect
        float[] rots =
        {
            MathF.PI,              // N
            3f * MathF.PI / 4f,    // NE
            MathF.PI / 2f,         // E
            MathF.PI / 4f,         // SE
            0f,                    // S
            -MathF.PI / 4f,        // SW
            -MathF.PI / 2f,        // W
            -3f * MathF.PI / 4f,   // NW
        };
        string[] dirNames = { "N","NE","E","SE","S","SW","W","NW" };

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs);

                var partyIds = sa.Data.PartyList.ToList();
                if (partyIds.Count == 0) return;

                var posById = new List<(uint id, Vector3 pos)>(partyIds.Count);
                foreach (var pid in partyIds)
                {
                    if (pid == 0) continue;
                    var obj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == pid);
                    if (obj == null) continue;
                    posById.Add((pid, obj.Position));
                }
                if (posById.Count == 0) return;

                if (actionId == 46707u)
                {
                    // 冰花
                    foreach (var (pid, basePos) in posById)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            var dp = sa.Data.GetDefaultDrawProperties();
                            dp.Name = $"P2-八叶转轮_Rect_{pid:X}_{dirNames[i]}_{Environment.TickCount64}";
                            dp.Owner = 0;
                            dp.Position = basePos;
                            dp.Rotation = rots[i];
                            dp.Color = sa.Data.DefaultDangerColor;
                            dp.DestoryAt = 7000;

                            dp.Scale = new Vector2(8f, 35f);
                            dp.ScaleMode = ScaleMode.ByTime;

                            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                        }
                    }
                }
                else
                {
                    // 月环
                    foreach (var (pid, basePos) in posById)
                    {
                        var dp = sa.Data.GetDefaultDrawProperties();
                        dp.Name = $"P2-八叶转轮_Donut_{pid:X}_{Environment.TickCount64}";
                        dp.Owner = 0;
                        dp.Position = basePos;
                        dp.Rotation = 0f;
                        dp.Color = sa.Data.DefaultDangerColor;
                        dp.DestoryAt = 7000;

                        dp.Scale = new Vector2(60f);
                        dp.InnerScale = new Vector2(8f);
                        dp.Radian = float.Pi * 2f;
                        dp.ScaleMode = ScaleMode.None;

                        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    }
                }
            }
            catch
            {
            }
        });
    }
    
    private readonly object _eightLock = new();
    private int _eightSeq = 0;
    private long _eightLastMs = 0;

    private readonly List<int> _eightR1 = new(3);
    private readonly List<int> _eightR2 = new(3);
    private readonly List<int> _eightR3 = new(3);

    private bool _eightR2TaskScheduled = false;
    private bool _eightR3GuideIssued = false;

    private static readonly Vector3[] EightCenters = new[]
    {
        new Vector3(157.00f, -16.01f, -828.00f),
        new Vector3(169.97f, -16.01f, -828.00f),
        new Vector3(182.97f, -16.01f, -828.00f),

        new Vector3(157.00f, -16.01f, -815.03f),
        new Vector3(169.97f, -16.01f, -815.03f),
        new Vector3(182.97f, -16.01f, -815.03f),

        new Vector3(157.00f, -16.01f, -802.03f),
        new Vector3(169.97f, -16.01f, -802.03f),
        new Vector3(182.97f, -16.01f, -802.03f),
    };

    private const float EightMatchEps = 0.6f;

    private void ResetEight()
    {
        lock (_eightLock)
        {
            _eightSeq++;
            _eightLastMs = 0;

            _eightR1.Clear();
            _eightR2.Clear();
            _eightR3.Clear();

            _eightR2TaskScheduled = false;
            _eightR3GuideIssued = false;
        }
    }

    private static int MatchEightIndex(Vector3 p)
    {
        int best = -1;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < EightCenters.Length; i++)
        {
            var d = p - EightCenters[i];
            float d2 = d.X * d.X + d.Z * d.Z;
            if (d2 < bestD2) { bestD2 = d2; best = i; }
        }
        return bestD2 <= EightMatchEps * EightMatchEps ? best : -1;
    }

    private static Vector3 GetMePos(ScriptAccessory sa)
    {
        var meId = sa.Data.Me;
        return sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == meId)?.Position ?? Vector3.Zero;
    }

    private static Vector3 PickNearest(Vector3 me, IEnumerable<int> indices)
    {
        float bestD2 = float.MaxValue;
        Vector3 best = Vector3.Zero;

        foreach (var idx in indices)
        {
            var c = EightCenters[idx];
            float d2 = (me.X - c.X) * (me.X - c.X) + (me.Z - c.Z) * (me.Z - c.Z);
            if (d2 < bestD2) { bestD2 = d2; best = c; }
        }
        return best;
    }

    private static void BuildR3FromR1R2(List<int> r1, List<int> r2, List<int> r3)
    {
        r3.Clear();
        var used = new HashSet<int>(r1);
        foreach (var x in r2) used.Add(x);

        for (int i = 0; i < 9; i++)
            if (!used.Contains(i))
                r3.Add(i);
    }

    private void AddIdxToRounds_NoAdvance(int idx)
    {
        if (idx < 0) return;

        if (_eightR1.Count < 3)
        {
            if (!_eightR1.Contains(idx)) _eightR1.Add(idx);
            return;
        }
        if (_eightR2.Count < 3)
        {
            if (!_eightR2.Contains(idx)) _eightR2.Add(idx);
            return;
        }

        if (_eightR3.Count < 3 && !_eightR3.Contains(idx))
            _eightR3.Add(idx);
    }

    [ScriptMethod(
        name: "剑术大师-P2-时差剑波",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46714"],
        userControl: false)]
    public void 剑术大师_P2_时差剑波(Event evt, ScriptAccessory sa)
    {
        var pos = evt.EffectPosition;
        int idx = MatchEightIndex(pos);
        if (idx < 0) return;

        long now = Environment.TickCount64;
        int seqLocal;

        bool r2Start = false; // 第二轮开始：R2 第一个圈
        bool r3Start = false; // 第三轮开始：R3 第一个圈

        lock (_eightLock)
        {
            // 超时重置
            if (_eightLastMs != 0 && now - _eightLastMs > 5000)
                ResetEight();

            _eightLastMs = now;

            int r2Before = _eightR2.Count;
            int r3Before = _eightR3.Count;

            AddIdxToRounds_NoAdvance(idx);

            if (r2Before == 0 && _eightR2.Count == 1) r2Start = true;
            if (r3Before == 0 && _eightR3.Count == 1) r3Start = true;

            seqLocal = _eightSeq;
        }

        if (r2Start)
        {
            bool canSchedule = false;
            lock (_eightLock)
            {
                if (!_eightR2TaskScheduled)
                {
                    _eightR2TaskScheduled = true;
                    canSchedule = true;
                }
            }

            if (canSchedule)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(250);

                    List<int> r1, r2, r3;
                    int seqNow;

                    lock (_eightLock)
                    {
                        seqNow = _eightSeq;
                        r1 = _eightR1.ToList();
                        r2 = _eightR2.ToList();
                        r3 = _eightR3.ToList();
                    }

                    if (seqNow != seqLocal) return;
                    if (r1.Count < 3 || r2.Count < 3) return;

                    // 推导第三轮
                    var r3Calc = new List<int>(3);
                    BuildR3FromR1R2(r1, r2, r3Calc);

                    var me = GetMePos(sa);
                    if (me == Vector3.Zero) return;

                    var target = PickNearest(me, r3Calc);

                    var dpWp = sa.WaypointDp(target, (uint)3700, 0, "八叶R2->去R3最近圈");
                    sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
                });
            }
        }

        if (r3Start)
        {
            bool need;
            lock (_eightLock)
            {
                need = !_eightR3GuideIssued && _eightR1.Count == 3;
                if (need) _eightR3GuideIssued = true;
            }

            if (need)
            {
                var me = GetMePos(sa);
                if (me == Vector3.Zero) return;

                List<int> r1Copy;
                lock (_eightLock) r1Copy = _eightR1.ToList();

                var target = PickNearest(me, r1Copy);

                var dpWp = sa.WaypointDp(target, (uint)2000, 3000, "八叶R3->去R1最近圈");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
            }
        }
    }

    [ScriptMethod(name: "剑术大师-P2-八叶回响-分散圈", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46711)$"])]
    public void 剑术大师_P2_八叶回响_分散圈(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        sa.tts("去标点放圈", TTSMode, TTSOpen);

        var wPos = myIdx switch
        {
            0 => new Vector3(182.97f, -16.00f, -828.07f),
            1 => new Vector3(157.00f, -16.00f, -828.12f),
            2 => new Vector3(156.99f, -16.00f, -801.99f),
            3 => new Vector3(182.97f, -16.00f, -801.92f),
            _ => default
        };
        
        var dpWp = sa.WaypointDp(wPos, (uint)5000, 4000, "剑术大师-P2-八叶回响-分散冰花指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
    }

    [ScriptMethod(name: "剑术大师-P2-八叶回响-分散圈-第二段指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(46712)$"])]
    public void 剑术大师_P2_八叶回响_分散圈_第二段指路(Event evt, ScriptAccessory sa)
    {
        if(_phase != 1) return;

        var myId = sa.Data.Me;
        var targetId = evt.TargetId();
        // sa.Method.SendChat($"/e {myId} {targetId}");
        if (targetId == 0 || targetId != myId) return;

        var myIdx = sa.MyIndex();
        sa.tts("快跑", TTSMode, TTSOpen);

        var wPos = myIdx switch
        {
            0 => new Vector3(170.00f, -16.00f, -822.58f),
            1 => new Vector3(170.00f, -16.00f, -822.58f),
            2 => new Vector3(162.40f, -16.00f, -815.06f),
            3 => new Vector3(169.87f, -16.00f, -807.40f),
            _ => default
        };

        var dpWp = sa.WaypointDp(wPos, (uint)3000, 0, "剑术大师-P2-八叶回响-分散冰花指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
    }

    [ScriptMethod(name: "剑术大师-P2-半场刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46716)$"])]
    public void 剑术大师_P2_半场刀(Event evt, ScriptAccessory sa)
    {
        float rot;
        try
        {
            rot = evt.SourceRotation;
        }
        catch
        {
            return;
        }

        var pos = evt.SourcePosition();
        if (pos == Vector3.Zero) return;

        int durMs = 0;
        _ = int.TryParse(evt["DurationMilliseconds"], out durMs);
        if (durMs <= 0) durMs = 6000;

        const float outer = 60f;

        sa.tts("半场刀", TTSMode, TTSOpen);
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"P2_半场刀_{Environment.TickCount64}";
        dp.Position = pos;
        dp.Rotation = rot;
        dp.Color = sa.Data.DefaultDangerColor;
        dp.DestoryAt = (uint)(durMs);
        dp.ScaleMode = ScaleMode.ByTime;

        dp.Scale = new Vector2(outer);
        dp.Radian = MathF.PI;         // 180°

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "剑术大师-P2-冥界重光波-三连分摊TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46717)$"])]
    public void 剑术大师_P2_冥界重光波_三连分摊TTS(Event evt, ScriptAccessory sa)
    {
        sa.tts("三连分摊", TTSMode, TTSOpen);
        // 换p进入p3
        if (_phase > 3) return;
        _phase = 3;
    }

    [ScriptMethod(name: "剑术大师-P3-四方凶兆2-凶兆记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(4784|4775)$"], userControl: false)]
    public void 剑术大师_P3_四方凶兆2_凶兆记录(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        uint tid = evt.TargetId();
        if (tid == 0) return;

        long now = Environment.TickCount64;

        lock (_p3Lock)
        {
            if (_p3LastMs != 0 && now - _p3LastMs > 12000)
                ResetP3();

            _p3LastMs = now;

            uint sid = evt.StatusId;
            if (sid == 4784u)
            {
                if (!_p3SafeEW.Contains(tid)) _p3SafeEW.Add(tid);
            }
            else if (sid == 4775u)
            {
                if (!_p3SafeNS.Contains(tid)) _p3SafeNS.Add(tid);
            }
        }
    }
    
    [ScriptMethod(name: "剑术大师-P3-四方凶兆2-灵击波记录1", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0173)$"], userControl: false)]
    public void 剑术大师_P3_四方凶兆2_灵击波记录1(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        uint sid = evt.SourceId();
        uint tid = evt.TargetId();
        if (sid == 0 || tid == 0) return;

        long now = Environment.TickCount64;

        Vector3 sPos = evt.SourcePosition();
        Vector3 tPos = evt.TargetPosition();

        lock (_p3Lock)
        {
            // 超时保护
            if (_p3LastMs != 0 && now - _p3LastMs > 12000)
                ResetP3();
            _p3LastMs = now;

            // 去重
            const int windowMs = 300;
            for (int i = _p3Tethers.Count - 1; i >= 0; i--)
            {
                var e = _p3Tethers[i];
                if (now - e.TimeMs > windowMs) break;
                if (e.SourceId == sid && e.TargetId == tid) return;
            }

            _p3Tethers.Add(new P3TetherEdge(sid, tid, sPos, tPos, now));
        }
    }

    [ScriptMethod(name: "剑术大师-P3-四方凶兆2-灵击波记录2",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(48653)$"],
        userControl: false)]
    public void 剑术大师_P3_四方凶兆2_灵击波记录2(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        int seqLocal;
        lock (_p3Lock)
        {
            if (_p3Resolved) return;
            _p3Resolved = true;
            seqLocal = _p3Seq;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(500);

            List<P3TetherEdge> edges;
            List<uint> party;
            Dictionary<uint, Vector3> partyPos = new();

            lock (_p3Lock)
            {
                if (seqLocal != _p3Seq) return;
                edges = _p3Tethers.ToList();
            }

            party = sa.Data.PartyList.Where(x => x != 0).ToList();
            foreach (var pid in party)
                partyPos[pid] = GetObjPos(sa, pid);

            if (edges.Count == 0 || party.Count == 0) return;

            var outMap = new Dictionary<uint, uint>();
            var posOf  = new Dictionary<uint, Vector3>();

            foreach (var e in edges)
            {
                outMap[e.SourceId] = e.TargetId;

                if (e.SourcePos != Vector3.Zero) posOf[e.SourceId] = e.SourcePos;
                if (e.TargetPos != Vector3.Zero && !posOf.ContainsKey(e.TargetId)) posOf[e.TargetId] = e.TargetPos;
            }

            var inMap = new Dictionary<uint, List<uint>>();
            foreach (var e in edges)
            {
                if (!inMap.TryGetValue(e.TargetId, out var list))
                    inMap[e.TargetId] = list = new List<uint>();
                if (!list.Contains(e.SourceId)) list.Add(e.SourceId);
            }

            var memberToBottom = new Dictionary<uint, P3MemberLink>();

            // ---- BEGIN INSERT ----
            var reservedOrbs = new HashSet<uint>();
            var usedBottoms  = new HashSet<uint>();

            uint GetBottom(uint start)
            {
                uint cur = start;
                int guard = 0;
                while (cur != 0 && guard++ < 16)
                {
                    if (!inMap.TryGetValue(cur, out var parents) || parents.Count == 0) break;
                    uint p = parents.FirstOrDefault(x => !partyPos.ContainsKey(x));
                    if (p == 0) break;
                    cur = p;
                }
                return cur;
            }

            void MarkChainReserved(uint startOrb)
            {
                uint cur = startOrb;
                int guard = 0;
                while (cur != 0 && guard++ < 16)
                {
                    reservedOrbs.Add(cur);

                    if (!inMap.TryGetValue(cur, out var parents) || parents.Count == 0) break;
                    uint p = parents.FirstOrDefault(x => !partyPos.ContainsKey(x));
                    if (p == 0) break;
                    cur = p;
                }
            }

            // 1) 正常匹配：orb -> player
            foreach (var m in party)
            {
                var incoming = edges.Where(x => x.TargetId == m).Select(x => x.SourceId).Distinct().ToList();
                if (incoming.Count == 0) continue;

                uint orb = incoming.FirstOrDefault(id => posOf.ContainsKey(id));
                if (orb == 0) orb = incoming[0];

                uint bottom = GetBottom(orb);
                Vector3 bpos = posOf.TryGetValue(bottom, out var bp) ? bp : Vector3.Zero;
                if (bpos == Vector3.Zero && posOf.TryGetValue(orb, out var op)) bpos = op;

                var key = MatchBottomKey(bpos);
                memberToBottom[m] = new P3MemberLink(m, bottom, bpos, key);

                MarkChainReserved(orb);
                MarkChainReserved(bottom);
                usedBottoms.Add(bottom);
            }

            // 2) orphan 兜底：只在未占用的 orb 节点里找
            var orphanOrbs = posOf
                .Where(kv => !partyPos.ContainsKey(kv.Key))
                .Where(kv => kv.Value != Vector3.Zero)
                .Where(kv => !reservedOrbs.Contains(kv.Key))
                .Select(kv => (id: kv.Key, pos: kv.Value))
                .ToList();

            foreach (var m in party)
            {
                if (memberToBottom.ContainsKey(m)) continue;
                if (!partyPos.TryGetValue(m, out var mp) || mp == Vector3.Zero) continue;

                var nearest = orphanOrbs
                    .Select(o => (o.id, o.pos, d2: DistXZ2(mp, o.pos)))
                    .OrderBy(x => x.d2)
                    .Take(6)
                    .ToList();

                foreach (var cand in nearest)
                {
                    uint bottom = GetBottom(cand.id);
                    if (bottom != 0 && usedBottoms.Contains(bottom))
                        continue;

                    Vector3 bpos = posOf.TryGetValue(bottom, out var bp) ? bp : cand.pos;
                    var key = MatchBottomKey(bpos);

                    memberToBottom[m] = new P3MemberLink(m, bottom, bpos, key);

                    MarkChainReserved(cand.id);
                    MarkChainReserved(bottom);
                    usedBottoms.Add(bottom);
                    break;
                }
            }
            // ---- END INSERT ----

            lock (_p3Lock)
            {
                if (seqLocal != _p3Seq) return;

                _p3MemberToBottom.Clear();
                foreach (var kv in memberToBottom)
                    _p3MemberToBottom[kv.Key] = kv.Value;
            }
        });
    }

    [ScriptMethod(name: "剑术大师-P3-四方凶兆2", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46725)$"])]
    public void 剑术大师_P3_四方凶兆2(Event evt, ScriptAccessory sa)
    {
        if (_phase != 3) return;

        int seqLocal;
        lock (_p3Lock)
        {
            if (_p3GuideIssued) return;
            _p3GuideIssued = true;
            seqLocal = _p3Seq;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);

            uint me = sa.Data.Me;
            if (me == 0) return;

            List<uint> ew, ns;
            Dictionary<uint, P3MemberLink> map;

            lock (_p3Lock)
            {
                if (seqLocal != _p3Seq) return;
                ew = _p3SafeEW.ToList();
                ns = _p3SafeNS.ToList();
                map = _p3MemberToBottom.ToDictionary(k => k.Key, v => v.Value);
            }

            bool iAmEW = ew.Contains(me);
            bool iAmNS = ns.Contains(me);

            if (!iAmEW && !iAmNS)
            {
                sa.tts("未记录凶兆分组", TTSMode, TTSOpen);
                return;
            }

            uint partner = iAmEW ? FindAnother(ew, me) : FindAnother(ns, me);
            if (partner == 0)
            {
                sa.tts("未找到同组队友", TTSMode, TTSOpen);
                return;
            }

            map.TryGetValue(me, out var myLink);
            map.TryGetValue(partner, out var ptLink);

            Vector3 EW_A = new(188.24f, -16.00f, -819.50f);
            Vector3 EW_B = new(151.26f, -16.00f, -810.56f);

            Vector3 NS_A = new(174.39f, -16.00f, -796.65f);
            Vector3 NS_B = new(165.24f, -16.00f, -833.78f);

            Vector3 go = Vector3.Zero;

            if (iAmEW)
            {
                // - 优先看谁连 LEFT：连 LEFT 的去 EW_A，另一个去 EW_B
                // - 如果两个人都不连 LEFT，则看谁连 RIGHT：连 RIGHT 的去 EW_B，另一个去 EW_A
                bool meLeft = myLink.Key == P3BottomKey.Left;
                bool ptLeft = ptLink.Key == P3BottomKey.Left;

                if (meLeft || ptLeft)
                {
                    go = meLeft ? EW_A : EW_B;
                }
                else
                {
                    bool meRight = myLink.Key == P3BottomKey.Right;
                    bool ptRight = ptLink.Key == P3BottomKey.Right;

                    if (meRight || ptRight)
                        go = meRight ? EW_B : EW_A;
                    else
                    {
                        // 兜底：按站位近的分配
                        sa.Method.SendChat("/e 四方凶兆2发生错误");
                        sa.Method.SendChat($"/e [O2ERR] me=0x{me:X} iAmEW={iAmEW} iAmNS={iAmNS} partner=0x{partner:X} " +
                            $"myKey={myLink.Key} myBottom=0x{myLink.BottomOrbId:X} myPos=({myLink.BottomPos.X:0.0},{myLink.BottomPos.Z:0.0}) " +
                            $"ptKey={ptLink.Key} ptBottom=0x{ptLink.BottomOrbId:X} ptPos=({ptLink.BottomPos.X:0.0},{ptLink.BottomPos.Z:0.0}) " +
                            $"EW=[{string.Join(",", ew.Select(x=>x.ToString("X")))}] NS=[{string.Join(",", ns.Select(x=>x.ToString("X")))}] mapHasMe={map.ContainsKey(me)} mapHasPt={map.ContainsKey(partner)}");
                        var mePos = GetObjPos(sa, me);
                        if (mePos != Vector3.Zero)
                        {
                            float d2a = DistXZ2(mePos, EW_A);
                            float d2b = DistXZ2(mePos, EW_B);
                            go = d2a <= d2b ? EW_A : EW_B;
                        }
                    }
                }

                sa.tts("东西安全 去点位", TTSMode, TTSOpen);
            }
            else
            {
                // - 优先看谁连 UP：连 UP 的去 NS_A，另一个去 NS_B
                // - 如果两个人都不连 UP，则看谁连 DOWN：连 DOWN 的去 NS_B，另一个去 NS_A
                bool meUp = myLink.Key == P3BottomKey.Up;
                bool ptUp = ptLink.Key == P3BottomKey.Up;

                if (meUp || ptUp)
                {
                    go = meUp ? NS_A : NS_B;
                }
                else
                {
                    bool meDown = myLink.Key == P3BottomKey.Down;
                    bool ptDown = ptLink.Key == P3BottomKey.Down;

                    if (meDown || ptDown)
                        go = meDown ? NS_B : NS_A;
                    else
                    {
                        sa.Method.SendChat($"/e [O2ERR] me=0x{me:X} iAmEW={iAmEW} iAmNS={iAmNS} partner=0x{partner:X} " +
                            $"myKey={myLink.Key} myBottom=0x{myLink.BottomOrbId:X} myPos=({myLink.BottomPos.X:0.0},{myLink.BottomPos.Z:0.0}) " +
                            $"ptKey={ptLink.Key} ptBottom=0x{ptLink.BottomOrbId:X} ptPos=({ptLink.BottomPos.X:0.0},{ptLink.BottomPos.Z:0.0}) " +
                            $"EW=[{string.Join(",", ew.Select(x=>x.ToString("X")))}] NS=[{string.Join(",", ns.Select(x=>x.ToString("X")))}] mapHasMe={map.ContainsKey(me)} mapHasPt={map.ContainsKey(partner)}");
                        // ===== dump map (member -> bottom) =====
                        try
                        {
                            var lines = map
                                .OrderBy(k => k.Key)
                                .Select(kv =>
                                    $"m=0x{kv.Key:X} key={kv.Value.Key} bottom=0x{kv.Value.BottomOrbId:X} " +
                                    $"pos=({kv.Value.BottomPos.X:0.0},{kv.Value.BottomPos.Z:0.0})"
                                )
                                .ToList();

                            // sa.Method.SendChat($"/e [O2MAP] count={lines.Count}");
                            // foreach (var s in lines)
                                // sa.Method.SendChat($"/e [O2MAP] {s}");
                        }
                        catch { }
                        var mePos = GetObjPos(sa, me);
                        if (mePos != Vector3.Zero)
                        {
                            float d2a = DistXZ2(mePos, NS_A);
                            float d2b = DistXZ2(mePos, NS_B);
                            go = d2a <= d2b ? NS_A : NS_B;
                        }
                    }
                }

                sa.tts("南北安全 去点位", TTSMode, TTSOpen);
            }

            if (go == Vector3.Zero) return;

            DrawWaypointToMe(sa, go, 6500, "P3_四方凶兆2_指路");

            await Task.Delay(5000);
            _phase = 4;
            sa.tts("天界交叉斩准备", TTSMode, TTSOpen);
        });
    }

    [ScriptMethod(
        name: "剑术大师-P3-四方凶兆3-灵击波记录",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(46749)$"],
        userControl: false)]
    public void 剑术大师_P3_四方凶兆3_灵击波记录(Event evt, ScriptAccessory sa)
    {
        if (_phase != 4) return;

        var pos = evt.SourcePosition();
        if (pos == Vector3.Zero) return;

        long now = Environment.TickCount64;

        // 1) 先尽量记录四个方向的 SourcePosition
        if (TryClassifyWaveDir(pos, out var dir))
        {
            lock (_p3O3Lock)
            {
                _p3O3WavePos[dir] = pos;
            }
        }

        // 2) 3秒内只允许调度一次
        bool shouldSchedule = false;
        lock (_p3O3Lock)
        {
            if (now - _p3O3LastExecMs < 3000) return;

            if (!_p3O3TaskScheduled)
            {
                _p3O3TaskScheduled = true;
                shouldSchedule = true;
            }
        }
        if (!shouldSchedule) return;
        // if (_四方凶兆3收尾) return;

        _ = Task.Run(async () =>
        {
            try
            {
                const uint SE = 4777u; // 东南危险
                const uint SW = 4778u; // 西南危险
                const uint NE = 4781u; // 东北危险
                const uint NW = 4782u; // 西北危险

                var myStatuses = GetMyStatusIdsSafe(sa);
                bool se = myStatuses.Contains(SE);
                bool sw = myStatuses.Contains(SW);
                bool ne = myStatuses.Contains(NE);
                bool nw = myStatuses.Contains(NW);

                // 没有四个buff之一 -> 不画
                if (!se && !sw && !ne && !nw)
                {
                    ClearP3O3();
                    return;
                }

                // 4) 等 200ms，让其它 SourcePosition 都进来
                await Task.Delay(200);

                Dictionary<P3O3Dir, Vector3> waveCopy;
                lock (_p3O3Lock)
                {
                    waveCopy = _p3O3WavePos.ToDictionary(k => k.Key, v => v.Value);
                }

                // SE危险 -> 安全NW -> West + North
                // SW危险 -> 安全NE -> East + North
                // NE危险 -> 安全SW -> West + South
                // NW危险 -> 安全SE -> East + South
                P3O3Dir ewNeed, nsNeed;
                if (se) { ewNeed = P3O3Dir.West; nsNeed = P3O3Dir.North; }
                else if (sw) { ewNeed = P3O3Dir.East; nsNeed = P3O3Dir.North; }
                else if (ne) { ewNeed = P3O3Dir.West; nsNeed = P3O3Dir.South; }
                else /*nw*/  { ewNeed = P3O3Dir.East; nsNeed = P3O3Dir.South; }

                if (!waveCopy.TryGetValue(ewNeed, out var ewPos) || !waveCopy.TryGetValue(nsNeed, out var nsPos))
                {
                    // 没收齐坐标就不画，避免误导
                    ClearP3O3();
                    return;
                }

                // 读取收尾标记 + 第四轮额外危险
                bool tail;
                P3O3Dir? fourthDanger;
                lock (_p3O3Lock)
                {
                    tail = _四方凶兆3收尾;
                    fourthDanger = _p3O3FourthDanger;
                }

                // =====================
                // 收尾逻辑：第四轮固定只有一个安全方向
                // =====================
                if (tail)
                {
                    sa.tts("去边上", TTSMode, TTSOpen);
                    // 1) 把“象限危险”拆成四向危险
                    var dangerSet = new HashSet<P3O3Dir>();
                    AddQuadrantDanger(dangerSet, se, sw, ne, nw);

                    // 2) 叠加第四轮额外危险（North/South）
                    if (fourthDanger.HasValue)
                        dangerSet.Add(fourthDanger.Value);

                    // sa.Method.SendChat($"/e [P3O3DBG] dangerSet=[{string.Join(",", dangerSet.Select(d => d.ToString()))}] " +
                    //     $"se={se} sw={sw} ne={ne} nw={nw} fourth={fourthDanger?.ToString() ?? "null"} p303={_p3O3FourthDanger}");
                    sa.Debug($"[P3O3DBG] dangerSet=[{string.Join(",", dangerSet.Select(d => d.ToString()))}] " + 
                            $"se={se} sw={sw} ne={ne} nw={nw} fourth={fourthDanger?.ToString() ?? "null"} p303={_p3O3FourthDanger}", 是否展示调试信息);

                    // 3) 找唯一安全方向
                    var safeDirs = AllDirs().Where(d => !dangerSet.Contains(d)).ToList();
                    if (safeDirs.Count != 1)
                    {
                        // 不符合“唯一安全”的预期 -> 兜底：回退到原本的“去安全角”逻辑
                    }
                    else
                    {
                        var safeDir = safeDirs[0];

                        // 先找 safeDir 那一边的灵击波
                        if (!waveCopy.TryGetValue(safeDir, out var safeWavePos))
                        {
                            sa.Debug($"[P3O3DBG] missing safeDir wave: {safeDir}", 是否展示调试信息);
                            ClearP3O3();
                            return;
                        }

                        // 四个固定安全点
                        var candidatePoints = new Dictionary<P3O3Dir, Vector3>
                        {
                            [P3O3Dir.East]  = new Vector3(155.00f, -16.00f, -810.00f),
                            [P3O3Dir.West]  = new Vector3(185.00f, -16.00f, -820.00f),
                            [P3O3Dir.North] = new Vector3(175.00f, -16.00f, -800.00f),
                            [P3O3Dir.South] = new Vector3(165.00f, -16.00f, -830.00f),
                        };

                        // safeDir 是东西时，看 Z；safeDir 是南北时，看 X
                        bool safeIsEW = (safeDir == P3O3Dir.East || safeDir == P3O3Dir.West);
                        float otherCoord = safeIsEW ? safeWavePos.Z : safeWavePos.X;

                        // 只在同轴的两个候选点里选“另一轴最接近”的那个
                        List<KeyValuePair<P3O3Dir, Vector3>> candidates;
                        if (safeDir == P3O3Dir.East || safeDir == P3O3Dir.West)
                        {
                            candidates = candidatePoints
                                .Where(kv => kv.Key == P3O3Dir.East || kv.Key == P3O3Dir.West)
                                .ToList();
                        }
                        else
                        {
                            candidates = candidatePoints
                                .Where(kv => kv.Key == P3O3Dir.North || kv.Key == P3O3Dir.South)
                                .ToList();
                        }

                        float bestDelta = float.MaxValue;
                        Vector3 safePos = Vector3.Zero;
                        P3O3Dir finalSafeDir = safeDir;

                        foreach (var kv in candidates)
                        {
                            float candidateOtherCoord = safeIsEW ? kv.Value.Z : kv.Value.X;
                            float delta = MathF.Abs(candidateOtherCoord - otherCoord);
                            if (delta < bestDelta)
                            {
                                bestDelta = delta;
                                safePos = kv.Value;
                                finalSafeDir = kv.Key;
                            }
                        }

                        if (safePos == Vector3.Zero)
                        {
                            sa.Debug($"[P3O3DBG] failed to match safe point. safeDir={safeDir} wave=({safeWavePos.X:0.0},{safeWavePos.Z:0.0})", 是否展示调试信息);
                            ClearP3O3();
                            return;
                        }

                        sa.Debug(
                            $"safeDir={safeDir} finalSafeDir={finalSafeDir} safeWave=({safeWavePos.X:0.0},{safeWavePos.Z:0.0}) otherCoord={otherCoord:0.0} safePos=({safePos.X:0.0},{safePos.Z:0.0})",
                            是否展示调试信息
                        );

                        // 画固定安全点 rect
                        var dp2 = sa.Data.GetDefaultDrawProperties();
                        dp2.Name = $"P3_凶兆3_第四轮安全Rect_{Environment.TickCount64}";
                        dp2.Owner = 0;
                        dp2.Position = safePos - new Vector3(5f,0f,0f);
                        dp2.Rotation = MathF.PI / 2f;
                        dp2.DestoryAt = 3500;
                        dp2.Color = sa.Data.DefaultSafeColor;
                        dp2.ScaleMode = ScaleMode.None;
                        dp2.Scale = new Vector2(10f, 10f);

                        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp2);
                    }
                    return;
                }

                var center = ComposeCenter(ewPos, nsPos) - new Vector3(0, 0, 5f);

                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"P3_凶兆3_安全Rect_{Environment.TickCount64}";
                dp.Owner = 0;
                dp.Position = center;
                dp.Rotation = 0f;
                dp.DestoryAt = 4000;
                dp.Color = sa.Data.DefaultSafeColor;
                dp.ScaleMode = ScaleMode.None;
                dp.Scale = new Vector2(10f, 10f);

                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp);
                sa.tts("去安全角", TTSMode, TTSOpen);

                // 收尾本轮
                lock (_p3O3Lock)
                {
                    _p3O3LastExecMs = Environment.TickCount64;
                    _p3O3TaskScheduled = false;
                    _p3O3WavePos.Clear();
                }
            }
            catch
            {
                lock (_p3O3Lock)
                {
                    _p3O3TaskScheduled = false;
                }
            }
        });
    }

    private List<uint> GetMyStatusIdsSafe(ScriptAccessory sa)
    {
        try
        {
            uint me = sa.Data.Me;
            var obj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == me);
            if (obj == null) return new List<uint>();

            // 常见状态列表属性名
            var t = obj.GetType();
            object? statusListObj =
                t.GetProperty("StatusList")?.GetValue(obj) ??
                t.GetProperty("Statuses")?.GetValue(obj) ??
                t.GetProperty("Status")?.GetValue(obj);

            if (statusListObj is not System.Collections.IEnumerable enumerable)
                return new List<uint>();

            var result = new List<uint>();

            foreach (var s in enumerable)
            {
                if (s == null) continue;
                var st = s.GetType();

                object? idObj =
                    st.GetProperty("StatusId")?.GetValue(s) ??
                    st.GetProperty("StatusID")?.GetValue(s) ??
                    st.GetProperty("Id")?.GetValue(s) ??
                    st.GetProperty("ID")?.GetValue(s);

                if (idObj == null) continue;

                try
                {
                    result.Add(Convert.ToUInt32(idObj));
                }
                catch
                {
                    // ignore
                }
            }

            return result;
        }
        catch
        {
            return new List<uint>();
        }
    }

    [ScriptMethod(
        name: "剑术大师-P3-四方凶兆3-清除绘图",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(46749)$"],
        userControl: false)]
    public void 剑术大师_P3_四方凶兆3_清除绘图(Event evt, ScriptAccessory sa)
    {
        if (_phase != 4) return;

        sa.Method.RemoveDraw($"^P3_凶兆3_安全Rect_.*$");
        ClearP3O3();
    }
    
    [ScriptMethod(
        name: "剑术大师-P3-四方凶兆3-第四轮",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:regex:^(0165|0166|0167|0168)$"])]
    public void 剑术大师_P3_四方凶兆3_第四轮(Event evt, ScriptAccessory sa)
    {
        // sa.Method.SendChat($"/e [O3-4] id={evt["Id"]} src=0x{evt.SourceId():X} tgt=0x{evt.TargetId():X} ticks={evt.DateTime.Ticks} ms={evt.DateTime.Millisecond}");
        if (_phase != 4) return;

        uint me = sa.Data.Me;
        if (me == 0) return;

        uint tid = evt.SourceId();
        if (tid == 0 || tid != me) return;
        string idStr = evt["Id"] ?? "";

        P3O3Dir? danger = null;
        if (idStr.Equals("0165", StringComparison.OrdinalIgnoreCase))
            danger = P3O3Dir.North; // 北凶
        else if (idStr.Equals("0168", StringComparison.OrdinalIgnoreCase))
            danger = P3O3Dir.South; // 南凶
        else if (idStr.Equals("0166", StringComparison.OrdinalIgnoreCase))
            danger = P3O3Dir.East; // 东凶
        else if (idStr.Equals("0167", StringComparison.OrdinalIgnoreCase))
            danger = P3O3Dir.West; // 西凶
        else
            return;

        lock (_p3O3Lock)
        {
            long t = evt.DateTime.Ticks;

            // 只接受更晚的那条
            if (t >= _p3O3FourthTicks)
            {
                _p3O3FourthTicks = t;
                _p3O3FourthDanger = danger;
                // sa.Method.SendChat($"/e [O3-4] ticks={t} 已更新危险区danger={_p3O3FourthDanger}");
                sa.Debug($"{danger}", 是否展示调试信息);
            }
        }
    }

    [ScriptMethod(
        name: "剑术大师-P4-落石运动会-集合",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:regex:^(028D)$"])]
    public void 剑术大师_P4_落石运动会_集合(Event evt, ScriptAccessory sa)
    {
        if (_phase < 4) return;

        uint me = sa.Data.Me;
        if (me == 0) return;

        uint tid = evt.TargetId();
        if (tid == 0 || tid != me) return; // 只对自己被点名时提示

        sa.tts("疾跑 中间集合", TTSMode, TTSOpen);
        DrawWaypointToMe(sa, P4Gather, 3500, $"P4_落石_集合_{Environment.TickCount64}");
    }

    [ScriptMethod(
        name: "剑术大师-P4-落石运动会-拉线",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:regex:^(00A3)$"])]
    public void 剑术大师_P4_落石运动会_拉线(Event evt, ScriptAccessory sa)
    {
        if (_phase < 4) return;

        uint me = sa.Data.Me;
        if (me == 0) return;

        uint sid = evt.SourceId();
        uint tid = evt.TargetId();
        if (sid == 0 || tid == 0) return;

        // 只处理与自己有关的 tether
        if (sid != me && tid != me) return;

        // 找到对方
        uint other = (sid == me) ? tid : sid;

        sa.tts("拉线", TTSMode, TTSOpen);

        _phase = 5;
        ResetP4O4();

        int myIdx = sa.MyIndex();
        int otherIdx = sa.Data.PartyList.IndexOf(other);

        if (myIdx < 0 || otherIdx < 0) return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(200);

            // 读取两颗陨石落点，判断 A/B
            List<Vector3> rocks;
            lock (_p4RockLock)
                rocks = _p4RockPositions.ToList();

            bool nearRef = false;
            foreach (var p in rocks)
            {
                if (NearXZ(p, P4RockRef, 1.0f))
                {
                    nearRef = true;
                    break;
                }
            }

            Vector3 p02 = nearRef ? P4_A_02 : P4_B_02;
            Vector3 p13 = nearRef ? P4_A_13 : P4_B_13;

            bool otherHigher = otherIdx > myIdx;
            Vector3 go = otherHigher ? p02 : p13;

            DrawWaypointToMe(
                sa,
                go,
                5500,
                $"P4_落石_拉线指路_{(nearRef ? "A" : "B")}_{(otherHigher ? "ME13" : "ME02")}_{Environment.TickCount64}"
            );
        });
    }

    [ScriptMethod(
        name: "剑术大师-P4-落石运动会-记录陨石位置",
        eventType: EventTypeEnum.SetObjPos,
        eventCondition: ["SourceDataId:19229"],
        userControl: false)]
    public void 剑术大师_P4_落石运动会_记录陨石位置(Event evt, ScriptAccessory sa)
    {
        if (_phase < 4) return;
        var pos = evt.SourcePosition();
        if (pos == Vector3.Zero) return;
        lock (_p4RockLock)
        {
            foreach (var p in _p4RockPositions)
                if (NearXZ(p, pos, P4RockEps))
                    return;

            if (_p4RockPositions.Count >= 2) return;
            _p4RockPositions.Add(pos);
        }
    }


    [ScriptMethod(
        name: "剑术大师-P4-四方凶兆4-灵击波(找落石挡箭头)",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47763"])]
    public void 剑术大师_P4_四方凶兆4_灵击波(Event evt, ScriptAccessory sa)
    {
        if (_phase != 5) return;

        var pos = evt.SourcePosition();
        if (pos == Vector3.Zero) return;

        // 1) 分类到 East/West/North/South
        if (!TryClassifyP4O4Dir(pos, out var dir))
            return;

        // 2) 写入 wavePos
        bool shouldSchedule = false;
        lock (_p4O4Lock)
        {
            _p4O4WavePos[dir] = pos;

            if (!_p4O4TaskScheduled)
            {
                _p4O4TaskScheduled = true;
                shouldSchedule = true;
            }
        }
        if (!shouldSchedule) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500);
                var myStatuses = GetMyStatusIdsSafe(sa);
                if (!TryGetP4O4SafeDir(myStatuses, out var safeDir))
                {
                    sa.tts("没读到安全方向buff", TTSMode, TTSOpen);
                    lock (_p4O4Lock) { _p4O4TaskScheduled = false; }
                    return;
                }

                bool safeIsEW = (safeDir == P3O3Dir.East || safeDir == P3O3Dir.West);

                // ===== B) 等齐需要的方向=====
                Dictionary<P3O3Dir, Vector3> wave = new();
                for (int i = 0; i < 12; i++) // 12*100=1200ms
                {
                    lock (_p4O4Lock)
                        wave = _p4O4WavePos.ToDictionary(k => k.Key, v => v.Value);

                    bool hasSafe = wave.ContainsKey(safeDir);
                    bool hasNS = wave.ContainsKey(P3O3Dir.North) && wave.ContainsKey(P3O3Dir.South);
                    bool hasEW = wave.ContainsKey(P3O3Dir.East)  && wave.ContainsKey(P3O3Dir.West);

                    if (hasSafe && (safeIsEW ? hasNS : hasEW))
                        break;

                    await Task.Delay(100);
                }

                // ===== C) copy rocks =====
                List<Vector3> rocks;
                lock (_p4RockLock)
                    rocks = _p4RockPositions.ToList();

                if (rocks.Count < 2)
                {
                    sa.tts("没记录到两颗落石", TTSMode, TTSOpen);
                    lock (_p4O4Lock) { _p4O4TaskScheduled = false; }
                    return;
                }

                if (!wave.TryGetValue(safeDir, out var safeWavePos))
                {
                    sa.tts("没抓到安全边灵击波", TTSMode, TTSOpen);
                    lock (_p4O4Lock) { _p4O4TaskScheduled = false; }
                    return;
                }

                // 安全东/西（靠 X 边） -> 取 Z
                // 安全南/北（靠 Z 边） -> 取 X
                float innerCoord = safeIsEW ? safeWavePos.Z : safeWavePos.X;

                // ===== E) 选出更匹配的落石 =====
                var chosenRock = PickRockByAxis(rocks, compareZ: safeIsEW, innerCoord: innerCoord);
                if (chosenRock == Vector3.Zero)
                {
                    lock (_p4O4Lock) { _p4O4TaskScheduled = false; }
                    return;
                }

                // ===== F) 用另一组边判断靠哪边，偏移 3 =====
                Vector3 go = chosenRock;

                if (safeIsEW)
                {
                    // 需要 North/South
                    if (!wave.TryGetValue(P3O3Dir.North, out var nPos) ||
                        !wave.TryGetValue(P3O3Dir.South, out var sPos))
                    {
                        sa.tts("没抓到南北灵击波", TTSMode, TTSOpen);
                        // sa.Method.SendChat($"/e [P4O4] missing NS, keys=[{string.Join(",", wave.Keys)}]");
                        lock (_p4O4Lock) { _p4O4TaskScheduled = false; }
                        return;
                    }

                    float dN = MathF.Abs(chosenRock.X - nPos.X);
                    float dS = MathF.Abs(chosenRock.X - sPos.X);

                    // 南更近 -> 往北 5（Z 更小）
                    // 北更近 -> 往南 5（Z 更大）
                    go = (dS <= dN) ? chosenRock + new Vector3(0, 0, -4f)
                                    : chosenRock + new Vector3(0, 0, +4f);
                }
                else
                {
                    // 需要 East/West
                    if (!wave.TryGetValue(P3O3Dir.East, out var ePos) ||
                        !wave.TryGetValue(P3O3Dir.West, out var wPos))
                    {
                        sa.tts("没抓到东西灵击波", TTSMode, TTSOpen);
                        // sa.Method.SendChat($"/e [P4O4] missing EW, keys=[{string.Join(",", wave.Keys)}]");
                        lock (_p4O4Lock) { _p4O4TaskScheduled = false; }
                        return;
                    }

                    float dE = MathF.Abs(chosenRock.Z - ePos.Z);
                    float dW = MathF.Abs(chosenRock.Z - wPos.Z);

                    go = (dE <= dW) ? chosenRock + new Vector3(-4f, 0, 0)
                                    : chosenRock + new Vector3(+4f, 0, 0);
                }

                // ===== G) 指路 =====
                sa.tts("去落石挡灵击波", TTSMode, TTSOpen);
                DrawWaypointToMe(sa, go, 4500, $"P4_凶兆4_落石指路");

                lock (_p4O4Lock) { _p4O4TaskScheduled = false; }
            }
            catch (Exception ex)
            {
                sa.Method.SendChat($"/e [P4凶兆4] EX={ex.GetType().Name}: {ex.Message}");
                lock (_p4O4Lock) { _p4O4TaskScheduled = false; }
            }
        });
    }
    #endregion
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
            var s = idStr.Replace("0x", "");
            id = uint.Parse(s, System.Globalization.NumberStyles.HexNumber);
            return true;
        }
        catch { return false; }
    }

    public static uint ActionId(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["ActionId"]);
    public static uint SourceId(this Event evt) => ParseHexId(evt["SourceId"], out var id) ? id : 0;
    public static uint TargetId(this Event evt) => ParseHexId(evt["TargetId"], out var id) ? id : 0;
    public static Vector3 SourcePosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["SourcePosition"]);
    public static Vector3 TargetPosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["TargetPosition"]);
}

public static class ScriptAccessoryExtensions
{
    public static int MyIndex(this ScriptAccessory sa) => sa.Data.PartyList.IndexOf(sa.Data.Me);

    public static void tts(this ScriptAccessory sa, string text, MerchantsTale.TTS播报方式 mode, bool open)
    {
        if (!open) return;
        switch (mode)
        {
            case MerchantsTale.TTS播报方式.原生TTS:
                sa.Method.TTS(text);
                break;
            case MerchantsTale.TTS播报方式.EdgeTTS:
                sa.Method.EdgeTTS(text);
                break;
            case MerchantsTale.TTS播报方式.DrTTS:
                sa.Method.SendChat($"/pdr tts {text}");
                break;
        }
    }
    public static void Debug(this ScriptAccessory sa, string text, bool debugOpen)
    {
        if (!debugOpen) return;
        sa.Method.SendChat($"/e {text}");
    }

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

    public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, Vector3 target, uint duration, uint delay = 0, string name = "Waypoint", Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;            // 绑定自己
        dp.TargetPosition = target;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }

    public static DrawPropertiesEdit WaypointFromToDp(this ScriptAccessory sa, Vector3 from, Vector3 to, uint duration, uint delay = 0, string name = "WaypointFromTo", Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = 0;                    // 不绑定任何物体，防止跟着人跑
        dp.Position = from;              // 起点
        dp.TargetPosition = to;          // 终点
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }
}

#endregion