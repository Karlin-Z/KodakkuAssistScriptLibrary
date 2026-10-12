using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;

namespace Codaaaaaa.Kefka;

[ScriptType(
    guid: "cc2c6d88-abe5-40be-89da-5f231b9d21d8",
    name: "绝凯夫卡P1指路先行版",
    territorys: [1363],
    version: "0.0.1.5",
    author: "Codaaaaaa",
    note: "自用拼好挂。请支持K佬&灵视佬")]
public class Kefka
{
    #region 用户设置

    internal static bool DebugEnabled;

    [UserSetting("调试：输出调试日志")]
    public bool DebugLog
    {
        get => DebugEnabled;
        set => DebugEnabled = value;
    }

    public enum BigIceSealGuideMode
    {
        斜角黑泥,
        _1A四人分摊_TH上半场,
        _1A四人分摊_MT组上半场,
        _1A四人分摊_T远北奶近南,
        Yan式八人全分摊,
    }

    public enum BigIceSealMtUpperHalfTankMode
    {
        不交换分组_MT上半场,
        交换分组_ST上半场,
    }

    public enum Statue3ArrowGuideMode
    {
        _1A,
        _1A改,
        方逆,
        方顺,
    }

    public enum Statue3TetherGuideMode
    {
        魅惑在外,
        魅惑在内,
        全固定,
    }

    private enum Statue3TetherType
    {
        Unknown,
        睡眠,
        魅惑,
    }

    public enum XuanHuHuGuideMode
    {
        固定半场,
        盗火烬,
    }

    [UserSetting("P1_神像1攻略")]
    public XuanHuHuGuideMode XuanHuHuMode { get; set; } = XuanHuHuGuideMode.盗火烬;

    [UserSetting("P1_神像2攻略")]
    public BigIceSealGuideMode BigIceSealMode { get; set; } = BigIceSealGuideMode.斜角黑泥;

    [UserSetting("P1_神像2指路_双T是否交换位置 只影响'斜角黑泥'和'_1A四人分摊_MT组上半场'")]
    public BigIceSealMtUpperHalfTankMode BigIceSealMtUpperHalfTank { get; set; } = BigIceSealMtUpperHalfTankMode.不交换分组_MT上半场;

    [UserSetting("P1_神像3箭头攻略")]
    public Statue3ArrowGuideMode Statue3ArrowMode { get; set; } = Statue3ArrowGuideMode.方逆;

    [UserSetting("P1_神像3_睡眠魅惑攻略")]
    public Statue3TetherGuideMode Statue3TetherMode { get; set; } = Statue3TetherGuideMode.魅惑在外;

    #endregion

    #region 全局变量与初始化

    // P1 子阶段：
    // 1.0 = P1 开场/未记录神像
    // 1.1 = P1 神像1
    // 1.2 = P1 神像2
    // 1.3 = P1 神像3
    // 2.0 = P2
    // 3.0 = P3
    private double _phase = 1.0;
    private int _p1StatueCount = 0;

    public void Init(ScriptAccessory sa)
    {
        _phase = 1.0;
        _p1StatueCount = 0;

        lock (_iceFireLock)
        {
            _iceIsReal = null;
            _iceDiagonalIsLeftUpRightDown = null;
            _actualStack = null;
            _iceFireTargets.Clear();
            _waveCannonTargets.Clear();
            _action47784Targets.Clear();
            _action47784GuideSent = false;
            _action47784Started = false;
        }

        lock (_bigIceSealLock)
        {
            _bigIceSealSafeIsLeftUpRightDown = null;
            _lastBigIceSealGuidePos = null;
            _bigIceSealTetherTargets.Clear();
            _bigIceSealActive = false;
            _bigIceSealGuideStarted = false;
            _bigIceSealSecondGuideDone = false;
            _bigIceSealTetherDetectionEnabled = false;
            _bigIceSealTetherDetectionWindowStarted = false;
        }

        ResetStatue3ArrowState();

        sa.Method.RemoveDraw(".*");
    }

    #endregion

