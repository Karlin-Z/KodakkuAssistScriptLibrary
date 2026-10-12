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
using Dalamud.Utility.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Codaaaaaa.TheForkedTowerMagic;

[ScriptType(
    guid: "45819e25-cb2d-4d84-a508-f110dc6a381a",
    name: "魔之塔画图",
    territorys: [1346],
    version: "0.0.1.11",
    author: "Codaaaaaa",
    note: "写完喽，还有电的可以在频道里圈我\n\n感谢铁虎老大的帮助\n感谢Yatel老大和洋葱炒米老大的arr")]
public class TheForkedTowerMagic
{
    #region 用户设置
    [UserSetting("双头决战：只能选中自己Buff对应的头")] public static bool DualHeadTargetLock { get; set; } = false;
    [UserSetting("排雷：显示塔内地雷点位（进图自动显示，/e 新月排雷 手动开关）")] public static bool 排雷显示 { get; set; } = true;
    [UserSetting("钟灵时钟：播报要打的钟灵 + 拉怪错误提醒")] public static bool 钟灵时钟启用 { get; set; } = false;
    [UserSetting("测试")] public static bool Debug输出 { get; set; } = false;
    // [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    #endregion

    private void Dbg(ScriptAccessory sa, string msg)
    {
        if (!Debug输出) return;
        sa.Method.SendChat($"/e [魔之塔] {msg}");
    }
    // 超魔自身会有Status 4228，普通没有
    private const uint 超魔Buff = 4228;
    private static bool Is超魔(ScriptAccessory sa)
        => sa.Data.Objects.Any(o => o is IBattleChara bc && bc.HasStatus(超魔Buff));
    // Map: 1136 魔之塔下层 Boss1
    // Map: 1178 魔之塔下层 小怪+Boss2
    // 换P
    private double _phase = 1.0;

