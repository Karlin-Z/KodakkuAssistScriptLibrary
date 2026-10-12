using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Script;

#nullable disable

namespace RyougiMioScriptNamespace
{
    [ScriptType(name: "(妖星乱舞绝境战)P4 指路&自动移动", territorys: [1363], guid: "79ae48d3-c462-4e4a-8108-9eb507e131b2", version: "0.0.1.3", author: "RyougiMio", note: "P4脚本。\n鸳鸯锅:攻击12345678左 其他圈右\n后续:攻击12是钢铁 禁止12是背对 锁链12是正对\n!!!!!!!!自动移动依赖于PromeRotation!!!!!!!!")]
    public class Script1363P4
    {
        #region Settings

        [UserSetting("-----全局设置-----")]
        public bool _____Global_Settings_____ { get; set; } = true;

        [UserSetting("是否开启屏幕文字提示")]
        public bool EnableText { get; set; } = true;

        [UserSetting("是否开启TTS语音提示")]
        public bool EnableTTS { get; set; } = true;

        [UserSetting("鸳鸯锅标记")]
        public bool EnableP4HotpotMarks { get; set; } = false;

        [UserSetting("后续机制标记")]
        public bool EnableP4LaterMarks { get; set; } = false;

        [UserSetting("P4真假预测播报(Off关闭 Echo默语 Party小队频道)")]
        public P4PlanMessageMode PlanMessageMode { get; set; } = P4PlanMessageMode.Off;

        [UserSetting("启用PR GreenMove自动移动")]
        public bool EnableGreenMove { get; set; } = false;

        [UserSetting("PR移动判定距离")]
        public float GreenMoveTolerance { get; set; } = 0.1f;

        [UserSetting("PR移动速度倍率")]
        public float GreenMoveSpeedMultiplier { get; set; } = 1.0f;

        [UserSetting("PR移动忽略Y轴")]
        public bool GreenMoveIgnoreY { get; set; } = true;

        [UserSetting("PR移动前清空队列")]
        public bool GreenMoveClearQueueBeforeMove { get; set; } = true;

        [UserSetting("PR移动最大读条等待ms")]
        public int GreenMoveMaxCastWaitMs { get; set; } = 2500;

        [UserSetting("PR移动等待队列读条/滑步")]
        public bool GreenMoveWaitForQueuedCast { get; set; } = true;

        [UserSetting("Developer mode")]
        public bool DeveloperMode { get; set; } = false;

