using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
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
   [ScriptType(name: "(妖星乱舞绝境战)P2 2222指路&自动移动", territorys: [1363], guid: "4c8f82b2-ab7f-45dc-bff1-ffce01bc67c8", version: "0.0.4.16", author: "RyougiMio", note: "电！\n指挥模式：每轮前4.5秒不显示头标。\n每轮后4.5秒显示本轮攻击1~4、禁止1~2、锁链1~2。\n攻击1234是踩塔的4人从左往右顺，禁止12是左侧的2闲人，锁链12是右侧的的2闲人 禁止1锁链1是靠近boss的 禁止2锁链2是远离boss的。\n!!!!!!!!自动移动依赖于PromeRotation!!!!!!!!")]
    public class ScriptDraft
    {
        #region Settings

        [UserSetting("-----全局设置-----")]
        public bool _____Global_Settings_____ { get; set; } = true;

        [UserSetting("是否开启屏幕文字提示")]
        public bool EnableText { get; set; } = true;

        [UserSetting("是否开启TTS语音提示")]
        public bool EnableTTS { get; set; } = true;

        [UserSetting("指挥模式（开启后允许脚本给全员上头标）")]
        public bool EnableCommandMode { get; set; } = false;

        [UserSetting("指挥模式头标显示秒数（0-9）")]
        public float CommandMarkDisplaySeconds { get; set; } = 4.5f;

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

        [UserSetting("P2闲人处理机制时的左右站位")]
        public P2TowerStrategy P2TowerStrategySetting { get; set; } = P2TowerStrategy.扇左钢右;
        public enum P2TowerStrategy
        {
            扇左钢右,
            TN左D右_BBY闲固,
        }

        [UserSetting("P2偶数轮闲人组站位")]
        public EvenGroup2PriorityMode EvenGroup2PrioritySetting { get; set; } = EvenGroup2PriorityMode.近南远北;
        public enum EvenGroup2PriorityMode
        {
            近南远北,
            近北远南,
        }

        [UserSetting("P2第八轮结束后集合位置")]
        public P2LastRoundOuterEdgeMode P2LastRoundOuterEdgeSetting { get; set; } = P2LastRoundOuterEdgeMode.塔中间;
        public enum P2LastRoundOuterEdgeMode
        {
            塔中间,
            A点,
        }

        [UserSetting("P2第二轮按刷新头标重新分组")]
        public bool RegroupOnRound2TargetIconRefresh { get; set; } = false;

        [UserSetting("Developer mode")]
        public bool DeveloperMode { get; set; } = false;

        [UserSetting("开发模式：详细Debug日志")]
        public bool VerboseDebugLog { get; set; } = false;

        [UserSetting("开发模式：记录读条ActionId")]
        public bool LogStartCastingActionId { get; set; } = false;

        [UserSetting("常用危险色")]
        public ScriptColor DangerColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 0.0f, 0.0f, 0.01f) };

        [UserSetting("常用安全色")]
        public ScriptColor SafeColor { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 0.0f, 0.01f) };

        [UserSetting("指路/引导颜色")]
        public ScriptColor GuideColor { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

        [UserSetting("P1打法")]
        public Phase1Strategy P1StrategySetting { get; set; } = Phase1Strategy.Default;
        public enum Phase1Strategy
        {
            Default,
            Alternative,
        }

        #endregion

        #region State

        private enum Phase
        {
            Init,
            P1,
            P2,
            P3,
            P4,
            P5,
            P6,
            Done,
        }

        private enum MarkerAlias
        {
            None,
            OL1,
            OL2,
            OL3,
            OL4,
            OR1,
            OR2,
            OR3,
            OR4,
            EL1,
            EL2,
            EL3,
            ER1,
            ER2,
            ER3,
            EC1,
            EC2,
        }

        private const uint InvalidObjectId = 0xE0000000;
        private const int MaxLoggedActionIds = 256;
        private const string DrawPrefix = "KDYD";
        private const string DebugLogFileName = "KDYD_P2L_Debug.log";
        private const string GreenMovePrefix = "PromeRotation.GreenMove.";
        private const uint TargetIcon02CB = 0x02CB;
        private const uint TargetIcon02CC = 0x02CC;
        private const uint TargetIcon02CD = 0x02CD;
        private const uint BossOuterEdgeNorthActionId = 47826;
        private const uint BossOuterEdgeSouthActionId = 47827;
        private const uint BossOuterEdgeResolveNorthActionId = 47836;
        private const uint BossOuterEdgeResolveSouthActionId = 47837;
        private const int EnvBigCircleFirstIndex = 1;
        private const int EnvBigCircleLastIndex = 8;
        private const int EnvBigCircleGuideDuration = 60000;
        private const int EnvBigCircleTargetIconUpdateCount = 4;
        private const int EnvBigCircleLastRound = 8;
        private const int EnvBigCircleLastRoundStartDelayMs = 650;
        private const int EnvBigCircleLastRoundGuideDuration = 11000;
        private const int EnvBigCircleLastRoundResolveGuideDuration = 5000;
        private const int CommandMarkWindowMs = 9000;
        private const float DefaultCommandMarkDisplaySeconds = 4.5f;
        private const float EnvBigCircleDistance = 8.0f;
        private const float EnvBigCircleRadius = 4.0f;
        private const float EnvMarkerCircleRadius = 0.25f;
        private const float EnvOuterEdgeGuideDistance = 8.0f;
        private const float EnvLastRoundDefaultOe3Distance = 8.0f;
        private const float EnvLastRoundDefaultOe4Distance = 6.0f;
        private const float EnvOddLeftGreenOffset = 3.25f;
        private const float EnvOddLeftRedSixOffset = 5.0f;
        private const float EnvInnerMarkerOffset = EnvBigCircleRadius - EnvMarkerCircleRadius;
        private const float EnvOuterMarkerOffset = EnvBigCircleRadius + EnvMarkerCircleRadius;
        private const float EnvCenterCircleRadius = 6.0f;
        private const float EnvCenterOuterMarkerOffset = EnvCenterCircleRadius + EnvMarkerCircleRadius;
        private static readonly Vector3 ArenaCenter = new Vector3(100.0f, 0.0f, 100.0f);
        private static readonly Vector3 EnvLastRoundFixedOe5Position = new Vector3(100.0f, 0.0f, 92.0f);
        private static readonly Vector3 EnvLastRoundFixedOe6Position = new Vector3(100.0f, 0.0f, 106.0f);
        private static readonly Vector3 DefaultNorth = new Vector3(0.0f, 0.0f, -1.0f);
        private static readonly Vector4 SolidDangerRed = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        private static readonly Vector4 SolidSafeGreen = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
        private static readonly string[] PartyPriorityLabels = new[] { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
        private static readonly int[][] TargetIconGroupPairs = new[]
        {
            new[] { 0, 2 },
            new[] { 1, 3 },
            new[] { 4, 6 },
            new[] { 5, 7 },
        };
        private static readonly int[] FanLeftSteelRightGroup2PriorityPartyIndexes = new[] { 0, 1, 4, 5, 2, 3, 6, 7 };
        private static readonly MarkType[] MarkerVariableMarkTypes = new[]
        {
            MarkType.Attack1,
            MarkType.Attack2,
            MarkType.Attack3,
            MarkType.Attack4,
            MarkType.Stop1,
            MarkType.Stop2,
            MarkType.Bind1,
            MarkType.Bind2,
        };

        private ScriptAccessory _acc;
        private Phase _phase = Phase.Init;
        private int _generation;
        private long _lastMechanicAt;
        private readonly object _lock = new object();
        private readonly object _debugFileLock = new object();
        private string _debugLogFilePath;
        private readonly HashSet<uint> _seenCasts = new HashSet<uint>();
        private readonly HashSet<uint> _loggedActionIds = new HashSet<uint>();
        private object _greenMoveToPointSub;
        private MethodInfo _greenMoveToPointInvoke;
        private object _greenMoveClearQueueSub;
        private MethodInfo _greenMoveClearQueueInvoke;
        private object _greenMoveStopSub;
        private MethodInfo _greenMoveStopInvoke;
        private bool _warnedGreenMove;
        private FieldInfo _legacyMarkingControllerField;
        private MethodInfo _legacyDoMarkingByActorIdMethod;
        private bool _warnedLegacyCommandMarkClear;

        private readonly object _targetIconLock = new object();
        private readonly uint[] _lastTargetIconByPartyIndex = new uint[8];
        private readonly uint[] _initialGroup2TargetIconByPartyIndex = new uint[8];
        private readonly int[] _targetIconGroupByPartyIndex = new int[8];
        private readonly List<int> _targetIconGroup1 = new List<int>();
        private readonly List<int> _targetIconGroup2 = new List<int>();
        private readonly List<int> _activeTargetIconGroup1 = new List<int>();
        private readonly List<int> _activeTargetIconGroup2 = new List<int>();
        private readonly MarkType?[] _roundMarkByPartyIndex = new MarkType?[8];
        private readonly int[] _markerVariableOwnerByIndex = new int[8];
        private bool _targetIconGroupsResolved;
        private bool _activeTargetIconGroupsReady;
        private bool _round2TargetIconRegroupDone;

        private readonly object _collectLock = new object();
        private readonly List<uint> _tetherTargets = new List<uint>();
        private readonly List<(uint Id, Vector3 Position, float Rotation)> _objects = new List<(uint Id, Vector3 Position, float Rotation)>();

        private readonly object _envBigCircleLock = new object();
        private readonly List<int> _pendingEnvBigCircleIndexes = new List<int>();
        private readonly HashSet<int> _pendingEnvBigCircleTargetIconUpdates = new HashSet<int>();
        private readonly Vector3[] _envBigCircleRoundTwelveDirections = new Vector3[EnvBigCircleLastRound + 1];
        private readonly bool[] _envBigCircleRoundTwelveDirectionReady = new bool[EnvBigCircleLastRound + 1];
        private int _envBigCircleRound;
        private int _pendingEnvBigCircleRoundToStart;
        private int _pendingEnvBigCircleFirstIndexToStart;
        private int _pendingEnvBigCircleSecondIndexToStart;
        private int _roundCommandMarkGeneration;
        private readonly object _commandMarkTimerLock = new object();
        private Timer _commandMarkApplyTimer;
        private Timer _commandMarkClearTimer;
        private readonly object _bossCastLock = new object();
        private uint _lastBossOuterEdgeActionId;
        private readonly object _outerEdgeGuideLock = new object();
        private int _outerEdgeResolveGeneration;
        private bool _activeOuterEdgeGuide;
        private int _activeOuterEdgeGuideRound;
        private MarkerAlias _activeOuterEdgeGuideFinalAlias = MarkerAlias.None;
        private Vector3 _activeOuterEdgeGuideFinalPosition;
        private int _activeOuterEdgeGuideDuration;
        private bool _activeOuterEdgeGuideHasFinalPosition;
        private bool _activeOuterEdgeGuideUseLastRoundConfig;
        private P2LastRoundOuterEdgeMode _activeOuterEdgeGuideLastRoundMode = P2LastRoundOuterEdgeMode.塔中间;
        private Vector3 _activeOuterEdgeGuideTwelveDirection;

        private readonly object _buffLock = new object();
        private readonly int[] _buffByPartyIndex = new int[8];
        private int _buffCount;
        private readonly AutoResetEvent _buffReady = new AutoResetEvent(false);

        private sealed class EnvBigCircleRoundPlan
        {
            public int Round;
            public int FirstIndex;
            public int SecondIndex;
            public int LeftIndex;
            public int RightIndex;
            public Vector3 LeftCenter;
            public Vector3 RightCenter;
            public Vector3 NewTwelveDirection;
            public MarkType?[] RoundMarks;
        }

        #endregion

        #region Initialization

        public void Init(ScriptAccessory accessory)
        {
            _acc = accessory;
            _phase = Phase.Init;
            _generation++;
            if (DeveloperMode)
                StartDebugFileSession();
            ResetMechanicState();
            ResetTargetIconRecords();

            lock (_lock)
            {
                _seenCasts.Clear();
                _loggedActionIds.Clear();
            }
            _warnedGreenMove = false;

            accessory.Method.RemoveDraw(".*");
            CommandMarkClear(accessory);
            if (DeveloperMode)
                DebugEcho(accessory, $"Debug file => {_debugLogFilePath}");
            DebugEcho(accessory, "New Duty Draft initialized.");
        }

        private void ResetMechanic(ScriptAccessory accessory, bool removeDraw = true)
        {
            _generation++;
            ResetMechanicState();

            lock (_lock)
                _seenCasts.Clear();

            if (removeDraw)
                accessory.Method.RemoveDraw($"{DrawPrefix}_.*");
        }

        private void ResetMechanicState()
        {
            _lastMechanicAt = 0;
            ClearRoundMarkerVariables();

            lock (_collectLock)
            {
                _tetherTargets.Clear();
                _objects.Clear();
            }

            lock (_buffLock)
            {
                Array.Clear(_buffByPartyIndex, 0, _buffByPartyIndex.Length);
                _buffCount = 0;
                _buffReady.Reset();
            }

            lock (_envBigCircleLock)
            {
                _pendingEnvBigCircleIndexes.Clear();
                ClearPendingEnvBigCircleRoundStartLocked();
                Array.Clear(_envBigCircleRoundTwelveDirections, 0, _envBigCircleRoundTwelveDirections.Length);
                Array.Clear(_envBigCircleRoundTwelveDirectionReady, 0, _envBigCircleRoundTwelveDirectionReady.Length);
                _envBigCircleRound = 0;
            }

            lock (_bossCastLock)
                _lastBossOuterEdgeActionId = 0;

            _outerEdgeResolveGeneration = 0;
            ClearActiveOuterEdgeGuideState();

            Interlocked.Increment(ref _roundCommandMarkGeneration);
            CancelCommandMarkTimers();
        }

        private void ResetTargetIconRecords()
        {
            lock (_targetIconLock)
            {
                Array.Clear(_lastTargetIconByPartyIndex, 0, _lastTargetIconByPartyIndex.Length);
                Array.Clear(_initialGroup2TargetIconByPartyIndex, 0, _initialGroup2TargetIconByPartyIndex.Length);
                Array.Clear(_targetIconGroupByPartyIndex, 0, _targetIconGroupByPartyIndex.Length);
                _targetIconGroup1.Clear();
                _targetIconGroup2.Clear();
                _activeTargetIconGroup1.Clear();
                _activeTargetIconGroup2.Clear();
                ClearRoundMarkerVariables();
                _targetIconGroupsResolved = false;
                _activeTargetIconGroupsReady = false;
                _round2TargetIconRegroupDone = false;
            }
        }

        private void ClearRoundMarkerVariables()
        {
            Array.Clear(_roundMarkByPartyIndex, 0, _roundMarkByPartyIndex.Length);

            for (var i = 0; i < _markerVariableOwnerByIndex.Length; i++)
                _markerVariableOwnerByIndex[i] = -1;
        }

        private void ClearPendingEnvBigCircleRoundStartLocked()
        {
            _pendingEnvBigCircleTargetIconUpdates.Clear();
            _pendingEnvBigCircleRoundToStart = 0;
            _pendingEnvBigCircleFirstIndexToStart = 0;
            _pendingEnvBigCircleSecondIndexToStart = 0;
        }

        private bool TryStartCollectedEnvBigCircleRoundLocked(
            out int round,
            out int firstIndex,
            out int secondIndex,
            out int updateCount,
            out List<int> targetIconUpdatePartyIndexes)
        {
            round = _pendingEnvBigCircleRoundToStart > 0
                ? _pendingEnvBigCircleRoundToStart
                : _envBigCircleRound + 1;
            firstIndex = _pendingEnvBigCircleFirstIndexToStart;
            secondIndex = _pendingEnvBigCircleSecondIndexToStart;
            updateCount = _pendingEnvBigCircleTargetIconUpdates.Count;
            targetIconUpdatePartyIndexes = _pendingEnvBigCircleTargetIconUpdates.OrderBy(index => index).ToList();

            if (_pendingEnvBigCircleRoundToStart <= 0) return false;
            if (_pendingEnvBigCircleRoundToStart != EnvBigCircleLastRound && updateCount < EnvBigCircleTargetIconUpdateCount) return false;

            _envBigCircleRound = _pendingEnvBigCircleRoundToStart;
            ClearPendingEnvBigCircleRoundStartLocked();
            return true;
        }

        private void StoreEnvBigCircleRoundTwelveDirection(int round, Vector3 newTwelveDirection)
        {
            if (round < 0 || round >= _envBigCircleRoundTwelveDirections.Length) return;

            lock (_envBigCircleLock)
            {
                _envBigCircleRoundTwelveDirections[round] = newTwelveDirection;
                _envBigCircleRoundTwelveDirectionReady[round] = true;
            }
        }

        private bool TryGetEnvBigCircleRoundTwelveDirection(int round, out Vector3 newTwelveDirection)
        {
            newTwelveDirection = default;
            if (round < 0 || round >= _envBigCircleRoundTwelveDirections.Length) return false;

            lock (_envBigCircleLock)
            {
                if (!_envBigCircleRoundTwelveDirectionReady[round])
                    return false;

                newTwelveDirection = _envBigCircleRoundTwelveDirections[round];
                return true;
            }
        }

        private bool TryCollectTargetIconAndStartEnvBigCircleRound(
            int partyIndex,
            out int round,
            out int firstIndex,
            out int secondIndex,
            out int updateCount,
            out List<int> targetIconUpdatePartyIndexes)
        {
            round = 0;
            firstIndex = 0;
            secondIndex = 0;
            updateCount = 0;
            targetIconUpdatePartyIndexes = new List<int>();

            if (partyIndex < 0) return false;

            lock (_envBigCircleLock)
            {
                if (_envBigCircleRound <= 0) return false;

                _pendingEnvBigCircleTargetIconUpdates.Add(partyIndex);
                return TryStartCollectedEnvBigCircleRoundLocked(out round, out firstIndex, out secondIndex, out updateCount, out targetIconUpdatePartyIndexes);
            }
        }

        private void SetPhase(ScriptAccessory accessory, Phase phase)
        {
            if (_phase == phase) return;

            _phase = phase;
            ResetMechanic(accessory);
            DebugEcho(accessory, $"Phase => {phase}");
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

        private string DefaultDebugLogPath()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(root))
                root = AppContext.BaseDirectory;

            return Path.Combine(root, "KodakkuAssist", DebugLogFileName);
        }

        private void EnsureDebugLogPath()
        {
            if (!string.IsNullOrWhiteSpace(_debugLogFilePath)) return;
            _debugLogFilePath = DefaultDebugLogPath();
        }

        private void AppendDebugFileLine(string message)
        {
            if (!DeveloperMode) return;

            try
            {
                EnsureDebugLogPath();
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{DrawPrefix}] {message}{Environment.NewLine}";

                lock (_debugFileLock)
                {
                    var directory = Path.GetDirectoryName(_debugLogFilePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);

                    File.AppendAllText(_debugLogFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
                // File logging must never break mechanic handling.
            }
        }

        private void StartDebugFileSession()
        {
            if (!DeveloperMode) return;

            try
            {
                EnsureDebugLogPath();
                var line = $"{Environment.NewLine}===== {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} New Script Session ====={Environment.NewLine}";

                lock (_debugFileLock)
                {
                    var directory = Path.GetDirectoryName(_debugLogFilePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);

                    File.AppendAllText(_debugLogFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
                // Keep normal debug output alive even if the filesystem is unavailable.
            }
        }

        private void DebugEcho(ScriptAccessory accessory, string message)
        {
            AppendDebugFileLine(message);
            accessory.Log.Debug($"[{DrawPrefix}] {message}");
            if (DeveloperMode)
                accessory.Method.SendChat($"/e [{DrawPrefix}] {message}");
        }

        private void DebugTrace(ScriptAccessory accessory, string message)
        {
            if (VerboseDebugLog)
                DebugEcho(accessory, message);
        }

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

                DebugTrace(accessory, $"GreenMove MoveToPoint {reason}: ({target.X:F2}, {target.Y:F2}, {target.Z:F2})");
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
                DebugTrace(accessory, $"GreenMove Stop/Clear {reason}");
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
            accessory.Log.Debug($"GreenMove IPC failed: {ex}");
            AppendDebugFileLine($"GreenMove IPC failed: {ex.Message}");
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
            if (sub == null || invoke == null)
            {
                sub = CreateSubscriber<T1, float, float, bool, int, bool, object>(GreenMovePrefix + name);
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
            if (method == null) throw new MissingMethodException("GetIpcSubscriber<TRet> not found.");

            return method.MakeGenericMethod(typeof(TRet)).Invoke(pluginInterface, new object[] { fullName })
                ?? throw new InvalidOperationException($"{fullName} subscriber is null.");
        }

        private static object CreateSubscriber<T1, T2, T3, T4, T5, T6, TRet>(string fullName)
        {
            var pluginInterface = GetKodakkuPluginInterface();
            var method = pluginInterface.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "GetIpcSubscriber" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 7);
            if (method == null) throw new MissingMethodException("GetIpcSubscriber<T1,T2,T3,T4,T5,T6,TRet> not found.");

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

        private void CancelCommandMarkTimers()
        {
            lock (_commandMarkTimerLock)
            {
                _commandMarkApplyTimer?.Dispose();
                _commandMarkApplyTimer = null;
                _commandMarkClearTimer?.Dispose();
                _commandMarkClearTimer = null;
            }
        }

        private void CancelCommandMarkApplyTimer()
        {
            lock (_commandMarkTimerLock)
            {
                _commandMarkApplyTimer?.Dispose();
                _commandMarkApplyTimer = null;
            }
        }

        private void CancelCommandMarkClearTimer()
        {
            lock (_commandMarkTimerLock)
            {
                _commandMarkClearTimer?.Dispose();
                _commandMarkClearTimer = null;
            }
        }

        private void ClearActiveOuterEdgeGuideState()
        {
            lock (_outerEdgeGuideLock)
            {
                _activeOuterEdgeGuide = false;
                _activeOuterEdgeGuideRound = 0;
                _activeOuterEdgeGuideFinalAlias = MarkerAlias.None;
                _activeOuterEdgeGuideFinalPosition = default;
                _activeOuterEdgeGuideDuration = 0;
                _activeOuterEdgeGuideHasFinalPosition = false;
                _activeOuterEdgeGuideUseLastRoundConfig = false;
                _activeOuterEdgeGuideLastRoundMode = P2LastRoundOuterEdgeMode.塔中间;
                _activeOuterEdgeGuideTwelveDirection = default;
            }
        }

        private static bool IsBossOuterEdgeAction(uint actionId)
        {
            return actionId == BossOuterEdgeNorthActionId || actionId == BossOuterEdgeSouthActionId;
        }

        private static bool IsBossOuterEdgeResolveAction(uint actionId)
        {
            return actionId == BossOuterEdgeResolveNorthActionId || actionId == BossOuterEdgeResolveSouthActionId;
        }

        private uint GetLastBossOuterEdgeActionId()
        {
            lock (_bossCastLock)
                return _lastBossOuterEdgeActionId;
        }

        private void SetLastBossOuterEdgeActionId(uint actionId)
        {
            lock (_bossCastLock)
                _lastBossOuterEdgeActionId = actionId;
        }

        private static string OuterEdgeAlias(uint actionId)
        {
            return actionId == BossOuterEdgeNorthActionId ? "OE1" : "OE2";
        }

        private static Vector3 OuterEdgePosition(Vector3 newTwelveDirection, uint actionId)
        {
            var direction = actionId == BossOuterEdgeNorthActionId
                ? newTwelveDirection
                : -newTwelveDirection;
            return ArenaCenter + direction * EnvOuterEdgeGuideDistance;
        }

        private static Vector3 LastRoundDefaultOuterEdgeInitialPosition(Vector3 newTwelveDirection)
        {
            return ArenaCenter - newTwelveDirection * EnvLastRoundDefaultOe3Distance;
        }

        private static Vector3 LastRoundDefaultOuterEdgeResolvePosition(Vector3 newTwelveDirection, uint bossOuterEdgeActionId)
        {
            return bossOuterEdgeActionId == BossOuterEdgeNorthActionId
                ? ArenaCenter + newTwelveDirection * EnvLastRoundDefaultOe4Distance
                : LastRoundDefaultOuterEdgeInitialPosition(newTwelveDirection);
        }

        private static Vector3 LastRoundFixedOuterEdgeResolvePosition(uint bossOuterEdgeActionId)
        {
            return bossOuterEdgeActionId == BossOuterEdgeNorthActionId
                ? EnvLastRoundFixedOe6Position
                : EnvLastRoundFixedOe5Position;
        }

        private void ReplaceCommandMarkApplyTimer(Timer timer)
        {
            lock (_commandMarkTimerLock)
            {
                _commandMarkApplyTimer?.Dispose();
                _commandMarkApplyTimer = timer;
            }
        }

        private void ReplaceCommandMarkClearTimer(Timer timer)
        {
            lock (_commandMarkTimerLock)
            {
                _commandMarkClearTimer?.Dispose();
                _commandMarkClearTimer = timer;
            }
        }

        private (int DelayMs, int DurationMs) GetCommandMarkTiming()
        {
            var displaySeconds = CommandMarkDisplaySeconds;
            if (float.IsNaN(displaySeconds) || float.IsInfinity(displaySeconds))
                displaySeconds = DefaultCommandMarkDisplaySeconds;

            if (displaySeconds < 0.0f)
                displaySeconds = 0.0f;
            else if (displaySeconds > CommandMarkWindowMs / 1000.0f)
                displaySeconds = CommandMarkWindowMs / 1000.0f;

            var durationMs = (int)Math.Round(displaySeconds * 1000.0f, MidpointRounding.AwayFromZero);
            durationMs = Math.Max(0, Math.Min(CommandMarkWindowMs, durationMs));
            return (CommandMarkWindowMs - durationMs, durationMs);
        }

        private void RunCommandMarkOnFrameworkThread(ScriptAccessory accessory, string context, System.Action action)
        {
            try
            {
                var task = accessory.Method.RunOnMainThreadAsync(action);
                task?.ContinueWith(t =>
                {
                    var ex = t.Exception?.GetBaseException();
                    if (ex != null)
                        DebugEcho(accessory, $"{context}: command mark framework action failed: {ex.Message}");
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                DebugEcho(accessory, $"{context}: command mark framework action schedule failed: {ex.Message}");
            }
        }

        private int BeginCommandMarkWait(ScriptAccessory accessory, string context, int assignedCount, int totalCount, int delayMs, int durationMs)
        {
            var generation = Interlocked.Increment(ref _roundCommandMarkGeneration);
            CancelCommandMarkApplyTimer();
            DebugTrace(accessory, $"{context}: command mark wait assigned={assignedCount}/{totalCount} delay={delayMs}ms duration={durationMs}ms generation={generation}");
            return generation;
        }

        private void ScheduleCommandMarkClear(ScriptAccessory accessory, string context, int generation, int durationMs)
        {
            if (durationMs <= 0) return;

            var mechanicGeneration = _generation;
            Timer timer = null;
            timer = new Timer(_ =>
            {
                try
                {
                    if (mechanicGeneration != _generation)
                    {
                        DebugTrace(accessory, $"{context}: command mark clear skipped, mechanic generation {mechanicGeneration}->{_generation}");
                        return;
                    }

                    RunCommandMarkOnFrameworkThread(accessory, context, () =>
                    {
                        if (mechanicGeneration != _generation)
                        {
                            DebugTrace(accessory, $"{context}: command mark clear skipped on framework thread, mechanic generation {mechanicGeneration}->{_generation}");
                            return;
                        }

                        DebugTrace(accessory, $"{context}: command mark duration expired, clear generation={generation}");
                        CommandMarkClearCore(accessory, context);
                    });
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"{context}: command mark duration clear schedule failed: {ex.Message}");
                }
                finally
                {
                    lock (_commandMarkTimerLock)
                    {
                        if (ReferenceEquals(_commandMarkClearTimer, timer))
                            _commandMarkClearTimer = null;
                    }

                    timer?.Dispose();
                }
            }, null, durationMs, Timeout.Infinite);

            ReplaceCommandMarkClearTimer(timer);
        }

        private bool TryLegacyCommandMarkClear(ScriptAccessory accessory, string context)
        {
            try
            {
                if (_legacyMarkingControllerField == null || _legacyDoMarkingByActorIdMethod == null)
                {
                    var markType = typeof(ScriptAccessory).Assembly.GetType("KodakkuAssist.Module.GameOperate.Mark")
                        ?? throw new TypeLoadException("KodakkuAssist.Module.GameOperate.Mark not found.");

                    _legacyMarkingControllerField = markType.GetField("MarkingController", BindingFlags.Static | BindingFlags.NonPublic)
                        ?? throw new MissingFieldException("KodakkuAssist.Module.GameOperate.Mark.MarkingController not found.");
                    _legacyDoMarkingByActorIdMethod = markType.GetMethod("DoMarkingByActorID", BindingFlags.Static | BindingFlags.NonPublic)
                        ?? throw new MissingMethodException("KodakkuAssist.Module.GameOperate.Mark.DoMarkingByActorID not found.");
                }

                var controller = (IntPtr)_legacyMarkingControllerField.GetValue(null);
                if (controller == IntPtr.Zero)
                    return false;

                var clearedCount = 0;
                for (var i = 0; i < 17; i++)
                {
                    var actorId = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(IntPtr.Add(controller, 16 + i * 8));
                    if (actorId == 0 || actorId == InvalidObjectId)
                        continue;

                    _legacyDoMarkingByActorIdMethod.Invoke(null, new object[] { actorId, (MarkType)(i + 1), false });
                    clearedCount++;
                }

                DebugTrace(accessory, $"{context}: legacy command mark clear queued {clearedCount} current marks.");
                return true;
            }
            catch (Exception ex)
            {
                if (!_warnedLegacyCommandMarkClear)
                {
                    _warnedLegacyCommandMarkClear = true;
                    DebugEcho(accessory, $"{context}: legacy command mark clear failed: {ex.Message}");
                }

                return false;
            }
        }

        private void CommandMarkClearCore(ScriptAccessory accessory, string context)
        {
            if (!EnableCommandMode) return;
            TryLegacyCommandMarkClear(accessory, context);
        }

        private void CommandMarkClear(ScriptAccessory accessory, string context = "Command mark clear")
        {
            if (!EnableCommandMode) return;
            RunCommandMarkOnFrameworkThread(accessory, context, () =>
            {
                CommandMarkClearCore(accessory, context);
            });
        }

        private void CommandMarkPartyMemberCore(ScriptAccessory accessory, int partyIndex, MarkType markType)
        {
            if (!EnableCommandMode) return;
            if (partyIndex < 0 || partyIndex >= accessory.Data.PartyList.Count) return;

            accessory.Method.Mark(accessory.Data.PartyList[partyIndex], markType);
        }

        private void CommandMarkRoundMarkersCore(ScriptAccessory accessory, int round, IReadOnlyList<MarkType?> roundMarks)
        {
            if (!EnableCommandMode) return;

            DebugTrace(accessory, $"Env big circle round {round}: command mark apply start assigned={roundMarks.Count(mark => mark.HasValue)}/{roundMarks.Count}");

            for (var i = 0; i < roundMarks.Count; i++)
            {
                var objectId = i >= 0 && i < accessory.Data.PartyList.Count
                    ? accessory.Data.PartyList[i]
                    : 0;

                if (!roundMarks[i].HasValue)
                {
                    DebugTrace(accessory, $"Env big circle round {round}: command mark skip {PartyPriorityLabel(i)} no mark {FormatObjectId(objectId)}");
                    continue;
                }

                if (i < 0 || i >= accessory.Data.PartyList.Count)
                {
                    DebugTrace(accessory, $"Env big circle round {round}: command mark skip {PartyPriorityLabel(i)} no party object");
                    continue;
                }

                DebugTrace(accessory, $"Env big circle round {round}: command mark {PartyPriorityLabel(i)}={roundMarks[i].Value} {FormatObjectId(objectId)}");
                CommandMarkPartyMemberCore(accessory, i, roundMarks[i].Value);
            }

            DebugTrace(accessory, $"Env big circle round {round}: command mark apply done");
        }

        private void CommandMarkRoundMarkersForDuration(ScriptAccessory accessory, int round, IReadOnlyList<MarkType?> roundMarks)
        {
            try
            {
                if (!EnableCommandMode)
                {
                    DebugTrace(accessory, $"Env big circle round {round}: command mark skipped, command mode disabled");
                    return;
                }

                var roundMarksSnapshot = roundMarks.ToArray();
                var context = $"Env big circle round {round}";
                var (delayMs, durationMs) = GetCommandMarkTiming();
                var generation = BeginCommandMarkWait(accessory, context, roundMarksSnapshot.Count(mark => mark.HasValue), roundMarksSnapshot.Length, delayMs, durationMs);
                if (durationMs <= 0)
                {
                    DebugTrace(accessory, $"{context}: command mark display duration is 0ms, apply skipped generation={generation}");
                    return;
                }

                Timer timer = null;
                timer = new Timer(_ =>
                {
                    try
                    {
                        if (!EnableCommandMode)
                        {
                            DebugTrace(accessory, $"{context}: command mark apply skipped, command mode disabled");
                            return;
                        }

                        if (generation != Volatile.Read(ref _roundCommandMarkGeneration))
                        {
                            DebugTrace(accessory, $"{context}: command mark apply skipped, generation {generation}->{Volatile.Read(ref _roundCommandMarkGeneration)}");
                            return;
                        }

                        DebugTrace(accessory, $"{context}: command mark wait done, apply duration={durationMs}ms generation={generation}");
                        RunCommandMarkOnFrameworkThread(accessory, context, () =>
                        {
                            if (!EnableCommandMode)
                            {
                                DebugTrace(accessory, $"{context}: command mark apply skipped on framework thread, command mode disabled");
                                return;
                            }

                            if (generation != Volatile.Read(ref _roundCommandMarkGeneration))
                            {
                                DebugTrace(accessory, $"{context}: command mark apply skipped on framework thread, generation {generation}->{Volatile.Read(ref _roundCommandMarkGeneration)}");
                                return;
                            }

                            CancelCommandMarkClearTimer();
                            CommandMarkRoundMarkersCore(accessory, round, roundMarksSnapshot);
                            ScheduleCommandMarkClear(accessory, context, generation, durationMs);
                        });
                    }
                    catch (Exception ex)
                    {
                        DebugEcho(accessory, $"{context}: command mark delayed apply failed: {ex.Message}");
                    }
                    finally
                    {
                        lock (_commandMarkTimerLock)
                        {
                            if (ReferenceEquals(_commandMarkApplyTimer, timer))
                                _commandMarkApplyTimer = null;
                        }

                        timer?.Dispose();
                    }
                }, null, delayMs, Timeout.Infinite);

                ReplaceCommandMarkApplyTimer(timer);
            }
            catch (Exception ex)
            {
                DebugEcho(accessory, $"Env big circle round {round}: command mark schedule failed: {ex.Message}");
            }
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

        private static bool TryParseObjectId(string text, out uint id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
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

        private static bool TryGetTargetIconId(Event @event, out uint iconId)
        {
            iconId = 0;
            var raw = NormalizeIconId(@event["Id"]);
            return uint.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out iconId);
        }

        private static bool IsTrackedTargetIcon(uint iconId)
        {
            return iconId == TargetIcon02CB
                || iconId == TargetIcon02CC
                || iconId == TargetIcon02CD;
        }

        private static string FormatTargetIconId(uint iconId)
        {
            return iconId == 0 ? "-" : iconId.ToString("X4", CultureInfo.InvariantCulture);
        }

        private static string PartyPriorityLabel(int partyIndex)
        {
            return partyIndex >= 0 && partyIndex < PartyPriorityLabels.Length
                ? PartyPriorityLabels[partyIndex]
                : $"P{partyIndex + 1}";
        }

        private static string PartyPriorityLabelOrNone(int partyIndex)
        {
            return partyIndex >= 0 ? PartyPriorityLabel(partyIndex) : "-";
        }

        private string FormatPartyMember(ScriptAccessory accessory, int partyIndex)
        {
            var objectId = partyIndex >= 0 && partyIndex < accessory.Data.PartyList.Count
                ? accessory.Data.PartyList[partyIndex]
                : 0;

            return $"{PartyPriorityLabel(partyIndex)}:{FormatTargetIconId(_lastTargetIconByPartyIndex[partyIndex])}:{FormatObjectId(objectId)}";
        }

        private string FormatTargetIconGroupText(ScriptAccessory accessory, IEnumerable<int> partyIndexes)
        {
            return string.Join(", ", partyIndexes.OrderBy(index => index).Select(index => FormatPartyMember(accessory, index)));
        }

        private void SetTargetIconGroupsLocked(IEnumerable<int> group1Indexes)
        {
            var group1Set = new HashSet<int>(group1Indexes.Where(index => index >= 0 && index < _lastTargetIconByPartyIndex.Length));

            _targetIconGroup1.Clear();
            _targetIconGroup2.Clear();
            Array.Clear(_targetIconGroupByPartyIndex, 0, _targetIconGroupByPartyIndex.Length);

            for (var i = 0; i < _lastTargetIconByPartyIndex.Length; i++)
            {
                if (group1Set.Contains(i))
                {
                    _targetIconGroupByPartyIndex[i] = 1;
                    _targetIconGroup1.Add(i);
                }
                else
                {
                    _targetIconGroupByPartyIndex[i] = 2;
                    _targetIconGroup2.Add(i);
                }
            }

            _targetIconGroupsResolved = true;
            Array.Clear(_initialGroup2TargetIconByPartyIndex, 0, _initialGroup2TargetIconByPartyIndex.Length);
            foreach (var index in _targetIconGroup2)
                _initialGroup2TargetIconByPartyIndex[index] = _lastTargetIconByPartyIndex[index];

            _activeTargetIconGroup1.Clear();
            _activeTargetIconGroup2.Clear();
            _activeTargetIconGroup1.AddRange(_targetIconGroup1);
            _activeTargetIconGroup2.AddRange(_targetIconGroup2);
            _activeTargetIconGroupsReady = true;
        }

        private bool TryResolveTargetIconGroupsLocked(ScriptAccessory accessory, out string group1Text, out string group2Text)
        {
            group1Text = string.Empty;
            group2Text = string.Empty;

            if (_targetIconGroupsResolved) return false;
            if (_lastTargetIconByPartyIndex.Any(iconId => iconId == 0)) return false;

            var group1Set = new HashSet<int>();
            foreach (var pair in TargetIconGroupPairs)
            {
                if (!pair.Any(index => _lastTargetIconByPartyIndex[index] == TargetIcon02CB))
                    continue;

                foreach (var index in pair)
                    group1Set.Add(index);
            }

            if (group1Set.Count != 4)
                return false;

            SetTargetIconGroupsLocked(group1Set);
            group1Text = FormatTargetIconGroupText(accessory, _targetIconGroup1);
            group2Text = FormatTargetIconGroupText(accessory, _targetIconGroup2);
            return true;
        }

        private void TryRegroupRound2TargetIconGroupsLocked(ScriptAccessory accessory, IReadOnlyList<int> refreshedPartyIndexes)
        {
            if (!RegroupOnRound2TargetIconRefresh || _round2TargetIconRegroupDone)
                return;

            if (refreshedPartyIndexes == null)
            {
                DebugEcho(accessory, "TargetIcon groups regroup round 2 skipped: refreshed target icon list is missing.");
                return;
            }

            var newGroup1 = refreshedPartyIndexes
                .Where(index => index >= 0 && index < _lastTargetIconByPartyIndex.Length)
                .Distinct()
                .OrderBy(index => index)
                .ToList();

            if (newGroup1.Count != 4)
            {
                DebugEcho(accessory, $"TargetIcon groups regroup round 2 skipped: refreshed={string.Join(", ", newGroup1.Select(PartyPriorityLabel))} count={newGroup1.Count}/4.");
                return;
            }

            SetTargetIconGroupsLocked(newGroup1);
            _round2TargetIconRegroupDone = true;
            DebugEcho(accessory, $"TargetIcon groups regrouped round 2: G1={FormatTargetIconGroupText(accessory, _targetIconGroup1)} | G2={FormatTargetIconGroupText(accessory, _targetIconGroup2)}");
        }

        private int FirstPartyIndexByIcon(IEnumerable<int> partyIndexes, uint iconId)
        {
            foreach (var index in partyIndexes.OrderBy(index => index))
            {
                if (index >= 0 && index < _lastTargetIconByPartyIndex.Length && _lastTargetIconByPartyIndex[index] == iconId)
                    return index;
            }

            return -1;
        }

        private List<int> PartyIndexesByIcon(IEnumerable<int> partyIndexes, uint iconId)
        {
            return partyIndexes
                .Where(index => index >= 0 && index < _lastTargetIconByPartyIndex.Length && _lastTargetIconByPartyIndex[index] == iconId)
                .OrderBy(index => index)
                .ToList();
        }

        private static int TargetIconPairPartner(int partyIndex)
        {
            foreach (var pair in TargetIconGroupPairs)
            {
                if (pair.Length < 2) continue;
                if (pair[0] == partyIndex) return pair[1];
                if (pair[1] == partyIndex) return pair[0];
            }

            return -1;
        }

        private bool TryGetPartyPosition(ScriptAccessory accessory, int partyIndex, out Vector3 position)
        {
            position = default;
            if (partyIndex < 0 || partyIndex >= accessory.Data.PartyList.Count) return false;

            var obj = accessory.Data.Objects.SearchById(accessory.Data.PartyList[partyIndex]);
            if (obj == null) return false;

            position = obj.Position;
            return true;
        }

        private float PartyClockDirectionScore(ScriptAccessory accessory, int partyIndex, Vector3 newTwelveDirection, float clockHour)
        {
            if (!TryGetPartyPosition(accessory, partyIndex, out var position))
                return float.NegativeInfinity;

            var playerDirection = NormalizeXZ(position - ArenaCenter, DefaultNorth);
            var clockDirection = ClockDirection(newTwelveDirection, clockHour);
            return DotXZ(playerDirection, clockDirection);
        }

        private List<int> PartyIndexesByClockCloseness(ScriptAccessory accessory, IEnumerable<int> partyIndexes, Vector3 newTwelveDirection, float clockHour)
        {
            return partyIndexes
                .OrderByDescending(index => PartyClockDirectionScore(accessory, index, newTwelveDirection, clockHour))
                .ThenBy(index => index)
                .ToList();
        }

        private string FormatClockCloseness(ScriptAccessory accessory, int partyIndex, Vector3 newTwelveDirection, float clockHour)
        {
            var score = PartyClockDirectionScore(accessory, partyIndex, newTwelveDirection, clockHour);
            var scoreText = float.IsNegativeInfinity(score)
                ? "missing"
                : score.ToString("F3", CultureInfo.InvariantCulture);
            return $"{PartyPriorityLabel(partyIndex)}={scoreText}";
        }

        private static int MarkerVariableIndex(MarkType markType)
        {
            for (var i = 0; i < MarkerVariableMarkTypes.Length; i++)
            {
                if (MarkerVariableMarkTypes[i] == markType)
                    return i;
            }

            return -1;
        }

        private void AssignMarkerVariable(MarkType markType, int partyIndex)
        {
            if (partyIndex < 0 || partyIndex >= _roundMarkByPartyIndex.Length) return;

            var markerIndex = MarkerVariableIndex(markType);
            if (markerIndex < 0 || markerIndex >= _markerVariableOwnerByIndex.Length) return;

            _markerVariableOwnerByIndex[markerIndex] = partyIndex;
            _roundMarkByPartyIndex[partyIndex] = markType;
        }

        private void AssignMarkerVariables(IEnumerable<int> partyIndexes, params MarkType[] markTypes)
        {
            AssignMarkerVariablesByPriority(partyIndexes, false, markTypes);
        }

        private void AssignMarkerVariablesByPriority(IEnumerable<int> partyIndexes, bool reversePriority, params MarkType[] markTypes)
        {
            var ordered = partyIndexes.OrderBy(index => index).ToList();
            if (reversePriority)
                ordered.Reverse();

            for (var i = 0; i < ordered.Count && i < markTypes.Length; i++)
                AssignMarkerVariable(markTypes[i], ordered[i]);
        }

        private bool UseReverseEvenGroup2Priority()
        {
            return EvenGroup2PrioritySetting == EvenGroup2PriorityMode.近北远南;
        }

        private void AssignTnDpsGroup2MarkerVariables(bool reversePriority)
        {
            AssignMarkerVariablesByPriority(_activeTargetIconGroup2.Where(IsTnPartyIndex), reversePriority, MarkType.Stop1, MarkType.Stop2);
            AssignMarkerVariablesByPriority(_activeTargetIconGroup2.Where(IsDpsPartyIndex), reversePriority, MarkType.Bind1, MarkType.Bind2);
        }

        private List<int> FanLeftSteelRightGroup2MembersByIcon(uint iconId, bool useInitialGroup2Icon, bool reversePriority = false)
        {
            var ordered = new List<int>();
            var priorityIndexes = reversePriority
                ? FanLeftSteelRightGroup2PriorityPartyIndexes.Reverse()
                : FanLeftSteelRightGroup2PriorityPartyIndexes;

            foreach (var index in priorityIndexes)
            {
                if (!_activeTargetIconGroup2.Contains(index)) continue;

                var storedIconId = useInitialGroup2Icon
                    ? _initialGroup2TargetIconByPartyIndex[index]
                    : _lastTargetIconByPartyIndex[index];
                if (storedIconId == iconId)
                    ordered.Add(index);
            }

            return ordered;
        }

        private bool IsTnPartyIndex(int partyIndex)
        {
            return partyIndex >= 0 && partyIndex <= 3;
        }

        private bool IsDpsPartyIndex(int partyIndex)
        {
            return partyIndex >= 4 && partyIndex <= 7;
        }

        private bool TryGetPreviousRoundNineOrder(ScriptAccessory accessory, int round, IEnumerable<int> partyIndexes, out List<int> ordered)
        {
            ordered = null;
            if (!TryGetEnvBigCircleRoundTwelveDirection(round - 1, out var previousRoundTwelveDirection))
            {
                DebugEcho(accessory, $"Env big circle round {round}: previous round twelve direction not ready for near 9 order");
                return false;
            }

            ordered = PartyIndexesByClockCloseness(accessory, partyIndexes, previousRoundTwelveDirection, 9.0f);
            return true;
        }

        private string FormatClockClosenessForPreviousRound(ScriptAccessory accessory, int round, int partyIndex, float clockHour)
        {
            if (!TryGetEnvBigCircleRoundTwelveDirection(round - 1, out var previousRoundTwelveDirection))
                return $"{PartyPriorityLabel(partyIndex)}=missing-direction";

            return FormatClockCloseness(accessory, partyIndex, previousRoundTwelveDirection, clockHour);
        }

        private void SwapActiveTargetIconGroupsLocked()
        {
            var oldGroup1 = _activeTargetIconGroup1.ToList();
            _activeTargetIconGroup1.Clear();
            _activeTargetIconGroup1.AddRange(_activeTargetIconGroup2);
            _activeTargetIconGroup2.Clear();
            _activeTargetIconGroup2.AddRange(oldGroup1);
        }

        private bool PrepareRoundMarkerVariablesLocked(ScriptAccessory accessory, int round, Vector3 newTwelveDirection, out MarkType?[] roundMarks)
        {
            roundMarks = null;
            if (!_activeTargetIconGroupsReady) return false;

            DebugTrace(accessory, $"Env big circle round {round}: round start");

            if (round == 4 || round == 8)
            {
                SwapActiveTargetIconGroupsLocked();
                DebugTrace(accessory, $"Env big circle round {round}: active target icon groups swapped");
            }

            DebugStoredTargetIconRecords(accessory, round);

            ClearRoundMarkerVariables();

            var ok = round % 2 == 1
                ? AssignOddRoundMarkerVariablesLocked(accessory, round, newTwelveDirection)
                : AssignEvenRoundMarkerVariablesLocked(accessory, round);

            roundMarks = new MarkType?[_roundMarkByPartyIndex.Length];
            Array.Copy(_roundMarkByPartyIndex, roundMarks, _roundMarkByPartyIndex.Length);
            return ok;
        }

        private bool AssignOddRoundMarkerVariablesLocked(ScriptAccessory accessory, int round, Vector3 newTwelveDirection)
        {
            var cd = FirstPartyIndexByIcon(_activeTargetIconGroup1, TargetIcon02CD);
            if (cd < 0) return false;

            var cc = FirstPartyIndexByIcon(_activeTargetIconGroup1, TargetIcon02CC);

            if (round == 1)
            {
                if (cc < 0) return false;

                var cbWithCdPartner = -1;
                var cbWithCcPartner = -1;
                var group1Cb = PartyIndexesByIcon(_activeTargetIconGroup1, TargetIcon02CB);
                foreach (var cb in group1Cb)
                {
                    var partner = TargetIconPairPartner(cb);
                    var partnerIcon = partner >= 0 && partner < _lastTargetIconByPartyIndex.Length
                        ? _lastTargetIconByPartyIndex[partner]
                        : 0;

                    DebugTrace(accessory, $"Env big circle round {round}: first G1 02CB pair check {PartyPriorityLabel(cb)} partner={PartyPriorityLabelOrNone(partner)}:{FormatTargetIconId(partnerIcon)}");

                    if (partnerIcon == TargetIcon02CD)
                    {
                        if (cbWithCdPartner >= 0)
                        {
                            DebugEcho(accessory, $"Env big circle round {round}: first G1 duplicate 02CB with 02CD partner");
                            return false;
                        }

                        cbWithCdPartner = cb;
                    }
                    else if (partnerIcon == TargetIcon02CC)
                    {
                        if (cbWithCcPartner >= 0)
                        {
                            DebugEcho(accessory, $"Env big circle round {round}: first G1 duplicate 02CB with 02CC partner");
                            return false;
                        }

                        cbWithCcPartner = cb;
                    }
                    else
                    {
                        DebugEcho(accessory, $"Env big circle round {round}: first G1 unexpected 02CB partner icon {PartyPriorityLabel(cb)} partner={PartyPriorityLabelOrNone(partner)}:{FormatTargetIconId(partnerIcon)}");
                    }
                }

                if (cbWithCdPartner < 0 || cbWithCcPartner < 0)
                {
                    DebugEcho(accessory, $"Env big circle round {round}: first G1 02CB pair rule incomplete Attack1={PartyPriorityLabelOrNone(cbWithCdPartner)} Attack4={PartyPriorityLabelOrNone(cbWithCcPartner)}");
                    return false;
                }

                AssignMarkerVariable(MarkType.Attack1, cbWithCdPartner);
                AssignMarkerVariable(MarkType.Attack2, cd);
                AssignMarkerVariable(MarkType.Attack3, cc);
                AssignMarkerVariable(MarkType.Attack4, cbWithCcPartner);
            }
            else
            {
                if (!TryGetEnvBigCircleRoundTwelveDirection(round - 1, out var previousRoundTwelveDirection))
                {
                    DebugEcho(accessory, $"Env big circle round {round}: previous round twelve direction not ready for 02CB near 9 order");
                    return false;
                }

                var cbByNine = PartyIndexesByClockCloseness(
                    accessory,
                    PartyIndexesByIcon(_activeTargetIconGroup1, TargetIcon02CB),
                    previousRoundTwelveDirection,
                    9.0f);

                DebugTrace(accessory, $"Env big circle round {round}: odd G1 02CB previous round near 9 order: {string.Join(", ", cbByNine.Select(index => FormatClockCloseness(accessory, index, previousRoundTwelveDirection, 9.0f)))}");

                AssignMarkerVariable(MarkType.Attack1, cbByNine.Count > 0 ? cbByNine[0] : -1);
                AssignMarkerVariable(MarkType.Attack2, cd);
                AssignMarkerVariable(MarkType.Attack3, cc);
                AssignMarkerVariable(MarkType.Attack4, cbByNine.Count > 1 ? cbByNine[1] : -1);
            }

            if (P2TowerStrategySetting == P2TowerStrategy.TN左D右_BBY闲固)
            {
                AssignTnDpsGroup2MarkerVariables(false);
            }
            else
            {
                var stopOrder = FanLeftSteelRightGroup2MembersByIcon(TargetIcon02CD, false);
                var bindOrder = FanLeftSteelRightGroup2MembersByIcon(TargetIcon02CC, false);

                DebugTrace(accessory, $"Env big circle round {round}: odd fan-left G2 priority Stop={string.Join(", ", stopOrder.Select(PartyPriorityLabel))} Bind={string.Join(", ", bindOrder.Select(PartyPriorityLabel))}");
                AssignMarkerVariable(MarkType.Stop1, stopOrder.Count > 0 ? stopOrder[0] : -1);
                AssignMarkerVariable(MarkType.Stop2, stopOrder.Count > 1 ? stopOrder[1] : -1);
                AssignMarkerVariable(MarkType.Bind1, bindOrder.Count > 0 ? bindOrder[0] : -1);
                AssignMarkerVariable(MarkType.Bind2, bindOrder.Count > 1 ? bindOrder[1] : -1);
            }

            return true;
        }

        private bool AssignEvenRoundMarkerVariablesLocked(ScriptAccessory accessory, int round)
        {
            if (P2TowerStrategySetting == P2TowerStrategy.TN左D右_BBY闲固)
                return AssignEvenRoundMarkerVariablesTnLeftDRightLocked(accessory, round);

            return AssignEvenRoundMarkerVariablesFanLeftSteelRightLocked(accessory, round);
        }

        private bool AssignEvenRoundMarkerVariablesFanLeftSteelRightLocked(ScriptAccessory accessory, int round)
        {
            var group1Cd = PartyIndexesByIcon(_activeTargetIconGroup1, TargetIcon02CD);
            var group1Cc = PartyIndexesByIcon(_activeTargetIconGroup1, TargetIcon02CC);

            if (!TryGetPreviousRoundNineOrder(accessory, round, group1Cd, out var group1CdByNine))
                return false;
            if (!TryGetPreviousRoundNineOrder(accessory, round, group1Cc, out var group1CcByNine))
                return false;

            DebugTrace(accessory, $"Env big circle round {round}: even fan-left G1 02CD previous round near 9 order: {string.Join(", ", group1CdByNine.Select(index => FormatClockClosenessForPreviousRound(accessory, round, index, 9.0f)))}");
            DebugTrace(accessory, $"Env big circle round {round}: even fan-left G1 02CC previous round near 9 order: {string.Join(", ", group1CcByNine.Select(index => FormatClockClosenessForPreviousRound(accessory, round, index, 9.0f)))}");

            AssignMarkerVariable(MarkType.Attack1, group1CdByNine.Count > 0 ? group1CdByNine[0] : -1);
            AssignMarkerVariable(MarkType.Attack2, group1CdByNine.Count > 1 ? group1CdByNine[1] : -1);
            AssignMarkerVariable(MarkType.Attack3, group1CcByNine.Count > 0 ? group1CcByNine[0] : -1);
            AssignMarkerVariable(MarkType.Attack4, group1CcByNine.Count > 1 ? group1CcByNine[1] : -1);

            var useInitialGroup2Icon = round == EnvBigCircleLastRound;
            var reverseGroup2Priority = UseReverseEvenGroup2Priority();
            var stopOrder = FanLeftSteelRightGroup2MembersByIcon(TargetIcon02CD, useInitialGroup2Icon, reverseGroup2Priority);
            var bindOrder = FanLeftSteelRightGroup2MembersByIcon(TargetIcon02CC, useInitialGroup2Icon, reverseGroup2Priority);

            DebugTrace(accessory, $"Env big circle round {round}: even fan-left G2 priority Stop={string.Join(", ", stopOrder.Select(PartyPriorityLabel))} Bind={string.Join(", ", bindOrder.Select(PartyPriorityLabel))} mode={EvenGroup2PrioritySetting} iconSource={(useInitialGroup2Icon ? "initialG2" : "current")}");
            AssignMarkerVariable(MarkType.Stop1, stopOrder.Count > 0 ? stopOrder[0] : -1);
            AssignMarkerVariable(MarkType.Stop2, stopOrder.Count > 1 ? stopOrder[1] : -1);
            AssignMarkerVariable(MarkType.Bind1, bindOrder.Count > 0 ? bindOrder[0] : -1);
            AssignMarkerVariable(MarkType.Bind2, bindOrder.Count > 1 ? bindOrder[1] : -1);

            return true;
        }

        private bool AssignEvenRoundMarkerVariablesTnLeftDRightLocked(ScriptAccessory accessory, int round)
        {
            var group1Cd = PartyIndexesByIcon(_activeTargetIconGroup1, TargetIcon02CD);
            var group1Cc = PartyIndexesByIcon(_activeTargetIconGroup1, TargetIcon02CC);

            if (!TryGetPreviousRoundNineOrder(accessory, round, group1Cd, out var group1CdByNine))
                return false;
            if (!TryGetPreviousRoundNineOrder(accessory, round, group1Cc, out var group1CcByNine))
                return false;

            DebugTrace(accessory, $"Env big circle round {round}: even G1 02CD previous round near 9 order: {string.Join(", ", group1CdByNine.Select(index => FormatClockClosenessForPreviousRound(accessory, round, index, 9.0f)))}");
            DebugTrace(accessory, $"Env big circle round {round}: even G1 02CC previous round near 9 order: {string.Join(", ", group1CcByNine.Select(index => FormatClockClosenessForPreviousRound(accessory, round, index, 9.0f)))}");

            AssignMarkerVariable(MarkType.Attack1, group1CdByNine.Count > 0 ? group1CdByNine[0] : -1);
            AssignMarkerVariable(MarkType.Attack2, group1CdByNine.Count > 1 ? group1CdByNine[1] : -1);
            AssignMarkerVariable(MarkType.Attack3, group1CcByNine.Count > 0 ? group1CcByNine[0] : -1);
            AssignMarkerVariable(MarkType.Attack4, group1CcByNine.Count > 1 ? group1CcByNine[1] : -1);

            AssignTnDpsGroup2MarkerVariables(UseReverseEvenGroup2Priority());

            return true;
        }

        private uint GetLastTargetIconByPartyIndex(int partyIndex)
        {
            if (partyIndex < 0 || partyIndex >= _lastTargetIconByPartyIndex.Length) return 0;

            lock (_targetIconLock)
                return _lastTargetIconByPartyIndex[partyIndex];
        }

        private uint GetLastTargetIcon(ScriptAccessory accessory, uint objectId)
        {
            return GetLastTargetIconByPartyIndex(GetPlayerIndex(accessory, objectId));
        }

        private int GetTargetIconGroupByPartyIndex(int partyIndex)
        {
            if (partyIndex < 0 || partyIndex >= _targetIconGroupByPartyIndex.Length) return 0;

            lock (_targetIconLock)
                return _targetIconGroupByPartyIndex[partyIndex];
        }

        private int GetTargetIconGroup(ScriptAccessory accessory, uint objectId)
        {
            return GetTargetIconGroupByPartyIndex(GetPlayerIndex(accessory, objectId));
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
            return accessory.Data.PartyList.IndexOf(objectId);
        }

        private static int GetMyIndex(ScriptAccessory accessory)
        {
            return GetPlayerIndex(accessory, GetMyId(accessory));
        }

        private static bool IsMe(ScriptAccessory accessory, uint objectId)
        {
            return objectId == GetMyId(accessory);
        }

        private static string NormalizeIconId(string iconId)
        {
            if (string.IsNullOrWhiteSpace(iconId)) return string.Empty;

            iconId = iconId.Trim();
            if (iconId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                iconId = iconId[2..];

            return iconId.ToUpperInvariant().PadLeft(4, '0');
        }

        private static string FormatObjectId(uint objectId)
        {
            return objectId == 0 ? "-" : objectId.ToString("X8", CultureInfo.InvariantCulture);
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

        private static Vector3 RotateAround(Vector3 point, Vector3 center, float radians)
        {
            var x = point.X - center.X;
            var z = point.Z - center.Z;
            var sin = MathF.Sin(radians);
            var cos = MathF.Cos(radians);

            return new Vector3(
                center.X + x * cos + z * sin,
                point.Y,
                center.Z - x * sin + z * cos);
        }

        private static bool IsEnvBigCircleIndex(int index)
        {
            return index >= EnvBigCircleFirstIndex && index <= EnvBigCircleLastIndex;
        }

        private static Vector3 GetEnvBigCircleCenter(int index)
        {
            var angle = (index - EnvBigCircleFirstIndex) * MathF.PI / 4.0f;
            return ArenaCenter + new Vector3(
                MathF.Sin(angle) * EnvBigCircleDistance,
                0.0f,
                -MathF.Cos(angle) * EnvBigCircleDistance);
        }

        private static Vector3 NormalizeXZ(Vector3 vector, Vector3 fallback)
        {
            var length = MathF.Sqrt(vector.X * vector.X + vector.Z * vector.Z);
            if (length < 0.001f)
                return fallback;

            return new Vector3(vector.X / length, 0.0f, vector.Z / length);
        }

        private static Vector3 RotateClockwiseXZ(Vector3 vector, float radians)
        {
            var sin = MathF.Sin(radians);
            var cos = MathF.Cos(radians);
            return new Vector3(
                vector.X * cos - vector.Z * sin,
                0.0f,
                vector.Z * cos + vector.X * sin);
        }

        private static Vector3 ClockDirection(Vector3 newTwelveDirection, float clockHour)
        {
            return RotateClockwiseXZ(newTwelveDirection, clockHour * MathF.PI / 6.0f);
        }

        private static float DotXZ(Vector3 a, Vector3 b)
        {
            return a.X * b.X + a.Z * b.Z;
        }

        private static Vector3 MarkerCircleCenter(Vector3 origin, Vector3 newTwelveDirection, float clockHour, float offset)
        {
            return origin + ClockDirection(newTwelveDirection, clockHour) * offset;
        }

        private static void GetRoundDirections(
            Vector3 firstCenter,
            Vector3 secondCenter,
            out Vector3 newTwelveDirection,
            out Vector3 newRightDirection)
        {
            var midpoint = (firstCenter + secondCenter) / 2.0f;
            newTwelveDirection = NormalizeXZ(ArenaCenter - midpoint, DefaultNorth);
            newRightDirection = RotateClockwiseXZ(newTwelveDirection, MathF.PI / 2.0f);
        }

        private static void SortEnvBigCirclesByRoundSide(
            int firstIndex,
            int secondIndex,
            Vector3 newRightDirection,
            out int leftIndex,
            out Vector3 leftCenter,
            out int rightIndex,
            out Vector3 rightCenter)
        {
            var firstCenter = GetEnvBigCircleCenter(firstIndex);
            var secondCenter = GetEnvBigCircleCenter(secondIndex);
            var firstRight = DotXZ(firstCenter - ArenaCenter, newRightDirection);
            var secondRight = DotXZ(secondCenter - ArenaCenter, newRightDirection);

            if (firstRight <= secondRight)
            {
                leftIndex = firstIndex;
                leftCenter = firstCenter;
                rightIndex = secondIndex;
                rightCenter = secondCenter;
            }
            else
            {
                leftIndex = secondIndex;
                leftCenter = secondCenter;
                rightIndex = firstIndex;
                rightCenter = firstCenter;
            }
        }

        private Vector3 DrawEnvMarkerCircle(
            ScriptAccessory accessory,
            int round,
            string side,
            string colorName,
            string clockName,
            Vector3 origin,
            Vector3 newTwelveDirection,
            float clockHour,
            float offset,
            Vector4 color)
        {
            var position = MarkerCircleCenter(origin, newTwelveDirection, clockHour, offset);
            var duration = EnvBigCircleGuideDurationForRound(round);
            DrawCircle(
                accessory,
                $"{DrawPrefix}_EnvBigCircleRound_{round}_{side}_{colorName}_{clockName}",
                position,
                EnvMarkerCircleRadius,
                duration,
                0,
                color,
                ScaleMode.None);
            return position;
        }

        private void DrawEnvMarkerCircleWithGuide(
            ScriptAccessory accessory,
            int round,
            MarkerAlias alias,
            string colorName,
            string clockName,
            Vector3 origin,
            Vector3 newTwelveDirection,
            float clockHour,
            float offset,
            Vector4 color)
        {
            var position = DrawEnvMarkerCircle(
                accessory,
                round,
                alias.ToString(),
                colorName,
                clockName,
                origin,
                newTwelveDirection,
                clockHour,
                offset,
                color);

            DrawGuide(accessory, $"{DrawPrefix}_EnvBigCircleRound_{round}_Guide_{alias}", position, EnvBigCircleGuideDurationForRound(round));
        }

        private static int EnvBigCircleGuideDurationForRound(int round)
        {
            return round == EnvBigCircleLastRound
                ? EnvBigCircleLastRoundGuideDuration
                : EnvBigCircleGuideDuration;
        }

        private MarkerAlias MarkerAliasForRoundMark(MarkType markType, bool oddRound)
        {
            if (oddRound)
            {
                switch (markType)
                {
                    case MarkType.Attack1: return MarkerAlias.OL1;
                    case MarkType.Attack2: return MarkerAlias.OL2;
                    case MarkType.Attack3: return MarkerAlias.OR1;
                    case MarkType.Attack4: return MarkerAlias.OR2;
                    case MarkType.Stop1: return MarkerAlias.OL3;
                    case MarkType.Stop2: return MarkerAlias.OL4;
                    case MarkType.Bind1: return MarkerAlias.OR3;
                    case MarkType.Bind2: return MarkerAlias.OR4;
                    default: return MarkerAlias.None;
                }
            }

            switch (markType)
            {
                case MarkType.Attack1: return MarkerAlias.EL1;
                case MarkType.Attack2: return MarkerAlias.EL2;
                case MarkType.Attack3: return MarkerAlias.ER2;
                case MarkType.Attack4: return MarkerAlias.ER1;
                case MarkType.Stop1: return MarkerAlias.EL3;
                case MarkType.Stop2: return MarkerAlias.EC1;
                case MarkType.Bind1: return MarkerAlias.ER3;
                case MarkType.Bind2: return MarkerAlias.EC2;
                default: return MarkerAlias.None;
            }
        }

        private string FormatStoredTargetIconRecord(ScriptAccessory accessory, int partyIndex)
        {
            var objectId = partyIndex >= 0 && partyIndex < accessory.Data.PartyList.Count
                ? accessory.Data.PartyList[partyIndex]
                : 0;
            var group = _activeTargetIconGroup1.Contains(partyIndex)
                ? "G1"
                : _activeTargetIconGroup2.Contains(partyIndex)
                    ? "G2"
                    : "G-";

            return $"{PartyPriorityLabel(partyIndex)}={group}:{FormatTargetIconId(_lastTargetIconByPartyIndex[partyIndex])}:{FormatObjectId(objectId)}";
        }

        private void DebugStoredTargetIconRecords(ScriptAccessory accessory, int round)
        {
            var firstHalf = Enumerable.Range(0, 4)
                .Select(index => FormatStoredTargetIconRecord(accessory, index));
            var secondHalf = Enumerable.Range(4, 4)
                .Select(index => FormatStoredTargetIconRecord(accessory, index));

            DebugTrace(accessory, $"Env big circle round {round}: stored targeticons 1/2: {string.Join(", ", firstHalf)}");
            DebugTrace(accessory, $"Env big circle round {round}: stored targeticons 2/2: {string.Join(", ", secondHalf)}");
        }

        private string FormatRoundMarkerAssignment(int partyIndex, IReadOnlyList<MarkType?> roundMarks, bool oddRound)
        {
            if (partyIndex < 0 || partyIndex >= roundMarks.Count || !roundMarks[partyIndex].HasValue)
                return null;

            var mark = roundMarks[partyIndex].Value;
            var alias = MarkerAliasForRoundMark(mark, oddRound);
            return $"{PartyPriorityLabel(partyIndex)}={mark}({alias})";
        }

        private void DebugRoundMarkerAssignments(ScriptAccessory accessory, int round, IReadOnlyList<MarkType?> roundMarks)
        {
            var oddRound = round % 2 == 1;
            var assignments = Enumerable.Range(0, roundMarks.Count)
                .Select(index => FormatRoundMarkerAssignment(index, roundMarks, oddRound))
                .Where(text => !string.IsNullOrWhiteSpace(text));

            DebugEcho(accessory, $"Env big circle round {round}: headmarks {string.Join(", ", assignments)}");
        }

        private bool DrawEnvMarkerAlias(
            ScriptAccessory accessory,
            int round,
            MarkerAlias alias,
            int leftIndex,
            Vector3 leftCenter,
            int rightIndex,
            Vector3 rightCenter,
            Vector3 newTwelveDirection)
        {
            return DrawEnvMarkerAlias(
                accessory,
                round,
                alias,
                leftIndex,
                leftCenter,
                rightIndex,
                rightCenter,
                newTwelveDirection,
                true,
                out _);
        }

        private bool DrawEnvMarkerAlias(
            ScriptAccessory accessory,
            int round,
            MarkerAlias alias,
            int leftIndex,
            Vector3 leftCenter,
            int rightIndex,
            Vector3 rightCenter,
            Vector3 newTwelveDirection,
            bool drawGuide,
            out Vector3 position)
        {
            position = default;

            switch (alias)
            {
                case MarkerAlias.OL1:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "GreenOffset", "0900", leftCenter, newTwelveDirection, 9.0f, EnvOddLeftGreenOffset, SolidSafeGreen, drawGuide, out position);
                case MarkerAlias.OL2:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "GreenOffset", "0620", leftCenter, newTwelveDirection, 6.0f + 1.0f / 3.0f, EnvOddLeftGreenOffset, SolidSafeGreen, drawGuide, out position);
                case MarkerAlias.OL3:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "RedOuter", "0900", leftCenter, newTwelveDirection, 9.0f, EnvOuterMarkerOffset, SolidDangerRed, drawGuide, out position);
                case MarkerAlias.OL4:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "RedOffset", "0600", leftCenter, newTwelveDirection, 6.0f, EnvOddLeftRedSixOffset, SolidDangerRed, drawGuide, out position);
                case MarkerAlias.OR1:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "GreenInner", "0840", rightCenter, newTwelveDirection, 8.0f + 2.0f / 3.0f, EnvInnerMarkerOffset, SolidSafeGreen, drawGuide, out position);
                case MarkerAlias.OR2:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "GreenInner", "0300", rightCenter, newTwelveDirection, 3.0f, EnvInnerMarkerOffset, SolidSafeGreen, drawGuide, out position);
                case MarkerAlias.OR3:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "RedOuter", "0300", rightCenter, newTwelveDirection, 3.0f, EnvOuterMarkerOffset, SolidDangerRed, drawGuide, out position);
                case MarkerAlias.OR4:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "RedOuter", "0320", rightCenter, newTwelveDirection, 3.0f + 1.0f / 3.0f, EnvOuterMarkerOffset, SolidDangerRed, drawGuide, out position);
                case MarkerAlias.EL1:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "GreenInner", "0835", leftCenter, newTwelveDirection, 8.0f + 7.0f / 12.0f, 3.625f, SolidSafeGreen, drawGuide, out position);
                case MarkerAlias.EL2:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "GreenInner", "0730", leftCenter, newTwelveDirection, 7.5f, EnvInnerMarkerOffset, SolidSafeGreen, drawGuide, out position);
                case MarkerAlias.EL3:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "RedOuter", "1245", leftCenter, newTwelveDirection, 12.0f + 3.0f / 4.0f, EnvOuterMarkerOffset, SolidDangerRed, drawGuide, out position);
                case MarkerAlias.ER1:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "GreenInner", "0300", rightCenter, newTwelveDirection, 3.0f, EnvInnerMarkerOffset, SolidSafeGreen, drawGuide, out position);
                case MarkerAlias.ER2:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "GreenInner", "0730", rightCenter, newTwelveDirection, 7.5f, EnvInnerMarkerOffset, SolidSafeGreen, drawGuide, out position);
                case MarkerAlias.ER3:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "RedOuter", "1040", rightCenter, newTwelveDirection, 10.0f + 2.0f / 3.0f, EnvOuterMarkerOffset, SolidDangerRed, drawGuide, out position);
                case MarkerAlias.EC1:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "RedOuter", "1120", ArenaCenter, newTwelveDirection, 11.0f + 1.0f / 3.0f, EnvCenterOuterMarkerOffset, SolidDangerRed, drawGuide, out position);
                case MarkerAlias.EC2:
                    return DrawEnvMarkerAliasPoint(accessory, round, alias, "RedOuter", "0130", ArenaCenter, newTwelveDirection, 1.5f, EnvCenterOuterMarkerOffset, SolidDangerRed, drawGuide, out position);
                default:
                    return false;
            }
        }

        private bool DrawEnvMarkerAliasPoint(
            ScriptAccessory accessory,
            int round,
            MarkerAlias alias,
            string colorName,
            string clockName,
            Vector3 origin,
            Vector3 newTwelveDirection,
            float clockHour,
            float offset,
            Vector4 color,
            bool drawGuide,
            out Vector3 position)
        {
            position = DrawEnvMarkerCircle(
                accessory,
                round,
                alias.ToString(),
                colorName,
                clockName,
                origin,
                newTwelveDirection,
                clockHour,
                offset,
                color);

            if (drawGuide)
                DrawGuide(accessory, $"{DrawPrefix}_EnvBigCircleRound_{round}_Guide_{alias}", position, EnvBigCircleGuideDurationForRound(round));

            return true;
        }

        private void DrawOuterEdgeRelayGuide(
            ScriptAccessory accessory,
            int round,
            uint bossOuterEdgeActionId,
            Vector3 newTwelveDirection,
            Vector3 finalPosition,
            MarkerAlias finalAlias,
            int duration)
        {
            var oeAlias = OuterEdgeAlias(bossOuterEdgeActionId);
            var oePosition = OuterEdgePosition(newTwelveDirection, bossOuterEdgeActionId);
            DrawCircle(
                accessory,
                $"{DrawPrefix}_EnvBigCircleRound_{round}_OE_{oeAlias}_Circle",
                oePosition,
                EnvMarkerCircleRadius,
                duration,
                0,
                SolidSafeGreen,
                ScaleMode.None);
            DrawGuide(accessory, $"{DrawPrefix}_EnvBigCircleRound_{round}_OE_Guide_To_{oeAlias}", oePosition, duration);
            DrawInactiveGuideBetweenPositions(accessory, $"{DrawPrefix}_EnvBigCircleRound_{round}_OE_Guide_{oeAlias}_To_{finalAlias}", oePosition, finalPosition, duration);
            SaveActiveOuterEdgeGuide(round, finalAlias, finalPosition, duration, true);
            GreenMoveToPoint(oePosition, accessory, $"round {round} {oeAlias}");
            DebugTrace(accessory, $"Env big circle round {round}: outer edge relay {oeAlias}->{finalAlias}");
        }

        private void DrawOuterEdgeGuideOnly(
            ScriptAccessory accessory,
            int round,
            uint bossOuterEdgeActionId,
            Vector3 newTwelveDirection,
            int duration)
        {
            var oeAlias = OuterEdgeAlias(bossOuterEdgeActionId);
            var oePosition = OuterEdgePosition(newTwelveDirection, bossOuterEdgeActionId);
            DrawCircle(
                accessory,
                $"{DrawPrefix}_EnvBigCircleRound_{round}_OE_{oeAlias}_Circle",
                oePosition,
                EnvMarkerCircleRadius,
                duration,
                0,
                SolidSafeGreen,
                ScaleMode.None);
            DrawGuide(accessory, $"{DrawPrefix}_EnvBigCircleRound_{round}_OE_Guide_To_{oeAlias}", oePosition, duration);
            SaveActiveOuterEdgeGuide(round, MarkerAlias.None, default, duration, false);
            GreenMoveToPoint(oePosition, accessory, $"round {round} {oeAlias}");
            DebugTrace(accessory, $"Env big circle round {round}: outer edge guide {oeAlias}");
        }

        private void DrawLastRoundOuterEdgeInitialGuide(
            ScriptAccessory accessory,
            int round,
            Vector3 newTwelveDirection,
            P2LastRoundOuterEdgeMode mode,
            int duration)
        {
            var oeAlias = mode == P2LastRoundOuterEdgeMode.A点 ? "OE5" : "OE3";
            var oePosition = mode == P2LastRoundOuterEdgeMode.A点
                ? EnvLastRoundFixedOe5Position
                : LastRoundDefaultOuterEdgeInitialPosition(newTwelveDirection);

            DrawCircle(
                accessory,
                $"{DrawPrefix}_EnvBigCircleRound_{round}_OE_{oeAlias}_Circle",
                oePosition,
                EnvMarkerCircleRadius,
                duration,
                0,
                SolidSafeGreen,
                ScaleMode.None);
            DrawGuide(accessory, $"{DrawPrefix}_EnvBigCircleRound_{round}_OE_Guide_To_{oeAlias}", oePosition, duration);
            SaveActiveLastRoundOuterEdgeGuide(round, mode, newTwelveDirection, duration);
            GreenMoveToPoint(oePosition, accessory, $"round {round} {oeAlias}");
            DebugTrace(accessory, $"Env big circle round {round}: last round outer edge initial guide {oeAlias} mode={mode}");
        }

        private void DrawLastRoundOuterEdgeResolveGuide(
            ScriptAccessory accessory,
            int round,
            P2LastRoundOuterEdgeMode mode,
            Vector3 newTwelveDirection,
            uint bossOuterEdgeActionId)
        {
            var northOuterEdge = bossOuterEdgeActionId == BossOuterEdgeNorthActionId;
            var oeAlias = mode == P2LastRoundOuterEdgeMode.A点
                ? (northOuterEdge ? "OE6" : "OE5")
                : (northOuterEdge ? "OE4" : "OE3");
            var oePosition = mode == P2LastRoundOuterEdgeMode.A点
                ? LastRoundFixedOuterEdgeResolvePosition(bossOuterEdgeActionId)
                : LastRoundDefaultOuterEdgeResolvePosition(newTwelveDirection, bossOuterEdgeActionId);

            DrawCircle(
                accessory,
                $"{DrawPrefix}_EnvBigCircleRound_{round}_OEResolve_{oeAlias}_Circle",
                oePosition,
                EnvMarkerCircleRadius,
                EnvBigCircleLastRoundResolveGuideDuration,
                0,
                SolidSafeGreen,
                ScaleMode.None);
            DrawGuide(accessory, $"{DrawPrefix}_EnvBigCircleRound_{round}_OEResolve_Guide_To_{oeAlias}", oePosition, EnvBigCircleLastRoundResolveGuideDuration);
            GreenMoveToPoint(oePosition, accessory, $"round {round} {oeAlias} after {bossOuterEdgeActionId}");
            DebugTrace(accessory, $"Boss cast {bossOuterEdgeActionId}: last round outer edge resolve guide {oeAlias} mode={mode}");
        }

        private void SaveActiveOuterEdgeGuide(int round, MarkerAlias finalAlias, Vector3 finalPosition, int duration, bool hasFinalPosition)
        {
            lock (_outerEdgeGuideLock)
            {
                _activeOuterEdgeGuide = true;
                _activeOuterEdgeGuideRound = round;
                _activeOuterEdgeGuideFinalAlias = finalAlias;
                _activeOuterEdgeGuideFinalPosition = finalPosition;
                _activeOuterEdgeGuideDuration = duration;
                _activeOuterEdgeGuideHasFinalPosition = hasFinalPosition;
                _activeOuterEdgeGuideUseLastRoundConfig = false;
                _activeOuterEdgeGuideLastRoundMode = P2LastRoundOuterEdgeMode.塔中间;
                _activeOuterEdgeGuideTwelveDirection = default;
            }
        }

        private void SaveActiveLastRoundOuterEdgeGuide(int round, P2LastRoundOuterEdgeMode mode, Vector3 newTwelveDirection, int duration)
        {
            lock (_outerEdgeGuideLock)
            {
                _activeOuterEdgeGuide = true;
                _activeOuterEdgeGuideRound = round;
                _activeOuterEdgeGuideFinalAlias = MarkerAlias.None;
                _activeOuterEdgeGuideFinalPosition = default;
                _activeOuterEdgeGuideDuration = duration;
                _activeOuterEdgeGuideHasFinalPosition = false;
                _activeOuterEdgeGuideUseLastRoundConfig = true;
                _activeOuterEdgeGuideLastRoundMode = mode;
                _activeOuterEdgeGuideTwelveDirection = newTwelveDirection;
            }
        }

        private void ResolveActiveOuterEdgeGuide(ScriptAccessory accessory, uint actionId)
        {
            int round;
            MarkerAlias finalAlias;
            Vector3 finalPosition;
            int duration;
            bool hasFinalPosition;
            bool useLastRoundConfig;
            P2LastRoundOuterEdgeMode lastRoundMode;
            Vector3 lastRoundTwelveDirection;

            lock (_outerEdgeGuideLock)
            {
                if (!_activeOuterEdgeGuide)
                    return;

                round = _activeOuterEdgeGuideRound;
                finalAlias = _activeOuterEdgeGuideFinalAlias;
                finalPosition = _activeOuterEdgeGuideFinalPosition;
                duration = _activeOuterEdgeGuideDuration > 0 ? _activeOuterEdgeGuideDuration : EnvBigCircleGuideDuration;
                hasFinalPosition = _activeOuterEdgeGuideHasFinalPosition;
                useLastRoundConfig = _activeOuterEdgeGuideUseLastRoundConfig;
                lastRoundMode = _activeOuterEdgeGuideLastRoundMode;
                lastRoundTwelveDirection = _activeOuterEdgeGuideTwelveDirection;
                _activeOuterEdgeGuide = false;
                _activeOuterEdgeGuideRound = 0;
                _activeOuterEdgeGuideFinalAlias = MarkerAlias.None;
                _activeOuterEdgeGuideFinalPosition = default;
                _activeOuterEdgeGuideDuration = 0;
                _activeOuterEdgeGuideHasFinalPosition = false;
                _activeOuterEdgeGuideUseLastRoundConfig = false;
                _activeOuterEdgeGuideLastRoundMode = P2LastRoundOuterEdgeMode.塔中间;
                _activeOuterEdgeGuideTwelveDirection = default;
            }

            accessory.Method.RemoveDraw($"{DrawPrefix}_EnvBigCircleRound_{round}_OE_.*");
            if (useLastRoundConfig)
            {
                var bossOuterEdgeActionId = GetLastBossOuterEdgeActionId();
                if (IsBossOuterEdgeAction(bossOuterEdgeActionId))
                {
                    DrawLastRoundOuterEdgeResolveGuide(accessory, round, lastRoundMode, lastRoundTwelveDirection, bossOuterEdgeActionId);
                }
                else
                {
                    GreenMoveStopAndClear(accessory, $"after {actionId}");
                    DebugEcho(accessory, $"Boss cast {actionId}: last round outer edge resolve skipped, last boss cast={bossOuterEdgeActionId}");
                }

                return;
            }

            if (hasFinalPosition && finalAlias != MarkerAlias.None)
            {
                DrawGuide(accessory, $"{DrawPrefix}_EnvBigCircleRound_{round}_Guide_{finalAlias}", finalPosition, duration);
                GreenMoveToPoint(finalPosition, accessory, $"round {round} {finalAlias} after {actionId}");
                DebugTrace(accessory, $"Boss cast {actionId}: outer edge guide resolved, direct guide={finalAlias}");
            }
            else
            {
                GreenMoveStopAndClear(accessory, $"after {actionId}");
                DebugTrace(accessory, $"Boss cast {actionId}: outer edge guide resolved, no final guide");
            }
        }

        private void ScheduleLastRoundOuterEdgeGuide(ScriptAccessory accessory, int round, Vector3 newTwelveDirection)
        {
            var generation = _generation;
            var resolveGeneration = Volatile.Read(ref _outerEdgeResolveGeneration);
            var mode = P2LastRoundOuterEdgeSetting;
            SaveActiveLastRoundOuterEdgeGuide(round, mode, newTwelveDirection, EnvBigCircleGuideDuration);
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(EnvBigCircleLastRoundGuideDuration);
                    if (generation != _generation)
                        return;

                    if (resolveGeneration != Volatile.Read(ref _outerEdgeResolveGeneration))
                        return;

                    lock (_envBigCircleLock)
                    {
                        if (_envBigCircleRound != round)
                            return;
                    }

                    DrawLastRoundOuterEdgeInitialGuide(accessory, round, newTwelveDirection, mode, EnvBigCircleGuideDuration);
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"Env big circle round {round}: outer edge delayed guide failed: {ex.Message}");
                }
            });
        }

        private int LastRoundStartDelayMs()
        {
            return EnvBigCircleLastRoundStartDelayMs;
        }

        private EnvBigCircleRoundPlan PrepareEnvBigCircleRoundPlan(ScriptAccessory accessory, int round, int firstIndex, int secondIndex, IReadOnlyList<int> roundTargetIconRefreshPartyIndexes)
        {
            var firstCenter = GetEnvBigCircleCenter(firstIndex);
            var secondCenter = GetEnvBigCircleCenter(secondIndex);
            GetRoundDirections(firstCenter, secondCenter, out var newTwelveDirection, out var newRightDirection);
            StoreEnvBigCircleRoundTwelveDirection(round, newTwelveDirection);
            SortEnvBigCirclesByRoundSide(firstIndex, secondIndex, newRightDirection, out var leftIndex, out var leftCenter, out var rightIndex, out var rightCenter);

            MarkType?[] roundMarks;
            var markersReady = false;
            lock (_targetIconLock)
            {
                if (round == 2)
                    TryRegroupRound2TargetIconGroupsLocked(accessory, roundTargetIconRefreshPartyIndexes);

                markersReady = PrepareRoundMarkerVariablesLocked(accessory, round, newTwelveDirection, out roundMarks);
            }

            if (!markersReady || roundMarks == null)
            {
                DebugEcho(accessory, $"Env big circle round {round}: marker variables not ready.");
                return null;
            }

            DebugRoundMarkerAssignments(accessory, round, roundMarks);
            return new EnvBigCircleRoundPlan
            {
                Round = round,
                FirstIndex = firstIndex,
                SecondIndex = secondIndex,
                LeftIndex = leftIndex,
                RightIndex = rightIndex,
                LeftCenter = leftCenter,
                RightCenter = rightCenter,
                NewTwelveDirection = newTwelveDirection,
                RoundMarks = roundMarks,
            };
        }

        private void ExecuteEnvBigCircleRoundPlan(ScriptAccessory accessory, EnvBigCircleRoundPlan plan)
        {
            var round = plan.Round;
            ClearActiveOuterEdgeGuideState();

            if (round > 1)
                accessory.Method.RemoveDraw($"{DrawPrefix}_EnvBigCircleRound_{round - 1}_.*");

            var myIndex = GetMyIndex(accessory);
            var roundMarks = plan.RoundMarks;
            if (myIndex >= 0 && myIndex < roundMarks.Length && roundMarks[myIndex].HasValue)
            {
                var alias = MarkerAliasForRoundMark(roundMarks[myIndex].Value, round % 2 == 1);
                var bossOuterEdgeActionId = GetLastBossOuterEdgeActionId();
                var useOuterEdgeRelay = round > 1 && round % 2 == 1 && IsBossOuterEdgeAction(bossOuterEdgeActionId);
                if (DrawEnvMarkerAlias(accessory, round, alias, plan.LeftIndex, plan.LeftCenter, plan.RightIndex, plan.RightCenter, plan.NewTwelveDirection, !useOuterEdgeRelay, out var finalPosition))
                {
                    if (useOuterEdgeRelay)
                        DrawOuterEdgeRelayGuide(accessory, round, bossOuterEdgeActionId, plan.NewTwelveDirection, finalPosition, alias, EnvBigCircleGuideDurationForRound(round));
                    else
                        GreenMoveToPoint(finalPosition, accessory, $"round {round} {alias}");

                    DebugTrace(accessory, $"Env big circle round {round}: my marker={roundMarks[myIndex].Value} alias={alias}");
                }
            }
            else
            {
                DebugTrace(accessory, $"Env big circle round {round}: no marker for my index={myIndex}");
            }

            DebugTrace(accessory, $"Env big circle round {round}: indexes={plan.FirstIndex},{plan.SecondIndex} left={plan.LeftIndex} right={plan.RightIndex}");
            CommandMarkRoundMarkersForDuration(accessory, round, roundMarks);

            if (round == EnvBigCircleLastRound)
                ScheduleLastRoundOuterEdgeGuide(accessory, round, plan.NewTwelveDirection);
        }

        private void ScheduleDelayedEnvBigCircleRoundPlan(ScriptAccessory accessory, EnvBigCircleRoundPlan plan, int delayMs)
        {
            var generation = _generation;
            DebugTrace(accessory, $"Env big circle round {plan.Round}: delayed start {delayMs}ms");
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs);
                    if (generation != _generation)
                        return;

                    lock (_envBigCircleLock)
                    {
                        if (_envBigCircleRound != plan.Round)
                            return;
                    }

                    ExecuteEnvBigCircleRoundPlan(accessory, plan);
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"Env big circle round {plan.Round}: delayed start failed: {ex.Message}");
                }
            });
        }

        private void DrawEnvBigCircleRound(ScriptAccessory accessory, int round, int firstIndex, int secondIndex, IReadOnlyList<int> roundTargetIconRefreshPartyIndexes = null)
        {
            var plan = PrepareEnvBigCircleRoundPlan(accessory, round, firstIndex, secondIndex, roundTargetIconRefreshPartyIndexes);
            if (plan == null)
                return;

            var delayMs = round == EnvBigCircleLastRound ? LastRoundStartDelayMs() : 0;
            if (delayMs > 0)
                ScheduleDelayedEnvBigCircleRoundPlan(accessory, plan, delayMs);
            else
                ExecuteEnvBigCircleRoundPlan(accessory, plan);
        }

        private async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs, int intervalMs = 25)
        {
            var start = Environment.TickCount64;

            while (!condition())
            {
                if (Environment.TickCount64 - start >= timeoutMs)
                    return false;

                await Task.Delay(intervalMs);
            }

            return true;
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

        private void DrawDonut(ScriptAccessory accessory, string name, Vector3 position, float innerRadius, float outerRadius, int duration, int delay = 0, Vector4? color = null, ScaleMode scaleMode = ScaleMode.ByTime)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = position;
            dp.Scale = new Vector2(outerRadius);
            dp.InnerScale = new Vector2(innerRadius);
            dp.Radian = MathF.PI * 2.0f;
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = duration;
            dp.ScaleMode = scaleMode;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
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
            DrawGuideBetweenPositions(accessory, name, sourcePosition, targetPosition, duration, delay, accessory.Data.DefaultDangerColor);
        }

        #endregion

        #region Mechanisms

        [ScriptMethod(name: "OE读条记录与指路切换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47826|47827|47836|47837)$"], userControl: false)]
        public void StartCasting_OuterEdgeGuide(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;

            if (!TryGetActionId(@event, out var actionId)) return;

            if (IsBossOuterEdgeAction(actionId))
            {
                SetLastBossOuterEdgeActionId(actionId);
                DebugTrace(accessory, $"Boss outer edge cast recorded: {actionId}->{OuterEdgeAlias(actionId)}");
                return;
            }

            if (IsBossOuterEdgeResolveAction(actionId))
            {
                Interlocked.Increment(ref _outerEdgeResolveGeneration);
                ResolveActiveOuterEdgeGuide(accessory, actionId);
            }
        }

        [ScriptMethod(name: "璁板綍02CB-02CD鐐瑰悕", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(02CB|02CC|02CD|02cb|02cc|02cd)$"], userControl: false)]
        public void RecordTargetIcon02CBTo02CD(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;
            _lastMechanicAt = NowMs();

            if (!TryGetTargetId(@event, out var targetId)) return;
            if (!TryGetTargetIconId(@event, out var iconId)) return;
            if (!IsTrackedTargetIcon(iconId)) return;

            var index = GetPlayerIndex(accessory, targetId);
            if (index < 0 || index >= _lastTargetIconByPartyIndex.Length) return;

            string group1Text = null;
            string group2Text = null;
            var groupsResolvedNow = false;
            lock (_targetIconLock)
            {
                _lastTargetIconByPartyIndex[index] = iconId;
                groupsResolvedNow = TryResolveTargetIconGroupsLocked(accessory, out group1Text, out group2Text);
            }

            if (DeveloperMode)
                DebugTrace(accessory, $"TargetIcon P{index + 1}={FormatTargetIconId(iconId)} target={FormatObjectId(targetId)}");

            if (groupsResolvedNow)
            {
                DebugEcho(accessory, $"TargetIcon groups fixed: G1={group1Text} | G2={group2Text}");
                CommandMarkClear(accessory, "TargetIcon groups fixed");
            }

            var collectedReady = TryCollectTargetIconAndStartEnvBigCircleRound(
                index,
                out var collectedRound,
                out var collectedFirstIndex,
                out var collectedSecondIndex,
                out var collectedUpdateCount,
                out var collectedTargetIconUpdatePartyIndexes);

            if (collectedUpdateCount > 0 && !collectedReady && DeveloperMode)
                DebugTrace(accessory, $"Env big circle round {collectedRound}: target icons collected {collectedUpdateCount}/{EnvBigCircleTargetIconUpdateCount}");

            if (collectedReady)
            {
                if (collectedRound == EnvBigCircleLastRound && collectedUpdateCount < EnvBigCircleTargetIconUpdateCount)
                    DebugTrace(accessory, $"Env big circle round {collectedRound}: big circles ready, no target icon refresh expected {collectedUpdateCount}/{EnvBigCircleTargetIconUpdateCount}");
                else
                    DebugTrace(accessory, $"Env big circle round {collectedRound}: target icons and big circles ready {collectedUpdateCount}/{EnvBigCircleTargetIconUpdateCount}");

                DrawEnvBigCircleRound(accessory, collectedRound, collectedFirstIndex, collectedSecondIndex, collectedTargetIconUpdatePartyIndexes);
            }
        }

        [ScriptMethod(name: "EnvControl澶у湀杞", eventType: EventTypeEnum.EnvControl, eventCondition: ["Index:regex:^\\d+$"], userControl: false)]
        public void EnvControl_BigCircleRound(Event @event, ScriptAccessory accessory)
        {
            _acc = accessory;

            if (!int.TryParse(@event["Index"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) return;
            if (!int.TryParse(@event["Flag"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var flag)) return;

            var name = $"{DrawPrefix}_Env_{index}";
            if (flag == 0 || flag == 4 || flag == 8)
            {
                accessory.Method.RemoveDraw($"{name}.*");
                return;
            }

            if (!IsEnvBigCircleIndex(index) || flag != 2)
            {
                DebugTrace(accessory, $"EnvControl index={index} flag={flag}");
                return;
            }

            int round = 0;
            int firstIndex = 0;
            int secondIndex = 0;
            int collectedUpdateCount = 0;
            List<int> collectedTargetIconUpdatePartyIndexes = null;
            var startImmediately = false;
            var collectedReady = false;
            var waitForBothConditions = false;
            lock (_envBigCircleLock)
            {
                if (_envBigCircleRound > 0 && _pendingEnvBigCircleRoundToStart > 0)
                    return;

                if (_pendingEnvBigCircleIndexes.Contains(index))
                    return;

                _pendingEnvBigCircleIndexes.Add(index);
                if (_pendingEnvBigCircleIndexes.Count < 2)
                    return;

                firstIndex = _pendingEnvBigCircleIndexes[0];
                secondIndex = _pendingEnvBigCircleIndexes[1];
                _pendingEnvBigCircleIndexes.Clear();

                if (_envBigCircleRound == 0)
                {
                    round = 1;
                    _envBigCircleRound = round;
                    ClearPendingEnvBigCircleRoundStartLocked();
                    startImmediately = true;
                }
                else
                {
                    _pendingEnvBigCircleRoundToStart = _envBigCircleRound + 1;
                    _pendingEnvBigCircleFirstIndexToStart = firstIndex;
                    _pendingEnvBigCircleSecondIndexToStart = secondIndex;
                    collectedReady = TryStartCollectedEnvBigCircleRoundLocked(out round, out firstIndex, out secondIndex, out collectedUpdateCount, out collectedTargetIconUpdatePartyIndexes);
                    waitForBothConditions = !collectedReady;
                }
            }

            if (startImmediately)
            {
                DrawEnvBigCircleRound(accessory, round, firstIndex, secondIndex);
            }
            else if (collectedReady)
            {
                if (round == EnvBigCircleLastRound && collectedUpdateCount < EnvBigCircleTargetIconUpdateCount)
                    DebugTrace(accessory, $"Env big circle round {round}: big circles ready, no target icon refresh expected {collectedUpdateCount}/{EnvBigCircleTargetIconUpdateCount}");
                else
                    DebugTrace(accessory, $"Env big circle round {round}: target icons and big circles ready {collectedUpdateCount}/{EnvBigCircleTargetIconUpdateCount}");

                DrawEnvBigCircleRound(accessory, round, firstIndex, secondIndex, collectedTargetIconUpdatePartyIndexes);
            }
            else if (waitForBothConditions)
            {
                DebugTrace(accessory, $"Env big circle round {round}: indexes={firstIndex},{secondIndex} waiting target icons {collectedUpdateCount}/{EnvBigCircleTargetIconUpdateCount}");
            }

            DebugTrace(accessory, $"EnvControl index={index} flag={flag}");
        }

        #endregion
    }
}