    [ScriptMethod(name: "Set Phase 1", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP1"], userControl: false)]
    public void SetP1(Event evt, ScriptAccessory sa) => _phase = 1;

    [ScriptMethod(name: "Set Phase 2", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void SetP2(Event evt, ScriptAccessory sa) => _phase = 2;
    
    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP2"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Method.SendChat($"/e Phase: {_phase}");

    // 测试
    [ScriptMethod(name: "Debug - 19842状态查询", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:test"], userControl: false)]
    public void Debug19842状态(Event evt, ScriptAccessory sa)
    {
        var found = false;
        foreach (var obj in sa.Data.Objects)
        {
            if (obj is not IBattleChara bc || bc.DataId != 19307) continue;
            found = true;
            var statuses = bc.StatusList
                .Where(s => s.StatusId != 0)
                .Select(s => $"{s.StatusId}x{s.Param}({s.RemainingTime:F1}s)")
                .ToList();
            sa.Method.SendChat($"/e [19842] {bc.Name} {bc.EntityId:X8} pos {bc.Position:F1}: {(statuses.Count == 0 ? "无状态" : string.Join(", ", statuses))}");
        }
        if (!found) sa.Method.SendChat("/e [19842] 场上未找到该对象");
    }

    // 普魔
    #region Boss 1 双头

    private const uint 绿头Buff = 4192;      // 只能打绿头
    private const uint 蓝头Buff = 4194;      // 只能打蓝头
    private static readonly uint[] 绿头DataIds = [19474, 19481];   // 普魔 / 超魔
    private static readonly uint[] 蓝头DataIds = [19475, 19482];   // 普魔 / 超魔

    private bool _dualHeadLockOn = false;
    private int _dualHeadLockState = 0;

    private readonly object _ballLock = new();
    private readonly List<Vector3> _thunderBalls = [];  // 19478 雷球
    private readonly List<Vector3> _iceBalls = [];      // 19479 冰球

    // 特殊功能：决战只能选择对应buff的怪物。我抄我抄
    [ScriptMethod(name: "BOSS1 - 决战Buff目标限制", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(419[24])$"], userControl: true)]
    public void 双头决战Buff目标限制(Event evt, ScriptAccessory sa)
    {
        if (!DualHeadTargetLock) return;
        if (_dualHeadLockOn) return;
        _dualHeadLockOn = true;
        _dualHeadLockState = 0;
        sa.Method.RegistFrameworkUpdateAction(Action);

        void Action()
        {
            const int 打绿头 = 1;
            const int 打蓝头 = 2;
            const int 无限制 = 3;

            var myObject = sa.Data.MyObject;
            if (myObject is null) return;

            int currentState = (myObject.HasStatus(绿头Buff), myObject.HasStatus(蓝头Buff)) switch
            {
                (true, false) => 打绿头,
                (false, true) => 打蓝头,
                _ => 无限制
            };
            if (currentState == _dualHeadLockState) return;

            var green = sa.FindByDataId(绿头DataIds);
            var blue = sa.FindByDataId(蓝头DataIds);
            if (green is null || blue is null) return;   // 找不到目标则下帧重试
            _dualHeadLockState = currentState;

            switch (currentState)
            {
                case 打绿头:
                    sa.SetTargetable(green, true);
                    sa.SetTargetable(blue, false);
                    break;
                case 打蓝头:
                    sa.SetTargetable(green, false);
                    sa.SetTargetable(blue, true);
                    break;
                default:
                    sa.SetTargetable(green, true);
                    sa.SetTargetable(blue, true);
                    break;
            }
        }
    }

    // 47617 蓝头剧毒吐息：8s，EffectPosition 18m危险圈
    [ScriptMethod(name: "BOSS1 - 蓝头剧毒吐息", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47617"])]
    public void 蓝头剧毒吐息(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("蓝头剧毒吐息", evt.EffectPosition(), 8000, new Vector2(18f));
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 47616 绿头风暴吐息：8s，EffectPosition 上 CreateOmen 386
    [ScriptMethod(name: "BOSS1 - 绿头风暴吐息", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47616"])]
    public void 绿头风暴吐息(Event evt, ScriptAccessory sa)
    {
        sa.Method.VfxMethod.CreateOmen(530, new Vector3(30f),
            evt.EffectPosition(), evt.SourceRotation(), new Vector4(1f,1f,1f,0.2f), 8000);
    }

    // 50658 双头恐惧：TargetPosition为起点，按SourceRotation画6s矩形，长40宽10
    [ScriptMethod(name: "BOSS1 - 双头恐惧", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50658"])]
    public void 双头恐惧(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("双头恐惧", evt.TargetPosition, 6000, new Vector2(10f, 40f));
        dp.Rotation = evt.SourceRotation();
        // dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 5403 定时诅咒-东风：击退朝向(-1,0,0)，13s；5404 定时诅咒-西风：击退朝向(1,0,0)，13s
    [ScriptMethod(name: "BOSS1 - 定时诅咒击退", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(540[34])$"])]
    public void 定时诅咒击退(Event evt, ScriptAccessory sa)
    {
        if (evt.TargetId() != sa.Data.Me) return;
        var dir = evt.StatusId == 5403 ? new Vector3(-1, 0, 0) : new Vector3(1, 0, 0);

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"定时诅咒击退-{evt.StatusId}";
        dp.Owner = sa.Data.Me;
        dp.FixRotation = true;
        dp.Rotation = MathF.Atan2(dir.X, dir.Z);
        dp.Color = new Vector4(1f, 1f, 0f, 1f);
        dp.DestoryAt = 13000;
        dp.Scale = new Vector2(0.7f, 15f);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    // 19478 雷球出现，记录位置
    [ScriptMethod(name: "BOSS1 - 雷球位置记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19478"], userControl: false)]
    public void 雷球位置记录(Event evt, ScriptAccessory sa)
    {
        var pos = evt.SourcePosition();
        lock (_ballLock)
        {
            if (_thunderBalls.All(p => DistXZ(p, pos) > 1f))
                _thunderBalls.Add(pos);
        }
    }

    // 19479 冰球出现，记录位置
    [ScriptMethod(name: "BOSS1 - 冰球位置记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19479"], userControl: false)]
    public void 冰球位置记录(Event evt, ScriptAccessory sa)
    {
        var pos = evt.SourcePosition();
        lock (_ballLock)
        {
            if (_iceBalls.All(p => DistXZ(p, pos) > 1f))
                _iceBalls.Add(pos);
        }
    }

    // 50698 冰簇：EffectPosition 15m内的冰球位置画15m危险圈8s，并从list删除
    [ScriptMethod(name: "BOSS1 - 冰簇", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50698"])]
    public void 冰簇(Event evt, ScriptAccessory sa)
        => TriggerBallExplosion(sa, _iceBalls, evt.EffectPosition(), "冰簇爆炸");

    // 50697 雷簇：EffectPosition 15m内的雷球位置画15m危险圈8s，并从list删除
    [ScriptMethod(name: "BOSS1 - 雷簇", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50697"])]
    public void 雷簇(Event evt, ScriptAccessory sa)
        => TriggerBallExplosion(sa, _thunderBalls, evt.EffectPosition(), "雷簇爆炸");

    private void TriggerBallExplosion(ScriptAccessory sa, List<Vector3> balls, Vector3 effectPos, string name)
    {
        List<Vector3> triggered = [];
        lock (_ballLock)
        {
            triggered.AddRange(balls.Where(p => DistXZ(p, effectPos) <= 15f));
            balls.RemoveAll(p => DistXZ(p, effectPos) <= 15f);
        }
        for (var i = 0; i < triggered.Count; i++)
        {
            var dp = sa.FastDp($"{name}-{i}", triggered[i], 10000, new Vector2(15f));
            dp.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    // 47735 雷霜暴风雨(绿头19474/超魔19481)：所有剩余雷球/冰球位置画15m危险圈5s，然后清空两个list
    [ScriptMethod(name: "BOSS1 - 雷霜暴风雨", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47735", "SourceDataId:regex:^(19474|19481)$"])]
    public void 雷霜暴风雨(Event evt, ScriptAccessory sa)
    {
        List<Vector3> remains = [];
        lock (_ballLock)
        {
            remains.AddRange(_thunderBalls);
            remains.AddRange(_iceBalls);
            _thunderBalls.Clear();
            _iceBalls.Clear();
        }
        for (var i = 0; i < remains.Count; i++)
        {
            var dp = sa.FastDp($"雷霜暴风雨-{i}", remains[i], 7000, new Vector2(15f));
            dp.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    // 50703 冰焰凝环：先去EffectPosition朝me方向6m外(6s)，6s后tts"穿"，指路4m内(2s)
    [ScriptMethod(name: "BOSS1 - 冰焰凝环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50703|50704)$"])]
    public async void 冰焰凝环(Event evt, ScriptAccessory sa)
    {
        var center = evt.EffectPosition();

        var outPos = center + DirToMe(sa, center) * 6f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(outPos, 6000, 0, "冰焰凝环-外"));

        await Task.Delay(6000);

        sa.Method.TTS("穿", 3);
        var inPos = center + DirToMe(sa, center) * 4f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(inPos, 2000, 0, "冰焰凝环-内"));
    }
    // 50705 冰焰凝环：先去EffectPosition朝me方向6m外(6s)，6s后tts"穿"，指路4m内(2s)
    [ScriptMethod(name: "BOSS1 - 连续冰焰凝环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(50705)$"])]
    public async void 冰焰凝环2(Event evt, ScriptAccessory sa)
    {
        var center = evt.EffectPosition();

        var outPos = center + DirToMe(sa, center) * 6f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(outPos, 2000, 4000, "冰焰凝环-外"));

        await Task.Delay(6000);

        sa.Method.TTS("穿", 3);
        var inPos = center + DirToMe(sa, center) * 4f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(inPos, 2000, 0, "冰焰凝环-内"));
    }

    // 从center指向me的水平单位向量
    private static Vector3 DirToMe(ScriptAccessory sa, Vector3 center)
    {
        var mePos = sa.Data.MyObject?.Position ?? center;
        var dir = new Vector3(mePos.X - center.X, 0, mePos.Z - center.Z);
        return dir.Length() < 0.01f ? new Vector3(0, 0, 1) : Vector3.Normalize(dir);
    }

    private static float DistXZ(Vector3 a, Vector3 b)
        => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    #endregion

    #region BOSS2 剑舞者
    private static readonly Vector3 Boss2场中 = new(600f, -674f, 704f);
    private readonly object _leapLock = new();
    private readonly List<Vector3> _leapPositions = [];   // 49594 跃进步法落点，按触发顺序

    private readonly object _bladeRectLock = new();
    private readonly List<(Vector3 Pos, float Rot)> _bladeRects = [];   // 2015283 剑刃矩形，按触发顺序

    // 49585 半圆：SourcePosition为圆心，面对SourceRotation
    [ScriptMethod(name: "BOSS2 - 秘法剑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49585"])]
    public void 半圆斩(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"半圆斩(49585)：src {evt.SourceId():X8} pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        var dp = sa.FastDp($"半圆斩-{evt.SourceId()}", evt.SourcePosition(), 5500, new Vector2(96f));
        dp.Rotation = evt.SourceRotation();
        dp.Radian = MathF.PI;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "BOSS2 - 剑舞", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:1", "Id2:2"], userControl: false)]
    public void 剑刃矩形(Event evt, ScriptAccessory sa)
    {
        if (Is超魔(sa)) return;   // 该机制仅普通触发，超魔由舞动之剑预判处理
        var obj = sa.Data.Objects.SearchById(evt.SourceId());
        if (obj is null || obj.DataId != 2015283) return;

        var pos = evt.SourcePosition();
        var rot = evt.SourceRotation();

        List<(Vector3 Pos, float Rot)> rects;
        lock (_bladeRectLock)
        {
            if (_bladeRects.Any(r => DistXZ(r.Pos, pos) < 1f && MathF.Abs(WrapPi(r.Rot - rot)) < 0.1f)) return;
            _bladeRects.Add((pos, rot));
            Dbg(sa, $"剑刃矩形记录 #{_bladeRects.Count}：pos {pos:F1} rot {rot:F2}");
            if (_bladeRects.Count < 4) return;

            rects = [.. _bladeRects];
            _bladeRects.Clear();   // 用完清空以便下次使用
        }

        for (var i = 0; i < rects.Count; i++)
        {
            var delay = (uint)(i == 0 ? 0 : 6000 + (i - 1) * 2500);
            var duration = (uint)(i == 0 ? 6000 : 2500);
            foreach (var (extraRot, tag) in new[] { (0f, "正"), (MathF.PI, "反") })
            {
                var dp = sa.FastDp($"剑刃矩形-{i + 1}-{tag}", rects[i].Pos, duration, new Vector2(20f, 60f));
                dp.Rotation = rects[i].Rot + extraRot;
                dp.Delay = delay;
                dp.ScaleMode = ScaleMode.ByTime;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
    }

    // 49594 跃进步法：TargetDataId不是19830时，按触发顺序记录EffectPosition，排除(0,0,0)
    [ScriptMethod(name: "BOSS2 - 跃进步法记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(49596|49597)$"], userControl: false)]
    public void 跃进步法记录(Event evt, ScriptAccessory sa)
    {
        var pos = evt.EffectPosition();
        if (pos.Length() < 0.01f) return;   // 排除(0,0,0)

        lock (_leapLock)
        {
            if (_leapPositions.All(p => DistXZ(p, pos) > 1f))
            {
                _leapPositions.Add(pos);
                Dbg(sa, $"跃进步法记录 #{_leapPositions.Count}：{pos:F1}");
            }
        }
    }

    // 49685 剑技爆发：按顺序指路各落点
    [ScriptMethod(name: "BOSS2 - 剑技爆发指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49685"])]
    public void 剑技爆发指路(Event evt, ScriptAccessory sa)
    {
        List<Vector3> points;
        lock (_leapLock)
        {
            points = _leapPositions.Select(p => 朝场中偏移(p, 9f)).ToList();
            _leapPositions.Clear();   // 用完清空
        }
        if (points.Count == 0)
        {
            Dbg(sa, $"剑技爆发(49685)：落点list为空，跳过指路");
            return;
        }
        Dbg(sa, $"剑技爆发(49685)：共 {points.Count} 个落点，偏移后 [{string.Join(" | ", points.Select(p => p.ToString("F1")))}]");

        var green = new Vector4(0f, 1f, 0f, 1f);
        var white = new Vector4(1f, 1f, 1f, 1f);

        for (var k = 0; k < points.Count; k++)
        {
            // 第一段显示5s，之后每段3s
            var delay = (uint)(k == 0 ? 0 : 5000 + (k - 1) * 2500);
            var duration = (uint)(k == 0 ? 5000 : 2500);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                sa.WaypointDp(points[k], duration, delay, $"剑技爆发-me到{k + 1}", green));
            for (var j = k; j < points.Count - 1; j++)
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                    sa.WaypointFromToDp(points[j], points[j + 1], duration, delay, $"剑技爆发-{j + 1}到{j + 2}-阶段{k + 1}", white));
        }
    }

    // 将pos沿水平方向朝场中移动dist米
    private static Vector3 朝场中偏移(Vector3 pos, float dist)
    {
        var dir = new Vector3(Boss2场中.X - pos.X, 0, Boss2场中.Z - pos.Z);
        return dir.Length() < 0.01f ? pos : pos + Vector3.Normalize(dir) * dist;
    }

    // 将pos绕场中水平旋转rad弧度
    private static Vector3 绕场中旋转(Vector3 pos, float rad)
    {
        var dx = pos.X - Boss2场中.X;
        var dz = pos.Z - Boss2场中.Z;
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);
        return new Vector3(Boss2场中.X + dx * cos - dz * sin, pos.Y, Boss2场中.Z + dx * sin + dz * cos);
    }

    // 将pos绕场中旋转deg度，方向取远离awayFrom的一侧
    private static Vector3 绕场中旋转远离(Vector3 pos, Vector3 awayFrom, float deg = 10f)
    {
        var rad = deg * MathF.PI / 180f;
        var a = 绕场中旋转(pos, rad);
        var b = 绕场中旋转(pos, -rad);
        return DistXZ(a, awayFrom) >= DistXZ(b, awayFrom) ? a : b;
    }

    // 49616 突进：30m长6m宽矩形，4s
    [ScriptMethod(name: "BOSS2 - 突进", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49616"])]
    public void 突进(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"突进(49616)：pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        var dp = sa.FastDp("突进", evt.SourcePosition(), 4000, new Vector2(6f, 30f));
        dp.Rotation = evt.SourceRotation();
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    private const float 剑月环内半径 = 15f;
    private const float 剑月环外半径 = 40f;
    private long _b2SwordTtsAt;

    private void B2SwordTts(ScriptAccessory sa, string word)
    {
        var now = Environment.TickCount64;
        if (now - Interlocked.Exchange(ref _b2SwordTtsAt, now) < 800) return;   // 双剑同触发时只报一次
        sa.Method.TTS(word, 3);
    }

    private static unsafe int B2GetModelState(ScriptAccessory sa, uint entityId)
    {
        var obj = sa.Data.Objects.SearchById(entityId);
        if (obj is null || !obj.IsValid()) return -1;
        var c = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)obj.Address;
        return c->Timeline.ModelState;
    }

    private void B2DrawSwordAoe(ScriptAccessory sa, uint sid, Vector3 pos, bool isDonut, uint duration, uint delay=0)
    {
        if (isDonut)
        {
            var dp = sa.FastDp($"剑月环-{sid}-{delay}", pos, duration, new Vector2(剑月环外半径));
            dp.InnerScale = new Vector2(剑月环内半径);
            dp.Radian = float.Pi * 2;
            dp.Delay = delay;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
        else
        {
            var dp = sa.FastDp($"剑钢铁-{sid}-{delay}", pos, duration, new Vector2(15f));
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Delay = delay;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "BOSS2 - 舞动之剑预判", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:9710", "SourceDataId:19833"])]
    public void 舞动之剑预判(Event evt, ScriptAccessory sa)
    {
        var sid = evt.SourceId();
        var pose = B2GetModelState(sa, sid);
        Dbg(sa, $"舞动之剑9710：src {sid:X8} 姿势 {pose}");
        switch (pose)
        {
            case 0:     // → 小月环(内10外40)
            {
                var donut = sa.FastDp($"剑月环-{sid}-0", evt.SourcePosition(), 9000, new Vector2(40f));
                donut.InnerScale = new Vector2(10f);
                donut.Radian = float.Pi * 2;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, donut);
                break;
            }
            case 4:     // idle_sp_1 → 月环
                B2DrawSwordAoe(sa, sid, evt.SourcePosition(), true, 9000);
                break;
            case 5:     // idle_sp_2 → 大月环(内20外40)
            {
                var donut = sa.FastDp($"剑月环-{sid}-0", evt.SourcePosition(), 9000, new Vector2(40f));
                donut.InnerScale = new Vector2(20f);
                donut.Radian = float.Pi * 2;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, donut);
                break;
            }
            case 6:     // idle_sp_3 → 小钢铁(10)
            {
                var circle = sa.FastDp($"剑钢铁-{sid}-0", evt.SourcePosition(), 9000, new Vector2(10f));
                circle.ScaleMode = ScaleMode.ByTime;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, circle);
                break;
            }
            case 7:     // idle_sp_4 → 钢铁
                B2DrawSwordAoe(sa, sid, evt.SourcePosition(), false, 9000);
                break;
            case 31:    // → 大钢铁(20)
            {
                var circle = sa.FastDp($"剑钢铁-{sid}-0", evt.SourcePosition(), 9000, new Vector2(20f));
                circle.ScaleMode = ScaleMode.ByTime;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, circle);
                break;
            }
            default:    // 未知姿势
                Dbg(sa, $"舞动之剑9710：未知姿势{pose}，不绘图");
                break;
        }
    }

    #endregion

    #region BOSS3 惧死者
    // 47465 魔具联动-爆炎 / 47468 古代爆炎：5.5s，施法者上18m危险圈
    [ScriptMethod(name: "BOSS3 - 爆炎", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47465|47468)$"])]
    public void 爆炎(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"爆炎({evt.ActionId()})：src {evt.SourceId():X8}");
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"爆炎-{evt.SourceId()}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Owner = evt.SourceId();
        dp.DestoryAt = 5500;
        dp.Scale = new Vector2(18f);
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 47466 魔具联动-冰封 / 47469 古代冰封：5.5s，以施法者rotation为正面，前后左右各45长15宽的rect十字
    [ScriptMethod(name: "BOSS3 - 冰封十字", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47466|47469)$"])]
    public void 冰封十字(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"冰封十字({evt.ActionId()})：src {evt.SourceId():X8}");
        List<(float Rot, string Tag)> dirs = [(0f, "前"), (MathF.PI / 2, "左"), (MathF.PI, "后"), (-MathF.PI / 2, "右")];
        foreach (var (rot, tag) in dirs)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"冰封十字-{evt.SourceId()}-{tag}";
            dp.Color = sa.Data.DefaultDangerColor;
            dp.Owner = evt.SourceId();
            dp.Rotation = rot;
            dp.DestoryAt = 5500;
            dp.Scale = new Vector2(15f, 45f);
            // dp.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    // 47471 古代暴雷：5.5s，SourcePosition上以SourceRotation为正面的45度60m扇形
    [ScriptMethod(name: "BOSS3 - 古代暴雷", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47471"])]
    public void 古代暴雷(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"古代暴雷(47479)：pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        var dp = sa.FastDp($"古代暴雷-{evt.SourceId()}", evt.SourcePosition(), 5500, new Vector2(60f));
        dp.Rotation = evt.SourceRotation();
        dp.Radian = 45f * MathF.PI / 180f;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "BOSS3 - 灭亡射线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47475"])]
    public async void 骷髅头47475(Event evt, ScriptAccessory sa)
    {
        var srcId = evt.SourceId();
        var pos = evt.SourcePosition();

        await Task.Delay(1000);

        // 0.5s后取施法者当前实时朝向，取不到则回退用事件快照
        var srcObj = sa.Data.Objects.SearchById(srcId);
        var rot = srcObj?.Rotation ?? evt.SourceRotation();
        Dbg(sa, $"灭亡射线(47475)：pos {pos:F1} rot {rot:F2}（实时:{srcObj != null}）");

        var dp = sa.FastDp($"灭亡射线-{srcId}", pos, 4000, new Vector2(6f, 30f));
        dp.Rotation = rot;
        // dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 47477 黑暗奔流：EffectPosition 沿 SourceRotation 的 60长10宽矩形，CreateOmen 689（rect omen：X=半宽 Z=全长）
    [ScriptMethod(name: "BOSS3 - 黑暗奔流", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47477"])]
    public void B3黑暗奔流(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"黑暗奔流(47477)：pos {evt.EffectPosition():F1} rot {evt.SourceRotation():F2}");
        sa.Method.VfxMethod.CreateOmen(689, new Vector3(5f, 10f, 60f),
            evt.EffectPosition(), evt.SourceRotation(), null, 4000);
    }

    // 47477 读条开始后：左右两侧步进地火，60长10宽矩形，每轮向外步进10m，每轮画3.5s紧接下一轮
    [ScriptMethod(name: "BOSS3 - 黑暗奔流地火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47477"])]
    public void B3黑暗奔流地火(Event evt, ScriptAccessory sa)
    {
        var pos = evt.EffectPosition();
        var rot = evt.SourceRotation();
        Dbg(sa, $"黑暗奔流地火(47477)：pos {pos:F1} rot {rot:F2}");
        for (var i = 0; i < 2; i++)
        {
            foreach (var (sign, tag) in new[] { (1f, "左"), (-1f, "右") })
            {
                var side = rot + sign * MathF.PI / 2;
                var center = pos + new Vector3(MathF.Sin(side), 0f, MathF.Cos(side)) * (10f * (i + 1));
                var dp = sa.FastDp($"黑暗奔流地火-{tag}-{i}", center, 4500, new Vector2(10f, 60f));
                dp.Rotation = rot;
                dp.Delay = (uint)(3500 + i * 2000);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
    }

    #endregion

    #region BOSS4 目录

    // 48385 封印武器-竖琴：7s，15m危险圈；48387 封印武器-弓：7s，11m危险圈
    [ScriptMethod(name: "BOSS4 - 封印武器", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(48385|48387)$"])]
    public void 封印武器(Event evt, ScriptAccessory sa)
    {
        var isHarp = evt.ActionId() == 48385;
        var radius = isHarp ? 15f : 11f;
        Dbg(sa, $"封印武器-{(isHarp ? "竖琴" : "弓")}({evt.ActionId()})：pos {evt.EffectPosition():F1} 半径 {radius}");

        var dp = sa.FastDp($"封印武器-{evt.SourceId()}", evt.EffectPosition(), 7000, new Vector2(radius));
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private static readonly Vector3 Boss4中心 = new(0f, -684f, -628f);

    private readonly object _zoneLock = new();
    private float? _fireZoneRot;      // 2015240 火区朝向（上下对称）
    private float? _waterZoneRot;     // 2015241 水区朝向（上下对称）
    private float? _thunderZoneRot;   // 2015242 雷区朝向（上下对称）
    private readonly List<Vector3> _fireballPositions = [];      // 19301 火球，每波两个
    private readonly List<Vector3> _iceballPositions = [];       // 19300 冰球，每波两个
    private readonly List<Vector3> _thunderballPositions = [];   // 19302 雷球，每波两个

    // 2015240 火区 / 2015241 水区 / 2015242 雷区：记录SourceRotation，rotation及其对面就是对应元素区
    [ScriptMethod(name: "BOSS4 - 火区记录", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2015240"], userControl: false)]
    public void 火区记录(Event evt, ScriptAccessory sa)
    {
        lock (_zoneLock) _fireZoneRot = evt.SourceRotation();
        Dbg(sa, $"火区记录：rot {evt.SourceRotation():F2}");
    }

    [ScriptMethod(name: "BOSS4 - 水区记录", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2015241"], userControl: false)]
    public void 水区记录(Event evt, ScriptAccessory sa)
    {
        lock (_zoneLock) _waterZoneRot = evt.SourceRotation();
        Dbg(sa, $"水区记录：rot {evt.SourceRotation():F2}");
    }

    [ScriptMethod(name: "BOSS4 - 雷区记录", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2015242"], userControl: false)]
    public void 雷区记录(Event evt, ScriptAccessory sa)
    {
        lock (_zoneLock) _thunderZoneRot = evt.SourceRotation();
        Dbg(sa, $"雷区记录：rot {evt.SourceRotation():F2}");
    }

    // 元素球每波两个：收齐后按中心→球朝向顺时针转到对应区的角度算duration，
    // 在上下对应区各画一个60度30m扇形；duration多于3s时只在最后3s显示
    [ScriptMethod(name: "BOSS4 - 火球扇形", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19301"], userControl: false)]
    public void 火球扇形(Event evt, ScriptAccessory sa)
        => 元素球扇形(sa, _fireballPositions, () => _fireZoneRot, evt.SourceId(), evt.SourcePosition(), "火");

    [ScriptMethod(name: "BOSS4 - 冰球扇形", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19300"], userControl: false)]
    public void 冰球扇形(Event evt, ScriptAccessory sa)
        => 元素球扇形(sa, _iceballPositions, () => _waterZoneRot, evt.SourceId(), evt.SourcePosition(), "冰");

    [ScriptMethod(name: "BOSS4 - 雷球扇形", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19302"], userControl: false)]
    public void 雷球扇形(Event evt, ScriptAccessory sa)
        => 元素球扇形(sa, _thunderballPositions, () => _thunderZoneRot, evt.SourceId(), evt.SourcePosition(), "雷");

    private readonly Dictionary<ulong, DateTime> _ballSeenAt = [];

    private void 元素球扇形(ScriptAccessory sa, List<Vector3> balls, Func<float?> getZoneRot, ulong sourceId, Vector3 pos, string tag)
    {
        float zoneRot;
        lock (_zoneLock)
        {
            var now = DateTime.Now;
            if (_ballSeenAt.TryGetValue(sourceId, out var seen) && (now - seen).TotalSeconds < 20)
            {
                Dbg(sa, $"{tag}球 {sourceId:X8} 为{(now - seen).TotalSeconds:F1}s内复读事件，忽略");
                return;
            }
            _ballSeenAt[sourceId] = now;

            if (balls.Any(p => DistXZ(p, pos) <= 1f)) return;
            balls.Add(pos);
            Dbg(sa, $"{tag}球记录 #{balls.Count}：{pos:F1}");
            if (balls.Count < 2) return;

            pos = balls[0];
            balls.Clear();   // 收齐一波即清空，等下一波

            var rot = getZoneRot();
            if (rot is null)
            {
                Dbg(sa, $"{tag}球已收齐但{tag}区未记录，跳过");
                return;
            }
            zoneRot = rot.Value;
        }

        // 中心→球的朝向；该角度约定逆时针增加，顺时针转动即角度减小；区域上下对称，对180度取模
        var ballRad = MathF.Atan2(pos.X - Boss4中心.X, pos.Z - Boss4中心.Z);
        var deltaRad = ((ballRad - zoneRot) % MathF.PI + MathF.PI) % MathF.PI;
        var deltaDeg = deltaRad * 180f / MathF.PI;
        var duration = (uint)(7000 + deltaDeg / 30f * 1000f);
        // duration多于3s时加延迟，只在最后3s显示扇形
        var delay = duration > 3000 ? duration - 3000 : 0;
        var show = duration - delay;
        Dbg(sa, $"{tag}球→{tag}区顺时针 {deltaDeg:F1}度，duration {duration}ms（延迟{delay}ms后显示{show}ms）");

        foreach (var (rot, dirTag) in new[] { (zoneRot, "上"), (zoneRot + MathF.PI, "下") })
        {
            var dp = sa.FastDp($"{tag}区扇形-{dirTag}", Boss4中心, show, new Vector2(30f));
            dp.Delay = delay;
            dp.Rotation = rot;
            dp.Radian = 60f * MathF.PI / 180f;
            dp.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
        }
    }
    // 圆环 2015243火 / 2015244水(视为冰属性) / 2015245雷：出现7s后AOE。
    [ScriptMethod(name: "BOSS4 - 元素圆环换区", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:regex:^(201524[345])$"], suppress: 1000)]
    public void 元素圆环换区(Event evt, ScriptAccessory sa)
    {
        if (Is超魔(sa)) return;   // 普魔专用，超魔由下方方法处理（间隔更短）
        if (元素创造中()) return;   // 元素创造期间由安全区指路接管
        _ = 元素圆环换区核心(evt, sa, 5000);
    }

    private async Task 元素圆环换区核心(Event evt, ScriptAccessory sa, int waitMs)
    {
        if (!uint.TryParse(evt["DataId"], out var dataId)) return;
        var ringElement = dataId switch
        {
            2015243u => "火",
            2015244u => "冰",
            _ => "雷"
        };
        Dbg(sa, $"圆环出现：{ringElement}({dataId})，7s后AOE");

        await Task.Delay(waitMs);

        var sectors = 元素扇区中心();
        if (sectors is null)
        {
            Dbg(sa, $"圆环换区：元素区未记录齐，无法判断");
            return;
        }

        var mePos = sa.Data.MyObject?.Position ?? Boss4中心;
        var meRad = MathF.Atan2(mePos.X - Boss4中心.X, mePos.Z - Boss4中心.Z);
        var mine = sectors.OrderBy(s => MathF.Abs(WrapPi(s.Rot - meRad))).First();
        if (mine.Element != ringElement)
        {
            Dbg(sa, $"圆环{ringElement}，自己站在{mine.Element}区，忽略");
            return;
        }

        // 自己区左右两条交界取离自己近的一条，向邻区偏10度，中心往外9m
        var neighbors = sectors
            .Where(s => s.Element != mine.Element)
            .Select(s => (s.Element, Delta: WrapPi(s.Rot - mine.Rot)))
            .ToList();
        var pick = WrapPi(meRad - mine.Rot) >= 0
            ? neighbors.Where(n => n.Delta > 0).OrderBy(n => n.Delta).First()
            : neighbors.Where(n => n.Delta < 0).OrderByDescending(n => n.Delta).First();
        var boundary = mine.Rot + pick.Delta / 2f;
        var guide = boundary + 10f * MathF.PI / 180f * MathF.Sign(pick.Delta);
        var guidePos = Boss4中心 + new Vector3(MathF.Sin(guide), 0, MathF.Cos(guide)) * 9f;

        var showMs = (uint)Math.Max(7000 - waitMs, 1000);
        Dbg(sa, $"圆环{ringElement}命中所在区，指路穿到{pick.Element}区交界：{guidePos:F1}");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(guidePos, showMs, 0, "元素圆环穿区指路"));

        sa.Method.TTS("穿", 3);
        sa.Method.TextInfo("穿", (int)showMs, false);
    }

    // 6个扇区中心朝向（每元素上下各一个）；任一元素区未记录则返回null
    private List<(float Rot, string Element)>? 元素扇区中心()
    {
        lock (_zoneLock)
        {
            if (_fireZoneRot is null || _waterZoneRot is null || _thunderZoneRot is null) return null;
            List<(float Rot, string Element)> sectors = [];
            foreach (var (rot, elem) in new[] { (_fireZoneRot.Value, "火"), (_waterZoneRot.Value, "冰"), (_thunderZoneRot.Value, "雷") })
            {
                sectors.Add((rot, elem));
                sectors.Add((rot + MathF.PI, elem));
            }
            return sectors;
        }
    }

    private static float WrapPi(float rad)
    {
        rad %= 2f * MathF.PI;
        if (rad > MathF.PI) rad -= 2f * MathF.PI;
        if (rad < -MathF.PI) rad += 2f * MathF.PI;
        return rad;
    }

    #endregion

    // 超魔
    #region 超魔Boss 1 双头

    // 47639 蓝头剧毒吐息：9s，EffectPosition 18m危险圈
    [ScriptMethod(name: "超魔BOSS1 - 蓝头剧毒吐息", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47639"])]
    public void 超魔蓝头剧毒吐息(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("超魔蓝头剧毒吐息", evt.EffectPosition(), 9000, new Vector2(18f));
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 47638 绿头风暴吐息：9s，EffectPosition 上 CreateOmen 386
    [ScriptMethod(name: "超魔BOSS1 - 绿头风暴吐息", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47638"])]
    public void 超魔绿头风暴吐息(Event evt, ScriptAccessory sa)
    {
        sa.Method.VfxMethod.CreateOmen(530, new Vector3(30f),
            evt.EffectPosition(), evt.SourceRotation(), new Vector4(1f,1f,1f,0.2f), 8000);
    }

    // 47640 绿头雷电赋格：9s，EffectPosition 月环 内15外60
    [ScriptMethod(name: "超魔BOSS1 - 绿头雷电赋格", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47640"])]
    public void 超魔绿头雷电赋格(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("超魔绿头雷电赋格", evt.EffectPosition(), 9000, new Vector2(60f));
        dp.InnerScale = new Vector2(18f);
        dp.Radian = float.Pi * 2;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    // 47641 蓝头冰柱赋格：9s，EffectPosition 20m危险圈，Imgui
    [ScriptMethod(name: "超魔BOSS1 - 蓝头冰柱赋格", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47641"])]
    public void 超魔蓝头冰柱赋格(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("超魔蓝头冰柱赋格", evt.EffectPosition(), 9000, new Vector2(20f));
        dp.Color = new Vector4(1f, 0f, 0f, 1f);
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }

    // 47685 冰焰交错：2s，SourcePosition上以SourceRotation为正面，前后左右各35长10宽的rect十字
    [ScriptMethod(name: "超魔BOSS1 - 冰焰交错", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47685"])]
    public void 冰焰交错(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"冰焰交错(47685)：pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        List<(float Rot, string Tag)> dirs = [(0f, "前"), (MathF.PI / 2, "左"), (MathF.PI, "后"), (-MathF.PI / 2, "右")];
        foreach (var (rot, tag) in dirs)
        {
            var dp = sa.FastDp($"冰焰交错-{tag}", evt.SourcePosition(), 2000, new Vector2(10f, 35f));
            dp.Rotation = evt.SourceRotation() + rot;
            // dp.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    // 47686 冰焰凝环：2s，SourcePosition 月环 内10外60
    [ScriptMethod(name: "超魔BOSS1 - 冰焰凝环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47686"])]
    public void 超魔冰焰凝环(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("超魔冰焰凝环", evt.SourcePosition(), 2000, new Vector2(60f));
        dp.InnerScale = new Vector2(5f);
        dp.Radian = float.Pi * 2;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    private const uint 小绿头DataId = 19490;
    private const uint 小蓝头DataId = 19491;
    private readonly object _smallHeadLock = new();
    private readonly List<(uint SourceId, uint DataId)> _smallHeads = [];   // 19491蓝 / 19490绿 小双头

    // AddCombatant DataId 19491（蓝） and 19490 (绿)，记录SourceId and DataId list
    [ScriptMethod(name: "超魔BOSS1 - 魔法阵记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1949[01])$"], userControl: false)]
    public void 超魔小双头记录(Event evt, ScriptAccessory sa)
    {
        if (!uint.TryParse(evt["DataId"], out var dataId)) return;
        var sid = evt.SourceId();
        lock (_smallHeadLock)
        {
            if (_smallHeads.Any(h => h.SourceId == sid)) return;
            _smallHeads.Add((sid, dataId));
            Dbg(sa, $"小双头记录 #{_smallHeads.Count}：{(dataId == 小蓝头DataId ? "蓝" : "绿")} {sid:X8}");
        }
    }

    [ScriptMethod(name: "超魔BOSS1 - 小双头恐惧", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47703"])]
    public void 超魔小双头恐惧(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("双头恐惧", evt.TargetPosition, 7000, new Vector2(10f, 40f));
        dp.Rotation = evt.SourceRotation();
        // dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 47702 大双头恐惧：SourceName判色（Green/绿 或 Blue/蓝），
    // 在已记录的对应颜色小双头身上以其rotation为正，画正反两个rect 60长5宽 6s
    [ScriptMethod(name: "超魔BOSS1 - 大双头恐惧", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47702"])]
    public void 超魔大双头恐惧(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("双头恐惧", evt.TargetPosition, 7000, new Vector2(20f, 40f));
        dp.Rotation = evt.SourceRotation();
        // dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        var srcName = evt["SourceName"] ?? "";
        uint? headDataId = null;
        if (srcName.Contains("Green", StringComparison.OrdinalIgnoreCase) || srcName.Contains('绿'))
            headDataId = 小绿头DataId;
        else if (srcName.Contains("Blue", StringComparison.OrdinalIgnoreCase) || srcName.Contains('蓝'))
            headDataId = 小蓝头DataId;
        if (headDataId is null)
        {
            Dbg(sa, $"大双头恐惧(47702)：SourceName [{srcName}] 无法判色，跳过小双头直线");
            return;
        }

        List<uint> heads;
        lock (_smallHeadLock)
        {
            heads = _smallHeads.Where(h => h.DataId == headDataId).Select(h => h.SourceId).ToList();
            _smallHeads.Clear();   // 用完清空以便下一波重新记录
        }
        Dbg(sa, $"大双头恐惧(47702)：{(headDataId == 小蓝头DataId ? "蓝" : "绿")}色小双头 {heads.Count} 个");

        foreach (var sid in heads)
        {
            foreach (var (extraRot, tag) in new[] { (0f, "正"), (MathF.PI, "反") })
            {
                var lineDp = sa.Data.GetDefaultDrawProperties();
                lineDp.Name = $"大双头恐惧直线-{sid:X8}-{tag}";
                lineDp.Color = sa.Data.DefaultDangerColor;
                lineDp.Owner = sid;
                lineDp.Rotation = extraRot;
                lineDp.DestoryAt = 7000;
                lineDp.Scale = new Vector2(5f, 60f);
                // lineDp.ScaleMode = ScaleMode.ByTime;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, lineDp);
            }
        }
    }

    private const uint 大绿头DataId = 19483;   // 绿=雷
    private const uint 大蓝头DataId = 19484;   // 蓝=冰
    private readonly object _conduitLock = new();
    private readonly List<(bool IsThunder, Vector3 Pos, float Rot)> _conduitBalls = [];   // 19487导流雷球 / 19488导流冰球
    private readonly Dictionary<uint, bool> _mahjongTethers = [];                         // Tether 019B：TargetId → 是否绿头(雷)
    private readonly List<(bool IsGreen, Vector3 Pos, int Index, long At)> _mahjongMarks = [];   // 麻将点名记录：头颜色/点名者位置/麻将几
    private readonly List<(Vector3 Pos, float Rot, long At)> _mahjongR1Fans = [];         // 麻将1轮命中的导流球扇形，用于四点安全区判断
    private static readonly Vector3 麻将中心 = new(-900f, -980f, 700f);                    // 麻将四点指路：中心±5四角

    // AddCombatant 19487导流雷球 / 19488导流冰球：记录属性、SourcePos、SourceRotation
    [ScriptMethod(name: "超魔BOSS1 - 导流球记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1948[78])$"], userControl: false)]
    public void 超魔导流球记录(Event evt, ScriptAccessory sa)
    {
        if (!uint.TryParse(evt["DataId"], out var dataId)) return;
        var isThunder = dataId == 19487u;
        var pos = evt.SourcePosition();
        var rot = evt.SourceRotation();
        lock (_conduitLock)
        {
            if (_conduitBalls.Any(b => DistXZ(b.Pos, pos) < 1f)) return;
            _conduitBalls.Add((isThunder, pos, rot));
            Dbg(sa, $"导流{(isThunder ? "雷" : "冰")}球记录 #{_conduitBalls.Count}：pos {pos:F1} rot {rot:F2}");
        }
    }

    // Tether 019B：SourceId是大蓝头19484或大绿头19483，记录TargetId对应的头颜色。一次机制两条线，一蓝一绿
    [ScriptMethod(name: "超魔BOSS1 - 麻将连线记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:019B"], userControl: false)]
    public void 超魔麻将连线记录(Event evt, ScriptAccessory sa)
    {
        var srcObj = sa.Data.Objects.SearchById(evt.SourceId());
        if (srcObj is null || (srcObj.DataId != 大绿头DataId && srcObj.DataId != 大蓝头DataId))
        {
            Dbg(sa, $"麻将连线(019B)：SourceId {evt.SourceId():X8} 不是大双头，忽略");
            return;
        }
        var isGreen = srcObj.DataId == 大绿头DataId;
        lock (_conduitLock) _mahjongTethers[evt.TargetId()] = isGreen;
        Dbg(sa, $"麻将连线(019B)：{(isGreen ? "绿" : "蓝")}头 → {evt.TargetId():X8}");
    }

    // TargetIcon 02D2~02D5 麻将1~4，各触发两次(一蓝一绿)：
    // 麻将1立即画15s；麻将2/3/4等15s后画4s。
    // 点名者位置画15m危险圈；15m内同属性导流球(绿头=雷19487/蓝头=冰19488)画60度60m扇形并从list删除
    [ScriptMethod(name: "超魔BOSS1 - 麻将1", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:02D2"])]
    public void 超魔麻将1(Event evt, ScriptAccessory sa) => 麻将处理(evt, sa, 1, 0, 17000);

    [ScriptMethod(name: "超魔BOSS1 - 麻将2", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:02D3"])]
    public void 超魔麻将2(Event evt, ScriptAccessory sa) => 麻将处理(evt, sa, 2, 12500, 4000);

    [ScriptMethod(name: "超魔BOSS1 - 麻将3", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:02D4"])]
    public void 超魔麻将3(Event evt, ScriptAccessory sa) => 麻将处理(evt, sa, 3, 12000, 4000);

    [ScriptMethod(name: "超魔BOSS1 - 麻将4", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:02D5"])]
    public void 超魔麻将4(Event evt, ScriptAccessory sa) => 麻将处理(evt, sa, 4, 11500, 4000);

    private async void 麻将处理(Event evt, ScriptAccessory sa, int index, int delayMs, uint duration)
    {
        var sid = evt.SourceId();
        bool isGreen;
        bool found;
        lock (_conduitLock) found = _mahjongTethers.TryGetValue(sid, out isGreen);
        if (!found)
        {
            Dbg(sa, $"麻将{index}：{sid:X8} 没有对应的连线记录，跳过");
            return;
        }

        var obj = sa.Data.Objects.SearchById(sid);
        var pos = obj?.Position ?? evt.SourcePosition();
        lock (_conduitLock)
        {
            _mahjongMarks.RemoveAll(m => Environment.TickCount64 - m.At > 60000);
            _mahjongMarks.Add((isGreen, pos, index, Environment.TickCount64));
        }
        Dbg(sa, $"麻将{index}记录：{(isGreen ? "绿(雷)" : "蓝(冰)")} {sid:X8} pos {pos:F1}");

        if (delayMs > 0) await Task.Delay(delayMs);

        var dp = sa.FastDp($"麻将{index}圈-{sid:X8}", pos, duration, new Vector2(15f));
        // dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        // 15m内同属性导流球画扇形并删除
        List<(bool IsThunder, Vector3 Pos, float Rot)> hit;
        lock (_conduitLock)
        {
            hit = _conduitBalls.Where(b => b.IsThunder == isGreen && DistXZ(b.Pos, pos) <= 15f).ToList();
            _conduitBalls.RemoveAll(b => b.IsThunder == isGreen && DistXZ(b.Pos, pos) <= 15f);
        }
        Dbg(sa, $"麻将{index}：命中导流{(isGreen ? "雷" : "冰")}球 {hit.Count} 个");

        if (index == 1)
            lock (_conduitLock)
            {
                _mahjongR1Fans.RemoveAll(f => Environment.TickCount64 - f.At > 60000);
                _mahjongR1Fans.AddRange(hit.Select(h => (h.Pos, h.Rot, Environment.TickCount64)));
            }

        for (var i = 0; i < hit.Count; i++)
        {
            var fan = sa.FastDp($"麻将{index}扇形-{i}", hit[i].Pos, duration, new Vector2(60f));
            fan.Rotation = hit[i].Rot;
            fan.Radian = 50f * MathF.PI / 180f;
            // fan.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, fan);
        }
    }

    // —— 麻将四点指路 ——
    [ScriptMethod(name: "超魔BOSS1 - 麻将指路", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:02D3"], userControl: false, suppress: 5000)]
    public async void 超魔麻将指路(Event evt, ScriptAccessory sa)
    {
        List<(bool IsGreen, Vector3 Pos, int Index, long At)> marks = [];
        for (var i = 0; i < 30 && marks.Count == 0; i++)
        {
            lock (_conduitLock)
            {
                var now = Environment.TickCount64;
                var fresh = _mahjongMarks.Where(m => now - m.At < 30000).ToList();
                if (fresh.Count(m => m.Index == 1) >= 2 && fresh.Count(m => m.Index == 2) >= 2) marks = fresh;
            }
            if (marks.Count == 0) await Task.Delay(100);
        }
        if (marks.Count == 0)
        {
            Dbg(sa, "麻将指路：3s内麻将1/2点名记录不齐，跳过");
            return;
        }

        List<(Vector3 Pos, float Rot)> fans;
        lock (_conduitLock)
            fans = _mahjongR1Fans.Where(f => Environment.TickCount64 - f.At < 30000)
                .Select(f => (f.Pos, f.Rot)).ToList();

        var r1 = marks.Where(m => m.Index == 1).ToList();
        var r2 = marks.Where(m => m.Index == 2).ToList();

        // 首轮安全角 = 不被麻将1的15m圈覆盖、也不在其扇形(60m/50°)内的那个角
        Vector2[] cornerVecs = [new(-5, -5), new(5, -5), new(-5, 5), new(5, 5)];
        Vector3 角点(Vector2 v) => 麻将中心 + new Vector3(v.X, 0, v.Y);
        string 角名(Vector2 v) => (v.X < 0 ? "左" : "右") + (v.Y < 0 ? "上" : "下");
        bool Covered(Vector3 c) =>
            r1.Any(m => DistXZ(m.Pos, c) <= 15f) ||
            fans.Any(f => DistXZ(f.Pos, c) <= 60f &&
                          MathF.Abs(WrapPi(MathF.Atan2(c.X - f.Pos.X, c.Z - f.Pos.Z) - f.Rot)) <= 25f * MathF.PI / 180f);
        var safeVecs = cornerVecs.Where(v => !Covered(角点(v))).ToList();
        if (safeVecs.Count == 0)
        {
            Dbg(sa, $"麻将指路：四角全被覆盖（圈{r1.Count} 扇{fans.Count}），跳过");
            return;
        }
        if (safeVecs.Count > 1)
            Dbg(sa, $"麻将指路：安全角不唯一({string.Join("/", safeVecs.Select(角名))})，取第一个");
        var v1 = safeVecs[0];

        // 同色点名左右是否换边；两色判定不一致时以绿头为准
        bool? 换边(bool green)
        {
            var a = r1.Where(m => m.IsGreen == green).ToList();
            var b = r2.Where(m => m.IsGreen == green).ToList();
            if (a.Count == 0 || b.Count == 0) return null;
            return (a[^1].Pos.X < 麻将中心.X) != (b[^1].Pos.X < 麻将中心.X);
        }
        var 绿换 = 换边(true);
        var 蓝换 = 换边(false);
        if (绿换 is null && 蓝换 is null)
        {
            Dbg(sa, "麻将指路：两色点名记录不全，无法判断交叉/平行，跳过");
            return;
        }
        if (绿换 is not null && 蓝换 is not null && 绿换 != 蓝换)
            Dbg(sa, $"麻将指路：两色换边判定不一致 绿{绿换} 蓝{蓝换}，以绿头为准");
        var 交叉 = 绿换 ?? 蓝换!.Value;

        // 交叉→左右翻，平行→前后翻；叉积定顺逆，之后按同向每轮转90°
        var v2 = 交叉 ? new Vector2(-v1.X, v1.Y) : new Vector2(v1.X, -v1.Y);
        var cw = v1.X * v2.Y - v1.Y * v2.X > 0;
        Vector2 下一角(Vector2 v) => cw ? new Vector2(-v.Y, v.X) : new Vector2(v.Y, -v.X);
        var v3 = 下一角(v2);
        var v4 = 下一角(v3);

        Dbg(sa, $"麻将指路：{(交叉 ? "交叉" : "平行")}，{角名(v1)}→{角名(v2)}→{角名(v3)}→{角名(v4)}，{(cw ? "顺" : "逆")}时针");
        sa.Method.TextInfo($"初始安全区{角名(v1)}，{(cw ? "顺" : "逆")}时针跑", 15000, false);

        (Vector2 V, uint Dur, uint Delay)[] wps = [(v1, 12500, 0), (v2, 4000, 12500), (v3, 4000, 16500), (v4, 4000, 20500)];
        for (var i = 0; i < wps.Length; i++)
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                sa.WaypointDp(角点(wps[i].V), wps[i].Dur, wps[i].Delay, $"麻将指路-{i + 1}"));
    }

    // 赋格触发对应属性魔法阵(19490绿=雷/19491蓝=冰)：正反两个rect 60长5宽，只清除被影响颜色的记录
    private void 触发魔法阵直线(ScriptAccessory sa, uint headDataId, string name, uint delay, uint duration)
    {
        List<uint> heads;
        lock (_smallHeadLock)
        {
            heads = _smallHeads.Where(h => h.DataId == headDataId).Select(h => h.SourceId).ToList();
            _smallHeads.RemoveAll(h => h.DataId == headDataId);   // 只清除对应颜色
        }

        // 兜底：AddCombatant大量同帧刷新时事件可能被丢，扫一遍对象表补上漏记的
        var missed = sa.Data.Objects
            .Where(o => o != null && o.DataId == headDataId && !heads.Contains(o.EntityId))
            .Select(o => o.EntityId)
            .ToList();
        if (missed.Count > 0)
        {
            heads.AddRange(missed);
            Dbg(sa, $"{name}：对象表补漏 {missed.Count} 个 [{string.Join(" ", missed.Select(m => m.ToString("X8")))}]");
        }
        Dbg(sa, $"{name}：{(headDataId == 小蓝头DataId ? "蓝" : "绿")}色魔法阵 {heads.Count} 个");

        foreach (var sid in heads)
        {
            foreach (var (extraRot, tag) in new[] { (0f, "正"), (MathF.PI, "反") })
            {
                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"{name}直线-{sid:X8}-{tag}";
                dp.Color = sa.Data.DefaultDangerColor;
                dp.Owner = sid;
                dp.Rotation = extraRot;
                dp.Delay = delay;
                dp.DestoryAt = duration;
                dp.Scale = new Vector2(5f, 60f);
                // dp.ScaleMode = ScaleMode.ByTime;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
    }

    // 50727 前雷电赋格：11s，EffectPosition 月环 内15外60 + 绿色魔法阵直线11s
    [ScriptMethod(name: "超魔BOSS1 - 前雷电赋格", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50727"])]
    public void 超魔前雷电赋格(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("超魔前雷电赋格", evt.EffectPosition(), 11000, new Vector2(60f));
        dp.InnerScale = new Vector2(18f);
        dp.Radian = float.Pi * 2;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

        触发魔法阵直线(sa, 小绿头DataId, "前雷电赋格", 0, 11000);
    }

    // 50728 前冰柱赋格：11s，EffectPosition 钢铁 20m + 蓝色魔法阵直线11s
    [ScriptMethod(name: "超魔BOSS1 - 前冰柱赋格", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50728"])]
    public void 超魔前冰柱赋格(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("超魔前冰柱赋格", evt.EffectPosition(), 11000, new Vector2(20f));
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        触发魔法阵直线(sa, 小蓝头DataId, "前冰柱赋格", 0, 11000);
    }

    // 47629 后雷电赋格：延迟11s后显示4s，EffectPosition 月环 内15外60 + 绿色魔法阵直线(同延迟)
    [ScriptMethod(name: "超魔BOSS1 - 后雷电赋格", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47629"])]
    public void 超魔后雷电赋格(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("超魔后雷电赋格", evt.EffectPosition(), 4000, new Vector2(60f));
        dp.Delay = 7000;
        dp.InnerScale = new Vector2(18f);
        dp.Radian = float.Pi * 2;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

        触发魔法阵直线(sa, 小绿头DataId, "后雷电赋格", 7000, 4000);
    }

    // 47630 后冰柱赋格：延迟11s后显示4s，EffectPosition 钢铁 20m + 蓝色魔法阵直线(同延迟)
    [ScriptMethod(name: "超魔BOSS1 - 后冰柱赋格", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47630"])]
    public void 超魔后冰柱赋格(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp("超魔后冰柱赋格", evt.EffectPosition(), 4000, new Vector2(20f));
        dp.Delay = 7000;
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        触发魔法阵直线(sa, 小蓝头DataId, "后冰柱赋格", 7000, 4000);
    }

    // —— 位移吐息：自己身上的status决定方向 ——
    private static int B1位移吐息时长(uint actionId) => actionId == 50708 ? 6000 : 11000;

    // SourceName判色（Green/绿 或 Blue/蓝）；统一只画最后3s，只有delay随读条长短不同
    [ScriptMethod(name: "超魔BOSS1 - 位移吐息", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^5070[6-8]$"])]
    public async void 超魔B1位移吐息(Event evt, ScriptAccessory sa)
    {
        var actionId = evt.ActionId();
        const int duration = 3000;
        await Task.Delay(B1位移吐息时长(actionId) - duration);

        var me = sa.Data.MyObject;
        if (me is null) return;

        var srcName = evt["SourceName"] ?? "";
        var isGreen = srcName.Contains("Green", StringComparison.OrdinalIgnoreCase) || srcName.Contains('绿');
        var isBlue = srcName.Contains("Blue", StringComparison.OrdinalIgnoreCase) || srcName.Contains('蓝');

        uint statusId = 0;
        if (isBlue) statusId = me.HasStatus(5054) ? 5054u : me.HasStatus(5055) ? 5055u : 0u;
        else if (isGreen) statusId = me.HasStatus(5052) ? 5052u : me.HasStatus(5053) ? 5053u : 0u;
        if (statusId == 0)
        {
            Dbg(sa, $"位移吐息({actionId})：SourceName [{srcName}] 蓝{isBlue} 绿{isGreen}，无对应status，跳过");
            return;
        }

        var isLeft = statusId is 5054 or 5052;
        var pos = isLeft ? new Vector3(-880f, -980f, 700f) : new Vector3(-920f, -980f, 700f);
        var rot = isLeft ? -MathF.PI / 2 : MathF.PI / 2;
        Dbg(sa, $"位移吐息({actionId}+{statusId})：{(isBlue ? "蓝头" : "绿头")}向{(isLeft ? "左" : "右")}位移");
        sa.Method.VfxMethod.CreateOmen(314, new Vector3(20f, 10f, 40f),
            pos, rot, new Vector4(1f, 1f, 1f, 0.3f), duration);
    }

    // —— 十字/月环连招预告 ——
    // 绿头47671~47674 / 蓝头47675~47678，各自含两轮：两次十字/两次月环/十字月环/月环十字。
    // 先读条的头占第1、3轮，后读条的头占第2、4轮，两头都到齐后TextInfo汇总四轮。
    // 自己带哪个头的位移吐息buff(绿5052/5053、蓝5054/5055)，该头的轮次前加[击退]
    private readonly object _b1ComboLock = new();
    private (bool IsGreen, string[] Skills, long At)? _b1ComboFirst;

    [ScriptMethod(name: "超魔BOSS1 - 十字月环连招预告", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^4767[1-8]$"])]
    public void 超魔B1十字月环预告(Event evt, ScriptAccessory sa)
    {
        var actionId = evt.ActionId();
        var isGreen = actionId <= 47674;
        string[] skills = (actionId - (isGreen ? 47671u : 47675u)) switch
        {
            0 => ["十字", "十字"],
            1 => ["月环", "月环"],
            2 => ["十字", "月环"],
            _ => ["月环", "十字"],
        };

        (bool IsGreen, string[] Skills, long At) first;
        lock (_b1ComboLock)
        {
            var now = Environment.TickCount64;
            // 没有先手记录、记录过期、或同一头重复读条：当作本波先手，等另一头
            if (_b1ComboFirst is not { } f || now - f.At > 30000 || f.IsGreen == isGreen)
            {
                _b1ComboFirst = (isGreen, skills, now);
                Dbg(sa, $"十字月环({actionId})：{(isGreen ? "绿" : "蓝")}头先手 {skills[0]}+{skills[1]}，等另一头");
                return;
            }
            first = f;
            _b1ComboFirst = null;
        }

        var me = sa.Data.MyObject;
        var 绿击退 = me is not null && (me.HasStatus(5052) || me.HasStatus(5053));
        var 蓝击退 = me is not null && (me.HasStatus(5054) || me.HasStatus(5055));
        string Mark(bool green, string skill) => (green ? 绿击退 : 蓝击退) ? $"[击退]{skill}" : skill;

        var text = string.Join("→", new[]
        {
            Mark(first.IsGreen, first.Skills[0]),
            Mark(isGreen, skills[0]),
            Mark(first.IsGreen, first.Skills[1]),
            Mark(isGreen, skills[1]),
        });
        Dbg(sa, $"十字月环({actionId})：{(isGreen ? "绿" : "蓝")}头后手，汇总 {text}");
        sa.Method.TextInfo(text, 20000, false);
    }
    #endregion

    #region 超魔BOSS2

    private readonly object _b2ChargeLock = new();
    private readonly List<(uint Sid, Vector3 Pos, float Rot, long At)> _b2剑Casts = [];   // 49645秘法剑读条记录，用于突进配对

    private static float AngleDiff(float a, float b) => MathF.Abs(MathF.Atan2(MathF.Sin(a - b), MathF.Cos(a - b)));

    // 49622：3.5s。若有面向刚好相对的秘法剑(49645)读条者，长度=到它的距离-4，否则48
    [ScriptMethod(name: "超魔BOSS2 - 突进", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49622"])]
    public async void 超魔B2突进(Event evt, ScriptAccessory sa)
    {
        var sid = evt.SourceId();
        var pos = evt.SourcePosition();
        var rot = evt.SourceRotation();

        void Draw(float length, uint duration)
        {
            foreach (var (extraRot, tag) in new[] { (0f, "正"), (MathF.PI, "反") })
            {
                var dp = sa.FastDp($"超魔B2突进-{sid}-{tag}", pos, duration, new Vector2(7f, length));
                dp.Rotation = rot + extraRot;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }

        await Task.Delay(300);

        var length = 48f;
        lock (_b2ChargeLock)
        {
            var now = Environment.TickCount64;
            _b2剑Casts.RemoveAll(c => now - c.At > 8000);
            var idx = _b2剑Casts.FindIndex(c => AngleDiff(c.Rot, rot + MathF.PI) < 0.3f);
            if (idx >= 0)
            {
                length = DistXZ(pos, _b2剑Casts[idx].Pos) - 5;
                Dbg(sa, $"突进(49622)：{sid:X8} 与秘法剑 {_b2剑Casts[idx].Sid:X8} 面向相对，长度 {length:F1}");
                _b2剑Casts.RemoveAt(idx);
            }
            else Dbg(sa, $"突进(49622)：{sid:X8} 没有面向相对的秘法剑，长度48");
        }
        Draw(length, 3200);
    }

    // 49635：3.5s
    [ScriptMethod(name: "超魔BOSS2 - 小回旋", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49635"])]
    public void 超魔B2小回旋(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp($"超魔B2小回旋-{evt.SourceId()}", evt.SourcePosition(), 3500, new Vector2(14f));
        dp.InnerScale = new Vector2(9f);
        dp.Rotation = evt.SourceRotation() - MathF.PI / 4;
        dp.Radian = float.Pi / 2;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    [ScriptMethod(name: "超魔BOSS2 - 中回旋", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49636"])]
    public void 超魔B2中回旋(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp($"超魔B2中回旋-{evt.SourceId()}", evt.SourcePosition(), 3500, new Vector2(19f));
        dp.InnerScale = new Vector2(14f);
        dp.Rotation = evt.SourceRotation() - MathF.PI / 4;
        dp.Radian = float.Pi / 2;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    // 49637：3.5s
    [ScriptMethod(name: "超魔BOSS2 - 大回旋", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49637"])]
    public void 超魔B2大回旋(Event evt, ScriptAccessory sa)
    {
        var dp = sa.FastDp($"超魔B2大回旋-{evt.SourceId()}", evt.SourcePosition(), 3500, new Vector2(24f));
        dp.InnerScale = new Vector2(19f);
        dp.Rotation = evt.SourceRotation() - MathF.PI / 4;
        dp.Radian = float.Pi / 2;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    // 49616 突进：30m长6m宽矩形，6s
    [ScriptMethod(name: "超魔BOSS2 - 突进", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49674"])]
    public void 超魔B2突进2(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"突进(49616)：pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        var dp = sa.FastDp("突进", evt.SourcePosition(), 6000, new Vector2(6f, 30f));
        dp.Rotation = evt.SourceRotation();
        // dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 49585 半圆：SourcePosition为圆心，面对SourceRotation
    [ScriptMethod(name: "超魔BOSS2 - 秘法剑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49645"])]
    public void 超魔B2秘法剑(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"半圆斩(49645)：src {evt.SourceId():X8} pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        lock (_b2ChargeLock)
            _b2剑Casts.Add((evt.SourceId(), evt.SourcePosition(), evt.SourceRotation(), Environment.TickCount64));
        var dp = sa.FastDp($"半圆斩-{evt.SourceId()}", evt.SourcePosition(), 5500, new Vector2(96f));
        dp.Rotation = evt.SourceRotation();
        dp.Radian = MathF.PI;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    private readonly object _swordPredictLock = new();
    private readonly List<(uint Sid, Vector3 Pos, int Pose, long At)> _swordPredicts = [];
    private const long 同环阈值 = 1500;   // 同一波金环的9710最大间隔

    // pose→AOE形状：0=小月环(内10外40)接小钢铁10，4=月环(内15外40)接钢铁15，5=大月环(内20外40)接大钢铁20，
    // 6=小钢铁10接小月环(内10外40)，7=钢铁15接月环(内15外40)，31=大钢铁20接大月环(内20外40)
    private void B2DrawSwordShape(ScriptAccessory sa, uint sid, Vector3 pos, int pose, bool second, uint duration, uint delay)
    {
        var (donutFirst, r) = pose switch
        {
            0 => (true, 10f),
            4 => (true, 15f),
            5 => (true, 20f),
            6 => (false, 10f),
            7 => (false, 15f),
            31 => (false, 20f),
            _ => (false, 0f),
        };
        if (r == 0f) return;
        if (donutFirst ^ second)
        {
            var dp = sa.FastDp($"剑月环-{sid}-{delay}", pos, duration, new Vector2(40f));
            dp.InnerScale = new Vector2(r);
            dp.Radian = float.Pi * 2;
            dp.Delay = delay;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
        else
        {
            var dp = sa.FastDp($"剑钢铁-{sid}-{delay}", pos, duration, new Vector2(r));
            dp.Delay = delay;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "超魔BOSS2 - 舞动之剑预判", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:9710"])]
    public void 超魔B2舞动之剑预判(Event evt, ScriptAccessory sa)
    {
        var sid = evt.SourceId();
        var obj = sa.Data.Objects.SearchById(sid);
        if (obj is null || obj.DataId == 19833) return;   // 19833 由普通BOSS2方法处理

        var pose = B2GetModelState(sa, sid);
        var pos = evt.SourcePosition();
        Dbg(sa, $"超魔舞动之剑9710：src {sid:X8} DataId {obj.DataId} 姿势 {pose}");
        if (pose is not (0 or 4 or 5 or 6 or 7 or 31))
        {
            Dbg(sa, $"超魔舞动之剑9710：未知姿势{pose}，不绘图");
            return;
        }
        var now = Environment.TickCount64;
        bool firstWave;
        lock (_swordPredictLock)
        {
            _swordPredicts.RemoveAll(s => now - s.At > 30000);   // 清团灭残留
            // 首波(定时未激活且无更早出现的剑)立即绘制第1段；后续波由定时按判定链排程显示
            firstWave = now - _b2SwordTimingAt > 20000 && _swordPredicts.All(s => now - s.At < 同环阈值);
            _swordPredicts.Add((sid, pos, pose, now));
        }
        if (firstWave) B2DrawSwordShape(sa, sid, pos, pose, false, 20000, 0);
        else Dbg(sa, $"超魔舞动之剑9710：{sid:X8} 非首波，待定时排程");
    }

    private long _b2SwordTimingAt;

    // 49647 定时：金环按9710出现顺序分波判定(约3s一波)，首波第1段读条开始5s后判定；
    // 显示跟随判定链：开始只显示首波第1段，首波判定时出现首波第2段+第2波第1段，
    // 第2波判定时出现第2波第2段+第3波第1段，依此类推；晚于读条出现的金环通过轮询追加
    [ScriptMethod(name: "超魔BOSS2 - 舞动之剑定时", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49647"], userControl: false)]
    public async void 超魔B2舞动之剑定时(Event evt, ScriptAccessory sa)
    {
        List<(uint Sid, Vector3 Pos, int Pose, long At)> swords;
        var castAt = Environment.TickCount64;
        lock (_swordPredictLock)
        {
            if (_swordPredicts.Count == 0) return;
            if (castAt - _b2SwordTimingAt < 15000) return;   // 双剑各读一次时只按首个定时
            _b2SwordTimingAt = castAt;
            swords = [.. _swordPredicts.OrderBy(s => s.At)];
            _swordPredicts.Clear();
        }
        Dbg(sa, $"超魔舞动之剑49647：定时开始，已出现 {swords.Count} 把剑");

        // 判定节奏固定：9710出现时间只用于分波排序，不决定判定时间
        const uint 首环判定 = 4000;   // 首波第1段判定 = 读条开始后
        const uint 波判定间隔 = 4000; // 相邻两波判定的间隔(前一波第2段与后一波第1段同判)
        var waveAt = swords[0].At;   // 当前波首剑出现时间
        var wave = 0;
        var next = 0;
        while (true)
        {
            for (; next < swords.Count; next++)
            {
                var (sid, pos, pose, at) = swords[next];
                if (at - waveAt > 同环阈值)
                {
                    wave++;
                    waveAt = at;
                }
                var curJ = 首环判定 + (uint)wave * 波判定间隔;   // 本波第1段判定点(相对读条开始)
                var elapsed = (uint)(Environment.TickCount64 - castAt);
                Dbg(sa, $"超魔舞动之剑49647：{sid:X8} 姿势{pose} 第{wave + 1}波 判定+{curJ}ms");
                if (wave == 0)
                {
                    // 首波：第1段已在预判时绘制，判定时移除
                    _ = B2SwordPredictRemove(sa, sid, curJ > elapsed ? curJ - elapsed : 0);
                }
                else
                {
                    // 后续波第1段：上一波判定时出现，显示到本波判定
                    var prevJ = curJ - 波判定间隔;
                    var start = Math.Max(prevJ, elapsed);
                    if (curJ > start)
                        B2DrawSwordShape(sa, sid, pos, pose, false, curJ - start, prevJ > elapsed ? prevJ - elapsed : 0);
                }
                // 第2段：本波判定时出现，显示到下一波判定
                B2DrawSwordShape(sa, sid, pos, pose, true, 波判定间隔, curJ > elapsed ? curJ - elapsed : 0);
            }
            if (Environment.TickCount64 - castAt > 12000) break;
            await Task.Delay(300);
            lock (_swordPredictLock)
            {
                if (_swordPredicts.Count > 0)
                {
                    swords.AddRange(_swordPredicts.OrderBy(s => s.At));
                    _swordPredicts.Clear();
                }
            }
        }
    }

    private static async Task B2SwordPredictRemove(ScriptAccessory sa, uint sid, uint delay)
    {
        if (delay > 0) await Task.Delay((int)delay);
        sa.Method.RemoveDraw($"^剑(月环|钢铁)-{sid}-0$");
    }

    [ScriptMethod(name: "超魔BOSS2 - 剑舞", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:1", "Id2:2"], userControl: false)]
    public void 超魔剑刃矩形(Event evt, ScriptAccessory sa)
    {
        if (!Is超魔(sa)) return;   // 该机制仅超魔触发
        var obj = sa.Data.Objects.SearchById(evt.SourceId());
        if (obj is null || obj.DataId != 2015283) return;

        var pos = evt.SourcePosition();
        var rot = evt.SourceRotation();

        List<(Vector3 Pos, float Rot)> rects;
        lock (_bladeRectLock)
        {
            if (_bladeRects.Any(r => DistXZ(r.Pos, pos) < 1f && MathF.Abs(WrapPi(r.Rot - rot)) < 0.1f)) return;
            _bladeRects.Add((pos, rot));
            Dbg(sa, $"剑刃矩形记录 #{_bladeRects.Count}：pos {pos:F1} rot {rot:F2}");
            if (_bladeRects.Count < 4) return;

            rects = [.. _bladeRects];
            _bladeRects.Clear();
        }

        for (var i = 0; i < rects.Count; i++)
        {
            var delay = (uint)(i == 0 ? 0 : 5000 + (i - 1) * 1500);
            var duration = (uint)(i == 0 ? 6500 : 3000);
            foreach (var (extraRot, tag) in new[] { (0f, "正"), (MathF.PI, "反") })
            {
                var dp = sa.FastDp($"剑刃矩形-{i + 1}-{tag}", rects[i].Pos, duration, new Vector2(20f, 60f));
                dp.Rotation = rects[i].Rot + extraRot;
                dp.Delay = delay;
                // dp.ScaleMode = ScaleMode.ByTime;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
    }

    [ScriptMethod(name: "超魔BOSS2 - 跃进步法记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(49656|49657)$"], userControl: false)]
    public void 超魔跃进步法记录(Event evt, ScriptAccessory sa)
    {
        var pos = evt.EffectPosition();
        if (pos.Length() < 0.01f) return;   // 排除(0,0,0)

        lock (_leapLock)
        {
            if (_leapPositions.All(p => DistXZ(p, pos) > 1f))
            {
                _leapPositions.Add(pos);
                Dbg(sa, $"跃进步法记录 #{_leapPositions.Count}：{pos:F1}");
            }
        }
    }

    // 49687 剑技爆发：按顺序指路各落点
    [ScriptMethod(name: "超魔BOSS2 - 剑技爆发指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:49687"])]
    public void 超魔剑技爆发指路(Event evt, ScriptAccessory sa)
    {
        List<Vector3> rawPoints;
        lock (_leapLock)
        {
            rawPoints = [.. _leapPositions];
            _leapPositions.Clear();   // 用完清空
        }
        if (rawPoints.Count == 0)
        {
            Dbg(sa, $"剑技爆发(49687)：落点list为空，跳过指路");
            return;
        }

        // 检测场上19842中带 Status 2056 且 Param 1173 的剑，按位置匹配落点轮次
        var steelSwordPos = sa.Data.Objects
            .Where(o => o is IBattleChara bc && bc.DataId == 19842
                        && bc.StatusList.Any(s => s.StatusId == 2056 && s.Param == 1173))
            .Select(o => o.Position)
            .ToList();
        var isSteel = rawPoints.Select(p => steelSwordPos.Any(sp => DistXZ(sp, p) < 3f)).ToList();
        Dbg(sa, $"剑技爆发(49687)：共 {rawPoints.Count} 个落点，1173钢铁轮 [{string.Join(",", isSteel.Select((s, i) => (s, i)).Where(t => t.s).Select(t => t.i + 1))}]，" +
                $"1173剑位 [{string.Join(" | ", steelSwordPos.Select(p => p.ToString("F1")))}]");

        var points = rawPoints.Select(p => 朝场中偏移(p, 5f)).ToList();
        var n = points.Count;

        // 钢铁轮前一轮的指路点：绕场中朝远离下一轮钢铁的方向旋转10°
        for (var k = 0; k + 1 < n; k++)
            if (isSteel[k + 1])
                points[k] = 绕场中旋转远离(points[k], rawPoints[k + 1]);

        var delays = new uint[n];
        var durations = new uint[n];
        var steelCount = 0;
        for (var k = 0; k < n; k++)
        {
            if (isSteel[k]) steelCount++;
            delays[k] = (uint)((k == 0 ? 0 : 5000 + (k - 1) * 2500) + steelCount * 2500);
            durations[k] = (uint)(k == 0 ? 5000 : 2500);
        }

        var green = new Vector4(0f, 1f, 0f, 1f);
        var white = new Vector4(1f, 1f, 1f, 1f);

        for (var k = 0; k < n; k++)
        {
            var delay = delays[k];
            var duration = durations[k];

            if (isSteel[k])
            {
                // 钢铁圈与前一轮指路同时出现，显示4s
                var dp = sa.FastDp($"剑技爆发-钢铁-{k + 1}", rawPoints[k], 4000, new Vector2(15f));
                dp.Delay = k == 0 ? 0 : delays[k - 1];
                dp.ScaleMode = ScaleMode.ByTime;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                // 钢铁时段同步显示本轮指路，全白色
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                    sa.WaypointDp(points[k], 2500, delay - 2500, $"剑技爆发-钢铁轮-me到{k + 1}", white));
                for (var j = k; j < n - 1; j++)
                    sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                        sa.WaypointFromToDp(points[j], points[j + 1], 2500, delay - 2500, $"剑技爆发-{j + 1}到{j + 2}-钢铁轮{k + 1}", white));
            }

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                sa.WaypointDp(points[k], duration, delay, $"剑技爆发-me到{k + 1}", green));
            for (var j = k; j < n - 1; j++)
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
                    sa.WaypointFromToDp(points[j], points[j + 1], duration, delay, $"剑技爆发-{j + 1}到{j + 2}-阶段{k + 1}", white));
        }
    }
    #endregion

    #region 2.5
    // 48848：12s，SourcePosition 20m危险圈
    [ScriptMethod(name: "超魔2.5 - 不可见钢铁", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:48848"])]
    public void 超魔25大圈(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"2.5大圈(48848)：pos {evt.SourcePosition():F1}");
        var dp = sa.FastDp($"2.5大圈-{evt.SourceId()}", evt.SourcePosition(), 12000, new Vector2(20f));
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    #endregion

    #region 超魔BOSS3
    // 超魔boss 3
    [ScriptMethod(name: "超魔BOSS3 - 爆炎", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47490|47494)$"])]
    public void 超魔爆炎(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"爆炎({evt.ActionId()})：src {evt.SourceId():X8}");
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"爆炎-{evt.SourceId()}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Owner = evt.SourceId();
        dp.DestoryAt = 5000;
        dp.Scale = new Vector2(18f);
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 47466 魔具联动-冰封 / 47469 古代冰封：5.5s，以施法者rotation为正面，前后左右各45长15宽的rect十字
    [ScriptMethod(name: "超魔BOSS3 - 冰封十字", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47495|47491)$"])]
    public void 超魔冰封十字(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"冰封十字({evt.ActionId()})：src {evt.SourceId():X8}");
        List<(float Rot, string Tag)> dirs = [(0f, "前"), (MathF.PI / 2, "左"), (MathF.PI, "后"), (-MathF.PI / 2, "右")];
        foreach (var (rot, tag) in dirs)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"冰封十字-{evt.SourceId()}-{tag}";
            dp.Color = sa.Data.DefaultDangerColor;
            dp.Owner = evt.SourceId();
            dp.Rotation = rot;
            dp.DestoryAt = 5500;
            dp.Scale = new Vector2(15f, 45f);
            // dp.ScaleMode = ScaleMode.ByTime;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    // 47471 古代暴雷：5.5s，SourcePosition上以SourceRotation为正面的45度60m扇形
    [ScriptMethod(name: "超魔BOSS3 - 古代暴雷", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47497|50358)"])]
    public void 超魔古代暴雷(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"古代暴雷(47479)：pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        var dp = sa.FastDp($"古代暴雷-{evt.SourceId()}", evt.SourcePosition(), 5500, new Vector2(60f));
        dp.Rotation = evt.SourceRotation();
        dp.Radian = 45f * MathF.PI / 180f;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "超魔BOSS3 - 灭亡射线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47504"])]
    public async void 超魔灭亡射线(Event evt, ScriptAccessory sa)
    {
        var srcId = evt.SourceId();
        var pos = evt.SourcePosition();

        await Task.Delay(1000);

        // 0.5s后取施法者当前实时朝向，取不到则回退用事件快照
        var srcObj = sa.Data.Objects.SearchById(srcId);
        var rot = srcObj?.Rotation ?? evt.SourceRotation();
        Dbg(sa, $"灭亡射线(47475)：pos {pos:F1} rot {rot:F2}（实时:{srcObj != null}）");

        var dp = sa.FastDp($"灭亡射线-{srcId}", pos, 3000, new Vector2(6f, 30f));
        dp.Rotation = rot;
        // dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 47500：5.5s，EffectPosition 沿 SourceRotation 的 60长10宽矩形，CreateOmen 689（rect omen：X=半宽 Z=全长）
    [ScriptMethod(name: "超魔BOSS3 - 黑暗奔涌", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47500"])]
    public void 超魔B3直线47500(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"直线(47500)：pos {evt.EffectPosition():F1} rot {evt.SourceRotation():F2}");
        sa.Method.VfxMethod.CreateOmen(689, new Vector3(5f, 10f, 60f),
            evt.EffectPosition(), evt.SourceRotation(), null, 4000);
    }

    // 47500 读条开始4s后：左右两侧步进地火，60长10宽矩形，每轮向外步进10m，每轮画3.5s紧接下一轮
    [ScriptMethod(name: "超魔BOSS3 - 黑暗奔流地火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47500"])]
    public void 超魔B3黑暗奔流地火(Event evt, ScriptAccessory sa)
    {
        var pos = evt.EffectPosition();
        var rot = evt.SourceRotation();
        Dbg(sa, $"黑暗奔流地火(47500)：pos {pos:F1} rot {rot:F2}");
        for (var i = 0; i < 2; i++)
        {
            foreach (var (sign, tag) in new[] { (1f, "左"), (-1f, "右") })
            {
                var side = rot + sign * MathF.PI / 2;
                var center = pos + new Vector3(MathF.Sin(side), 0f, MathF.Cos(side)) * (10f * (i + 1));
                var dp = sa.FastDp($"黑暗奔流地火-{tag}-{i}", center, 3500, new Vector2(10f, 60f));
                dp.Rotation = rot;
                dp.Delay = (uint)(2500 + i * 2000);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
    }

    // 47502 真空波：等1s让boss完成转向后，SourcePosition 上以实时朝向为正面的180度30m扇形
    [ScriptMethod(name: "超魔BOSS3 - 真空波", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47502"])]
    public async void 超魔B3真空波(Event evt, ScriptAccessory sa)
    {
        var srcId = evt.SourceId();
        var pos = evt.SourcePosition();

        await Task.Delay(1000);

        // 取施法者当前实时朝向，取不到则回退用事件快照
        var srcObj = sa.Data.Objects.SearchById(srcId);
        var rot = srcObj?.Rotation ?? evt.SourceRotation();
        Dbg(sa, $"真空波(47502)：pos {pos:F1} rot {rot:F2}（实时:{srcObj != null}）");

        var dp = sa.FastDp($"真空波-{srcId}", pos, 3000, new Vector2(30f));
        dp.Rotation = rot;
        dp.Radian = MathF.PI;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    // —— 魔具联动：黑暗奔流 三连 ——
    private readonly object _b3联动Lock = new();
    private readonly Dictionary<uint, uint> _b3屏障头属性 = [];   // 屏障头Id → 属性Param(1114火/1115冰/1116雷)
    private readonly List<uint> _b3奔流顺序 = [];                 // 三波2552属性的接收顺序，集齐后播报
    private int _b3奔流波次;                                      // 已收到的2552个数(只取前三)
    private bool _b3联动就绪;                                     // 47507读条开始→true，期间的2552才触发绘制

    private static string B3属性名(uint param) => param switch { 1114 => "火", 1115 => "冰", 1116 => "雷", _ => $"?{param}" };

    [ScriptMethod(name: "超魔BOSS3 - 屏障头注能记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^019[0-2]$"], userControl: false)]
    public void 超魔B3屏障头注能记录(Event evt, ScriptAccessory sa)
    {
        var elem = evt["Id"] switch { "0190" => 1114u, "0191" => 1115u, "0192" => 1116u, _ => 0u };
        if (elem == 0) return;
        lock (_b3联动Lock) _b3屏障头属性[evt.SourceId()] = elem;
        Dbg(sa, $"屏障头注能：{evt.SourceId():X8} → {B3属性名(elem)}");
    }

    [ScriptMethod(name: "超魔BOSS3 - 魔具联动黑暗奔流", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2552", "Param:regex:^111[4-6]$"])]
    public async void 超魔B3魔具联动黑暗奔流(Event evt, ScriptAccessory sa)
    {
        if (!Is超魔(sa)) return;
        if (!uint.TryParse(evt["Param"], out var elem)) return;

        int wave;
        List<uint> heads;
        string? order = null;
        lock (_b3联动Lock)
        {
            if (!_b3联动就绪) return;       // 非联动期间的2552(如47490/47491/47492读条伴随)，忽略
            if (_b3奔流波次 >= 3) return;   // 每波结算时的重复Add，忽略
            wave = _b3奔流波次++;
            _b3奔流顺序.Add(elem);
            if (_b3奔流波次 == 3) order = string.Join("→", _b3奔流顺序.Select(B3属性名));
            heads = _b3屏障头属性.Where(kv => kv.Value == elem).Select(kv => kv.Key).ToList();
        }

        // 集齐三个属性后播报结算顺序（如"冰→雷→火"），持续到第三波结算
        if (order != null) sa.Method.TextInfo(order, 18000, false);

        uint[] durations = [8900, 13600, 18100];   // 自己的2552到本波结算
        uint[] delays = [0, 8900, 13600];          // 第2/3波延后开画
        var duration = durations[wave];
        var delay = delays[wave];
        var visible = duration - delay;
        var rot = MathF.PI - wave * 2f * MathF.PI / 3f;   // 第1波C→A(π)，每波直线逆时针转60°
        var center = new Vector3(100f, -723.96f, 800f);
        var anchor = center - new Vector3(MathF.Sin(rot), 0f, MathF.Cos(rot)) * 30f;
        Dbg(sa, $"魔具联动第{wave + 1}波：{B3属性名(elem)}，{heads.Count}个屏障头，{delay}ms后开画，{duration}ms后结算");

        // 步进地火：直线结算-1s起，左右±10、±20两轮，每轮3.5s
        DrawPropertiesEdit dp;
        for (var i = 0; i < 2; i++)
        {
            foreach (var (sign, tag) in new[] { (1f, "左"), (-1f, "右") })
            {
                var side = rot + sign * MathF.PI / 2;
                var pos = anchor + new Vector3(MathF.Sin(side), 0f, MathF.Cos(side)) * (10f + i * 10f);
                dp = sa.FastDp($"B3联动地火-{wave}-{tag}-{i}", pos, 3500, new Vector2(10f, 60f));
                dp.Rotation = rot;
                dp.Delay = (uint)(duration - 1000 + i * 2100);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }

        // 该属性两个屏障头的AoE，与直线同时结算
        foreach (var head in heads)
        {
            switch (elem)
            {
                case 1114:   // 火：18m圆
                    dp = sa.Data.GetDefaultDrawProperties();
                    dp.Name = $"B3联动火圆-{wave}-{head:X8}";
                    dp.Color = sa.Data.DefaultDangerColor;
                    dp.Owner = head;
                    dp.Delay = delay;
                    dp.DestoryAt = visible;
                    dp.Scale = new Vector2(18f);
                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    break;
                case 1115:   // 冰：以头朝向前后左右15x45十字
                    foreach (var (r, tag) in new[] { (0f, "前"), (MathF.PI / 2, "左"), (MathF.PI, "后"), (-MathF.PI / 2, "右") })
                    {
                        dp = sa.Data.GetDefaultDrawProperties();
                        dp.Name = $"B3联动冰十字-{wave}-{head:X8}-{tag}";
                        dp.Color = sa.Data.DefaultDangerColor;
                        dp.Owner = head;
                        dp.Rotation = r;
                        dp.Delay = delay;
                        dp.DestoryAt = visible;
                        dp.Scale = new Vector2(15f, 45f);
                        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    }
                    break;
                case 1116:   // 雷：以头朝向左上左下右上右下四个45°60m扇形
                    foreach (var (r, tag) in new[] { (MathF.PI / 4, "左上"), (3 * MathF.PI / 4, "左下"), (-MathF.PI / 4, "右上"), (-3 * MathF.PI / 4, "右下") })
                    {
                        dp = sa.Data.GetDefaultDrawProperties();
                        dp.Name = $"B3联动雷扇-{wave}-{head:X8}-{tag}";
                        dp.Color = sa.Data.DefaultDangerColor;
                        dp.Owner = head;
                        dp.Rotation = r;
                        dp.Radian = 45f * MathF.PI / 180f;
                        dp.Delay = delay;
                        dp.DestoryAt = visible;
                        dp.Scale = new Vector2(60f);
                        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                    }
                    break;
            }
        }

        // 直线：Omen 689（X=半宽 Z=全长），从场边锚点沿rot穿过场中心，等上一波结算后再出现
        if (delay > 0) await Task.Delay((int)delay);
        sa.Method.VfxMethod.CreateOmen(689, new Vector3(5f, 10f, 60f), anchor, rot, null, (int)visible);
    }

    // 47507 魔具联动：黑暗奔流读条开始：开闸，期间的2552触发绘制
    [ScriptMethod(name: "超魔BOSS3 - 联动开始", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47507"], userControl: false)]
    public void 超魔B3联动开始(Event evt, ScriptAccessory sa)
    {
        lock (_b3联动Lock)
        {
            _b3联动就绪 = true;
            _b3奔流波次 = 0;
            _b3奔流顺序.Clear();
        }
    }

    // 47490爆炎/47491冰封/47492暴雷 读条时也会Add 2552，不触发联动：关闸并清空计数
    [ScriptMethod(name: "超魔BOSS3 - 联动误触发拦截", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^4749[0-2]$"], userControl: false)]
    public void 超魔B3联动误触发拦截(Event evt, ScriptAccessory sa)
    {
        lock (_b3联动Lock)
        {
            _b3联动就绪 = false;
            _b3奔流波次 = 0;
            _b3奔流顺序.Clear();
        }
    }

    // 魔力注入开始(新一轮注能前) / 真空波(本轮联动收尾)：清空记录
    [ScriptMethod(name: "超魔BOSS3 - 联动记录清空", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47486|47502)$"], userControl: false)]
    public void 超魔B3联动记录清空(Event evt, ScriptAccessory sa)
    {
        lock (_b3联动Lock)
        {
            _b3联动就绪 = false;
            _b3屏障头属性.Clear();
            _b3奔流波次 = 0;
            _b3奔流顺序.Clear();
        }
    }

    // 47521 古代爆炎：5s，施法者上18m危险圈
    [ScriptMethod(name: "超魔BOSS3 - 古代爆炎", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47521"])]
    public void 超魔B3古代爆炎(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"古代爆炎(47521)：src {evt.SourceId():X8}");
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"古代爆炎-{evt.SourceId()}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Owner = evt.SourceId();
        dp.DestoryAt = 5000;
        dp.Scale = new Vector2(18f);
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }

    // 47522 古代冰封：5s，以施法者rotation为正面，前后左右各45长15宽的rect十字
    [ScriptMethod(name: "超魔BOSS3 - 古代冰封", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47522"])]
    public void 超魔B3古代冰封(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"古代冰封(47522)：src {evt.SourceId():X8}");
        List<(float Rot, string Tag)> dirs = [(0f, "前"), (MathF.PI / 2, "左"), (MathF.PI, "后"), (-MathF.PI / 2, "右")];
        foreach (var (rot, tag) in dirs)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"古代冰封-{evt.SourceId()}-{tag}";
            dp.Color = sa.Data.DefaultDangerColor;
            dp.Owner = evt.SourceId();
            dp.Rotation = rot;
            dp.DestoryAt = 5000;
            dp.Scale = new Vector2(15f, 45f);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp);
        }
    }

    // 47493 分身古代暴雷：5s，SourcePosition上以SourceRotation为正面的45度60m扇形
    [ScriptMethod(name: "超魔BOSS3 - 分身古代暴雷", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47493"])]
    public void 超魔B3分身古代暴雷(Event evt, ScriptAccessory sa)
    {
        Dbg(sa, $"分身古代暴雷(47493)：pos {evt.SourcePosition():F1} rot {evt.SourceRotation():F2}");
        var dp = sa.FastDp($"分身古代暴雷-{evt.SourceId()}", evt.SourcePosition(), 5000, new Vector2(60f));
        dp.Rotation = evt.SourceRotation();
        dp.Radian = 45f * MathF.PI / 180f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
    }

    // —— 多产的土壤（鸳鸯锅）——
    private static readonly Vector3 Boss3场中 = new(100f, -723.96f, 800f);
    private readonly object _b3鸳鸯锅Lock = new();
    private readonly HashSet<uint> _b3鸳鸯锅Heads = [];   // 本轮已记录的屏障头，防重复Add
    private int _b3鸳鸯锅序号;

    // 47514 多产的土壤读条：重置鸳鸯锅计数
    [ScriptMethod(name: "超魔BOSS3 - 鸳鸯锅重置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47514"], userControl: false)]
    public void 超魔B3鸳鸯锅重置(Event evt, ScriptAccessory sa)
    {
        lock (_b3鸳鸯锅Lock)
        {
            _b3鸳鸯锅序号 = 0;
            _b3鸳鸯锅Heads.Clear();
        }
    }

    [ScriptMethod(name: "超魔BOSS3 - 鸳鸯锅指路", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2552", "Param:regex:^111[78]$"])]
    public async void 超魔B3鸳鸯锅指路(Event evt, ScriptAccessory sa)
    {
        if (!Is超魔(sa)) return;
        if (!uint.TryParse(evt["Param"], out var param)) return;

        var headId = evt.TargetId();
        int idx;
        lock (_b3鸳鸯锅Lock)
        {
            if (!_b3鸳鸯锅Heads.Add(headId)) return;   // 重复Add，忽略
            idx = _b3鸳鸯锅序号++;
        }

        // StatusAdd时刻头可能还没转完，等1s后再取实时朝向
        await Task.Delay(1000);
        var head = sa.Data.Objects.SearchById(headId);
        if (head is null)
        {
            Dbg(sa, $"鸳鸯锅#{idx + 1}：找不到屏障头 {headId:X8}，跳过");
            return;
        }
        var rot = head.Rotation;
        Dbg(sa, $"鸳鸯锅#{idx + 1}：头 {headId:X8} {(param == 1117 ? "左蓝右红" : "左红右蓝")} rot {rot:F2}");

        // 本锅结算(半场AoE命中)≈自己StatusAdd后 13270+4800*idx+500ms；
        // 吃锅后自己的5136/5137会交换颜色，交换包在上一锅命中后约1s到达：
        // 首锅从现在开始显示，其余显示结算前5s(比窗口开始晚1s判定，确保读到交换后的颜色)
        var settle = 13770 + 4800 * idx - 1000;   // 已等1s取朝向
        var showDur = (uint)(idx == 0 ? settle : 5050);
        if (settle > showDur) await Task.Delay(settle - (int)showDur);

        var me = sa.Data.MyObject;
        var meBlue = me?.HasStatus(5136) ?? false;   // 蓝→吃红
        var meRed = me?.HasStatus(5137) ?? false;    // 红→吃蓝
        if (!meBlue && !meRed)
        {
            Dbg(sa, $"鸳鸯锅#{idx + 1}：自己无5136/5137，跳过指路");
            return;
        }

        // 1117=左蓝右红，1118=左红右蓝 蓝吃红去红半场，红吃蓝去蓝半场
        var 红在左 = param == 1118;
        var 去左 = meBlue == 红在左;
        var dp = sa.FastDp($"鸳鸯锅安全半场-{idx + 1}", Boss3场中, showDur, new Vector2(24f), safe: true);
        dp.Rotation = rot + (去左 ? MathF.PI / 2 : -MathF.PI / 2);
        dp.Radian = MathF.PI;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        Dbg(sa, $"鸳鸯锅#{idx + 1}：自己{(meBlue ? "蓝吃红" : "红吃蓝")}，安全半场在头{(去左 ? "左" : "右")}侧，显示{showDur}ms");
    }
    #endregion

    #region 超魔BOSS4

    // —— 四连召唤·封印武器（48907/48909，13.2s读条）——
    private readonly object _b4WeaponLock = new();
    private readonly List<uint> _b4Weapons = [];   // 按StatusAdd顺序记录的武器statusId
    private uint _b4BossId;

    private static string B4武器名(uint statusId) => statusId switch { 5534 => "弓[月环]", 5533 => "刀[正刀]", 5535 => "琴[钢铁]", _ => "铃铛[斜刀]" };

    // TextInfo横幅用去的位置：弓=内（月环贴脸）、刀=斜（正刀躲斜角）、铃铛=正（斜刀躲正点）、琴=外（钢铁远离）
    private static string B4武器位置(uint statusId) => statusId switch { 5534 => "靠近", 5533 => "去斜", 5535 => "远离", _ => "去正" };

    // 弓的钢铁不在boss/场中，而是三个固定点（盯准施法者位置）
    private static readonly Vector3[] 盯准固定点 =
    [
        new(0.00f, -684.00f, -607.50f),
        new(17.75f, -684.00f, -638.25f),
        new(-17.75f, -684.00f, -638.25f),
    ];

    // 弓：三个固定点上各画一个11m钢铁，显示窗口相对StatusAdd为[delay, delay+duration]
    private void 超魔B4画弓钢铁(ScriptAccessory sa, int k, uint delay, uint duration)
    {
        for (var i = 0; i < 盯准固定点.Length; i++)
        {
            var dp = sa.FastDp($"四连武器-{k + 1}-弓-{i}", 盯准固定点[i], duration, new Vector2(11f));
            dp.Delay = delay;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "超魔BOSS4 - 四连召唤开始", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(48906|48907|48908|48909)$"], userControl: false)]
    public void 超魔B4四连召唤开始(Event evt, ScriptAccessory sa)
    {
        lock (_b4WeaponLock)
        {
            _b4Weapons.Clear();
            _b4BossId = evt.SourceId();
        }
        Dbg(sa, $"四连召唤({evt.ActionId()})：boss {_b4BossId:X8}，开始记录武器顺序");
    }

    [ScriptMethod(name: "超魔BOSS4 - 四连召唤武器", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(553[2-5])$"])]
    public void 超魔B4四连召唤武器(Event evt, ScriptAccessory sa)
    {
        uint bossId;
        int k;
        string? order = null;
        lock (_b4WeaponLock)
        {
            bossId = _b4BossId;
            if (bossId == 0 || evt.TargetId() != bossId) return;   // 只认四连召唤读条中boss身上的status
            if (_b4Weapons.Contains(evt.StatusId)) return;
            if (_b4Weapons.Count >= 4) return;
            k = _b4Weapons.Count;
            _b4Weapons.Add(evt.StatusId);
            if (_b4Weapons.Count == 4) order = string.Join("→", _b4Weapons.Select(B4武器位置));
        }

        // 每个武器现身时/e即时播报武器名，四个集齐后TextInfo汇总去的位置（如"内→斜→外→正"），
        // 横幅持续到第四把结算（第4个StatusAdd + 14400+300*3）
        var name = B4武器名(evt.StatusId);
        sa.Method.SendChat($"/e [魔之塔] 四连武器{k + 1}：{name}");
        if (order != null) sa.Method.TextInfo(order, 15300, false);

        // 第k个武器：结算 = StatusAdd + 14400+300k ms；
        // 显示窗口 = [上一个武器结算, 自己结算]（第一个武器获得status时立即显示）
        var delay = (uint)(k == 0 ? 0 : 11100 + 300 * k);
        var duration = (uint)(k == 0 ? 14400 : 3300);
        Dbg(sa, $"四连武器#{k + 1}：{name}({evt.StatusId})，{delay}ms后显示{duration}ms");

        switch (evt.StatusId)
        {
            case 5534:   // 弓：三个固定点上各一个11m钢铁
                超魔B4画弓钢铁(sa, k, delay, duration);
                break;
            case 5535:   // 琴：15m钢铁（同普魔琴）
            {
                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"四连武器-{k + 1}-{name}";
                dp.Color = sa.Data.DefaultDangerColor;
                dp.Owner = bossId;
                dp.Delay = delay;
                dp.DestoryAt = duration;
                dp.Scale = new Vector2(15f);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                break;
            }
            case 5532:   // 铃铛：正后/右前/左前 60度扇形（左前右前之间隔正面60度空隙）
            case 5533:   // 刀：正面/左后/右后 60度扇形（左后右后之间隔正后60度空隙）
            {
                var dirs = evt.StatusId == 5532
                    ? new[] { (MathF.PI, "正后"), (-MathF.PI / 3, "右前"), (MathF.PI / 3, "左前") }
                    : new[] { (0f, "正面"), (2 * MathF.PI / 3, "左后"), (-2 * MathF.PI / 3, "右后") };
                foreach (var (rot, tag) in dirs)
                {
                    var dp = sa.Data.GetDefaultDrawProperties();
                    dp.Name = $"四连武器-{k + 1}-{name}-{tag}";
                    dp.Color = sa.Data.DefaultDangerColor;
                    dp.Owner = bossId;
                    dp.Rotation = rot;
                    dp.Radian = 60f * MathF.PI / 180f;
                    dp.Delay = delay;
                    dp.DestoryAt = duration;
                    dp.Scale = new Vector2(30f);
                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }
                break;
            }
        }
    }

    // —— 元素展开（48399）——
    // 圆环机制同普魔（出现约7s后AOE），但相邻圆环间隔缩短为约1.2~1.6s（普魔更长），
    // 判定提前量从剩2s改为剩1.5s。普魔/超魔各自只触发自己的方法。
    [ScriptMethod(name: "超魔BOSS4 - 元素圆环换区", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:regex:^(201524[345])$"], suppress: 1000)]
    public void 超魔B4元素圆环换区(Event evt, ScriptAccessory sa)
    {
        if (!Is超魔(sa)) return;   // 超魔专用
        if (元素创造中()) return;   // 元素创造期间由安全区指路接管
        _ = 元素圆环换区核心(evt, sa, 5500);
    }

    // —— 封印武器（单发，48384琴/48386弓）——
    [ScriptMethod(name: "超魔BOSS4 - 封印武器", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4842[23])$"])]
    public void 超魔B4封印武器(Event evt, ScriptAccessory sa)
    {
        var isHarp = evt.ActionId() == 48422;
        var radius = isHarp ? 15f : 11f;
        var pos = isHarp ? evt.EffectPosition() : evt.SourcePosition();
        if (pos.Length() < 0.01f) pos = evt.SourcePosition();
        Dbg(sa, $"封印武器-{(isHarp ? "琴" : "弓")}({evt.ActionId()})：pos {pos:F1} 半径 {radius}");

        var dp = sa.FastDp($"超魔封印武器-{evt.SourceId()}", pos, 7000, new Vector2(radius));
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "BOSS4 - 预言现象", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19299"])]
        public void 普魔B4预言现象(Event evt, ScriptAccessory sa) => _ = B4预言现象核心(evt, sa);

    // —— 元素整合（48434，19.7s读条）——
    // 读条期间分三轮每轮3个球AddCombatant（19971冰/19972火/19973雷，间隔约3.2s，出现约6s后爆炸）。
    // 只在第一轮生成时：从场中朝每个球的方向画绿色60度60m扇形，持续6s。
    private readonly object _b4IntegrateLock = new();
    private bool _b4IntegrateArmed;
    private readonly List<Vector3> _b4IntegrateBalls = [];

    [ScriptMethod(name: "超魔BOSS4 - 元素整合开始", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:48434"], userControl: false)]
    public void 超魔B4元素整合开始(Event evt, ScriptAccessory sa)
    {
        lock (_b4IntegrateLock)
        {
            _b4IntegrateArmed = true;
            _b4IntegrateBalls.Clear();
        }
        Dbg(sa, "元素整合(48434)：等待第一轮元素球");
    }

    [ScriptMethod(name: "超魔BOSS4 - 元素整合首轮标记", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1997[1-3])$"])]
    public void 超魔B4元素整合首轮标记(Event evt, ScriptAccessory sa)
    {
        var pos = evt.SourcePosition();
        int idx;
        lock (_b4IntegrateLock)
        {
            if (!_b4IntegrateArmed) return;                          // 只标记第一轮
            if (_b4IntegrateBalls.Any(p => DistXZ(p, pos) <= 1f)) return;   // 复读事件去重
            idx = _b4IntegrateBalls.Count;
            _b4IntegrateBalls.Add(pos);
            if (_b4IntegrateBalls.Count >= 3) _b4IntegrateArmed = false;
        }

        var rot = MathF.Atan2(pos.X - Boss4中心.X, pos.Z - Boss4中心.Z);
        Dbg(sa, $"元素整合首轮球#{idx + 1}：DataId {evt["DataId"]} pos {pos:F1} rot {rot:F2}");

        var dp = sa.FastDp($"元素整合首轮-{idx + 1}", Boss4中心, 6000, new Vector2(60f));
        dp.Color = new Vector4(0f, 1f, 0f, 1f);
        dp.Rotation = rot;
        dp.Radian = 60f * MathF.PI / 180f;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    // —— 魔法剑·石化（48444本体/48445分身，4.7s读条）——
    // 第0~3跳沿读条朝向直线前进、每跳6m；第3~5跳绕场中转弯、每跳40°（第4跳半径9、第5跳半径同第3跳）；
    // 第5~8跳沿(朝向∓60°)直线离场、每跳6m。
    // 转弯方向由起点车道相对场中的偏移决定：朝向×(场中-起点)叉积为负则+40°/-60°，为正则镜像。
    // 读条开始即可排程全部9跳，每跳显示结算前4s。
    private const float 石化地火半径 = 6f;
    private const int 石化跳间隔 = 2000;
    private const int 石化起爆时间 = 5000;   // 读条开始到第0跳起爆

    [ScriptMethod(name: "超魔BOSS4 - 石化地火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:48445"])]
    public void 超魔B4石化地火(Event evt, ScriptAccessory sa)
    {
        var sid = evt.SourceId();
        var p0 = evt.SourcePosition();
        var rot = evt.SourceRotation();
        var v = new Vector3(MathF.Sin(rot), 0, MathF.Cos(rot));

        // 场中在行进方向的哪一侧决定转弯方向
        var wx = Boss4中心.X - p0.X;
        var wz = Boss4中心.Z - p0.Z;
        var dir = v.X * wz - v.Z * wx < 0 ? 1 : -1;

        var nodes = new Vector3[9];
        for (var k = 0; k <= 3; k++)
            nodes[k] = p0 + v * (6f * k);

        var a3 = MathF.Atan2(nodes[3].X - Boss4中心.X, nodes[3].Z - Boss4中心.Z);
        var r3 = DistXZ(nodes[3], Boss4中心);
        var turn = 40f * MathF.PI / 180f * dir;
        nodes[4] = Boss4中心 + new Vector3(MathF.Sin(a3 + turn), 0, MathF.Cos(a3 + turn)) * 9f;
        nodes[5] = Boss4中心 + new Vector3(MathF.Sin(a3 + 2 * turn), 0, MathF.Cos(a3 + 2 * turn)) * r3;

        var outHeading = rot - dir * 60f * MathF.PI / 180f;
        var v2 = new Vector3(MathF.Sin(outHeading), 0, MathF.Cos(outHeading));
        for (var k = 6; k <= 8; k++)
            nodes[k] = nodes[5] + v2 * (6f * (k - 5));

        Dbg(sa, $"石化地火(48445)：src {sid:X8} 起点 {p0:F1} rot {rot:F2} 转向{(dir > 0 ? "+" : "-")}，" +
                $"终点 {nodes[8]:F1}");

        for (var k = 0; k < nodes.Length; k++)
        {
            nodes[k].Y = p0.Y;
            var resolve = 石化起爆时间 + k * 石化跳间隔;
            var delay = (uint)Math.Max(resolve - 4000, 0);
            var dp = sa.FastDp($"石化地火-{sid}-{k}", nodes[k], (uint)(resolve - delay), new Vector2(石化地火半径));
            dp.Delay = delay;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    // —— 全斩（48455，8.7s读条，12个小目录分三批释放）——
    private readonly object _b4ZenzanLock = new();
    private long _b4ZenzanFirstAt;

    [ScriptMethod(name: "超魔BOSS4 - 全斩", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:48455"])]
    public void 超魔B4全斩(Event evt, ScriptAccessory sa)
    {
        var now = Environment.TickCount64;
        long offset;
        lock (_b4ZenzanLock)
        {
            if (now - _b4ZenzanFirstAt > 15000) _b4ZenzanFirstAt = now;   // 新一轮机制
            offset = now - _b4ZenzanFirstAt;
        }
        var delay = (uint)(offset < 1000 ? 0 : Math.Max(7950 - offset, 0));
        var duration = (uint)(8950 - delay);

        var rot = float.TryParse(evt["TargetRotation"], out var tr) ? tr : evt.SourceRotation();
        Dbg(sa, $"全斩(48455)：src {evt.SourceId():X8} offset {offset}ms，{delay}ms后显示{duration}ms");

        var dp = sa.FastDp($"全斩-{evt.SourceId()}", evt.EffectPosition(), duration, new Vector2(8f, 15f));
        dp.Rotation = rot;
        dp.Delay = delay;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // VfxEvent 671去火 / 670去雷 / 672去水
    [ScriptMethod(name: "超魔BOSS4 - 属性点名指路", eventType: EventTypeEnum.VfxEvent, eventCondition: ["Id:regex:^(67[0-2])$"])]
    public void 超魔B4属性点名指路(Event evt, ScriptAccessory sa)
    {
        if (evt.SourceId() != sa.Data.Me) return;
        var element = evt["Id"] switch { "671" => "火", "670" => "雷", _ => "冰" };
        var word = element == "冰" ? "去水" : $"去{element}";
        Dbg(sa, $"属性点名(VfxEvent {evt["Id"]})：{word}");

        sa.Method.TTS(word, 3);
        sa.Method.TextInfo(word, 5000, false);

        var sectors = 元素扇区中心();
        if (sectors is null)
        {
            Dbg(sa, $"属性点名：元素区未记录齐，只播报不指路");
            return;
        }

        // 上下两个同属性扇区取离自己近的一个，指路到该扇区中心方向往外9m
        var mePos = sa.Data.MyObject?.Position ?? Boss4中心;
        var meRad = MathF.Atan2(mePos.X - Boss4中心.X, mePos.Z - Boss4中心.Z);
        var target = sectors
            .Where(s => s.Element == element)
            .OrderBy(s => MathF.Abs(WrapPi(s.Rot - meRad)))
            .First();
        var guidePos = Boss4中心 + new Vector3(MathF.Sin(target.Rot), 0, MathF.Cos(target.Rot)) * 9f;

        Dbg(sa, $"属性点名：{word}，指路 {guidePos:F1}");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(guidePos, 5000, 3000, "属性点名指路"));
    }

    // —— 元素创造（48400）——
    private readonly object _b4CreationLock = new();
    private long _b4CreationUntil;
    private readonly Dictionary<string, float> _b4CreationLineAxis = [];   // 元素→直线轴角[0,π)
    private List<string>? _b4CreationLineOrder;                            // 本波直线结算顺序
    private List<string>? _b4CreationPredictedSafe;                        // 首轮推出的三轮安全元素
    private int _b4CreationRound;
    private float? _b4CreationPrevSafeRot;

    private bool 元素创造中()
    {
        lock (_b4CreationLock) return Environment.TickCount64 < _b4CreationUntil;
    }

    [ScriptMethod(name: "超魔BOSS4 - 元素创造开始", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:48400"], userControl: false)]
    public void 超魔B4元素创造开始(Event evt, ScriptAccessory sa)
    {
        if (!Is超魔(sa)) return;
        lock (_b4CreationLock)
        {
            _b4CreationUntil = Environment.TickCount64 + 60000;
            _b4CreationLineAxis.Clear();
            _b4CreationLineOrder = null;
            _b4CreationPredictedSafe = null;
            _b4CreationRound = 0;
            _b4CreationPrevSafeRot = null;
        }
        Dbg(sa, "元素创造(48400)：进入元素创造模式60s");
    }

    [ScriptMethod(name: "超魔BOSS4 - 元素创造直线记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1930[89]|19310)$"], userControl: false)]
    public void 超魔B4元素创造直线记录(Event evt, ScriptAccessory sa)
    {
        if (!元素创造中()) return;
        if (!uint.TryParse(evt["DataId"], out var dataId)) return;
        var elem = dataId switch { 19308u => "冰", 19309u => "火", _ => "雷" };
        var pos = evt.SourcePosition();
        var axis = MathF.Atan2(pos.X - Boss4中心.X, pos.Z - Boss4中心.Z);
        axis = (axis % MathF.PI + MathF.PI) % MathF.PI;   // 直线上下对称，取[0,π)

        lock (_b4CreationLock)
        {
            // 上一波3轮已用完，收到新直线说明第二波开始：清空重记
            if (_b4CreationRound >= 3)
            {
                _b4CreationLineAxis.Clear();
                _b4CreationLineOrder = null;
                _b4CreationPredictedSafe = null;
                _b4CreationRound = 0;
            }
            if (!_b4CreationLineAxis.TryAdd(elem, axis)) return;   // 同元素成对出现，只记第一个
        }
        Dbg(sa, $"元素创造直线：{elem} 轴角 {axis * 180f / MathF.PI:F1}°");
    }

    [ScriptMethod(name: "超魔BOSS4 - 元素创造安全区", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:regex:^(201524[345])$"], suppress: 1000)]
    public void 超魔B4元素创造安全区(Event evt, ScriptAccessory sa)
    {
        if (!元素创造中()) return;
        if (!uint.TryParse(evt["DataId"], out var dataId)) return;
        var ringElem = dataId switch { 2015243u => "火", 2015244u => "冰", _ => "雷" };

        Dictionary<string, float> zoneRots;
        lock (_zoneLock)
        {
            if (_fireZoneRot is null || _waterZoneRot is null || _thunderZoneRot is null)
            {
                Dbg(sa, "元素创造安全区：元素区未记录齐，跳过");
                return;
            }
            zoneRots = new Dictionary<string, float>
            {
                ["火"] = _fireZoneRot.Value,
                ["冰"] = _waterZoneRot.Value,
                ["雷"] = _thunderZoneRot.Value,
            };
        }

        string lineElem;
        int round;
        lock (_b4CreationLock)
        {
            if (_b4CreationLineOrder is null)
            {
                if (_b4CreationLineAxis.Count < 3)
                {
                    Dbg(sa, "元素创造安全区：直线未记录齐，跳过");
                    return;
                }
                // 顺时针旋转 = 轴角减小，转到同属性区的角度 = (直线轴角-区轴角) mod π，小者先结算
                _b4CreationLineOrder = _b4CreationLineAxis
                    .OrderBy(kv => ((kv.Value - zoneRots[kv.Key]) % MathF.PI + MathF.PI) % MathF.PI)
                    .Select(kv => kv.Key)
                    .ToList();
                Dbg(sa, $"元素创造直线结算顺序：{string.Join("→", _b4CreationLineOrder)}");
            }
            if (_b4CreationRound >= 3) return;   // 本波已结束，等下一波直线重置
            round = _b4CreationRound++;
            lineElem = _b4CreationLineOrder[round];
        }

        var safeElem = new[] { "火", "冰", "雷" }.First(e => e != ringElem && e != lineElem);

        // 三轮的环也是火冰雷各一次且环≠线：首轮环一出现即可唯一推出后两轮，汇总显示三轮安全区
        if (round == 0)
        {
            List<string> lines;
            lock (_b4CreationLock) lines = _b4CreationLineOrder!.ToList();
            var rest = new[] { "火", "冰", "雷" }.Where(e => e != ringElem).ToList();
            string ring1, ring2;
            if (lines[1] == ringElem)
            {
                ring2 = rest.First(e => e != lines[2]);
                ring1 = rest.First(e => e != ring2);
            }
            else
            {
                ring1 = rest.First(e => e != lines[1]);
                ring2 = rest.First(e => e != ring1);
            }
            var safe1 = new[] { "火", "冰", "雷" }.First(e => e != ring1 && e != lines[1]);
            var safe2 = new[] { "火", "冰", "雷" }.First(e => e != ring2 && e != lines[2]);
            lock (_b4CreationLock) _b4CreationPredictedSafe = [safeElem, safe1, safe2];
            Dbg(sa, $"元素创造预测：环序{ringElem}→{ring1}→{ring2}，安全{safeElem}→{safe1}→{safe2}");
            sa.Method.TextInfo($"去{safeElem}→{safe1}→{safe2}", 15000, false);
        }
        else
        {
            lock (_b4CreationLock)
            {
                if (_b4CreationPredictedSafe is { } pred && pred[round] != safeElem)
                    Dbg(sa, $"元素创造预测失误：第{round + 1}轮实际安全{safeElem}≠预测{pred[round]}");
            }
        }

        // 安全元素上下两块扇区：首轮取离自己近的，之后取上一块逆时针(角度增加)方向最近的
        var zr = zoneRots[safeElem];
        float[] candidates = [zr, zr + MathF.PI];
        float safeRot;
        lock (_b4CreationLock)
        {
            if (_b4CreationPrevSafeRot is null)
            {
                var mePos = sa.Data.MyObject?.Position ?? Boss4中心;
                var meRad = MathF.Atan2(mePos.X - Boss4中心.X, mePos.Z - Boss4中心.Z);
                safeRot = candidates.OrderBy(c => MathF.Abs(WrapPi(c - meRad))).First();
            }
            else
            {
                var prev = _b4CreationPrevSafeRot.Value;
                safeRot = candidates
                    .OrderBy(c => ((c - prev) % (2 * MathF.PI) + 2 * MathF.PI) % (2 * MathF.PI))
                    .First();
            }
            _b4CreationPrevSafeRot = safeRot;
        }

        // 本轮环出现时上一轮还有约2.4s判定(6.8-4.4)：非首轮延迟到上一轮判定后再显示
        var delay = (uint)(round == 0 ? 0 : 2400);
        var duration = 6800u - delay;
        Dbg(sa, $"元素创造第{round + 1}轮：环{ringElem}+线{lineElem}，安全={safeElem}，" +
                $"扇区角 {safeRot * 180f / MathF.PI:F0}°，{delay}ms后显示{duration}ms");

        var dp = sa.FastDp($"元素创造安全区-{round}", Boss4中心, duration, new Vector2(40f), safe: true);
        dp.Rotation = safeRot;
        dp.Radian = 60f * MathF.PI / 180f;
        dp.Delay = delay;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        var guidePos = Boss4中心 + new Vector3(MathF.Sin(safeRot), 0, MathF.Cos(safeRot)) * 9f;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement,
            sa.WaypointDp(guidePos, duration, delay, $"元素创造安全区指路-{round}"));
    }

    // —— 预言（48412）——
    private const uint 幻影DataId = 19311;
    private const int 预言结算时间 = 9700;   // 预言现象出现到结算

    [ScriptMethod(name: "超魔BOSS4 - 预言现象", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19307"])]
    public void 超魔B4预言现象(Event evt, ScriptAccessory sa) => _ = B4预言现象核心(evt, sa);

    private async Task B4预言现象核心(Event evt, ScriptAccessory sa)
    {
        var oid = evt.SourceId();
        var spawnPos = evt.SourcePosition();
        var startAt = Environment.TickCount64;

        // 等状态附加后读球上的2552：1101钢铁 / 1100月环
        var param = 0;
        while (param == 0 && Environment.TickCount64 - startAt < 4000)
        {
            await Task.Delay(300);
            if (sa.Data.Objects.SearchById(oid) is not IBattleChara orb) return;
            foreach (var s in orb.StatusList)
            {
                if (s.StatusId != 2552 || (s.Param != 1100 && s.Param != 1101)) continue;
                param = s.Param;
                break;
            }
        }
        if (param == 0)
        {
            Dbg(sa, $"预言现象 {oid:X8}：未读到2552(1100/1101)，跳过");
            return;
        }
        var isDonut = param == 1100;
        Dbg(sa, $"预言现象 {oid:X8}：{(isDonut ? "月环" : "钢铁")}，等待飘移判向");

        // 5s后开始飘移：6s起每0.3s取实时位置，飘出0.7m即可判定目标幻影
        var wait = 6000 - (int)(Environment.TickCount64 - startAt);
        if (wait > 0) await Task.Delay(wait);

        while (Environment.TickCount64 - startAt < 预言结算时间 - 700)
        {
            if (sa.Data.Objects.SearchById(oid) is not { } orbObj) return;
            var moved = orbObj.Position - spawnPos;
            moved.Y = 0;
            if (moved.Length() >= 0.7f)
            {
                var phantom = sa.Data.Objects
                    .Where(o => o != null && o.DataId == 幻影DataId)
                    .OrderByDescending(o =>
                    {
                        var to = o.Position - spawnPos;
                        to.Y = 0;
                        return to.Length() < 0.1f ? float.MinValue
                            : Vector3.Dot(Vector3.Normalize(moved), Vector3.Normalize(to));
                    })
                    .FirstOrDefault();
                if (phantom is null)
                {
                    Dbg(sa, $"预言现象 {oid:X8}：场上找不到幻影(19311)，跳过");
                    return;
                }

                var remain = (uint)Math.Max(预言结算时间 - (Environment.TickCount64 - startAt), 1000);
                Dbg(sa, $"预言现象 {oid:X8}：目标幻影 {phantom.EntityId:X8} pos {phantom.Position:F1}，" +
                        $"{(isDonut ? "月环" : "钢铁")}显示{remain}ms");

                if (isDonut)
                {
                    var dp = sa.FastDp($"预言月环-{oid:X8}", phantom.Position, remain, new Vector2(15f));
                    dp.InnerScale = new Vector2(4f);
                    dp.Radian = float.Pi * 2;
                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                }
                else
                {
                    var dp = sa.FastDp($"预言钢铁-{oid:X8}", phantom.Position, remain, new Vector2(10f));
                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
                return;
            }
            await Task.Delay(300);
        }
        Dbg(sa, $"预言现象 {oid:X8}：到结算前仍未观测到飘移，跳过");
    }

    #endregion

    #region 钟灵时钟
    // 进入 MapId 1187 开始检测，收到 EnvControl Index 17 flag 8 结束（离开该图也结束）。
    // flag 给出本轮要求把钟推到的目标时间，钟面固定从 12:00 起走，指针只前进。
    // 钟灵推进指针：19770最大=时针+3格 / 19771次大=时针+1格 / 19772次小=分针+3格(15分) / 19773最小=分针+1格(5分)。
    // 全部走首击判定：谁第一下打到这只钟灵，就按 EntityId 计一次格数（被秒了也算），同时判开怪的是不是 T。
    private const uint 钟灵MapId = 1187;
    private const int 时钟结束Flag = 8;
    private static readonly Dictionary<int, (int 时, int 分)> 时钟目标表 = new()
    {
        [2]    = (8, 45),
        [16]   = (9, 40),
        [32]   = (10, 45),
        [64]   = (11, 30),
        [128]  = (5, 55),
        [256]  = (8, 50),
        [512]  = (10, 50),
        [1024] = (11, 35),
        [2048] = (11, 45),
    };

    // DataId → 该钟灵推进的格数与名称
    private static readonly Dictionary<uint, (int 时格, int 分格, string 名)> 钟灵表 = new()
    {
        [19770] = (3, 0, "最大"),
        [19771] = (1, 0, "次大"),
        [19772] = (0, 3, "次小"),
        [19773] = (0, 1, "最小"),
    };

    private readonly object _钟灵Lock = new();
    private bool _钟灵检测中;
    private (int 时, int 分)? _钟灵目标;
    private (int 时格, int 分格) _钟灵已投入;
    private readonly HashSet<uint> _钟灵首击 = [];   // 已处理过首击的 EntityId，同一只只算一次

    // 进 1187 开检测、离开就关；ChangeMap 会重复触发，状态没变就不输出
    [ScriptMethod(name: "钟灵时钟 - 检测开关", eventType: EventTypeEnum.ChangeMap, userControl: false)]
    public void 钟灵检测开关(Event evt, ScriptAccessory sa)
    {
        var 在钟灵图 = 钟灵时钟启用 && uint.TryParse(evt["MapId"], out var mapId) && mapId == 钟灵MapId;
        lock (_钟灵Lock)
        {
            if (_钟灵检测中 == 在钟灵图) return;
            _钟灵检测中 = 在钟灵图;
            重置钟灵进度();
        }
        if (!在钟灵图) 结束钟灵检测(sa);
        Dbg(sa, 在钟灵图 ? $"钟灵时钟：进入 {钟灵MapId}，开始检测" : "钟灵时钟：离开该图，停止检测");
    }

    // 调用方必须持有 _钟灵Lock
    private void 重置钟灵进度()
    {
        _钟灵目标 = null;
        _钟灵已投入 = (0, 0);
        _钟灵首击.Clear();
    }

    private void 结束钟灵检测(ScriptAccessory sa)
    {
        lock (_钟灵Lock)
        {
            _钟灵检测中 = false;
            重置钟灵进度();
        }
        sa.Method.RemoveDraw("钟灵拉错-.*");
    }

    [ScriptMethod(name: "钟灵时钟 - 目标时间播报", eventType: EventTypeEnum.EnvControl, eventCondition: ["Index:17"])]
    public void 钟灵时钟播报(Event evt, ScriptAccessory sa)
    {
        if (!钟灵时钟启用) { 结束钟灵检测(sa); return; }   // 运行中关掉设置：顺手收干净

        bool 检测中;
        lock (_钟灵Lock) 检测中 = _钟灵检测中;
        if (!检测中)
        {
            Dbg(sa, $"钟灵时钟：不在检测窗口内，忽略 flag {evt["Flag"]}");
            return;
        }
        if (!int.TryParse(evt["Flag"], out var flag))
        {
            Dbg(sa, $"钟灵时钟：Flag 解析失败（原始 {evt["Flag"]}）");
            return;
        }
        if (flag == 时钟结束Flag)
        {
            结束钟灵检测(sa);
            Dbg(sa, "钟灵时钟：flag 8，结束检测");
            return;
        }
        if (!时钟目标表.TryGetValue(flag, out var 目标))
        {
            Dbg(sa, $"钟灵时钟：未知 flag {flag}，未收录");
            return;
        }

        // 新一轮：钟面回到 12:00，上一轮的首击计数作废
        lock (_钟灵Lock)
        {
            重置钟灵进度();
            _钟灵目标 = 目标;
        }
        sa.Method.RemoveDraw("钟灵拉错-.*");

        var 需求 = 拆解格数(目标.时 % 12, 目标.分 / 5 % 12);
        Dbg(sa, $"钟灵时钟：flag {flag} → 目标 {目标.时}:{目标.分:00}，需 最大{需求.最大} 次大{需求.次大} 次小{需求.次小} 最小{需求.最小}");
        播报钟灵需求(sa);
    }

    // 全量 EnvControl 日志，用来补收未收录的 flag（仅 Debug 输出开启时可见）
    // [ScriptMethod(name: "钟灵时钟 - EnvControl全量日志", eventType: EventTypeEnum.EnvControl, userControl: false)]
    // public void 钟灵EnvControl日志(Event evt, ScriptAccessory sa)
    // {
    //     if (!钟灵时钟启用) return;
    //     Dbg(sa, $"EnvControl Index={evt["Index"]} Flag={evt["Flag"]} DirectorId={evt["DirectorId"]} Id={evt["Id"]}");
    // }

    // 首击 = 这只钟灵被打的第一下：计一次格数并重播需求，同时判开怪的是不是 T，一只只处理一次。
    // 这里不加 eventCondition：TargetDataId 键在 ActionEffect 上没实测过，改用 TargetId 查对象判 DataId；
    [ScriptMethod(name: "钟灵时钟 - 首击计数与非T提醒", eventType: EventTypeEnum.ActionEffect)]
    public void 钟灵首击检查(Event evt, ScriptAccessory sa)
    {
        if (!钟灵时钟启用) return;
        lock (_钟灵Lock) if (!_钟灵检测中) return;

        var 目标Id = evt.TargetId();
        if (目标Id == 0) return;
        var 钟灵 = sa.Data.Objects.SearchById(目标Id);
        if (钟灵 is null || !钟灵表.TryGetValue(钟灵.DataId, out var 格)) return;

        var 计入 = false;
        lock (_钟灵Lock)
        {
            if (!_钟灵首击.Add(目标Id)) return;   // 只认第一下
            if (_钟灵目标 is not null)
            {
                _钟灵已投入 = (_钟灵已投入.时格 + 格.时格, _钟灵已投入.分格 + 格.分格);
                计入 = true;
                Dbg(sa, $"钟灵首击：{格.名} {目标Id:X8}，累计 时针{_钟灵已投入.时格}格 分针{_钟灵已投入.分格}格");
            }
        }
        if (计入) 播报钟灵需求(sa);

        // 宠物/召唤兽先蹭到第一下时算到主人头上
        var 来源 = sa.Data.Objects.SearchById(evt.SourceId());
        var 打手 = 来源 as IPlayerCharacter
                   ?? (来源 is { OwnerId: not 0 } ? sa.Data.Objects.SearchById(来源.OwnerId) as IPlayerCharacter : null);
        if (打手 is null) return;
        if (打手.IsTank())
        {
            Dbg(sa, $"钟灵首击：{格.名} {目标Id:X8} 由 {打手.Name} 开打（T）");
            return;
        }

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"钟灵拉错-{目标Id}";
        dp.Color = new Vector4(1, 0, 0, 1);
        dp.Owner = 目标Id;
        dp.Scale = new Vector2(钟灵.HitboxRadius);
        dp.InnerScale = new Vector2(钟灵.HitboxRadius + 1f);
        dp.Radian = float.Pi * 2;
        dp.DestoryAt = 60000;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

        sa.Method.TextInfo($"钟灵首击异常：{格.名} 由 {打手.Name} 开怪，检查是否拉错", 8000, true);
        sa.Method.TTS($"{格.名}钟灵首击异常", 3);
        Dbg(sa, $"钟灵首击异常：{格.名} {目标Id:X8} 由 {打手.Name} 开怪");
    }

    private void 播报钟灵需求(ScriptAccessory sa)
    {
        (int 时, int 分) 目标;
        (int 时格, int 分格) 已投入;
        lock (_钟灵Lock)
        {
            if (!_钟灵检测中 || _钟灵目标 is null) return;
            目标 = _钟灵目标.Value;
            已投入 = _钟灵已投入;
        }

        var 剩余时格 = 目标.时 % 12 - 已投入.时格;
        var 剩余分格 = 目标.分 / 5 % 12 - 已投入.分格;

        List<string> 超出 = [];
        if (剩余时格 < 0) 超出.Add($"时针多{-剩余时格}格");
        if (剩余分格 < 0) 超出.Add($"分针多{-剩余分格}格");

        var 需求 = 拆解格数(Math.Max(剩余时格, 0), Math.Max(剩余分格, 0));
        List<(int 数量, string 名)> 项 = [(需求.最大, "最大"), (需求.次大, "次大"), (需求.次小, "次小"), (需求.最小, "最小")];
        项.RemoveAll(x => x.数量 <= 0);

        var 还需 = 项.Count == 0 ? "" : $"还需 {string.Join(" ", 项.Select(x => $"{x.名}×{x.数量}"))}";
        var 头 = $"目标 {目标.时}:{目标.分:00}";

        if (超出.Count > 0)
        {
            var 超文 = string.Join("，", 超出);
            // sa.Method.TextInfo($"{头} 打多了：{超文}{(还需 == "" ? "" : $"/{还需}")}", 1500, true);
            // sa.Method.TTS("打多了", 3);
            sa.Method.SendChat($"/e {头} 打多了：{超文}{(还需 == "" ? "" : $"/{还需}")} <se.10>");
            return;
        }

        if (项.Count == 0)
        {
            // sa.Method.TextInfo($"{头} 已凑齐", 4000, false);
            sa.Method.TTS("齐了", 3);
            return;
        }

        // sa.Method.TextInfo($"{头} → {还需}", 1500, false);
        // sa.Method.TTS(string.Join("", 项.Select(x => $"{x.名}{x.数量}")), 3);
        sa.Method.SendChat($"/e {头} → {还需} <se.1>");
    }

    // 时针每格1小时、分针每格5分钟：先用大格(3格)再用小格(1格)，即击杀数最少的组合
    private static (int 最大, int 次大, int 次小, int 最小) 拆解格数(int 时格, int 分格)
        => (时格 / 3, 时格 % 3, 分格 / 3, 分格 % 3);

    #endregion

    #region 排雷
    // 塔内地雷点位表见文件末尾 地雷数据库，按 MapId 分类。进入有雷的地图自动画出全部点位，
    // 盗贼扫雷 / 猎人排雷 / 雷实体生成 / 雷爆炸 / 有人踩过而没炸 这五种情况都会把对应点位抹掉。
    private const uint 大雷DataId = 2014585;
    private const uint 小雷DataId = 2014584;
    private const long 雷点显示时长 = 1800000;
    private const float 雷点匹配半径 = 1.5f;       // 事件坐标 → 点位表的匹配容差
    private const float 踩点判定半径 = 1.5f;       // 比雷的触发范围保守，避免擦边路过就误消
    private const float 踩点高度容差 = 3f;         // 塔内各区域 Y 不同，防止上下层坐标重叠误消
    private const long 踩点扫描间隔 = 100;         // ms，不必每帧全量比对

    private bool _雷点已显示;
    private uint _当前MapId;
    private readonly object _mineLock = new();
    private readonly HashSet<string> _踩点已排除 = [];   // 已踩过的点，避免每帧重复 RemoveDraw
    private string? _踩点扫描Guid;
    private long _上次踩点扫描;

    private static string 雷点名(int g, int m) => $"FTM_Mine_G{g}_M{m}";
    private static string 爆点名(int g, int m) => $"FTM_Boom_G{g}_M{m}";

    // 切图 / 团灭重置钩子
    public void Init(ScriptAccessory sa)
    {
        lock (_mineLock)
        {
            _雷点已显示 = false;
            _当前MapId = 0;
            _踩点已排除.Clear();
        }
        结束钟灵检测(sa);
        sa.Method.RemoveDraw("FTM_(Mine|Boom)_.*");
        注册踩点扫描(sa);
    }

    [ScriptMethod(name: "排雷 - 手动开关地雷显示", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:新月排雷"])]
    public void 手动开关排雷(Event evt, ScriptAccessory sa)
    {
        if (!排雷显示) { sa.Method.TextInfo("排雷功能已在设置中关闭。", 2000); return; }
        lock (_mineLock)
        {
            if (!地雷数据库.MinesByMap.ContainsKey(_当前MapId))
            {
                sa.Method.TextInfo("当前地图无地雷数据。", 2000);
                return;
            }

            _雷点已显示 = !_雷点已显示;
            if (_雷点已显示)
            {
                画出地雷点(sa, _当前MapId);
                sa.Method.TextInfo("显示地雷位置", 2000);
            }
            else
            {
                sa.Method.RemoveDraw("FTM_Mine_.*");
                sa.Method.TextInfo("隐藏地雷位置", 2000);
            }
        }
    }

    #region 排雷 - 进图
    [ScriptMethod(name: "排雷 - 切图处理", eventType: EventTypeEnum.ChangeMap, userControl: false)]
    public void 排雷切图(Event evt, ScriptAccessory sa)
    {
        if (!uint.TryParse(evt["MapId"], out var mapId)) return;
        if (mapId is 1183 or 1184) 沿用上一张图标记(mapId, sa);
        else if (地雷数据库.MinesByMap.ContainsKey(mapId)) 进入雷区(mapId, sa);
        else 离开雷区(mapId, sa);
    }

    // 无雷区域：收掉上一张图残留的标记并复位状态，避免踩点扫描继续按老图点位表误消点。
    private void 离开雷区(uint mapId, ScriptAccessory sa)
    {
        lock (_mineLock)
        {
            if (mapId == _当前MapId) return;   // ChangeMap 会重复触发
            _当前MapId = mapId;
            _雷点已显示 = false;
            _踩点已排除.Clear();
            sa.Method.RemoveDraw("FTM_(Mine|Boom)_.*");
        }
        Dbg(sa, $"进入无雷区域 ({mapId})，已清除地雷标记。");
    }

    // 1182 → 1183 → 1184 是同一片区域随进度换的 MapId，点位表是 1182 的子集。
    // 换到 1183/1184 时不动任何东西：既不清图也不重画，继续沿用上一张图已经排掉一部分的标记
    // （重画会把已经确认过的点又变回来），_当前MapId 也保持不变，扫雷/爆炸/踩点仍按原图的点位索引对应。
    private void 沿用上一张图标记(uint mapId, ScriptAccessory sa)
        => Dbg(sa, $"进入 {mapId}，沿用上一张图（{_当前MapId}）的地雷标记。");

    private async void 进入雷区(uint mapId, ScriptAccessory sa)
    {
        if (!排雷显示) return;
        if (mapId == _当前MapId) return;   // ChangeMap 会重复触发

        uint newMapId;
        lock (_mineLock)
        {
            _当前MapId = mapId;
            newMapId = _当前MapId;
            sa.Method.RemoveDraw("FTM_(Mine|Boom)_.*");
        }
        await Task.Delay(50);
        lock (_mineLock)
        {
            if (_当前MapId != newMapId) return;
            _雷点已显示 = true;
            画出地雷点(sa, newMapId);
            Dbg(sa, $"进入地雷区域 ({newMapId})，已自动显示标记。");
        }
    }
    #endregion

    private void 画出地雷点(ScriptAccessory sa, uint mapId)
    {
        if (!地雷数据库.MinesByMap.TryGetValue(mapId, out var mineGroups)) return;
        _踩点已排除.Clear();
        注册踩点扫描(sa);

        var 小雷色 = new Vector4(1.0f, 0.65f, 0.0f, 2.0f);
        var 大雷色 = new Vector4(0.86f, 0.08f, 0.23f, 2.0f);

        for (var g = 0; g < mineGroups.Count; g++)
        {
            for (var m = 0; m < mineGroups[g].Mines.Count; m++)
            {
                var mine = mineGroups[g].Mines[m];
                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = 雷点名(g, m);
                dp.Position = mine.Position;
                dp.DestoryAt = 雷点显示时长;
                dp.Color = mine.IsLarge ? 大雷色 : 小雷色;
                dp.Scale = new Vector2(4f);
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
            }
        }
    }

    // 命中点位表就回调 (组号, 点号)，radius 内全部命中；stopAtFirst 只处理第一个
    private void 遍历命中雷点(Vector3 pos, float radius, bool stopAtFirst, Action<int, int> onHit)
    {
        if (!地雷数据库.MinesByMap.TryGetValue(_当前MapId, out var mineGroups)) return;
        for (var g = 0; g < mineGroups.Count; g++)
        {
            for (var m = 0; m < mineGroups[g].Mines.Count; m++)
            {
                if (Vector3.Distance(mineGroups[g].Mines[m].Position, pos) > radius) continue;
                onHit(g, m);
                if (stopAtFirst) return;
            }
        }
    }

    // 41648 盗贼扫雷：以自身为心 15m 内的雷位全部确认
    [ScriptMethod(name: "排雷 - 盗贼扫雷", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:41648"])]
    public void 盗贼扫雷(Event evt, ScriptAccessory sa)
    {
        if (!排雷显示) return;
        遍历命中雷点(evt.SourcePosition(), 15f, false, (g, m) => sa.Method.RemoveDraw(雷点名(g, m)));
    }

    // 41601 猎人排雷：落点 9m 内的雷位全部确认
    [ScriptMethod(name: "排雷 - 猎人排雷", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:41601"])]
    public void 猎人排雷(Event evt, ScriptAccessory sa)
    {
        if (!排雷显示) return;
        遍历命中雷点(evt.EffectPosition(), 9f, false, (g, m) => sa.Method.RemoveDraw(雷点名(g, m)));
    }

    [ScriptMethod(name: "排雷 - 大雷生成", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2014585"], userControl: false)]
    public void 大雷生成(Event evt, ScriptAccessory sa) => 雷实体生成(evt.SourcePosition(), true, sa);

    [ScriptMethod(name: "排雷 - 小雷生成", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2014584"], userControl: false)]
    public void 小雷生成(Event evt, ScriptAccessory sa) => 雷实体生成(evt.SourcePosition(), false, sa);

    // 雷实体现形 = 这一组已经确定，抹掉整组点位，改画实际爆炸范围（大雷 30m / 小雷 7m）
    private async void 雷实体生成(Vector3 pos, bool isLarge, ScriptAccessory sa)
    {
        if (!排雷显示) return;
        if (!地雷数据库.MinesByMap.TryGetValue(_当前MapId, out var mineGroups)) return;

        var hitG = -1;
        var hitM = -1;
        遍历命中雷点(pos, 雷点匹配半径, true, (g, m) => { hitG = g; hitM = m; });
        if (hitG < 0) return;

        for (var m = 0; m < mineGroups[hitG].Mines.Count; m++)
            sa.Method.RemoveDraw(雷点名(hitG, m));

        await Task.Delay(50);   // 等 Remove 处理完再画，避免同帧被顺带清掉

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = 爆点名(hitG, hitM);
        dp.Position = pos;
        dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 0.6f);
        dp.DestoryAt = 1000000;
        dp.Scale = isLarge ? new Vector2(30f) : new Vector2(7f);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }

    // 42050 / 42051 雷爆炸：炸完了，点位和爆炸圈一起收掉
    [ScriptMethod(name: "排雷 - 雷爆炸", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(42050|42051)$"], userControl: false)]
    public void 雷爆炸(Event evt, ScriptAccessory sa)
    {
        遍历命中雷点(evt.SourcePosition(), 雷点匹配半径, true, (g, m) =>
        {
            sa.Method.RemoveDraw(雷点名(g, m));
            sa.Method.RemoveDraw(爆点名(g, m));
        });
    }

    private void 注册踩点扫描(ScriptAccessory sa)
    {
        if (_踩点扫描Guid != null) return;
        _踩点扫描Guid = sa.Method.RegistFrameworkUpdateAction(() => 踩点扫描(sa));
    }

    // 有玩家（含非小队玩家）走进某雷位而没炸，说明这点没雷，抹掉标记
    private void 踩点扫描(ScriptAccessory sa)
    {
        var now = Environment.TickCount64;
        if (now - _上次踩点扫描 < 踩点扫描间隔) return;
        _上次踩点扫描 = now;

        lock (_mineLock)
        {
            if (!排雷显示)
            {
                // 运行中关掉设置：立刻收掉已有绘图
                if (!_雷点已显示) return;
                _雷点已显示 = false;
                sa.Method.RemoveDraw("FTM_(Mine|Boom)_.*");
                return;
            }
            if (!_雷点已显示) return;
            if (!地雷数据库.MinesByMap.TryGetValue(_当前MapId, out var mineGroups)) return;

            var players = sa.Data.Objects
                .OfType<IPlayerCharacter>()
                .Where(p => !p.IsDead)
                .Select(p => p.Position)
                .ToList();
            if (players.Count == 0) return;

            for (var g = 0; g < mineGroups.Count; g++)
            {
                for (var m = 0; m < mineGroups[g].Mines.Count; m++)
                {
                    var name = 雷点名(g, m);
                    if (_踩点已排除.Contains(name)) continue;
                    var minePos = mineGroups[g].Mines[m].Position;
                    if (!players.Any(p => MathF.Abs(p.Y - minePos.Y) <= 踩点高度容差
                                          && DistXZ(p, minePos) <= 踩点判定半径)) continue;

                    _踩点已排除.Add(name);
                    sa.Method.RemoveDraw(name);
                }
            }
        }
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
    public static uint DirectorId(this Event evt) => ParseHexId(evt["DirectorId"], out var id) ? id : 0;
}

public static class ScriptAccessoryExtensions
{
    public static int MyIndex(this ScriptAccessory sa) => sa.Data.PartyList.IndexOf(sa.Data.Me);

    public static IGameObject? FindByDataId(this ScriptAccessory sa, params uint[] dataIds)
        => sa.Data.Objects.FirstOrDefault(x => x != null && dataIds.Contains(x.DataId));

    // 修改物体可选中状态
    public static void SetTargetable(this ScriptAccessory sa, IGameObject? obj, bool targetable)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            if (targetable)
            {
                if (obj.IsDead || obj.IsTargetable) return;
                charaStruct->TargetableStatus |= ObjectTargetableFlags.IsTargetable;
            }
            else
            {
                if (!obj.IsTargetable) return;
                charaStruct->TargetableStatus &= ~ObjectTargetableFlags.IsTargetable;
            }
        }
        sa.Log.Debug($"SetTargetable {targetable} => {obj.Name} {obj}");
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

#region 排雷点位表

internal static class 地雷数据库
{
    public struct Mine
    {
        public Vector3 Position;
        public bool IsLarge;
    }

    public class MineGroup
    {
        public List<Mine> Mines = new List<Mine>();
    }

    // 数据已按地图ID分类
    public static readonly Dictionary<uint, List<MineGroup>> MinesByMap = new Dictionary<uint, List<MineGroup>>
    {
        // --- Map ID: 1178 Data ---  (3 组 / 18 雷)
        [1178] = new List<MineGroup>
        {
            // 区域 26: x[554.5~568.5] z[922.5~929.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(554.5f, -699.901f, 922.5f), IsLarge = false }, new Mine { Position = new Vector3(554.5f, -699.9f, 929.5f), IsLarge = false },
                new Mine { Position = new Vector3(561.5f, -700f, 922.5f), IsLarge = false }, new Mine { Position = new Vector3(561.5f, -700.001f, 929.5f), IsLarge = false },
                new Mine { Position = new Vector3(568.5f, -699.941f, 922.5f), IsLarge = false }, new Mine { Position = new Vector3(568.5f, -699.941f, 929.5f), IsLarge = false },
            }},
            // 区域 27: x[631.5~645.5] z[922.5~929.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(631.5f, -699.941f, 922.5f), IsLarge = false }, new Mine { Position = new Vector3(631.5f, -699.941f, 929.5f), IsLarge = false },
                new Mine { Position = new Vector3(638.5f, -700.001f, 922.5f), IsLarge = false }, new Mine { Position = new Vector3(638.5f, -700.001f, 929.5f), IsLarge = false },
                new Mine { Position = new Vector3(645.5f, -699.901f, 922.5f), IsLarge = false }, new Mine { Position = new Vector3(645.5f, -699.901f, 929.5f), IsLarge = false },
            }},
            // 区域 28: x[596.5~603.5] z[943~957]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(596.5f, -700f, 943f), IsLarge = false }, new Mine { Position = new Vector3(596.5f, -699.94f, 950f), IsLarge = false },
                new Mine { Position = new Vector3(596.5f, -700f, 957f), IsLarge = false }, new Mine { Position = new Vector3(603.5f, -700f, 943f), IsLarge = false },
                new Mine { Position = new Vector3(603.5f, -699.94f, 950f), IsLarge = false }, new Mine { Position = new Vector3(603.5f, -700f, 957f), IsLarge = false },
            }},
        },
        // --- Map ID: 1179 Data ---  (12 组 / 40 雷)
        [1179] = new List<MineGroup>
        {
            // 区域 11: x[463.5~482.5] z[728.5~735.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(463.5f, -680f, 732f), IsLarge = false }, new Mine { Position = new Vector3(469.5f, -680f, 728.5f), IsLarge = false },
                new Mine { Position = new Vector3(469.5f, -680f, 735.5f), IsLarge = false }, new Mine { Position = new Vector3(476.5f, -680f, 735.5f), IsLarge = false },
                new Mine { Position = new Vector3(482.5f, -680f, 732f), IsLarge = false },
            }},
            // 区域 13: x[365~375] z[758.5~765.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(365f, -698f, 765.5f), IsLarge = false }, new Mine { Position = new Vector3(375f, -698f, 758.5f), IsLarge = false },
            }},
            // 区域 15: x[386~400] z[772~792]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(386f, -700f, 772f), IsLarge = true }, new Mine { Position = new Vector3(386f, -699.94f, 782f), IsLarge = true },
                new Mine { Position = new Vector3(386f, -700f, 792f), IsLarge = true }, new Mine { Position = new Vector3(393f, -700f, 772f), IsLarge = true },
                new Mine { Position = new Vector3(393f, -700f, 782f), IsLarge = true }, new Mine { Position = new Vector3(393f, -700f, 792f), IsLarge = true },
                new Mine { Position = new Vector3(400f, -700f, 772f), IsLarge = true }, new Mine { Position = new Vector3(400f, -699.94f, 782f), IsLarge = true },
                new Mine { Position = new Vector3(400f, -700f, 792f), IsLarge = true },
            }},
            // 区域 17: x[596.5~603.5] z[776~776]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(596.5f, -684f, 776f), IsLarge = true }, new Mine { Position = new Vector3(603.5f, -684f, 776f), IsLarge = false },
            }},
            // 区域 18: x[463.5~482.5] z[780.5~787.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(463.5f, -680f, 784f), IsLarge = true }, new Mine { Position = new Vector3(469.5f, -680f, 780.5f), IsLarge = true },
                new Mine { Position = new Vector3(469.5f, -680f, 787.5f), IsLarge = true }, new Mine { Position = new Vector3(476.5f, -680f, 780.5f), IsLarge = true },
                new Mine { Position = new Vector3(476.5f, -680f, 787.5f), IsLarge = true }, new Mine { Position = new Vector3(482.5f, -680f, 784f), IsLarge = true },
            }},
            // 区域 20: x[365~375] z[798.5~805.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(365f, -698f, 798.5f), IsLarge = false }, new Mine { Position = new Vector3(365f, -698f, 805.5f), IsLarge = false },
                new Mine { Position = new Vector3(375f, -698f, 805.5f), IsLarge = false },
            }},
            // 区域 22: x[561~561] z[825.5~832.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(561f, -680f, 825.5f), IsLarge = false }, new Mine { Position = new Vector3(561f, -680f, 832.5f), IsLarge = false },
            }},
            // 区域 23: x[639~639] z[825.5~825.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(639f, -680f, 825.5f), IsLarge = false },
            }},
            // 区域 24: x[514.5~521.5] z[849~861]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(514.5f, -680f, 849f), IsLarge = false }, new Mine { Position = new Vector3(514.5f, -680f, 861f), IsLarge = false },
                new Mine { Position = new Vector3(521.5f, -680f, 849f), IsLarge = false }, new Mine { Position = new Vector3(521.5f, -680f, 861f), IsLarge = false },
            }},
            // 区域 25: x[678.5~685.5] z[849~861]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(678.5f, -680f, 849f), IsLarge = false }, new Mine { Position = new Vector3(685.5f, -680f, 861f), IsLarge = false },
            }},
            // 区域 26: x[561.5~568.5] z[922.5~922.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(561.5f, -700f, 922.5f), IsLarge = false }, new Mine { Position = new Vector3(568.5f, -699.941f, 922.5f), IsLarge = false },
            }},
            // 区域 27: x[638.5~645.5] z[922.5~922.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(638.5f, -700.001f, 922.5f), IsLarge = false }, new Mine { Position = new Vector3(645.5f, -699.901f, 922.5f), IsLarge = false },
            }},
        },
        // --- Map ID: 1180 Data ---  (14 组 / 58 雷)
        [1180] = new List<MineGroup>
        {
            // 区域 10: x[763.5~770.5] z[660~672]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(763.5f, -690f, 660f), IsLarge = false }, new Mine { Position = new Vector3(763.5f, -690f, 672f), IsLarge = false },
                new Mine { Position = new Vector3(770.5f, -690f, 660f), IsLarge = false }, new Mine { Position = new Vector3(770.5f, -690f, 672f), IsLarge = false },
            }},
            // 区域 12: x[717.5~736.5] z[728.5~735.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(717.5f, -680f, 732f), IsLarge = false }, new Mine { Position = new Vector3(723.5f, -680f, 728.5f), IsLarge = false },
                new Mine { Position = new Vector3(723.5f, -680f, 735.5f), IsLarge = false }, new Mine { Position = new Vector3(730.5f, -680f, 728.5f), IsLarge = false },
                new Mine { Position = new Vector3(730.5f, -680f, 735.5f), IsLarge = false }, new Mine { Position = new Vector3(736.5f, -680f, 732f), IsLarge = false },
            }},
            // 区域 14: x[825~835] z[758.5~765.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(825f, -698f, 758.5f), IsLarge = false }, new Mine { Position = new Vector3(825f, -698f, 765.5f), IsLarge = false },
                new Mine { Position = new Vector3(835f, -698f, 758.5f), IsLarge = false }, new Mine { Position = new Vector3(835f, -698f, 765.5f), IsLarge = false },
            }},
            // 区域 16: x[800~814] z[772~792]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(800f, -700f, 772f), IsLarge = true }, new Mine { Position = new Vector3(800f, -699.94f, 782f), IsLarge = true },
                new Mine { Position = new Vector3(800f, -700f, 792f), IsLarge = true }, new Mine { Position = new Vector3(807f, -700f, 772f), IsLarge = true },
                new Mine { Position = new Vector3(807f, -700f, 782f), IsLarge = true }, new Mine { Position = new Vector3(807f, -700f, 792f), IsLarge = true },
                new Mine { Position = new Vector3(814f, -700f, 772f), IsLarge = true }, new Mine { Position = new Vector3(814f, -699.94f, 782f), IsLarge = true },
                new Mine { Position = new Vector3(814f, -700f, 792f), IsLarge = true },
            }},
            // 区域 17: x[596.5~603.5] z[776~776]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(596.5f, -684f, 776f), IsLarge = false }, new Mine { Position = new Vector3(596.5f, -684f, 776f), IsLarge = true },
                new Mine { Position = new Vector3(603.5f, -684f, 776f), IsLarge = false }, new Mine { Position = new Vector3(603.5f, -684f, 776f), IsLarge = true },
            }},
            // 区域 19: x[717.5~736.5] z[780.5~787.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(717.5f, -680f, 784f), IsLarge = true }, new Mine { Position = new Vector3(723.5f, -680f, 780.5f), IsLarge = true },
                new Mine { Position = new Vector3(723.5f, -680f, 787.5f), IsLarge = true }, new Mine { Position = new Vector3(730.5f, -680f, 780.5f), IsLarge = true },
                new Mine { Position = new Vector3(730.5f, -680f, 787.5f), IsLarge = true }, new Mine { Position = new Vector3(736.5f, -680f, 784f), IsLarge = true },
            }},
            // 区域 21: x[825~835] z[798.5~805.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(825f, -698f, 798.5f), IsLarge = false }, new Mine { Position = new Vector3(825f, -698f, 805.5f), IsLarge = false },
                new Mine { Position = new Vector3(835f, -698f, 798.5f), IsLarge = false }, new Mine { Position = new Vector3(835f, -698f, 805.5f), IsLarge = false },
            }},
            // 区域 22: x[561~561] z[825.5~832.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(561f, -680f, 825.5f), IsLarge = false }, new Mine { Position = new Vector3(561f, -680f, 832.5f), IsLarge = false },
            }},
            // 区域 23: x[639~639] z[825.5~832.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(639f, -680f, 825.5f), IsLarge = false }, new Mine { Position = new Vector3(639f, -680f, 832.5f), IsLarge = false },
            }},
            // 区域 24: x[514.5~521.5] z[849~861]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(514.5f, -680f, 849f), IsLarge = false }, new Mine { Position = new Vector3(514.5f, -680f, 861f), IsLarge = false },
                new Mine { Position = new Vector3(521.5f, -680f, 849f), IsLarge = false }, new Mine { Position = new Vector3(521.5f, -680f, 861f), IsLarge = false },
            }},
            // 区域 25: x[678.5~685.5] z[849~861]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(678.5f, -680f, 849f), IsLarge = false }, new Mine { Position = new Vector3(678.5f, -680f, 861f), IsLarge = false },
                new Mine { Position = new Vector3(685.5f, -680f, 849f), IsLarge = false }, new Mine { Position = new Vector3(685.5f, -680f, 861f), IsLarge = false },
            }},
            // 区域 26: x[561.5~568.5] z[922.5~929.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(561.5f, -700.001f, 929.5f), IsLarge = false }, new Mine { Position = new Vector3(568.5f, -699.941f, 922.5f), IsLarge = false },
            }},
            // 区域 27: x[631.5~645.5] z[922.5~929.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(631.5f, -699.941f, 922.5f), IsLarge = false }, new Mine { Position = new Vector3(631.5f, -699.941f, 929.5f), IsLarge = false },
                new Mine { Position = new Vector3(638.5f, -700.001f, 922.5f), IsLarge = false }, new Mine { Position = new Vector3(645.5f, -699.901f, 922.5f), IsLarge = false },
                new Mine { Position = new Vector3(645.5f, -699.901f, 929.5f), IsLarge = false },
            }},
            // 区域 28: x[596.5~603.5] z[943~957]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(596.5f, -700f, 957f), IsLarge = false }, new Mine { Position = new Vector3(603.5f, -700f, 943f), IsLarge = false },
            }},
        },
        // --- Map ID: 1181 Data ---  (10 组 / 31 雷)
        [1181] = new List<MineGroup>
        {
            // 区域 17: x[596.5~603.5] z[776~776]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(596.5f, -684f, 776f), IsLarge = false }, new Mine { Position = new Vector3(596.5f, -684f, 776f), IsLarge = true },
                new Mine { Position = new Vector3(603.5f, -684f, 776f), IsLarge = false }, new Mine { Position = new Vector3(603.5f, -684f, 776f), IsLarge = true },
            }},
        },
        // --- Map ID: 1182 Data ---  (8 组 / 53 雷)
        [1182] = new List<MineGroup>
        {
            // 区域 2: x[530.5~537.5] z[81.5~88.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(530.5f, -700f, 81.5f), IsLarge = false }, new Mine { Position = new Vector3(530.5f, -700f, 88.5f), IsLarge = false },
                new Mine { Position = new Vector3(537.5f, -700f, 81.5f), IsLarge = false }, new Mine { Position = new Vector3(537.5f, -700f, 88.5f), IsLarge = false },
            }},
            // 区域 3: x[582~618] z[107~141]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(582f, -700f, 124f), IsLarge = false }, new Mine { Position = new Vector3(585f, -700f, 117f), IsLarge = false },
                new Mine { Position = new Vector3(585f, -700f, 131f), IsLarge = false }, new Mine { Position = new Vector3(589f, -699.95f, 124f), IsLarge = false },
                new Mine { Position = new Vector3(592f, -699.95f, 116f), IsLarge = false }, new Mine { Position = new Vector3(592f, -699.95f, 132f), IsLarge = false },
                new Mine { Position = new Vector3(593f, -700f, 109f), IsLarge = false }, new Mine { Position = new Vector3(593f, -700f, 139f), IsLarge = false },
                new Mine { Position = new Vector3(600f, -699.956f, 107f), IsLarge = false }, new Mine { Position = new Vector3(600f, -699.95f, 113f), IsLarge = false },
                new Mine { Position = new Vector3(600f, -699.95f, 135f), IsLarge = false }, new Mine { Position = new Vector3(600f, -699.956f, 141f), IsLarge = false },
                new Mine { Position = new Vector3(607f, -700f, 109f), IsLarge = false }, new Mine { Position = new Vector3(607f, -700f, 139f), IsLarge = false },
                new Mine { Position = new Vector3(608f, -699.95f, 116f), IsLarge = false }, new Mine { Position = new Vector3(608f, -699.95f, 132f), IsLarge = false },
                new Mine { Position = new Vector3(611f, -699.95f, 124f), IsLarge = false }, new Mine { Position = new Vector3(615f, -700f, 117f), IsLarge = false },
                new Mine { Position = new Vector3(615f, -700f, 131f), IsLarge = false }, new Mine { Position = new Vector3(618f, -700f, 124f), IsLarge = false },
            }},
            // 区域 4: x[634.5~642.5] z[117~131]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(634.5f, -700f, 117f), IsLarge = true }, new Mine { Position = new Vector3(634.5f, -700f, 124f), IsLarge = true },
                new Mine { Position = new Vector3(634.5f, -700f, 131f), IsLarge = true }, new Mine { Position = new Vector3(642.5f, -700f, 117f), IsLarge = true },
                new Mine { Position = new Vector3(642.5f, -700f, 124f), IsLarge = true }, new Mine { Position = new Vector3(642.5f, -700f, 131f), IsLarge = true },
            }},
            // 区域 5: x[669.5~677.5] z[117~131]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(669.5f, -700f, 117f), IsLarge = true }, new Mine { Position = new Vector3(669.5f, -700f, 124f), IsLarge = true },
                new Mine { Position = new Vector3(669.5f, -700f, 131f), IsLarge = true }, new Mine { Position = new Vector3(677.5f, -700f, 117f), IsLarge = true },
                new Mine { Position = new Vector3(677.5f, -700f, 124f), IsLarge = true }, new Mine { Position = new Vector3(677.5f, -700f, 131f), IsLarge = true },
            }},
            // 区域 6: x[528.343~539.657] z[118.343~129.657]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(528.343f, -700f, 118.343f), IsLarge = true }, new Mine { Position = new Vector3(528.343f, -700f, 129.657f), IsLarge = true },
                new Mine { Position = new Vector3(534f, -700f, 124f), IsLarge = true }, new Mine { Position = new Vector3(539.657f, -700f, 118.343f), IsLarge = true },
                new Mine { Position = new Vector3(539.657f, -700f, 129.657f), IsLarge = true },
            }},
            // 区域 7: x[491.5~498.5] z[120.5~127.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(491.5f, -700f, 120.5f), IsLarge = false }, new Mine { Position = new Vector3(491.5f, -700f, 127.5f), IsLarge = false },
                new Mine { Position = new Vector3(498.5f, -700f, 120.5f), IsLarge = false }, new Mine { Position = new Vector3(498.5f, -700f, 127.5f), IsLarge = false },
            }},
            // 区域 8: x[560~568] z[120.5~127.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(560f, -700f, 120.5f), IsLarge = false }, new Mine { Position = new Vector3(560f, -700f, 127.5f), IsLarge = false },
                new Mine { Position = new Vector3(568f, -700f, 120.5f), IsLarge = false }, new Mine { Position = new Vector3(568f, -700f, 127.5f), IsLarge = false },
            }},
            // 区域 9: x[530.5~537.5] z[159.5~166.5]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(530.5f, -700f, 159.5f), IsLarge = false }, new Mine { Position = new Vector3(530.5f, -700f, 166.5f), IsLarge = false },
                new Mine { Position = new Vector3(537.5f, -700f, 159.5f), IsLarge = false }, new Mine { Position = new Vector3(537.5f, -700f, 166.5f), IsLarge = false },
            }},
        },
        // // --- Map ID: 1183 Data ---  (8 组 / 25 雷)
        // [1183] = new List<MineGroup>
        // {
        //     // 区域 2: x[537.5~537.5] z[81.5~88.5]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(537.5f, -700f, 81.5f), IsLarge = false }, new Mine { Position = new Vector3(537.5f, -700f, 88.5f), IsLarge = false },
        //     }},
        //     // 区域 3: x[582~618] z[109~139]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(582f, -700f, 124f), IsLarge = false }, new Mine { Position = new Vector3(585f, -700f, 131f), IsLarge = false },
        //         new Mine { Position = new Vector3(589f, -699.95f, 124f), IsLarge = false }, new Mine { Position = new Vector3(592f, -699.95f, 132f), IsLarge = false },
        //         new Mine { Position = new Vector3(593f, -700f, 109f), IsLarge = false }, new Mine { Position = new Vector3(593f, -700f, 139f), IsLarge = false },
        //         new Mine { Position = new Vector3(607f, -700f, 109f), IsLarge = false }, new Mine { Position = new Vector3(607f, -700f, 139f), IsLarge = false },
        //         new Mine { Position = new Vector3(608f, -699.95f, 116f), IsLarge = false }, new Mine { Position = new Vector3(608f, -699.95f, 132f), IsLarge = false },
        //         new Mine { Position = new Vector3(618f, -700f, 124f), IsLarge = false },
        //     }},
        //     // 区域 4: x[634.5~642.5] z[117~131]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(634.5f, -700f, 131f), IsLarge = true }, new Mine { Position = new Vector3(642.5f, -700f, 117f), IsLarge = true },
        //         new Mine { Position = new Vector3(642.5f, -700f, 124f), IsLarge = true },
        //     }},
        //     // 区域 5: x[669.5~669.5] z[117~117]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(669.5f, -700f, 117f), IsLarge = true },
        //     }},
        //     // 区域 6: x[528.343~539.657] z[118.343~129.657]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(528.343f, -700f, 129.657f), IsLarge = true }, new Mine { Position = new Vector3(534f, -700f, 124f), IsLarge = true },
        //         new Mine { Position = new Vector3(539.657f, -700f, 118.343f), IsLarge = true },
        //     }},
        //     // 区域 7: x[498.5~498.5] z[127.5~127.5]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(498.5f, -700f, 127.5f), IsLarge = false },
        //     }},
        //     // 区域 8: x[568~568] z[120.5~127.5]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(568f, -700f, 120.5f), IsLarge = false }, new Mine { Position = new Vector3(568f, -700f, 127.5f), IsLarge = false },
        //     }},
        //     // 区域 9: x[530.5~537.5] z[159.5~166.5]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(530.5f, -700f, 159.5f), IsLarge = false }, new Mine { Position = new Vector3(537.5f, -700f, 166.5f), IsLarge = false },
        //     }},
        // },
        // --- Map ID: 1184 Data ---  (5 组 / 8 雷)
        // [1184] = new List<MineGroup>
        // {
        //     // 区域 2: x[530.5~530.5] z[88.5~88.5]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(530.5f, -700f, 88.5f), IsLarge = false },
        //     }},
        //     // 区域 3: x[618~618] z[124~124]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(618f, -700f, 124f), IsLarge = false },
        //     }},
        //     // 区域 4: x[634.5~642.5] z[117~124]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(634.5f, -700f, 117f), IsLarge = true }, new Mine { Position = new Vector3(642.5f, -700f, 124f), IsLarge = true },
        //     }},
        //     // 区域 7: x[491.5~498.5] z[120.5~120.5]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(491.5f, -700f, 120.5f), IsLarge = false }, new Mine { Position = new Vector3(498.5f, -700f, 120.5f), IsLarge = false },
        //     }},
        //     // 区域 9: x[537.5~537.5] z[159.5~166.5]
        //     new MineGroup { Mines = {
        //         new Mine { Position = new Vector3(537.5f, -700f, 159.5f), IsLarge = false }, new Mine { Position = new Vector3(537.5f, -700f, 166.5f), IsLarge = false },
        //     }},
        // },
        // --- Map ID: 1189 Data ---  (2 组 / 15 雷)
        [1189] = new List<MineGroup>
        {
            // 区域 0: x[-9~10] z[-433~-421]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-9f, -707.95f, -430f), IsLarge = false }, new Mine { Position = new Vector3(-4f, -707.95f, -430f), IsLarge = false },
                new Mine { Position = new Vector3(0f, -707.95f, -433f), IsLarge = false }, new Mine { Position = new Vector3(0f, -707.95f, -427f), IsLarge = false },
                new Mine { Position = new Vector3(0f, -707.95f, -421f), IsLarge = false }, new Mine { Position = new Vector3(4f, -707.95f, -430f), IsLarge = false },
                new Mine { Position = new Vector3(10f, -708f, -430f), IsLarge = false },
            }},
            // 区域 1: x[27~46] z[-403~-385]
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(27f, -715.95f, -394f), IsLarge = false }, new Mine { Position = new Vector3(32f, -715.95f, -394f), IsLarge = false },
                new Mine { Position = new Vector3(36f, -715.95f, -403f), IsLarge = false }, new Mine { Position = new Vector3(36f, -715.95f, -397f), IsLarge = false },
                new Mine { Position = new Vector3(36f, -715.95f, -391f), IsLarge = false }, new Mine { Position = new Vector3(36f, -715.95f, -385f), IsLarge = false },
                new Mine { Position = new Vector3(40f, -715.95f, -394f), IsLarge = false }, new Mine { Position = new Vector3(46f, -716f, -394f), IsLarge = false },
            }},
        },
    };
}

#endregion
