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

using System.Diagnostics;
using System.Runtime.Intrinsics.Arm;

namespace Codaaaaaa.M9S;

[ScriptType(
    guid: "2c8b7d4a-6e91-4f3c-a5d2-9b7e1f6c8a03",
    name: "阿卡狄亚零式登天斗技场M10S 指路",
    territorys: [1323],
    version: "0.0.1.0",
    author: "Codaaaaaa",
    note: "大部分的机制都做了指路，使用之前请务必调整可达鸭内位置和选择打法。由于版本初拿不到arr用来测试，有较大概率会被电...如果电了可以在频道反馈。感谢灵视佬提供的arr\n目前支持\nP2 第一轮打法\n * 水波\n * 镜像水波\n\nP2 第二三轮打法\n * 近战优化\n * 美野\n\n进水牢方式\n * 坦克\n * 近战\n * 治疗\n\n水牢打法\n * 无脑\n * MMW\n")]
public class M10S
{
    #region 用户设置
    // [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;

    // 水波或者镜像水波
    [ UserSetting("P2第一轮打法")] public P2第一轮打法 p2第一轮打法 { get; set; } = P2第一轮打法.水波;
    // 近战优化或者美式野
    [ UserSetting("P2第二三轮打法")] public P2第二三轮打法 p2第二三轮打法 { get; set; } = P2第二三轮打法.近战优化;
    // 进水牢方式
    [ UserSetting("进水牢方式")] public 进水牢 进水牢方式 { get; set; } = 进水牢.近战进水牢;
    [ UserSetting("水牢打法")] public 水牢打法 水牢打法选择 { get; set; } = 水牢打法.无脑;
    [ UserSetting("P5换边指路")] public bool p5换边指路 { get; set; } = true;
    #endregion
    public enum P2第一轮打法
    {
        水波,
        镜像水波
    }

    public enum P2第二三轮打法
    {
        近战优化,
        美式野
    }
    public enum 进水牢
    {
        坦克进水牢,
        近战进水牢,
        治疗进水牢
    }

    public enum 水牢打法
    {
        无脑,
        MMW
    }

    public enum P5火水蛇
    {
        无,
        火,
        水
    }
    private static readonly Vector3 Center = new(100f, 0f, 100f);

    private uint _phase = 1;
    private uint 炽红 = 19287u;
    private uint 深蓝 = 19288u;

    // =========================
    // 空中旋火：射线记录 & 点位动态下移
    // =========================
    private bool _airSpinRayActive = false;
    private Vector3 _airSpinRayBossPos;
    private Vector3 _airSpinRayTargetPos;
    private Vector2 _airSpinRayDirXZ; // (dx, dz)
    private readonly Dictionary<int, Vector3> _airSpinP1Override = new();
    private bool _bossBasisReady = false;
    private Vector3 _bossBasisPos;     // 火冲开始时boss位置
    private Vector2 _bossFwdXZ;        // 火冲开始时boss forward(朝场内)
    private Vector2 _bossRightXZ;      // 火冲开始时boss right
    
    private static readonly Dictionary<int, Vector3> AirSpinRowZ103_57 = new()
    {
        { 4, new Vector3(118.01f, 0f, 103.57f) },
        { 5, new Vector3(111.8f,  0f, 103.57f) },
        { 1, new Vector3(105.59f, 0f, 103.57f) },
        { 3, new Vector3(99.38f,  0f, 103.57f) },
        { 7, new Vector3(93.17f,  0f, 103.57f) },
    };

    // =========================
    // 分摊分散
    private int _分摊分散 = 0;
    private Vector3 _浪花位置 = Vector3.Zero;
    public void Init(ScriptAccessory sa)
    {
        ResetAll();
        sa.Method.RemoveDraw(".*");
    }

    private void ResetAll()
    {
        _phase = 1;
        _airSpinRayActive = false;
        _airSpinP1Override.Clear();
        _分摊分散 = 0;
        _P2左右 = 1;
        _P2火圈次数 = 0;
        _P3水牢次数 = 0;
        _P3水牢深蓝初始位置 = 0;
        _浪花位置 = Vector3.Zero;
        _P5火水蛇 = P5火水蛇.无;
        _P5狂浪次数 = 0;
    }

    // P2左右
    private int _P2左右 = 1; // 1=左 2=右
    private int _P2火圈次数 = 0;

    // P3
    private int _P3水牢次数 = 0;
    private int _P3水牢深蓝初始位置 = 0; // 1 或者 4

    // P5
    private P5火水蛇 _P5火水蛇 = P5火水蛇.无;
    private int _P5狂浪次数 = 0; // 0 奶 1 近 2 远

