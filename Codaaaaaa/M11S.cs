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

namespace Codaaaaaa.M11S;

[ScriptType(
    guid: "6f3d1b82-9d44-4c5a-8277-3a8f5c0f2b1e",
    name: "M11S补充画图",
    territorys: [1325],
    version: "0.0.1.3",
    author: "Codaaaaaa",
    note: "设置里面改打法，但目前支持的不是很多有很大概率被电。\n- 该脚本只对RyougiMio佬的画图更新前做指路补充，需要配合使用。\n- 谢谢灵视佬和7dsa1wd1s佬提供的arr")]
public class M11S
{
    #region 用户设置
    // [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    [UserSetting("铸兵之令统治打法")] public 铸兵之令统治打法 铸兵之令统治打法选择 { get; set; } = 铸兵之令统治打法.近战固定法;
    [UserSetting("王者陨石L改踩塔打法")] public 王者陨石踩塔打法 王者陨石踩塔打法选择 { get; set; } = 王者陨石踩塔打法.tndd;
    [UserSetting("王者陨石L改踩塔击飞打法")] public 王者陨石击飞打法 王者陨石踩塔击飞打法选择 { get; set; } = 王者陨石击飞打法.同平台;
    [UserSetting("陨石狂奔打法")] public 陨石狂奔打法 陨石狂奔打法选择 { get; set; } = 陨石狂奔打法.十引导;
    // [UserSetting("流星雨打法")] public 流星雨打法 流星雨打法选择 { get; set; } = 流星雨打法.奶远近;
    [UserSetting("六连风圈高亮颜色")] public ScriptColor 六连风圈高亮颜色 { get; set; } = new ScriptColor() { V4 = new Vector4(0.9803922f, 0.4313726f, 0.1960784f, 0.35f) }; // 默认淡蓝

    #endregion

    public enum 流星雨打法
    {
        奶远近
    }
    
    public enum 铸兵之令统治打法
    {
        近战固定法,
    }
    public enum 王者陨石踩塔打法
    {
        近近远远,
        tndd
    }
    public enum 王者陨石击飞打法
    {
        同平台,
        闲人斜飞
    }
    public enum 陨石狂奔打法
    {
        十引导,
        X引导,
    }

    private enum Corner
    {
        左上,
        右上,
        左下,
        右下,
        未设定
    }


    private static readonly Vector3[] DomPoints = new[]
    {
        new Vector3(85f,  0f,  100f),   // 点0
        new Vector3(115f, 0f,  100f),   // 点1
        new Vector3(100f,  0f,  115f),  // 点2
        new Vector3(100f, 0f,  85f),  // 点3
    };

    private static readonly Vector3[][] SafeByMissing近固 = new[]
    {
        // missing = 0 (DomPoints[0] 没出现)
        new[]
        {
            new Vector3(100.14f, 0f, 91.23f), // group0: myIdx 0/4
            new Vector3(99.89f, 0f, 108.96f), // group1: myIdx 1/5
            new Vector3(85.21f, 0f, 94.08f), // group2: myIdx 2/6
            new Vector3(85.49f, 0f, 106.01f), // group3: myIdx 3/7
        },

        // missing = 1 (DomPoints[1] 没出现)
        new[]
        {
            new Vector3(100.14f, 0f, 91.23f), // group0: myIdx 0/4
            new Vector3(99.89f, 0f, 108.96f), // group1: myIdx 1/5
            new Vector3(114.51f, 0f, 106.01f), // group2: myIdx 2/6
            new Vector3(114.51f, 0f, 94.08f), // group3: myIdx 3/7
        },

        // missing = 2 (DomPoints[2] 没出现)
        new[]
        {
            new Vector3(91.25f, 0f, 100.08f),
            new Vector3(108.63f, 0f, 99.68f),
            new Vector3(94.15f, 0f, 114.20f),
            new Vector3(105.70f, 0f, 114.08f),
        },

        // missing = 3 (DomPoints[3] 没出现)
        new[]
        {
            new Vector3(91.25f, 0f, 100.08f),
            new Vector3(108.63f, 0f, 99.68f),
            new Vector3(105.70f, 0f, 85.92f), 
            new Vector3(94.15f, 0f, 85.92f),
        },
    };

    private static readonly IReadOnlyDictionary<Corner, Vector3[]> 王者陨石塔位置
        = new Dictionary<Corner, Vector3[]>
    {
        [Corner.左上] = new[]
        {
            new Vector3(86.10f, 0f, 88.73f), // 拉线踩塔
            new Vector3(83.94f, 0f, 91.43f), // 闲人踩塔同平台
            new Vector3(86.94f, 0f, 90.25f), // 闲人踩塔斜飞
        },
        [Corner.右上] = new[]
        {
            new Vector3(113.05f, 0f, 88.90f),
            new Vector3(116.02f, 0f, 91.73f),
            new Vector3(113.02f, 0f, 90.22f),
        },
        [Corner.左下] = new[]
        {
            new Vector3(86.74f, 0f, 110.78f),
            new Vector3(84.40f, 0f, 108.26f),
            new Vector3(86.94f, 0f, 109.79f),
        },
        [Corner.右下] = new[]
        {
            new Vector3(113.40f, 0f, 111.06f),
            new Vector3(116.06f, 0f, 108.47f),
            new Vector3(113.05f, 0f, 109.78f),
        },
    };
    private static readonly IReadOnlyDictionary<Corner, Vector3[]> 王者陨石火圈引导美
        = new Dictionary<Corner, Vector3[]>
    {
        [Corner.左上] = new[]
        {
            new Vector3(92.86f, 0f, 97.76f),
            new Vector3(92.47f, 0f, 91.70f),
            new Vector3(92.41f, 0f, 85.55f),
        },
        [Corner.右上] = new[]
        {
            new Vector3(107.14f, 0f, 97.76f),
            new Vector3(107.53f, 0f, 91.70f),
            new Vector3(107.59f, 0f, 85.55f),
        },
        [Corner.左下] = new[]
        {
            new Vector3(92.86f, 0f, 102.24f),
            new Vector3(92.47f, 0f, 108.30f),
            new Vector3(92.41f, 0f, 114.45f),
        },
        [Corner.右下] = new[]
        {
            new Vector3(107.14f, 0f, 102.24f),
            new Vector3(107.53f, 0f, 108.30f),
            new Vector3(107.59f, 0f, 114.45f),
        },
    };

    // 匹配固定点的容差（只看 XZ）
    private const float DomPointEps = 1.2f;

    // 收集机制触发的状态（线程安全）
    private readonly object _domLock = new();
    private int _domMask = 0;     // 4bit：出现过哪些点
    private int _domCount = 0;    // 已收到几次（目标 6 次）
    private int _domSeq = 0;      // 每轮机制序号（防止上一轮 Task 影响下一轮）
    private long _domLastTick = 0;

    // 王者陨石拉线记录
    private bool 王者陨石是否有拉线Buff = false;
    private Vector3 王者陨石陨石Pos = Vector3.Zero;
    private Corner 王者陨石下一次Corner = Corner.未设定;

    private readonly object _meteorLock = new();
    private CancellationTokenSource _meteorCts = new();
    private int _meteorSeq = 0;
    

