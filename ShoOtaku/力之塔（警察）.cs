using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Data;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Extensions;

namespace KodakkuAssistXSZYYSPolice;

[ScriptType(
    name: "力之塔（警察）",
    territorys: [1252],
    guid: "9F8E7D6C-5B4A-3C2D-1E0F-9A8B7C6D5E4F",
    version: "0.0.3",
    author: "XSZYYS",
    note: "力之塔副本的小警察功能，提供关键机制点名播报和检查功能。同时可主动检查扔钱/复活/食物/蓝药等。\r\n\r\n------------以下为默语频道的主动查询功能示意，可配置响应来自小队的检查指令并在小队频道输出------------\r\n检查蓝药：输入【/e 蓝药检查】会输出药师蓝药使用情况，输入【/e 蓝药清理】会清理所有数据\r\n检查复活：输入【/e 复活检查 <数字>...】，数字仅限0~3，支持空格分隔、去重排序，比如【/e 复活检查 1 2】。不指定次数则输出复活次数在0~3次的所有玩家\r\n检查食物：输入【/e 食物检查】会输出所有塔内未进食/食物剩余时间小于等于指定阈值的玩家，负数阈值按0分钟处理\r\n检查扔钱：输入【/e 扔钱检查】会输出所有使用扔钱的玩家和扔钱次数，输入【/e 扔钱清理】会清空扔钱统计数据\r\n标记药师：输入【/e 标记药师】会先清空公共头顶标记，再以攻击1~8标记至多8名药师；标记对其他玩家可见",
    updateInfo: "检查输出改用可取消异步快照排程，重置、卸载或新同类命令使旧回调失效；校验复活参数及食物阈值；修正陨石和斧猎物状态ID、雪球连线ID，恢复南北分摊图标事件；修正尾王三平台坐标和状态去重生命周期，圣枪排序保留至41557轮次重置；公共药师标记上限8人。"
)]
public class TowerPolice : IDisposable
{
    [UserSetting("小警察（开启后默语频道输出关键机制被点名玩家名字）")]
    public bool PoliceMode { get; set; } = false;

    [UserSetting("接收小队内的扔钱/复活/食物/蓝药检查请求")]
    public bool ReceivePartyCheckRequest { get; set; } = false;
    [UserSetting("食物检查剩余时间阈值（单位：分钟），仅输出时间小于等于该值的玩家；负数按0处理")]
    public int FoodRemainingTimeThreshold { get; set; } = 0;

    [UserSetting("蓝药检查范围（仅小队）")]
    public bool Partycheck { get; set; } = false;

    [UserSetting("开发者模式（调试日志）")]
    public bool EnableDeveloperMode { get; set; } = false;

    private static readonly Dictionary<uint, string> _supportJobStatus = new()
    {
        { 4242, "辅助自由人" },
        { 4358, "辅助骑士" },
        { 4359, "辅助狂战士" },
        { 4360, "辅助武僧" },
        { 4361, "辅助猎人" },
        { 4362, "辅助武士" },
        { 4363, "辅助吟游诗人" },
        { 4364, "辅助风水师" },
        { 4365, "辅助时魔法师" },
        { 4366, "辅助炮击士" },
        { 4367, "辅助药剂师" },
        { 4368, "辅助预言师" },
        { 4369, "辅助盗贼" }
    };
    private string GetSupportJob(IPlayerCharacter player)
    {
        if (player == null) return "无";
        var status = player.StatusList.FirstOrDefault(s => _supportJobStatus.ContainsKey(s.StatusId));
        return status != null ? _supportJobStatus[status.StatusId] : "无";
    }
    private readonly Dictionary<string, Dictionary<string, int>> _moneyThrowCounts = new();
    private readonly object _moneyThrowLock = new();

    private readonly Dictionary<string, Dictionary<string, int>> _bluePotionCounts = new();
    private readonly object _bluePotionLock = new();

    private readonly HashSet<(uint StatusId, ulong PlayerId)> _checkedPreyPlayers = new();
    private readonly object _preyCheckLock = new();

    private readonly HashSet<(uint StatusId, ulong PlayerId)> _sacredBowPreyRecordedPlayers = new();
    private readonly Dictionary<int, List<(ulong PlayerId, float Duration)>> _lanceShareAssignments = new();
    private readonly object _lanceShareLock = new();