    // Debug
    [ScriptMethod(name: "clear draw", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASCLEAR"], userControl: false)]
    public void ClearDraw(Event evt, ScriptAccessory sa) => sa.Method.RemoveDraw(".*");

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

    [ScriptMethod(name: "Show Debug", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:debug"], userControl: false)]
    public void ShowDebug(Event evt, ScriptAccessory sa) => sa.Method.SendChat($"/e Current Param: {_P2火圈次数}, 左右: {_P2左右}");

    [ScriptMethod(
    name: "分摊死刑",
    eventType: EventTypeEnum.StartCasting,
    eventCondition: ["ActionId:46518"])]
    public void 分摊死刑(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        var color = myIdx switch
        {
            0 => sa.Data.DefaultSafeColor,
            1 => sa.Data.DefaultSafeColor,
            _ => sa.Data.DefaultDangerColor
        };
        var targetId = evt.TargetId();

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"死刑圈-{targetId:X}";
        dp.Owner = targetId;
        dp.Color = color;
        dp.DestoryAt = 5500;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(6);
        
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 开场火冲
    [ScriptMethod(
        name: "开场火冲麻将",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(3004|3005|3006|3451)$"]
    )]
    public void 开场火冲麻将(Event evt, ScriptAccessory sa)
    {
        var targetId = evt.TargetId();
        var myId = sa.Data.Me;

        if (!RayHelpers.TryGetBossBasis(sa, 炽红, Center, out var bossPos, out var fwd, out var right))
        {
            sa.Method.SendChat("/e 找不到Boss，无法缓存火冲基底");
            return;
        }

        _bossBasisReady = true;
        _bossBasisPos = new Vector3(bossPos.X, 0f, bossPos.Z);
        _bossFwdXZ = fwd;
        _bossRightXZ = right;

        // sa.Method.SendChat($"/e 缓存Boss基底 pos=({_bossBasisPos.X:F1},{_bossBasisPos.Z:F1}) fwd=({_bossFwdXZ.X:F2},{_bossFwdXZ.Y:F2}) right=({_bossRightXZ.X:F2},{_bossRightXZ.Y:F2})");
        // 只画自己身上的
        if (targetId == 0 || myId == 0 || targetId != myId)
            return;

        // 取 boss 作为锚点 + 计算朝向
        // 后面全部用缓存
        var bossPos0 = _bossBasisPos;
        var fwd0 = _bossFwdXZ;
        var right0 = _bossRightXZ;

        var wpos = evt["StatusID"] switch
        {
            "3004" => RayHelpers.BossRelativePoint(bossPos0, fwd0, right0,  19f, 1f),
            "3005" => RayHelpers.BossRelativePoint(bossPos0, fwd0, right0,  -4f, 1f),
            "3006" => RayHelpers.BossRelativePoint(bossPos0, fwd0, right0,  19f, 6f),
            "3451" => RayHelpers.BossRelativePoint(bossPos0, fwd0, right0,  -4f, 6f),
            _ => Center
        };


        sa.Method.SendChat($"/e 动态麻将点 sid={evt["StatusID"]} boss=({bossPos0.X:F1},{bossPos0.Z:F1}) => wpos=({wpos.X:F1},{wpos.Z:F1})");

        var dp = sa.WaypointDp(wpos, 20000, 0, "开场火冲麻将指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }


    [ScriptMethod(
        name: "开场火冲麻将删除画图空中旋火射线记录",
        eventType: EventTypeEnum.StatusRemove,
        eventCondition: ["StatusID:regex:^(3004|3005|3006|3451)$"],
        userControl: false
    )]
    public void 开场火冲麻将删除画图空中旋火射线记录(Event evt, ScriptAccessory sa)
    {
        var targetId = evt.TargetId();
        var myId = sa.Data.Me;
        var statusId = evt["StatusID"];

        // 只删自己身上的
        if (statusId != "3451")
        {
            if (targetId == 0 || myId == 0 || targetId != myId)
                return;
        }

        sa.Method.RemoveDraw("开场火冲麻将指路");

        // 记录
        if (statusId != "3451")
            return;
        sa.Method.SendChat("/e 记录");
        if (targetId == 0) return;

        var targetObj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == targetId);
        if (targetObj == null) {
            // sa.Method.SendChat($"/e 目标对象未找到，TargetId={targetId:X}");
            return;
        }

        var bossObj = sa.Data.Objects.FirstOrDefault(o => o.DataId == 炽红);
        if (bossObj == null) {
            // sa.Method.SendChat($"/e Boss对象未找到，DataId={炽红:X}");
            return;
        }

        var bossPos = bossObj.Position;
        var targetPos = targetObj.Position;

        // 方向（XZ）
        var d = new Vector2(targetPos.X - bossPos.X, targetPos.Z - bossPos.Z);
        // sa.Method.SendChat($"/e 方向d=({d.X},{d.Y})");
        if (d.LengthSquared() < 0.0001f) return;

        _airSpinRayActive = true;
        _airSpinRayBossPos = bossPos;
        _airSpinRayTargetPos = targetPos;
        _airSpinRayDirXZ = d;

        if (!_bossBasisReady)
        {
            // 说明火冲没触发过或者缓存丢了，没法按你的逻辑做
            return;
        }
        var bpos = _bossBasisPos;
        var fwd  = _bossFwdXZ;
        var right= _bossRightXZ;

        var baseMap = new Dictionary<int, Vector3>
        {
            // Row points you described
            { 4, RayHelpers.BossRelativePoint(bpos, fwd, right,  19f, 8f) },
            { 5, RayHelpers.BossRelativePoint(bpos, fwd, right,   9.5f, 8f) },
            { 1, RayHelpers.BossRelativePoint(bpos, fwd, right,   0f,  8f) },
            { 3, RayHelpers.BossRelativePoint(bpos, fwd, right,  -9.5f, 8f) },
            { 7, RayHelpers.BossRelativePoint(bpos, fwd, right, -16f,  8f) },
        };

        _airSpinP1Override.Clear();

        // Move along boss forward until not covered
        foreach (var kv in baseMap)
        {
            var idx = kv.Key;
            var p0 = kv.Value;

            var pNew = RayHelpers.AdjustPointAlongDirUntilNotCoveredXZ(
                p0,
                _airSpinRayBossPos,
                _airSpinRayDirXZ,
                moveDirXZ: fwd,      // IMPORTANT: move along boss forward towards center
                halfWidth: 5f,
                epsilon: 0.05f,
                maxMove: 40f
            );

            pNew = RayHelpers.ClampXZ(pNew, 80f, 120f);
            _airSpinP1Override[idx] = pNew;
        }

        var baseRight19 = RayHelpers.BossRelativePoint(bpos, fwd, right, 19f, 8f);
        // 先拿 right=19, forward=0 的最近不覆盖点当锚点
        var anchor = RayHelpers.AdjustPointAlongDirUntilNotCoveredXZ(
            baseRight19, _airSpinRayBossPos, _airSpinRayDirXZ,
            moveDirXZ: fwd,
            halfWidth: 5f, epsilon: 0.05f, maxMove: 40f
        );

        static Vector3 AddForward(Vector3 p, Vector2 fwd, float dist)
            => new Vector3(p.X + fwd.X * dist, 0f, p.Z + fwd.Y * dist);

        // 再从 anchor 加 9/18/27
        (int idx, float dist)[] col = { (0, 9f), (2, 18f), (6, 27f) };

        foreach (var (idx, dist) in col)
        {
            var p = AddForward(anchor, fwd, dist);

            var pNew = RayHelpers.AdjustPointAlongDirUntilNotCoveredXZ(
                p, _airSpinRayBossPos, _airSpinRayDirXZ,
                moveDirXZ: fwd,
                halfWidth: 5f, epsilon: 0.05f, maxMove: 40f
            );


            pNew = RayHelpers.ClampXZ(pNew, 80f, 120f);
            _airSpinP1Override[idx] = pNew;
        }

        sa.Method.SendChat($"/e Ray boss={bossObj.GameObjectId:X} target={targetObj.GameObjectId:X} dir=({_airSpinRayDirXZ.X:F2},{_airSpinRayDirXZ.Y:F2}), adjusted {_airSpinP1Override[7]}.");
    }
    // 转场换P
    [ScriptMethod(
        name: "转场换P2",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46553"],
        userControl: false
    )]
    public void 转场换P2(Event evt, ScriptAccessory sa)
    {
        _phase = 2;
        sa.Method.SendChat("/e 转场换P，当前阶段 P2");
    }
    // 转场换P
    [ScriptMethod(
        name: "转场换P3",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46563"],
        userControl: false
    )]
    public void 转场换P3(Event evt, ScriptAccessory sa)
    {
        _phase = 3;
        sa.Method.SendChat("/e 转场换P，当前阶段 P3");
    }

    // 空中旋火
    [ScriptMethod(
        name: "空中旋火指路P1",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46529"]
    )]
    public void 空中旋火指路P1(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        var targetId = evt.TargetId();
        var myId = sa.Data.Me;
        if (_phase == 1)
        {
            // P1
            // 只画自己身上的
            if (targetId == 0 || myId == 0 || targetId != myId)
                return;

            Vector3 wpos;
            if (_airSpinRayActive && _airSpinP1Override.TryGetValue(myIdx, out var ov))
            {
                wpos = ov;
            }
            else
            {
                wpos = myIdx switch
                {
                    _ => new Vector3(0f, 0f, 0f)
                };
            }

            var dp = sa.WaypointDp(wpos, 6000, 0, "空中旋火指路P1");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        
        if (_phase == 2 && _P2火圈次数 <= 3)
        {   
            _P2火圈次数++;
            // sa.Method.SendChat($"/e P2火圈次数 {_P2火圈次数}");

            const uint BuffId = 4974;
            var spots = new Vector3[4];
            if (p2第二三轮打法 == P2第二三轮打法.近战优化)
            {
                // 四个落点（y 固定 0）
                // 如果P2左右==1 左边点，否则右边点
                if (_P2左右 == 1)
                {
                    spots = new[]
                    {
                        new Vector3(118.91f, 0.00f,  87.64f),
                        new Vector3(119.09f, 0.00f,  95.83f),
                        new Vector3(118.96f, 0.00f, 103.66f),
                        new Vector3(119.39f, 0.00f, 110.78f),
                    };
                }
                else
                {
                    spots = new[]
                    {
                        new Vector3(118.91f, 0.00f,  87.64f),
                        new Vector3(119.09f, 0.00f,  95.83f),
                        new Vector3(118.96f, 0.00f, 103.66f),
                        new Vector3(119.39f, 0.00f, 110.78f),
                    };
                }
            }
            else
            {
                // 美
                if (_P2左右 == 1)
                {
                    spots = new[]
                    {
                        new Vector3(118.09f, 0.00f, 96.88f), // 6 7
                        new Vector3(112.2f, 0.00f,  93.43f), // 4 5
                        new Vector3(112.2f, 0.00f,  103.74f), // 0 1
                        new Vector3(117.98f, 0.00f, 102.98f), // 2 3
                    };
                }
                else
                {
                    spots = new[]
                    {
                        new Vector3(81.72f, 0.00f,  96.88f), // 6 7
                        new Vector3(87.8f, 0.00f,  93.43f), // 4 5
                        new Vector3(87.8f, 0.00f,  103.74f), // 0 1
                        new Vector3(81.72f, 0.00f,  102.98f), // 2 3
                    };
                }
            }
            // 优先级：6 7 4 5 0 1 2 3
            var prio = new[] { 6, 7, 4, 5, 0, 1, 2, 3 };

            // 1) 找到所有带 4974 的人（用 GetParty + HasStatus）
            var buffed = sa.GetParty()
                .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                .ToList();

            if (buffed.Count == 0) return;

            // 2) 按 prio 排序（按 PartyList 的 index 排）
            var ordered = buffed
                .Select(p => new { Player = p, Idx = sa.Data.PartyList.IndexOf(p.EntityId) })
                .Where(x => x.Idx >= 0) // 找不到就丢掉
                .OrderBy(x => Array.IndexOf(prio, x.Idx)) // prio 里越靠前越优先
                .ToList();

            // prio 里没有的（Array.IndexOf 返回 -1）会排到最前面，所以要过滤掉
            ordered = ordered.Where(x => Array.IndexOf(prio, x.Idx) >= 0).ToList();

            if (ordered.Count == 0) return;

            // 3) 只给自己画：我必须在前4个里
            var mySlot = -1;
            for (int i = 0; i < Math.Min(4, ordered.Count); i++)
            {
                if (ordered[i].Player.EntityId == sa.Data.Me)
                {
                    mySlot = i;
                    break;
                }
            }
            if (mySlot < 0) return;

            var wpos = spots[mySlot];
            var dp = sa.WaypointDp(wpos, 6000, 0, "空中旋火指路P2");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            
        }
        else if (_phase == 2 && _P2火圈次数 <= 7)
        {
            _P2火圈次数++;
            // sa.Method.SendChat($"/e P2火圈次数 {_P2火圈次数}");
            
        }

        if (_phase == 3)
        {
            // 其实是P4
            Vector3 wpos = myIdx switch
            {
                0 => new Vector3(81.27f, 0.00f, 87.70f),
                1 => new Vector3(118.99f, 0.00f, 87.11f),
                2 => new Vector3(80.93f, 0.00f, 118.95f),
                3 => new Vector3(118.66f, 0.00f, 118.24f),
                4 => new Vector3(80.90f, 0.00f, 110.95f),
                5 => new Vector3(119.21f, 0.00f, 109.14f),
                6 => new Vector3(81.11f, 0.00f, 81.26f),
                7 => new Vector3(119.21f, 0.00f, 80.52f),  
                _ => Center
            };
            var dp = sa.WaypointDp(wpos, 6000, 0, "空中旋火指路P3");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }


        
    }

    // 破势乘浪
    [ScriptMethod(
        name: "破势乘浪",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46542"]
    )]
    public async void 破势乘浪(Event evt, ScriptAccessory sa)
    {
        // await Task.Delay(3000);
        // 获得危险区位置
        var effectPos = evt.EffectPosition();
        var sourceRotation = evt.SourceRotation;
        // 画危险区
        var dpDanger = sa.Data.GetDefaultDrawProperties();
        dpDanger.Name = "破势乘浪-危险区";
        dpDanger.Position = effectPos;
        dpDanger.Color = sa.Data.DefaultDangerColor;
        dpDanger.DestoryAt = 8000;
        dpDanger.Scale = new Vector2(15f, 50f);
        dpDanger.Rotation = sourceRotation;
        dpDanger.FixRotation = true;
        dpDanger.ScaleMode = ScaleMode.None;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpDanger);

        // 获得 DataId 19290的对象的位置和朝向
        var obj = sa.Data.Objects.FirstOrDefault(o => o.DataId == 19290);
        if (obj == null) return;

        var myIdx = sa.MyIndex();

        // 以 obj 朝向构建局部坐标（y 固定 0）
        var objPos = new Vector3(obj.Position.X, 0f, obj.Position.Z);
        var rot = obj.Rotation;

        // forward: 朝向；left/right: 垂直于朝向
        var forward = new Vector3((float)Math.Sin(rot), 0f, (float)Math.Cos(rot));
        if (forward.LengthSquared() < 1e-6f) forward = new Vector3(0f, 0f, 1f);
        forward = Vector3.Normalize(forward);

        var right = new Vector3(-forward.Z, 0f, forward.X);
        right = Vector3.Normalize(right);
        var left = -right;
        var back = -forward;

        Vector3 wpos;

        if (_分摊分散 == 1)
        {
            sa.Method.EdgeTTS("分摊");
            // 双奶画分摊圈
            if (_phase == 1 || _phase == 3)
            {
                for (int i = 2; i < 4; i++)
                {
                    var oid = sa.Data.PartyList[i];
                    if (oid == 0) continue;

                    var dpCircle = sa.Data.GetDefaultDrawProperties();
                    dpCircle.Name = $"分摊圈_{oid:X}";
                    dpCircle.Owner = oid;
                    dpCircle.Color = sa.Data.DefaultSafeColor;
                    dpCircle.DestoryAt = 10000;
                    dpCircle.Scale = new Vector2(5f);
                    dpCircle.ScaleMode = ScaleMode.ByTime;

                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                }
            }
            else if (_phase == 2)
            {
                // P2 分摊只画有BUFF的治疗
                var buffId = 4975u;
                for (int i = 2; i < 4; i++)
                {
                    var oid = sa.Data.PartyList[i];
                    var p = sa.GetParty().FirstOrDefault(x => x != null && x.EntityId == oid);
                    if (p == null || oid == 0 || !p.HasStatus(buffId))
                        continue;

                    var dpCircle = sa.Data.GetDefaultDrawProperties();
                    dpCircle.Name = $"分摊圈_{oid:X}";
                    dpCircle.Owner = oid;
                    dpCircle.Color = sa.Data.DefaultSafeColor;
                    dpCircle.DestoryAt = 10000;
                    dpCircle.Scale = new Vector2(5f);
                    dpCircle.ScaleMode = ScaleMode.ByTime;

                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                }
            }
           
            var dangerCenter = new Vector3(effectPos.X, 0f, effectPos.Z);
            float dangerYaw = sourceRotation;
            float dangerW = 15f;
            float dangerL = 50f;

            // 分摊用的“车道距离”：左(-12) / 中(0) / 右(+12)
            float lane = 15f;

            // 分摊只需要判断“后12”这一排会不会被危险区盖住
            float[] fbSamples = { -15f };  // forward * (-12) 等价于 back * 12

            // 选出给偶数组(0/2/4/6)与奇数组(1/3/5/7)的车道
            RayHelpers.PickSafeLanesByDangerRect(
                objPos,
                right,       // obj 的 right
                forward,     // obj 的 forward
                dangerCenter,
                dangerYaw,
                dangerW,
                dangerL,
                lane,
                fbSamples,
                out var evenLane,
                out var oddLane
            );

            bool isOdd = (myIdx % 2) == 1;
            float laneOffset = isOdd ? oddLane : evenLane;
            if (_phase == 1)
            {
                // 分摊固定：后15
                float fbDist = -15f;

                wpos = objPos + right * laneOffset + forward * fbDist;
                wpos = new Vector3(wpos.X, 0f, wpos.Z);

                var dp = sa.WaypointDp(wpos, 7000, 0, "破势乘浪-分摊指路");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }

            // sa.Method.SendChat($"/e 分摊 idx={myIdx} evenLane={evenLane:F2} oddLane={oddLane:F2}");
        }
        else
        {
            sa.Method.EdgeTTS("分散");
            // 所有人画 5m 的危险圈
            if (_phase == 1 || _phase == 3)
            {
                 foreach (var oid in sa.Data.PartyList) // 这里的 oid 就是 uint
                {
                    if (oid == 0) continue;

                    var dpCircle = sa.Data.GetDefaultDrawProperties();
                    dpCircle.Name = $"分散圈_{oid:X}";
                    dpCircle.Owner = oid;
                    dpCircle.Color = sa.Data.DefaultDangerColor;
                    dpCircle.DestoryAt = 10000;
                    dpCircle.Scale = new Vector2(5f);
                    dpCircle.ScaleMode = ScaleMode.ByTime;

                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                }
            }
            else if (_phase == 2)
            {
                // P2 分散只画有BUFF的人
                var buffId = 4975u;
                foreach (var p in sa.GetParty())
                {
                    if (p == null || p.EntityId == 0 || !p.HasStatus(buffId))
                        continue;

                    var dpCircle = sa.Data.GetDefaultDrawProperties();
                    dpCircle.Name = $"分散圈_{p.EntityId:X}";
                    dpCircle.Owner = p.EntityId;
                    dpCircle.Color = sa.Data.DefaultDangerColor;
                    dpCircle.DestoryAt = 10000;
                    dpCircle.Scale = new Vector2(5f);
                    dpCircle.ScaleMode = ScaleMode.ByTime;

                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                }
            }
           

            // 分散：按 idx 映射
            // 3/5/1/7: 左15，前后：+8 / -2 / -12 / -18
            // 2/4/0/6: 右15，前后：+8 / -2 / -12 / -18
            // 这里把 “前” 记为 +forward，“后” 记为 -forward
            float lane = 15f;
            float[] fbSamples = { +8f, -2f, -12f, -18f };

            var dangerCenter = new Vector3(effectPos.X, 0f, effectPos.Z);
            float dangerYaw = sourceRotation;
            float dangerW = 15f;
            float dangerL = 50f;

            RayHelpers.PickSafeLanesByDangerRect(
                objPos,
                right,        // 注意：这里用 obj 的 right（你上面算出来的 Vector3 right）
                forward,      // 用 obj 的 forward
                dangerCenter,
                dangerYaw,
                dangerW,
                dangerL,
                lane,
                fbSamples,
                out var evenLane,
                out var oddLane
            );
            // ====== 每个人前后位置还是按 idx 映射 ======
            float fbDist;
            if (myIdx == 3) fbDist = +8f;
            else if (myIdx == 5) fbDist = -2f;
            else if (myIdx == 1) fbDist = -12f;
            else if (myIdx == 7) fbDist = -18f;
            else if (myIdx == 2) fbDist = +8f;
            else if (myIdx == 4) fbDist = -2f;
            else if (myIdx == 0) fbDist = -12f;
            else if (myIdx == 6) fbDist = -18f;
            else fbDist = -12f;

            bool isOdd = (myIdx % 2) == 1;
            float laneOffset = isOdd ? oddLane : evenLane;

            if (_phase == 1)
            {
                wpos = objPos + right * laneOffset + forward * fbDist;
                wpos = new Vector3(wpos.X, 0f, wpos.Z);

                var dp = sa.WaypointDp(wpos, 7000, 0, "破势乘浪-分散指路");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            
            // sa.Method.SendChat($"/e 分散 idx={myIdx} evenLane={evenLane:F2} oddLane={oddLane:F2}");
        }
    }

    // 水基佬分摊分散记录
    [ScriptMethod(
        name: "水基佬分摊分散记录",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:2056"],
        userControl: false
    )
    ]
    public void 水基佬分摊分散记录(Event evt, ScriptAccessory sa)
    {
        if (!int.TryParse(evt["Param"], out var param)) return;
        _分摊分散 = param switch
        {
            1005 => 1, // 分摊
            1006 => 2,  // 分散
            1007 => 1, // 分摊（P3）
            1008 => 2, // 分散（P3）
        };
        sa.Method.EdgeTTS(_分摊分散 == 1 ? "待会儿分摊" : "待会儿分散");
    }

    // 击退
    [ScriptMethod(
        name: "惊涛击退画图",
        eventType: EventTypeEnum.SetObjPos,
        eventCondition: ["SourceDataId:19290"]
    )]
    public async void 惊涛击退(Event evt, ScriptAccessory sa)
    {
        var duration = 19000;
        var rotation = evt.SourceRotation;

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "击退方向";
        dp.Owner = sa.Data.Me;
        dp.Color = new Vector4 (1f, 1f, 0, 1f); // yellow
        dp.DestoryAt = duration;
        dp.Scale = new Vector2(1f, 10f);
        dp.Rotation = rotation;
        dp.FixRotation = true;
        dp.ScaleMode = ScaleMode.None;

        if (_phase == 1)
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        
        if (_phase == 2)
        {
            await Task.Delay(30000);
            dp.DestoryAt = 10000;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        } 
        

    }

    // 火基佬四连跳画图
    [ScriptMethod(
        name: "火基佬四连跳画图",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46532"]
    )]
    public void 火基佬四连跳(Event evt, ScriptAccessory sa)
    {
        const uint FireBuffId = 4974;   // 火buff
        const uint Duration   = 11000;
        // 只有自己有buff的才画
        var buffed = sa.GetParty()
                .Where(p => p != null && p.EntityId != 0 && p.HasStatus(FireBuffId))
                .ToList();
        if (!sa.GetParty().Any(p => p != null && p.EntityId == sa.Data.Me && p.HasStatus(FireBuffId)))
            return;
        
        // 3) 美式野：火buff组指路（你给的固定坐标）
        if (_phase >= 2 && p2第二三轮打法 == P2第二三轮打法.美式野)
        {
            var myIdx = sa.MyIndex();
            if (myIdx < 0 || myIdx > 7) return;

            Vector3 wpos = myIdx switch
            {
                0 or 1 => new Vector3(88.26f, 0f, 88.20f),
                2 or 3 => new Vector3(112.14f, 0f, 88.24f),
                4 or 5 => new Vector3(112.22f, 0f, 111.80f),
                _      => new Vector3(88.18f, 0f, 111.87f),
            };

            var dpWp = sa.WaypointDp(wpos, Duration, 0, "火基佬四连跳指路");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
        }
    }


    // 清除火基佬四连跳画图
    [ScriptMethod(
        name: "火基佬四连跳二连指路",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(47390|47391|47392|47393)$"],
        userControl: false
    )]
    public void 火基佬四连跳二连指路(Event evt, ScriptAccessory sa)
    {
        var targetId = evt.TargetId();
        if (targetId == 0) return;

        // 清除指路/连线/圈
        if (targetId == sa.Data.Me)
        {
            sa.Method.RemoveDraw("火基佬四连跳指路");
            // sa.Method.SendChat("/e 火基佬四连跳指路2");

            // var dp  = sa.WaypointDp(Center, 4000, 0, "火基佬四连跳指路2");
            // sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            // 指勾八，自己去场中吧
        }
    }

    // 深海冲击
    [ScriptMethod(
        name: "深海冲击",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46519"]
    )]
    public void 深海冲击(Event evt, ScriptAccessory sa)
    {
        sa.Method.EdgeTTS("坦克远离人群，注意死刑");
        sa.Method.TextInfo("T远离人群，注意死刑", 4500, true);
    }



    // ===== 水波P2指路补完：角色/角度工具 =====
    private enum WaterRole
    {
        Tank,
        Healer,
        Melee,
        Ranged
    }

    private static WaterRole GetRoleByPartyIndex(int idx)
    {
        // 默认按你常用的小队排序：
        // 0/1 = T, 2/3 = H, 4/5 = 近战, 6/7 = 远程
        if (idx == 0 || idx == 1) return WaterRole.Tank;
        if (idx == 2 || idx == 3) return WaterRole.Healer;
        if (idx == 4 || idx == 5) return WaterRole.Melee;
        return WaterRole.Ranged;
    }

    private static Vector3 DirFromYaw(float yaw)
    {
        // 与你脚本其它地方保持一致：forward = (sin(yaw), cos(yaw))
        return new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
    }

    private static float YawFromForward(Vector3 fwd)
    {
        // 与你 YawFromDir 同一套：yaw = atan2(x, z)
        var d = new Vector2(fwd.X, fwd.Z);
        if (d.LengthSquared() < 1e-6f) return 0f;
        d = Vector2.Normalize(d);
        return MathF.Atan2(d.X, d.Y);
    }

    private static float DegToRad(float deg) => deg * (MathF.PI / 180f);

    // 约定：0=南，90=东，180=北，270=西
    // 北偏东 = 180 - t；南偏东 = 0 + t
    private static float YawDeg_N_By_E(float t) => 180f - t;
    private static float YawDeg_S_By_E(float t) => 0f + t;

    // 北偏西 = 180 + t；南偏西 = 360 - t
    private static float YawDeg_N_By_W(float t) => 180f + t;
    private static float YawDeg_S_By_W(float t) => 360f - t;

    private static float Normalize180(float deg)
    {
        deg %= 360f;
        if (deg > 180f) deg -= 360f;
        if (deg < -180f) deg += 360f;
        return deg;
    }
    private float GetWaterOffsetRad(P2第一轮打法 strat, bool zHigh, WaterRole role, int 左右)
    {
        float yawDeg = 90f; // 默认给个值
        float baseDeg = (左右 == 1) ? 90f : 270f; // 左=东基准；右=西基准

        if (左右 == 1)
        {
            // ===== 你原来的“左(东基准)”逻辑不动 =====
            if (strat == P2第一轮打法.水波)
            {
                if (zHigh)
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_S_By_E(5f),
                        WaterRole.Melee  => YawDeg_N_By_E(5f),
                        WaterRole.Ranged => YawDeg_S_By_E(45f),
                        WaterRole.Healer => YawDeg_S_By_E(30f),
                        _ => 90f
                    };
                }
                else
                {
                    yawDeg = role switch
                    {
                        WaterRole.Melee  => YawDeg_N_By_E(5f),
                        WaterRole.Tank   => YawDeg_S_By_E(5f),
                        WaterRole.Healer => YawDeg_N_By_E(45f),
                        WaterRole.Ranged => YawDeg_N_By_E(30f),
                        _ => 90f
                    };
                }
            }
            else // 镜像水波
            {
                if (zHigh)
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_N_By_E(5f),
                        WaterRole.Melee  => YawDeg_S_By_E(5f),
                        WaterRole.Ranged => YawDeg_S_By_E(35f),
                        WaterRole.Healer => YawDeg_S_By_E(45f),
                        _ => 90f
                    };
                }
                else
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_S_By_E(5f),
                        WaterRole.Melee  => YawDeg_N_By_E(5f),
                        WaterRole.Ranged => YawDeg_N_By_E(30f),
                        WaterRole.Healer => YawDeg_N_By_E(45f),
                        _ => 90f
                    };
                }
            }
        }
        else if (左右 == 2)
        {
            // ===== 右(西基准)：把 E 换成 W，其它结构保持一致 =====
            if (strat == P2第一轮打法.水波)
            {
                if (zHigh)
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_S_By_W(5f),
                        WaterRole.Melee  => YawDeg_N_By_W(5f),
                        WaterRole.Ranged => YawDeg_S_By_W(45f),
                        WaterRole.Healer => YawDeg_S_By_W(30f),
                        _ => 270f
                    };
                }
                else
                {
                    yawDeg = role switch
                    {
                        WaterRole.Melee  => YawDeg_N_By_W(5f),
                        WaterRole.Tank   => YawDeg_S_By_W(5f),
                        WaterRole.Healer => YawDeg_N_By_W(45f),
                        WaterRole.Ranged => YawDeg_N_By_W(30f),
                        _ => 270f
                    };
                }
            }
            else // 镜像水波
            {
                if (zHigh)
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_N_By_W(5f),
                        WaterRole.Melee  => YawDeg_S_By_W(5f),
                        WaterRole.Ranged => YawDeg_S_By_W(35f),
                        WaterRole.Healer => YawDeg_S_By_W(45f),
                        _ => 270f
                    };
                }
                else
                {
                    yawDeg = role switch
                    {
                        WaterRole.Tank   => YawDeg_S_By_W(5f),
                        WaterRole.Melee  => YawDeg_N_By_W(5f),
                        WaterRole.Ranged => YawDeg_N_By_W(30f),
                        WaterRole.Healer => YawDeg_N_By_W(45f),
                        _ => 270f
                    };
                }
            }
        }

        // 最终偏移角：左用东(90)，右用西(270)
        float offDeg = Normalize180(yawDeg - baseDeg);
        return DegToRad(offDeg);
    }

    // 水基佬水波
    [ScriptMethod(
        name: "水波画图",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(46557|46560)$"]
    )]
    public async void 水波画图(Event evt, ScriptAccessory sa)
    {
        var actionId = evt["ActionId"];

        // 给队里所有人画扇形 from boss to oid
        if (!int.TryParse(evt["DurationMilliseconds"], out var duration)) return;
        if (_phase == 1 || _phase == 3 || _phase == 5)
        {
            foreach (var oid in sa.Data.PartyList)
            {
                var sourceId = evt.SourceId();
                if (oid == 0) continue;

                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"水波扇形_{oid:X}";
                dp.Owner = evt.SourceId();
                dp.TargetObject = oid;
                dp.Color = sa.Data.DefaultDangerColor;
                dp.DestoryAt = (uint)duration;
                dp.Scale = new Vector2(60f, 60f); // radius, length
                dp.ScaleMode = ScaleMode.ByTime;
                dp.Radian = float.Pi / 6f; // 30 degrees

                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }
        }
        else if (_phase == 2)
        {
            // 只在身上有statusid 4975的人画扇形
            const uint BuffId = 4975;
            var buffed = sa.GetParty()
                .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                .ToList();

            if (buffed.Count == 0) return;
            foreach (var p in buffed)
            {
                var oid = p.EntityId;

                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"水波扇形_{oid:X}";
                dp.Owner = evt.SourceId();
                dp.TargetObject = oid;
                dp.Color = sa.Data.DefaultDangerColor;
                dp.DestoryAt = (uint)duration;
                dp.Scale = new Vector2(60f, 60f); // radius, length
                dp.ScaleMode = ScaleMode.ByTime;
                dp.Radian = float.Pi / 6f; // 30 degrees

                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }

        }
        
        var myIdx = sa.MyIndex();
        var bossId = evt.SourceId();
        var bossObj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == bossId);
        // boss 位置（y 固定 0）
        var bossPos = new Vector3(bossObj.Position.X, 0f, bossObj.Position.Z);

        if (bossObj == null) return;
        // 画图，0去boss正前面，1去boss正后，2去boss左侧，3去boss右侧，4去boss左后，5去boss右后，6去boss左前，7去boss右前。去的位置都是以boss为中心，距离4m处
        if (_phase == 1)
        {
            // boss 朝向 -> forward/right（y 固定 0）
            var rot = bossObj.Rotation;
            var fwd = new Vector3((float)Math.Sin(rot), 0f, (float)Math.Cos(rot));
            if (fwd.LengthSquared() < 1e-6f) fwd = new Vector3(0f, 0f, 1f);
            fwd = Vector3.Normalize(fwd);

            var right = new Vector3(-fwd.Z, 0f, fwd.X);
            right = Vector3.Normalize(right);

            var left = -right;
            var back = -fwd;

            float r = 4f; // 距离 4m

            Vector3 wpos = myIdx switch
            {
                0 => bossPos + fwd * r,                        // 正前
                1 => bossPos + back * r,                       // 正后
                2 => bossPos + left * r,                       // 左
                3 => bossPos + right * r,                      // 右
                4 => bossPos + Vector3.Normalize(left + back) * r,   // 左后
                5 => bossPos + Vector3.Normalize(right + back) * r,  // 右后
                6 => bossPos + Vector3.Normalize(left + fwd) * r,    // 左前
                7 => bossPos + Vector3.Normalize(right + fwd) * r,   // 右前
                _ => bossPos
            };

            wpos = new Vector3(wpos.X, 0f, wpos.Z);

            // 指路（持续时间用同一个 duration）
            var dpWp = sa.WaypointDp(wpos, (uint)duration, 0, "水波站位指路");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
        }
        else if (_phase == 2)
        {
            const uint BuffId = 4975;
            float r = 4f;
            // 判断boss在左半场和右半场：x<100左以东基准，x>100右以西基准
            _P2左右 = bossPos.X < 100f ? 1 : 2;
            // sa.Method.SendChat($"/e P2左右");
            // sa.Method.SendChat($"/e P2左右={_P2左右}");
            // 只在身上有 4975 的人参与分配
            var buffed = sa.GetParty()
                .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                .ToList();

            if (buffed.Count == 0) return;

            // 只给自己画（你之前其它指路也是这个风格）
            if (!buffed.Any(p => p.EntityId == sa.Data.Me)) return;

            // 判断 boss 在上半场还是下半场：z>100
            bool zHigh = bossPos.Z > 100f;

            var myIdx2 = sa.MyIndex();
            if (myIdx2 < 0) return;

            var role = GetRoleByPartyIndex(myIdx2);
            float baseYaw;
            if (_P2左右 == 1)
            {
                // 左半场，以东为基准
                baseYaw = MathF.PI / 2f;
            }
            else
            {
                // 右半场，以西为基准
                baseYaw = -MathF.PI / 2f;
            }
            // float baseYaw = MathF.PI / 2f;

            float offRad = GetWaterOffsetRad(p2第一轮打法, zHigh, role, _P2左右);
            float yaw = baseYaw + offRad;

            var dir = DirFromYaw(yaw);           // (sin(yaw), 0, cos(yaw))
            var wpos2 = bossPos + dir * r;
            wpos2 = new Vector3(wpos2.X, 0f, wpos2.Z);

            var dpWp2 = sa.WaypointDp(wpos2, (uint)duration, 0, "水波站位指路P2");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp2);
        }

        

        await Task.Delay(duration);
        // 记录每个玩家位置
        // 然后// --- 情况 A: 46558 (保持原方向 往每个玩家位置喷30度) ---  // --- 情况 B: 46560 (左右偏转 24度，各画一个 15度扇形) ---
        var snapPos = new Dictionary<uint, Vector3>();
        foreach (var oid in sa.Data.PartyList)
        {
            if (oid == 0) continue;

            var obj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == oid);
            if (obj == null) continue;

            snapPos[oid] = new Vector3(obj.Position.X, 0f, obj.Position.Z);
        }
        sa.Method.SendChat($"/e 水波喷射记录完毕");

        // 重新抓一次 boss（防止 cast 期间移动）
        bossObj = sa.Data.Objects.FirstOrDefault(o => o.GameObjectId == bossId);
        if (bossObj == null) return;

        bossPos = new Vector3(bossObj.Position.X, 0f, bossObj.Position.Z);

        // ============= 4) 根据 actionId 画后续喷射 =============
        const uint afterMs = 3500; // 后续扇形显示时间，你想短点/长点都行
        const float radius = 60f;
        const float length = 60f;

        static float YawFromDir(Vector2 d)
        {
            // 与你 sin/cos 的 forward 定义一致：yaw=atan2(x,z)
            if (d.LengthSquared() < 1e-6f) return 0f;
            d = Vector2.Normalize(d);
            return MathF.Atan2(d.X, d.Y);
        }

        void DrawFanFixed(string name, float yaw, float radian)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = bossPos;               // 固定从 boss 位置发射
            dp.Rotation = yaw;
            dp.FixRotation = true;
            dp.Color = sa.Data.DefaultDangerColor;
            dp.DestoryAt = afterMs;
            dp.Scale = new Vector2(radius, length);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Radian = radian;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        if (actionId == "46557")
        {
            // --- 情况 A: 保持原方向，往每个玩家当时位置喷 30度 ---
            if (_phase == 1 || _phase == 3 || _phase == 5)
            {
                foreach (var kv in snapPos)
                {
                    var oid = kv.Key;
                    var p = kv.Value;

                    var dir = new Vector2(p.X - bossPos.X, p.Z - bossPos.Z);
                    var yaw = YawFromDir(dir);

                    DrawFanFixed($"水波喷射A_{oid:X}", yaw, float.Pi / 6f); // 30°
                }
            }
            else if (_phase == 2)
            {
                const uint BuffId = 4975;
                var buffed = sa.GetParty()
                    .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                    .ToList();
                if (buffed.Count == 0) return;
                foreach (var p in buffed)
                {
                    var oid = p.EntityId;
                    if (!snapPos.TryGetValue(oid, out var pos)) continue;

                    var dir = new Vector2(pos.X - bossPos.X, pos.Z - bossPos.Z);
                    var yaw = YawFromDir(dir);

                    DrawFanFixed($"水波喷射A_{oid:X}", yaw, float.Pi / 6f); // 30°
                }
            }
        }
        else if (actionId == "46560")
        {
            // --- 情况 B: 左右偏转 24度，各画一个 15度扇形 ---
            float off = 24f * (float)Math.PI / 180f;   // 24°
            float rad = 15f * (float)Math.PI / 180f;   // 15°
            if (_phase == 1 || _phase == 3 || _phase == 5)
            {
                foreach (var kv in snapPos)
                {
                    var oid = kv.Key;
                    var p = kv.Value;

                    var dir = new Vector2(p.X - bossPos.X, p.Z - bossPos.Z);
                    var baseYaw = YawFromDir(dir);

                    DrawFanFixed($"水波喷射B_L_{oid:X}", baseYaw - off, rad);
                    DrawFanFixed($"水波喷射B_R_{oid:X}", baseYaw + off, rad);
                }
            }
            else if (_phase == 2)
            {
                const uint BuffId = 4975;
                var buffed = sa.GetParty()
                    .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                    .ToList();
                if (buffed.Count == 0) return;
                foreach (var p in buffed)
                {
                    var oid = p.EntityId;
                    if (!snapPos.TryGetValue(oid, out var pos)) continue;

                    var dir = new Vector2(pos.X - bossPos.X, pos.Z - bossPos.Z);
                    var baseYaw = YawFromDir(dir);

                    DrawFanFixed($"水波喷射B_L_{oid:X}", baseYaw - off, rad);
                    DrawFanFixed($"水波喷射B_R_{oid:X}", baseYaw + off, rad);
                }
            }
        }
    }

    [ScriptMethod(name: "狂浪腾空", eventType: EventTypeEnum.EnvControl, eventCondition: ["Index:regex:^\\d+$"])]
    public void 狂浪腾空(Event evt, ScriptAccessory sa)
    {
        // 14 => new Vector3(87f, 0f, 87f),
        // 15 => new Vector3(100f, 0f, 87f),
        // 16 => new Vector3(113f, 0f, 87f),
        // 17 => new Vector3(87f, 0f, 100f),
        // 18 => new Vector3(100f, 0f, 100f),
        // 19 => new Vector3(113f, 0f, 100f),
        // 20 => new Vector3(87f, 0f, 113f),
        // 21 => new Vector3(100f, 0f, 113f),
        // 22 => new Vector3(113f, 0f, 113f),
        if (!EnvHelpers.TryGetGridIndexFlag(evt, out var index, out var flag)) return;
        if (!EnvHelpers.TryGetGridPos(index, out var gridPos)) return;

        var baseName = $"九宫格_{index}";

        // 仅 flag=4/8 时清除并退出（保持原逻辑）
        if (EnvHelpers.IsClearFlag(flag))
        {
            EnvHelpers.ClearGridDraw(sa, baseName);
            return;
        }

        var drawColor = sa.Data.DefaultDangerColor;

        switch (EnvHelpers.GetMechanic(flag))
        {
            case EnvHelpers.GridMechanic.Tankbuster:
                EnvHelpers.DrawTB(sa, baseName, gridPos, drawColor);
                break;
            case EnvHelpers.GridMechanic.Stack:
                EnvHelpers.DrawStack(sa, baseName, gridPos, sa.Data.DefaultSafeColor);
                break;
            case EnvHelpers.GridMechanic.Spread:
                EnvHelpers.DrawSpread(sa, baseName, gridPos, drawColor);
                break;
            default:
                break;
        }

        if (_phase != 5) return;
        // P5
        bool isWaterSnake = EnvHelpers.IsWater(flag);                 // 2/32/128
        bool isFireSnake  = (flag == 512 || flag == 2048 || flag == 8192);

        if (!isWaterSnake && !isFireSnake) return;

        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        // 当前格子对应的蛇类型
        var snakeFromFlag = isWaterSnake ? P5火水蛇.水 : P5火水蛇.火;

        // 我自己的蛇形BUFF是否与该格子“相同”
        bool sameSnake = (_P5火水蛇 != P5火水蛇.无) && (_P5火水蛇 == snakeFromFlag);

        // 发指路的小函数：只给自己发
        void DrawWpIfNeeded(string name)
        {
            var wpos = new Vector3(gridPos.X, 0f, gridPos.Z);
            var dpWp = sa.WaypointDp(wpos, 6000, 0, name);
            if (p5换边指路)
            {
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
            }
        }

        // 1) 死刑(TB)：flag=128 或 8192
        // TextInfo 提示换T，并且只给 T(0/1) 指路：如果自己的蛇形 != 格子的蛇形 才去
        if (flag == 128 || flag == 8192)
        {
            sa.Method.TextInfo("死刑：换T", 2500, false);

            if (myIdx == 0 || myIdx == 1)
            {
                if (!sameSnake)
                    DrawWpIfNeeded("狂浪腾空-换T指路");
            }
            return; // 死刑格子不参与下面的“轮换奶/近/远”
        }

        // 2) 分摊/分散：flag=2/32/512/2048
        bool isSpreadOrStack = (flag == 2 || flag == 32 || flag == 512 || flag == 2048);
        if (!isSpreadOrStack) return;

        // 轮换提示：0奶 -> 1近 -> 2远
        if (_P5狂浪次数 < 2) sa.Method.TextInfo("狂浪：换奶", 2500, false);
        else if (_P5狂浪次数 < 4) sa.Method.TextInfo("狂浪：换近战", 2500, false);
        else sa.Method.TextInfo("狂浪：换远程", 2500, false);

        // 决定这一轮谁要去“对蛇格子”（且：自己的蛇形 != 格子的蛇形 才去）
        bool shouldHandle =
            (_P5狂浪次数 < 2 && (myIdx == 2 || myIdx == 3)) || // 奶
            (_P5狂浪次数 >= 2 && _P5狂浪次数 < 4 && (myIdx == 4 || myIdx == 5)) || // 近战
            (_P5狂浪次数 >= 4 && _P5狂浪次数 < 6 && (myIdx == 6 || myIdx == 7));   // 远程
        
        // bool shouldHandleHeal = (_P5狂浪次数 < 2 && (myIdx == 2 || myIdx == 3));
        // bool shouldHandleMelee = (_P5狂浪次数 >= 2 && _P5狂浪次数 < 4 && (myIdx == 4 || myIdx == 5));
        // bool shouldHandleRanged = (_P5狂浪次数 >= 4 && _P5狂浪次数 < 6 && (myIdx == 6 || myIdx == 7));
        // sa.Method.SendChat($"/e 狂浪 shouldHandle={shouldHandle} 奶={shouldHandleHeal} 近={shouldHandleMelee} 远={shouldHandleRanged} sameSnake={sameSnake}");
        if (shouldHandle)
        {
            if (!sameSnake)
                DrawWpIfNeeded("狂浪腾空-对蛇指路");
        }

        // 次数推进：0->1->2->0 循环
        // if (!isWaterSnake) return;
        _P5狂浪次数 = _P5狂浪次数 + 1;
        sa.Method.SendChat($"/e P5狂浪次数={_P5狂浪次数}");
    }

    [ScriptMethod(
        name: "浪尖转体分摊分散",
        eventType: EventTypeEnum.EnvControl,
        eventCondition: ["Index:regex:^\\d+$"]
    )]
    public async void 浪尖转体分摊分散(Event evt, ScriptAccessory sa)
    {
        if (!int.TryParse(evt["Index"], out var index)) return;
        if (!int.TryParse(evt["Flag"], out var flag)) return;

        if (index != 4)
            return;

        if (flag == 2048)
        {
            sa.Method.EdgeTTS("待会儿分散");
            
            await Task.Delay(5000);
            // 有BUFF的人画圈
            const uint BuffId = 4975;
            var buffed = sa.GetParty()
                .Where(p => p != null && p.EntityId != 0 && p.HasStatus(BuffId))
                .ToList();
            if (buffed.Count == 0) return;
            foreach (var p in buffed)
            {
                var oid = p.EntityId;

                var dpCircle = sa.Data.GetDefaultDrawProperties();
                dpCircle.Name = $"浪尖转体分散圈_{oid:X}";
                dpCircle.Owner = oid;
                dpCircle.Color = sa.Data.DefaultDangerColor;
                dpCircle.DestoryAt = 5000;
                dpCircle.Scale = new Vector2(5f);
                dpCircle.ScaleMode = ScaleMode.ByTime;

                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
            }
        }
        else if (flag == 128)
        {
            sa.Method.EdgeTTS("待会儿分摊");

            await Task.Delay(5000);
            // 有BUFF的人画圈
            // P2 分摊只画有BUFF的治疗
            var buffId = 4975u;
            for (int i = 2; i < 4; i++)
            {
                var oid = sa.Data.PartyList[i];
                var p = sa.GetParty().FirstOrDefault(x => x != null && x.EntityId == oid);
                if (p == null || oid == 0 || !p.HasStatus(buffId))
                    continue;

                var dpCircle = sa.Data.GetDefaultDrawProperties();
                dpCircle.Name = $"分摊圈_{oid:X}";
                dpCircle.Owner = oid;
                dpCircle.Color = sa.Data.DefaultSafeColor;
                dpCircle.DestoryAt = 5000;
                dpCircle.Scale = new Vector2(5f);
                dpCircle.ScaleMode = ScaleMode.ByTime;

                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                }
        }
    }

    // 浪尖转体
    [ScriptMethod(
        name: "浪尖转体",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(46547|46488)$"]
    )]
    public void 浪尖转体(Event evt, ScriptAccessory sa)
    {
        var sourcePos0 = evt.SourcePosition();
        var sourceRot = evt.SourceRotation;

        // y 固定 0
        var sourcePos = new Vector3(sourcePos0.X, 0f, sourcePos0.Z);

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "浪尖转体-扇形";
        dp.Position = sourcePos;
        dp.Rotation = sourceRot;
        dp.FixRotation = true;
        dp.Color = sa.Data.DefaultDangerColor;
        dp.DestoryAt = 6500;
        dp.Radian = (float)Math.PI * 2f / 3f; // 120 degrees
        dp.Scale = new Vector2(50f);
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        _浪花位置 = sourcePos;

        // ===== Buff 判定 =====
        const uint WaterBuffId = 4975;
        const uint FireBuffId  = 4974;

        bool hasWater = sa.GetParty().Any(p => p != null && p.EntityId == sa.Data.Me && p.HasStatus(WaterBuffId));
        bool hasFire  = sa.GetParty().Any(p => p != null && p.EntityId == sa.Data.Me && p.HasStatus(FireBuffId));

        if (!hasWater && !hasFire) return;

        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;

        bool zHigh = sourcePos.Z > 100f; // 上/下

        // 分组：0/1, 2/3, 4/5, 6/7
        int pairGroup = myIdx switch
        {
            0 or 1 => 0,
            2 or 3 => 1,
            4 or 5 => 2,
            _      => 3, // 6 or 7
        };

        void DrawWp(Vector3 wpos, string name)
        {
            wpos = new Vector3(wpos.X, 0f, wpos.Z);
            var dpWp = sa.WaypointDp(wpos, 6000, 0, name);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
        }

        // =========================================
        // P1：你原来的兜底（没写 P1 美野就也走这个）
        // =========================================
        if (_phase < 2)
        {
            var forward = new Vector3(MathF.Sin(sourceRot), 0f, MathF.Cos(sourceRot));
            if (forward.LengthSquared() < 1e-6f) forward = new Vector3(0f, 0f, 1f);
            forward = Vector3.Normalize(forward);

            var wpos = sourcePos + forward * 10f;
            DrawWp(wpos, "浪尖转体-指路");
            return;
        }

        // =========================================
        // P2：按打法分流
        // =========================================
        if (p2第二三轮打法 == P2第二三轮打法.近战优化)
        {
            // -------------------------
            // 水 buff：所有水去固定点（你注释的 4 点）
            // -------------------------
            if (hasWater)
            {
                Vector3 wposWater;
                if (_P2左右 == 1)
                {
                    // 左(东侧基准)
                    // wposWater = zHigh ? new Vector3(89f, 0f, 119f) : new Vector3(89f, 0f, 81f);

                    wposWater = myIdx switch
                    {
                        0 or 1 or 2 or 3 => zHigh ? new Vector3(80.65f, 0f, 113.99f) : new Vector3(80.65f, 0f, 86.01f),
                        4 or 5 or 6 or 7 => zHigh ? new Vector3(87.49f, 0f, 114.21f): new Vector3(87.49f, 0f, 85.79f),
                        _ => zHigh ? new Vector3(89f, 0f, 119f) : new Vector3(89f, 0f, 81f),
                    };
                }
                else
                {
                    // 右(西侧基准)
                    wposWater = zHigh ? new Vector3(111f, 0f, 119f) : new Vector3(111f, 0f, 81f);
                }

                DrawWp(wposWater, "浪尖转体-指路P2(水-近战优化)");
            }

            // -------------------------
            // 火 buff：按优先级 0 1 4 5 2 3 6 7 分 4 组去 4 个点
            // 你原来 xs 顺序写反了，这里按注释修正
            // -------------------------
            if (hasFire)
            {
                // z
                float z = zHigh ? 119.5f : 80.5f;

                // _P2左右==1: 组0/1/2/3 => 116.83, 109.62, 90.4, 82.50
                // _P2左右==2: 组0/1/2/3 => 82.50, 90.4, 109.62, 116.83
                float[] xs = _P2左右 == 1
                    ? new float[] { 80.50f, 90.40f, 104.62f, 111f }
                    : new float[] { 111f, 104.62f, 90.40f, 80.50f };

                // 分组： (0,1)->0, (4,5)->1, (2,3)->2, (6,7)->3  （按你注释）
                int fireGroup = myIdx switch
                {
                    0 or 1 => 0,
                    4 or 5 => 1,
                    2 or 3 => 2,
                    _      => 3, // 6 or 7
                };

                var wposFire = new Vector3(xs[fireGroup], 0f, z);
                DrawWp(wposFire, "浪尖转体-指路P2(火-近战优化)");
            }

            return;
        }

        // =========================================
        // 美式野：严格按你给的表（火 buff 表 / 水 buff 表）
        // =========================================
        // 你给的点位是“固定坐标表”，这里不再用 _P2左右
        // 只要有火就走火表，否则走水表（避免同一人画两次）
        bool useFireTable = hasFire;

        Vector3 wposUS;
        if (!zHigh)
        {
            // sourcePos.Z < 100
            if (useFireTable)
            {
                wposUS = pairGroup switch
                {
                    0 => new Vector3(103.22f, 0.00f, 80.61f), // 0/1
                    1 => new Vector3(119.57f, 0.00f, 80.61f), // 2/3
                    2 => new Vector3(109.78f, 0.00f, 80.61f), // 4/5
                    _ => new Vector3(118.76f, 0.00f, 88.66f), // 6/7
                };
            }
            else
            {
                wposUS = pairGroup switch
                {
                    0 => new Vector3(96.78f, 0.00f, 80.61f),  // 100-3.22
                    1 => new Vector3(80.43f, 0.00f, 80.61f),
                    2 => new Vector3(90.22f, 0.00f, 80.61f),
                    _ => new Vector3(81.24f, 0.00f, 88.66f),
                };
            }
        }
        else
        {
            // sourcePos.Z > 100
            if (useFireTable)
            {
                wposUS = pairGroup switch
                {
                    0 => new Vector3(103.22f, 0.00f, 119.39f), // 0/1
                    1 => new Vector3(119.57f, 0.00f, 119.55f), // 2/3
                    2 => new Vector3(109.78f, 0.00f, 119.35f), // 4/5
                    _ => new Vector3(118.76f, 0.00f, 113.26f), // 6/7
                };
            }
            else
            {
                wposUS = pairGroup switch
                {
                    0 => new Vector3(96.78f, 0.00f, 119.39f),  // 100-3.22
                    1 => new Vector3(80.43f, 0.00f, 119.55f),
                    2 => new Vector3(90.22f, 0.00f, 119.35f),
                    _ => new Vector3(81.24f, 0.00f, 113.26f),
                };
            }
        }

        DrawWp(wposUS, useFireTable ? "浪尖转体-指路P2(火-美式野)" : "浪尖转体-指路P2(水-美式野)");
    }

    // P3 水牢
    [ScriptMethod(
        name: "进牢指路",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46563"]
    )]
    public void 进牢指路(Event evt, ScriptAccessory sa)
    {
        // 看进水牢方式的选择，指路(100,0,100).如果选择是T就是只有0 1画，治疗是2 3画，近战是4 5画.但是你还是要mydex来查看自己的index，如果不是就不画
        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 5) return;
        if (进水牢方式 == 进水牢.坦克进水牢 && myIdx > 1) return;
        if (进水牢方式 == 进水牢.治疗进水牢 && (myIdx < 2 || myIdx > 3)) return;
        if (进水牢方式 == 进水牢.近战进水牢 && (myIdx < 4 || myIdx > 5)) return;

        Vector3 wpos = new Vector3(100f, 0f, 100f);
        var dpWp = sa.WaypointDp(wpos, 8000, 0, "进牢指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);

    }

    // 水牢画图指路-火
    [ScriptMethod(
        name: "水牢画图指路-火",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:regex:027C"]
    )]
    public void 水牢画图指路火(Event evt, ScriptAccessory sa)
    {
        var sourceId = evt.SourceId();
        // 查找BOSS炽红 DATABID对应的位置
        var bossObj = sa.Data.Objects.FirstOrDefault(o => o.DataId == 炽红);
        if (bossObj == null) return;
        var bossPos = new Vector3(bossObj.Position.X, 0f, bossObj.Position.Z);
        // 画图
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "水牢画图指路-火";
        dp.Position = bossPos;
        dp.TargetObject = sourceId;
        dp.Color = new Vector4(0.8863f, 0.5451f, 0.1569f, 1f);
        dp.DestoryAt = 6000;
        dp.Scale = new Vector2(8f, 50f);
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);


        if (sourceId != sa.Data.Me) return;
        if (水牢打法选择 == 水牢打法.MMW)
        {
            // 指路 一共有四个点
            var p_NE = new Vector3(113.10f, 0.00f, 113.21f); // 上半场 + 左半场  -> 去右上
            var p_NW = new Vector3(86.37f,  0.00f, 112.98f); // 上半场 + 右半场  -> 去左上
            var p_SW = new Vector3(86.90f,  0.00f, 86.44f);  // 下半场 + 右半场  -> 去左下
            var p_SE = new Vector3(113.38f, 0.00f, 86.16f);  // 下半场 + 左半场  -> 去右下

            bool topHalf = bossPos.Z < 100f;    // 你注释里写的：Z<100 上半场
            bool rightHalf = bossPos.X > 100f;  // X>100 右半场（否则左半场）

            Vector3 wpos;
            if (topHalf)
            {
                // 上半场
                wpos = rightHalf ? p_NW : p_NE;
            }
            else
            {
                // 下半场
                wpos = rightHalf ? p_SW : p_SE;
            }

            // 发一个位移指路
            var dpWp = sa.WaypointDp(wpos, 6000, 0, "水牢指路-火");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
        }
        else
        {
            // 无脑
            // 如果boss上半场，指路  (95.39f, -0.00f, 118.40f)
            // 如果boss下半场，指路 {96.97, 0.00, 81.30}
            if (_P3水牢次数 == 0)
            {
                Vector3 wpos = bossPos.Z < 100f
                    ? new Vector3(97.97f, 0.00f, 119.09f)
                    : new Vector3(96.97f, 0.00f, 80.73f);
                var dpWp = sa.WaypointDp(wpos, 6000, 0, "水牢指路-火");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
            }
            else
            {
                if (_P3水牢深蓝初始位置 == 1)
                {
                    Vector3 wpos = bossPos.Z < 100f
                    ? new Vector3(97.97f, 0.00f, 119.09f)
                    : new Vector3(110.36f, 0.00f, 83.66f);
                var dpWp = sa.WaypointDp(wpos, 6000, 0, "水牢指路-火");
                sa.Method.SendChat($"/e 深蓝初始位置1, 水牢>1. wpos:{wpos}");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
                }
                else
                {
                    Vector3 wpos = bossPos.Z < 100f
                    ? new Vector3(108.41f, 0.00f, 117.58f)
                    : new Vector3(96.97f, 0.00f, 80.73f);
                    sa.Method.SendChat($"/e 深蓝初始位置{_P3水牢深蓝初始位置}, 水牢>1. wpos:{wpos}");
                var dpWp = sa.WaypointDp(wpos, 6000, 0, "水牢指路-火");
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
                }
            }
            
        }
    }

    // // 水牢画图指路-水
    [ScriptMethod(
        name: "水牢画图指路-水",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:regex:027B"]
    )]
    public void 水牢画图指路水(Event evt, ScriptAccessory sa)
    {
        var sourceId = evt.SourceId();

        var bossObj = sa.Data.Objects.FirstOrDefault(o => o.DataId == 深蓝);
        if (bossObj == null) return;
        var bossPos = new Vector3(bossObj.Position.X, 0f, bossObj.Position.Z);
        // 画图
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "水牢画图指路-水";
        dp.Position = bossPos;
        dp.TargetObject = sourceId;
        dp.Color = new Vector4(0.1569f, 0.5451f, 0.8863f, 1f);
        dp.DestoryAt = 6000;
        dp.Scale = new Vector2(8f, 50f);
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        
        // 指路
        // 首先判断p3水牢方法
        Vector3 wpos = default;
        if (水牢打法选择 == 水牢打法.MMW)
        {
            if (_P3水牢次数 == 0)
            {
                // 如果BossPos.Z > 100 BOSS下半场，_P3水牢深蓝初始位置 = 4 去 {95.67, 0.00, 81.59}
                // 如果bossPos.Z < 100 上半场，_P3水牢深蓝初始位置 = 1 去 {104.04, 0.00, 119.04}
                
                if (bossPos.Z > 100f)
                {
                    _P3水牢深蓝初始位置 = 4;
                    sa.Method.SendChat($"/e _P3水牢深蓝初始位置设为4");
                    wpos = new Vector3(95.67f, 0.00f, 81.59f);
                }
                else
                {
                    sa.Method.SendChat($"/e _P3水牢深蓝初始位置设为4");
                    _P3水牢深蓝初始位置 = 1;
                    wpos = new Vector3(104.04f, 0.00f, 119.04f);
                }
            }
            else
            {
                // sa.Method.SendChat($"/e >1次水牢");
                if (_P3水牢深蓝初始位置 == 4)
                {
                    // 初始 4：
                    // bossPos.Z > 93  -> {118.13, 0.00, 105.24}
                    // bossPos.Z < 93  -> {95.67, 0.00, 81.59}
                    wpos = bossPos.Z < 93f
                        ? new Vector3(118.13f, 0.00f, 105.24f)
                        : new Vector3(95.67f, 0.00f, 81.59f);
                }
                else // 初始 1（默认按 1 处理）
                {
                    // 初始 1：
                    // bossPos.X > 93  -> {81.76, 0.00, 94.95}
                    // bossPos.X < 93  -> {104.04, 0.00, 119.04}
                    wpos = bossPos.X > 93f
                        ? new Vector3(81.76f, 0.00f, 94.95f)
                        : new Vector3(104.04f, 0.00f, 119.04f);
                }
            }
        }
        else
        {
            // 无脑
            // 如果boss上半场，指路  {88.57, 0.00, 115.93}
            // 如果boss下半场，指路  {88.79, -0.00, 84.46}
            if (_P3水牢次数 == 0)
            {
                if (bossPos.Z > 100f)
                {
                    _P3水牢深蓝初始位置 = 4;
                    sa.Method.SendChat($"/e _P3水牢深蓝初始位置设为4");
                    wpos = new Vector3(95.67f, 0.00f, 81.59f);
                }
                else
                {
                    sa.Method.SendChat($"/e _P3水牢深蓝初始位置设为4");
                    _P3水牢深蓝初始位置 = 1;
                    wpos = new Vector3(104.04f, 0.00f, 119.04f);
                }
            }
        
            wpos = bossPos.Z < 100f
                ? new Vector3(88.57f, 0.00f, 115.93f)
                : new Vector3(88.79f, -0.00f, 84.46f);

        }
        var wp = sa.WaypointDp(wpos, 6000, 0, $"水牢指路-水_{_P3水牢次数 + 1}");
        if (sourceId == sa.Data.Me)
        {
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, wp);
        }
        // sa.Method.SendChat($"/e P3水牢次数={_P3水牢次数} wpos={wpos.X:F2},{wpos.Z:F2} bossPos={bossPos.X:F2},{bossPos.Z:F2}");
        _P3水牢次数++;
    }

    // P4
    // 浪顶炽火
    [ScriptMethod(
        name: "浪顶炽火",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46548"]
    )]
    public void 浪顶炽火(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;
        // 指路
        Vector3 wpos = myIdx switch
        {
            0 or 2 or 4 or 6 => new Vector3(93.23f, 0.00f, 95.37f),
            1 or 3 or 5 or 7 => new Vector3(106.34f, -0.00f, 96.78f),
            _ => Center,
        };
        var dpWp = sa.WaypointDp(wpos, 5000, 0, "浪顶炽火指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);
    }

    // 异常旋转巨火
    [ScriptMethod(
        name: "异常旋转巨火",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:46486"]
    )]
    public void 异常旋转巨火(Event evt, ScriptAccessory sa)
    {
        var myIdx = sa.MyIndex();
        if (myIdx < 0 || myIdx > 7) return;
        // 指路
        Vector3 wpos = myIdx switch
        {
            0 or 6 => new Vector3(90.37f, 0.00f, 82.63f),
            1 or 7 => new Vector3(109.58f, 0.00f, 82.10f),
            2 or 4 => new Vector3(89.81f, 0.00f, 117.30f),
            3 or 5 => new Vector3(108.98f, 0.00f, 117.27f),
            _ => Center,
        };
        var dpWp = sa.WaypointDp(wpos, 5000, 0, "异常旋转巨火指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpWp);

        if (_phase == 5) return;
        _phase = 5;
        sa.Method.SendChat("/e 马上进入P5阶段");
    }

    // 获得buff
    [ScriptMethod(
        name: "P5火水蛇形BUFF判定",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(4827|4828)$"],
        userControl: false
    )]
    public void P5火水蛇形BUFF判定(Event evt, ScriptAccessory sa)
    {
        var targetId = evt.TargetId();
        if (targetId != sa.Data.Me) return;

        var statusId = evt["StatusID"];
        if (statusId == "4827")
        {
            _P5火水蛇 = P5火水蛇.火;
            sa.Method.SendChat("/e 获得火蛇形BUFF");
        }
        else if (statusId == "4828")
        {
            _P5火水蛇 = P5火水蛇.水;
            sa.Method.SendChat("/e 获得水蛇形BUFF");
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

    public static DrawPropertiesEdit FastDpWithPosition(this ScriptAccessory sa, string name, Vector3 pos, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.DestoryAt = duration;
        dp.Scale = scale;
        return dp;
    }

    public static DrawPropertiesEdit FastDpWithTarget(this ScriptAccessory sa, string name, uint targetId, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Owner = targetId;
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
    public static IEnumerable<IPlayerCharacter> GetParty(this ScriptAccessory sa)
    {
        foreach (var pid in sa.Data.PartyList)
        {
            var obj = sa.Data.Objects.SearchByEntityId(pid);
            if (obj is IPlayerCharacter character) yield return character;
        }
    }
}

internal static class RayHelpers
{
    private static readonly Vector3 Center = new(100f, 0f, 100f);
    private const uint 炽红 = 12345;
    public static bool IsCoveredByRayXZ(Vector3 point, Vector3 bossPos, Vector2 dirXZ, float halfWidth)
    {
        var d2 = dirXZ.LengthSquared();
        if (d2 < 0.0001f) return false;

        var vx = point.X - bossPos.X;
        var vz = point.Z - bossPos.Z;

        var t = (vx * dirXZ.X + vz * dirXZ.Y) / d2;
        if (t < 0f) return false;

        var cross = dirXZ.X * vz - dirXZ.Y * vx;
        var dist = MathF.Abs(cross) / MathF.Sqrt(d2);

        return dist <= halfWidth;
    }

    public static Vector3 AdjustPointDownUntilNotCoveredXZ(
        Vector3 p0, Vector3 bossPos, Vector2 dirXZ,
        float halfWidth, float epsilon, float maxDown)
    {
        if (!IsCoveredByRayXZ(p0, bossPos, dirXZ, halfWidth))
            return p0;

        var dx = dirXZ.X;
        var dz = dirXZ.Y;
        var dLen = MathF.Sqrt(dx * dx + dz * dz);
        if (dLen < 0.0001f) return p0;

        if (MathF.Abs(dx) < 1e-4f)
        {
            var z = p0.Z;
            var minZ = p0.Z - maxDown;
            while (z > minZ)
            {
                z -= 0.1f;
                var p = new Vector3(p0.X, p0.Y, z);
                if (!IsCoveredByRayXZ(p, bossPos, dirXZ, halfWidth))
                    return p;
            }
            return new Vector3(p0.X, p0.Y, minZ);
        }

        var x = p0.X;
        var bz = bossPos.Z;
        var bx = bossPos.X;

        var C = -dx * bz - dz * (x - bx);
        var K = halfWidth * dLen;

        var z1 = ( K - C) / dx;
        var z2 = (-K - C) / dx;

        var low = MathF.Min(z1, z2);
        var high = MathF.Max(z1, z2);

        var z0 = p0.Z;
        if (z0 >= low && z0 <= high)
        {
            var zNew = low - epsilon;
            var pTry = new Vector3(x, p0.Y, zNew);
            if (!IsCoveredByRayXZ(pTry, bossPos, dirXZ, halfWidth))
                return pTry;

            zNew -= 0.2f;
            return new Vector3(x, p0.Y, zNew);
        }

        var zFallback = z0;
        var minZFb = z0 - maxDown;
        while (zFallback > minZFb)
        {
            zFallback -= 0.1f;
            var p = new Vector3(x, p0.Y, zFallback);
            if (!IsCoveredByRayXZ(p, bossPos, dirXZ, halfWidth))
                return p;
        }
        return new Vector3(x, p0.Y, minZFb);
    }

    public static bool TryGetBossBasis(
        ScriptAccessory sa,
        uint bossDataId,
        Vector3 center,
        out Vector3 bossPos,
        out Vector2 forwardXZ,
        out Vector2 rightXZ)
    {
        bossPos = default;
        forwardXZ = default;
        rightXZ = default;

        var bossObj = sa.Data.Objects.FirstOrDefault(o => o.DataId == bossDataId);
        if (bossObj == null) return false;

        bossPos = bossObj.Position;

        // forward = 指向中心（面向场内）
        var f = new Vector2(center.X - bossPos.X, center.Z - bossPos.Z);
        if (f.LengthSquared() < 0.0001f) return false;
        f = Vector2.Normalize(f);

        // right = forward 的右侧（确保和你给的例子匹配）
        var r = new Vector2(-f.Y, f.X);

        forwardXZ = f;
        rightXZ = r;
        return true;
    }

    public static Vector3 BossRelativePoint(
        Vector3 bossPos,
        Vector2 forwardXZ,
        Vector2 rightXZ,
        float rightDist,
        float upDist)
    {
        var x = bossPos.X + rightXZ.X * rightDist + forwardXZ.X * upDist;
        var z = bossPos.Z + rightXZ.Y * rightDist + forwardXZ.Y * upDist;
        return new Vector3(x, 0f, z);
    }

    public static Vector3 AdjustPointAlongDirUntilNotCoveredXZ(
        Vector3 p0,
        Vector3 rayBossPos,
        Vector2 rayDirXZ,
        Vector2 moveDirXZ,   // IMPORTANT: normalized direction you want to move along
        float halfWidth,
        float epsilon,
        float maxMove)
    {
        if (!IsCoveredByRayXZ(p0, rayBossPos, rayDirXZ, halfWidth))
            return p0;

        var r2 = rayDirXZ.LengthSquared();
        if (r2 < 1e-6f) return p0;

        if (moveDirXZ.LengthSquared() < 1e-6f) return p0;
        var m = Vector2.Normalize(moveDirXZ);

        // v0 = p0 - boss
        var v0 = new Vector2(p0.X - rayBossPos.X, p0.Z - rayBossPos.Z);

        // cross(r, v) = rx*vz - rz*vx
        float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        var r = rayDirXZ;
        var cross0 = Cross(r, v0);
        var rLen = MathF.Sqrt(r2);
        var K = halfWidth * rLen;

        // If moving direction doesn't change cross, stepping won't escape by distance -> fallback stepping
        var crossRM = Cross(r, m);

        // Also consider escaping by t<0 (behind boss on the ray direction)
        var dot0 = Vector2.Dot(v0, r);
        var dotMR = Vector2.Dot(m, r);

        float bestS = float.PositiveInfinity;

        // Candidate 1: escape by distance boundary |cross| > K
        if (MathF.Abs(crossRM) > 1e-6f)
        {
            // Solve cross(s) = ±K, where cross(s) = cross0 + crossRM * s
            var sA = ( K - cross0) / crossRM;
            var sB = (-K - cross0) / crossRM;

            void TryPick(float s)
            {
                if (s < 0) return;
                var s2 = s + epsilon; // step slightly past boundary
                if (s2 > maxMove) return;

                var px = p0.X + m.X * s2;
                var pz = p0.Z + m.Y * s2;
                var pTry = new Vector3(px, p0.Y, pz);
                if (!IsCoveredByRayXZ(pTry, rayBossPos, rayDirXZ, halfWidth))
                    bestS = MathF.Min(bestS, s2);
            }

            TryPick(sA);
            TryPick(sB);
        }

        // Candidate 2: escape by t<0 (move behind ray origin so t<0 => not covered)
        // t(s) = dot(v0 + m*s, r) / |r|^2
        // Need dot(v0 + m*s, r) < 0 => dot0 + dotMR*s < 0
        if (dot0 >= 0f && dotMR < -1e-6f)
        {
            var sT = (-dot0 / dotMR) + epsilon;
            if (sT >= 0f && sT <= maxMove)
            {
                var px = p0.X + m.X * sT;
                var pz = p0.Z + m.Y * sT;
                var pTry = new Vector3(px, p0.Y, pz);
                if (!IsCoveredByRayXZ(pTry, rayBossPos, rayDirXZ, halfWidth))
                    bestS = MathF.Min(bestS, sT);
            }
        }

        // If analytic fails, do small-step fallback
        if (float.IsInfinity(bestS))
        {
            float step = 0.1f;
            for (float s = step; s <= maxMove; s += step)
            {
                var px = p0.X + m.X * s;
                var pz = p0.Z + m.Y * s;
                var pTry = new Vector3(px, p0.Y, pz);
                if (!IsCoveredByRayXZ(pTry, rayBossPos, rayDirXZ, halfWidth))
                    return pTry;
            }
            // worst-case: clamp to maxMove
            return new Vector3(p0.X + m.X * maxMove, p0.Y, p0.Z + m.Y * maxMove);
        }

        return new Vector3(p0.X + m.X * bestS, p0.Y, p0.Z + m.Y * bestS);
    }

    public static Vector3 ClampXZ(Vector3 p, float min, float max)
    {
        var x = MathF.Max(min, MathF.Min(max, p.X));
        var z = MathF.Max(min, MathF.Min(max, p.Z));
        return new Vector3(x, 0f, z);
    }

    public static bool IsInsideRotRectXZ(Vector3 p, Vector3 rectCenter, float yaw, float width, float length)
    {
        // width/length 这里按“全宽/全长”理解，所以半宽半长要 /2
        float hx = width * 0.5f;
        float hz = length * 0.5f;

        // forward / right in XZ
        var f = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw)); // forward
        var r = new Vector2(-f.Y, f.X);                      // right

        var v = new Vector2(p.X - rectCenter.X, p.Z - rectCenter.Z);
        float localX = v.X * r.X + v.Y * r.Y; // right axis
        float localZ = v.X * f.X + v.Y * f.Y; // forward axis

        return MathF.Abs(localX) <= hx && MathF.Abs(localZ) <= hz;
    }

    /// <summary>
    /// 根据危险区（旋转矩形）判断三条车道：左(-lane), 中(0), 右(+lane) 哪些被覆盖。
    /// 返回给偶数组(0/2/4/6)与奇数组(1/3/5/7)的车道偏移值（沿 objRight 方向）。
    /// 优先级：左 -> 中 -> 右，从没覆盖的里面选两条不同的。
    /// </summary>
    public static void PickSafeLanesByDangerRect(
        Vector3 objPos,
        Vector3 objRight,
        Vector3 objForward,
        Vector3 dangerCenter,
        float dangerYaw,
        float dangerWidth,
        float dangerLength,
        float laneOffset,             // 13.86
        IReadOnlyList<float> fbSamples, // {+8,-2,-12,-22}
        out float evenLane,
        out float oddLane)
    {
        // lane candidates in priority order: left, center, right
        float[] lanes = { +laneOffset, 0f, -laneOffset };

        bool LaneCovered(float lane)
        {
            foreach (var fb in fbSamples)
            {
                var p = objPos + objRight * lane + objForward * fb;
                p = new Vector3(p.X, 0f, p.Z);
                if (IsInsideRotRectXZ(p, dangerCenter, dangerYaw, dangerWidth, dangerLength))
                    return true;
            }
            return false;
        }

        var safe = lanes.Where(l => !LaneCovered(l)).ToList();
        if (safe.Count == 0) safe.AddRange(lanes); // 兜底：全覆盖/算法异常时至少不崩

        evenLane = safe[0];
        oddLane = (safe.Count >= 2) ? safe[1] : safe[0];
    }

}

internal static class EnvHelpers
{
    public enum GridMechanic
    {
        None = 0,
        Tankbuster = 1,
        Stack = 2,
        Spread = 3,
    }

    public static bool TryGetGridIndexFlag(Event evt, out int index, out int flag)
    {
        index = 0;
        flag = 0;

        // 你原来的范围是 14~22（方法名写 15-21 但逻辑是 14-22，保持原样）
        if (!int.TryParse(evt["Index"], out index)) return false;
        if (index < 14 || index > 22) return false;

        if (!int.TryParse(evt["Flag"], out flag)) return false;
        return true;
    }

    public static bool TryGetGridPos(int index, out Vector3 pos)
    {
        // 14~22 = 3x3 grid:
        // 14 15 16  -> z=87,  x=87,100,113
        // 17 18 19  -> z=100, x=87,100,113
        // 20 21 22  -> z=113, x=87,100,113
        pos = Vector3.Zero;
        if (index < 14 || index > 22) return false;

        int i = index - 14;
        int row = i / 3; // 0..2
        int col = i % 3; // 0..2

        float x = 87f + 13f * col;
        float z = 87f + 13f * row;
        pos = new Vector3(x, 0f, z);
        return true;
    }

    public static bool IsClearFlag(int flag) => flag == 4 || flag == 8;

    public static bool IsWater(int flag) => flag == 2 || flag == 32 || flag == 128;

    public static GridMechanic GetMechanic(int flag)
    {
        // 保持原判定集合完全一致
        if (flag == 128 || flag == 8192) return GridMechanic.Tankbuster;
        if (flag == 32 || flag == 2048) return GridMechanic.Stack;
        if (flag == 2 || flag == 512) return GridMechanic.Spread;
        return GridMechanic.None;
    }

    public static void ClearGridDraw(ScriptAccessory sa, string baseName)
    {
        sa.Method.RemoveDraw($"{baseName}_TB");
        sa.Method.RemoveDraw($"{baseName}_Stack");
        for (int i = 1; i <= 4; i++)
            sa.Method.RemoveDraw($"{baseName}_Spread_{i}");
    }

    public static void SetupCommon(DrawPropertiesEdit dp, string name, Vector3 pos, Vector4 color)
    {
        dp.Name = name;
        dp.Position = pos;
        dp.Color = color;
        dp.DestoryAt = 9999999;
        dp.ScaleMode = ScaleMode.None;
    }

    public static void DrawTB(ScriptAccessory sa, string baseName, Vector3 pos, Vector4 color)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        SetupCommon(dp, $"{baseName}_TB", pos, color);

        dp.Scale = new Vector2(6f);
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.CentreOrderIndex = 1;

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    public static void DrawStack(ScriptAccessory sa, string baseName, Vector3 pos, Vector4 color)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        SetupCommon(dp, $"{baseName}_Stack", pos, color);

        dp.Scale = new Vector2(60f);
        dp.Radian = float.Pi / 4;
        dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.TargetOrderIndex = 1;

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    public static void DrawSpread(ScriptAccessory sa, string baseName, Vector3 pos, Vector4 color)
    {
        for (uint i = 1; i <= 4; i++)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            SetupCommon(dp, $"{baseName}_Spread_{i}", pos, color);

            dp.Scale = new Vector2(60f);
            dp.Radian = float.Pi / 4;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = i;

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }
}


#endregion