    // 换P
    private int _phase = 1;

    // 陨石狂奔：拉线只触发一次
    private bool _runMeteorTetherTriggered = false;
    private readonly object _runMeteorLock = new();
    private readonly List<uint> _runMeteorFireTargets = new();   // 每轮 2 个火圈 TargetId
    private readonly List<Vector3> _runMeteorPositions = new();  // 每轮 2 个陨石位置

    private const float RunMeteorPosEps = 0.35f;

    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa) => _phase = 1;

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2;

    #region StarTrack

    // ==================== [Star Track] Config ====================
    private const int StarTrackActionId = 46131;
    private bool _stGuideIssued = false; // 本轮是否已经提前出过指路

    private const int TotalCasts = 8;   // 8 次炮线
    private const int PairCount  = 4;   // 两两一组 -> 4 个交点（标 1..4）

    // 4x4 网格映射
    private const float GridMinXZ = 80f;   // NW 角最小 X/Z
    private const float CellSize  = 10f;   // 每格 10

    // 去重参数
    private const int   DuplicateWindowMs     = 300;
    private const float DuplicatePosTolerance = 0.2f;
    private const float DuplicateRotTolerance = 0.05f;

    private static readonly Vector3 PtSW = new(98.52f, 0f, 100.68f); // 左下
    private static readonly Vector3 PtNW = new(98.54f, 0f,  99.25f); // 左上
    private static readonly Vector3 PtNE = new(101.55f, 0f,  99.35f); // 右上
    private static readonly Vector3 PtSE = new(101.28f, 0f, 100.61f); // 右下

    // 半透明黄色
    private static readonly Vector4 Yellow50 = new(1f, 1f, 0f, 0.5f);

    // ==================== [Star Track] State ====================
    private readonly object _stLock = new();

    private readonly record struct StarCast(long TimeMs, uint SourceId, Vector3 Origin, float Rot);
    private readonly record struct DedupInfo(long TimeMs, Vector3 Pos, float Rot);

    private int _stCastSeq = 0; // 1..8
    private readonly List<StarCast> _stCasts = new(TotalCasts);
    private readonly List<Vector3> _stIntersections = new(PairCount);
    private readonly int[,] _stGrid = new int[4, 4];                 // 4x4 结果网格（0/1..4）
    private readonly Dictionary<uint, DedupInfo> _stLastBySource = new();

    private readonly int[,] _stLastSnapshot = new int[4, 4];

    // ==================== [Beastflame/Tail] Gate ====================
    // 兽焰连尾击：记录面向，用来 gate 星轨链指路
    private readonly object _beastLock = new();
    private bool _beastFacingAC = true;
    private float _beastLastRot = 0f;
    private long _beastLastMs = 0;

    // 容忍角度：0.35rad ~ 20°
    private const float BeastFacingTol = 0.35f;

    // 仅在最近 N ms 内记录有效（避免上一轮的“坏面向”影响下一轮）
    private const int BeastFacingValidMs = 15000;

    // 默认：C = +Z (rot ~ 0)，A = -Z (rot ~ PI)
    private const float AngleC = 0f;
    private const float AngleA = MathF.PI;
    private bool BeastflameAllowsStarTrack(long nowMs)
    {
        lock (_beastLock)
        {
            if (_beastLastMs == 0) return true; // 没记录到就不拦
            if (nowMs - _beastLastMs > BeastFacingValidMs) return true; // 记录过旧就不拦
            return _beastFacingAC;
        }
    }

    private static bool IsFacingAngle(float rot, float target, float tol)
        => MathF.Abs(NormalizeAngle(rot - target)) <= tol;

    // ==================== [Star Track] Utils ====================
    private static float NormalizeAngle(float a)
    {
        float twoPi = MathF.PI * 2f;
        while (a > MathF.PI) a -= twoPi;
        while (a < -MathF.PI) a += twoPi;
        return a;
    }

    // FFXIV 常见旋转：rot=0 朝 +Z
    private static Vector2 DirXZ(float rot) => new(MathF.Sin(rot), MathF.Cos(rot));
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static bool TryIntersectXZ(Vector3 o1, float r1, Vector3 o2, float r2, out Vector3 hit)
    {
        Vector2 p1 = new(o1.X, o1.Z);
        Vector2 p2 = new(o2.X, o2.Z);
        Vector2 d1 = DirXZ(r1);
        Vector2 d2 = DirXZ(r2);

        float denom = Cross(d1, d2);
        if (MathF.Abs(denom) < 1e-4f)
        {
            hit = default;
            return false;
        }

        Vector2 diff = p2 - p1;
        float t = Cross(diff, d2) / denom;

        Vector2 ip = p1 + d1 * t;
        hit = new Vector3(ip.X, 0f, ip.Y);
        return true;
    }

    // World(XZ) -> Grid(row,col); row=0 是“上/北”(Z小), col=0 是“左/西”(X小)
    private static (int row, int col) WorldToGrid(Vector3 p)
    {
        int col = (int)MathF.Floor((p.X - GridMinXZ) / CellSize);
        int row = (int)MathF.Floor((p.Z - GridMinXZ) / CellSize);
        col = Math.Clamp(col, 0, 3);
        row = Math.Clamp(row, 0, 3);
        return (row, col);
    }

    private static void ClearGrid(int[,] grid)
    {
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                grid[r, c] = 0;
    }

    private static bool TryFindValue(int[,] grid, int value, out int row, out int col)
    {
        for (int r = 0; r < 4; r++)
        for (int c = 0; c < 4; c++)
        {
            if (grid[r, c] == value)
            {
                row = r;
                col = c;
                return true;
            }
        }
        row = col = -1;
        return false;
    }

    private enum STCorner { NW, NE, SW, SE }

    private static STCorner Opposite(STCorner c) => c switch
    {
        STCorner.NW => STCorner.SE,
        STCorner.NE => STCorner.SW,
        STCorner.SW => STCorner.NE,
        STCorner.SE => STCorner.NW,
        _ => STCorner.NW
    };

    private static Vector3 CornerPoint(STCorner c) => c switch
    {
        STCorner.NW => PtNW,
        STCorner.NE => PtNE,
        STCorner.SW => PtSW,
        STCorner.SE => PtSE,
        _ => PtNW
    };

    // 2 在中心 2x2 的哪个格 -> 对应哪个角
    // [1][1]=左上，[1][2]=右上，[2][1]=左下，[2][2]=右下
    private static STCorner? Inner2x2ToCorner(int row, int col)
    {
        if (row == 1 && col == 1) return STCorner.NW;
        if (row == 1 && col == 2) return STCorner.NE;
        if (row == 2 && col == 1) return STCorner.SW;
        if (row == 2 && col == 2) return STCorner.SE;
        return null;
    }

    private static bool IsCornerCell(int r, int c)
        => (r == 0 || r == 3) && (c == 0 || c == 3);

    private static bool IsEdgeCell(int r, int c)
        => (r == 0 || r == 3 || c == 0 || c == 3) && !IsCornerCell(r, c);

    // ==================== [Star Track] Reset & Snapshot ====================
    private void ResetStarTrackState()
    {
        lock (_stLock)
        {
            _stCastSeq = 0;
            _stCasts.Clear();
            _stIntersections.Clear();
            _stLastBySource.Clear();
            ClearGrid(_stGrid);
            _stGuideIssued = false;
        }
    }

    private void SnapshotGrid()
    {
        lock (_stLock)
        {
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    _stLastSnapshot[r, c] = _stGrid[r, c];
        }
    }

    // ==================== [Star Track] Draw wrappers（全用 Displacement） ====================
    private void DrawWpToMe(ScriptAccessory sa, string name, Vector3 target, Vector4 color, int delayMs, int durMs)
    {
        var dp = sa.WaypointDp(target, (uint)durMs, (uint)delayMs, $"{name}_{Environment.TickCount64}", color);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private void DrawWpPosToPos(ScriptAccessory sa, string name, Vector3 from, Vector3 to, Vector4 color, int delayMs, int durMs)
    {
        var dp = sa.WaypointFromToDp(from, to, (uint)durMs, (uint)delayMs, $"{name}_{Environment.TickCount64}", color);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    // ==================== [Star Track] Main ====================
    [ScriptMethod(name: "星轨链近战无损指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46131"])]
    public void OnStarTrackCast(Event evt, ScriptAccessory sa)
    {
        long nowMs = Environment.TickCount64;

        // ========= Gate：兽焰连尾击不面向 A/C 则禁用星轨链本轮指路 =========
        if (!BeastflameAllowsStarTrack(nowMs))
        {
            // 清空星轨链状态，防止残留数据导致后续误触发
            lock (_stLock)
            {
                _stCastSeq = 0;
                _stCasts.Clear();
                _stIntersections.Clear();
                _stLastBySource.Clear();
                ClearGrid(_stGrid);
            }
            return;
        }

        uint sourceId = evt.SourceId();
        Vector3 pos = evt.SourcePosition();
        float rot = evt.SourceRotation();   // 下面我在 EventExtensions 里补了这个方法

        // 颜色：正常用 DefaultSafeColor
        var normal = sa.Data.DefaultSafeColor;

        lock (_stLock)
        {
            // ---- 去重：同一 SourceId，短时间 + 位置/角度几乎一致 => 重复
            if (_stLastBySource.TryGetValue(sourceId, out var last))
            {
                bool within = (nowMs - last.TimeMs) <= DuplicateWindowMs;
                bool samePos = Vector3.Distance(last.Pos, pos) <= DuplicatePosTolerance;
                bool sameRot = MathF.Abs(NormalizeAngle(rot - last.Rot)) <= DuplicateRotTolerance;
                if (within && samePos && sameRot) return;
            }
            _stLastBySource[sourceId] = new DedupInfo(nowMs, pos, rot);

            // ---- 收集 cast
            _stCastSeq++;
            _stCasts.Add(new StarCast(nowMs, sourceId, pos, rot));

            // ---- 每两条算一组交点（标号 1..4）
            if (_stCastSeq % 2 == 0 && _stCasts.Count >= 2 && _stIntersections.Count < PairCount)
            {
                var a = _stCasts[_stCasts.Count - 2];
                var b = _stCasts[_stCasts.Count - 1];

                if (TryIntersectXZ(a.Origin, a.Rot, b.Origin, b.Rot, out var hit))
                {
                    _stIntersections.Add(hit);
                    int idx = _stIntersections.Count; // 1..4
                    var (row, col) = WorldToGrid(hit);
                    _stGrid[row, col] = idx;

                    if (!_stGuideIssued && _stIntersections.Count >= 2)
                    {
                        _stGuideIssued = true;

                        var gridCopyEarly = new int[4, 4];
                        for (int rr = 0; rr < 4; rr++)
                            for (int cc = 0; cc < 4; cc++)
                                gridCopyEarly[rr, cc] = _stGrid[rr, cc];

                        sa.Method.SendChat("/e 记录两个 指路");
                        Task.Run(() => ResolveStarTrackGuide(sa, gridCopyEarly, normal, 3000));
                    }
                }
            }

            // ---- 8 次齐了：做指路逻辑 + snapshot + reset
            if (_stCastSeq >= TotalCasts)
            {
                int[,]? gridCopyEnd = null;

                if (!_stGuideIssued)
                {
                    gridCopyEnd = new int[4, 4];
                    for (int r = 0; r < 4; r++)
                        for (int c = 0; c < 4; c++)
                            gridCopyEnd[r, c] = _stGrid[r, c];
                }

                SnapshotGrid();
                ResetStarTrackState();

                if (gridCopyEnd != null)
                {
                    sa.Method.SendChat("/e 保底 指路");
                    Task.Run(() => ResolveStarTrackGuide(sa, gridCopyEnd, normal));
                }                
            }
        }
    }

    // ==================== [Star Track] 3 Cases ====================
    private void ResolveStarTrackGuide(ScriptAccessory sa, int[,] grid, Vector4 normalColor, int delay = 0)
    {
        if (!TryFindValue(grid, 1, out int r1, out int c1)) return;
        if (!TryFindValue(grid, 2, out int r2, out int c2)) return;

        // --------------------
        // Case 1: 1 在 [2][1] 或 [1][2]
        // --------------------
        if ((r1 == 2 && c1 == 1) || (r1 == 1 && c1 == 2))
        {
            // 1在[2][1] -> 起跑点=右上(NE) -> 黄线 NE->SW(4s) -> 4s后指路到SW
            // 1在[1][2] -> 起跑点=左下(SW) -> 黄线 SW->NE(4s) -> 4s后指路到NE
            STCorner startCorner = (r1 == 2 && c1 == 1) ? STCorner.NE : STCorner.SW;
            STCorner endCorner   = Opposite(startCorner);

            Vector3 start = CornerPoint(startCorner);
            Vector3 end   = CornerPoint(endCorner);

            DrawWpToMe(sa, "ST_C1_Start", start, normalColor, 0, 1500+delay);
            DrawWpPosToPos(sa, "ST_C1_Preview", start, end, Yellow50, 0, 1500+delay);
            DrawWpToMe(sa, "ST_C1_End", end, normalColor, 1500+delay, 1500);
            return;
        }

        // --------------------
        // Case 2: 1 在四角
        // --------------------
        if (IsCornerCell(r1, c1))
        {
            var targetOpt = Inner2x2ToCorner(r2, c2);
            if (!targetOpt.HasValue) return;

            STCorner target = targetOpt.Value;     // 2 对应的“目标角”
            STCorner start  = Opposite(target);    // 起跑点=目标角对角

            Vector3 startPos  = CornerPoint(start);
            Vector3 targetPos = CornerPoint(target);

            DrawWpToMe(sa, "ST_C2_Start", startPos, normalColor, 0, 3000+delay);
            DrawWpPosToPos(sa, "ST_C2_Preview", startPos, targetPos, Yellow50, 0, 3000+delay);
            DrawWpToMe(sa, "ST_C2_Target", targetPos, normalColor, 3000+delay, 1500);
            return;
        }

        // --------------------
        // Case 3: 1 在四边（非四角）
        // --------------------
        if (IsEdgeCell(r1, c1))
        {
            // 0-4s   指路去起跑点
            // 0-4s   起跑->对角  黄线预告
            // 4-12s  对角->起跑  黄线预告（持续8s）
            // 4-8s   4s后指路去对角
            // 8-12s  再4s后指路回起跑
            var startCornerOpt = Inner2x2ToCorner(r2, c2);
            if (startCornerOpt == null) return;

            STCorner startCorner = startCornerOpt.Value;
            STCorner endCorner   = Opposite(startCorner);

            Vector3 start = CornerPoint(startCorner);
            Vector3 end   = CornerPoint(endCorner);

            DrawWpToMe(sa, "ST_C3_Start", start, normalColor, 0, 1500+delay);

            DrawWpPosToPos(sa, "ST_C3_Preview_1", start, end, Yellow50, 0, 1500+delay);
            DrawWpPosToPos(sa, "ST_C3_Preview_2", end, start, Yellow50, 1500+delay, 1500);

            DrawWpToMe(sa, "ST_C3_GoEnd", end, normalColor, 1500+delay, 1500);
            DrawWpToMe(sa, "ST_C3_GoBack", start, normalColor, 3000+delay, 1500);
            return;
        }
    }

    #endregion

    [ScriptMethod(
        name: "兽焰连尾击",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(46072|46128|46073|46129)$"],
        userControl: false)]
    public void 兽焰连尾击记录面向(Event evt, ScriptAccessory sa)
    {
        float rot;
        try
        {
            rot = evt.SourceRotation();
        }
        catch
        {
            return;
        }

        long nowMs = Environment.TickCount64;

        // 面向 C(南,+Z) 或 A(北,-Z)
        bool ok = IsFacingAngle(rot, AngleC, BeastFacingTol) || IsFacingAngle(rot, AngleA, BeastFacingTol);

        lock (_beastLock)
        {
            _beastLastRot = rot;
            _beastLastMs = nowMs;
            _beastFacingAC = ok;
        }

        sa.Method.SendChat($"/e [兽焰连尾击] rot={rot:0.00} => {(ok ? "面向A/C(允许星轨)" : "非A/C(禁用星轨)")} ");
    }


    private (int seq, CancellationToken token) GetMeteorToken()
    {
        lock (_meteorLock)
            return (_meteorSeq, _meteorCts.Token);
    }

    private bool IsMeteorSeqValid(int seq)
    {
        lock (_meteorLock)
            return seq == _meteorSeq;
    }

    private void CancelMeteorTasks()
    {
        lock (_meteorLock)
        {
            _meteorSeq++;
            try { _meteorCts.Cancel(); } catch { }
            try { _meteorCts.Dispose(); } catch { }
            _meteorCts = new CancellationTokenSource();
        }
    }
    public void Init(ScriptAccessory sa)
    {
        ResetAll();
        sa.Method.RemoveDraw(".*");
    }

    private void ResetAll()
    {
        ResetStarTrackState();
        CancelMeteorTasks();

        _phase = 1;
        _runMeteorTetherTriggered = false;
        王者陨石是否有拉线Buff = false;
        王者陨石陨石Pos = Vector3.Zero;
        王者陨石下一次Corner = Corner.未设定;

        lock (_domLock)
        {
            _domMask = 0;
            _domCount = 0;
            _domSeq++;
            _domLastTick = DateTime.UtcNow.Ticks;
        }
        lock (_runMeteorLock)
        {
            _runMeteorFireTargets.Clear();
            _runMeteorPositions.Clear();
        }
        lock (_beastLock)
        {
            _beastFacingAC = true;
            _beastLastRot = 0f;
            _beastLastMs = 0;
        }
    }

    // =========================
    // 统治：6次点名 -> 4点缺1点 -> 指路安全区
    // =========================
    [ScriptMethod(name: "铸兵之令统治指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46112"])]
    public void 铸兵之令统治指路(Event evt, ScriptAccessory sa)
    {
        if (!int.TryParse(evt["DurationMilliseconds"], out var dur)) return;

        var targetPos = evt.TargetPosition;

        int seqLocal;
        bool startJudgeTask = false;

        lock (_domLock)
        {
            // 超时保护：如果距离上一次 > 4秒，认为新一轮开始
            var nowTicks = DateTime.UtcNow.Ticks;
            var dtMs = (nowTicks - _domLastTick) / TimeSpan.TicksPerMillisecond;
            if (dtMs > 4000)
            {
                _domMask = 0;
                _domCount = 0;
                _domSeq++;
            }
            _domLastTick = nowTicks;

            // 第一发进来就开一个延迟判定任务
            if (_domCount == 0)
                startJudgeTask = true;

            var idx = MatchDomPointIndex(targetPos);
            if (idx >= 0)
                _domMask |= (1 << idx);

            _domCount++;
            seqLocal = _domSeq;
        }

        if (startJudgeTask)
        {
            _ = Task.Run(async () =>
            {
                // 等一小会，给 6 次事件进来
                await Task.Delay(250);

                int mask, cnt, seqNow;
                lock (_domLock)
                {
                    mask = _domMask;
                    cnt = _domCount;
                    seqNow = _domSeq;
                }

                // 如果期间 reset/新一轮了，直接丢弃
                if (seqNow != seqLocal) return;

                if (cnt < 6) return;

                var missing = FindMissingIndexFromMask(mask);
                if (missing < 0 || missing > 3) return;

                var myIdx = sa.MyIndex();
                if (myIdx < 0 || myIdx > 7) return;

                var group = myIdx % 4;
                var safe = SafeByMissing近固[missing][group];

                DrawWaypointToMe(sa, safe, dur, "Dom_Waypoint");

                // 本轮收尾：清空（也可以不清，让超时逻辑去清）
                lock (_domLock)
                {
                    _domMask = 0;
                    _domCount = 0;
                    _domSeq++;
                }
            });
        }
    }

    [ScriptMethod(name: "王者陨石指路-开场", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47760"])]
    public void 王者陨石指路开场(Event evt, ScriptAccessory sa)
    {
        if (!int.TryParse(evt["DurationMilliseconds"], out var dur)) return;

        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        // myIdx 奇数去左边，偶数去右边
        var targetPos = myIdx % 2 == 0 ? new Vector3(98.05f, 0f, 99.29f) : new Vector3(102.47f, 0f, 100.09f);

        DrawWaypointToMe(sa, targetPos, dur, "Meteor_Waypoint");
    }

    [ScriptMethod(name: "王者陨石指路-拉线记录buff", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0039"], userControl: false)]
    public void 王者陨石指路拉线记录buff(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        var targetId = evt.TargetId();
        var myId = sa.Data.Me;
        if (targetId != myId) return;

        if (_phase == 1)
        {
            sa.Method.SendChat("/e 记录王者陨石拉线Buff和位置");
            王者陨石是否有拉线Buff = true;
            王者陨石陨石Pos = evt.SourcePosition();
        }
    }

    [ScriptMethod(name: "王者陨石指路-拉线", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:46144"])]
    public async void 王者陨石指路拉线(Event evt, ScriptAccessory sa)
    {
        var (seq, token) = GetMeteorToken();

         _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000, token); // 等buff先触发
                if (!IsMeteorSeqValid(seq)) return;

                var myIdx = sa.MyIndex();
                if (myIdx < 0 || myIdx > 7) return;

                // 先确定第一次踩哪里的塔
                if (王者陨石踩塔打法选择 == 王者陨石踩塔打法.近近远远)
                {
                    王者陨石下一次Corner = myIdx switch
                    {
                        0 or 4 => Corner.左上,
                        1 or 5 => Corner.右上,
                        2 or 6 => Corner.左下,
                        3 or 7 => Corner.右下,
                        _ => Corner.未设定,
                    };
                }
                else if (王者陨石踩塔打法选择 == 王者陨石踩塔打法.tndd)
                {
                    王者陨石下一次Corner = myIdx switch
                    {
                        0 or 2 => Corner.左上,
                        1 or 3 => Corner.右上,
                        4 or 6 => Corner.左下,
                        5 or 7 => Corner.右下,
                        _ => Corner.未设定,
                    };
                }

                // 第一次踩塔循环
                await RunMeteorCycleAsync(sa, seq, token);

                await Task.Delay(6500, token); // 视情况调小/调大
                sa.Method.SendChat("/e 第二次踩塔，出现buff");

                // 你可以按你自己的机制点，在这里等 buff / tetherPos 更新完成
                // （如果你已经在别的事件里更新了 王者陨石是否有拉线Buff / 王者陨石陨石Pos，就只需要等一小会）
                

                // 第二次踩塔循环（此时王者陨石是否有拉线Buff 通常变 true）
                await RunMeteorCycleAsync(sa, seq, token);
            }
            catch (OperationCanceledException)
            {
                // 任务被取消，安全退出
                sa.Method.SendChat("/e 王者陨石指路任务已取消");
            }
            catch (Exception ex)
            {
                sa.Method.SendChat($"/e [M11S] Meteor task crashed: {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    [ScriptMethod(name: "六连风圈指路", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19183"])]
    public async void 六连风圈指路(Event evt, ScriptAccessory sa)
    {

        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        var sourcePos = evt.SourcePosition();
        if (MathF.Abs(sourcePos.X - 100f) > 0.1f || MathF.Abs(sourcePos.Z - 87.97f) > 0.1f)
            return;

        var wPos = myIdx switch
        {
            0 => new Vector3(103.35f, 0.00f, 83.27f),
            1 => new Vector3(93.43f, 0.00f, 116.62f),
            2 => new Vector3(85.91f, 0.00f, 96.18f),
            3 => new Vector3(115.15f, 0.00f, 103.49f),
            4 => new Vector3(95.72f, 0.00f, 83.80f),
            5 => new Vector3(101.60f, 0.00f, 117.08f),
            6 => new Vector3(86.04f, 0.00f, 102.22f),
            7 => new Vector3(116.16f, 0.00f, 96.73f),
            _ => default
        };
        
        DrawWaypointToMe(sa, wPos, 6000, "六连风圈指路");
    }
    
    [ScriptMethod(name: "六连风圈高亮", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19183"])]
    public void 六连风圈高亮(Event evt, ScriptAccessory sa)
    {
        var sourcePos = evt.SourcePosition();

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"六连风圈高亮_{Environment.TickCount64}";
        dp.Position = sourcePos;
        dp.Rotation = 0f;
        dp.DestoryAt = 20000;

        dp.Scale = new Vector2(4f);
        dp.ScaleMode = ScaleMode.None;

        dp.Color = 六连风圈高亮颜色.V4;

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "六连风圈删除", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46120"], userControl: false)]
    public void 六连风圈删除(Event evt, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw("^六连风圈高亮_.*$");
    }

    [ScriptMethod(name: "陨石狂奔-远程组AC火圈BUFF记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:001E"], userControl: false)]
    public void 陨石狂奔远程组AC火圈BUFF记录(Event evt, ScriptAccessory sa)
    {
        if (_phase != 2) return;

        var tid = evt.TargetId();
        if (tid == 0) return;

        lock (_runMeteorLock)
        {
            if (_runMeteorFireTargets.Count >= 2) return;
            if (_runMeteorFireTargets.Contains(tid)) return;

            _runMeteorFireTargets.Add(tid);
            sa.Method.SendChat($"/e [P2] 记录火圈点名 {_runMeteorFireTargets.Count}/2 => 0x{tid:X}");
        }
    }


    [ScriptMethod(name: "陨石狂奔-陨石位置记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46163"], userControl: false)]
    public void 陨石狂奔陨石位置记录(Event evt, ScriptAccessory sa)
    {
        if (_phase != 2) return;

        var pos = evt.SourcePosition();
        if (pos == Vector3.Zero) return;

        lock (_runMeteorLock)
        {
            if (_runMeteorPositions.Count >= 2) return;

            foreach (var p in _runMeteorPositions)
            {
                if (MathF.Abs(p.X - pos.X) < RunMeteorPosEps && MathF.Abs(p.Z - pos.Z) < RunMeteorPosEps)
                    return; // 同点重复触发
            }

            _runMeteorPositions.Add(pos);
            sa.Method.SendChat($"/e [P2] 记录陨石位置 {_runMeteorPositions.Count}/2 => X={pos.X:0.00}, Z={pos.Z:0.00}");
        }
    }

    [ScriptMethod(name: "陨石狂奔指路-远程组AC火圈", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:46162"])]
    public void 陨石狂奔指路远程组AC火圈(Event evt, ScriptAccessory sa)
    {
        if (_phase != 2) return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);

            List<uint> targets;
            List<Vector3> meteors;

            lock (_runMeteorLock)
            {
                targets = _runMeteorFireTargets.ToList();
                meteors = _runMeteorPositions.ToList();
            }

            if (targets.Count < 2) return;
            if (meteors.Count < 2) return;

            var myId = sa.Data.Me;
            if (!targets.Contains(myId)) return; // 只给被点的两个人画

            // 优先级：H1 D3 D4 H2 -> partyIdx: 2 6 7 3
            int[] prio = { 2, 6, 7, 3 };
            int PartyIdxOf(uint id) => sa.Data.PartyList.IndexOf(id);

            var ordered = targets
                .Select(id => new { id, pidx = PartyIdxOf(id) })
                .Where(x => x.pidx >= 0)
                .OrderBy(x =>
                {
                    var k = Array.IndexOf(prio, x.pidx);
                    return k < 0 ? 999 : k;
                })
                .ToList();

            if (ordered.Count < 2) return;

            var highId = ordered[0].id;
            var lowId  = ordered[1].id;

            bool sameXZ =
                MathF.Abs(meteors[0].X - meteors[0].Z) < RunMeteorPosEps &&
                MathF.Abs(meteors[1].X - meteors[1].Z) < RunMeteorPosEps;

            Vector3 highPos, lowPos;
            if (sameXZ)
            {
                highPos = new Vector3(110.04f, 0.00f, 81.11f);
                lowPos  = new Vector3(89.66f,  0.00f, 119.10f);
            }
            else
            {
                highPos = new Vector3(89.36f,  0.00f, 82.47f);
                lowPos  = new Vector3(108.91f, 0.00f, 119.13f);
            }

            var go = (myId == highId) ? highPos : lowPos;
            DrawWaypointToMe(sa, go, 6500, "陨石狂奔_AC火圈_Waypoint");
        });
    }



    [ScriptMethod(name: "陨石狂奔指路-拉线", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0039"])]
    public void 陨石狂奔指路拉线(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        var targetId = evt.TargetId();
        var myId = sa.Data.Me;
        if (targetId != myId) return;

        if (_phase != 2) return;

        // 只能触发一次
        if (_runMeteorTetherTriggered) return;
        _runMeteorTetherTriggered = true;

        var sourcePos = evt.SourcePosition();
        sa.Method.SendChat($"/e [P2] 记录陨石狂奔Buff和位置并指路 (srcX={sourcePos.X:0.00}, srcZ={sourcePos.Z:0.00})");

        var center = new Vector3(100f, 0f, 100f);

        var forward = center - new Vector3(sourcePos.X, 0f, sourcePos.Z);
        var len = forward.Length();
        if (len < 0.001f)
        {
            // 极端情况：sourcePos 就在中心附近，给个兜底朝向
            forward = new Vector3(0f, 0f, -1f);
        }
        else
        {
            forward /= len;
        }

        // 左方向：在 XZ 平面把 forward 逆时针转 90°（x,z)->(-z,x)
        var left = new Vector3(forward.Z, 0f, -forward.X);

        // 从中心：前 40 + 左 20
        var targetPos = center + forward * 19f + left * 19f;

        DrawWaypointToMe(sa, targetPos, 7500, "陨石狂奔_Waypoint");
        sa.Method.SendChat($"/e [P2] 陨石狂奔目标点 => X={targetPos.X:0.00}, Z={targetPos.Z:0.00}");
    }

    [ScriptMethod(name: "陨石狂奔指路-二向四向火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46170|47037)$"])]
    public void 陨石狂奔指路二向四向火(Event evt, ScriptAccessory sa)
    {
        if (_phase != 2) return;

        // 46170 = 四向火, 47037 = 二向火
        var actionId = evt.ActionId();
        bool isFour = actionId == 46170;
        bool isTwo  = !isFour;

        // 读一下 cast 时长（拿不到就给个默认）
        int dur = 0;
        _ = int.TryParse(evt["DurationMilliseconds"], out dur);

        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        _ = Task.Run(async () =>
        {
            // 给 TargetIcon / StartCasting 事件一点时间进来
            await Task.Delay(200);

            List<uint> fireTargets;
            lock (_runMeteorLock)
            {
                fireTargets = _runMeteorFireTargets.ToList();
            }
            if (fireTargets.Count < 2) return;

            var myId = sa.Data.Me;

            bool iAmFire = fireTargets.Contains(myId);
            bool iAm45   = (myIdx == 4 || myIdx == 5);
            bool isTen   = (陨石狂奔打法选择 == 陨石狂奔打法.十引导);
            bool isX     = (陨石狂奔打法选择 == 陨石狂奔打法.X引导);

            // 22X 的二向火
            bool iAm01ForXTwo = isTwo && isX && (myIdx == 0 || myIdx == 1);

            // 只有相关人画：两火圈 + idx4/5 + (22X 二向火的 idx0/1)
            if (!iAmFire && !iAm45 && !iAm01ForXTwo) return;

            var center = new Vector3(100f, 0f, 100f);

            // Step 1: 先集合到中点 (100,0,100)，4秒
            const int gatherMs = 3500;
            DrawWaypointToMe(sa, center, gatherMs, isFour ? "四向火_集合" : "二向火_集合");
            await Task.Delay(gatherMs);

            // 计算两名火圈点名的高/低优先
            int[] prio = { 2, 6, 7, 3 }; // H1 D3 D4 H2 -> partyIdx: 2 6 7 3
            int PartyIdxOf(uint id) => sa.Data.PartyList.IndexOf(id);

            var ordered = fireTargets
                .Select(id => new { id, pidx = PartyIdxOf(id) })
                .Where(x => x.pidx >= 0)
                .OrderBy(x =>
                {
                    var k = Array.IndexOf(prio, x.pidx);
                    return k < 0 ? 999 : k;
                })
                .ToList();

            if (ordered.Count < 2) return;

            var highId = ordered[0].id;
            var lowId  = ordered[1].id;

            // Step 2: 4秒后去最终点
            Vector3 go = Vector3.Zero;

            if (isFour)
            {
                if (isTen)
                {
                    // 22十 四向火
                    if (iAmFire)
                        go = (myId == highId) ? new Vector3(100f, 0.00f, 95f) : new Vector3(100f, 0.00f, 105f);
                    else if (myIdx == 4)
                        go = new Vector3(95f, 0.00f, 100f);
                    else if (myIdx == 5)
                        go = new Vector3(105f, 0.00f, 100f);
                }
                else
                {
                    // 22X 四向火
                    if (iAmFire)
                        go = (myId == highId) ? new Vector3(103.90f, 0.00f, 96.83f) : new Vector3(96.08f, 0.00f, 103.24f);
                    else if (myIdx == 4)
                        go = new Vector3(96.82f, 0.00f, 96.11f);
                    else if (myIdx == 5)
                        go = new Vector3(103.17f, 0.00f, 103.92f);
                }
            }
            else
            {
                // 二向火
                if (isTen)
                {
                    // 22十 二向火
                    if (iAmFire)
                        go = (myId == highId) ? new Vector3(100f, 0.00f, 95f) : new Vector3(100f, 0.00f, 105f);
                    else if (myIdx == 4)
                        go = new Vector3(100f, 0.00f, 93f);
                    else if (myIdx == 5)
                        go = new Vector3(100f, 0.00f, 107f);
                }
                else
                {
                    // 22X 二向火：火圈两人 + idx4/5 同 22十；idx0/1 选离自己近的(92.5/107.5)
                    if (iAmFire)
                        go = (myId == highId) ? new Vector3(100f, 0.00f, 95f) : new Vector3(100f, 0.00f, 105f);
                    else if (myIdx == 4)
                        go = new Vector3(100f, 0.00f, 93f);
                    else if (myIdx == 5)
                        go = new Vector3(100f, 0.00f, 107f);
                    else if (myIdx == 0 || myIdx == 1)
                    {
                        // 选更近的点
                        var mePos = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == myId)?.Position ?? Vector3.Zero;
                        var a = new Vector3(100f, 0.00f, 92.5f);
                        var b = new Vector3(100f, 0.00f, 107.5f);

                        float d2a = (mePos.X - a.X) * (mePos.X - a.X) + (mePos.Z - a.Z) * (mePos.Z - a.Z);
                        float d2b = (mePos.X - b.X) * (mePos.X - b.X) + (mePos.Z - b.Z) * (mePos.Z - b.Z);
                        go = (d2a <= d2b) ? a : b;
                    }
                }
            }

            if (go == Vector3.Zero) return;

            // 第二段持续时间：尽量用剩余 cast time；拿不到就默认 6.5s
            int remainMs = dur > 0 ? Math.Max(2000, dur - gatherMs) : 6500;
            DrawWaypointToMe(sa, go, remainMs, isFour ? "四向火_最终点" : "二向火_最终点");

            // 用完再清
            lock (_runMeteorLock)
            {
                _runMeteorFireTargets.Clear();
                _runMeteorPositions.Clear();
            }
        });
    }


    
    // 换P
    [ScriptMethod(name: "陨石狂奔换P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46162"], userControl: false)]
    public void 陨石狂奔换P(Event evt, ScriptAccessory sa)
    {
        _phase = 2;
        _runMeteorTetherTriggered = false;
        lock (_runMeteorLock)
        {
            _runMeteorFireTargets.Clear();
            _runMeteorPositions.Clear();
        }
    }


    [ScriptMethod(
        name: "王者陨石掀地板指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(46155|46157|46159|46161)$"]
    )]
    public async void 王者陨石掀地板指路(Event evt, ScriptAccessory sa)
    {

        var (seq, token) = GetMeteorToken();

        uint actionId = evt.ActionId();
        var corner = 王者陨石下一次Corner;
        if (corner == Corner.未设定) return;

        // ---- 1) 先确定“踩塔位置索引” ----
        int towerIdx = actionId switch
        {
            46159u => corner switch
            {
                Corner.右上 => 2,
                Corner.右下 => 0,
                Corner.左上 => 1,
                Corner.左下 => 1,
                _ => -1,
            },
            46161u => corner switch
            {
                Corner.右上 => 0,
                Corner.右下 => 2,
                Corner.左上 => 1,
                Corner.左下 => 1,
                _ => -1,
            },
            46157u => corner switch
            {
                Corner.右上 => 1,
                Corner.右下 => 1,
                Corner.左上 => 2,
                Corner.左下 => 0,
                _ => -1,
            },
            46155u => corner switch
            {
                Corner.右上 => 1,
                Corner.右下 => 1,
                Corner.左上 => 0,
                Corner.左下 => 2,
                _ => -1,
            },
            _ => -1
        };

        if (towerIdx < 0) return;
        if (!王者陨石塔位置.TryGetValue(corner, out var towerArr)) return;
        if (towerIdx >= towerArr.Length) return;

        var towerPos = towerArr[towerIdx];

        // ---- 2) 再确定“掀地板后的安全点” ----
        var safePos = actionId switch
        {
            // 46159 面右（安全区4）
            46159u => new Vector3(83.84f, 0.00f, 117.36f),
            // 46161 面右（安全区1）
            46161u => new Vector3(83.98f, 0.00f, 82.67f),
            // 46157 面左（安全区3）
            46157u => new Vector3(115.73f, 0.00f, 117.35f),
            // 46155 面左（安全区2)
            46155u => new Vector3(116.10f, 0.00f, 82.45f),
            _ => Vector3.Zero
        };
        if (safePos == Vector3.Zero) return;

        // ---- 3) 画图：先踩塔 7s；7s 后再去安全点 7s ----
        const int firstMs = 7000;
        const int safeMs  = 7000;

        sa.Method.SendChat($"/e [掀地板] action={actionId} corner={corner} towerIdx={towerIdx}");

        DrawWaypointToMe(sa, towerPos, firstMs, $"掀地板_踩塔_{actionId}");

        try
        {
            await Task.Delay(firstMs, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!IsMeteorSeqValid(seq)) return;

        DrawWaypointToMe(sa, safePos, safeMs, $"掀地板_安全区_{actionId}");
    }


    private static bool IsLeftCorner(Corner c) => c is Corner.左上 or Corner.左下;
    private static bool IsRightCorner(Corner c) => c is Corner.右上 or Corner.右下;

    private async Task RunMeteorCycleAsync(ScriptAccessory sa, int seq, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsMeteorSeqValid(seq)) return;

        if (王者陨石下一次Corner == Corner.未设定) return;

        // 1) 先引导去踩塔：idx 逻辑更新
        int idx;
        if (王者陨石是否有拉线Buff)
        {
            idx = 0;

            var meteorPos = 王者陨石陨石Pos;
            bool hasMeteorPos = meteorPos != Vector3.Zero;

            if (hasMeteorPos)
            {
                bool cornerRight = IsRightCorner(王者陨石下一次Corner);
                bool cornerLeft  = IsLeftCorner(王者陨石下一次Corner);

                // - 陨石.X < 100：如果 corner 在右边 => idx=1，否则 idx=0
                // - 陨石.X > 100：如果 corner 在左边 => idx=1，否则 idx=0
                if (meteorPos.X < 100f)
                {
                    idx = cornerRight ? 1 : 0;
                }
                else if (meteorPos.X > 100f)
                {
                    idx = cornerLeft ? 1 : 0;
                }
                // X == 100：保持 idx=0
            }
        }
        else
        {
            // 没拉线 buff：保持原逻辑
            idx = (王者陨石踩塔击飞打法选择 == 王者陨石击飞打法.同平台 ? 1 : 2);
        }

        sa.Method.SendChat($"/e 王者陨石下一次Corner: {王者陨石下一次Corner}，idx: {idx}");
        var wPos = 王者陨石塔位置[王者陨石下一次Corner][idx];
        DrawWaypointToMe(sa, wPos, 11000, "Meteor_Tower_Waypoint");
        await Task.Delay(11000, token);
        if (!IsMeteorSeqValid(seq)) return;

        sa.Method.SendChat("/e 空中飞人+判断下一次踩塔位置");

        // 2) 空中飞人后，计算下一次 Corner
        UpdateCornerAfterKnockback();

        await Task.Delay(2500, token);
        if (!IsMeteorSeqValid(seq)) return;
        sa.Method.SendChat("/e 引导到待定位置");

        // 3) 待定
        var wPos2 = 王者陨石下一次Corner switch
        {
            Corner.左上 => 王者陨石是否有拉线Buff ? new Vector3(88.15f, 0f, 100f) : new Vector3(93.08f, 0f, 100.38f),
            Corner.右上 => 王者陨石是否有拉线Buff ? new Vector3(111.85f, 0f, 100f) : new Vector3(106.92f, 0f, 100.38f),
            Corner.左下 => 王者陨石是否有拉线Buff ? new Vector3(88.15f, 0f, 100f) : new Vector3(93.08f, 0f, 100.38f),
            Corner.右下 => 王者陨石是否有拉线Buff ? new Vector3(111.85f, 0f, 100f) : new Vector3(106.92f, 0f, 100.38f),
            _ => Vector3.Zero,
        };
        DrawWaypointToMe(sa, wPos2, 4000, "待定位置_Waypoint");
        await Task.Delay(3500, token);
        if (!IsMeteorSeqValid(seq)) return;
        // TODO: 这里再加一个根据同边同组没有buff的人的位置，修改corner。就近原则。
        await Task.Delay(500, token);
        if (!IsMeteorSeqValid(seq)) return;

        sa.Method.SendChat("/e 引导火圈");

        // 4) 火圈引导
        // foreach (var pos in 王者陨石火圈引导美[王者陨石下一次Corner])
        // {
        //     DrawWaypointToMe(sa, pos, 2000, "火圈_Waypoint");
        //     await Task.Delay(2000);
        //     sa.Method.SendChat("/e 下一个火圈");
        // }
        var fireList = 王者陨石火圈引导美[王者陨石下一次Corner];

        for (int i = 0; i < fireList.Length; i++)
        {
            // sa.Method.SendChat($"/e i={i}");
            if (i == 2 && !王者陨石是否有拉线Buff)
            {
                // 在引导第二个火圈时重算一次 Corner
                UpdateCornerByMyPosition(sa);

                // Corner 可能变了，重新取火圈路径
                fireList = 王者陨石火圈引导美[王者陨石下一次Corner];
            }

            var pos = fireList[i];
            if (王者陨石是否有拉线Buff)
                DrawWaypointToMe(sa, pos, 2000, "火圈_Waypoint");
            await Task.Delay(2000, token);
            if (!IsMeteorSeqValid(seq)) return;
            // sa.Method.SendChat("/e 下一个火圈");
        }

        // 5) 最终位
        sa.Method.SendChat("/e 最终位置_Waypoint");
        var wPos3 = 王者陨石下一次Corner switch
        {
            Corner.左上 => 王者陨石是否有拉线Buff ? new Vector3(84.01f, 0f, 80.59f) : new Vector3(83.92f, 0f, 88.78f),
            Corner.右上 => 王者陨石是否有拉线Buff ? new Vector3(116.01f, 0f, 80.91f) : new Vector3(115.98f, 0f, 88.78f),
            Corner.左下 => 王者陨石是否有拉线Buff ? new Vector3(84.01f, 0f, 119.21f) : new Vector3(83.87f, 0f, 111.25f),
            Corner.右下 => 王者陨石是否有拉线Buff ? new Vector3(115.96f, 0f, 119.02f) : new Vector3(116.01f, 0f, 111.13f),
            _ => Vector3.Zero,
        };
        DrawWaypointToMe(sa, wPos3, 2000, "最终位置_Waypoint");
        await Task.Delay(2000, token);
        if (!IsMeteorSeqValid(seq)) return;

        // 初始化
        王者陨石是否有拉线Buff = false;
        王者陨石陨石Pos = Vector3.Zero;
    }

    private void UpdateCornerAfterKnockback()
    {
        if (王者陨石是否有拉线Buff)
            {
                var meteorPos = 王者陨石陨石Pos;

            bool isUpper = true;
            bool hasMeteorPos = meteorPos != Vector3.Zero;
            if (hasMeteorPos)
            {
                isUpper = meteorPos.Z > 100f;
            }

            // 陨石.X < 100 => corner 一定是右
            // 陨石.X > 100 => corner 一定是左
            // X==100 或没抓到位置：保留原 corner 的左右
            bool isLeft;
            if (hasMeteorPos && meteorPos.X < 100f)
                isLeft = false; // 右
            else if (hasMeteorPos && meteorPos.X > 100f)
                isLeft = true;  // 左
            else
                isLeft = IsLeftCorner(王者陨石下一次Corner); // fallback

            王者陨石下一次Corner = (isLeft, isUpper) switch
            {
                (true, true) => Corner.左上,
                (true, false) => Corner.左下,
                (false, true) => Corner.右上,
                (false, false) => Corner.右下,
            };

            return;
        }

        // 没有拉线buff：
        // - 同平台：只换上下（左上<->左下, 右上<->右下）
        // - 斜飞：只换左右（左上<->右上, 左下<->右下）
        if (王者陨石踩塔击飞打法选择 == 王者陨石击飞打法.同平台)
        {
            王者陨石下一次Corner = 王者陨石下一次Corner switch
            {
                Corner.左上 => Corner.左下,
                Corner.左下 => Corner.左上,
                Corner.右上 => Corner.右下,
                Corner.右下 => Corner.右上,
                _ => 王者陨石下一次Corner,
            };
        }
        else
        {
            王者陨石下一次Corner = 王者陨石下一次Corner switch
            {
                Corner.左上 => Corner.右上,
                Corner.右上 => Corner.左上,
                Corner.左下 => Corner.右下,
                Corner.右下 => Corner.左下,
                _ => 王者陨石下一次Corner,
            };
        }
    }
    private void UpdateCornerByMyPosition(ScriptAccessory sa)
    {
        // 仅用于“没有拉线 buff”的情况
        if (王者陨石是否有拉线Buff) return;

        // 这里用 sa.Data.MePosition
        var meId = sa.Data.Me;
        // 通过id获得自己的坐标
        var p = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == meId)?.Position ?? Vector3.Zero;
        if (p == Vector3.Zero) return;

        bool isLeft = IsLeftCorner(王者陨石下一次Corner);
        bool isUpper = 王者陨石下一次Corner is Corner.左上 or Corner.右上;

        // X<100 左 / X>100 右；X==100 保持
        if (p.X < 100f) isLeft = true;
        else if (p.X > 100f) isLeft = false;

        // Z<100 上 / Z>100 下；Z==100 保持
        if (p.Z < 100f) isUpper = true;
        else if (p.Z > 100f) isUpper = false;

        王者陨石下一次Corner = (isLeft, isUpper) switch
        {
            (true,  true)  => Corner.左上,
            (true,  false) => Corner.左下,
            (false, true)  => Corner.右上,
            (false, false) => Corner.右下,
        };

        sa.Method.SendChat($"/e [无拉线] 按站位重算Corner => {王者陨石下一次Corner} (X={p.X:0.00}, Z={p.Z:0.00})");
    }


    private static int MatchDomPointIndex(Vector3 p)
    {
        int best = -1;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < DomPoints.Length; i++)
        {
            var d = p - DomPoints[i];
            var d2 = d.X * d.X + d.Z * d.Z;
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = i;
            }
        }

        return bestD2 <= DomPointEps * DomPointEps ? best : -1;
    }
    private static int FindMissingIndexFromMask(int mask)
    {
        for (int i = 0; i < 4; i++)
        {
            if ((mask & (1 << i)) == 0) return i;
        }
        return -1;
    }

    private void DrawWaypointToMe(ScriptAccessory sa, Vector3 wpos, int durMs, string name)
    {
        var dpWp = sa.WaypointDp(wpos, (uint)durMs, 0, name);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
    }

    private void DrawSafeCircle(ScriptAccessory sa, Vector3 pos, int durMs, string name)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Position = pos;
        dp.DestoryAt = durMs;
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Scale = new Vector2(6f);
        dp.ScaleMode = ScaleMode.None;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
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

    // public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, Vector3 pos, uint duration, uint delay = 0, string name = "Waypoint")
    // {
    //     var dp = sa.Data.GetDefaultDrawProperties();
    //     dp.Name = name;
    //     dp.Color = sa.Data.DefaultSafeColor;
    //     dp.Owner = sa.Data.Me;
    //     dp.TargetPosition = pos;
    //     dp.DestoryAt = duration;
    //     dp.Delay = delay;
    //     dp.Scale = new Vector2(2);
    //     dp.ScaleMode = ScaleMode.YByDistance;
    //     return dp;
    // }
    public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, Vector3 target, uint duration, uint delay = 0, string name = "Waypoint", Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;            // 仍然绑定自己
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