    // 与力之塔.cs尾王方形绘图使用同一组世界坐标和角度，索引固定为0=下、1=右上、2=左上。
    // SquarePositions与SquareAngles必须一一对应；站位检测只使用X/Z平面。
    private static readonly List<Vector3> SquarePositions = new()
    {
        new(700f, -476f, -659.504f),
        new(712.554f, -476f, -681.248f),
        new(687.443f, -476f, -681.25f)
    };

    private static readonly float[] SquareAngles =
    {
        -45 * MathF.PI / 180.0f,
        -15 * MathF.PI / 180.0f,
        105 * MathF.PI / 180.0f
    };

    // 大斧9/21秒共用状态4337，小斧13/21秒共用状态4336；按初始持续时间区分点名。
    private const float PREY_DURATION_9S = 9.0f;
    private const float PREY_DURATION_13S = 13.0f;
    private const float PREY_DURATION_21S = 21.0f;
    private const float PREY_DURATION_TOLERANCE = 0.05f;

    private readonly object _commandLock = new();
    private CancellationTokenSource _commandCancellation = new();
    private bool _disposed;
    private long _initGeneration;
    private long _resurrectionCommandGeneration;
    private long _foodCommandGeneration;
    private long _moneyCommandGeneration;
    private long _bluePotionCommandGeneration;
    private long _markCommandGeneration;

    public void Init(ScriptAccessory accessory)
    {
        lock (_commandLock)
        {
            if (_disposed) return;
            _initGeneration++;
            _commandCancellation.Cancel();
            _commandCancellation.Dispose();
            _commandCancellation = new CancellationTokenSource();
            lock (_moneyThrowLock)
            {
                _moneyThrowCounts.Clear();
            }
            lock (_bluePotionLock)
            {
                _bluePotionCounts.Clear();
            }
            lock (_preyCheckLock)
            {
                _checkedPreyPlayers.Clear();
            }
            lock (_lanceShareLock)
            {
                _sacredBowPreyRecordedPlayers.Clear();
                _lanceShareAssignments.Clear();
            }
        }
    }

    public void Dispose()
    {
        lock (_commandLock)
        {
            if (_disposed) return;
            _disposed = true;
            _initGeneration++;
            _commandCancellation.Cancel();
            _commandCancellation.Dispose();
        }
    }

