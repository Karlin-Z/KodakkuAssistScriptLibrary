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
    [ScriptType(
        name: "妖星乱舞绝境战P2 宝宝椅一套&自动移动 RyougiMio框架改装版",
        territorys: [1363],
        guid: "87e770a3-1a69-4278-8da4-3b07cc73bf07",
        version: "0.1.3.0",
        author: "llllinkz",
        note: "在RyougiMio佬的框架基础上修改而来，融合了灵视佬和洛神宝宝椅的逻辑。感谢各位大佬。\n"
            + "设置中可选宝宝椅打法：原版（分摊在两侧）/ 近战优化（分摊在前方）。\n"
            + "自动移动：使用需开启PromeRotation，移动前可设延迟 0~1000ms。\n"
            + "指挥模式：每轮后半段显示攻击1~4、禁止1~2、锁链1~2。\n"
            + "请勿与其它P2遗弃末世指路同时开启。")]
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

        [UserSetting("自动移动方式（GreenMove）")]
        public AutoMoveModeSetting AutoMoveMode { get; set; } = AutoMoveModeSetting.关闭;

        public enum AutoMoveModeSetting
        {
            关闭,
            GreenMove,
        }

        [UserSetting("自动移动前延迟ms（0-1000）")]
        public int AutoMoveDelayMs { get; set; } = 0;

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

        [UserSetting("遗弃末世奇数轮站位")]
        public Phase2Sub2Strats Phase2Sub2Strat { get; set; } = Phase2Sub2Strats.原版_分摊在两侧;

        public enum Phase2Sub2Strats
        {
            原版_分摊在两侧,
            近战优化_分摊在前方,
        }

        [UserSetting("偶数轮扇形辅助圆点")]
        public bool EnableEvenFanAuxiliaryCircles { get; set; } = true;

        [UserSetting("Developer mode")]
        public bool DeveloperMode { get; set; } = false;

        [UserSetting("开发模式：详细Debug日志")]
        public bool VerboseDebugLog { get; set; } = false;

        [UserSetting("指路/引导颜色")]
        public ScriptColor GuideColor { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

        [UserSetting("辅助线颜色")]
        public ScriptColor AuxiliaryColor { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

        #endregion

        #region Constants

        private const uint InvalidObjectId = 0xE0000000;
        private const string DrawPrefix = "KDYD";
        private const string DebugLogFileName = "KDYD_P2L_Debug.log";
        private const string GreenMovePrefix = "PromeRotation.GreenMove.";
        private const int CommonInterval = 2500;
        private const int MaximumDuration = 7200000;
        private const double CommonDeviation = 1.0;
        private const int CommandMarkWindowMs = 9000;
        private const float DefaultCommandMarkDisplaySeconds = 4.5f;
        private const int Round1GuideDuration = 10750;
        private const int AllThingsEndingWaitMs = 6375 + CommonInterval;

        private static readonly Vector3 ArenaCenter = new Vector3(100.0f, 0.0f, 100.0f);
        private static readonly Vector3 RawTowerPosition = new Vector3(100.0f, 0.0f, 92.0f);
        private static readonly string[] PartyPriorityLabels = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };

        // Cicero / 宝宝椅：正北塔基准坐标
        private static readonly Vector3 OddLeftStack = new Vector3(106.3f, 0, 95.3f);
        private static readonly Vector3 OddFan = new Vector3(105.66f, 0, 90.84f);
        private static readonly Vector3 OddTank = new Vector3(108.8f, 0, 97.5f);
        private static readonly Vector3 OddHealer = new Vector3(105.66f, 0, 88.34f);
        private static readonly Vector3 OddRightStack = new Vector3(91.81f, 0, 96.34f);
        private static readonly Vector3 OddSpread = new Vector3(97.8f, 0, 93.5f);
        private static readonly Vector3 OddMeleeDps = new Vector3(91.3f, 0, 97.66f);
        private static readonly Vector3 OddRangedDps = new Vector3(90.41f, 0, 96.55f);

        private static readonly Vector3 UptimeOddLeftStack = new Vector3(104.95f, 0, 95.05f);
        private static readonly Vector3 UptimeOddFan = new Vector3(108, 0, 92);
        private static readonly Vector3 UptimeOddTank = new Vector3(102.45f, 0, 97.55f);
        private static readonly Vector3 UptimeOddHealer = new Vector3(109, 0, 91);
        private static readonly Vector3 UptimeOddRightStack = new Vector3(94.34f, 0, 97.84f);
        private static readonly Vector3 UptimeOddSpread = new Vector3(91.87f, 0, 91.87f);
        private static readonly Vector3 UptimeOddMeleeDps = new Vector3(97.35f, 0, 97.75f);
        private static readonly Vector3 UptimeOddRangedDps = new Vector3(97.75f, 0, 97.35f);

        private static readonly Vector3 EvenLeftFan = new Vector3(109.51f, 0, 93.73f);
        private static readonly Vector3 EvenRightFan = new Vector3(108.3f, 0, 91.48f);
        private static readonly Vector3 EvenLeftSpread = new Vector3(95.29f, 0, 92.28f);
        private static readonly Vector3 EvenRightSpread = new Vector3(91.31f, 0, 96.78f);
        private static readonly Vector3 EvenTank = new Vector3(101.9f, 0, 103.5f);
        private static readonly Vector3 EvenHealer = new Vector3(103.2f, 0, 97.6f);
        private static readonly Vector3 EvenMeleeDps = new Vector3(96.03f, 0, 103.55f);
        private static readonly Vector3 EvenRangedDps = new Vector3(96.8f, 0, 97.6f);

        // index: 0 unused; 1..8 rounds. true = Group A does mechanic
        private static readonly bool[] IsGroupARound =
        {
            false, true, true, true, false, false, false, false, true
        };

        private enum IconType
        {
            Unknown,
            Fan,
            Spread,
            Stack,
        }

        #endregion

        #region State

        private ScriptAccessory _acc;
        private int _generation;
        private readonly object _debugFileLock = new object();
        private string _debugLogFilePath;
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
        private int _roundCommandMarkGeneration;
        private readonly object _commandMarkTimerLock = new object();
        private Timer _commandMarkApplyTimer;
        private Timer _commandMarkClearTimer;

        private readonly IconType[] _iconType = Enumerable.Range(0, 8).Select(_ => IconType.Unknown).ToArray();
        private volatile int _iconUpdateCounter;
        private readonly AutoResetEvent[] _roundSemaphore =
            Enumerable.Range(0, 7).Select(_ => new AutoResetEvent(false)).ToArray();
        private volatile int _pathOfLightCounter;
        private readonly object _pathOfLightLock = new object();
        private readonly List<int> _groupA = new List<int>();
        private volatile int _initialLeftStack = -1;
        private volatile int _initialRightStack = -1;
        private readonly List<int> _groupB = new List<int>();
        private volatile int _initialUpperFan = -1;
        private volatile int _initialUpperSpread = -1;
        private readonly int[] _discretizedTowerGap = Enumerable.Range(0, 9).Select(_ => -1).ToArray();
        private volatile int _towerCounter;
        private volatile int _discretizedPositionOfLastTower = -1;
        private readonly AutoResetEvent _towerGap1Semaphore = new AutoResetEvent(false);
        private readonly AutoResetEvent _towerGap2Semaphore = new AutoResetEvent(false);
        private readonly object _towerGapLock = new object();
        private volatile bool _isFuturesEnd;
        private volatile int _allThingsEndingCounter;
        private readonly object _allThingsEndingLock = new object();
        private readonly AutoResetEvent _allThingsEndingSemaphore = new AutoResetEvent(false);
        private volatile bool _forsakenActive;

        #endregion

        #region Initialization

        public void Init(ScriptAccessory accessory)
        {
            _acc = accessory;
            _generation++;
            if (DeveloperMode)
                StartDebugFileSession();

            ResetForsakenState();
            _warnedGreenMove = false;
            accessory.Method.RemoveDraw(".*");
            CommandMarkClear(accessory);
            if (DeveloperMode)
                DebugEcho(accessory, $"Debug file => {_debugLogFilePath}");
            DebugEcho(accessory, "P2 2222 (Cicero站位) initialized.");
        }

        private void ResetForsakenState()
        {
            for (var i = 0; i < _iconType.Length; i++)
                _iconType[i] = IconType.Unknown;
            _iconUpdateCounter = 0;
            for (var i = 0; i < _roundSemaphore.Length; i++)
                _roundSemaphore[i].Reset();
            _pathOfLightCounter = 0;
            _groupA.Clear();
            _groupB.Clear();
            _initialLeftStack = -1;
            _initialRightStack = -1;
            _initialUpperFan = -1;
            _initialUpperSpread = -1;
            for (var i = 0; i < _discretizedTowerGap.Length; i++)
                _discretizedTowerGap[i] = -1;
            _towerCounter = 0;
            _discretizedPositionOfLastTower = -1;
            _towerGap1Semaphore.Reset();
            _towerGap2Semaphore.Reset();
            _isFuturesEnd = false;
            _allThingsEndingCounter = 0;
            _allThingsEndingSemaphore.Reset();
            _forsakenActive = false;
            Interlocked.Increment(ref _roundCommandMarkGeneration);
            CancelCommandMarkTimers();
        }

        #endregion

        #region Helpers: debug / parse / party

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
                // ignore
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
                // ignore
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

        private static string NormalizeIconId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            raw = raw.Trim();
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                raw = raw[2..];
            return raw.PadLeft(4, '0').ToUpperInvariant();
        }

        private static bool TryParseObjectId(string text, out uint id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
            return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id)
                || uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
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

        private static bool IsLegalPartyIndex(int partyIndex) => partyIndex >= 0 && partyIndex <= 7;

        private static bool IsSupporter(int partyIndex) => partyIndex is >= 0 and <= 3;
        private static bool IsDps(int partyIndex) => partyIndex is >= 4 and <= 7;
        private static bool IsMelee(int partyIndex) => partyIndex is 0 or 1 or 4 or 5;
        private static bool IsRanged(int partyIndex) => partyIndex is 2 or 3 or 6 or 7;
        private static bool IsTank(int partyIndex) => IsSupporter(partyIndex) && IsMelee(partyIndex);
        private static bool IsHealer(int partyIndex) => IsSupporter(partyIndex) && IsRanged(partyIndex);
        private static bool IsMeleeDps(int partyIndex) => IsDps(partyIndex) && IsMelee(partyIndex);
        private static bool IsRangedDps(int partyIndex) => IsDps(partyIndex) && IsRanged(partyIndex);

        private static string PartyPriorityLabel(int partyIndex) =>
            partyIndex >= 0 && partyIndex < PartyPriorityLabels.Length
                ? PartyPriorityLabels[partyIndex]
                : $"P{partyIndex + 1}";

        private static uint GetMyId(ScriptAccessory accessory) => accessory.Data.Me;

        private static int GetMyIndex(ScriptAccessory accessory) =>
            accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        private static Vector3 RotatePosition(Vector3 position, Vector3 center, double radian, bool preserveHeight = true)
        {
            var positionInVector2 = new Vector2(position.X - center.X, position.Z - center.Z);
            var polarAngleAfterRotation = Math.PI - Math.Atan2(positionInVector2.X, positionInVector2.Y) + radian;
            return new Vector3(
                (float)(center.X + Math.Sin(polarAngleAfterRotation) * positionInVector2.Length()),
                preserveHeight ? position.Y : center.Y,
                (float)(center.Z - Math.Cos(polarAngleAfterRotation) * positionInVector2.Length()));
        }

        private static double ConvertDegreesToRadians(double degree) => degree * Math.PI / 180.0;

        private bool UseUptimeOdd() => Phase2Sub2Strat == Phase2Sub2Strats.近战优化_分摊在前方;

        private Vector3 PickOdd(Vector3 vanilla, Vector3 uptime) => UseUptimeOdd() ? uptime : vanilla;

        #endregion

        #region AutoMove (GreenMove)

        private void AutoMoveToPoint(Vector3 target, ScriptAccessory accessory, string reason)
        {
            if (AutoMoveMode != AutoMoveModeSetting.GreenMove)
                return;

            // 移动前可调延迟：战斗重置（generation 变化）时中途取消
            var delayMs = Math.Clamp(AutoMoveDelayMs, 0, 1000);
            if (delayMs > 0)
            {
                var generation = _generation;
                DebugTrace(accessory, $"AutoMove delay {delayMs}ms before {reason}");
                var deadline = Environment.TickCount64 + delayMs;
                while (Environment.TickCount64 < deadline)
                {
                    if (generation != _generation)
                        return;
                    var remain = (int)(deadline - Environment.TickCount64);
                    if (remain <= 0) break;
                    Thread.Sleep(Math.Min(50, remain));
                }
                if (generation != _generation)
                    return;
            }

            GreenMoveToPoint(target, accessory, reason);
        }

        private void GreenMoveToPoint(Vector3 target, ScriptAccessory accessory, string reason)
        {
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
            return method.MakeGenericMethod(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(TRet))
                .Invoke(pluginInterface, new object[] { fullName })
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

        #region Command marks

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

                for (var i = 0; i < 17; i++)
                {
                    var actorId = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(IntPtr.Add(controller, 16 + i * 8));
                    if (actorId == 0 || actorId == InvalidObjectId)
                        continue;
                    _legacyDoMarkingByActorIdMethod.Invoke(null, new object[] { actorId, (int)MarkType.None, false });
                }

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
            RunCommandMarkOnFrameworkThread(accessory, context, () => CommandMarkClearCore(accessory, context));
        }

        private void CommandMarkPartyMemberCore(ScriptAccessory accessory, int partyIndex, MarkType markType)
        {
            if (!EnableCommandMode) return;
            if (partyIndex < 0 || partyIndex >= accessory.Data.PartyList.Count) return;
            accessory.Method.Mark(accessory.Data.PartyList[partyIndex], markType);
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
                    if (mechanicGeneration != _generation) return;
                    RunCommandMarkOnFrameworkThread(accessory, context, () =>
                    {
                        if (mechanicGeneration != _generation) return;
                        CommandMarkClearCore(accessory, context);
                    });
                }
                catch (Exception ex)
                {
                    DebugEcho(accessory, $"{context}: command mark clear failed: {ex.Message}");
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

        private void CommandMarkRoundMarkersForDuration(ScriptAccessory accessory, int round, MarkType?[] roundMarks)
        {
            try
            {
                if (!EnableCommandMode || roundMarks == null) return;
                var snapshot = roundMarks.ToArray();
                var context = $"Round {round}";
                var (delayMs, durationMs) = GetCommandMarkTiming();
                var generation = Interlocked.Increment(ref _roundCommandMarkGeneration);
                CancelCommandMarkApplyTimer();
                if (durationMs <= 0) return;

                Timer timer = null;
                timer = new Timer(_ =>
                {
                    try
                    {
                        if (!EnableCommandMode) return;
                        if (generation != Volatile.Read(ref _roundCommandMarkGeneration)) return;
                        RunCommandMarkOnFrameworkThread(accessory, context, () =>
                        {
                            if (!EnableCommandMode) return;
                            if (generation != Volatile.Read(ref _roundCommandMarkGeneration)) return;
                            CancelCommandMarkClearTimer();
                            for (var i = 0; i < snapshot.Length; i++)
                            {
                                if (!snapshot[i].HasValue) continue;
                                CommandMarkPartyMemberCore(accessory, i, snapshot[i].Value);
                            }
                            ScheduleCommandMarkClear(accessory, context, generation, durationMs);
                        });
                    }
                    catch (Exception ex)
                    {
                        DebugEcho(accessory, $"{context}: command mark apply failed: {ex.Message}");
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
                DebugEcho(accessory, $"Round {round}: command mark schedule failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 按 C「闲人固定」布局合成 Attack/Stop/Bind，供可选指挥模式使用。
        /// </summary>
        private MarkType?[] BuildCommandMarksForRound(int round, ScriptAccessory accessory)
        {
            var marks = new MarkType?[8];
            if (round < 1 || round > 8) return marks;

            var mechanicIsA = IsGroupARound[round];
            var mechanic = mechanicIsA ? _groupA : _groupB;
            var idle = mechanicIsA ? _groupB : _groupA;
            if (mechanic.Count != 4 || idle.Count != 4) return marks;

            if (round % 2 == 1)
            {
                // Attack: 左分摊 / 扇 / 散 / 右分摊（与 C 正北塔布局一致）
                int leftStack = -1, fan = -1, spread = -1, rightStack = -1;
                foreach (var idx in mechanic)
                {
                    switch (_iconType[idx])
                    {
                        case IconType.Fan:
                            fan = idx;
                            break;
                        case IconType.Spread:
                            spread = idx;
                            break;
                        case IconType.Stack:
                            if (round == 1)
                            {
                                if (idx == _initialLeftStack) leftStack = idx;
                                if (idx == _initialRightStack) rightStack = idx;
                            }
                            else
                            {
                                var left = IsOnLeftInGroup(idx, accessory);
                                if (left == true) leftStack = idx;
                                else if (left == false) rightStack = idx;
                            }
                            break;
                    }
                }

                if (leftStack >= 0) marks[leftStack] = MarkType.Attack1;
                if (fan >= 0) marks[fan] = MarkType.Attack2;
                if (spread >= 0) marks[spread] = MarkType.Attack3;
                if (rightStack >= 0) marks[rightStack] = MarkType.Attack4;
            }
            else if (round == 4)
            {
                foreach (var idx in mechanic)
                {
                    if (_iconType[idx] == IconType.Fan)
                        marks[idx] = idx == _initialUpperFan ? MarkType.Attack1 : MarkType.Attack2;
                    if (_iconType[idx] == IconType.Spread)
                        marks[idx] = idx == _initialUpperSpread ? MarkType.Attack4 : MarkType.Attack3;
                }
            }
            else
            {
                foreach (var idx in mechanic)
                {
                    var left = IsOnLeftInGroup(idx, accessory);
                    if (_iconType[idx] == IconType.Fan)
                        marks[idx] = left == true ? MarkType.Attack1 : MarkType.Attack2;
                    if (_iconType[idx] == IconType.Spread)
                        marks[idx] = left == true ? MarkType.Attack3 : MarkType.Attack4;
                }
            }

            foreach (var idx in idle)
            {
                if (IsTank(idx)) marks[idx] = MarkType.Stop1;
                else if (IsHealer(idx)) marks[idx] = MarkType.Stop2;
                else if (IsMeleeDps(idx)) marks[idx] = MarkType.Bind1;
                else if (IsRangedDps(idx)) marks[idx] = MarkType.Bind2;
            }

            return marks;
        }

        #endregion

        #region Draw

        private void DrawGuide(ScriptAccessory accessory, string name, Vector3 targetPosition, int duration, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Owner = GetMyId(accessory);
            dp.TargetPosition = targetPosition;
            dp.Scale = new Vector2(2);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = color ?? GuideColor.V4;
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        private void DrawGuideBetween(ScriptAccessory accessory, string name, Vector3 from, Vector3 to, int duration, Vector4? color = null)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = from;
            dp.TargetPosition = to;
            dp.Scale = new Vector2(2);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = color ?? accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        private void DrawFanHint(ScriptAccessory accessory, string name, Vector3 baitPosition, int duration)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(20);
            dp.Radian = (float)ConvertDegreesToRadians(5);
            dp.Position = ArenaCenter;
            dp.TargetPosition = baitPosition;
            dp.Color = GuideColor.V4;
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
        }

        private void DrawAuxCircle(ScriptAccessory accessory, string name, Vector3 position, int duration)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(0.5f);
            dp.Position = position;
            var auxColor = AuxiliaryColor.V4;
            dp.Color = new Vector4(auxColor.X, auxColor.Y, auxColor.Z, 1f);
            dp.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        private void GuideAndMove(ScriptAccessory accessory, string name, Vector3 position, int duration, string reason)
        {
            DrawGuide(accessory, name, position, duration);
            AutoMoveToPoint(position, accessory, reason);
        }

        #endregion

        #region Cicero grouping / sort / left-right

        private bool GroupPartyMembersAtBeginning(ScriptAccessory accessory)
        {
            int[][] pairs =
            {
                new[] { 0, 2 },
                new[] { 1, 3 },
                new[] { 4, 6 },
                new[] { 5, 7 },
            };

            _groupA.Clear();
            _groupB.Clear();
            _initialLeftStack = -1;
            _initialRightStack = -1;
            _initialUpperFan = -1;
            _initialUpperSpread = -1;

            for (var i = 0; i < 4; i++)
            {
                var member1 = pairs[i][0];
                var member2 = pairs[i][1];
                if (_iconType[member1] == IconType.Stack || _iconType[member2] == IconType.Stack)
                {
                    _groupA.Add(member1);
                    _groupA.Add(member2);
                    if (_iconType[member1] == IconType.Fan) _initialLeftStack = member2;
                    if (_iconType[member2] == IconType.Fan) _initialLeftStack = member1;
                    if (_iconType[member1] == IconType.Spread) _initialRightStack = member2;
                    if (_iconType[member2] == IconType.Spread) _initialRightStack = member1;
                }
                else
                {
                    _groupB.Add(member1);
                    _groupB.Add(member2);
                    if (IsMelee(member1))
                    {
                        if (_iconType[member1] == IconType.Fan) _initialUpperFan = member1;
                        if (_iconType[member1] == IconType.Spread) _initialUpperSpread = member1;
                    }
                    if (IsMelee(member2))
                    {
                        if (_iconType[member2] == IconType.Fan) _initialUpperFan = member2;
                        if (_iconType[member2] == IconType.Spread) _initialUpperSpread = member2;
                    }
                }
            }

            DebugTrace(accessory,
                $"groupA=[{string.Join(",", _groupA)}] groupB=[{string.Join(",", _groupB)}] " +
                $"LStack={_initialLeftStack} RStack={_initialRightStack} UFan={_initialUpperFan} USpread={_initialUpperSpread}");

            return _groupA.Count == 4
                && _groupB.Count == 4
                && !_groupA.ToHashSet().Overlaps(_groupB.ToHashSet());
        }

        private bool ScanAndSort(bool isGroupA, int discretizedGap, ScriptAccessory accessory)
        {
            if (discretizedGap < 0 || discretizedGap > 7) return false;
            var list = isGroupA ? _groupA : _groupB;
            list.Sort((x, y) =>
            {
                var xObject = accessory.Data.Objects.SearchById(accessory.Data.PartyList[x]);
                var yObject = accessory.Data.Objects.SearchById(accessory.Data.PartyList[y]);
                if (xObject == null || !xObject.IsValid()) return -1;
                if (yObject == null || !yObject.IsValid()) return 1;
                var xRel = RotatePosition(xObject.Position, ArenaCenter, -Math.PI / 4 * discretizedGap);
                var yRel = RotatePosition(yObject.Position, ArenaCenter, -Math.PI / 4 * discretizedGap);
                if (xRel.X > yRel.X) return -1;
                if (xRel.X < yRel.X) return 1;
                return 0;
            });
            DebugTrace(accessory, $"{(isGroupA ? "A" : "B")} sorted gap={discretizedGap}: [{string.Join(",", list)}]");
            return true;
        }

        private bool? IsOnLeftInGroup(int targetIndex, ScriptAccessory accessory)
        {
            if (!IsLegalPartyIndex(targetIndex)) return null;
            var inA = _groupA.Contains(targetIndex);
            var inB = _groupB.Contains(targetIndex);
            if (inA == inB) return null;
            var targetIcon = _iconType[targetIndex];
            if (targetIcon == IconType.Unknown) return null;
            var group = inA ? _groupA : _groupB;
            foreach (var member in group)
            {
                if (_iconType[member] != targetIcon) continue;
                return member == targetIndex;
            }
            return null;
        }

        private static int GetDiscretizedTowerGap(int tower1, int tower2)
        {
            if (tower1 < 0 || tower1 > 7 || tower2 < 0 || tower2 > 7) return -1;
            if (Math.Abs(tower1 - tower2) == 2)
                return ((tower1 + tower2) / 2 + 16) % 8;
            if (Math.Abs(tower1 - tower2) == 6)
                return ((tower1 + tower2 + 8) / 2 + 16) % 8;
            return -1;
        }

        #endregion

        #region Position resolution (Cicero)

        private Vector3 ResolveOddIdleBase(int partyIndex)
        {
            if (IsTank(partyIndex)) return PickOdd(OddTank, UptimeOddTank);
            if (IsHealer(partyIndex)) return PickOdd(OddHealer, UptimeOddHealer);
            if (IsMeleeDps(partyIndex)) return PickOdd(OddMeleeDps, UptimeOddMeleeDps);
            if (IsRangedDps(partyIndex)) return PickOdd(OddRangedDps, UptimeOddRangedDps);
            return ArenaCenter;
        }

        private Vector3 ResolveEvenIdleBase(int partyIndex)
        {
            if (IsTank(partyIndex)) return EvenTank;
            if (IsHealer(partyIndex)) return EvenHealer;
            if (IsMeleeDps(partyIndex)) return EvenMeleeDps;
            if (IsRangedDps(partyIndex)) return EvenRangedDps;
            return ArenaCenter;
        }

        private Vector3 ResolveRound1Position(int myIndex)
        {
            var pos = ArenaCenter;
            if (_groupA.Contains(myIndex))
            {
                if (_iconType[myIndex] == IconType.Fan)
                    pos = PickOdd(OddFan, UptimeOddFan);
                if (_iconType[myIndex] == IconType.Spread)
                    pos = PickOdd(OddSpread, UptimeOddSpread);
                if (_iconType[myIndex] == IconType.Stack)
                {
                    if (myIndex == _initialLeftStack)
                        pos = PickOdd(OddLeftStack, UptimeOddLeftStack);
                    if (myIndex == _initialRightStack)
                        pos = PickOdd(OddRightStack, UptimeOddRightStack);
                }
            }
            if (_groupB.Contains(myIndex))
                pos = ResolveOddIdleBase(myIndex);
            return pos;
        }

        private Vector3 ResolveRound4Position(int myIndex)
        {
            var pos = ArenaCenter;
            if (_groupB.Contains(myIndex))
            {
                if (_iconType[myIndex] == IconType.Fan)
                    pos = myIndex == _initialUpperFan ? EvenLeftFan : EvenRightFan;
                if (_iconType[myIndex] == IconType.Spread)
                    pos = myIndex == _initialUpperSpread ? EvenRightSpread : EvenLeftSpread;
            }
            if (_groupA.Contains(myIndex))
                pos = ResolveEvenIdleBase(myIndex);
            return pos;
        }

        private bool TryResolveGenericRoundPosition(int currentRound, int myIndex, ScriptAccessory accessory, out Vector3 myPosition)
        {
            myPosition = ArenaCenter;
            if (currentRound < 2 || currentRound == 4 || currentRound > 8) return false;

            var mechanicIsA = IsGroupARound[currentRound];
            var inMechanic = mechanicIsA ? _groupA.Contains(myIndex) : _groupB.Contains(myIndex);
            var inIdle = mechanicIsA ? _groupB.Contains(myIndex) : _groupA.Contains(myIndex);

            if (currentRound % 2 == 1)
            {
                if (inMechanic)
                {
                    if (_iconType[myIndex] == IconType.Fan)
                        myPosition = PickOdd(OddFan, UptimeOddFan);
                    if (_iconType[myIndex] == IconType.Spread)
                        myPosition = PickOdd(OddSpread, UptimeOddSpread);
                    if (_iconType[myIndex] == IconType.Stack)
                    {
                        var left = IsOnLeftInGroup(myIndex, accessory);
                        if (left == null) return false;
                        myPosition = left.Value
                            ? PickOdd(OddLeftStack, UptimeOddLeftStack)
                            : PickOdd(OddRightStack, UptimeOddRightStack);
                    }
                }
                if (inIdle)
                    myPosition = ResolveOddIdleBase(myIndex);
            }
            else
            {
                if (inMechanic)
                {
                    if (_iconType[myIndex] == IconType.Fan)
                    {
                        var left = IsOnLeftInGroup(myIndex, accessory);
                        if (left == null) return false;
                        myPosition = left.Value ? EvenLeftFan : EvenRightFan;
                    }
                    if (_iconType[myIndex] == IconType.Spread)
                    {
                        var left = IsOnLeftInGroup(myIndex, accessory);
                        if (left == null) return false;
                        myPosition = left.Value ? EvenLeftSpread : EvenRightSpread;
                    }
                }
                if (inIdle)
                    myPosition = ResolveEvenIdleBase(myIndex);
            }

            return Vector3.Distance(myPosition, ArenaCenter) >= CommonDeviation;
        }

        private Vector3 RotateForRound(Vector3 basePosition, int round)
        {
            var gap = _discretizedTowerGap[round];
            return RotatePosition(basePosition, ArenaCenter, Math.PI / 4 * gap);
        }

        private Vector3 ResolveBaitPosition(int round)
        {
            var bait = RawTowerPosition;
            if (_isFuturesEnd)
                bait = RotatePosition(RawTowerPosition, ArenaCenter, Math.PI);
            return RotatePosition(bait, ArenaCenter, Math.PI / 4 * _discretizedTowerGap[round]);
        }

        #endregion

        #region Guide execution

        private void EmitRoundGuide(ScriptAccessory accessory, int round, Vector3 myPosition, bool withOddBait)
        {
            var guideName = $"{DrawPrefix}_P2_遗弃末世_指路_第{round}轮";
            var duration = round == 1 ? Round1GuideDuration : MaximumDuration;

            if (withOddBait && round % 2 == 1 && round != 1)
            {
                var bait = ResolveBaitPosition(round);
                var baitName1 = $"{DrawPrefix}_P2_遗弃末世_消灭之脚指路1_第{round}轮";
                var baitName2 = $"{DrawPrefix}_P2_遗弃末世_消灭之脚指路2_第{round}轮";
                var baitName3 = $"{DrawPrefix}_P2_遗弃末世_消灭之脚指路3_第{round}轮";
                DrawGuide(accessory, baitName1, bait, MaximumDuration);
                DrawFanHint(accessory, baitName2, bait, MaximumDuration);
                DrawGuideBetween(accessory, baitName3, bait, myPosition, MaximumDuration);
                AutoMoveToPoint(bait, accessory, $"round {round} bait");

                _allThingsEndingSemaphore.WaitOne(AllThingsEndingWaitMs);
                accessory.Method.RemoveDraw(baitName1);
                accessory.Method.RemoveDraw(baitName2);
                accessory.Method.RemoveDraw(baitName3);
            }

            if (round % 2 == 0 && EnableEvenFanAuxiliaryCircles)
            {
                var fan1 = RotateForRound(EvenLeftFan, round);
                var fan2 = RotateForRound(EvenRightFan, round);
                DrawAuxCircle(accessory, $"{DrawPrefix}_P2_遗弃末世_指路辅助线1_第{round}轮", fan1, MaximumDuration);
                DrawAuxCircle(accessory, $"{DrawPrefix}_P2_遗弃末世_指路辅助线2_第{round}轮", fan2, MaximumDuration);
            }

            GuideAndMove(accessory, guideName, myPosition, duration, $"round {round} final");
            CommandMarkRoundMarkersForDuration(accessory, round, BuildCommandMarksForRound(round, accessory));
            DebugTrace(accessory, $"Guide round {round}: pos=({myPosition.X:F2},{myPosition.Z:F2}) futures={_isFuturesEnd}");
        }

        private void GuideRound1(ScriptAccessory accessory)
        {
            var myIndex = GetMyIndex(accessory);
            if (!IsLegalPartyIndex(myIndex)) return;
            if (!_roundSemaphore[0].WaitOne(1500 + CommonInterval)) return;
            if (!_towerGap1Semaphore.WaitOne(1500 + CommonInterval)) return;

            var basePos = ResolveRound1Position(myIndex);
            if (Vector3.Distance(basePos, ArenaCenter) < CommonDeviation) return;
            var myPosition = RotateForRound(basePos, 1);
            EmitRoundGuide(accessory, 1, myPosition, false);
        }

        private void GuideRound4(ScriptAccessory accessory)
        {
            var myIndex = GetMyIndex(accessory);
            if (!IsLegalPartyIndex(myIndex)) return;
            var basePos = ResolveRound4Position(myIndex);
            if (Vector3.Distance(basePos, ArenaCenter) < CommonDeviation) return;
            var myPosition = RotateForRound(basePos, 4);
            EmitRoundGuide(accessory, 4, myPosition, false);
        }

        private void GuideGenericRound(int currentRound, ScriptAccessory accessory)
        {
            var myIndex = GetMyIndex(accessory);
            if (!IsLegalPartyIndex(myIndex)) return;
            if (!TryResolveGenericRoundPosition(currentRound, myIndex, accessory, out var basePos)) return;
            var myPosition = RotateForRound(basePos, currentRound);
            EmitRoundGuide(accessory, currentRound, myPosition, withOddBait: true);
        }

        private void GuideTail(ScriptAccessory accessory)
        {
            var baitName1 = $"{DrawPrefix}_P2_遗弃末世_消灭之脚指路1_末尾";
            var baitName2 = $"{DrawPrefix}_P2_遗弃末世_消灭之脚指路2_末尾";
            var baitName3 = $"{DrawPrefix}_P2_遗弃末世_消灭之脚指路3_末尾";

            DrawGuide(accessory, baitName1, RawTowerPosition, MaximumDuration);
            DrawFanHint(accessory, baitName2, RawTowerPosition, MaximumDuration);
            AutoMoveToPoint(RawTowerPosition, accessory, "tail bait north");

            _allThingsEndingSemaphore.WaitOne(AllThingsEndingWaitMs);
            accessory.Method.RemoveDraw(baitName1);
            accessory.Method.RemoveDraw(baitName2);

            var finalPos = _isFuturesEnd
                ? RotatePosition(RawTowerPosition, ArenaCenter, Math.PI)
                : RawTowerPosition;
            GuideAndMove(accessory, baitName3, finalPos, 5000, "tail final");
        }

        private void ClearRoundDraws(ScriptAccessory accessory, int lastRound)
        {
            accessory.Method.RemoveDraw($"{DrawPrefix}_P2_遗弃末世_消灭之脚指路1_第{lastRound}轮");
            accessory.Method.RemoveDraw($"{DrawPrefix}_P2_遗弃末世_消灭之脚指路2_第{lastRound}轮");
            accessory.Method.RemoveDraw($"{DrawPrefix}_P2_遗弃末世_消灭之脚指路3_第{lastRound}轮");
            accessory.Method.RemoveDraw($"{DrawPrefix}_P2_遗弃末世_指路辅助线1_第{lastRound}轮");
            accessory.Method.RemoveDraw($"{DrawPrefix}_P2_遗弃末世_指路辅助线2_第{lastRound}轮");
            accessory.Method.RemoveDraw($"{DrawPrefix}_P2_遗弃末世_指路_第{lastRound}轮");
        }

        #endregion

        #region Events

        [ScriptMethod(
            name: "P2 遗弃末世 (阶段进入)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:47804"],
            userControl: false)]
        public void ForsakenPhaseEnter(Event @event, ScriptAccessory accessory)
        {
            // 每次进入遗弃末世重置，便于团灭重开后重新分组
            _generation++;
            ResetForsakenState();
            _forsakenActive = true;
            accessory.Method.RemoveDraw($"{DrawPrefix}_P2_.*");
            DebugEcho(accessory, "Forsaken phase entered.");
        }

        [ScriptMethod(
            name: "P2 遗弃末世 咏唱危机 (数据收集)",
            eventType: EventTypeEnum.TargetIcon,
            eventCondition: ["Id:regex:^(02CD|02CC|02CB)$"],
            userControl: false)]
        public void CollectSpellInDanger(Event @event, ScriptAccessory accessory)
        {
            if (!_forsakenActive) return;
            if (_iconUpdateCounter >= 8 + 4 * 6) return;
            if (!TryGetTargetId(@event, out var targetId)) return;
            var targetIndex = accessory.Data.PartyList.IndexOf(targetId);
            if (!IsLegalPartyIndex(targetIndex)) return;

            lock (_iconType)
            {
                var valid = false;
                var id = NormalizeIconId(@event["Id"]);
                if (string.Equals(id, "02CD", StringComparison.OrdinalIgnoreCase))
                {
                    _iconType[targetIndex] = IconType.Fan;
                    valid = true;
                }
                else if (string.Equals(id, "02CC", StringComparison.OrdinalIgnoreCase))
                {
                    _iconType[targetIndex] = IconType.Spread;
                    valid = true;
                }
                else if (string.Equals(id, "02CB", StringComparison.OrdinalIgnoreCase))
                {
                    _iconType[targetIndex] = IconType.Stack;
                    valid = true;
                }

                if (!valid) return;
                Interlocked.Increment(ref _iconUpdateCounter);
                if (_iconUpdateCounter < 8) return;

                if (_iconUpdateCounter == 8)
                {
                    if (!GroupPartyMembersAtBeginning(accessory)) return;
                    _roundSemaphore[0].Set();
                    DebugTrace(accessory, "Round0 semaphore set.");
                    return;
                }

                var updateCountAfterInitialization = _iconUpdateCounter - 8;
                if (updateCountAfterInitialization % 4 != 0) return;
                var lastRound = updateCountAfterInitialization / 4;
                if (lastRound is >= 1 and <= 3)
                    ScanAndSort(true, _discretizedTowerGap[lastRound], accessory);
                if (lastRound is >= 4 and <= 6)
                    ScanAndSort(false, _discretizedTowerGap[lastRound], accessory);
                if (lastRound >= 0 && lastRound < _roundSemaphore.Length)
                    _roundSemaphore[lastRound].Set();
                DebugTrace(accessory, $"Round {lastRound} semaphore set.");
            }
        }

        [ScriptMethod(
            name: "P2 遗弃末世 塔 (数据收集)",
            eventType: EventTypeEnum.EnvControl,
            eventCondition: ["Flag:2"],
            userControl: false)]
        public void CollectTowers(Event @event, ScriptAccessory accessory)
        {
            if (!_forsakenActive) return;
            if (_towerCounter >= 16) return;
            if (!int.TryParse(@event["Index"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                return;
            if (index < 1 || index > 8) return;

            lock (_towerGapLock)
            {
                Interlocked.Increment(ref _towerCounter);
                if (_towerCounter % 2 == 1)
                {
                    _discretizedPositionOfLastTower = (index - 1 + 8) % 8;
                    return;
                }

                var currentTower = (index - 1 + 8) % 8;
                var currentRound = _towerCounter / 2;
                _discretizedTowerGap[currentRound] = GetDiscretizedTowerGap(_discretizedPositionOfLastTower, currentTower);
                if (_discretizedTowerGap[currentRound] == -1) return;

                if (currentRound == 1)
                {
                    _towerGap1Semaphore.Set();
                    DebugTrace(accessory, $"Tower gap1={_discretizedTowerGap[1]}");
                }

                if (currentRound == 2)
                {
                    var trend = _discretizedTowerGap[2] - _discretizedTowerGap[1];
                    if (trend == -7) trend = 1;
                    if (trend == 7) trend = -1;
                    for (var i = 3; i < _discretizedTowerGap.Length; i++)
                        _discretizedTowerGap[i] = (_discretizedTowerGap[i - 1] + trend + 16) % 8;
                    _towerGap2Semaphore.Set();
                    DebugTrace(accessory, $"Tower gaps=[{string.Join(",", _discretizedTowerGap)}] trend={trend}");
                }
            }
        }

        [ScriptMethod(
            name: "P2 遗弃末世 (指路,第1轮)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:47804"],
            suppress: CommonInterval)]
        public void GuideRound1Event(Event @event, ScriptAccessory accessory)
        {
            if (!_forsakenActive) _forsakenActive = true;
            GuideRound1(accessory);
        }

        [ScriptMethod(
            name: "P2 遗弃末世 (指路,第2轮到第8轮)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:47806"])]
        public void GuideRound2To8Event(Event @event, ScriptAccessory accessory)
        {
            if (!_forsakenActive) return;
            if (!string.Equals(@event["TargetIndex"], "1")) return;
            if (_pathOfLightCounter >= 2 * 8) return;

            var lastRoundEnded = false;
            var lastRound = 0;
            lock (_pathOfLightLock)
            {
                Interlocked.Increment(ref _pathOfLightCounter);
                if (_pathOfLightCounter % 2 == 0)
                {
                    lastRoundEnded = true;
                    lastRound = _pathOfLightCounter / 2;
                }
            }

            if (!lastRoundEnded || lastRound < 1 || lastRound > 8) return;

            if (lastRound is >= 1 and <= 6)
            {
                if (!_roundSemaphore[lastRound].WaitOne(625 + CommonInterval)) return;
                if (lastRound == 1 && !_towerGap2Semaphore.WaitOne(CommonInterval)) return;
            }

            _allThingsEndingSemaphore.Reset();
            ClearRoundDraws(accessory, lastRound);

            if (lastRound == 3)
            {
                GuideRound4(accessory);
                return;
            }

            if (lastRound == 8)
            {
                GuideTail(accessory);
                return;
            }

            GuideGenericRound(lastRound + 1, accessory);
        }

        [ScriptMethod(
            name: "P2 遗弃末世 过去/未来终结 (数据收集)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(47826|47827)$"],
            userControl: false)]
        public void CollectFuturesPastEnd(Event @event, ScriptAccessory accessory)
        {
            if (!_forsakenActive) return;
            if (string.Equals(@event["ActionId"], "47826"))
                _isFuturesEnd = true;
            if (string.Equals(@event["ActionId"], "47827"))
                _isFuturesEnd = false;
            DebugTrace(accessory, $"isFuturesEnd={_isFuturesEnd}");
        }

        [ScriptMethod(
            name: "P2 遗弃末世 消灭之脚 (数据收集)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(47836|47837)$"],
            userControl: false)]
        public void CollectAllThingsEnding(Event @event, ScriptAccessory accessory)
        {
            if (!_forsakenActive) return;
            lock (_allThingsEndingLock)
            {
                Interlocked.Increment(ref _allThingsEndingCounter);
                if (_allThingsEndingCounter % 4 != 0) return;
                _allThingsEndingSemaphore.Set();
                DebugTrace(accessory, $"AllThingsEnding signaled counter={_allThingsEndingCounter}");
            }
        }

        #endregion
    }
}