    #region 阶段控制

    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa)
    {
        _phase = 1.1;
        _p1StatueCount = 1;
        ResetStatue3ArrowState();
    }

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2.0;

    [ScriptMethod(name: "Set Phase 3", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP3"], userControl: false)]
    public void SetP3(Event evt, ScriptAccessory sa) => _phase = 3.0;

    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:phase"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Debug($"当前 Phase={_phase}, P1StatueCount={_p1StatueCount}");

    [ScriptMethod(
        name: "P1_众神之像_阶段控制",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:48370"],
        userControl: false)]
    public void P1_众神之像_阶段控制(Event evt, ScriptAccessory sa)
    {
        if (_phase >= 2.0)
            return;

        _p1StatueCount++;

        _phase = _p1StatueCount switch
        {
            1 => 1.1,
            2 => 1.2,
            3 => 1.3,
            _ => _phase,
        };

        if (IsPhase(_phase, 1.3))
            ResetStatue3ArrowState();

        sa.Debug($"P1众神之像阶段控制：Count={_p1StatueCount}, Phase={_phase}");
    }

    [ScriptMethod(
        name: "P1结束_切换到P2",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47804"],
        userControl: false)]
    public void P1结束_切换到P2(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.3))
            return;

        _phase = 2.0;

        sa.Debug($"P1结束检测到 47804，切换到 P2。Phase={_phase}");
    }

    #endregion

    #region P1 神像1（冰爆）

    #region 神像1 变量

    private readonly object _iceFireLock = new();

    // true = 真冰，false = 假冰
    private bool? _iceIsReal = null;

    // true = 左上右下，false = 左下右上
    private bool? _iceDiagonalIsLeftUpRightDown = null;

    // true = 实际分摊，false = 实际散开
    private bool? _actualStack = null;

    private readonly HashSet<uint> _iceFireTargets = new();
    private readonly HashSet<uint> _waveCannonTargets = new();

    private readonly Dictionary<uint, Vector3> _action47784Targets = new();
    private bool _action47784GuideSent = false;
    private bool _action47784Started = false;

    private static readonly Vector3 UpperLeftPoint = new(93.90f, 0.00f, 93.94f);
    private static readonly Vector3 UpperRightPoint = new(106.09f, 0.00f, 93.93f);
    
    private static readonly Vector3 UpperLeftAreaUpperPoint = new(94.67f, 0.00f, 88.49f);
    private static readonly Vector3 UpperRightAreaUpperPoint = new(108.29f, 0.00f, 88.06f);

    private static readonly Vector3 LowerLeftPoint = new(93.90f, 0.00f, 106.06f);
    private static readonly Vector3 LowerRightPoint = new(106.09f, 0.00f, 106.07f);

    private const uint RealIceAction = 47768;
    private const uint StackIcon = 0x0080;
    private const uint SpreadIcon = 0x007F;

    #endregion

    [ScriptMethod(
        name: "P1_冰爆_记录冰真假与方向",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47768|47771)$"],
        userControl: false)]
    public void P1_冰爆_记录冰真假与方向(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.1))
            return;

        var actionId = evt.ActionId();

        bool iceIsReal = actionId == RealIceAction;
        bool rawDiagonalIsLeftUpRightDown = IsLeftUpRightDownByRotation(evt.SourceRotation());

        // 47768 真冰：实际覆盖方向 = SourceRotation 判断出来的方向
        // 47771 假冰：实际覆盖方向 = 反方向
        bool actualDiagonalIsLeftUpRightDown = iceIsReal
            ? rawDiagonalIsLeftUpRightDown
            : !rawDiagonalIsLeftUpRightDown;

        lock (_iceFireLock)
        {
            _iceIsReal = iceIsReal;
            _iceDiagonalIsLeftUpRightDown = actualDiagonalIsLeftUpRightDown;
        }

        sa.Debug($"""
        Ice cast:
        Phase={_phase}
        ActionId={actionId}
        SourceRotation={evt.SourceRotation()}
        IceIsReal={iceIsReal}
        RawDiagonal={(rawDiagonalIsLeftUpRightDown ? "左上右下" : "左下右上")}
        ActualDiagonal={(actualDiagonalIsLeftUpRightDown ? "左上右下" : "左下右上")}
        UpperCoveredPoint={(actualDiagonalIsLeftUpRightDown ? "左上点" : "右上点")}
        """);
    }

    [ScriptMethod(
        name: "P1_冰爆_记录分摊散开",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:regex:^(0080|007F)$"],
        userControl: false)]
    public void P1_冰爆_记录分摊散开(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.1))
            return;

        uint iconId = evt.IconId();
        uint targetId = evt.TargetId();

        if (targetId == 0)
            return;

        lock (_iceFireLock)
        {
            _iceFireTargets.Add(targetId);

            // 2 个 0080：认为实际是分摊
            if (_iceFireTargets.Count == 2 && iconId == StackIcon)
                _actualStack = true;

            // 8 个 007F：认为实际是散开
            if (_iceFireTargets.Count == 8 && iconId == SpreadIcon)
                _actualStack = false;
        }

        // sa.Debug($"""
        // Fire icon:
        // Phase={_phase}
        // IconId=0x{iconId:X4}
        // TargetId=0x{targetId:X}
        // TargetCount={_iceFireTargets.Count}
        // ActualStack={_actualStack}
        // """);
    }

    [ScriptMethod(
        name: "P1_冰爆_记录002D点名",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:002D"],
        userControl: false)]
    public void P1_冰爆_记录002D点名(Event evt, ScriptAccessory sa)
    {
        uint targetId = evt.TargetId();
        uint sourceId = evt.SourceId();

        uint tetherPlayerId = sa.Data.PartyList.Contains(targetId)
            ? targetId
            : sa.Data.PartyList.Contains(sourceId)
                ? sourceId
                : targetId;

        if (tetherPlayerId == 0)
            return;

        // 神像1：只记录玄乎乎魔法需要的 002D。
        if (IsPhase(_phase, 1.1))
        {
            lock (_iceFireLock)
            {
                _waveCannonTargets.Add(tetherPlayerId);
            }

            sa.Debug($"神像1 002D记录：Player=0x{tetherPlayerId:X}, Source=0x{sourceId:X}, Target=0x{targetId:X}, IsMe={tetherPlayerId == sa.Data.Me}, Phase={_phase}");
            return;
        }

        // 神像2：只在扩大大冰封结算后 5 秒检测窗打开时，才接受 002D 并触发第二段。
        if (IsPhase(_phase, 1.2))
        {
            bool shouldSendBigIceSealSecondGuide = false;
            bool bigIceSealDetectionEnabledSnapshot = false;
            BigIceSealGuideMode modeSnapshot = BigIceSealMode;
            double phaseSnapshot = _phase;
            Vector3? firstGuidePosSnapshot = null;

            lock (_bigIceSealLock)
            {
                bigIceSealDetectionEnabledSnapshot = _bigIceSealTetherDetectionEnabled;

                if (_bigIceSealActive
                    && _bigIceSealTetherDetectionEnabled
                    && !_bigIceSealSecondGuideDone)
                {
                    _bigIceSealTetherTargets.Add(tetherPlayerId);

                    if (tetherPlayerId == sa.Data.Me)
                    {
                        modeSnapshot = BigIceSealMode;
                        phaseSnapshot = _phase;
                        firstGuidePosSnapshot = _lastBigIceSealGuidePos;

                        _bigIceSealSecondGuideDone = true;
                        _bigIceSealActive = false;
                        _bigIceSealTetherDetectionEnabled = false;
                        shouldSendBigIceSealSecondGuide = true;
                    }
                }
            }

            sa.Debug($"神像2 002D：Player=0x{tetherPlayerId:X}, Source=0x{sourceId:X}, Target=0x{targetId:X}, IsMe={tetherPlayerId == sa.Data.Me}, DetectionEnabled={bigIceSealDetectionEnabledSnapshot}, Phase={_phase}");

            if (shouldSendBigIceSealSecondGuide)
                SendBigIceSeal002DSecondGuide(sa, modeSnapshot, phaseSnapshot, firstGuidePosSnapshot);

            return;
        }

        // 神像3 002D 由 P1_神像3_002D指路 单独处理。
        sa.Debug($"002D忽略：当前 Phase={_phase}, Player=0x{tetherPlayerId:X}, Source=0x{sourceId:X}, Target=0x{targetId:X}");
    }

    [ScriptMethod(
        name: "P1_冰爆_玄乎乎魔法指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47764"])]
    public void P1_冰爆_玄乎乎魔法指路(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.1))
            return;

        Task.Run(async () =>
        {
            await Task.Delay(100);

            bool? iceDiagonalIsLeftUpRightDown;
            bool? iceIsReal;
            bool? actualStack;
            HashSet<uint> waveCannonTargetsSnapshot;

            lock (_iceFireLock)
            {
                iceDiagonalIsLeftUpRightDown = _iceDiagonalIsLeftUpRightDown;
                iceIsReal = _iceIsReal;
                actualStack = _actualStack;
                waveCannonTargetsSnapshot = new HashSet<uint>(_waveCannonTargets);
            }

            if (iceDiagonalIsLeftUpRightDown == null)
            {
                sa.Debug("玄乎乎魔法指路失败：没有记录到冰方向。");
                return;
            }

            var party = sa.Data.PartyList;
            int myIdx = sa.MyIndex();

            if (myIdx < 0 || myIdx > 7)
            {
                sa.Debug($"玄乎乎魔法指路失败：自己的 index 异常。MyIndex={myIdx}");
                return;
            }

            bool meHas002D = waveCannonTargetsSnapshot.Contains(sa.Data.Me);

            Vector3 coveredUpperPoint = iceDiagonalIsLeftUpRightDown.Value
                ? UpperLeftPoint
                : UpperRightPoint;

            Vector3 uncoveredUpperPoint = iceDiagonalIsLeftUpRightDown.Value
                ? UpperRightPoint
                : UpperLeftPoint;

            Vector3 GetFirstGuidePos(uint playerId)
            {
                bool has002D = waveCannonTargetsSnapshot.Contains(playerId);
                int playerIdx = PartyIndex(party, playerId);

                if (XuanHuHuMode == XuanHuHuGuideMode.固定半场)
                {
                    // 固定半场：0/1/2/3 固定左半场，4/5/6/7 固定右半场。
                    bool isLeftHalf = playerIdx <= 3;
                    bool upperLeftHasIce = iceDiagonalIsLeftUpRightDown.Value;
                    return GetFixedHalfPos(isLeftHalf, has002D, upperLeftHasIce);
                }

                // 盗火烬：002D 去上半场有冰点，其他人去上半场无冰点。
                return has002D
                    ? coveredUpperPoint
                    : uncoveredUpperPoint;
            }

            bool IsRightHalfForSecondGuide(uint playerId)
            {
                int playerIdx = PartyIndex(party, playerId);

                if (XuanHuHuMode == XuanHuHuGuideMode.固定半场)
                {
                    // 固定半场只有第一段半场判定不同；第二段仍按对应半场分左右点。
                    return playerIdx >= 4;
                }

                // 盗火烬按第一段实际去到的点决定左右半场。
                return GetFirstGuidePos(playerId).X > 100.0f;
            }

            Vector3 myFirstGuidePos = GetFirstGuidePos(sa.Data.Me);

            var firstGuideDp = sa.WaypointDp(
                myFirstGuidePos,
                3000,
                0,
                $"P1_冰爆_玄乎乎魔法指路_第一段_{XuanHuHuMode}_{myIdx}"
            );

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, firstGuideDp);

            sa.Debug($"""
            玄乎乎魔法第一段指路:
            Phase={_phase}
            Mode={XuanHuHuMode}
            IceIsReal={iceIsReal}
            ActualStack={actualStack}
            ActualDiagonal={(iceDiagonalIsLeftUpRightDown.Value ? "左上右下" : "左下右上")}
            MyIndex={myIdx}
            MeHas002D={meHas002D}
            FirstSide={(IsRightHalfForSecondGuide(sa.Data.Me) ? "右半场" : "左半场")}
            TargetPoint={myFirstGuidePos}
            DrawName=P1_冰爆_玄乎乎魔法指路_第一段_{XuanHuHuMode}_{myIdx}
            """);

            await Task.Delay(5000);

            var rightSidePoints = new List<Vector3>
            {
                new(102.25f, 0.00f, 100.00f),
                new(106.81f, 0.00f, 100.00f),
                new(111.45f, 0.00f, 100.00f),
                new(116.25f, 0.00f, 100.00f),
            };

            var leftSidePoints = new List<Vector3>
            {
                new(97.75f, 0.00f, 100.00f),
                new(93.19f, 0.00f, 100.00f),
                new(88.55f, 0.00f, 100.00f),
                new(83.75f, 0.00f, 100.00f),
            };

            var rightSideMembers = party
                .Where(IsRightHalfForSecondGuide)
                .OrderBy(id => PartyIndex(party, id))
                .ToList();

            var leftSideMembers = party
                .Where(id => !IsRightHalfForSecondGuide(id))
                .OrderBy(id => PartyIndex(party, id))
                .ToList();

            Vector3? mySecondGuidePos = null;

            int myRightOrder = rightSideMembers.IndexOf(sa.Data.Me);
            if (myRightOrder >= 0 && myRightOrder < rightSidePoints.Count)
                mySecondGuidePos = rightSidePoints[myRightOrder];

            int myLeftOrder = leftSideMembers.IndexOf(sa.Data.Me);
            if (myLeftOrder >= 0 && myLeftOrder < leftSidePoints.Count)
                mySecondGuidePos = leftSidePoints[myLeftOrder];

            if (mySecondGuidePos == null)
            {
                sa.Debug("玄乎乎魔法第二段指路失败：没有找到自己的左右半场排序。");
                return;
            }

            var secondGuideDp = sa.WaypointDp(
                mySecondGuidePos.Value,
                5000,
                0,
                $"P1_冰爆_玄乎乎魔法指路_第二段_{XuanHuHuMode}_{myIdx}"
            );

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, secondGuideDp);

            string side = myRightOrder >= 0 ? "右半场" : "左半场";
            int order = myRightOrder >= 0 ? myRightOrder : myLeftOrder;

            sa.Debug($"""
            玄乎乎魔法第二段指路:
            Phase={_phase}
            Mode={XuanHuHuMode}
            Side={side}
            Order={order}
            MyIndex={myIdx}
            TargetPoint={mySecondGuidePos.Value}
            DrawName=P1_冰爆_玄乎乎魔法指路_第二段_{XuanHuHuMode}_{myIdx}
            RightMembers={string.Join(",", rightSideMembers.Select(x => PartyIndex(party, x)))}
            LeftMembers={string.Join(",", leftSideMembers.Select(x => PartyIndex(party, x)))}
            """);
        });
    }

    [ScriptMethod(
        name: "P1_冰爆_47784换位指路",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47784"],
        userControl: false)]
    public void P1_冰爆_47784换位指路(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.1))
            return;

        uint targetId = evt.TargetId();
        Vector3 effectPosition = evt.TargetPosition();

        if (targetId == 0)
            return;

        Dictionary<uint, Vector3>? snapshot = null;

        lock (_iceFireLock)
        {
            // 47784 已经完成过一次指路，后面的重复 ActionEffect 直接丢掉
            if (_action47784GuideSent)
                return;

            _action47784Started = true;
            _action47784Targets[targetId] = effectPosition;

            sa.Debug($"""
            47784 ActionEffect:
            Phase={_phase}
            Started={_action47784Started}
            GuideSent={_action47784GuideSent}
            TargetId=0x{targetId:X}
            TargetIndex={sa.Data.PartyList.IndexOf(targetId)}
            TargetPosition={effectPosition}
            Count={_action47784Targets.Count}
            """);

            if (_action47784Targets.Count < 4)
                return;

            _phase = 1.2;
            _action47784GuideSent = true;
            snapshot = new Dictionary<uint, Vector3>(_action47784Targets);
        }

        Task.Run(async () =>
        {
            await Task.Delay(100);

            var party = sa.Data.PartyList;
            int myIdx = sa.MyIndex();

            if (myIdx < 0 || myIdx > 7)
            {
                sa.Debug($"47784换位指路失败：自己的 index 异常。MyIndex={myIdx}");
                return;
            }

            var lowGroup = Enumerable.Range(0, 4)
                .Where(i => i < party.Count)
                .Select(i => party[i])
                .ToList();

            var highGroup = Enumerable.Range(4, 4)
                .Where(i => i < party.Count)
                .Select(i => party[i])
                .ToList();

            Vector3? myGuidePos = Get47784GuidePosForGroup(sa.Data.Me, lowGroup, party, snapshot);
            myGuidePos ??= Get47784GuidePosForGroup(sa.Data.Me, highGroup, party, snapshot);

            if (myGuidePos == null)
            {
                sa.Debug($"""
                47784换位指路：自己不需要指路或没有匹配到。
                MyIndex={myIdx}
                MeWasTargeted={snapshot.ContainsKey(sa.Data.Me)}
                Targets={string.Join(",", snapshot.Keys.Select(x => party.IndexOf(x)).OrderBy(x => x))}
                """);
                return;
            }

            var guideDp = sa.WaypointDp(
                myGuidePos.Value,
                4500,
                0,
                $"P1_冰爆_47784换位指路_{myIdx}"
            );

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, guideDp);

            sa.Debug($"""
            47784换位指路:
            Phase={_phase}
            MyIndex={myIdx}
            TargetPoint={myGuidePos.Value}
            DrawName=P1_冰爆_47784换位指路_{myIdx}
            All47784Targets={string.Join(",", snapshot.Keys.Select(x => party.IndexOf(x)).OrderBy(x => x))}
            LowGroupTargets={string.Join(",", snapshot.Keys.Where(x => party.IndexOf(x) >= 0 && party.IndexOf(x) <= 3).Select(x => party.IndexOf(x)).OrderBy(x => x))}
            HighGroupTargets={string.Join(",", snapshot.Keys.Where(x => party.IndexOf(x) >= 4 && party.IndexOf(x) <= 7).Select(x => party.IndexOf(x)).OrderBy(x => x))}
            """);
        });
    }

    #endregion

    #region P1 神像2（扩大大冰封）

    #region 神像2 变量

    private readonly object _bigIceSealLock = new();

    private static readonly Vector3 ArenaCenter = new(100.00f, 0.00f, 100.00f);

    // true = 左上右下是安全区，false = 左下右上是安全区
    private bool? _bigIceSealSafeIsLeftUpRightDown = null;
    private Vector3? _lastBigIceSealGuidePos = null;
    private readonly HashSet<uint> _bigIceSealTetherTargets = new();
    private bool _bigIceSealActive = false;
    private bool _bigIceSealGuideStarted = false;
    private bool _bigIceSealSecondGuideDone = false;

    // 只有扩大大冰封 ActionEffect 结算后再等 5 秒，才开启神像2第二段 002D 检测。
    private bool _bigIceSealTetherDetectionEnabled = false;
    private bool _bigIceSealTetherDetectionWindowStarted = false;

    #endregion

    [ScriptMethod(
        name: "P1_神像2_记录47774安全对角",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47774|47768)$"],
        userControl: false)]
    public void P1_神像2_记录47774安全对角(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.2))
            return;

        bool hitIsLeftUpRightDown = IsLeftUpRightDownByRotation(evt.SourceRotation());
        bool safeIsLeftUpRightDown = !hitIsLeftUpRightDown;

        lock (_bigIceSealLock)
        {
            _bigIceSealSafeIsLeftUpRightDown = safeIsLeftUpRightDown;
        }

        sa.Debug($"神像2安全对角: Phase={_phase}, ActionId={evt.ActionId()}, Rot={evt.SourceRotation()}, Hit={(hitIsLeftUpRightDown ? "左上右下" : "左下右上")}, Safe={(safeIsLeftUpRightDown ? "左上右下" : "左下右上")}");
    }

    [ScriptMethod(
        name: "P1_神像2指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47765"])]
    public void P1_第二次扩大大冰封指路(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.2))
            return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(100);

            if (!IsPhase(_phase, 1.2))
                return;

            int myIdx = sa.MyIndex();

            if (myIdx < 0 || myIdx > 7)
            {
                sa.Debug($"扩大大冰封指路失败：自己的 index 异常。MyIndex={myIdx}");
                return;
            }

            bool safeIsLeftUpRightDown;

            lock (_bigIceSealLock)
            {
                if (_bigIceSealGuideStarted)
                {
                    sa.Debug($"扩大大冰封指路跳过：本次 init 后已经触发过。Mode={BigIceSealMode}, MyIndex={myIdx}");
                    return;
                }

                if (_bigIceSealSafeIsLeftUpRightDown == null)
                {
                    sa.Debug($"扩大大冰封指路失败：没有记录到 47774 安全对角。Mode={BigIceSealMode}, MyIndex={myIdx}");
                    return;
                }

                _bigIceSealGuideStarted = true;
                safeIsLeftUpRightDown = _bigIceSealSafeIsLeftUpRightDown.Value;
            }

            Vector3? myGuidePos = GetBigIceSealInitialPos(
                BigIceSealMode,
                myIdx,
                safeIsLeftUpRightDown,
                BigIceSealMtUpperHalfTank
            );

            if (myGuidePos == null)
            {
                sa.Debug($"扩大大冰封指路失败：没有匹配到点位。Mode={BigIceSealMode}, MyIndex={myIdx}, SafeDiagonal={(safeIsLeftUpRightDown ? "左上右下" : "左下右上")}");
                return;
            }

            lock (_bigIceSealLock)
            {
                _lastBigIceSealGuidePos = myGuidePos.Value;
                _bigIceSealTetherTargets.Clear();
                _bigIceSealActive = true;
                _bigIceSealSecondGuideDone = false;
                _bigIceSealTetherDetectionEnabled = false;
                _bigIceSealTetherDetectionWindowStarted = false;
            }

            bool useTankGroupSwapSetting = BigIceSealMode is
                BigIceSealGuideMode.斜角黑泥 or
                BigIceSealGuideMode._1A四人分摊_MT组上半场;

            string initialDrawName = useTankGroupSwapSetting
                ? $"P1_第二次扩大大冰封指路_{BigIceSealMode}_{BigIceSealMtUpperHalfTank}_{myIdx}"
                : $"P1_第二次扩大大冰封指路_{BigIceSealMode}_{myIdx}";

            var guideDp = sa.WaypointDp(
                myGuidePos.Value,
                5000,
                0,
                initialDrawName
            );

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, guideDp);

            sa.Debug($"""
            第二次扩大大冰封指路:
            Phase={_phase}
            Mode={BigIceSealMode}
            TankGroupSwapSetting={BigIceSealMtUpperHalfTank}
            MyIndex={myIdx}
            SafeDiagonal={(safeIsLeftUpRightDown ? "左上右下" : "左下右上")}
            TargetPoint={myGuidePos.Value}
            DrawName={initialDrawName}
            """);
        });
    }

    [ScriptMethod(
        name: "P1_神像2指路第二段",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47765"],
        userControl: false)]
    public void P1_神像2_扩大大冰封结算后开启002D检测(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.2))
            return;

        bool shouldStartWindowTask = false;

        lock (_bigIceSealLock)
        {
            if (_bigIceSealActive
                && _bigIceSealGuideStarted
                && !_bigIceSealSecondGuideDone
                && !_bigIceSealTetherDetectionWindowStarted)
            {
                _bigIceSealTetherDetectionWindowStarted = true;
                _bigIceSealTetherDetectionEnabled = false;
                shouldStartWindowTask = true;
            }
        }

        if (!shouldStartWindowTask)
            return;

        sa.Debug("神像2扩大大冰封已结算：5秒后开启002D检测。首个结算 ActionEffect=47765");

        _ = Task.Run(async () =>
        {
            await Task.Delay(5000);

            lock (_bigIceSealLock)
            {
                if (!IsPhase(_phase, 1.2) || !_bigIceSealActive || _bigIceSealSecondGuideDone)
                    return;

                _bigIceSealTetherDetectionEnabled = true;
            }

            sa.Debug("神像2第二段002D检测已开启：现在收到自己的002D才会指路。");
        });
    }

    private void SendBigIceSeal002DSecondGuide(
        ScriptAccessory sa,
        BigIceSealGuideMode modeSnapshot,
        double phaseSnapshot,
        Vector3? firstGuidePosSnapshot)
    {
        int myIdx = sa.MyIndex();

        if (myIdx < 0 || myIdx > 7)
        {
            sa.Debug($"扩大大冰封002D第二段失败：自己的 index 异常。MyIndex={myIdx}");
            return;
        }

        Vector3? myGuidePos2 = null;

        if (modeSnapshot == BigIceSealGuideMode.Yan式八人全分摊)
        {
            myGuidePos2 = new Vector3(100.00f, 0.00f, 88.00f);
        }
        else if (firstGuidePosSnapshot != null)
        {
            myGuidePos2 = MoveTowards(firstGuidePosSnapshot.Value, ArenaCenter, 6.50f);
        }

        if (myGuidePos2 == null)
        {
            sa.Debug($"扩大大冰封002D第二段失败：没有第一段安全点。Mode={modeSnapshot}, MyIndex={myIdx}");
            return;
        }

        Vector3 finalGuidePos = myGuidePos2.Value;
        string drawName = $"P1_第二次扩大大冰封指路_002D第二段_{modeSnapshot}_{myIdx}";

        sa.Debug($"第二次扩大大冰封002D第二段已触发：4.5秒后显示指路。Mode={modeSnapshot}, MyIndex={myIdx}, TargetPoint={finalGuidePos}");

        _ = Task.Run(async () =>
        {
            await Task.Delay(4500);

            if (!IsPhase(_phase, 1.2))
            {
                sa.Debug($"扩大大冰封002D第二段跳过：延迟后 Phase={_phase}，已经不在神像2。");
                return;
            }

            var guideDp2 = sa.WaypointDp(
                finalGuidePos,
                5000,
                0,
                drawName
            );

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, guideDp2);

            sa.Debug($"""
            第二次扩大大冰封002D第二段指路:
            Trigger=Self002D_Delay
            Phase={phaseSnapshot}
            CurrentPhase={_phase}
            Mode={modeSnapshot}
            MyIndex={myIdx}
            FirstPoint={firstGuidePosSnapshot}
            TargetPoint={finalGuidePos}
            DrawName={drawName}
            """);
        });
    }

    #endregion

    #region P1 神像3（箭头 + 002D）

    #region 神像3 变量

    private readonly object _statue3ArrowLock = new();

    private int _statue3ArrowCount = 0;
    private int? _statue3SmallDir4 = null;
    private int? _statue3LargeDir4 = null;
    private bool? _statue3SmallIsShort = null;
    private bool _statue3ArrowGuideSent = false;
    private bool _statue3TetherGuideDone = false;

    private const uint Statue3SmallArrowMinStatus = 0x130C;
    private const uint Statue3SmallArrowMaxStatus = 0x130F;

    private static readonly Vector3 Statue3SleepSourcePos = new(107.00f, 8.50f, 43.00f);

    private static readonly Vector3 Statue3CharmSourcePos = new(95f, 27f, 25f);

    #endregion

    [ScriptMethod(
        name: "P1_神像3_箭头指路",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(4876|4877|4878|4879|5079|5080|5081|5082|130C|130D|130E|130F|13D7|13D8|13D9|13DA)$"])]
    public void P1_神像3_箭头指路(Event evt, ScriptAccessory sa)
    {
        if (_phase >= 2.0)
            return;

        // 神像3箭头 buff 是神像3专属；如果阶段控制 48370 漏了，用它兜底切到 1.3。
        if (!IsPhase(_phase, 1.3))
        {
            _phase = 1.3;
            _p1StatueCount = Math.Max(_p1StatueCount, 3);
            sa.Debug($"神像3箭头触发，兜底修正 Phase=1.3。Old status event entered with Phase={_phase}");
        }

        uint targetId = evt.TargetId();
        if (targetId != 0 && targetId != sa.Data.Me)
            return;

        uint statusId = evt.StatusId();
        sa.Debug($"神像3statusID: StatusID={statusId}");
        int? dir4 = GetStatue3ArrowDir4(statusId);
        if (dir4 == null)
        {
            sa.Debug($"神像3箭头记录失败：未知 StatusID={statusId}");
            return;
        }

        uint sourceId = evt.SourceId();
        Vector3 sourcePos = evt.SourcePosition();
        double duration = evt.Duration();

        if (sourceId == 0 || sourcePos == Vector3.Zero || duration > 100)
        {
            sa.Debug($"""
            神像3箭头忽略旧/无效StatusAdd:
            StatusID=0x{statusId:X}
            SourceId=0x{sourceId:X}
            SourcePos={sourcePos}
            Duration={duration}
            Phase={_phase}
            """);
            return;
        }

        bool isSmallId = statusId >= Statue3SmallArrowMinStatus && statusId <= Statue3SmallArrowMaxStatus;

        bool shouldSendGuide = false;
        bool sameBuff;
        bool smallIsShort;
        int smallDir4;
        int largeDir4;

        lock (_statue3ArrowLock)
        {
            if (_statue3ArrowGuideSent)
                return;

            _statue3ArrowCount++;

            if (isSmallId)
            {
                _statue3SmallDir4 = dir4.Value;
                _statue3SmallIsShort = duration < 8.0;
            }
            else
            {
                _statue3LargeDir4 = dir4.Value;
            }

            if (_statue3ArrowCount < 2)
            {
                sa.Debug($"神像3箭头记录：Count={_statue3ArrowCount}, StatusID=0x{statusId:X}, IsSmall={isSmallId}, Dir4={dir4.Value}, Duration={duration}, Phase={_phase}");
                return;
            }

            
            sameBuff = !_statue3SmallDir4.HasValue || !_statue3LargeDir4.HasValue;
            smallIsShort = _statue3SmallIsShort ?? false;
            smallDir4 = _statue3SmallDir4 ?? 0;
            largeDir4 = _statue3LargeDir4 ?? 0;

            sa.Debug($"神像3箭头记录完毕：Count={_statue3ArrowCount}, StatusID=0x{statusId:X}, IsSmall={isSmallId}, Dir4={dir4.Value}, Duration={duration}, sameBuff={sameBuff}, smallIsShort={smallIsShort}, ");

            _statue3ArrowGuideSent = true;
            shouldSendGuide = true;
        }

        if (!shouldSendGuide)
            return;

        var plan = GetStatue3ArrowPlan(Statue3ArrowMode, sameBuff, smallIsShort, smallDir4, largeDir4);
        Vector3 firstPos = RotateStatue3RelativePoint(plan.Dir4, plan.FirstRelative);
        Vector3 secondPos = RotateStatue3RelativePoint(plan.Dir4, plan.SecondRelative);

        var firstDp = sa.WaypointDp(
            firstPos,
            7500,
            0,
            $"P1_神像3_箭头指路_第一段_{Statue3ArrowMode}",
            new Vector4(0, 1, 0, 1)
        );

        var secondDp = sa.WaypointDp(
            secondPos,
            3000,
            7500,
            $"P1_神像3_箭头指路_第二段_{Statue3ArrowMode}",
            new Vector4(0, 1, 0, 1)
        );

        var firstToSecondDp = sa.WaypointDp(
            secondPos,
            7500,
            0,
            $"P1_神像3_箭头指路_第一段到第二段提前观察_{Statue3ArrowMode}",
            new Vector4(1, 1, 1, 1)
        );
        firstToSecondDp.Position = firstPos;
        firstToSecondDp.Owner = 0;

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, firstDp);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, secondDp);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, firstToSecondDp);

        sa.Debug($"""
        神像3箭头指路:
        Phase={_phase}
        Mode={Statue3ArrowMode}
        SameBuff={sameBuff}
        SmallIsShort={smallIsShort}
        SmallDir4={smallDir4}
        LargeDir4={largeDir4}
        FinalDir4={plan.Dir4}
        FirstRelative={plan.FirstRelative}
        SecondRelative={plan.SecondRelative}
        FirstPoint={firstPos}
        SecondPoint={secondPos}
        Callout={plan.Callout}
        """);
    }

    [ScriptMethod(
        name: "P1_神像3_魅惑睡眠指路",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:002D"])]
    public void P1_神像3_魅惑睡眠指路(Event evt, ScriptAccessory sa)
    {
        if (!IsPhase(_phase, 1.3))
            return;

        uint sourceId = evt.SourceId(); // 众神之像
        uint targetId = evt.TargetId(); // 被连线玩家

        if (targetId != sa.Data.Me)
            return;

        int myIdx = sa.MyIndex();

        if (myIdx < 0 || myIdx > 7)
        {
            sa.Debug($"神像3 002D指路失败：自己的 index 异常。MyIndex={myIdx}");
            return;
        }

        Vector3 statueSourcePos = evt.SourcePosition();

        Statue3TetherType tetherType = GetStatue3TetherType(statueSourcePos);
        Vector3? wpos = GetStatue3TetherGuidePos(myIdx, tetherType, Statue3TetherMode);

        if (wpos == null)
        {
            sa.Debug($"""
            神像3 002D指路失败：没有匹配到点位或未识别睡眠/魅惑。
            Phase={_phase}
            Mode={Statue3TetherMode}
            TetherType={tetherType}
            MyIndex={myIdx}
            SourceId=0x{sourceId:X}
            TargetId=0x{targetId:X}
            StatueSourcePos={statueSourcePos}
            """);
            return;
        }

        lock (_statue3ArrowLock)
        {
            if (_statue3TetherGuideDone)
                return;

            _statue3TetherGuideDone = true;
        }

        var dp = sa.WaypointDp(
            wpos.Value,
            5000,
            5000,
            $"P1_神像3_002D指路_{Statue3TetherMode}_{tetherType}_{myIdx}"
        );

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        sa.Debug($"""
        神像3 002D指路:
        Phase={_phase}
        Mode={Statue3TetherMode}
        TetherType={tetherType}
        MyIndex={myIdx}
        SourceId=0x{sourceId:X}
        TargetId=0x{targetId:X}
        StatueSourcePos={statueSourcePos}
        TargetPoint={wpos.Value}
        """);
    }

    private void ResetStatue3ArrowState()
    {
        lock (_statue3ArrowLock)
        {
            _statue3ArrowCount = 0;
            _statue3SmallDir4 = null;
            _statue3LargeDir4 = null;
            _statue3SmallIsShort = null;
            _statue3ArrowGuideSent = false;
            _statue3TetherGuideDone = false;
        }
    }

    #endregion

    #region 工具方法

    private readonly struct Statue3ArrowPlan
    {
        public Statue3ArrowPlan(int dir4, Vector2 firstRelative, Vector2 secondRelative, string callout)
        {
            Dir4 = dir4;
            FirstRelative = firstRelative;
            SecondRelative = secondRelative;
            Callout = callout;
        }

        public int Dir4 { get; }
        public Vector2 FirstRelative { get; }
        public Vector2 SecondRelative { get; }
        public string Callout { get; }
    }

    private static int? GetStatue3ArrowDir4(uint statusId)
    {
        return statusId switch
        {
            0x130C or 0x13D7 => 0,
            0x130D or 0x13D8 => 2,
            0x130E or 0x13D9 => 3,
            0x130F or 0x13DA => 1,
            _ => null,
        };
    }

    private static Statue3ArrowPlan GetStatue3ArrowPlan(
        Statue3ArrowGuideMode mode,
        bool sameBuff,
        bool smallIsShort,
        int smallDir4,
        int largeDir4)
    {
        int dir4;
        Vector2 first;
        Vector2 second;
        string callout;

        switch (mode)
        {
            case Statue3ArrowGuideMode._1A:
                dir4 = sameBuff ? (Math.Max(smallDir4, largeDir4) + 3) % 4 : smallDir4;
                first = sameBuff
                    ? new Vector2(0, -14)
                    : smallIsShort ? new Vector2(6, -9) : new Vector2(0, -8);
                second = sameBuff
                    ? new Vector2(6, -14)
                    : smallIsShort ? new Vector2(0, -8) : new Vector2(6, -9);
                callout = sameBuff
                    ? $"{CardinalDirText(dir4)}外侧放两次"
                    : $"{CardinalDirText(dir4)}内侧，{(smallIsShort ? "先斜后正" : "先正后斜")}";
                break;

            case Statue3ArrowGuideMode._1A改:
                dir4 = sameBuff ? (Math.Max(smallDir4, largeDir4) + 2) % 4 : smallDir4;
                first = sameBuff
                    ? new Vector2(6, -9)
                    : smallIsShort ? new Vector2(0, -8) : new Vector2(0, -14);
                second = sameBuff
                    ? new Vector2(6, -14)
                    : smallIsShort ? new Vector2(0, -14) : new Vector2(0, -8);
                callout = sameBuff
                    ? $"{CardinalDirText(dir4)}斜方向，放两次"
                    : $"{CardinalDirText(dir4)}正方向，{(smallIsShort ? "先近后远" : "先远后近")}";
                break;

            case Statue3ArrowGuideMode.方顺:
                dir4 = sameBuff ? (Math.Max(smallDir4, largeDir4) + 1) % 4 : (largeDir4 + 2) % 4;
                first = sameBuff
                    ? new Vector2(0, -12)
                    : smallIsShort ? new Vector2(6, -12) : new Vector2(12, -12);
                second = sameBuff
                    ? new Vector2(-6, -12)
                    : smallIsShort ? new Vector2(12, -12) : new Vector2(6, -12);
                callout = sameBuff
                    ? $"{CardinalDirText(dir4)}正点放两次"
                    : $"{DiagonalClockwiseDirText(dir4)}，{(smallIsShort ? "先角后边" : "先边后角")}";
                break;

            case Statue3ArrowGuideMode.方逆:
            default:
                dir4 = sameBuff ? (Math.Max(smallDir4, largeDir4) + 3) % 4 : (smallDir4 + 2) % 4;
                first = sameBuff
                    ? new Vector2(0, -12)
                    : smallIsShort ? new Vector2(-12, -12) : new Vector2(-6, -12);
                second = sameBuff
                    ? new Vector2(6, -12)
                    : smallIsShort ? new Vector2(-6, -12) : new Vector2(-12, -12);
                callout = sameBuff
                    ? $"{CardinalDirText(dir4)}正点放两次"
                    : $"{DiagonalCounterClockwiseDirText(dir4)}，{(smallIsShort ? "先角后边" : "先边后角")}";
                break;
        }

        return new Statue3ArrowPlan(dir4, first, second, callout);
    }

    private static Vector3 RotateStatue3RelativePoint(int dir4, Vector2 relative)
    {
        float x = relative.X;
        float z = relative.Y;

        (float rotatedX, float rotatedZ) = ((dir4 % 4 + 4) % 4) switch
        {
            0 => (x, z),
            1 => (z, -x),
            2 => (-x, -z),
            3 => (-z, x),
            _ => (x, z),
        };

        return new Vector3(100.00f + rotatedX, 0.00f, 100.00f + rotatedZ);
    }

    private static string CardinalDirText(int dir4)
    {
        return ((dir4 % 4 + 4) % 4) switch
        {
            0 => "上北",
            1 => "左西",
            2 => "下南",
            3 => "右东",
            _ => "未知",
        };
    }

    private static string DiagonalCounterClockwiseDirText(int dir4)
    {
        return ((dir4 % 4 + 4) % 4) switch
        {
            0 => "左上",
            1 => "左下",
            2 => "右下",
            3 => "右上",
            _ => "未知",
        };
    }

    private static string DiagonalClockwiseDirText(int dir4)
    {
        return ((dir4 % 4 + 4) % 4) switch
        {
            0 => "右上",
            1 => "左上",
            2 => "左下",
            3 => "右下",
            _ => "未知",
        };
    }

    private static Statue3TetherType GetStatue3TetherType(Vector3 statueSourcePos)
    {
        const float deviation = 1.0f;

        if (Vector3.Distance(statueSourcePos, Statue3SleepSourcePos) < deviation)
            return Statue3TetherType.睡眠;

        if (Vector3.Distance(statueSourcePos, Statue3CharmSourcePos) < deviation)
            return Statue3TetherType.魅惑;

        return Statue3TetherType.Unknown;
    }

    private static Vector3? GetStatue3TetherGuidePos(
        int myIdx,
        Statue3TetherType tetherType,
        Statue3TetherGuideMode mode)
    {
        if (myIdx < 0 || myIdx > 7)
            return null;

        if (mode == Statue3TetherGuideMode.全固定)
        {
            return myIdx switch
            {
                0 => new Vector3(100f, 0.00f, 91f),
                1 => new Vector3(109f, 0.00f, 100f),
                2 => new Vector3(85f, 0.00f, 100f),
                3 => new Vector3(100f, 0.00f, 115f),
                4 => new Vector3(91f, 0.00f, 100f),
                5 => new Vector3(100f, 0.00f, 109f),
                6 => new Vector3(100f, 0.00f, 85f),
                7 => new Vector3(115f, 0.00f, 100f),
                _ => null,
            };
        }

        return tetherType switch
        {
            // 魅惑在外：睡眠走内侧，魅惑走外侧。
            // 魅惑在内：睡眠/魅惑点位反过来。
            Statue3TetherType.睡眠 => mode == Statue3TetherGuideMode.魅惑在内
                ? GetStatue3TetherOuterPos(myIdx)
                : GetStatue3TetherInnerPos(myIdx),

            Statue3TetherType.魅惑 => mode == Statue3TetherGuideMode.魅惑在内
                ? GetStatue3TetherInnerPos(myIdx)
                : GetStatue3TetherOuterPos(myIdx),

            _ => null,
        };
    }

    private static Vector3? GetStatue3TetherInnerPos(int myIdx)
    {
        return myIdx switch
        {
            0 or 6 => new Vector3(100f, 0.00f, 91f),
            1 or 7 => new Vector3(109f, 0.00f, 100f),
            2 or 4 => new Vector3(91f, 0.00f, 100f),
            3 or 5 => new Vector3(100f, 0.00f, 109f),
            _      => null,
        };
    }

    private static Vector3? GetStatue3TetherOuterPos(int myIdx)
    {
        return myIdx switch
        {
            0 or 6 => new Vector3(100f, 0.00f, 85f),
            1 or 7 => new Vector3(115f, 0.00f, 100f),
            2 or 4 => new Vector3(85f, 0.00f, 100f),
            3 or 5 => new Vector3(100f, 0.00f, 115f),
            _      => null,
        };
    }

    private static Vector3? GetBigIceSealInitialPos(
        BigIceSealGuideMode mode,
        int index,
        bool safeIsLeftUpRightDown,
        BigIceSealMtUpperHalfTankMode mtUpperHalfTankMode)
    {
        return mode switch
        {
            BigIceSealGuideMode.斜角黑泥 => GetBigIceSealDiagonalBlackMudPos(index, safeIsLeftUpRightDown, mtUpperHalfTankMode),
            BigIceSealGuideMode._1A四人分摊_TH上半场 => GetBigIceSeal1AThUpperHalfPos(index, safeIsLeftUpRightDown),
            BigIceSealGuideMode._1A四人分摊_MT组上半场 => GetBigIceSeal1AMtUpperHalfPos(index, safeIsLeftUpRightDown, mtUpperHalfTankMode),
            BigIceSealGuideMode._1A四人分摊_T远北奶近南 => GetBigIceSeal1ATFarNorthHealerNearSouthPos(index, safeIsLeftUpRightDown),
            BigIceSealGuideMode.Yan式八人全分摊 => GetBigIceSealYanEightStackPos(safeIsLeftUpRightDown),
            _ => null,
        };
    }

    private static Vector3? GetBigIceSealDiagonalBlackMudPos(
        int index,
        bool safeIsLeftUpRightDown,
        BigIceSealMtUpperHalfTankMode mtUpperHalfTankMode)
    {
        int groupIndex = GetBigIceSealTankGroupIndex(index, mtUpperHalfTankMode);

        return groupIndex switch
        {
            2 or 4 => safeIsLeftUpRightDown
                ? new Vector3(84.39f, 0.00f, 90.18f)
                : new Vector3(84.30f, 0.00f, 109.84f),

            0 or 6 => safeIsLeftUpRightDown
                ? new Vector3(90.25f, 0.00f, 83.63f)
                : new Vector3(109.65f, 0.00f, 84.57f),

            3 or 5 => safeIsLeftUpRightDown
                ? new Vector3(110.09f, 0.00f, 115.08f)
                : new Vector3(89.38f, 0.00f, 115.14f),

            1 or 7 => safeIsLeftUpRightDown
                ? new Vector3(115.83f, 0.00f, 109.33f)
                : new Vector3(115.92f, 0.00f, 90.07f),

            _ => null,
        };
    }

    private static Vector3? GetBigIceSeal1AThUpperHalfPos(int index, bool safeIsLeftUpRightDown)
    {
        return index switch
        {
            0 or 1 or 2 or 3 => safeIsLeftUpRightDown
                ? new Vector3(99.50f, 0.00f, 81.00f)
                : new Vector3(100.50f, 0.00f, 81.00f),

            4 or 5 or 6 or 7 => safeIsLeftUpRightDown
                ? new Vector3(100.00f, 0.00f, 119.50f)
                : new Vector3(100.00f, 0.00f, 118.50f),

            _ => null,
        };
    }

    private static Vector3? GetBigIceSeal1ATFarNorthHealerNearSouthPos(int index, bool safeIsLeftUpRightDown)
    {
        // T远北奶近南：固定 0/1/6/7 去北，2/3/4/5 去南。
        return index switch
        {
            0 or 1 or 6 or 7 => safeIsLeftUpRightDown
                ? new Vector3(99.50f, 0.00f, 81.00f)
                : new Vector3(100.50f, 0.00f, 81.00f),

            2 or 3 or 4 or 5 => safeIsLeftUpRightDown
                ? new Vector3(100.50f, 0.00f, 119f)
                : new Vector3(100.00f, 0.00f, 118.50f),

            _ => null,
        };
    }

    private static Vector3? GetBigIceSeal1AMtUpperHalfPos(
        int index,
        bool safeIsLeftUpRightDown,
        BigIceSealMtUpperHalfTankMode mtUpperHalfTankMode)
    {
        int groupIndex = GetBigIceSealTankGroupIndex(index, mtUpperHalfTankMode);

        return groupIndex switch
        {
            0 or 2 or 4 or 6 => safeIsLeftUpRightDown
                ? new Vector3(99.50f, 0.00f, 81.00f)
                : new Vector3(100.50f, 0.00f, 81.00f),

            1 or 3 or 5 or 7 => safeIsLeftUpRightDown
                ? new Vector3(100.50f, 0.00f, 119.00f)
                : new Vector3(99.50f, 0.00f, 119.00f),

            _ => null,
        };
    }

    private static int GetBigIceSealTankGroupIndex(int index, BigIceSealMtUpperHalfTankMode mtUpperHalfTankMode)
    {
        if (mtUpperHalfTankMode == BigIceSealMtUpperHalfTankMode.交换分组_ST上半场)
        {
            return index switch
            {
                0 => 1,
                1 => 0,
                _ => index,
            };
        }

        return index;
    }

    private static Vector3? GetBigIceSealYanEightStackPos(bool safeIsLeftUpRightDown)
    {
        return safeIsLeftUpRightDown
            ? new Vector3(100.50f, 0.00f, 112.00f)
            : new Vector3(99.50f, 0.00f, 112.00f);
    }

    private static Vector3 MoveTowards(Vector3 from, Vector3 to, float distance)
    {
        Vector3 dir = to - from;
        dir.Y = 0;

        if (dir.LengthSquared() <= 0.0001f)
            return from;

        dir = Vector3.Normalize(dir);
        return from + dir * distance;
    }

    private static Vector3? Get47784GuidePosForGroup(
        uint me,
        List<uint> groupMembers,
        IReadOnlyList<uint> party,
        Dictionary<uint, Vector3> targetPositions)
    {
        if (!groupMembers.Contains(me))
            return null;

        if (targetPositions.ContainsKey(me))
            return null;

        var targetedMembers = groupMembers
            .Where(id => targetPositions.ContainsKey(id))
            .OrderBy(id => PartyIndex(party, id))
            .ToList();

        var untargetedMembers = groupMembers
            .Where(id => !targetPositions.ContainsKey(id))
            .OrderBy(id => PartyIndex(party, id))
            .ToList();

        if (targetedMembers.Count != 2 || untargetedMembers.Count != 2)
            return null;

        int myOrder = untargetedMembers.IndexOf(me);

        if (myOrder < 0 || myOrder >= targetedMembers.Count)
            return null;

        uint targetToGo = targetedMembers[myOrder];

        return targetPositions[targetToGo];
    }

    private static Vector3 GetFixedHalfPos(bool isLeftHalf, bool has002D, bool upperLeftHasIce)
    {
        if (isLeftHalf)
        {
            // 0/1/2/3：固定左半场
            if (has002D)
            {
                return upperLeftHasIce
                    ? UpperLeftPoint
                    : UpperLeftAreaUpperPoint;
            }

            // 没002D
            return upperLeftHasIce
                ? LowerLeftPoint
                : UpperLeftPoint;
        }

        // 4/5/6/7：固定右半场
        if (has002D)
        {
            return upperLeftHasIce
                ? UpperRightAreaUpperPoint
                : UpperRightPoint;
        }

        // 没002D
        return upperLeftHasIce
            ? UpperRightPoint
            : LowerRightPoint;
    }

    private static int PartyIndex(IReadOnlyList<uint> party, uint playerId)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] == playerId)
                return i;
        }

        return -1;
    }

    private static bool IsPhase(double current, double target)
    {
        return Math.Abs(current - target) < 0.0001;
    }

    private static bool IsLeftUpRightDownByRotation(float rotation)
    {
        double degree = rotation * 180.0 / Math.PI;
        degree = (degree % 360.0 + 360.0) % 360.0;

        double distanceTo45Or225 = Math.Min(
            AngleDistance(degree, 45.0),
            AngleDistance(degree, 225.0)
        );

        double distanceTo135Or315 = Math.Min(
            AngleDistance(degree, 135.0),
            AngleDistance(degree, 315.0)
        );

        return distanceTo45Or225 <= distanceTo135Or315;
    }

    private static double AngleDistance(double a, double b)
    {
        double diff = Math.Abs(a - b) % 360.0;
        return diff > 180.0 ? 360.0 - diff : diff;
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

        if (string.IsNullOrEmpty(idStr))
            return false;

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

    private static bool TryParseUIntAny(string? raw, out uint id)
    {
        id = 0;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = raw.Trim();

        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(
                raw[2..],
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out id
            );
        }

        if (raw.Any(c => c is >= 'A' and <= 'F' or >= 'a' and <= 'f'))
        {
            return uint.TryParse(
                raw,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out id
            );
        }

        return uint.TryParse(
            raw,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out id
        );
    }

    public static uint ActionId(this Event evt)
        => JsonConvert.DeserializeObject<uint>(evt["ActionId"]);

    public static uint DataId(this Event evt)
        => JsonConvert.DeserializeObject<uint>(evt["DataId"]);

    public static uint StatusId(this Event evt)
    {
        if (TryParseUIntAny(evt["StatusID"], out var id))
            return id;

        if (TryParseUIntAny(evt["StatusId"], out id))
            return id;

        if (TryParseUIntAny(evt["ActionId"], out id))
            return id;

        return 0;
    }

    public static double Duration(this Event evt)
    {
        return double.TryParse(
            evt["Duration"],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var duration
        )
            ? duration
            : 0.0;
    }

    public static uint SourceId(this Event evt)
        => ParseHexId(evt["SourceId"], out var id) ? id : 0;

    public static uint TargetId(this Event evt)
        => ParseHexId(evt["TargetId"], out var id) ? id : 0;

    public static uint IconId(this Event evt)
    {
        var raw = evt["Id"];

        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        raw = raw.Trim();

        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            raw = raw[2..];

        return uint.TryParse(
            raw,
            System.Globalization.NumberStyles.HexNumber,
            null,
            out var result
        )
            ? result
            : 0;
    }

    public static Vector3 SourcePosition(this Event evt)
        => ParseVector3(evt["SourcePosition"]);

    public static Vector3 EffectPosition(this Event evt)
        => ParseVector3(evt["EffectPosition"]);

    public static Vector3 TargetPosition(this Event evt)
        => ParseVector3(evt["TargetPosition"]);

    public static uint DirectorId(this Event evt)
        => ParseHexId(evt["DirectorId"], out var id) ? id : 0;

    private static Vector3 ParseVector3(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Vector3.Zero;

        raw = raw.Trim();

        try
        {
            return JsonConvert.DeserializeObject<Vector3>(raw);
        }
        catch
        {
        }

        try
        {
            float x = ExtractFloat(raw, "X");
            float y = ExtractFloat(raw, "Y");
            float z = ExtractFloat(raw, "Z");
            return new Vector3(x, y, z);
        }
        catch
        {
            return Vector3.Zero;
        }
    }

    private static float ExtractFloat(string raw, string key)
    {
        var pattern = $@"{key}\s*[:=]\s*(-?\d+(?:[\.,]\d+)?)";
        var match = System.Text.RegularExpressions.Regex.Match(
            raw,
            pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        if (!match.Success)
            return 0f;

        var value = match.Groups[1].Value.Replace(',', '.');

        return float.Parse(
            value,
            System.Globalization.CultureInfo.InvariantCulture
        );
    }
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

    public static DrawPropertiesEdit WaypointDp(
        this ScriptAccessory sa,
        Vector3 target,
        uint duration,
        uint delay = 0,
        string name = "Waypoint",
        Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.TargetPosition = target;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }

    public static DrawPropertiesEdit WaypointFromToDp(
        this ScriptAccessory sa,
        Vector3 from,
        Vector3 to,
        uint duration,
        uint delay = 0,
        string name = "WaypointFromTo",
        Vector4? color = null)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? sa.Data.DefaultSafeColor;
        dp.Position = from;
        dp.Owner = 0;
        dp.TargetPosition = to;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }

    public static void Debug(this ScriptAccessory sa, object message)
    {
        if (!Kefka.DebugEnabled)
            return;

        sa.Method.SendChat($"/e [Kefka] {message}");
    }
}

#endregion