    // 命令先生成完整快照再排程。检查代次与发送共用锁，避免重置/新命令插入检查与发送之间。
    private void ScheduleChatLines(ScriptAccessory accessory, List<string> lines, long initGeneration, Func<bool> isCurrentCommand)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            RunCommandDelayed(accessory, i * 100, initGeneration, isCurrentCommand, () => accessory.Method.SendChat(line));
        }
    }

    private void RunCommandDelayed(ScriptAccessory accessory, int delayMs, long initGeneration, Func<bool> isCurrentCommand, Action action)
    {
        CancellationToken cancellationToken;
        lock (_commandLock)
        {
            if (_disposed || _initGeneration != initGeneration || !isCurrentCommand()) return;
            cancellationToken = _commandCancellation.Token;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, cancellationToken);
                lock (_commandLock)
                {
                    if (_disposed || _initGeneration != initGeneration || !isCurrentCommand()) return;
                    action();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Init重置或卸载会主动取消旧任务，允许忽略该预期取消。
            }
            catch (Exception ex)
            {
                accessory.Log.Error($"TowerPolice delayed command error: {ex}");
            }
        });
    }

    #region 老一：分摊点名播报

    [ScriptMethod(
        name: "分摊点名播报（南侧）",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:023E"],
        userControl: false
    )]
    public void OnSouthStack(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        var target = accessory.Data.Objects.SearchById(@event.TargetId);
        if (target != null)
        {
            accessory.Method.SendChat($"/e 分摊（南侧）点名: {target.Name}");
        }
    }

    [ScriptMethod(
        name: "分摊点名播报（北侧）",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:023F"],
        userControl: false
    )]
    public void OnNorthStack(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        var target = accessory.Data.Objects.SearchById(@event.TargetId);
        if (target != null)
        {
            accessory.Method.SendChat($"/e 分摊（北侧）点名: {target.Name}");
        }
    }

    #endregion

    #region 老一：陨石点名播报

    [ScriptMethod(
        name: "陨石点名播报",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:4354"],
        userControl: false
    )]
    public void OnCometeorStatusAdd(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        var target = accessory.Data.Objects.SearchById(@event.TargetId);
        if (target != null)
        {
            accessory.Method.SendChat($"/e 陨石点名: {target.Name}");
        }
    }

    #endregion

    #region 老二：雪球连线点名播报

    [ScriptMethod(
        name: "雪球连线点名播报",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:00F6"],
        userControl: false
    )]
    public void OnGlacialImpactTether(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        var target = accessory.Data.Objects.SearchById(@event.TargetId);
        if (target != null)
        {
            accessory.Method.SendChat($"/e 雪球连线点名: {target.Name}");
        }
    }

    #endregion

    #region 尾王：大斧猎物点名播报

    [ScriptMethod(
        name: "大斧猎物点名播报（9秒）",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:4337"],
        userControl: false
    )]
    public void GreatAxePrey(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        if (float.TryParse(@event["Duration"], out var duration1) &&
            Math.Abs(duration1 - PREY_DURATION_9S) < PREY_DURATION_TOLERANCE)
        {
            var target = accessory.Data.Objects.SearchById(@event.TargetId);
            if (target != null)
            {
                accessory.Method.SendChat($"/e 大圈（9秒）点名: {target.Name}");
            }
        }
    }

    [ScriptMethod(
        name: "大斧猎物点名播报（21秒）",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:4337"],
        userControl: false
    )]
    public void GreatAxePreyLong(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        if (float.TryParse(@event["Duration"], out var duration1) &&
            Math.Abs(duration1 - PREY_DURATION_21S) < PREY_DURATION_TOLERANCE)
        {
            CheckPreyPosition(accessory, @event.StatusId, @event.TargetId);
            var target = accessory.Data.Objects.SearchById(@event.TargetId);
            if (target != null)
            {
                accessory.Method.SendChat($"/e 大圈（21秒）点名: {target.Name}");
            }
        }
    }

    #endregion

    #region 尾王：小斧猎物点名播报

    [ScriptMethod(
        name: "小斧猎物点名播报",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:4336"],
        userControl: false
    )]
    public void LesserAxePrey(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        if (!float.TryParse(@event["Duration"], out var duration1)) return;

        if (Math.Abs(duration1 - PREY_DURATION_13S) < PREY_DURATION_TOLERANCE)
        {
            CheckPreyPosition(accessory, @event.StatusId, @event.TargetId);
            var target = accessory.Data.Objects.SearchById(@event.TargetId);
            if (target != null)
            {
                accessory.Method.SendChat($"/e 小圈（13秒）点名: {target.Name}");
            }
        }
        else if (Math.Abs(duration1 - PREY_DURATION_21S) < PREY_DURATION_TOLERANCE)
        {
            CheckPreyPosition(accessory, @event.StatusId, @event.TargetId);
            var target = accessory.Data.Objects.SearchById(@event.TargetId);
            if (target != null)
            {
                accessory.Method.SendChat($"/e 小圈（21秒）点名: {target.Name}");
            }
        }
    }

    #endregion

    #region 尾王：圣枪分摊点名播报和擦边检查

    [ScriptMethod(
        name: "圣枪分摊记录重置",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:41557"],
        userControl: false
    )]
    public void ResetLanceShareRound(Event @event, ScriptAccessory accessory)
    {
        lock (_lanceShareLock)
        {
            // 与主脚本HolyLanceGuide使用相同轮次边界；单人到期不能删除本轮初始排序依据。
            _sacredBowPreyRecordedPlayers.Clear();
            _lanceShareAssignments.Clear();
        }
    }

    [ScriptMethod(
        name: "圣枪分摊点名",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:4338"]
    )]
    public void SacredBowPrey_RecordAndBroadcast(Event @event, ScriptAccessory accessory)
    {
        if (!float.TryParse(@event["Duration"], out var duration) || !float.IsFinite(duration) || duration <= 0) return;
        var player = accessory.Data.Objects.SearchById(@event.TargetId);
        if (player == null) return;

        lock (_lanceShareLock)
        {
            if (!_sacredBowPreyRecordedPlayers.Add((@event.StatusId, player.EntityId))) return;
            // 保留本轮首次记录的持续时间；同一玩家重新获得状态也不能改变后续玩家的排序。
            if (_lanceShareAssignments.Values.Any(assignments => assignments.Any(p => p.PlayerId == player.EntityId))) return;

            int platformIndex = -1;
            for (int i = 0; i < SquarePositions.Count; i++)
            {
                if (IsPointInRotatedRect(player.Position, SquarePositions[i], 20, 20, SquareAngles[i]))
                {
                    platformIndex = i;
                    break;
                }
            }

            if (platformIndex != -1)
            {
                if (!_lanceShareAssignments.ContainsKey(platformIndex))
                {
                    _lanceShareAssignments[platformIndex] = new List<(ulong, float)>();
                }
                _lanceShareAssignments[platformIndex].Add((player.EntityId, duration));
                if (EnableDeveloperMode)
                {
                    accessory.Log.Debug($"圣枪分摊记录: {player.Name.TextValue} 在平台 {platformIndex + 1}，持续时间 {duration:F2}s");
                }
            }
            else if (EnableDeveloperMode)
            {
                accessory.Log.Debug($"圣枪分摊记录: {player.Name.TextValue} 不在任何平台。");
            }

            if (PoliceMode)
            {
                string platformName = platformIndex switch
                {
                    0 => "下",
                    1 => "右上",
                    2 => "左上",
                    _ => "平台外"
                };

                accessory.Method.SendChat($"/e 圣枪分摊点名: {player.Name.TextValue} - {platformName} ({duration:F1}s)");
            }
        }
    }

    [ScriptMethod(
        name: "圣枪分摊 - 擦边检查",
        eventType: EventTypeEnum.StatusRemove,
        eventCondition: ["StatusID:4338"]
    )]
    public void SacredBowPrey_CheckOnExpire(Event @event, ScriptAccessory accessory)
    {
        lock (_lanceShareLock)
        {
            if (!_sacredBowPreyRecordedPlayers.Remove((@event.StatusId, @event.TargetId))) return;
            // 仅对正常到期的存活玩家判定擦边；提前移除、死亡或对象不可见也必须释放去重状态。
            if (!float.TryParse(@event["Duration"], out var remainingDuration) ||
                !float.IsFinite(remainingDuration) || remainingDuration > 0.1f) return;
            var player = accessory.Data.Objects.SearchById(@event.TargetId);
            if (player == null || player.IsDead) return;

            int initialPlatform = -1;
            foreach (var entry in _lanceShareAssignments)
            {
                if (entry.Value.Any(p => p.PlayerId == player.EntityId))
                {
                    initialPlatform = entry.Key;
                    break;
                }
            }

            if (initialPlatform == -1) return;

            // 按初始持续时间从短到长固定分摊顺序；所有历史条目保留至41557或Init，避免后续玩家被重新编号。
            var sortedPlayers = _lanceShareAssignments[initialPlatform].OrderBy(p => p.Duration).ToList();
            int orderIndex = sortedPlayers.FindIndex(p => p.PlayerId == player.EntityId);

            bool shouldCheck = false;
            // 沿用主脚本策略：下平台检查第1/3次，右上检查第2/3次，左上检查第1/2次。
            switch (initialPlatform)
            {
                case 0:
                    if (orderIndex == 0 || orderIndex == 2) shouldCheck = true;
                    break;
                case 1:
                    if (orderIndex == 1 || orderIndex == 2) shouldCheck = true;
                    break;
                case 2:
                    if (orderIndex == 0 || orderIndex == 1) shouldCheck = true;
                    break;
            }

            if (shouldCheck && PoliceMode && !IsCircleFullyContainedInAnyPlatform(player.Position))
            {
                accessory.Method.SendChat($"/e 分摊擦边: {player.Name.TextValue}");
            }
        }
    }

    #endregion

    #region 尾王：站位检测辅助方法

    [ScriptMethod(
        name: "释放猎物站位检查记录",
        eventType: EventTypeEnum.StatusRemove,
        eventCondition: ["StatusID:regex:^(4336|4337)$"],
        userControl: false
    )]
    public void ReleasePreyPositionCheck(Event @event, ScriptAccessory accessory)
    {
        lock (_preyCheckLock)
        {
            _checkedPreyPlayers.Remove((@event.StatusId, @event.TargetId));
        }
    }

    private void CheckPreyPosition(ScriptAccessory accessory, uint statusId, ulong targetId)
    {
        if (!PoliceMode) return;

        var player = accessory.Data.Objects.SearchById(targetId);
        if (player == null) return;

        // 仅在时长匹配且对象可见后记录；不同状态独立去重，状态移除或Init后可再次检查。
        lock (_preyCheckLock)
        {
            if (!_checkedPreyPlayers.Add((statusId, targetId))) return;
        }

        bool isInAnySquare = false;
        for (int i = 0; i < SquarePositions.Count; i++)
        {
            if (IsPointInRotatedRect(player.Position, SquarePositions[i], 20, 20, SquareAngles[i]))
            {
                isInAnySquare = true;
                break;
            }
        }

        if (!isInAnySquare)
        {
            accessory.Method.SendChat($"/e {player.Name} 站位错误！");
        }
    }

    private bool IsPointInRotatedRect(Vector3 point, Vector3 rectCenter, float rectWidth, float rectHeight, float rectAngleRad)
    {
        float translatedX = point.X - rectCenter.X;
        float translatedZ = point.Z - rectCenter.Z;

        float cosAngle = MathF.Cos(-rectAngleRad);
        float sinAngle = MathF.Sin(-rectAngleRad);

        float rotatedX = translatedX * cosAngle - translatedZ * sinAngle;
        float rotatedZ = translatedX * sinAngle + translatedZ * cosAngle;

        return (Math.Abs(rotatedX) <= rectWidth / 2) && (Math.Abs(rotatedZ) <= rectHeight / 2);
    }

    private bool IsCircleFullyContainedInAnyPlatform(Vector3 circleCenter)
    {
        const float circleRadius = 6f;
        const float platformSize = 20f;
        // 通过收缩平台来简化问题：如果圆心在一个更小的矩形内，那么整个圆就在原始矩形内
        float shrunkenSize = platformSize - 2 * circleRadius;

        for (int i = 0; i < SquarePositions.Count; i++)
        {
            if (IsPointInRotatedRect(circleCenter, SquarePositions[i], shrunkenSize, shrunkenSize, SquareAngles[i]))
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region 复活检查功能

    [ScriptMethod(
        name: "检查复活",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$"]
    )]
    public void CheckResurrection(Event @event, ScriptAccessory accessory)
    {
        try
        {
            string channel = @event["Type"].ToLowerInvariant();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            string[] parts = @event["Message"].Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0] != "复活检查") return;

            lock (_commandLock)
            {
                long initGeneration = _initGeneration;
                long commandGeneration = ++_resurrectionCommandGeneration;
                var lines = new List<string>();
                if (!TryParseResurrectionCounts(@event["Message"], out var targetCounts))
                {
                    lines.Add($"/{channel} 用法：复活检查 [0~3]...，可用空格分隔多个次数；省略参数则检查0~3次。");
                    ScheduleChatLines(accessory, lines, initGeneration, () => commandGeneration == _resurrectionCommandGeneration);
                    return;
                }

                var allResurrectionData = new List<(string Name, string Job, string SupportJob, int Count)>();
                foreach (var gameObject in accessory.Data.Objects)
                {
                    if (gameObject is IPlayerCharacter player)
                    {
                        var resStatus = player.StatusList.FirstOrDefault(s => s.StatusId == 4262 || s.StatusId == 4263);
                        if (resStatus != null)
                        {
                            allResurrectionData.Add((player.Name.TextValue, player.ClassJob.Value.Name.ToString(), GetSupportJob(player), resStatus.Param));
                        }
                    }
                }

                lines.Add($"/{channel} --- 开始复活检查 ---");
                foreach (int count in targetCounts)
                {
                    var filteredData = allResurrectionData.Where(data => data.Count == count).ToList();
                    if (filteredData.Count == 0)
                    {
                        lines.Add($"/{channel} --- 未找到复活次数为 {count} 的玩家 ---");
                        continue;
                    }
                    lines.Add($"/{channel} --- 复活次数为 {count} 的玩家（共{filteredData.Count}人） ---");
                    foreach (var data in filteredData)
                    {
                        lines.Add($"/{channel} {data.Name} ({data.Job} | {data.SupportJob})");
                    }
                }
                ScheduleChatLines(accessory, lines, initGeneration, () => commandGeneration == _resurrectionCommandGeneration);
            }
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"CheckResurrection error: {ex}");
        }
    }

    private static bool TryParseResurrectionCounts(string message, out List<int> counts)
    {
        counts = new List<int>();
        if (string.IsNullOrWhiteSpace(message)) return false;

        string[] parts = message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (parts[0] != "复活检查") return false;

        var targetCounts = new SortedSet<int>();
        foreach (string part in parts.Skip(1))
        {
            if (!int.TryParse(part, out int count) || count < 0 || count > 3) return false;
            targetCounts.Add(count);
        }
        if (parts.Length == 1) targetCounts.UnionWith(Enumerable.Range(0, 4));

        counts = targetCounts.ToList();
        return true;
    }

    #endregion

    #region 食物检查功能

    [ScriptMethod(
        name: "检查食物",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$", "Message:食物检查"]
    )]
    public void CheckFoodStatus(Event @event, ScriptAccessory accessory)
    {
        try
        {
            string channel = @event["Type"].ToLowerInvariant();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            lock (_commandLock)
            {
                long initGeneration = _initGeneration;
                long commandGeneration = ++_foodCommandGeneration;
                int thresholdMinutes = Math.Max(0, FoodRemainingTimeThreshold);
                double thresholdSeconds = (double)thresholdMinutes * 60;
                int towerPlayerCount = 0;
                var foodStatusData = new List<(string Name, string Job, string SupportJob, string Status)>();
                foreach (var gameObject in accessory.Data.Objects)
                {
                    if (gameObject is IPlayerCharacter player && player.HasStatusAny([4262, 4263]))
                    {
                        towerPlayerCount++;
                        string playerName = player.Name.TextValue;
                        string classJob = player.ClassJob.Value.Name.ToString();
                        string supportJob = GetSupportJob(player);
                        var foodStatus = player.StatusList.FirstOrDefault(s => s.StatusId == 48);
                        if (foodStatus == null)
                        {
                            foodStatusData.Add((playerName, classJob, supportJob, "未进食"));
                        }
                        else if (foodStatus.RemainingTime <= thresholdSeconds)
                        {
                            foodStatusData.Add((playerName, classJob, supportJob, $"食物剩余约{Math.Ceiling(Math.Max(0, foodStatus.RemainingTime) / 60)}分钟"));
                        }
                    }
                }

                var lines = new List<string>
                {
                    $"/{channel} --- 检查未进食或食物剩余不超过{thresholdMinutes}分钟的玩家（塔内共{towerPlayerCount}人） ---"
                };
                if (foodStatusData.Count > 0)
                {
                    foreach (var data in foodStatusData.OrderBy(data => data.Status))
                    {
                        lines.Add($"/{channel} {data.Name} ({data.Job} | {data.SupportJob}): {data.Status}");
                    }
                }
                else
                {
                    lines.Add($"/{channel} 所有玩家食物时间符合要求");
                }
                ScheduleChatLines(accessory, lines, initGeneration, () => commandGeneration == _foodCommandGeneration);
            }
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"CheckFoodStatus error: {ex}");
        }
    }

    #endregion

    #region 扔钱检查功能

    [ScriptMethod(
        name: "记录扔钱次数",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:41606"],
        userControl: false
    )]
    public void RecordMoneyThrow(Event @event, ScriptAccessory accessory)
    {
        var source = accessory.Data.Objects.SearchById(@event.SourceId);
        var target = accessory.Data.Objects.SearchById(@event.TargetId);

        if (source == null || target == null || !(source is IBattleChara) || !(target is IBattleChara))
            return;

        string playerName = source.Name.TextValue;
        string bossName = target.Name.TextValue;

        lock (_moneyThrowLock)
        {
            if (!_moneyThrowCounts.ContainsKey(bossName))
            {
                _moneyThrowCounts[bossName] = new Dictionary<string, int>();
            }
            if (!_moneyThrowCounts[bossName].ContainsKey(playerName))
            {
                _moneyThrowCounts[bossName][playerName] = 0;
            }
            _moneyThrowCounts[bossName][playerName]++;
        }
    }

    [ScriptMethod(
        name: "检查扔钱",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$", "Message:扔钱检查"]
    )]
    public void CheckMoneyThrow(Event @event, ScriptAccessory accessory)
    {
        try
        {
            string channel = @event["Type"].ToLowerInvariant();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            lock (_commandLock)
            {
                long initGeneration = _initGeneration;
                long commandGeneration = ++_moneyCommandGeneration;
                var lines = new List<string>();
                lock (_moneyThrowLock)
                {
                    foreach (var bossEntry in _moneyThrowCounts)
                    {
                        lines.Add($"/{channel} --- {bossEntry.Key} 扔钱统计 ---");
                        foreach (var data in bossEntry.Value.OrderBy(kvp => kvp.Value))
                        {
                            lines.Add($"/{channel} {data.Key}: {data.Value} 次");
                        }
                    }
                }
                if (lines.Count == 0)
                {
                    lines.Add($"/{channel} 未记录到任何扔钱数据。");
                }
                ScheduleChatLines(accessory, lines, initGeneration, () => commandGeneration == _moneyCommandGeneration);
            }
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"CheckMoneyThrow error: {ex}");
        }
    }

    [ScriptMethod(
        name: "清理扔钱数据",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$", "Message:扔钱清理"]
    )]
    public void ClearMoneyThrowData(Event @event, ScriptAccessory accessory)
    {
        try
        {
            string channel = @event["Type"].ToLowerInvariant();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            lock (_commandLock)
            {
                long initGeneration = _initGeneration;
                long commandGeneration = ++_moneyCommandGeneration;
                lock (_moneyThrowLock)
                {
                    _moneyThrowCounts.Clear();
                }
                ScheduleChatLines(accessory, [$"/{channel} 扔钱数据已清理。"], initGeneration, () => commandGeneration == _moneyCommandGeneration);
            }
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"ClearMoneyThrowData error: {ex}");
        }
    }

    #endregion

    #region 蓝药检查功能

    [ScriptMethod(
        name: "记录蓝药次数",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:41633"],
        userControl: false
    )]
    public void RecordBluePotion(Event @event, ScriptAccessory accessory)
    {
        var source = accessory.Data.Objects.SearchById(@event.SourceId);
        var target = accessory.Data.Objects.SearchById(@event.TargetId);

        if (source == null || target == null || !(source is IPlayerCharacter) || !(target is IPlayerCharacter))
            return;

        string sourcePlayerName = source.Name.TextValue;
        string targetPlayerName = target.Name.TextValue;

        lock (_bluePotionLock)
        {
            if (!_bluePotionCounts.ContainsKey(targetPlayerName))
            {
                _bluePotionCounts[targetPlayerName] = new Dictionary<string, int>();
            }
            if (!_bluePotionCounts[targetPlayerName].ContainsKey(sourcePlayerName))
            {
                _bluePotionCounts[targetPlayerName][sourcePlayerName] = 0;
            }
            _bluePotionCounts[targetPlayerName][sourcePlayerName]++;
        }
    }

    [ScriptMethod(
        name: "检查蓝药",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$", "Message:蓝药检查"]
    )]
    public void CheckBluePotion(Event @event, ScriptAccessory accessory)
    {
        try
        {
            string channel = @event["Type"].ToLowerInvariant();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            lock (_commandLock)
            {
                long initGeneration = _initGeneration;
                long commandGeneration = ++_bluePotionCommandGeneration;
                // 本次命令固定检查范围，排程回调不再读取队伍或对象表。
                var partyMemberNames = Partycheck
                    ? accessory.Data.PartyList
                        .Select(id => accessory.Data.Objects.SearchById(id)?.Name.TextValue)
                        .Where(name => name != null)
                        .ToHashSet()
                    : null;

                var lines = new List<string>();
                lock (_bluePotionLock)
                {
                    foreach (var targetEntry in _bluePotionCounts)
                    {
                        var filteredPlayers = targetEntry.Value
                            .Where(kvp => partyMemberNames == null ||
                                (partyMemberNames.Contains(kvp.Key) && partyMemberNames.Contains(targetEntry.Key)))
                            .OrderBy(kvp => kvp.Value)
                            .ToList();
                        if (filteredPlayers.Count == 0) continue;
                        lines.Add($"/{channel} --- 对 {targetEntry.Key} 的蓝药统计 ---");
                        foreach (var data in filteredPlayers)
                        {
                            lines.Add($"/{channel} {data.Key}: {data.Value} 次");
                        }
                    }
                    if (lines.Count == 0)
                    {
                        lines.Add(_bluePotionCounts.Count == 0
                            ? $"/{channel} 未记录到任何蓝药数据。"
                            : $"/{channel} 当前范围内未记录到符合条件的蓝药数据。");
                    }
                }
                ScheduleChatLines(accessory, lines, initGeneration, () => commandGeneration == _bluePotionCommandGeneration);
            }
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"CheckBluePotion error: {ex}");
        }
    }

    [ScriptMethod(
        name: "清理蓝药数据",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$", "Message:蓝药清理"]
    )]
    public void ClearBluePotionData(Event @event, ScriptAccessory accessory)
    {
        try
        {
            string channel = @event["Type"].ToLowerInvariant();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            lock (_commandLock)
            {
                long initGeneration = _initGeneration;
                long commandGeneration = ++_bluePotionCommandGeneration;
                lock (_bluePotionLock)
                {
                    _bluePotionCounts.Clear();
                }
                ScheduleChatLines(accessory, [$"/{channel} 蓝药数据已清理。"], initGeneration, () => commandGeneration == _bluePotionCommandGeneration);
            }
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"ClearBluePotionData error: {ex}");
        }
    }

    #endregion

    #region 标记药师功能

    [ScriptMethod(
        name: "标记药师",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:Echo", "Message:标记药师"]
    )]
    public void MarkChemists(Event @event, ScriptAccessory accessory)
    {
        try
        {
            if (@event["Message"] != "标记药师") return;

            lock (_commandLock)
            {
                long initGeneration = _initGeneration;
                long commandGeneration = ++_markCommandGeneration;
                var chemistIds = accessory.Data.Objects
                    .OfType<IPlayerCharacter>()
                    .Where(player => player.StatusList.Any(status => status.StatusId == 4367))
                    .Select(player => player.EntityId)
                    .Distinct()
                    .ToList();
                var markedIds = chemistIds.Take(8).ToList();

                if (EnableDeveloperMode) accessory.Log.Debug("检测到'标记药师'指令...");
                ScheduleChatLines(accessory, ["/e 标记药师：将清空公共头顶标记，并标记至多8名药师。"],
                    initGeneration, () => commandGeneration == _markCommandGeneration);
                accessory.Method.MarkClear();
                // 保留清空公共标记后等待1秒的语义；重置或新的标记命令会使整个旧批次失效。
                RunCommandDelayed(accessory, 1000, initGeneration, () => commandGeneration == _markCommandGeneration, () =>
                {
                    int markedCount = 0;
                    for (int i = 0; i < markedIds.Count; i++)
                    {
                        // 使用命令时的目标快照，但跳过等待期间已离开视野或不再是药师的玩家。
                        var player = accessory.Data.Objects.SearchById(markedIds[i]) as IPlayerCharacter;
                        if (player == null || !player.StatusList.Any(status => status.StatusId == 4367)) continue;
                        accessory.Method.Mark(player.EntityId, (MarkType)((int)MarkType.Attack1 + markedCount));
                        markedCount++;
                    }

                    var lines = new List<string> { $"/e 已标记 {markedCount} 名药师。" };
                    if (chemistIds.Count > 8)
                    {
                        lines.Add($"/e 共发现 {chemistIds.Count} 名药师，公共攻击标记仅支持8个，其余未标记。");
                    }
                    ScheduleChatLines(accessory, lines, initGeneration, () => commandGeneration == _markCommandGeneration);
                });
            }
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"MarkChemists error: {ex}");
        }
    }

    #endregion
}
