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

namespace Codaaaaaa.DoomTrainScripts;

[ScriptType(guid: "3f8c6b2e-91c4-4a87-bd63-0b7a5f0d7e42", name: "格莱杨拉波尔歼殛战 指路+TTS", territorys: [1308], version: "0.0.0.9", author: "Codaaaaaa", note: "画图+指路+TTS。做个测试，使用前请务必调整小队顺序")]
public class NewRaid4P
{
    #region 用户设置
    [UserSetting("是否开启TTS")] public static bool TTSOpen { get; set; } = true;
    #endregion

    private static readonly Vector3 Center = new(100, 0, 100);
    
    // =========================
    // 共享机制：每个P都要独立存一次“分摊/分散”
    // =========================
    private enum 分摊分散
    {
        None = 0,
        Stack = 1,   // 分摊
        Spread = 2,  // 分散
    }

    private uint _phase = 1;
    private 分摊分散 超增压 = 分摊分散.None;

    // 异世界
    private static readonly Vector2 Origin = new(-400, -400);
    private const uint TargetBNpcId = 19329;

    private const int SampleCount = 7;
    private const int SampleIntervalMs = 500;
    private const float PredictStopT = 7.6f;
    private bool 以太晶球最大近战距离 = false;

    // 其他
    // private bool 最后平台飞箱 = false;

    public void Init(ScriptAccessory sa)
    {
        ResetAll();
        sa.Method.RemoveDraw(".*");
    }

    private void ResetAll()
    {
        _phase = 1;
        超增压 = 分摊分散.None;
        以太晶球最大近战距离 = false;
    }