        [UserSetting("常用危险色")]
        public ScriptColor DangerColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 0.0f, 0.0f, 0.01f) };

        [UserSetting("常用安全色")]
        public ScriptColor SafeColor { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 0.0f, 0.01f) };

        [UserSetting("指路/引导颜色")]
        public ScriptColor GuideColor { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

        #endregion

        #region State

        private enum Phase
        {
            Init,
            P4,
            Done,
        }

        public enum P4PlanMessageMode
        {
            Off,
            Echo,
            Party,
        }

        private enum P4HalfSide
        {
            Unknown,
            Left,
            Right,
        }

        private enum P4HalfColor
        {
            Unknown,
            Blue,
            Purple,
        }

        private enum P4ChainStep
        {
            None,
            Move,
            Petrify,
            Element,
        }

        private enum P4ClockDirection
        {
            Unknown,
            Twelve,
            Three,
            Six,
            Nine,
        }

        private enum P4ElementCall
        {
            Unknown,
            FireSteel,
            WaterMoon,
        }

        private enum P4BuffGroupKind
        {
            Unknown,
            A,
            B,
            C,
            D,
            E,
            F,
        }

        private enum P4DangerShape
        {
            Rect,
            Fan,
        }

        private enum P4BuffCategory
        {
            Mixed,
            ThunderShare,
            Petrify,
            Element,
        }

        private sealed class P4BuffRecord
        {
            public int PartyIndex;
            public uint StatusId;
            public int State;
            public int DurationMs;
            public long ExpiresAt;
            public int SnapshotRemainingMs;
        }

        private sealed class P4BuffGroup
        {
            public P4BuffGroupKind Kind;
            public P4BuffCategory Category;
            public int DurationMs;
            public int SnapshotRemainingMs;
            public long ExpiresAt;
            public readonly List<P4BuffRecord> Records = new List<P4BuffRecord>();
        }

        private sealed class P4DangerRecord
        {
            public uint ActionId;
            public P4DangerShape Shape;
            public Vector3 Position;
            public float Rotation;
            public long CapturedAt;
        }

        private const uint InvalidObjectId = 0xE0000000;
        private const uint P4StartActionId = 49884;
        private const uint P4PurpleSourceActionId = 50068;
        private const uint P4ResolveActionId = 50069;
        private const uint P4ChainStartActionEffectId = 50070;
        private const uint P4TruthStatusId = 2056;
        private const uint P4XDataId = 19510;
        private const uint P4CDataId = 19507;
        private const int P4StatusWindowMs = 11000;
        private const int P4GuideDurationMs = 6000;
        private const int P4ChainGuideDurationMs = 8000;
        private const int P4ChainDelayMs = 500;
        private const int P4LaterMarkApplyDelayMs = 300;
        private const int P4StateUnknown = 0;
        private const int P4StateTrue = 1;
        private const int P4StateFalse = 2;
        private const float P4GuideOffset = 4.0f;
        private const float P4ChainGuideOffset = 8.0f;
        private const int P4BuffGroupToleranceMs = 1200;
        private const int P4MaxScheduledGroupCount = 6;
        private const float P4RectDangerWidth = 10.0f;
        private const float P4RectDangerLength = 40.0f;
        private const float P4FanDangerRadius = 40.0f;
        private const float P4FanDangerRadian = MathF.PI / 2.0f;
        private const int P4BRectLookbackMs = 7000;
        private const int P4DFanLookbackMs = 10000;
        private const string DrawPrefix = "KDYD_P4";
        private const string GreenMovePrefix = "PromeRotation.GreenMove.";
        private const string DebugLogPath = @"e:\game\Workplace\Kodd\KDYD_P4_debug.log";

        private static readonly Vector3 P4ArenaCenter = new Vector3(100.0f, 0.0f, 100.0f);
        private static readonly Vector3 DefaultNorth = new Vector3(0.0f, 0.0f, -1.0f);
        private static readonly Vector4 SolidDangerRed = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        private static readonly string[] PartyPriorityLabels = new[] { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
        private static readonly uint[] P4TrackedStatusIds = new uint[] { 5541, 5542, 5543, 5544, 5545, 5546, 5547, 5548, 454, 1382 };
        private static readonly MarkType[] P4LeftMarks = new[] { MarkType.Attack1, MarkType.Attack2, MarkType.Attack3, MarkType.Attack4, MarkType.Attack5, MarkType.Attack6, MarkType.Attack7, MarkType.Attack8 };
        private static readonly MarkType[] P4RightMarks = new[] { MarkType.Bind1, MarkType.Bind2, MarkType.Bind3, MarkType.Circle, MarkType.Stop1, MarkType.Stop2, MarkType.Cross, MarkType.Square };
        private static readonly MarkType[] P4PetrifyTrueMarks = new[] { MarkType.Stop1, MarkType.Stop2 };
        private static readonly MarkType[] P4PetrifyFalseMarks = new[] { MarkType.Bind1, MarkType.Bind2 };
        private static readonly float[] P4CardinalClocks = new[] { 0.0f, 3.0f, 6.0f, 9.0f };
        private static readonly float[] P4FinalHalfClocks = new[] { 11.5f, 12.5f, 2.5f, 3.5f, 5.5f, 6.5f, 8.5f, 9.5f };

        private ScriptAccessory _acc;
        private Phase _phase = Phase.Init;
        private int _generation;
        private readonly object _p4Lock = new object();
        private readonly object _p4CommandMarkTimerLock = new object();
        private readonly int[,] _p4StatusStateByPartyAndStatus = new int[8, 10];
        private readonly long[,] _p4StatusExpiresAtByPartyAndStatus = new long[8, 10];
        private readonly P4HalfSide[] _p4ResolvedSideByPartyIndex = new P4HalfSide[8];
        private readonly P4HalfColor[] _p4ResolvedColorByPartyIndex = new P4HalfColor[8];
        private readonly MarkType?[] _p4CommandMarkByPartyIndex = new MarkType?[8];
        private readonly List<P4BuffRecord> _p4BuffRecords = new List<P4BuffRecord>();
        private readonly List<P4BuffGroup> _p4BuffGroups = new List<P4BuffGroup>();
        private readonly List<P4DangerRecord> _p4DangerRecords = new List<P4DangerRecord>();
        private readonly object _p4DebugFileLock = new object();
        private Timer _p4CommandMarkClearTimer;
        private int _p4LaterMarkApplyToken;
        private P4ChainStep _p4ChainStep = P4ChainStep.None;
        private int _p4ChainRound;
        private int _p4ChainStepGeneration;
        private bool _p4BuffGroupsScheduled;
        private bool _p4BuffGroupsScheduleQueued;
        private bool _p4FGroupDangerDrawn;
        private long _p4FGroupStartedAt;
        private P4ElementCall _p4CElementCall = P4ElementCall.Unknown;
        private P4ElementCall _p4FElementCall = P4ElementCall.Unknown;
        private int _p4XParam;
        private long _p4XExpiresAt;
        private long _p4XUpdatedAt;
        private int _p4CParam;
        private long _p4CExpiresAt;
        private long _p4CUpdatedAt;
        private int _p4XEventCount;
        private int _p4GroupSnapshotParam;
        private long _p4GroupSnapshotAt;
        private int _p4FourthXParam;
        private long _p4FourthSnapshotAt;
        private Vector3 _p4FourthTargetPosition;
        private Vector3 _p4FourthNewTwelveDirection;
        private bool _p4FourthDirectionReady;
        private Vector3 _p4HotpotBlueSourcePosition;
        private Vector3 _p4HotpotPurpleSourcePosition;
        private bool _p4HotpotBlueSourceReady;
        private bool _p4HotpotPurpleSourceReady;
        private bool _p4HotpotResolved;
        private object _greenMoveToPointSub;
        private MethodInfo _greenMoveToPointInvoke;
        private object _greenMoveClearQueueSub;
        private MethodInfo _greenMoveClearQueueInvoke;
        private object _greenMoveStopSub;
        private MethodInfo _greenMoveStopInvoke;
        private bool _warnedGreenMove;

        #endregion

        #region Initialization

        public void Init(ScriptAccessory accessory)
        {
            _acc = accessory;
            _phase = Phase.Init;
            _generation++;
            ResetP4State();
            _warnedGreenMove = false;

            accessory.Method.RemoveDraw(".*");
            CommandMarkClear(accessory);
        }

        private void ResetMechanic(ScriptAccessory accessory, bool removeDraw = true)
        {
            _generation++;
            ResetP4State();

            GreenMoveStopAndClear(accessory, "reset mechanic");

            if (removeDraw)
                accessory.Method.RemoveDraw($"{DrawPrefix}_.*");

            CommandMarkClear(accessory);
        }

        private void SetPhase(ScriptAccessory accessory, Phase phase)
        {
            if (_phase == phase) return;

            _phase = phase;
            ResetMechanic(accessory);
        }

        private void ResetP4State()
        {
            CancelP4CommandMarkClearTimer();
            CancelP4LaterCommandMarkApply();

            lock (_p4Lock)
            {
                Array.Clear(_p4StatusStateByPartyAndStatus, 0, _p4StatusStateByPartyAndStatus.Length);
                Array.Clear(_p4StatusExpiresAtByPartyAndStatus, 0, _p4StatusExpiresAtByPartyAndStatus.Length);
                Array.Clear(_p4ResolvedSideByPartyIndex, 0, _p4ResolvedSideByPartyIndex.Length);
                Array.Clear(_p4ResolvedColorByPartyIndex, 0, _p4ResolvedColorByPartyIndex.Length);
                Array.Clear(_p4CommandMarkByPartyIndex, 0, _p4CommandMarkByPartyIndex.Length);
                _p4BuffRecords.Clear();
                _p4BuffGroups.Clear();
                _p4DangerRecords.Clear();

                _p4ChainStep = P4ChainStep.None;
                _p4ChainRound = 0;
                _p4ChainStepGeneration++;
                _p4BuffGroupsScheduled = false;
                _p4BuffGroupsScheduleQueued = false;
                _p4FGroupDangerDrawn = false;
                _p4FGroupStartedAt = 0;
                _p4CElementCall = P4ElementCall.Unknown;
                _p4FElementCall = P4ElementCall.Unknown;
                _p4XParam = 0;
                _p4XExpiresAt = 0;
                _p4XUpdatedAt = 0;
                _p4CParam = 0;
                _p4CExpiresAt = 0;
                _p4CUpdatedAt = 0;
                _p4XEventCount = 0;
                _p4GroupSnapshotParam = 0;
                _p4GroupSnapshotAt = 0;
                _p4FourthXParam = 0;
                _p4FourthSnapshotAt = 0;
                _p4FourthTargetPosition = default;
                _p4FourthNewTwelveDirection = DefaultNorth;
                _p4FourthDirectionReady = false;
                _p4HotpotBlueSourcePosition = default;
                _p4HotpotPurpleSourcePosition = default;
                _p4HotpotBlueSourceReady = false;
                _p4HotpotPurpleSourceReady = false;
                _p4HotpotResolved = false;
            }
        }

        #endregion

        #region Helpers

        private void QTTS(string text, int rate = 0)
        {
            if (!EnableTTS || _acc == null) return;
            _acc.Method.TTS(text, rate);
        }

        private void QText(string text, int duration, bool isWarning = false)
        {
            if (!EnableText || _acc == null) return;
            _acc.Method.TextInfo(text, duration, isWarning);
        }

        private void Alert(string text, int duration = 3000, bool isWarning = true)
        {
            QTTS(text);
            QText(text, duration, isWarning);
        }

        private void DebugEcho(ScriptAccessory accessory, string message)
        {
            if (!DeveloperMode) return;
            accessory.Log.Debug($"[KDYD_P4] {message}");
            accessory.Method.SendChat($"/e [KDYD_P4] {message}");
            try
            {
                lock (_p4DebugFileLock)
                {
                    File.AppendAllText(
                        DebugLogPath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [KDYD_P4] {message}{Environment.NewLine}");
                }
            }
            catch
            {
            }
        }

        private bool AnyP4MarksEnabled => EnableP4HotpotMarks || EnableP4LaterMarks;

        private void CommandMarkClear(ScriptAccessory accessory)
        {
            if (!AnyP4MarksEnabled) return;
            accessory.Method.MarkClear();
        }

        private void CommandMarkPartyMember(ScriptAccessory accessory, int partyIndex, MarkType markType)
        {
            if (!AnyP4MarksEnabled) return;
            if (partyIndex < 0 || partyIndex >= accessory.Data.PartyList.Count) return;

            accessory.Method.Mark(accessory.Data.PartyList[partyIndex], markType);
        }

        private void ClearP4CommandMarksNow(ScriptAccessory accessory)
        {
            CancelP4CommandMarkClearTimer();
            CancelP4LaterCommandMarkApply();
            CommandMarkClear(accessory);
        }

        private void CancelP4LaterCommandMarkApply()
        {
            Interlocked.Increment(ref _p4LaterMarkApplyToken);
        }

        private void CancelP4CommandMarkClearTimer()
        {
            lock (_p4CommandMarkTimerLock)
            {
                _p4CommandMarkClearTimer?.Dispose();
                _p4CommandMarkClearTimer = null;
            }
        }

        private void ReplaceP4CommandMarkClearTimer(Timer timer)
        {
            lock (_p4CommandMarkTimerLock)
            {
                _p4CommandMarkClearTimer?.Dispose();
                _p4CommandMarkClearTimer = timer;
            }
        }

        private void ScheduleP4CommandMarkClear(ScriptAccessory accessory, int generation, int durationMs)
        {
            Timer timer = null;
            timer = new Timer(_ =>
            {
                try
                {
                    if (_generation != generation)
                        return;

                    accessory.Method.MarkClear();
                }
                finally
                {
                    lock (_p4CommandMarkTimerLock)
                    {
                        if (ReferenceEquals(_p4CommandMarkClearTimer, timer))
                            _p4CommandMarkClearTimer = null;
                    }

                    timer?.Dispose();
                }
            }, null, Math.Max(0, durationMs), Timeout.Infinite);

            ReplaceP4CommandMarkClearTimer(timer);
        }

        private static long NowMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static int Duration(Event @event, int fallback = 5000)
        {
            return int.TryParse(@event["DurationMilliseconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration)
                && duration > 0
                ? duration
                : fallback;
        }

        private static bool TryGetDurationMs(Event @event, out int durationMs)
        {
            durationMs = 0;

            var raw = @event["DurationMilliseconds"];
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
                return false;

            if (duration <= 0.0 || duration > int.MaxValue)
                return false;

            durationMs = (int)Math.Round(duration);
            return durationMs > 0;
        }

        private static bool TryParseObjectId(string text, out uint id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.Substring(2) : text;
            return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);
        }

        private static bool TryParseUInt(string text, out uint value)
        {
            value = 0;
            return !string.IsNullOrWhiteSpace(text)
                && uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetActionId(Event @event, out uint actionId)
        {
            return TryParseUInt(@event["ActionId"], out actionId);
        }

        private static bool TryGetStatusId(Event @event, out uint statusId)
        {
            var raw = @event["StatusID"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["StatusId"];

            return TryParseUInt(raw, out statusId);
        }

        private static bool TryGetSourceId(Event @event, out uint sourceId)
        {
            var raw = @event["SourceId"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["SourceID"];

            if (!TryParseObjectId(raw, out sourceId))
                sourceId = (uint)@event.SourceId;

            return sourceId != 0 && sourceId != InvalidObjectId;
        }

        private static bool TryGetTargetId(Event @event, out uint targetId)
        {
            var raw = @event["TargetId"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["TargetID"];

            if (!TryParseObjectId(raw, out targetId))
                targetId = (uint)@event.TargetId;

            return targetId != 0 && targetId != InvalidObjectId;
        }

        private static bool TryGetTargetDataId(Event @event, ScriptAccessory accessory, out uint dataId)
        {
            dataId = 0;

            var raw = @event["TargetDataId"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["TargetDataID"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["DataId"];
            if (string.IsNullOrWhiteSpace(raw))
                raw = @event["DataID"];

            if (TryParseUInt(raw, out dataId) && dataId != 0)
                return true;

            if (!TryGetTargetId(@event, out var targetId))
                return false;

            var obj = accessory.Data.Objects.SearchById(targetId);
            if (obj == null) return false;

            dataId = obj.DataId;
            return dataId != 0;
        }

        private static Vector3 ResolveEventTargetPosition(Event @event, ScriptAccessory accessory)
        {
            var position = @event.TargetPosition;
            if (IsLikelyArenaPosition(position))
                return position;

            if (TryGetTargetId(@event, out var targetId))
            {
                var obj = accessory.Data.Objects.SearchById(targetId);
                if (obj != null)
                    return obj.Position;
            }

            return position;
        }

        private static uint GetMyId(ScriptAccessory accessory)
        {
            var myObject = accessory.Data.MyObject;
            if (myObject != null)
            {
                var myObjectId = (uint)myObject.EntityId;
                if (myObjectId != 0 && myObjectId != InvalidObjectId)
                    return myObjectId;
            }

            return accessory.Data.Me;
        }

        private static int GetPlayerIndex(ScriptAccessory accessory, uint objectId)
        {
            for (var i = 0; i < accessory.Data.PartyList.Count; i++)
            {
                if (accessory.Data.PartyList[i] == objectId)
                    return i;
            }

            return -1;
        }

        private static int GetMyIndex(ScriptAccessory accessory)
        {
            return GetPlayerIndex(accessory, GetMyId(accessory));
        }

        private static string FormatObjectId(uint objectId)
        {
            return objectId == 0 ? "-" : objectId.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static string FormatPosition(Vector3 position)
        {
            return $"({position.X:F2},{position.Y:F2},{position.Z:F2})";
        }

        private static string PartyPriorityLabel(int partyIndex)
        {
            if (partyIndex < 0)
                return "-";

            return partyIndex < PartyPriorityLabels.Length
                ? PartyPriorityLabels[partyIndex]
                : $"P{partyIndex + 1}";
        }

        private static bool IsLikelyArenaPosition(Vector3 position)
        {
            return position.X > 50.0f && position.X < 150.0f
                && position.Z > 50.0f && position.Z < 150.0f;
        }

        private static bool TryNormalizeFromCenter(Vector3 position, out Vector3 direction)
        {
            var delta = new Vector3(position.X - P4ArenaCenter.X, 0.0f, position.Z - P4ArenaCenter.Z);
            var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            if (length < 0.001f)
            {
                direction = DefaultNorth;
                return false;
            }

            direction = new Vector3(delta.X / length, 0.0f, delta.Z / length);
            return true;
        }

        private static Vector3 RightVectorFromNewTwelve(Vector3 newTwelveDirection)
        {
            return new Vector3(-newTwelveDirection.Z, 0.0f, newTwelveDirection.X);
        }

        private static P4HalfSide SideOfPosition(Vector3 position, Vector3 rightVector)
        {
            var delta = new Vector3(position.X - P4ArenaCenter.X, 0.0f, position.Z - P4ArenaCenter.Z);
            var dot = delta.X * rightVector.X + delta.Z * rightVector.Z;
            return dot >= 0.0f ? P4HalfSide.Right : P4HalfSide.Left;
        }

        private static P4HalfSide OppositeSide(P4HalfSide side)
        {
            if (side == P4HalfSide.Left) return P4HalfSide.Right;
            if (side == P4HalfSide.Right) return P4HalfSide.Left;
            return P4HalfSide.Unknown;
        }

        private static P4HalfColor OppositeColor(P4HalfColor color)
        {
            if (color == P4HalfColor.Blue) return P4HalfColor.Purple;
            if (color == P4HalfColor.Purple) return P4HalfColor.Blue;
            return P4HalfColor.Unknown;
        }

        private static bool TryGetP4TrackedStatusIndex(uint statusId, out int index)
        {
            if (statusId >= 5541 && statusId <= 5548)
            {
                index = (int)(statusId - 5541);
                return true;
            }

            if (statusId == 454)
            {
                index = 8;
                return true;
            }

            if (statusId == 1382)
            {
                index = 9;
                return true;
            }

            index = -1;
            return false;
        }

        private static bool TryGetP4StateValueFromParam(int param, out int value)
        {
            if (param == 1122 || param == 1120)
            {
                value = P4StateTrue;
                return true;
            }

            if (param == 1121 || param == 1119)
            {
                value = P4StateFalse;
                return true;
            }

            value = P4StateUnknown;
            return false;
        }

        #region P4 Hotpot Helpers

        private static bool TryGetP4DirectTrueStatus(uint statusId, out uint trueStatusId)
        {
            switch (statusId)
            {
                case 4887:
                    trueStatusId = 5542;
                    return true;
                case 4888:
                    trueStatusId = 5541;
                    return true;
                case 5464:
                    trueStatusId = 454;
                    return true;
                default:
                    trueStatusId = 0;
                    return false;
            }
        }

        private bool TryGetActiveP4StateValueLocked(long now, out int value)
        {
            return TryGetActiveP4StateValueLocked(now, out value, out _, out _, out _);
        }

        private bool TryGetActiveP4StateValueLocked(long now, out int value, out string windowName, out int param, out int remainingMs)
        {
            value = P4StateUnknown;
            windowName = "-";
            param = 0;
            remainingMs = 0;

            var hasX = _p4XParam != 0 && now <= _p4XExpiresAt;
            var hasC = _p4CParam != 0 && now <= _p4CExpiresAt;
            if (!hasX && !hasC)
                return false;

            if (hasX && (!hasC || _p4XUpdatedAt >= _p4CUpdatedAt))
            {
                windowName = "X";
                param = _p4XParam;
                remainingMs = RemainingMs(_p4XExpiresAt, now);
            }
            else
            {
                windowName = "C";
                param = _p4CParam;
                remainingMs = RemainingMs(_p4CExpiresAt, now);
            }

            return TryGetP4StateValueFromParam(param, out value);
        }

        private static Vector3 ExtendGame(Vector3 position, float gameRotation, float length)
        {
            return new Vector3(
                position.X + MathF.Sin(gameRotation) * length,
                position.Y,
                position.Z + MathF.Cos(gameRotation) * length);
        }

        private static Vector3 ExtendDir(Vector3 position, float directionRadians, float length)
        {
            return new Vector3(
                position.X + MathF.Sin(directionRadians) * length,
                position.Y,
                position.Z - MathF.Cos(directionRadians) * length);
        }

        private static float Norm2Pi(float radians)
        {
            var value = radians % (2.0f * MathF.PI);
            return value < 0 ? value + 2.0f * MathF.PI : value;
        }

        private static float GameToDir(float gameRotation)
        {
            return Norm2Pi(MathF.PI - gameRotation);
        }

        private static float DirToGame(float directionRadians)
        {
            return MathF.PI - directionRadians;
        }

        private static int Dir8(Vector3 position, Vector3 center)
        {
            var direction = Math.Round(4.0 - 4.0 * Math.Atan2(position.X - center.X, position.Z - center.Z) / Math.PI) % 8.0;
            return direction < 0 ? (int)direction + 8 : (int)direction;
        }

        private static bool TryResolveExclusiveP4Truth(int stateA, uint statusA, int stateB, uint statusB, out uint trueStatus)
        {
            if (stateA == P4StateTrue)
            {
                trueStatus = statusA;
                return true;
            }

            if (stateB == P4StateTrue)
            {
                trueStatus = statusB;
                return true;
            }

            if (stateA == P4StateFalse)
            {
                trueStatus = statusB;
                return true;
            }

            if (stateB == P4StateFalse)
            {
                trueStatus = statusA;
                return true;
            }

            trueStatus = 0;
            return false;
        }

        private static bool TryResolveP4PlayerColor(int[,] states, int partyIndex, out P4HalfColor color, out uint trueShapeStatus, out uint trueColorStatus)
        {
            color = P4HalfColor.Unknown;
            trueShapeStatus = 0;
            trueColorStatus = 0;

            if (partyIndex < 0 || partyIndex >= 8)
                return false;

            if (!TryResolveExclusiveP4Truth(states[partyIndex, 0], 5541, states[partyIndex, 1], 5542, out trueShapeStatus))
                return false;

            if (!TryResolveExclusiveP4Truth(states[partyIndex, 8], 454, states[partyIndex, 9], 1382, out trueColorStatus))
                return false;

            if ((trueShapeStatus == 5541 && trueColorStatus == 454)
                || (trueShapeStatus == 5542 && trueColorStatus == 1382))
            {
                color = P4HalfColor.Blue;
                return true;
            }

            if ((trueShapeStatus == 5541 && trueColorStatus == 1382)
                || (trueShapeStatus == 5542 && trueColorStatus == 454))
            {
                color = P4HalfColor.Purple;
                return true;
            }

            return false;
        }

        private static P4HalfSide SideForColor(P4HalfColor color, P4HalfColor leftColor, P4HalfColor rightColor)
        {
            if (color == leftColor) return P4HalfSide.Left;
            if (color == rightColor) return P4HalfSide.Right;
            return P4HalfSide.Unknown;
        }

        #endregion

        private static string FormatP4Side(P4HalfSide side)
        {
            switch (side)
            {
                case P4HalfSide.Left: return "L";
                case P4HalfSide.Right: return "R";
                default: return "-";
            }
        }

        private static string FormatP4Color(P4HalfColor color)
        {
            switch (color)
            {
                case P4HalfColor.Blue: return "蓝";
                case P4HalfColor.Purple: return "紫";
                default: return "-";
            }
        }

        private static string FormatP4Mark(MarkType? mark)
        {
            return mark.HasValue ? mark.Value.ToString() : "-";
        }

        private static string FormatP4StatusStates(int[,] states, int partyIndex)
        {
            return string.Join(" ", P4TrackedStatusIds.Select((statusId, statusIndex) => $"{statusId}={states[partyIndex, statusIndex]}"));
        }

        private static int RemainingMs(long expiresAt, long now)
        {
            if (expiresAt <= now)
                return 0;

            var remaining = expiresAt - now;
            return remaining > int.MaxValue ? int.MaxValue : (int)remaining;
        }

        private static string FormatRemainingMs(int remainingMs)
        {
            return remainingMs > 0
                ? $"{remainingMs / 1000.0:F1}s"
                : "-";
        }

        private static string FormatP4StatusStatesWithRemaining(int[,] states, long[,] expiresAt, int partyIndex, long now)
        {
            return string.Join(
                " ",
                P4TrackedStatusIds.Select((statusId, statusIndex) =>
                    $"{statusId}={states[partyIndex, statusIndex]}({FormatRemainingMs(RemainingMs(expiresAt[partyIndex, statusIndex], now))})"));
        }

        private static bool IsP4ChainDebugStatus(uint statusId)
        {
            return statusId >= 5543 && statusId <= 5548;
        }

        private static bool IsP4LaterXStatus(uint statusId)
        {
            return statusId == 5543 || statusId == 5544 || statusId == 5545 || statusId == 5546;
        }

        private static bool IsP4LaterCStatus(uint statusId)
        {
            return statusId == 5547 || statusId == 5548;
        }

        private static bool TryGetP4BuffCategory(uint statusId, out P4BuffCategory category)
        {
            switch (statusId)
            {
                case 5544:
                case 5545:
                case 5546:
                    category = P4BuffCategory.ThunderShare;
                    return true;
                case 5543:
                    category = P4BuffCategory.Petrify;
                    return true;
                case 5547:
                case 5548:
                    category = P4BuffCategory.Element;
                    return true;
                default:
                    category = default;
                    return false;
            }
        }

        private static string P4BuffName(uint statusId)
        {
            switch (statusId)
            {
                case 5547: return "Fire";
                case 5548: return "Water";
                case 5545: return "WaterShare";
                case 5544: return "Thunder";
                case 5543: return "Petrify";
                case 5546: return "Accel";
                default: return statusId.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static bool TryResolveP4LaterStateFromWindow(uint statusId, int xParam, long xExpiresAt, int cParam, long cExpiresAt, long now, out int state, out string windowName, out int usedParam, out int remainingMs)
        {
            state = P4StateUnknown;
            windowName = "-";
            usedParam = 0;
            remainingMs = 0;

            if (IsP4LaterXStatus(statusId))
            {
                if (xParam == 0 || now > xExpiresAt)
                    return false;

                windowName = "X";
                usedParam = xParam;
                remainingMs = RemainingMs(xExpiresAt, now);
                return TryGetP4StateValueFromParam(xParam, out state);
            }

            if (IsP4LaterCStatus(statusId))
            {
                if (cParam == 0 || now > cExpiresAt)
                    return false;

                windowName = "C";
                usedParam = cParam;
                remainingMs = RemainingMs(cExpiresAt, now);
                return TryGetP4StateValueFromParam(cParam, out state);
            }

            return false;
        }

        private static string FormatP4StatusStateWithRemaining(int[,] states, long[,] expiresAt, int partyIndex, uint statusId, long now)
        {
            if (partyIndex < 0
                || partyIndex >= 8
                || !TryGetP4TrackedStatusIndex(statusId, out var statusIndex))
                return $"{statusId}=0(-)";

            return $"{statusId}={states[partyIndex, statusIndex]}({FormatRemainingMs(RemainingMs(expiresAt[partyIndex, statusIndex], now))})";
        }

        private static string FormatP4StatusesWithRemaining(int[,] states, long[,] expiresAt, int partyIndex, long now, params uint[] statusIds)
        {
            return string.Join(" ", statusIds.Select(statusId => FormatP4StatusStateWithRemaining(states, expiresAt, partyIndex, statusId, now)));
        }

        private static string FormatP4WindowDebug(string name, int param, int remainingMs, bool active)
        {
            if (param == 0)
                return $"{name}-";

            return $"{name}{param}({(active ? FormatRemainingMs(remainingMs) : "-")})";
        }

        private static string FormatP4UsedWindowDebug(string name, int param, int remainingMs)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "-")
                return "-";

            if (name == "direct")
                return "direct";

            return $"{name}{param}({FormatRemainingMs(remainingMs)})";
        }

        private static bool IsTnPartyIndex(int partyIndex)
        {
            return partyIndex >= 0 && partyIndex <= 3;
        }

        private static bool IsDpsPartyIndex(int partyIndex)
        {
            return partyIndex >= 4 && partyIndex <= 7;
        }

        private static bool TryGetP4ActiveSoonState(
            int[,] states,
            long[,] expiresAt,
            int partyIndex,
            uint statusId,
            long now,
            out int state,
            out int remainingMs)
        {
            state = P4StateUnknown;
            remainingMs = 0;

            if (partyIndex < 0 || partyIndex >= 8)
                return false;

            if (!TryGetP4TrackedStatusIndex(statusId, out var statusIndex))
                return false;

            state = states[partyIndex, statusIndex];
            remainingMs = RemainingMs(expiresAt[partyIndex, statusIndex], now);
            return state != P4StateUnknown && remainingMs > 0 && remainingMs < 10000;
        }

        private static bool HasP4StatusStateSoon(
            int[,] states,
            long[,] expiresAt,
            int partyIndex,
            uint statusId,
            int expectedState,
            long now,
            out int remainingMs)
        {
            return TryGetP4ActiveSoonState(states, expiresAt, partyIndex, statusId, now, out var state, out remainingMs)
                && state == expectedState;
        }

        private static void NormalizeP4ExclusivePairByTruth(int[,] states, long[,] expiresAt, int partyIndex, int statusIndexA, int statusIndexB, long now)
        {
            if (partyIndex < 0 || partyIndex >= 8)
                return;

            var stateA = RemainingMs(expiresAt[partyIndex, statusIndexA], now) > 0
                ? states[partyIndex, statusIndexA]
                : P4StateUnknown;
            var stateB = RemainingMs(expiresAt[partyIndex, statusIndexB], now) > 0
                ? states[partyIndex, statusIndexB]
                : P4StateUnknown;

            if (stateA == P4StateTrue || stateB == P4StateTrue)
                return;

            if (stateA == P4StateFalse)
            {
                var oldExpiresAt = expiresAt[partyIndex, statusIndexA];
                states[partyIndex, statusIndexA] = P4StateUnknown;
                expiresAt[partyIndex, statusIndexA] = 0;
                states[partyIndex, statusIndexB] = P4StateTrue;
                if (expiresAt[partyIndex, statusIndexB] <= 0)
                    expiresAt[partyIndex, statusIndexB] = oldExpiresAt;
                return;
            }

            if (stateB == P4StateFalse)
            {
                var oldExpiresAt = expiresAt[partyIndex, statusIndexB];
                states[partyIndex, statusIndexB] = P4StateUnknown;
                expiresAt[partyIndex, statusIndexB] = 0;
                states[partyIndex, statusIndexA] = P4StateTrue;
                if (expiresAt[partyIndex, statusIndexA] <= 0)
                    expiresAt[partyIndex, statusIndexA] = oldExpiresAt;
            }
        }

        private static P4ClockDirection ResolveP4MoveClock(int[,] states, long[,] expiresAt, int partyIndex, long now)
        {
            if (!IsTnPartyIndex(partyIndex) && !IsDpsPartyIndex(partyIndex))
                return P4ClockDirection.Unknown;

            var hasTrue5544 = HasP4StatusStateSoon(states, expiresAt, partyIndex, 5544, P4StateTrue, now, out _);
            if (hasTrue5544)
                return IsTnPartyIndex(partyIndex) ? P4ClockDirection.Three : P4ClockDirection.Nine;

            return IsTnPartyIndex(partyIndex) ? P4ClockDirection.Twelve : P4ClockDirection.Six;
        }

        private static Vector3 P4ClockTarget(Vector3 newTwelveDirection, P4ClockDirection clock)
        {
            var rightVector = RightVectorFromNewTwelve(newTwelveDirection);
            switch (clock)
            {
                case P4ClockDirection.Twelve:
                    return P4ArenaCenter + newTwelveDirection * P4ChainGuideOffset;
                case P4ClockDirection.Three:
                    return P4ArenaCenter + rightVector * P4ChainGuideOffset;
                case P4ClockDirection.Six:
                    return P4ArenaCenter - newTwelveDirection * P4ChainGuideOffset;
                case P4ClockDirection.Nine:
                    return P4ArenaCenter - rightVector * P4ChainGuideOffset;
                default:
                    return P4ArenaCenter;
            }
        }

        private static string FormatP4Clock(P4ClockDirection clock)
        {
            switch (clock)
            {
                case P4ClockDirection.Twelve: return "12";
                case P4ClockDirection.Three: return "3";
                case P4ClockDirection.Six: return "6";
                case P4ClockDirection.Nine: return "9";
                default: return "-";
            }
        }

        private static string FormatP4BuffGroupKind(P4BuffGroupKind kind)
        {
            switch (kind)
            {
                case P4BuffGroupKind.A: return "A";
                case P4BuffGroupKind.B: return "B";
                case P4BuffGroupKind.C: return "C";
                case P4BuffGroupKind.D: return "D";
                case P4BuffGroupKind.E: return "E";
                case P4BuffGroupKind.F: return "F";
                default: return "-";
            }
        }

        private static float ClockToRadians(float clock)
        {
            return clock / 6.0f * MathF.PI;
        }

        private static Vector3 P4ClockPoint(Vector3 newTwelveDirection, float clock, float distance)
        {
            var rightVector = RightVectorFromNewTwelve(newTwelveDirection);
            var radians = ClockToRadians(clock);
            var direction = newTwelveDirection * MathF.Cos(radians) + rightVector * MathF.Sin(radians);
            return P4ArenaCenter + direction * distance;
        }

        private static P4ClockDirection RoleDefaultClock(bool isThunder, int partyIndex)
        {
            if (IsTnPartyIndex(partyIndex))
                return isThunder ? P4ClockDirection.Three : P4ClockDirection.Twelve;

            if (IsDpsPartyIndex(partyIndex))
                return isThunder ? P4ClockDirection.Nine : P4ClockDirection.Six;

            return P4ClockDirection.Unknown;
        }

        private static float ClockDirectionToClock(P4ClockDirection clock)
        {
            switch (clock)
            {
                case P4ClockDirection.Twelve: return 0.0f;
                case P4ClockDirection.Three: return 3.0f;
                case P4ClockDirection.Six: return 6.0f;
                case P4ClockDirection.Nine: return 9.0f;
                default: return 0.0f;
            }
        }

        private static MarkType?[] BuildP4ChainMoveMarks(IReadOnlyList<P4ClockDirection> clocks)
        {
            var marks = new MarkType?[8];
            var markedThree = false;
            var markedNine = false;

            for (var i = 0; i < marks.Length && i < clocks.Count; i++)
            {
                if (clocks[i] == P4ClockDirection.Three && !markedThree)
                {
                    marks[i] = MarkType.Attack1;
                    markedThree = true;
                }
                else if (clocks[i] == P4ClockDirection.Nine && !markedNine)
                {
                    marks[i] = MarkType.Attack2;
                    markedNine = true;
                }
            }

            return marks;
        }

        private static MarkType?[] BuildP4PetrifyMarks(int[,] states, long[,] expiresAt, long now)
        {
            var marks = new MarkType?[8];
            var trueCount = 0;
            var falseCount = 0;

            for (var i = 0; i < marks.Length; i++)
            {
                if (!TryGetP4ActiveSoonState(states, expiresAt, i, 5543, now, out var state, out _))
                    continue;

                if (state == P4StateTrue && trueCount < P4PetrifyTrueMarks.Length)
                {
                    marks[i] = P4PetrifyTrueMarks[trueCount];
                    trueCount++;
                }
                else if (state == P4StateFalse && falseCount < P4PetrifyFalseMarks.Length)
                {
                    marks[i] = P4PetrifyFalseMarks[falseCount];
                    falseCount++;
                }
            }

            return marks;
        }

        private bool ApplyP4CommandMarksNoTimer(ScriptAccessory accessory, IReadOnlyList<MarkType?> marks)
        {
            if (!EnableP4LaterMarks) return false;

            ClearP4CommandMarksNow(accessory);
            var token = Interlocked.Increment(ref _p4LaterMarkApplyToken);
            var generation = _generation;
            var markSnapshot = marks == null ? Array.Empty<MarkType?>() : marks.Take(8).ToArray();

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(P4LaterMarkApplyDelayMs);
                    if (!EnableP4LaterMarks
                        || generation != _generation
                        || _phase != Phase.P4
                        || token != Volatile.Read(ref _p4LaterMarkApplyToken))
                        return;

                    var totalCount = Math.Min(markSnapshot.Length, accessory.Data.PartyList.Count);
                    for (var i = 0; i < totalCount; i++)
                    {
                        if (markSnapshot[i].HasValue)
                            CommandMarkPartyMember(accessory, i, markSnapshot[i].Value);
                    }
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"P4 later mark delayed apply fail {ex.Message}");
                }
            });

            return true;
        }

        private static string P4MoveCallText(int state)
        {
            if (state == P4StateTrue) return "停停停";
            if (state == P4StateFalse) return "动动动";
            return null;
        }

        private static string P4PetrifyCallText(int state, bool isSelf)
        {
            if (state == P4StateTrue) return isSelf ? "你是背对石化" : "背对石化";
            if (state == P4StateFalse) return isSelf ? "你是正对石化" : "正对石化";
            return null;
        }

        private static bool TryResolveP4ElementCall(
            int[,] states,
            long[,] expiresAt,
            long now,
            out P4ElementCall call,
            out int partyIndex,
            out uint statusId,
            out int state,
            out int remainingMs)
        {
            call = P4ElementCall.Unknown;
            partyIndex = -1;
            statusId = 0;
            state = P4StateUnknown;
            remainingMs = 0;

            for (var i = 0; i < 8; i++)
            {
                if (TryGetP4ActiveSoonState(states, expiresAt, i, 5547, now, out state, out remainingMs))
                {
                    call = state == P4StateTrue ? P4ElementCall.FireSteel : P4ElementCall.WaterMoon;
                    partyIndex = i;
                    statusId = 5547;
                    return call != P4ElementCall.Unknown;
                }

                if (TryGetP4ActiveSoonState(states, expiresAt, i, 5548, now, out state, out remainingMs))
                {
                    call = state == P4StateFalse ? P4ElementCall.FireSteel : P4ElementCall.WaterMoon;
                    partyIndex = i;
                    statusId = 5548;
                    return call != P4ElementCall.Unknown;
                }
            }

            return false;
        }

        private static string P4ElementCallText(P4ElementCall call)
        {
            switch (call)
            {
                case P4ElementCall.FireSteel: return "火钢铁 中间集合稍后散开";
                case P4ElementCall.WaterMoon: return "水月环 中间集合稍后不动";
                default: return null;
            }
        }

        private static string FormatP4ElementCall(P4ElementCall call)
        {
            switch (call)
            {
                case P4ElementCall.FireSteel: return "火钢铁";
                case P4ElementCall.WaterMoon: return "水月环";
                default: return "-";
            }
        }

        private bool TryStartP4Chain(out int round, out int stepGeneration)
        {
            lock (_p4Lock)
            {
                if (_p4ChainStep != P4ChainStep.None || _p4ChainRound > 0)
                {
                    round = _p4ChainRound;
                    stepGeneration = _p4ChainStepGeneration;
                    return false;
                }

                _p4ChainRound = 1;
                _p4ChainStep = P4ChainStep.Move;
                _p4ChainStepGeneration++;
                round = _p4ChainRound;
                stepGeneration = _p4ChainStepGeneration;
                return true;
            }
        }

        private bool TryAdvanceP4ChainStep(P4ChainStep requiredStep, P4ChainStep nextStep, out int round, out int stepGeneration)
        {
            lock (_p4Lock)
            {
                if (_p4ChainRound <= 0 || _p4ChainStep != requiredStep)
                {
                    round = _p4ChainRound;
                    stepGeneration = _p4ChainStepGeneration;
                    return false;
                }

                _p4ChainStep = nextStep;
                _p4ChainStepGeneration++;
                round = _p4ChainRound;
                stepGeneration = _p4ChainStepGeneration;
                return true;
            }
        }

        private bool TryAdvanceP4ChainRound(out int round, out int stepGeneration)
        {
            lock (_p4Lock)
            {
                if (_p4ChainRound <= 0 || _p4ChainStep != P4ChainStep.Element)
                {
                    round = _p4ChainRound;
                    stepGeneration = _p4ChainStepGeneration;
                    return false;
                }

                _p4ChainRound++;
                _p4ChainStep = P4ChainStep.Move;
                _p4ChainStepGeneration++;
                round = _p4ChainRound;
                stepGeneration = _p4ChainStepGeneration;
                return true;
            }
        }

        private void ScheduleP4DelayedStep(
            ScriptAccessory accessory,
            int generation,
            int stepGeneration,
            int round,
            P4ChainStep step,
            int delayMs,
            Action action)
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(Math.Max(0, delayMs));

                    if (generation != _generation || _phase != Phase.P4)
                        return;

                    lock (_p4Lock)
                    {
                        if (_p4ChainStepGeneration != stepGeneration
                            || _p4ChainRound != round
                            || _p4ChainStep != step)
                            return;
                    }

                    action();
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"P4链式阶段{step}延迟执行失败：{ex.Message}");
                }
            });
        }

        private void ScheduleP4SelfTtsAtRemaining3(ScriptAccessory accessory, int generation, int round, int partyIndex, int expectedState, int remainingMs, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || remainingMs <= 0)
                return;

            var delayMs = Math.Max(0, remainingMs - 3000);
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs);

                    if (generation != _generation || _phase != Phase.P4)
                        return;

                    lock (_p4Lock)
                    {
                        if (_p4ChainRound != round || _p4ChainStep == P4ChainStep.None)
                            return;

                        if (partyIndex < 0
                            || partyIndex >= 8
                            || !TryGetP4TrackedStatusIndex(5546, out var statusIndex)
                            || _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] != expectedState)
                            return;

                        var currentRemaining = RemainingMs(_p4StatusExpiresAtByPartyAndStatus[partyIndex, statusIndex], NowMs());
                        if (currentRemaining <= 0 || currentRemaining > 4500)
                            return;
                    }

                    QTTS(text);
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"P4 5546 TTS延迟执行失败：{ex.Message}");
                }
            });
        }

        private void SendP4PartyCallouts(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation, int round)
        {
            if (PlanMessageMode != P4PlanMessageMode.Party || messages == null || messages.Count == 0)
                return;

            Task.Run(async () =>
            {
                try
                {
                    for (var i = 0; i < messages.Count; i++)
                    {
                        if (generation != _generation || _phase != Phase.P4)
                            return;

                        lock (_p4Lock)
                        {
                            if (_p4ChainRound != round || _p4ChainStep == P4ChainStep.None)
                                return;
                        }

                        accessory.Method.SendChat($"/p {messages[i]}");
                        if (i + 1 < messages.Count)
                            await Task.Delay(150);
                    }
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"P4小队频道提示发送失败：{ex.Message}");
                }
            });
        }

        private void ScheduleP4LaterGroupsIfReady(ScriptAccessory accessory)
        {
            var generation = _generation;

            lock (_p4Lock)
            {
                if (_p4BuffGroupsScheduled || _p4BuffGroupsScheduleQueued)
                    return;

                if (_p4GroupSnapshotAt <= 0 || (_p4GroupSnapshotParam != 1121 && _p4GroupSnapshotParam != 1122))
                    return;

                if (BuildP4BuffGroupsLocked().Count < P4MaxScheduledGroupCount)
                    return;

                _p4BuffGroupsScheduleQueued = true;
            }

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500);
                    FinalizeAndScheduleP4LaterGroups(accessory, generation);
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"P4GROUP queue fail {ex.Message}");
                }
            });
        }

        private void FinalizeAndScheduleP4LaterGroups(ScriptAccessory accessory, int generation)
        {
            var groups = new List<P4BuffGroup>();
            lock (_p4Lock)
            {
                if (generation != _generation || _phase != Phase.P4)
                    return;

                if (_p4BuffGroupsScheduled)
                    return;

                groups = BuildP4BuffGroupsLocked();
                if (groups.Count < P4MaxScheduledGroupCount)
                {
                    _p4BuffGroupsScheduleQueued = false;
                    return;
                }

                _p4BuffGroups.Clear();
                _p4BuffGroups.AddRange(groups);
                _p4BuffGroupsScheduled = true;
                _p4BuffGroupsScheduleQueued = false;
            }

            DebugEcho(accessory, $"P4GROUP ready {string.Join(" ", groups.Select(FormatP4GroupReadyDebug))}");
            SendP4GroupPlanSummaries(accessory, groups, generation);
            foreach (var group in groups)
                ScheduleP4BuffGroup(accessory, generation, group);
        }

        private List<P4BuffGroup> BuildP4BuffGroupsLocked()
        {
            if (_p4GroupSnapshotAt <= 0)
                return new List<P4BuffGroup>();

            var snapshotAt = _p4GroupSnapshotAt;
            var sorted = _p4BuffRecords
                .Where(r => r.ExpiresAt > snapshotAt && TryGetP4BuffCategory(r.StatusId, out _))
                .Select(r => new
                {
                    Record = r,
                    SnapshotRemainingMs = (int)Math.Max(1L, r.ExpiresAt - snapshotAt),
                })
                .OrderBy(x => x.SnapshotRemainingMs)
                .ThenBy(x => x.Record.StatusId)
                .ThenBy(x => x.Record.PartyIndex)
                .ToList();

            var groups = new List<P4BuffGroup>();
            foreach (var item in sorted)
            {
                var record = item.Record;
                record.SnapshotRemainingMs = item.SnapshotRemainingMs;

                var group = groups.FirstOrDefault(g =>
                    Math.Abs(g.SnapshotRemainingMs - item.SnapshotRemainingMs) <= P4BuffGroupToleranceMs);
                if (group == null)
                {
                    group = new P4BuffGroup
                    {
                        Category = TryGetP4BuffCategory(record.StatusId, out var category) ? category : P4BuffCategory.Mixed,
                        DurationMs = record.DurationMs,
                        SnapshotRemainingMs = item.SnapshotRemainingMs,
                        ExpiresAt = snapshotAt + item.SnapshotRemainingMs,
                    };
                    groups.Add(group);
                }

                group.Records.Add(record);
                group.DurationMs = (int)Math.Round(group.Records.Average(r => r.DurationMs));
                group.SnapshotRemainingMs = (int)Math.Round(group.Records.Average(r => r.SnapshotRemainingMs));
                group.ExpiresAt = snapshotAt + group.SnapshotRemainingMs;
                group.Category = ResolveP4BuffGroupCategory(group.Records);
            }

            groups = groups
                .OrderBy(g => g.SnapshotRemainingMs)
                .Take(P4MaxScheduledGroupCount)
                .ToList();
            for (var i = 0; i < groups.Count; i++)
                groups[i].Kind = (P4BuffGroupKind)(i + 1);

            return groups;
        }

        private static P4BuffCategory ResolveP4BuffGroupCategory(IReadOnlyList<P4BuffRecord> records)
        {
            var categories = records
                .Select(r => TryGetP4BuffCategory(r.StatusId, out var category) ? category : P4BuffCategory.Mixed)
                .Distinct()
                .ToList();

            return categories.Count == 1 ? categories[0] : P4BuffCategory.Mixed;
        }

        private static string FormatP4GroupReadyDebug(P4BuffGroup group)
        {
            return $"{FormatP4BuffGroupKind(group.Kind)}:{group.Category}:{group.Records.Count} snap={FormatRemainingMs(group.SnapshotRemainingMs)} now={FormatRemainingMs(RemainingMs(group.ExpiresAt, NowMs()))} ids={FormatP4GroupStatusIds(group)} dur={FormatP4GroupDurations(group)}";
        }

        private static string FormatP4GroupStatusIds(P4BuffGroup group)
        {
            return string.Join(
                "/",
                group.Records
                    .Select(r => r.StatusId)
                    .Distinct()
                    .OrderBy(id => id)
                    .Select(P4BuffName));
        }

        private static string FormatP4GroupDurations(P4BuffGroup group)
        {
            var durations = group.Records
                .Select(r => r.DurationMs)
                .Where(ms => ms > 0)
                .Distinct()
                .OrderBy(ms => ms)
                .Select(FormatRemainingMs)
                .ToList();

            return durations.Count > 0 ? string.Join("/", durations) : "-";
        }

        private void ScheduleP4BuffGroup(ScriptAccessory accessory, int generation, P4BuffGroup group)
        {
            var triggerRemainingMs = P4GroupTriggerRemainingMs(group.Kind);
            if (triggerRemainingMs <= 0)
                return;

            var delayMs = (int)Math.Max(0L, group.ExpiresAt - NowMs() - triggerRemainingMs);
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs);
                    if (generation != _generation || _phase != Phase.P4)
                        return;

                    ExecuteP4BuffGroup(accessory, generation, group.Kind);
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"P4GROUP {FormatP4BuffGroupKind(group.Kind)} delayed fail {ex.Message}");
                }
            });
        }

        private static int P4GroupTriggerRemainingMs(P4BuffGroupKind kind)
        {
            switch (kind)
            {
                case P4BuffGroupKind.A: return 6500;
                case P4BuffGroupKind.B: return 4900;
                case P4BuffGroupKind.C: return 6000;
                case P4BuffGroupKind.D: return 8500;
                case P4BuffGroupKind.E: return 5500;
                case P4BuffGroupKind.F: return 4900;
                default: return 0;
            }
        }

        private void ExecuteP4BuffGroup(ScriptAccessory accessory, int generation, P4BuffGroupKind kind)
        {
            P4BuffGroup group = null;
            Vector3 newTwelveDirection;
            lock (_p4Lock)
            {
                group = _p4BuffGroups.FirstOrDefault(g => g.Kind == kind);
                newTwelveDirection = DefaultNorth;
                if (kind == P4BuffGroupKind.F)
                    _p4FGroupStartedAt = NowMs();
            }

            if (group == null)
            {
                DebugEcho(accessory, $"P4GROUP {FormatP4BuffGroupKind(kind)} missing");
                return;
            }

            DebugEcho(accessory, $"P4GROUP {FormatP4BuffGroupKind(kind)} fire records={FormatP4GroupRecords(group)}");

            switch (kind)
            {
                case P4BuffGroupKind.A:
                    ExecuteP4ThunderShareGroup(accessory, generation, group, newTwelveDirection, 8.0f, 4000, "P4_A", false, 0);
                    break;
                case P4BuffGroupKind.B:
                    ExecuteP4PetrifyGroupB(accessory, group, newTwelveDirection);
                    break;
                case P4BuffGroupKind.C:
                    ExecuteP4ElementGroup(accessory, group, newTwelveDirection, P4BuffGroupKind.C, 5500);
                    break;
                case P4BuffGroupKind.D:
                    ExecuteP4GroupD(accessory, generation, group, newTwelveDirection);
                    break;
                case P4BuffGroupKind.E:
                    ExecuteP4PetrifyGroupE(accessory, group, newTwelveDirection);
                    break;
                case P4BuffGroupKind.F:
                    ExecuteP4ElementGroup(accessory, group, newTwelveDirection, P4BuffGroupKind.F, 4500);
                    TryDrawP4FinalSafe(accessory, generation);
                    break;
            }
        }

        private static string FormatP4GroupRecords(P4BuffGroup group)
        {
            return string.Join(
                " ",
                group.Records
                    .OrderBy(r => r.PartyIndex)
                    .ThenBy(r => r.StatusId)
                    .Select(r => $"{PartyPriorityLabel(r.PartyIndex)}:{P4BuffName(r.StatusId)}={r.State}/{FormatRemainingMs(RemainingMs(r.ExpiresAt, NowMs()))}"));
        }

        private bool TryGetP4ThunderState(IReadOnlyList<P4BuffRecord> records, int partyIndex, out bool isThunder)
        {
            isThunder = false;

            var thunder = records.FirstOrDefault(r => r.PartyIndex == partyIndex && r.StatusId == 5544);
            if (thunder != null)
            {
                if (thunder.State == P4StateTrue)
                {
                    isThunder = true;
                    return true;
                }

                if (thunder.State == P4StateFalse)
                {
                    isThunder = false;
                    return true;
                }
            }

            var waterShare = records.FirstOrDefault(r => r.PartyIndex == partyIndex && r.StatusId == 5545);
            if (waterShare != null)
            {
                if (waterShare.State == P4StateTrue)
                {
                    isThunder = false;
                    return true;
                }

                if (waterShare.State == P4StateFalse)
                {
                    isThunder = true;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetP4AccelCall(IReadOnlyList<P4BuffRecord> records, int partyIndex, out string call)
        {
            call = null;
            var accel = records.FirstOrDefault(r => r.PartyIndex == partyIndex && r.StatusId == 5546);
            if (accel == null)
                return false;

            call = P4MoveCallText(accel.State);
            return !string.IsNullOrWhiteSpace(call);
        }

        private void SendP4GroupPlanSummaries(ScriptAccessory accessory, IReadOnlyList<P4BuffGroup> groups, int generation)
        {
            if (groups == null || PlanMessageMode == P4PlanMessageMode.Off)
                return;

            var messages = new List<string>();
            AddP4RoundPlanSummary(
                accessory,
                messages,
                "\u7b2c\u4e00\u8f6e",
                groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.A),
                groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.B),
                groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.C));
            messages.Add("---------------");
            AddP4RoundPlanSummary(
                accessory,
                messages,
                "\u7b2c\u4e8c\u8f6e",
                groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.D),
                groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.E),
                groups.FirstOrDefault(g => g.Kind == P4BuffGroupKind.F));
            SendP4PlanMessages(accessory, messages, generation);
        }

        private void AddP4RoundPlanSummary(
            ScriptAccessory accessory,
            List<string> messages,
            string roundLabel,
            P4BuffGroup thunderGroup,
            P4BuffGroup petrifyGroup,
            P4BuffGroup elementGroup)
        {
            if (messages == null)
                return;

            AddP4ThunderSharePlanSummary(accessory, messages, thunderGroup, roundLabel);
            AddP4PetrifyPlanSummary(accessory, messages, petrifyGroup, roundLabel);
            AddP4ElementPlanSummary(messages, elementGroup, roundLabel);
        }

        private void AddP4ThunderSharePlanSummary(ScriptAccessory accessory, List<string> messages, P4BuffGroup group, string roundLabel)
        {
            if (messages == null || group == null)
                return;

            var accelParts = new List<string>();
            var accelStop = new List<string>();
            var accelMove = new List<string>();
            var thunder = new List<string>();
            var records = group.Records;

            for (var i = 0; i < 8; i++)
            {
                var accel = records.FirstOrDefault(r => r.PartyIndex == i && r.StatusId == 5546);
                if (accel != null)
                {
                    if (accel.State == P4StateTrue)
                        accelStop.Add(PartyMemberJobName(accessory, i));
                    else if (accel.State == P4StateFalse)
                        accelMove.Add(PartyMemberJobName(accessory, i));
                }

                if (TryGetP4ThunderState(records, i, out var isThunder) && isThunder)
                    thunder.Add(PartyMemberJobName(accessory, i));
            }

            if (accelMove.Count > 0)
                accelParts.Add($"{string.Join("\uff0c", accelMove)} \u52a8\u52a8\u52a8");
            if (accelStop.Count > 0)
                accelParts.Add($"{string.Join("\uff0c", accelStop)} \u505c\u505c\u505c");

            if (accelParts.Count > 0)
                messages.Add($"{roundLabel}\uff1a{string.Join("    ", accelParts)}");
            if (thunder.Count > 0)
                messages.Add($"{roundLabel}\uff1a{string.Join(" ", thunder)} \u96f7\u5206\u6563");
        }

        private void AddP4PetrifyPlanSummary(ScriptAccessory accessory, List<string> messages, P4BuffGroup group, string roundLabel)
        {
            if (messages == null || group == null)
                return;

            var faceOut = new List<string>();
            var faceEachOther = new List<string>();
            foreach (var record in group.Records.Where(r => r.StatusId == 5543).OrderBy(r => r.PartyIndex))
            {
                if (record.State == P4StateTrue)
                    faceOut.Add(PartyMemberJobName(accessory, record.PartyIndex));
                else if (record.State == P4StateFalse)
                    faceEachOther.Add(PartyMemberJobName(accessory, record.PartyIndex));
            }

            if (faceOut.Count > 0)
                messages.Add($"{roundLabel}\uff1a{string.Join(" ", faceOut)} \u80cc\u5bf9");
            if (faceEachOther.Count > 0)
                messages.Add($"{roundLabel}\uff1a{string.Join(" ", faceEachOther)} \u6b63\u5bf9");
        }

        private void AddP4ElementPlanSummary(List<string> messages, P4BuffGroup group, string roundLabel)
        {
            if (messages == null || group == null)
                return;

            var call = ResolveP4ElementCallFromRecords(group.Records);
            if (call == P4ElementCall.FireSteel)
                messages.Add($"{roundLabel}\uff1a\u5168\u4f53\u94a2\u94c1");
            else if (call == P4ElementCall.WaterMoon)
                messages.Add($"{roundLabel}\uff1a\u5168\u4f53\u6708\u73af");
        }

        private void SendP4PlanMessages(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation)
        {
            if (messages == null || messages.Count == 0)
                return;

            switch (PlanMessageMode)
            {
                case P4PlanMessageMode.Party:
                    SendP4PartyMessages(accessory, messages, generation);
                    break;
                case P4PlanMessageMode.Echo:
                    SendP4EchoMessages(accessory, messages, generation);
                    break;
            }
        }

        private void SendP4PartyMessages(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation)
        {
            if (messages == null || messages.Count == 0)
                return;

            Task.Run(async () =>
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    if (generation != _generation || _phase != Phase.P4)
                        return;

                    accessory.Method.SendChat($"/p {messages[i]}");
                    if (i + 1 < messages.Count)
                        await Task.Delay(150);
                }
            });
        }

        private void SendP4EchoMessages(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation)
        {
            if (messages == null || messages.Count == 0)
                return;

            Task.Run(async () =>
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    if (generation != _generation || _phase != Phase.P4)
                        return;

                    accessory.Method.SendChat($"/e {messages[i]}");
                    if (i + 1 < messages.Count)
                        await Task.Delay(150);
                }
            });
        }

        private void ExecuteP4ThunderShareGroup(
            ScriptAccessory accessory,
            int generation,
            P4BuffGroup group,
            Vector3 newTwelveDirection,
            float distance,
            int duration,
            string drawName,
            bool sendAccelParty,
            int delay)
        {
            var records = group.Records;
            var targets = new Vector3[8];
            var marks = new MarkType?[8];
            var thunderPlayers = new List<int>();
            var partyMessages = new List<string>();

            for (var i = 0; i < 8; i++)
            {
                var hasThunderState = TryGetP4ThunderState(records, i, out var isThunder);
                if (isThunder)
                {
                    thunderPlayers.Add(i);
                    partyMessages.Add($"{PartyPriorityLabel(i)}{PartyMemberJobName(accessory, i)}\u96f7\u5206\u6563");
                }

                var clock = RoleDefaultClock(isThunder, i);
                targets[i] = P4ClockPoint(newTwelveDirection, ClockDirectionToClock(clock), distance);

                if (TryGetP4AccelCall(records, i, out var accelCall))
                    partyMessages.Add($"{PartyPriorityLabel(i)}{PartyMemberJobName(accessory, i)}{accelCall}");

                DebugEcho(accessory, $"P4{FormatP4BuffGroupKind(group.Kind)} MOVE {PartyPriorityLabel(i)} thunder={(hasThunderState ? isThunder.ToString() : "-")} accel={(TryGetP4AccelCall(records, i, out var c) ? c : "-")} clock={FormatP4Clock(clock)} target={FormatPosition(targets[i])}");
            }

            for (var i = 0; i < thunderPlayers.Count && i < 2; i++)
                marks[thunderPlayers[i]] = i == 0 ? MarkType.Attack1 : MarkType.Attack2;

            if (ApplyP4CommandMarksNoTimer(accessory, marks))
                ScheduleP4CommandMarkClear(accessory, generation, P4LaterMarkApplyDelayMs + 4000);

            if (sendAccelParty)
                SendP4PartyCalloutsSimple(accessory, partyMessages, generation);

            var myIndex = GetMyIndex(accessory);
            if (myIndex >= 0 && myIndex < 8)
            {
                var hasThunderState = TryGetP4ThunderState(records, myIndex, out var isThunder);
                var parts = new List<string> { isThunder ? "钢铁" : "分摊" };
                if (TryGetP4AccelCall(records, myIndex, out var accelCall))
                    parts.Add(accelCall);
                Alert(string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))));

                if (hasThunderState || IsTnPartyIndex(myIndex) || IsDpsPartyIndex(myIndex))
                    DrawP4SelfGuide(accessory, $"{DrawPrefix}_{drawName}_Guide", $"{DrawPrefix}_{drawName}_Target", targets[myIndex], duration, delay);
            }
        }

        private void SendP4PartyCalloutsSimple(ScriptAccessory accessory, IReadOnlyList<string> messages, int generation)
        {
            if (PlanMessageMode != P4PlanMessageMode.Party || messages == null || messages.Count == 0)
                return;

            Task.Run(async () =>
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    if (generation != _generation || _phase != Phase.P4)
                        return;

                    accessory.Method.SendChat($"/p {messages[i]}");
                    if (i + 1 < messages.Count)
                        await Task.Delay(150);
                }
            });
        }

        private void DrawP4SelfGuide(ScriptAccessory accessory, string guideName, string targetName, Vector3 target, int duration, int delay = 0)
        {
            DrawGuide(accessory, guideName, target, duration, delay);
            DrawStaticCircle(accessory, targetName, target, 0.25f, duration, GuideColor.V4);
            if (delay <= 0)
                GreenMoveToPoint(target, accessory, guideName);
        }

        private void ExecuteP4PetrifyGroupB(ScriptAccessory accessory, P4BuffGroup group, Vector3 newTwelveDirection)
        {
            var safeClock = FindSafeNorthSouthClock(newTwelveDirection);
            ExecuteP4PetrifyGroup(accessory, group, newTwelveDirection, safeClock, 4000, "P4_B");
        }

        private void ExecuteP4PetrifyGroupE(ScriptAccessory accessory, P4BuffGroup group, Vector3 newTwelveDirection)
        {
            ExecuteP4PetrifyGroup(accessory, group, newTwelveDirection, 6.0f, 5000, "P4_E");
        }

        private void ExecuteP4PetrifyGroup(ScriptAccessory accessory, P4BuffGroup group, Vector3 newTwelveDirection, float safeClock, int duration, string drawName)
        {
            var petrifies = group.Records
                .Where(r => r.StatusId == 5543)
                .OrderBy(r => r.PartyIndex)
                .ToList();
            var petrifySet = new HashSet<int>(petrifies.Select(r => r.PartyIndex));
            var targets = new Vector3[8];
            var marks = new MarkType?[8];

            for (var i = 0; i < petrifies.Count && i < 2; i++)
            {
                var record = petrifies[i];
                var distance = record.State == P4StateTrue ? 8.0f : record.State == P4StateFalse ? 3.0f : 6.0f;
                targets[record.PartyIndex] = P4ClockPoint(newTwelveDirection, safeClock, distance);
                marks[record.PartyIndex] = record.State == P4StateTrue
                    ? (i == 0 ? MarkType.Stop1 : MarkType.Stop2)
                    : (i == 0 ? MarkType.Bind1 : MarkType.Bind2);
            }

            var others = Enumerable.Range(0, 8)
                .Where(i => !petrifySet.Contains(i))
                .OrderBy(i => i)
                .ToList();
            for (var i = 0; i < others.Count; i++)
            {
                targets[others[i]] = P4ClockPoint(newTwelveDirection, safeClock, 6.0f);
            }

            if (ApplyP4CommandMarksNoTimer(accessory, marks))
                ScheduleP4CommandMarkClear(accessory, _generation, P4LaterMarkApplyDelayMs + 4000);

            var myIndex = GetMyIndex(accessory);
            var myPetrify = petrifies.FirstOrDefault(r => r.PartyIndex == myIndex);
            var oldPetrifyAlertEnabled = false;
            if (myPetrify != null)
                Alert(myPetrify.State == P4StateTrue ? "\u9762\u5411boss" : "\u4e92\u76f8\u9762\u5411");
            if (oldPetrifyAlertEnabled && myPetrify != null)
                Alert(myPetrify.State == P4StateTrue ? "出去背对" : "出去正对");
            else if (oldPetrifyAlertEnabled && petrifies.Count > 0)
                Alert(petrifies[0].State == P4StateTrue ? "背对" : "正对");

            if (myIndex >= 0 && myIndex < 8)
                DrawP4SelfGuide(accessory, $"{DrawPrefix}_{drawName}_Guide", $"{DrawPrefix}_{drawName}_Target", targets[myIndex], duration);

            DebugEcho(accessory, $"P4{FormatP4BuffGroupKind(group.Kind)} PETRI safe={safeClock:F1} petrify={string.Join(",", petrifies.Select(r => $"{PartyPriorityLabel(r.PartyIndex)}:{r.State}/{(r.State == P4StateTrue ? "8m" : r.State == P4StateFalse ? "3m" : "6m")}"))} idle=6m");
        }

        private void ExecuteP4ElementGroup(ScriptAccessory accessory, P4BuffGroup group, Vector3 newTwelveDirection, P4BuffGroupKind kind, int duration)
        {
            var call = ResolveP4ElementCallFromRecords(group.Records);
            if (kind == P4BuffGroupKind.C)
                _p4CElementCall = call;
            if (kind == P4BuffGroupKind.F)
                _p4FElementCall = call;

            var text = call == P4ElementCall.FireSteel ? "场中集合钢铁" : call == P4ElementCall.WaterMoon ? "场中集合月环" : null;
            if (!string.IsNullOrWhiteSpace(text))
                QTTS(text);

            DrawP4SelfGuide(accessory, $"{DrawPrefix}_P4_{FormatP4BuffGroupKind(kind)}_Center_Guide", $"{DrawPrefix}_P4_{FormatP4BuffGroupKind(kind)}_Center_Target", P4ArenaCenter, duration);
            DebugEcho(accessory, $"P4{FormatP4BuffGroupKind(kind)} ELEM result={FormatP4ElementCall(call)} records={FormatP4GroupRecords(group)}");
        }

        private P4ElementCall ResolveP4ElementCallFromRecords(IReadOnlyList<P4BuffRecord> records)
        {
            var record = records
                .Where(r => r.StatusId == 5547 || r.StatusId == 5548)
                .OrderBy(r => r.PartyIndex)
                .ThenBy(r => r.StatusId)
                .FirstOrDefault();
            if (record == null)
                return P4ElementCall.Unknown;

            if (record.StatusId == 5547)
                return record.State == P4StateTrue ? P4ElementCall.FireSteel : P4ElementCall.WaterMoon;

            return record.State == P4StateFalse ? P4ElementCall.FireSteel : P4ElementCall.WaterMoon;
        }

        private void ExecuteP4GroupD(ScriptAccessory accessory, int generation, P4BuffGroup group, Vector3 newTwelveDirection)
        {
            var startedAt = NowMs();
            var firstDistance = _p4CElementCall == P4ElementCall.WaterMoon ? 3.0f : 8.0f;
            ExecuteP4ThunderShareGroup(accessory, generation, group, newTwelveDirection, firstDistance, 5000, "P4_D_First", false, 0);

            Task.Run(async () =>
            {
                await Task.Delay(5000);

                var deadline = NowMs() + 1500;
                while (NowMs() < deadline)
                {
                    if (generation != _generation || _phase != Phase.P4)
                        return;

                    if (CountP4Dangers(P4DangerShape.Fan, startedAt) >= 2)
                        break;

                    await Task.Delay(100);
                }

                if (generation != _generation || _phase != Phase.P4)
                    return;

                DrawP4GroupDSafe(accessory, group, newTwelveDirection, startedAt);
            });
        }

        private void DrawP4GroupDSafe(ScriptAccessory accessory, P4BuffGroup group, Vector3 newTwelveDirection, long minCapturedAt)
        {
            var safeByRoleClock = FindSafeHalfClockByQuadrant(newTwelveDirection, minCapturedAt);
            var myIndex = GetMyIndex(accessory);
            if (myIndex < 0 || myIndex >= 8)
                return;

            TryGetP4ThunderState(group.Records, myIndex, out var isThunder);
            var baseClock = ClockDirectionToClock(RoleDefaultClock(isThunder, myIndex));
            var targetClock = safeByRoleClock.ContainsKey(baseClock) ? safeByRoleClock[baseClock] : baseClock;
            var target = P4ClockPoint(newTwelveDirection, targetClock, 8.0f);
            DrawP4SelfGuide(accessory, $"{DrawPrefix}_P4_D_Safe_Guide", $"{DrawPrefix}_P4_D_Safe_Target", target, 3000);
            DebugEcho(accessory, $"P4D SAFE my={PartyPriorityLabel(myIndex)} base={baseClock:F1} safe={targetClock:F1} target={FormatPosition(target)}");
        }

        private int CountP4Dangers(P4DangerShape shape, long minCapturedAt)
        {
            lock (_p4Lock)
            {
                return _p4DangerRecords.Count(r => r.Shape == shape && r.CapturedAt >= minCapturedAt);
            }
        }

        private List<P4DangerRecord> GetP4RecentDangers(P4DangerShape shape, int count, int lookbackMs, long minCapturedAt = 0)
        {
            var now = NowMs();
            lock (_p4Lock)
            {
                return _p4DangerRecords
                    .Where(r => r.Shape == shape
                        && r.CapturedAt >= minCapturedAt
                        && now - r.CapturedAt <= lookbackMs)
                    .OrderByDescending(r => r.CapturedAt)
                    .Take(count)
                    .OrderBy(r => r.CapturedAt)
                    .ToList();
            }
        }

        private float FindSafeNorthSouthClock(Vector3 newTwelveDirection)
        {
            var dangers = GetP4RecentDangers(P4DangerShape.Rect, 2, P4BRectLookbackMs);
            var candidates = new[] { 0.0f, 6.0f };
            var safe = candidates
                .Where(clock =>
                    IsPointSafeForDangers(P4ClockPoint(newTwelveDirection, clock, 3.0f), dangers)
                    && IsPointSafeForDangers(P4ClockPoint(newTwelveDirection, clock, 6.0f), dangers)
                    && IsPointSafeForDangers(P4ClockPoint(newTwelveDirection, clock, 8.0f), dangers))
                .ToList();

            if (safe.Count <= 0)
            {
                if (_acc != null)
                    DebugEcho(_acc, $"P4B SAFE fallback rects={dangers.Count} safe={string.Join(",", safe.Select(c => c.ToString("F1", CultureInfo.InvariantCulture)))}");

                return 0.0f;
            }

            return safe[0];
        }

        private Dictionary<float, float> FindSafeHalfClockByQuadrant(Vector3 newTwelveDirection, long minCapturedAt)
        {
            var dangers = GetP4RecentDangers(P4DangerShape.Fan, 2, P4DFanLookbackMs, minCapturedAt);
            var result = new Dictionary<float, float>();
            var candidates = new Dictionary<float, float[]>
            {
                { 0.0f, new[] { 11.5f, 12.5f } },
                { 3.0f, new[] { 2.5f, 3.5f } },
                { 6.0f, new[] { 5.5f, 6.5f } },
                { 9.0f, new[] { 8.5f, 9.5f } },
            };

            foreach (var pair in candidates)
            {
                var safeClock = pair.Value.FirstOrDefault(clock =>
                    IsPointSafeForDangers(P4ClockPoint(newTwelveDirection, clock, 8.0f), dangers));
                result[pair.Key] = safeClock == 0.0f && pair.Key != 0.0f ? pair.Key : safeClock;
            }

            return result;
        }

        private bool TryDrawP4FinalSafe(ScriptAccessory accessory, int generation)
        {
            long startedAt;
            Vector3 newTwelveDirection;
            P4ElementCall call;
            List<P4DangerRecord> dangers;

            lock (_p4Lock)
            {
                if (_p4FGroupDangerDrawn || _p4FGroupStartedAt <= 0)
                    return false;

                startedAt = _p4FGroupStartedAt;
                newTwelveDirection = DefaultNorth;
                call = _p4FElementCall;
                var rects = _p4DangerRecords
                    .Where(r => r.Shape == P4DangerShape.Rect && r.CapturedAt >= startedAt)
                    .OrderBy(r => r.CapturedAt)
                    .Take(2)
                    .ToList();
                var fans = _p4DangerRecords
                    .Where(r => r.Shape == P4DangerShape.Fan && r.CapturedAt >= startedAt)
                    .OrderBy(r => r.CapturedAt)
                    .Take(2)
                    .ToList();

                if (rects.Count < 2 || fans.Count < 2)
                    return false;

                dangers = rects.Concat(fans).ToList();
            }

            var finalQuadrants = new[]
            {
                new[] { 11.5f, 12.5f },
                new[] { 2.5f, 3.5f },
                new[] { 5.5f, 6.5f },
                new[] { 8.5f, 9.5f },
            };
            var safeClocks = new List<float>();
            foreach (var quadrant in finalQuadrants)
            {
                var safeClock = quadrant.FirstOrDefault(clock =>
                    IsPointSafeForDangers(P4ClockPoint(newTwelveDirection, clock, 3.0f), dangers)
                    && IsPointSafeForDangers(P4ClockPoint(newTwelveDirection, clock, 8.0f), dangers));
                if (safeClock > 0.0f)
                    safeClocks.Add(safeClock);
                if (safeClocks.Count >= 2)
                    break;
            }

            if (safeClocks.Count < 2)
            {
                DebugEcho(accessory, $"P4F FINAL no-safe dangers={FormatP4Dangers(dangers)} safe={string.Join(",", safeClocks.Select(c => c.ToString("F1", CultureInfo.InvariantCulture)))}");
                return false;
            }

            lock (_p4Lock)
            {
                if (_p4FGroupDangerDrawn || generation != _generation || _phase != Phase.P4)
                    return false;

                _p4FGroupDangerDrawn = true;
            }

            var myIndex = GetMyIndex(accessory);
            var distance = call == P4ElementCall.WaterMoon ? 3.0f : 8.0f;
            if (myIndex >= 0 && myIndex < 8)
            {
                var targetClock = myIndex < 4 ? safeClocks[0] : safeClocks[1];
                var target = P4ClockPoint(newTwelveDirection, targetClock, distance);
                DrawP4SelfGuide(accessory, $"{DrawPrefix}_P4_F_Final_Guide", $"{DrawPrefix}_P4_F_Final_Target", target, 5000);
                DebugEcho(accessory, $"P4F FINAL my={PartyPriorityLabel(myIndex)} clock={targetClock:F1} dist={distance:F1} target={FormatPosition(target)} call={FormatP4ElementCall(call)} dangers={FormatP4Dangers(dangers)}");
            }

            return true;
        }

        private static bool IsPointSafeForDangers(Vector3 point, IReadOnlyList<P4DangerRecord> dangers)
        {
            return dangers == null || dangers.All(danger => !IsPointInP4Danger(point, danger));
        }

        private static bool IsPointInP4Danger(Vector3 point, P4DangerRecord danger)
        {
            return danger.Shape == P4DangerShape.Rect
                ? IsPointInP4RectDanger(point, danger)
                : IsPointInP4FanDanger(point, danger);
        }

        private static bool IsPointInP4RectDanger(Vector3 point, P4DangerRecord danger)
        {
            var forward = new Vector3(MathF.Sin(danger.Rotation), 0.0f, MathF.Cos(danger.Rotation));
            var right = new Vector3(forward.Z, 0.0f, -forward.X);
            var delta = new Vector3(point.X - danger.Position.X, 0.0f, point.Z - danger.Position.Z);
            var along = delta.X * forward.X + delta.Z * forward.Z;
            var side = delta.X * right.X + delta.Z * right.Z;

            return along >= -0.25f
                && along <= P4RectDangerLength + 0.25f
                && MathF.Abs(side) <= P4RectDangerWidth / 2.0f + 0.25f;
        }

        private static bool IsPointInP4FanDanger(Vector3 point, P4DangerRecord danger)
        {
            var delta = new Vector3(point.X - danger.Position.X, 0.0f, point.Z - danger.Position.Z);
            var distance = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            if (distance > P4FanDangerRadius + 0.25f)
                return false;

            if (distance < 0.001f)
                return true;

            var forward = new Vector3(MathF.Sin(danger.Rotation), 0.0f, MathF.Cos(danger.Rotation));
            var dot = (delta.X * forward.X + delta.Z * forward.Z) / distance;
            var limit = MathF.Cos(P4FanDangerRadian / 2.0f + 0.02f);
            return dot >= limit;
        }

        private static string FormatP4Dangers(IReadOnlyList<P4DangerRecord> dangers)
        {
            if (dangers == null || dangers.Count == 0)
                return "-";

            return string.Join(
                ",",
                dangers.Select(d => $"{d.ActionId}/{d.Shape}@{FormatPosition(d.Position)}/{d.Rotation:F2}"));
        }

        private static bool TryConvertToUInt(object value, out uint result)
        {
            result = 0;
            if (value == null)
                return false;

            switch (value)
            {
                case byte v:
                    result = v;
                    return true;
                case ushort v:
                    result = v;
                    return true;
                case uint v:
                    result = v;
                    return true;
                case int v when v >= 0:
                    result = (uint)v;
                    return true;
                case long v when v >= 0 && v <= uint.MaxValue:
                    result = (uint)v;
                    return true;
                case string s:
                    return TryParseUInt(s, out result);
            }

            try
            {
                if (value is IConvertible convertible)
                {
                    result = convertible.ToUInt32(CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                result = 0;
            }

            return false;
        }

        private static object ReadPropertyValue(object value, string propertyName)
        {
            if (value == null)
                return null;

            var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(value);
        }

        private static bool TryReadUIntProperty(object value, string propertyName, out uint result)
        {
            result = 0;
            var propertyValue = ReadPropertyValue(value, propertyName);
            return TryConvertToUInt(propertyValue, out result);
        }

        private static bool TryExtractClassJobRowId(object value, out uint rowId)
        {
            rowId = 0;
            if (value == null)
                return false;

            if (TryConvertToUInt(value, out rowId))
                return true;

            if (TryReadUIntProperty(value, "RowId", out rowId)
                || TryReadUIntProperty(value, "Id", out rowId)
                || TryReadUIntProperty(value, "ID", out rowId))
                return true;

            var nestedValue = ReadPropertyValue(value, "Value");
            return nestedValue != null
                && !ReferenceEquals(nestedValue, value)
                && TryExtractClassJobRowId(nestedValue, out rowId);
        }

        private static bool TryGetPartyMemberClassJobRowId(ScriptAccessory accessory, int partyIndex, out uint rowId)
        {
            rowId = 0;
            if (partyIndex < 0 || partyIndex >= accessory.Data.PartyList.Count)
                return false;

            var obj = accessory.Data.Objects.SearchById(accessory.Data.PartyList[partyIndex]);
            if (obj == null)
                return false;

            if (TryReadUIntProperty(obj, "ClassJobId", out rowId)
                || TryReadUIntProperty(obj, "ClassJobID", out rowId))
                return true;

            return TryExtractClassJobRowId(ReadPropertyValue(obj, "ClassJob"), out rowId);
        }

        private static string PartyMemberJobName(ScriptAccessory accessory, int partyIndex)
        {
            return TryGetPartyMemberClassJobRowId(accessory, partyIndex, out var rowId)
                ? JobShortName(rowId)
                : "未知职业";
        }

        private static string JobShortName(uint classJobRowId)
        {
            switch (classJobRowId)
            {
                case 1: return "剑术";
                case 2: return "格斗";
                case 3: return "斧术";
                case 4: return "枪术";
                case 5: return "弓术";
                case 6: return "幻术";
                case 7: return "咒术";
                case 19: return "骑士";
                case 20: return "武僧";
                case 21: return "战士";
                case 22: return "龙骑";
                case 23: return "诗人";
                case 24: return "白魔";
                case 25: return "黑魔";
                case 26: return "秘术";
                case 27: return "召唤";
                case 28: return "学者";
                case 29: return "双剑";
                case 30: return "忍者";
                case 31: return "机工";
                case 32: return "暗骑";
                case 33: return "占星";
                case 34: return "武士";
                case 35: return "赤魔";
                case 36: return "青魔";
                case 37: return "绝枪";
                case 38: return "舞者";
                case 39: return "钐镰";
                case 40: return "贤者";
                case 41: return "蝰蛇";
                case 42: return "绘灵";
                default: return "未知";
            }
        }

        private static string JobChineseName(uint classJobRowId)
        {
            switch (classJobRowId)
            {
                case 1: return "剑术师";
                case 2: return "格斗家";
                case 3: return "斧术师";
                case 4: return "枪术师";
                case 5: return "弓箭手";
                case 6: return "幻术师";
                case 7: return "咒术师";
                case 19: return "骑士";
                case 20: return "武僧";
                case 21: return "战士";
                case 22: return "龙骑士";
                case 23: return "吟游诗人";
                case 24: return "白魔法师";
                case 25: return "黑魔法师";
                case 26: return "秘术师";
                case 27: return "召唤师";
                case 28: return "学者";
                case 29: return "双剑师";
                case 30: return "忍者";
                case 31: return "机工士";
                case 32: return "暗黑骑士";
                case 33: return "占星术士";
                case 34: return "武士";
                case 35: return "赤魔法师";
                case 36: return "青魔法师";
                case 37: return "绝枪战士";
                case 38: return "舞者";
                case 39: return "钐镰客";
                case 40: return "贤者";
                case 41: return "蝰蛇剑士";
                case 42: return "绘灵法师";
                default: return "未知职业";
            }
        }

        private MarkType?[] BuildP4CommandMarks(IReadOnlyList<P4HalfSide> sides)
        {
            var marks = new MarkType?[8];
            var leftCount = 0;
            var rightCount = 0;

            for (var i = 0; i < marks.Length && i < sides.Count; i++)
            {
                if (sides[i] == P4HalfSide.Left && leftCount < P4LeftMarks.Length)
                {
                    marks[i] = P4LeftMarks[leftCount];
                    leftCount++;
                }
                else if (sides[i] == P4HalfSide.Right && rightCount < P4RightMarks.Length)
                {
                    marks[i] = P4RightMarks[rightCount];
                    rightCount++;
                }
            }

            return marks;
        }

        private void ApplyP4CommandMarks(ScriptAccessory accessory, IReadOnlyList<MarkType?> marks)
        {
            if (!EnableP4HotpotMarks) return;

            CommandMarkClear(accessory);

            var totalCount = Math.Min(marks.Count, accessory.Data.PartyList.Count);
            for (var i = 0; i < totalCount; i++)
            {
                if (marks[i].HasValue)
                    CommandMarkPartyMember(accessory, i, marks[i].Value);
            }

            ScheduleP4CommandMarkClear(accessory, _generation, P4GuideDurationMs);
        }

        private void DebugP4Resolve(
            ScriptAccessory accessory,
            int[,] states,
            IReadOnlyList<P4HalfSide> sides,
            IReadOnlyList<P4HalfColor> colors,
            IReadOnlyList<MarkType?> marks,
            int fourthParam,
            P4HalfSide sourceSide,
            P4HalfColor leftColor,
            P4HalfColor rightColor,
            Vector3 newTwelveDirection,
            Vector3 l1,
            Vector3 r1)
        {
            if (!DeveloperMode) return;

            DebugEcho(accessory, $"P4一运结算 fourthParam={fourthParam} sourceSide={FormatP4Side(sourceSide)} left={FormatP4Color(leftColor)} right={FormatP4Color(rightColor)} new12={FormatPosition(newTwelveDirection)} L1={FormatPosition(l1)} R1={FormatPosition(r1)}");

            for (var i = 0; i < 8; i++)
            {
                var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
                DebugEcho(
                    accessory,
                    $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} => color={FormatP4Color(colors[i])} side={FormatP4Side(sides[i])} mark={FormatP4Mark(marks[i])}");
            }
        }

        private void ExecuteP4ChainMove(ScriptAccessory accessory, int generation, int round)
        {
            var states = new int[8, 10];
            var expiresAt = new long[8, 10];
            Vector3 newTwelveDirection;
            bool directionReady;
            var now = NowMs();

            lock (_p4Lock)
            {
                for (var i = 0; i < 8; i++)
                    NormalizeP4ExclusivePairByTruth(_p4StatusStateByPartyAndStatus, _p4StatusExpiresAtByPartyAndStatus, i, 3, 4, now);

                Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
                Array.Copy(_p4StatusExpiresAtByPartyAndStatus, expiresAt, _p4StatusExpiresAtByPartyAndStatus.Length);
                newTwelveDirection = _p4FourthDirectionReady ? _p4FourthNewTwelveDirection : DefaultNorth;
                directionReady = _p4FourthDirectionReady;
            }

            var clocks = new P4ClockDirection[8];
            var targets = new Vector3[8];
            var moveCalls = new string[8];
            var moveCallStates = new int[8];
            var moveCallRemaining = new int[8];
            var partyMessages = new List<string>();

            for (var i = 0; i < 8; i++)
            {
                clocks[i] = ResolveP4MoveClock(states, expiresAt, i, now);
                targets[i] = P4ClockTarget(newTwelveDirection, clocks[i]);

                if (TryGetP4ActiveSoonState(states, expiresAt, i, 5546, now, out var state5546, out var remaining5546))
                {
                    var callText = P4MoveCallText(state5546);
                    if (!string.IsNullOrWhiteSpace(callText))
                    {
                        moveCalls[i] = callText;
                        moveCallStates[i] = state5546;
                        moveCallRemaining[i] = remaining5546;

                        if (i < accessory.Data.PartyList.Count)
                            partyMessages.Add($"{PartyMemberJobName(accessory, i)} {PartyPriorityLabel(i)} {callText}");
                    }
                }
            }

            var marks = BuildP4ChainMoveMarks(clocks);
            lock (_p4Lock)
                Array.Copy(marks, _p4CommandMarkByPartyIndex, marks.Length);

            ApplyP4CommandMarksNoTimer(accessory, marks);

            var myIndex = GetMyIndex(accessory);
            if (myIndex >= 0 && myIndex < 8 && clocks[myIndex] != P4ClockDirection.Unknown)
            {
                var target = targets[myIndex];
                DrawGuide(accessory, $"{DrawPrefix}_P4_Chain_Move_Guide", target, P4ChainGuideDurationMs);
                DrawStaticCircle(accessory, $"{DrawPrefix}_P4_Chain_Move_Target", target, 0.25f, P4ChainGuideDurationMs, GuideColor.V4);
                DebugEcho(accessory, $"P4循环移动指路已画：round={round} my={PartyPriorityLabel(myIndex)} clock={FormatP4Clock(clocks[myIndex])} target={FormatPosition(target)} duration={P4ChainGuideDurationMs}ms greenMove={EnableGreenMove}");
                GreenMoveToPoint(target, accessory, $"P4 chain move round {round}");

                if (!string.IsNullOrWhiteSpace(moveCalls[myIndex]))
                    ScheduleP4SelfTtsAtRemaining3(accessory, generation, round, myIndex, moveCallStates[myIndex], moveCallRemaining[myIndex], moveCalls[myIndex]);
            }
            else
            {
                DebugEcho(accessory, $"P4循环移动指路未画：round={round} myIndex={myIndex}");
            }

            SendP4PartyCallouts(accessory, partyMessages, generation, round);
            DebugP4ChainMove(accessory, round, states, expiresAt, clocks, targets, marks, moveCalls, moveCallRemaining, newTwelveDirection, directionReady, now);
        }

        private void ExecuteP4Petrify(ScriptAccessory accessory, int round)
        {
            var states = new int[8, 10];
            var expiresAt = new long[8, 10];
            var now = NowMs();

            lock (_p4Lock)
            {
                Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
                Array.Copy(_p4StatusExpiresAtByPartyAndStatus, expiresAt, _p4StatusExpiresAtByPartyAndStatus.Length);
            }

            var marks = BuildP4PetrifyMarks(states, expiresAt, now);
            lock (_p4Lock)
                Array.Copy(marks, _p4CommandMarkByPartyIndex, marks.Length);

            ApplyP4CommandMarksNoTimer(accessory, marks);

            var myIndex = GetMyIndex(accessory);
            string alertText = null;
            var alertPartyIndex = -1;
            var alertState = P4StateUnknown;
            var alertRemaining = 0;

            if (TryGetP4ActiveSoonState(states, expiresAt, myIndex, 5543, now, out var myState5543, out var myRemaining5543))
            {
                alertText = P4PetrifyCallText(myState5543, true);
                alertPartyIndex = myIndex;
                alertState = myState5543;
                alertRemaining = myRemaining5543;
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    if (!TryGetP4ActiveSoonState(states, expiresAt, i, 5543, now, out var state5543, out var remaining5543))
                        continue;

                    alertText = P4PetrifyCallText(state5543, false);
                    alertPartyIndex = i;
                    alertState = state5543;
                    alertRemaining = remaining5543;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(alertText))
                Alert(alertText);

            DebugP4Petrify(accessory, round, states, expiresAt, marks, alertPartyIndex, alertState, alertRemaining, alertText, now);
        }

        private void ExecuteP4Element(ScriptAccessory accessory, int round)
        {
            var states = new int[8, 10];
            var expiresAt = new long[8, 10];
            var now = NowMs();

            lock (_p4Lock)
            {
                Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
                Array.Copy(_p4StatusExpiresAtByPartyAndStatus, expiresAt, _p4StatusExpiresAtByPartyAndStatus.Length);
            }

            TryResolveP4ElementCall(states, expiresAt, now, out var call, out var partyIndex, out var statusId, out var state, out var remainingMs);
            var text = P4ElementCallText(call);
            if (!string.IsNullOrWhiteSpace(text))
                QTTS(text);

            DebugP4Element(accessory, round, states, expiresAt, call, partyIndex, statusId, state, remainingMs, now);
        }

        private void DebugP4ChainMove(
            ScriptAccessory accessory,
            int round,
            int[,] states,
            long[,] expiresAt,
            IReadOnlyList<P4ClockDirection> clocks,
            IReadOnlyList<Vector3> targets,
            IReadOnlyList<MarkType?> marks,
            IReadOnlyList<string> moveCalls,
            IReadOnlyList<int> moveCallRemaining,
            Vector3 newTwelveDirection,
            bool directionReady,
            long now)
        {
            if (!DeveloperMode) return;

            DebugEcho(accessory, $"P4MOVE r={round} new12={FormatPosition(newTwelveDirection)} ready={directionReady}");
            for (var i = 0; i < 8; i++)
            {
                var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
                var buffs = FormatP4StatusesWithRemaining(states, expiresAt, i, now, 5544, 5545, 5546);
                DebugEcho(
                    accessory,
                    $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} {buffs} => clock={FormatP4Clock(clocks[i])} target={FormatPosition(targets[i])} call={moveCalls[i] ?? "-"}({FormatRemainingMs(moveCallRemaining[i])}) mark={FormatP4Mark(marks[i])}");
            }
        }
        private void DebugP4Petrify(
            ScriptAccessory accessory,
            int round,
            int[,] states,
            long[,] expiresAt,
            IReadOnlyList<MarkType?> marks,
            int alertPartyIndex,
            int alertState,
            int alertRemaining,
            string alertText,
            long now)
        {
            if (!DeveloperMode) return;

            DebugEcho(accessory, $"P4PETRI r={round} alert={PartyPriorityLabel(alertPartyIndex)} state={alertState}({FormatRemainingMs(alertRemaining)}) text={alertText ?? "-"}");
            for (var i = 0; i < 8; i++)
            {
                TryGetP4ActiveSoonState(states, expiresAt, i, 5543, now, out var state5543, out var remaining5543);
                var call = P4PetrifyCallText(state5543, false) ?? "-";
                var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
                DebugEcho(
                    accessory,
                    $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} {FormatP4StatusStateWithRemaining(states, expiresAt, i, 5543, now)} => active={state5543}({FormatRemainingMs(remaining5543)}) call={call} mark={FormatP4Mark(marks[i])}");
            }
        }
        private void DebugP4Element(
            ScriptAccessory accessory,
            int round,
            int[,] states,
            long[,] expiresAt,
            P4ElementCall call,
            int partyIndex,
            uint statusId,
            int state,
            int remainingMs,
            long now)
        {
            if (!DeveloperMode) return;

            DebugEcho(accessory, $"P4ELEM r={round} result={FormatP4ElementCall(call)} src={PartyPriorityLabel(partyIndex)} id={statusId} state={state}({FormatRemainingMs(remainingMs)})");
            for (var i = 0; i < 8; i++)
            {
                TryGetP4ActiveSoonState(states, expiresAt, i, 5547, now, out var active5547, out var activeRemaining5547);
                TryGetP4ActiveSoonState(states, expiresAt, i, 5548, now, out var active5548, out var activeRemaining5548);
                var objectId = i < accessory.Data.PartyList.Count ? accessory.Data.PartyList[i] : 0;
                DebugEcho(
                    accessory,
                    $"{PartyPriorityLabel(i)} {FormatObjectId(objectId)} {FormatP4StatusesWithRemaining(states, expiresAt, i, now, 5547, 5548)} => active5547={active5547}({FormatRemainingMs(activeRemaining5547)}) active5548={active5548}({FormatRemainingMs(activeRemaining5548)})");
            }
        }
        #endregion

        #region GreenMove

        private void GreenMoveToPoint(Vector3 target, ScriptAccessory accessory, string reason)
        {
            if (!EnableGreenMove) return;

            try
            {
                if (GreenMoveClearQueueBeforeMove)
                    GreenMoveClearQueue();

                InvokeGreenMove6(
                    "MoveToPoint",
                    ref _greenMoveToPointSub,
                    ref _greenMoveToPointInvoke,
                    target,
                    GreenMoveTolerance > 0.0f ? GreenMoveTolerance : 0.1f,
                    GreenMoveSpeedMultiplier > 0.0f ? GreenMoveSpeedMultiplier : 1.0f,
                    GreenMoveIgnoreY,
                    Math.Max(0, GreenMoveMaxCastWaitMs),
                    GreenMoveWaitForQueuedCast);
            }
            catch (Exception ex)
            {
                WarnGreenMoveFailure(accessory, ex);
            }
        }

        private void GreenMoveStopAndClear(ScriptAccessory accessory, string reason)
        {
            if (!EnableGreenMove) return;

            try
            {
                GreenMoveClearQueue();
                GreenMoveStop();
            }
            catch (Exception ex)
            {
                WarnGreenMoveFailure(accessory, ex);
            }
        }

        private void GreenMoveClearQueue()
        {
            InvokeGreenMoveAction0("ClearQueue", ref _greenMoveClearQueueSub, ref _greenMoveClearQueueInvoke);
        }

        private void GreenMoveStop()
        {
            InvokeGreenMoveAction0("Stop", ref _greenMoveStopSub, ref _greenMoveStopInvoke);
        }

        private void WarnGreenMoveFailure(ScriptAccessory accessory, Exception ex)
        {
            if (_warnedGreenMove) return;

            _warnedGreenMove = true;
            accessory.Method.SendChat("/e GreenMove IPC调用失败，请确认PromeRotation已加载且GreenMove IPC已注册。");
        }

        private static void InvokeGreenMove6<T1>(
            string name,
            ref object sub,
            ref MethodInfo invoke,
            T1 arg1,
            float tolerance,
            float speed,
            bool ignoreY,
            int maxCastWaitMs,
            bool waitForQueuedCast)
        {
            var fullName = GreenMovePrefix + name;
            if (sub == null || invoke == null)
            {
                sub = CreateSubscriber<T1, float, float, bool, int, bool, object>(fullName);
                invoke = sub.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "InvokeAction" && m.GetParameters().Length == 6)
                    ?? throw new MissingMethodException($"{name} missing InvokeAction.");
            }

            try
            {
                invoke.Invoke(sub, new object[] { arg1, tolerance, speed, ignoreY, maxCastWaitMs, waitForQueuedCast });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static void InvokeGreenMoveAction0(string name, ref object sub, ref MethodInfo invoke)
        {
            var fullName = GreenMovePrefix + name;
            if (sub == null || invoke == null)
            {
                sub = CreateSubscriber<object>(fullName);
                invoke = sub.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "InvokeAction" && m.GetParameters().Length == 0)
                    ?? throw new MissingMethodException($"{name} missing InvokeAction.");
            }

            try
            {
                invoke.Invoke(sub, Array.Empty<object>());
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static object CreateSubscriber<TRet>(string fullName)
        {
            var pluginInterface = GetKodakkuPluginInterface();
            var method = pluginInterface.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "GetIpcSubscriber" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1);

            if (method == null)
                throw new MissingMethodException("GetIpcSubscriber<TRet> not found.");

            return method.MakeGenericMethod(typeof(TRet)).Invoke(pluginInterface, new object[] { fullName })
                ?? throw new InvalidOperationException($"{fullName} subscriber is null.");
        }

        private static object CreateSubscriber<T1, T2, T3, T4, T5, T6, TRet>(string fullName)
        {
            var pluginInterface = GetKodakkuPluginInterface();
            var method = pluginInterface.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "GetIpcSubscriber" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 7);

            if (method == null)
                throw new MissingMethodException("GetIpcSubscriber<T1,T2,T3,T4,T5,T6,TRet> not found.");

            return method.MakeGenericMethod(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(TRet)).Invoke(pluginInterface, new object[] { fullName })
                ?? throw new InvalidOperationException($"{fullName} subscriber is null.");
        }

        private static object GetKodakkuPluginInterface()
        {
            var serviceType = typeof(ScriptAccessory).Assembly.GetType("KodakkuAssist.Data.Service")
                ?? throw new TypeLoadException("KodakkuAssist.Data.Service not found.");
            var prop = serviceType.GetProperty("PluginInterface", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMemberException("KodakkuAssist.Data.Service.PluginInterface not found.");

            return prop.GetValue(null)
                ?? throw new InvalidOperationException("KodakkuAssist PluginInterface is null.");
        }

        #endregion

        #region Draw Helpers

        private void DrawCircle(ScriptAccessory accessory, string name, Vector3 position, float radius, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Scale = new Vector2(radius);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void DrawStaticCircle(ScriptAccessory accessory, string name, Vector3 position, float radius, int duration, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Scale = new Vector2(radius);
            dp.Color = color ?? accessory.Data.DefaultSafeColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.None;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void DrawCircleOnOwner(ScriptAccessory accessory, string name, uint ownerId, float radius, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = ownerId;
            dp.Scale = new Vector2(radius);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void DrawRect(Event @event, ScriptAccessory accessory, string name, float width, float length, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.YByTime)
        {
            DrawRect(accessory, name, @event.SourcePosition, @event.SourceRotation, width, length, duration, delay, color, scaleMode);
        }

        private void DrawRect(ScriptAccessory accessory, string name, Vector3 position, float rotation, float width, float length, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.YByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Rotation = rotation;
            dp.Scale = new Vector2(width, length);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        private void DrawFan(Event @event, ScriptAccessory accessory, string name, float radius, float radian, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            DrawFan(accessory, name, @event.SourcePosition, @event.SourceRotation, radius, radian, duration, delay, color, scaleMode);
        }

        private void DrawFan(ScriptAccessory accessory, string name, Vector3 position, float rotation, float radius, float radian, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Rotation = rotation;
            dp.Scale = new Vector2(radius);
            dp.Radian = radian;
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        private void DrawLineToTarget(ScriptAccessory accessory, string name, uint sourceId, uint targetId, float width, int duration, int delay = 0, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = sourceId;
            dp.TargetObject = targetId;
            dp.Scale = new Vector2(width, 1.0f);
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.YByDistance;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        private void DrawGuide(ScriptAccessory accessory, string name, Vector3 targetPosition, int duration, int delay = 0, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = GetMyId(accessory);
            dp.TargetPosition = targetPosition;
            dp.Scale = new Vector2(0.5f);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = color ?? GuideColor.V4;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        private void DrawGuideBetweenPositions(ScriptAccessory accessory, string name, Vector3 sourcePosition, Vector3 targetPosition, int duration, int delay = 0, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = sourcePosition;
            dp.TargetPosition = targetPosition;
            dp.Scale = new Vector2(0.5f);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = color ?? GuideColor.V4;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        private void DrawInactiveGuideBetweenPositions(ScriptAccessory accessory, string name, Vector3 sourcePosition, Vector3 targetPosition, int duration, int delay = 0)
        {
            DrawGuideBetweenPositions(accessory, name, sourcePosition, targetPosition, duration, delay, SolidDangerRed);
        }

        #endregion

        #region Mechanisms

        [ScriptMethod(name: "P4开始", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49884"], userControl: false)]
        public void P4_Start(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _phase = Phase.P4;
            ResetMechanic(accessory);
        }

        [ScriptMethod(name: "P4 2056真伪窗口", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056"], userControl: false)]
        public void P4_StatusTruthWindow(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            if (!int.TryParse(@event["Param"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var param))
                return;

            if (!TryGetTargetDataId(@event, accessory, out var targetDataId))
                return;

            var now = NowMs();

            if (targetDataId == P4XDataId && (param == 1121 || param == 1122))
            {
                var targetPosition = ResolveEventTargetPosition(@event, accessory);
                var newTwelveDirection = DefaultNorth;
                var directionReady = IsLikelyArenaPosition(targetPosition)
                    && TryNormalizeFromCenter(targetPosition, out newTwelveDirection);

                lock (_p4Lock)
                {
                    _p4XParam = param;
                    _p4XUpdatedAt = now;
                    _p4XExpiresAt = now + P4StatusWindowMs;
                    _p4XEventCount++;

                    if (_p4XEventCount == 3)
                    {
                        _p4GroupSnapshotParam = param;
                        _p4GroupSnapshotAt = now;
                    }

                    if (_p4XEventCount == 4)
                    {
                        _p4FourthXParam = param;
                        _p4FourthSnapshotAt = now;
                        _p4FourthTargetPosition = targetPosition;
                        _p4FourthNewTwelveDirection = directionReady ? newTwelveDirection : DefaultNorth;
                        _p4FourthDirectionReady = directionReady;
                    }
                }
            }
            else if (targetDataId == P4CDataId && (param == 1119 || param == 1120))
            {
                lock (_p4Lock)
                {
                    _p4CParam = param;
                    _p4CUpdatedAt = now;
                    _p4CExpiresAt = now + P4StatusWindowMs;
                }
            }

            ScheduleP4LaterGroupsIfReady(accessory);
        }

        [ScriptMethod(name: "P4 状态变量赋值", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(554[1-8]|454|1382|4887|4888|5464)$"], userControl: false)]
        public void P4_TrackedStatusAdd(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            if (!TryGetStatusId(@event, out var rawStatusId))
                return;

            var directTrue = TryGetP4DirectTrueStatus(rawStatusId, out var directTrueStatusId);
            var statusId = directTrue ? directTrueStatusId : rawStatusId;

            if (!TryGetP4TrackedStatusIndex(statusId, out var statusIndex))
                return;

            if (!TryGetTargetId(@event, out var targetId))
                return;

            var partyIndex = GetPlayerIndex(accessory, targetId);
            if (partyIndex < 0 || partyIndex >= 8)
                return;

            var now = NowMs();
            var hasDuration = TryGetDurationMs(@event, out var durationMs);
            var assigned = false;
            var assignedState = P4StateUnknown;
            var xParam = 0;
            var cParam = 0;
            var xExpiresAt = 0L;
            var cExpiresAt = 0L;
            var xRemainingMs = 0;
            var cRemainingMs = 0;
            var xActive = false;
            var cActive = false;
            var usedWindow = "-";
            var usedParam = 0;
            var usedWindowRemainingMs = 0;
            var storedState = P4StateUnknown;
            var storedRemainingMs = 0;
            var chainRound = 0;
            var chainStep = P4ChainStep.None;

            lock (_p4Lock)
            {
                if (hasDuration)
                    _p4StatusExpiresAtByPartyAndStatus[partyIndex, statusIndex] = now + durationMs;

                if (directTrue)
                {
                    _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] = P4StateTrue;
                    assigned = true;
                    assignedState = P4StateTrue;
                    usedWindow = "direct";
                }
                else if (IsP4ChainDebugStatus(statusId)
                    && TryResolveP4LaterStateFromWindow(statusId, _p4XParam, _p4XExpiresAt, _p4CParam, _p4CExpiresAt, now, out var laterStateValue, out usedWindow, out usedParam, out usedWindowRemainingMs))
                {
                    _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] = laterStateValue;
                    assigned = true;
                    assignedState = laterStateValue;

                    if (hasDuration)
                    {
                        _p4BuffRecords.Add(new P4BuffRecord
                        {
                            PartyIndex = partyIndex,
                            StatusId = statusId,
                            State = laterStateValue,
                            DurationMs = durationMs,
                            ExpiresAt = now + durationMs,
                        });
                    }
                }
                else if (!IsP4ChainDebugStatus(statusId)
                    && TryGetActiveP4StateValueLocked(now, out var stateValue, out usedWindow, out usedParam, out usedWindowRemainingMs))
                {
                    _p4StatusStateByPartyAndStatus[partyIndex, statusIndex] = stateValue;
                    assigned = true;
                    assignedState = stateValue;
                }

                xParam = _p4XParam;
                cParam = _p4CParam;
                xExpiresAt = _p4XExpiresAt;
                cExpiresAt = _p4CExpiresAt;
                xRemainingMs = RemainingMs(_p4XExpiresAt, now);
                cRemainingMs = RemainingMs(_p4CExpiresAt, now);
                xActive = _p4XParam != 0 && xRemainingMs > 0;
                cActive = _p4CParam != 0 && cRemainingMs > 0;
                storedState = _p4StatusStateByPartyAndStatus[partyIndex, statusIndex];
                storedRemainingMs = RemainingMs(_p4StatusExpiresAtByPartyAndStatus[partyIndex, statusIndex], now);
                chainRound = _p4ChainRound;
                chainStep = _p4ChainStep;
            }

            if (IsP4ChainDebugStatus(statusId))
            {
                DebugEcho(
                    accessory,
                    $"P4BUFF+ r={chainRound}/{chainStep} {PartyPriorityLabel(partyIndex)} {P4BuffName(statusId)} id={statusId} set={(assigned ? assignedState.ToString(CultureInfo.InvariantCulture) : "-")} now={storedState}({FormatRemainingMs(storedRemainingMs)}) dur={(hasDuration ? FormatRemainingMs(durationMs) : "-")} use={FormatP4UsedWindowDebug(usedWindow, usedParam, usedWindowRemainingMs)} {FormatP4WindowDebug("X", xParam, xRemainingMs, xActive)} {FormatP4WindowDebug("C", cParam, cRemainingMs, cActive)}");
                ScheduleP4LaterGroupsIfReady(accessory);
            }
        }

        [ScriptMethod(name: "P4 50070后续循环开始", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:50070"], userControl: false)]
        public void P4_ChainStartActionEffect(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            accessory.Method.RemoveDraw($"{DrawPrefix}_P4_First_.*");
            ClearP4CommandMarksNow(accessory);
            GreenMoveStopAndClear(accessory, "P4 chain start");
            DebugEcho(accessory, "P4 later timeline start by 50070");
            ScheduleP4LaterGroupsIfReady(accessory);
        }

        [ScriptMethod(name: "P4 后续状态移除推进", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^554[3-8]$"], userControl: false)]
        public void P4_TrackedStatusRemove(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            if (!TryGetStatusId(@event, out var statusId))
                return;

            if (!TryGetP4TrackedStatusIndex(statusId, out var statusIndex))
                return;

            if (!TryGetTargetId(@event, out var targetId))
                return;

            var partyIndex = GetPlayerIndex(accessory, targetId);
            if (partyIndex < 0 || partyIndex >= 8)
                return;
        }

        [ScriptMethod(name: "P4 后续危险范围捕获", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47777|47775|47768|47774)$"], userControl: false)]
        public void P4_LaterDangerStartCasting(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            if (!TryGetActionId(@event, out var actionId))
                return;

            P4DangerRecord record;
            if (actionId == 47777 || actionId == 47775)
            {
                record = new P4DangerRecord
                {
                    ActionId = actionId,
                    Shape = P4DangerShape.Rect,
                    Position = @event.SourcePosition,
                    Rotation = @event.SourceRotation,
                    CapturedAt = NowMs(),
                };
            }
            else
            {
                record = new P4DangerRecord
                {
                    ActionId = actionId,
                    Shape = P4DangerShape.Fan,
                    Position = P4ArenaCenter,
                    Rotation = @event.SourceRotation,
                    CapturedAt = NowMs(),
                };
            }

            var shouldTryFinal = false;
            lock (_p4Lock)
            {
                _p4DangerRecords.Add(record);
                shouldTryFinal = _p4FGroupStartedAt > 0 && !_p4FGroupDangerDrawn;
            }

            DebugEcho(accessory, $"P4DANGER+ id={actionId} shape={record.Shape} pos={FormatPosition(record.Position)} rot={record.Rotation:F3}");

            if (shouldTryFinal)
                TryDrawP4FinalSafe(accessory, _generation);
        }

        [ScriptMethod(name: "P4 一运半场指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50068|50069)$"], userControl: false)]
        public void P4_FirstMechanicGuide(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            if (_phase != Phase.P4) return;

            if (!TryGetActionId(@event, out var actionId))
                return;

            var states = new int[8, 10];
            int fourthParam;
            int fourthCount;
            long fourthSnapshotAt;
            Vector3 fourthTargetPosition;
            Vector3 blueSourcePosition;
            Vector3 purpleSourcePosition;
            Vector3 newTwelveDirection;
            Vector3 midpoint;

            lock (_p4Lock)
            {
                if (_p4HotpotResolved)
                    return;

                if (actionId == P4ResolveActionId)
                {
                    _p4HotpotBlueSourcePosition = @event.SourcePosition;
                    _p4HotpotBlueSourceReady = true;
                }
                else if (actionId == P4PurpleSourceActionId)
                {
                    _p4HotpotPurpleSourcePosition = @event.SourcePosition;
                    _p4HotpotPurpleSourceReady = true;
                }
                else
                {
                    return;
                }

                fourthParam = _p4FourthXParam;
                fourthCount = _p4XEventCount;
                fourthSnapshotAt = _p4FourthSnapshotAt;
                fourthTargetPosition = _p4FourthTargetPosition;
                blueSourcePosition = _p4HotpotBlueSourcePosition;
                purpleSourcePosition = _p4HotpotPurpleSourcePosition;

                if (!_p4HotpotBlueSourceReady || !_p4HotpotPurpleSourceReady)
                {
                    DebugEcho(
                        accessory,
                        $"P4HOTPOT WAIT action={actionId} blueReady={_p4HotpotBlueSourceReady} purpleReady={_p4HotpotPurpleSourceReady} blue50069={FormatPosition(_p4HotpotBlueSourcePosition)} purple50068={FormatPosition(_p4HotpotPurpleSourcePosition)}");
                    return;
                }

                if (fourthParam != 1121 && fourthParam != 1122)
                {
                    DebugEcho(accessory, $"P4一运结算跳过：第四次19510/2056未就绪 param={fourthParam} target={FormatPosition(_p4FourthTargetPosition)}");
                    return;
                }

                midpoint = new Vector3(
                    (blueSourcePosition.X + purpleSourcePosition.X) * 0.5f,
                    (blueSourcePosition.Y + purpleSourcePosition.Y) * 0.5f,
                    (blueSourcePosition.Z + purpleSourcePosition.Z) * 0.5f);

                if (!TryNormalizeFromCenter(midpoint, out newTwelveDirection))
                {
                    DebugEcho(accessory, $"P4一运结算跳过：50068/50069中点无效 blue50069={FormatPosition(blueSourcePosition)} purple50068={FormatPosition(purpleSourcePosition)} mid={FormatPosition(midpoint)}");
                    return;
                }

                Array.Copy(_p4StatusStateByPartyAndStatus, states, _p4StatusStateByPartyAndStatus.Length);
                _p4HotpotResolved = true;
            }

            var rightVector = RightVectorFromNewTwelve(newTwelveDirection);
            var l1 = P4ArenaCenter - rightVector * P4GuideOffset;
            var r1 = P4ArenaCenter + rightVector * P4GuideOffset;
            var blueSide = SideOfPosition(blueSourcePosition, rightVector);
            var purpleSide = SideOfPosition(purpleSourcePosition, rightVector);
            var blueDelta = new Vector3(blueSourcePosition.X - P4ArenaCenter.X, 0.0f, blueSourcePosition.Z - P4ArenaCenter.Z);
            var purpleDelta = new Vector3(purpleSourcePosition.X - P4ArenaCenter.X, 0.0f, purpleSourcePosition.Z - P4ArenaCenter.Z);
            var blueDot = blueDelta.X * rightVector.X + blueDelta.Z * rightVector.Z;
            var purpleDot = purpleDelta.X * rightVector.X + purpleDelta.Z * rightVector.Z;
            var leftColor = blueSide == P4HalfSide.Left
                ? P4HalfColor.Blue
                : purpleSide == P4HalfSide.Left
                    ? P4HalfColor.Purple
                    : P4HalfColor.Unknown;
            var rightColor = blueSide == P4HalfSide.Right
                ? P4HalfColor.Blue
                : purpleSide == P4HalfSide.Right
                    ? P4HalfColor.Purple
                    : P4HalfColor.Unknown;
            if (leftColor == P4HalfColor.Unknown && rightColor != P4HalfColor.Unknown)
                leftColor = OppositeColor(rightColor);
            if (rightColor == P4HalfColor.Unknown && leftColor != P4HalfColor.Unknown)
                rightColor = OppositeColor(leftColor);
            var fourthAgeMs = fourthSnapshotAt > 0 ? NowMs() - fourthSnapshotAt : -1;

            DebugEcho(
                accessory,
                $"P4HOTPOT DIAG xCount={fourthCount} fourthParam={fourthParam} fourthAge={fourthAgeMs}ms fourthTarget={FormatPosition(fourthTargetPosition)} blue50069={FormatPosition(blueSourcePosition)} purple50068={FormatPosition(purpleSourcePosition)} mid={FormatPosition(midpoint)} new12={FormatPosition(newTwelveDirection)} rightVec={FormatPosition(rightVector)} blueDot={blueDot:F3} purpleDot={purpleDot:F3} blueSide={FormatP4Side(blueSide)} purpleSide={FormatP4Side(purpleSide)} left={FormatP4Color(leftColor)} right={FormatP4Color(rightColor)}");

            var sides = new P4HalfSide[8];
            var colors = new P4HalfColor[8];

            for (var i = 0; i < 8; i++)
            {
                if (!TryResolveP4PlayerColor(states, i, out var color, out _, out _))
                {
                    colors[i] = P4HalfColor.Unknown;
                    sides[i] = P4HalfSide.Unknown;
                    continue;
                }

                colors[i] = color;
                sides[i] = SideForColor(color, leftColor, rightColor);
            }

            var marks = BuildP4CommandMarks(sides);

            lock (_p4Lock)
            {
                Array.Copy(sides, _p4ResolvedSideByPartyIndex, sides.Length);
                Array.Copy(colors, _p4ResolvedColorByPartyIndex, colors.Length);
                Array.Copy(marks, _p4CommandMarkByPartyIndex, marks.Length);
            }

            ApplyP4CommandMarks(accessory, marks);

            var myIndex = GetMyIndex(accessory);
            if (myIndex >= 0 && myIndex < sides.Length)
            {
                var mySide = sides[myIndex];
                if (mySide == P4HalfSide.Left || mySide == P4HalfSide.Right)
                {
                    var targetPosition = mySide == P4HalfSide.Right ? r1 : l1;
                    DrawGuide(accessory, $"{DrawPrefix}_P4_First_Guide", targetPosition, P4GuideDurationMs);
                    DrawStaticCircle(accessory, $"{DrawPrefix}_P4_First_Target", targetPosition, 0.25f, P4GuideDurationMs, GuideColor.V4);
                    DebugEcho(accessory, $"P4一运指路已画：my={PartyPriorityLabel(myIndex)} side={FormatP4Side(mySide)} color={FormatP4Color(colors[myIndex])} target={FormatPosition(targetPosition)} duration={P4GuideDurationMs}ms greenMove={EnableGreenMove}");
                    GreenMoveToPoint(targetPosition, accessory, "P4 first mechanic");
                }
                else
                {
                    DebugEcho(accessory, $"P4一运指路未画：my={PartyPriorityLabel(myIndex)} side={FormatP4Side(mySide)} color={FormatP4Color(colors[myIndex])}");
                }
            }
            else
            {
                DebugEcho(accessory, $"P4一运指路未画：myIndex={myIndex} partyCount={accessory.Data.PartyList.Count}");
            }

            DebugP4Resolve(accessory, states, sides, colors, marks, fourthParam, blueSide, leftColor, rightColor, newTwelveDirection, l1, r1);
        }

        #endregion
    }
}