    [ScriptMethod(name: "clear draw", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASCLEAR"], userControl: false)]
    public void ClearDraw(Event evt, ScriptAccessory sa) => sa.Method.RemoveDraw(".*");

    // =========================
    // 手动切P口令测试
    // =========================
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

    [ScriptMethod(name: "Set Phase 6", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:KASP6"], userControl: false)]
    public void SetP6(Event evt, ScriptAccessory sa) => _phase = 6;
    
    [ScriptMethod(name: "Show Phase", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:phase"], userControl: false)]
    public void ShowPhase(Event evt, ScriptAccessory sa) => sa.Method.SendChat($"/e Current Phase: {_phase}");

    [ScriptMethod(
        name: "换p",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(45680|45709|46489|46490)$"],
        userControl: false)]
    public async void 换p(Event evt, ScriptAccessory sa)
    {
        var actionId = evt.ActionId();
        if (actionId == 45709) _phase = 4;
        else if (actionId == 46489) _phase = 5;
        else if (actionId == 46490) _phase = 6;
        else _phase++;

        超增压 = 分摊分散.None;
        sa.Method.TextInfo($"快跑, 前往第{_phase}平台", 5500, true);
    }

    [ScriptMethod(
        name: "Shared-Store-Stack",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:45664"],
        userControl: false)]
    public void SharedStoreStack(Event evt, ScriptAccessory sa)
    {
        超增压 = 分摊分散.Stack;
        // sa.Method.TextInfo($"P{_phase} 共享机制：分摊(已记录)", 2500, true);
        // 你也可以 /e 广播
        // sa.Method.SendChat($"/e P{_phase} share = STACK");
    }

    [ScriptMethod(
        name: "Shared-Store-Spread",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:45663"],
        userControl: false)]
    public void SharedStoreSpread(Event evt, ScriptAccessory sa)
    {
        超增压 = 分摊分散.Spread;
        if (TTSOpen)
            sa.Method.EdgeTTS("待会儿分散");
        // sa.Method.SendChat($"/e P{_phase} share = SPREAD");
    }

    [ScriptMethod(
        name: "雷光环",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:19000"])]
    public void P1LightningRingSpawn(Event evt, ScriptAccessory sa)
    {
        
        // sa.Method.SendChat("/e P1 机制：雷光环出现");
        var pos = evt.SourcePosition();

        // 平台1/2 只处理 Y:0.00
        if (_phase == 1 || _phase == 2 || _phase == 6)
        {
            if (MathF.Abs(pos.Y - 0.0f) > 0.01f) return;
        }


        // 画矩形危险区
        if (_phase == 1 || _phase == 6){
            var dp = sa.FastDp("雷光环矩形危险区", pos, 7000, new Vector2(5, 30), safe: false);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        else if (_phase == 2){
            var dp1 = sa.FastDp("雷光环矩形危险区-半透明", pos, 4000, new Vector2(5, 30), safe: false);
            dp1.Color = new Vector4(dp1.Color.X, dp1.Color.Y, dp1.Color.Z, 0.2f);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);

            var dp2 = sa.FastDp("雷光环矩形危险区-不透明", pos, 3000, new Vector2(5, 30), safe: false);
            dp2.Delay = 4000; // 延后 4 秒出现
            dp2.Color = new Vector4(dp2.Color.X, dp2.Color.Y, dp2.Color.Z, 1f);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
        }
    }

    [ScriptMethod(
        name: "超增压急行",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:45670"])]
    public async void 超增压急行(Event evt, ScriptAccessory sa)
    {
        // if (_phase != 1) return;
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"超增压急行";
        dp.Owner = sa.Data.Me;
        dp.FixRotation = true;
        dp.Rotation = 0f;
        dp.Color = new Vector4(1f, 1f, 0f, 1f); // 黄色
        // dp.Color = sa.Data.DefaultSafeColor;
        dp.DestoryAt = 7000;
        dp.Scale = new Vector2(0.7f, 15f);

        if (TTSOpen)
            sa.Method.EdgeTTS("击退");
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        await Task.Delay(5500);
        // TTS
        switch (超增压)
        {
            case 分摊分散.Stack:
                if (TTSOpen)
                    sa.Method.EdgeTTS("分摊");
                sa.Method.TextInfo("分摊", 4000, false);
                break;
            case 分摊分散.Spread:
                if (TTSOpen)
                    sa.Method.EdgeTTS("分散");
                sa.Method.TextInfo("分散", 4000, false);
                break;
            default:
                sa.Method.TextInfo("未记录分摊/分散！", 4000, true);
                break;
        }

        await Task.Delay(500);

        // 分散集合画图
        switch (超增压)
        {
            case 分摊分散.Stack:
                sa.DrawStackPairLines10s();
                break;
            case 分摊分散.Spread:
                sa.DrawSpreadCircles10s();
                break;
        }
    }

    [ScriptMethod(
        name: "超增压抽雾",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:45677"])]
    public async void 超增压抽雾(Event evt, ScriptAccessory sa)
    {
        // if (_phase != 1) return;
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"超增压抽雾";
        dp.Owner = sa.Data.Me;
        dp.FixRotation = true;
        dp.Rotation = MathF.PI; // 180 度
        dp.Color = new Vector4(1f, 1f, 0f, 1f); // 黄色
        // dp.Color = sa.Data.DefaultSafeColor;
        dp.DestoryAt = 7000;
        dp.Scale = new Vector2(0.7f, 15f);

        if (TTSOpen)
            sa.Method.EdgeTTS("吸引");
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        await Task.Delay(5500);
        // TTS
        switch (超增压)
        {
            case 分摊分散.Stack:
                if (TTSOpen)
                    sa.Method.EdgeTTS("分摊");
                sa.Method.TextInfo("分摊", 4000, false);
                break;
            case 分摊分散.Spread:
                if (TTSOpen)
                    sa.Method.EdgeTTS("分散");
                sa.Method.TextInfo("分散", 4000, false);
                break;
            default:
                sa.Method.TextInfo("未记录分摊/分散！", 4000, true);
                break;
        }

        await Task.Delay(500);

        // 分散集合画图
        switch (超增压)
        {
            case 分摊分散.Stack:
                sa.DrawStackPairLines10s();
                break;
            case 分摊分散.Spread:
                sa.DrawSpreadCircles10s();
                break;
        }
    }

    // 第二平台
    [ScriptMethod(
    name: "雷转质射线",
    eventType: EventTypeEnum.StartCasting,
    eventCondition: ["ActionId:regex:^(45681|45683)$"])]
    public void 雷转质射线(Event evt, ScriptAccessory sa)
    {
        var actionId = evt.ActionId();
        var length = 0f;
        if(_phase == 2){
            length = 30f;
        }
        else{
            length = actionId == 45683 ? 20f : 5f;
        }

        // if (actionId == 45681 && 最后平台飞箱 == true) {
        if (actionId == 45681) {
            length = 40f;
        }

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"雷转质射线-{actionId}-{evt.SourceId():X}";
        dp.Owner = evt.SourceId();
        dp.Color = sa.Data.DefaultDangerColor;
        dp.DestoryAt = 7000;
        dp.Scale = new Vector2(5, length);

        dp.FixRotation = false;

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 最后平台
    [ScriptMethod(
    name: "雷转质射线2",
    eventType: EventTypeEnum.StartCasting,
    eventCondition: ["ActionId:45682"])]
    public void 雷转质射线2(Event evt, ScriptAccessory sa)
    {
        if(_phase != 6) return;
        var actionId = evt.ActionId();

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"雷转质射线2-{actionId}-{evt.SourceId():X}";
        dp.Owner = evt.SourceId();
        dp.Color = sa.Data.DefaultDangerColor;
        dp.DestoryAt = 7000;
        dp.Scale = new Vector2(5, 10);

        dp.FixRotation = false;

        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        // 最后平台飞箱 = true;
    }

    private float Theta(Vector2 origin, Vector2 target) => MathF.Atan2(target.Y - origin.Y, target.X - origin.X);

    private float RelTheta(float baseTheta, float t)
    {
        var diff = t - baseTheta;
        while (diff < -MathF.PI) diff += 2 * MathF.PI;
        while (diff > MathF.PI) diff -= 2 * MathF.PI;
        return diff;
    }

    // 异世界
    [ScriptMethod(
        name: "以太晶球最大近战距离",
        eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:9020"])]
    public void P2AetherSphereSpawn(Event evt, ScriptAccessory sa)
    {
        // 不知道为什么每次会同时生成六个以太晶球...
        // sa.Method.RemoveDraw("以太晶球最大近战距离");

        // 检测有没有以太晶球最大近战距离,有就不画了
        // 笨办法，DrawList从哪获得啊
        if (以太晶球最大近战距离) return;
        以太晶球最大近战距离 = true;
        // sa.Method.SendChat("/e 以太晶球出现，画最大近战距离圈");

        var pos = evt.SourcePosition();
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"以太晶球最大近战距离";
        dp.Position = new Vector3(-400f, -900f, -400f);
        dp.Color = new Vector4(sa.Data.DefaultSafeColor.X, sa.Data.DefaultSafeColor.Y, sa.Data.DefaultSafeColor.Z, 0.01f);
        dp.DestoryAt = 100000;
        dp.Scale = new Vector2(5f); // 半径4米

        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(
        name: "异世界-双T&D3D4鸣笛指路",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:027F"]
    )]
    public async void OnHorn(Event evt, ScriptAccessory sa)
    {
        // sa.Method.SendChat("/e 异世界鸣笛 预测开始");
        // 扇形范围：只针对当前队伍的坦克
        var tankTargets = new List<uint>(sa.Data.PartyList.Count);
        foreach (var memberId in sa.Data.PartyList)
        {
            var obj = sa.Data.Objects.SearchById(memberId);
            if (obj is IBattleChara battleChara && battleChara.IsTank())
            {
                tankTargets.Add(memberId);
            }
        }

        foreach (var tankId in tankTargets)
        {
            var dp2 = sa.Data.GetDefaultDrawProperties();
            dp2.Name = $"HornCone_{tankId:X}_{evt.SourceId():X}";
            dp2.Owner = sa.Data.Objects.FirstOrDefault(x => x.DataId == TargetBNpcId).EntityId;
            dp2.TargetObject = tankId;
            dp2.Color = sa.Data.DefaultDangerColor;
            dp2.Radian = MathF.PI / 5.143f; // 22.5°
            dp2.Scale = new Vector2(60f);
            dp2.DestoryAt = 9000;

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp2);
        }
        
        // 前置
        var myIdx = sa.MyIndex();
        if (new List<int>() { 2, 3, 4, 5 }.Contains(myIdx)) return;

        // 1) 采样 7 个角度
        var thetas = new List<float>(SampleCount);
        
        Vector2? TryGetEntityPos2D(uint id)
        {
            var obj = sa.Data.Objects.FirstOrDefault(x => x.DataId == id);
            return obj == default ? null : new Vector2(obj.Position.X, obj.Position.Z);
        }

        var p0 = TryGetEntityPos2D(TargetBNpcId);
        if (p0 == null) return;

        float theta0 = Theta(Origin, p0.Value);
        thetas.Add(theta0);

        for (int i = 1; i < SampleCount; i++)
        {
            await Task.Delay(SampleIntervalMs);

            var p = TryGetEntityPos2D(TargetBNpcId);
            if (p == null) return;

            thetas.Add(Theta(Origin, p.Value));
        }

        // 2) relθ：相对初始角度，避免跨 π 跳变
        var rel = thetas.Select(t => RelTheta(theta0, t)).ToList();

        // 3) ω 拟合
        float omega = (
            -2f * rel[1]
            -1f * rel[2]
            +1f * rel[4]
            +2f * rel[5]
            +3f * rel[6]
        ) / 14f;

        // 4) θerr
        float avg = rel.Sum() / SampleCount;
        float thetaErr = avg - 1.5f * omega;

        // 5) θ停
        float thetaStop = theta0 + PredictStopT * omega + thetaErr;

        // 6) 画图
        float facingIn = thetaStop + MathF.PI;
        float offset  = 35f * MathF.PI / 180f;

        float leftDir  = facingIn + offset;
        float rightDir = facingIn - offset;
        Vector2 dirIn = new Vector2(MathF.Cos(facingIn), MathF.Sin(facingIn));

        static Vector2 Rotate(Vector2 v, float rad)
        {
            float c = MathF.Cos(rad);
            float s = MathF.Sin(rad);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        Vector2 local = new(25, 0);
        Vector2 stopPos = Origin + Rotate(local, thetaStop);
        // sa.Method.SendChat($"/e 停止位置: {stopPos.X:F2}, {stopPos.Y:F2}");

        Vector2 mtPoint  = stopPos + new Vector2(MathF.Cos(leftDir),  MathF.Sin(leftDir))  * 19f;
        Vector2 stPoint = stopPos + new Vector2(MathF.Cos(rightDir), MathF.Sin(rightDir)) * 19f;
        Vector2 d3Point = stopPos + dirIn * 11f;
        Vector2 d4Point = stopPos + dirIn * 39f;

        // 使用
        Vector3 ToWaypoint(Vector2 source) => new Vector3(source.X, -900, source.Y);

        var wpos = myIdx switch
        {
            0 => ToWaypoint(mtPoint),
            1 => ToWaypoint(stPoint),
            6 => ToWaypoint(d3Point),
            7 => ToWaypoint(d4Point),
            _ => Vector3.Zero
        };
        
        var dp = sa.WaypointDp(wpos, 5500);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(
        name: "异世界-分摊分散",
        eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:regex:^(027D|027E)$"]
    )]
    public async void p3StackSpread(Event evt, ScriptAccessory sa){
        var idStr = (evt["Id"] ?? "").Replace("0x", "", StringComparison.OrdinalIgnoreCase)
                                .ToUpperInvariant();

        const uint duration = 8000;

        // 分散
        if (idStr == "027E")
        {   
            if (TTSOpen)
                sa.Method.EdgeTTS("分散");
            sa.Method.TextInfo("分散", 4000, false);
            foreach (var pid in sa.Data.PartyList)
            {
                var obj = sa.Data.Objects.SearchById(pid);
                if (obj is IBattleChara bc && bc.IsTank())
                    continue; // 不给坦克画圈
                    
                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"P3-SpreadCircle-{pid:X}";
                dp.Owner = pid;
                dp.Color = sa.Data.DefaultDangerColor;
                dp.DestoryAt = duration;
                dp.Scale = new Vector2(5);

                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            return;
        }

        // 分摊
        if (idStr == "027D")
        {   
            if (TTSOpen)
                sa.Method.EdgeTTS("分摊");
            sa.Method.TextInfo("分摊", 4000, false);
            if (sa.Data.PartyList.Count < 4) return;

            var h1 = sa.Data.PartyList[2];
            var h2 = sa.Data.PartyList[3];
            var d1 = sa.Data.PartyList[4];
            var d2 = sa.Data.PartyList[5];

            void DrawYellowCircle(uint pid)
            {
                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"P3-StackCircle-{pid:X}";
                dp.Owner = pid;
                dp.Color = new Vector4(1f, 1f, 0f, 0.1f);
                dp.DestoryAt = duration;
                dp.Scale = new Vector2(5);

                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
            }

            DrawYellowCircle(h1);
            DrawYellowCircle(h2);

            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"P2-StackLine-h1";
            dp.Owner = h1;                 // 线起点跟随 from
            dp.TargetObject = d1;            // 线终点跟随 to
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Scale = new Vector2(5);       // 线宽
            dp.Color = sa.Data.DefaultDangerColor;
            dp.DestoryAt = duration;

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);

            var dp2 = sa.Data.GetDefaultDrawProperties();
            dp2.Name = $"P2-StackLine-h2";
            dp2.Owner = h2;                 // 线起点跟随 from
            dp2.TargetObject = d2;            // 线终点跟随 to
            dp2.ScaleMode = ScaleMode.YByDistance;
            dp2.Scale = new Vector2(5);       // 线宽
            dp2.Color = sa.Data.DefaultDangerColor;
            dp2.DestoryAt = duration;

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp2);
        }

    }

    [ScriptMethod(
        name: "雷光一闪(拍拍手)",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:45666"]
    )]
    public void OnClap(Event evt, ScriptAccessory sa)
    {
        var pos = evt.EffectPosition();
        var dp = sa.FastDp("雷光一闪", pos, 3500, new Vector2(4), safe: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(
        name: "前照光",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(45690|45687)$"]
    )]
    public async void OnHeadlight(Event evt, ScriptAccessory sa)
    {
        var actionId = evt.ActionId();
        // 先打上后打下
        if (actionId == 45690){
            // sa.Method.SendChat($"/e phase: {_phase}");
            if (TTSOpen)
                sa.Method.EdgeTTS("留在下面");
            sa.Method.TextInfo("留在下面", 3000, false);

            if (_phase == 4){
                var dp1 = sa.FastDp("前照光矩形危险区-上面1", new Vector3(107.5f, 5f, 245f), 6500, new Vector2(5, 10), safe: false);
                var dp2 = sa.FastDp("前照光矩形危险区-上面2", new Vector3(92.5f, 5f, 250f), 6500, new Vector2(5, 15), safe: false);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
            }
            else
            {
                var dp1 = sa.FastDp("前照光矩形危险区-上面1", new Vector3(92.5f, 5f, 345f), 6500, new Vector2(5, 20), safe: false);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);
            }

            await Task.Delay(6500);
            if (TTSOpen)
                sa.Method.EdgeTTS("上去");
            sa.Method.TextInfo("上去", 3000, false);

            if (_phase == 4){
                var dp3 = sa.FastDp("前照光矩形危险区-下面1", new Vector3(97.5f, 0f, 235f), 3000, new Vector2(15, 15), safe: false);
                var dp5 = sa.FastDp("前照光矩形危险区-下面3", new Vector3(107.5f, 0f, 235f), 3000, new Vector2(5, 10), safe: false);
                var dp6 = sa.FastDp("前照光矩形危险区-下面4", new Vector3(102.5f, 0f, 255f), 3000, new Vector2(15, 10), safe: false);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp3);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp5);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp6);
            }
            else{
                var dp3 = sa.FastDp("前照光矩形危险区-下面1", new Vector3(92.5f, 0f, 335f), 3000, new Vector2(5, 10), safe: false);
                var dp4 = sa.FastDp("前照光矩形危险区-下面4", new Vector3(102.5f, 0f, 335f), 3000, new Vector2(15, 30), safe: false);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp3);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp4);
            }
        }
        else{
            // sa.Method.SendChat($"/e phase: {_phase}");
            if (TTSOpen)
                sa.Method.EdgeTTS("上去");
            sa.Method.TextInfo("上去", 3000, false);

            if (_phase == 4){
                var dp1 = sa.FastDp("前照光矩形危险区-下面1", new Vector3(97.5f, 0f, 235f), 6500, new Vector2(15, 15), safe: false);
                var dp5 = sa.FastDp("前照光矩形危险区-下面3", new Vector3(107.5f, 0f, 235f), 6500, new Vector2(5, 10), safe: false);
                var dp6 = sa.FastDp("前照光矩形危险区-下面4", new Vector3(102.5f, 0f, 255f), 6500, new Vector2(15, 10), safe: false);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp5);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp6);
            }
            else{
                var dp1 = sa.FastDp("前照光矩形危险区-下面1", new Vector3(92.5f, 0f, 335f), 6500, new Vector2(5, 10), safe: false);
                var dp2 = sa.FastDp("前照光矩形危险区-下面4", new Vector3(102.5f, 0f, 335f), 6500, new Vector2(15, 30), safe: false);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
            }

            await Task.Delay(6500);
            if (TTSOpen)
                sa.Method.EdgeTTS("下去");
            sa.Method.TextInfo("下去", 3000, false);

            if (_phase == 4){
                var dp2 = sa.FastDp("前照光矩形危险区-上面1", new Vector3(107.5f, 5f, 245f), 3000, new Vector2(5, 10), safe: false);
                var dp4 = sa.FastDp("前照光矩形危险区-上面2", new Vector3(92.5f, 5f, 250f), 3000, new Vector2(5, 15), safe: false);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp4);
            }
            else{
                var dp2 = sa.FastDp("前照光矩形危险区-上面1", new Vector3(92.5f, 5f, 345f), 3000, new Vector2(5, 20), safe: false);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
            }
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
    private static readonly string[] DefaultRoleByIndex =
    [
        "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"
    ];

    public static int MyIndex(this ScriptAccessory sa)
    {
        return sa.Data.PartyList.IndexOf(sa.Data.Me);
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

    public static void DrawSpreadCircles10s(this ScriptAccessory sa)
    {
        // sa.Method.RemoveDraw("P1-SpreadCircle.*");

        foreach (var pid in sa.Data.PartyList)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"P1-SpreadCircle-{pid:X}";
            dp.Owner = pid;
            dp.Color = sa.Data.DefaultDangerColor;
            dp.DestoryAt = 6000;
            dp.Scale = new Vector2(5);

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    public static void DrawStackPairLines10s(this ScriptAccessory sa)
    {
        // sa.Method.RemoveDraw("P1-Stack.*");

        void DrawLine(uint from, uint to, string tag)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"P1-StackLine-{from:X}";
            dp.Owner = from;                 // 线起点跟随 from
            dp.TargetObject = to;            // 线终点跟随 to
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Scale = new Vector2(5);       // 线宽
            dp.Color = sa.Data.DefaultDangerColor;
            dp.DestoryAt = 6000;

            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
        }
        void DrawCircle(uint ply, string tag)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"P1-StackCircle-{ply:X}";
            dp.Owner = ply;
            dp.Color = new Vector4(1f, 1f, 0f, 0.1f); // 黄色
            dp.DestoryAt = 6000;
            dp.Scale = new Vector2(5);

            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }

        // 防止队伍不足 8 人时崩
        if (sa.Data.PartyList.Count < 8) return;

        var mt = sa.Data.PartyList[0];
        var st = sa.Data.PartyList[1];
        var h1 = sa.Data.PartyList[2];
        var h2 = sa.Data.PartyList[3];
        var d1 = sa.Data.PartyList[4];
        var d2 = sa.Data.PartyList[5];
        var d3 = sa.Data.PartyList[6];
        var d4 = sa.Data.PartyList[7];

        DrawLine(mt, d1, "MT-D1");
        DrawLine(st, d2, "ST-D2");
        DrawLine(d3, h1, "D3-H1");
        DrawLine(d4, h2, "D4-H2");

        DrawCircle(mt, "MT");
        DrawCircle(st, "ST");
        DrawCircle(h1, "H1");
        DrawCircle(h2, "H2");
    }
}

#endregion
