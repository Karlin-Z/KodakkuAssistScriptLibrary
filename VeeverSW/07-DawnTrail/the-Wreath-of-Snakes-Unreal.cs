using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.GameEvent.Types;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Script;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Veever.DawnTrail.the_Wreath_of_Snakes_Unreal;

[ScriptType(name: Name, territorys: [825, 1302], guid: "3a915832-971d-4c27-b802-407a1e30ae53",
    version: Version, author: "Veever & Usami", note: NoteStr, updateInfo: UpdateStr)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$
// ^\[\w+\|[^|]+\|E\]\s\w+

public class UnrealSeiryu
{
    const string NoteStr =
    $"""
    v{Version}
    1. 如果需要某个机制的绘画或者哪里出了问题请在dc@我或者私信我
    2. 刀禁咒分摊强制绑定画双奶（双奶死了的话就听天由命了）
    3. 塔站位绘图按照如下方式
          MT / D1    ST / D2
          H1 / D3    H2 / D4
    4. 极神机制还未彻底完成（缺少技能id（其实是懒得去，有朝一日会加的））
    5. 鲶鱼精标点为基础北东南西ABCD标点
    鸭门
    ----------------------------------
    1. If you need a draw or notice any issues, @ me on DC or DM me.
    2. For the Forbidden Arts Stack, both healers are hard-locked draw
       (if both healers die, it’s GG / pray to Hydaelyn xd).
    3. Tower positions are as follows:
          MT / D1    ST / D2
          H1 / D3    H2 / D4
    4. EX trial draw are not fully implemented yet (missing skill IDs — honestly just lazy, 
       will add them one day).
    5. PostNamazu markers default to standard N/E/S/W (ABCD).
    Duckmen.
    """;

    const string UpdateStr =
    $"""
    v{Version}
    修复了TTS
    鸭门
    ----------------------------------
    Fixed TTS
    Duckmen.
    """;

    private const string Name = "LV.100 青龙诗魂-幻巧战 [the Wreath of Snakes Ex-Unreal]";
    private const string Version = "0.0.0.2";
    private const string DebugVersion = "a";

    private const bool Debugging = false;

    private static readonly List<string> Role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
    private static readonly Vector3 Center = new Vector3(100, 0, 100);
    
    // TODO 根据第一个AOE的读条调整
    private static uint BossDataId = 9708;

    [UserSetting("播报语言(language)")]
    public Language language { get; set; } = Language.Chinese;

    [UserSetting("绘图不透明度，数值越大越显眼(Draw opacity — higher value = more visible)")]
    public static float ColorAlpha { get; set; } = 1f;

    [UserSetting("文字横幅提示开关(Banner text toggle)")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关(TTS toggle)")]
    public bool isTTS { get; set; } = true;

    [UserSetting("是否自动使用防击退(Auto anti-knockback)")]
    public bool useaction { get; set; } = true;

    [UserSetting("指路开关(Guide arrow toggle)")]
    public bool isLead { get; set; } = true;

    [UserSetting("目标标记开关(Target Marker toggle)")]
    public bool isMark { get; set; } = true;

    [UserSetting("本地目标标记开关(打开则为本地开关，关闭则为小队) - Local target marker toggle (ON = local only, OFF = party shared)")]
    public bool LocalMark { get; set; } = true;

    [UserSetting("是否进行场地标点引导(Waymark guide toggle)")]
    public bool PostNamazuPrint { get; set; } = true;

    [UserSetting("鲶鱼精邮差端口设置(PostNamazuPort Setting)")]
    public int PostNamazuPort { get; set; } = 2019;

    [UserSetting("场地标点是否为本地标点(如果选择非本地标点，脚本只会在非战斗状态下进行标点) - Waymarks: local toggle(off = party shared, OOC only)")]
    public bool PostNamazuisLocal { get; set; } = true;

    [UserSetting("Debug开关, 非开发用请关闭 - Debug on/off (don't touch unless you know what you're doing)")]
    public bool isDebug { get; set; } = false;

    public enum Language
    {
        Chinese,
        English
    }

    private volatile List<bool> _bools = new bool[20].ToList();      // 被记录flag
    private List<int> _numbers = Enumerable.Repeat(0, 8).ToList();
    private static List<ManualResetEvent> _events = Enumerable
        .Range(0, 20)
        .Select(_ => new ManualResetEvent(false))
        .ToList();
    
    private Dictionary<ulong, ulong> _tethersDict = new();
    private static bool _initHint = false;
    public int KanaboTTSCount;

    private enum SeiryuPhase
    {
        Init,
        P2A_Mobs,           // P2A 小怪
        P3A_RainStorm,      // P3A 暴雨
        P3B_BrazenSoul,     // P3B 荒魂
    }
    
    private static SeiryuPhase _seiryuPhase = SeiryuPhase.Init;

    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    public void Init(ScriptAccessory sa)
    {
        _seiryuPhase = SeiryuPhase.Init;
        RefreshParams();
        sa.Log.Debug($"脚本 {Name} v{Version}{DebugVersion} 完成初始化.");
        sa.Method.RemoveDraw(".*");
        if (PostNamazuPrint) PostWaymark(sa);
        KanaboTTSCount = 0;
        _initHint = false;
    }
    
    private void RefreshParams()
    {
        _bools = new bool[20].ToList();
        _numbers = Enumerable.Repeat(0, 20).ToList();
        _events = Enumerable
            .Range(0, 20)
            .Select(_ => new ManualResetEvent(false))
            .ToList();
        _tethersDict = new Dictionary<ulong, ulong>();
    }



    #region Waymark
    private static readonly Vector3 posA = new Vector3(100.00f, -0.00f, 80.86f);
    private static readonly Vector3 posB = new Vector3(119.28f, -0.00f, 100.00f);
    private static readonly Vector3 posC = new Vector3(100.00f, -0.00f, 119.03f); 
    private static readonly Vector3 posD = new Vector3(80.51f, -0.00f, 100.00f);


    public void PostWaymark(ScriptAccessory accessory)
    {
        var waymark = new NamazuHelper.Waymark(accessory);
        waymark.AddWaymarkType("A", posA);
        waymark.AddWaymarkType("B", posB);
        waymark.AddWaymarkType("C", posC);
        waymark.AddWaymarkType("D", posD);

        waymark.SetJsonPayload(LocalMark, PostNamazuisLocal);
        waymark.PostWaymarkCommand(PostNamazuPort);
    }
    #endregion

    [ScriptMethod(name: "主动进行场地标点 - Place waymarks", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdk"],
    userControl: true)]
    public void userNamazuPost(Event ev, ScriptAccessory sa)
    {
        PostWaymark(sa);
    }


    [ScriptMethod(name: "---- 时间轴记录 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void SplitLine_Timeline(Event ev, ScriptAccessory sa)
    {
        // DataId
        // 9708 青龙
        // 9710 红之式鬼
        // 9711 苍之式鬼
        // 9712 岩之式鬼
        // 10103 天之式鬼
        // 9714 泥之式鬼（小）
        // 9713 沼之式鬼（大）
        // 9715 山之式鬼
        
        // SkillId
        // - 14275 AOE 阴阳五行
        // - 14301 圆形点名 降蛇
        // - 14849 钢铁 阴阳之印
        // - 14305 读条 九子切
        // - 14306 九子切直线 阳之刀印
        // - 14290 咒怨的替身
        // - 14292 死刑 虚证弹
        // - 14291 诅咒返还
        
        // - 14286 召唤小怪 式鬼召唤
        // - 14321 赤突进
        // - 14320 青突进
        // - 14317 大钢铁 百吨回转
        // - 15393 直线 山神乐
        // - 14356 接线扇形 如虎添翼
        // 14324 需插言打断 石肤
        // - 14322 爆炸（沼）
        // - 14323 爆炸（泥）
        // - 14281 转场 灵气
        // - 14282 玩家眩晕时 云蒸龙变 ActionEffect
        // - 14283 玩家眩晕时 云蒸龙变 23s
        
        // - 14328 荒波（扇形） 4s
        // - 14329 荒波（月环） 3s
        // - 14330 荒波（月环） 3s
        // - 14331 荒波（月环） 3s
        // - 14288 召唤小怪 式鬼召唤
        // - 14326 蛇崩 7.5s 这个技能让玩家受伤
        // - 14327 蛇崩 7.5s 使用这个技能的在场边
        // - 14325 蛇崩 5s
        // - 14626 蛇崩 5s
        // - 14627 蛇崩 5s
        // - 14287 召唤小怪 式鬼召唤
        
        // - 14312 压杀掌 读条
        // - 14311 压杀掌 读条
        // - 14309 压杀掌 ActionEffect 右手
        // - 14310 压杀掌 ActionEffect 左手
        // - 14315 大压杀
        // - 14314 大压杀
        // - 14313 大压杀 ActionEffect
        
        // 14277 刀禁咒 分摊
        // 14279 刀禁咒 伤害 #1
        // 14280 刀禁咒 伤害 #2
        
        // 15394 刀禁咒 伤害（荒魂） #1
        // 15395 刀禁咒 伤害（荒魂） #2
        
        // 14308 荒魂燃烧
        // - 14851 钢铁（荒魂） 阴阳之印（先发）
        // - 14852 月环（荒魂） 蛇眼之印（后发）
        // - 14853 月环（荒魂） 蛇眼之印（先发）
        // - 14854 钢铁（荒魂） 阴阳之印（后发）
        // - 14307 九子切直线 阴之刀印
        
        // - 15397 升龙
        // - 14302 原地点名黄圈 降蛇
        // - MorelogCompat EventObj 出塔
        
        // - 14285 召唤小怪 式鬼召唤
        
        // StatusId
        // 1725 咒怨的替身 Buff
        // 1696 淹没 无法移动与使用技能
        // 1054 受伤加重
        
        // TetherId
        // 0039 苍之式鬼 分摊
        // 0011 红之式鬼 连线击退
        // 0054 岩之式鬼 接线扇形
    }

    [ScriptMethod(name: "---- 测试项 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 测试项(Event ev, ScriptAccessory sa)
    {
        var bossId = GetBossObject(sa).GameObjectId;
        var dp = sa.DrawCircle(bossId, 0, 3000, $"钢铁", 12f, byTime: true, draw: false);
        dp.Color = sa.Data.DefaultDangerColor.WithW(ColorAlpha);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        
        sa.ScaleModify(GetBossObject(sa), 2f);
    }
    
    [ScriptMethod(name: "策略与身份提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(14275|43962)$"], 
        userControl: true)]
    public void MethodRoleTips(Event ev, ScriptAccessory sa)
    {
        KanaboTTSCount = 0;
        if (_initHint) return;
        _initHint = true;
        
        // 根据第一个AOE的读条，确定是极青龙还是幻青龙
        var aid = ev.ActionId;
        BossDataId = aid == 43962u ? 18643u : 9708u;
        
        var myIndex = sa.Data.PartyList.IndexOf(sa.Data.Me); 
        List<string> role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
        sa.Method.TextInfo(
            $"你是【{role[myIndex]}】，" +
            $"若有误请及时调整。", 5000);
    }

    [ScriptMethod(name: "AOE提示-AOE warnings", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(43962)$"],
        userControl: true)]
    public void AOENotify(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "AOE" : "AOE";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 3700, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "九字切绘图 - Kuji-kiri Draw", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4399[3])$"],
        userControl: true)]
    public void bladeSigil(Event ev, ScriptAccessory sa)
    {
        // 1.57 -> 东西
        // 0 -> 南北
        Vector3 TargetPos0 = ev.TargetPosition;
        Vector3 TargetPos1 = ev.TargetPosition;
        DebugMsg($"TargetPos0: {TargetPos0}, TargetPos1: {TargetPos1}, rotation: {ev.SourceRotation}", sa);
        //DrawHelper.DrawRect(sa, ev.SourcePosition, )
        if (ev.SourceRotation == 1.57f)
        {
            DebugMsg($"X change", sa);
            TargetPos0.X += 30f;
            TargetPos1.X -= 30f;
        }
        else if (ev.SourceRotation == 0f)
        {
            DebugMsg($"Z change", sa);
            TargetPos0.Z += 30f; 
            TargetPos1.Z -= 30f;
        }

        DrawHelper.DrawRect(sa, ev.SourcePosition, TargetPos0, new Vector2(4f, 50f), 3000, $"bladeSigil0-{ev.SourceId}", color: new Vector4(0, 1, 1, ColorAlpha));
        DrawHelper.DrawRect(sa, ev.SourcePosition, TargetPos1, new Vector2(4f, 50f), 3000, $"bladeSigil1-{ev.SourceId}", color: new Vector4(0, 1, 1, ColorAlpha));
    }

    [ScriptMethod(name: "本体：钢铁月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(148(49|51|53)|4402[468])$"], 
        userControl: true)]
    public void 钢铁月环(Event ev, ScriptAccessory sa)
    {
        // OnmyoSigil
        var aid = ev.ActionId;
        var sid = ev.SourceId;
        sa.Log.Debug($"检测到 钢铁月环 {aid}，执行绘图");

        DrawPropertiesEdit? dp, dp2;
        
        switch (aid)
        {
            case 14849 or 44024:
                dp = sa.DrawCircle(sid, 0, 3000, $"钢铁", 12f, draw: false);
                dp.Color = sa.Data.DefaultDangerColor.WithW(ColorAlpha);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                string msg = language == Language.Chinese ? "钢铁远离" : "Chariot - Stay away from boss";
                if (isText) sa.Method.TextInfo($"{msg}", duration: 2700, true);
                if (isTTS) sa.Method.TTS($"{msg}");

                break;
            
            case 14853 or 44028:
                string msg1 = language == Language.Chinese ? "先月环后钢铁" : "Dynamo → Chariot";
                if (isText) sa.Method.TextInfo($"{msg1}", duration: 2700, true);
                if (isTTS) sa.Method.TTS($"{msg1}");
                
                dp = sa.DrawDonut(sid, 0, 3000, $"月环", 30f, 7f, draw: false);
                dp.Color = sa.Data.DefaultDangerColor.WithW(ColorAlpha);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                
                dp2 = sa.DrawCircle(sid, 3000, 3000, $"钢铁", 12f, draw: false);
                dp2.Color = sa.Data.DefaultDangerColor.WithW(ColorAlpha);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
                break;
            
            case 14851 or 44026:
                string msg2 = language == Language.Chinese ? "先钢铁后月环" : "Chariot → Dynamo";
                if (isText) sa.Method.TextInfo($"{msg2}", duration: 2700, true);
                if (isTTS) sa.Method.TTS($"{msg2}");

                dp = sa.DrawCircle(sid, 0, 3000, $"钢铁", 12f, draw: false);
                dp.Color = sa.Data.DefaultDangerColor.WithW(ColorAlpha);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                
                dp2 = sa.DrawDonut(sid, 3000, 3000, $"月环", 30f, 7f, draw: false);
                dp2.Color = sa.Data.DefaultDangerColor.WithW(ColorAlpha);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
                break;
                
            default:
                break;
        }
    }
    
    [ScriptMethod(name: "本体：咒怨的替身 + 死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(14290|43977)$"],
        userControl: true)]
    public async void 咒怨的替身与死刑(Event ev, ScriptAccessory sa)
    {
        var myIndex = sa.Data.PartyList.IndexOf(sa.Data.Me);
        // InfirmSoul
        var aid = ev.ActionId;
        sa.Log.Debug($"检测到 咒怨的替身 {aid}，执行绘图");
        var sid = ev.SourceId;
        var tid = ev.TargetId;

        // 对当前一仇绘图，表示接下来的死刑目标
        var dp = sa.DrawCircle(sid, 0, 10000, $"死刑目标", 4f, byTime: false, draw: false);
        dp.SetOwnersEnmityOrder(1);
        dp.Color = sa.Data.DefaultDangerColor.WithW(ColorAlpha);
        
        // 对当前施法目标绘图，表示Debuff目标，不可吃死刑
        var dp0 = sa.DrawCircle(tid, 0, 10000, $"Debuff目标", 4f, byTime: false, draw: false);
        dp0.Color = new Vector4(0f, 0f, 1f, ColorAlpha);
        
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);

        if (myIndex == 0 || myIndex == 1)
        {
            string msg = language == Language.Chinese ? "换T" : "Tank swap";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 3000, true);
            if (isTTS) sa.Method.TTS($"{msg}");

            await Task.Delay(13000);
            string msg1 = language == Language.Chinese ? "换T" : "Tank swap";
            if (isText) sa.Method.TextInfo($"{msg1}", duration: 3000);
            if (isTTS) sa.Method.TTS($"{msg1}");
        }
    }
    
    [ScriptMethod(name: "本体：咒怨的替身 + 死刑（绘图删除）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(14292|43979)$"],
        userControl: Debugging)]
    public void 咒怨的替身与死刑_绘图删除(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"死刑目标");
        sa.Method.RemoveDraw($"Debuff目标");
    }
    
    [ScriptMethod(name: "--- 式鬼召唤一 阶段转换 ---", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(14286|43973)$"],
        userControl: Debugging)]
    public void 式鬼召唤一_阶段转换(Event ev, ScriptAccessory sa)
    {
        // SummonShiki
        sa.Log.Debug($"检测到 式鬼召唤一 {ev.ActionId}。");
        _seiryuPhase = SeiryuPhase.P2A_Mobs;
        RefreshParams();
        sa.Log.Debug($"阶段转换为：{_seiryuPhase}");
    }

    [ScriptMethod(name: "红轮：赤突进", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0011)$"],
        userControl: true)]
    public void 赤突进(Event ev, ScriptAccessory sa)
    {
        // RedRush
        sa.Log.Debug($"检测到 赤突进 连线 {ev.Id0()}，连线目标 {ev.SourceId}, {ev.TargetId}。");

        // 分出玩家与轮轮
        var playerId = sa.Data.PartyList.Contains((uint)ev.SourceId) ? ev.SourceId : ev.TargetId;
        var bossId = playerId == ev.SourceId ? ev.TargetId : ev.SourceId;
        
        var dp = sa.DrawRect(bossId, playerId, 0, 10000, $"赤突进",
            0, 5f, 10, false, false, true, false);
        dp.Color = sa.Data.DefaultDangerColor.WithW(ColorAlpha);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        lock (_numbers)
        {
            // 自身为目标时出现击退
            if (ev.TargetId == sa.Data.Me)
            {
                _bools[0] = true;
                string msg = language == Language.Chinese ? "两侧引导，不要冲击人群" : "Go to flanks, avoid cleaving";
                if (isText) sa.Method.TextInfo($"{msg}", duration: 3000, true);
                if (isTTS) sa.Method.TTS($"{msg}");

                if (useaction) sa.Method.UseAction(sa.Data.Me, 7559);
                if (useaction) sa.Method.UseAction(sa.Data.Me, 7548);
            }
                
        
            _numbers[0]++;
            sa.Log.Debug($"已检测 赤突进 #{_numbers[0]}");

            if (_numbers[0] == 2)
            {
                _events[0].Set();
                sa.Log.Debug($"赤突进记录成功，释放锁");
            }
        }
        
        if (ev.TargetId != sa.Data.Me) return;
        var dp0 = sa.DrawKnockBack(bossId, 0, 10000, $"赤突进击退",
            3f, 15, false, false);
        dp0.Color = new Vector4(0f, 1f, 1f, ColorAlpha);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp0);
    }
    
    [ScriptMethod(name: "蓝轮：青突进", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0039)$"],
        userControl: true)]
    public void 青突进(Event ev, ScriptAccessory sa)
    {
        // BlueBolt
        sa.Log.Debug($"检测到 青突进 连线 {ev.Id0()}，连线目标 {ev.SourceId}, {ev.TargetId}。");
        _events[0].WaitOne();
        sa.Log.Debug($"青突进解锁成功，玩家{(_bools[0] ? "避开分摊" : "参与分摊")}");
        
        // 分出玩家与轮轮
        var playerId = sa.Data.PartyList.Contains((uint)ev.SourceId) ? ev.SourceId : ev.TargetId;
        var bossId = playerId == ev.SourceId ? ev.TargetId : ev.SourceId;
        
        var dp = sa.DrawRect(bossId, playerId, 0, 6000, $"青突进",
            0, 5f, 40, false, false, false, false);
        dp.Color = _bools[0]
            ? sa.Data.DefaultDangerColor.WithW(ColorAlpha)
            : sa.Data.DefaultSafeColor.WithW(ColorAlpha);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        _events[0].Reset();
    }


    [ScriptMethod(name: "式鬼-百吨回转", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44004)$"],
    userControl: true)]
    public void tonzeSwing(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircleObject(sa, ev.SourceId, new Vector2(16f), 3700, "100-tonzeSwing");
    }

    [ScriptMethod(name: "赤突进、青突进（绘图删除）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(1432[01]|4400[78])$"],
        userControl: Debugging)]
    public void 青突进绘图删除(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"赤突进.*");
        sa.Method.RemoveDraw($"青突进.*");
    }
    
    [ScriptMethod(name: "石头人：如虎添翼", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0054)$"],
        userControl: true)]
    public void Kanabo(Event ev, ScriptAccessory sa)
    {
        // Kanabo
        sa.Log.Debug($"检测到 如虎添翼 连线 {ev.Id0()}，连线目标 {ev.SourceId}, {ev.TargetId}。");
        var bossId = ev.SourceId;
        var playerId = ev.TargetId;
        var myIndex = sa.Data.PartyList.IndexOf(sa.Data.Me);

        if (myIndex == 0 && KanaboTTSCount == 0)
        {
            string msg = language == Language.Chinese ? "接场地西侧的线，并引导至场外" : "Take WEST line, drag out";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 3000, true);
            if (isTTS) sa.Method.TTS($"{msg}");
            KanaboTTSCount++;
        }

        if (myIndex == 1 && KanaboTTSCount == 0)
        {
            string msg = language == Language.Chinese ? "接场地东侧的线，并引导至场外" : "Take EAST line, drag out";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 3000, true);
            if (isTTS) sa.Method.TTS($"{msg}");
            KanaboTTSCount++;
        }


        var oldTargetId = _tethersDict.GetValueOrDefault(bossId, 0u);
        sa.Log.Debug($"{bossId}的前一任目标为{oldTargetId}。");
        if (oldTargetId == playerId) return;
        
        sa.Method.RemoveDraw($"如虎添翼{bossId}_{oldTargetId}");
        _tethersDict[bossId] = playerId;
        
        var dp = sa.DrawFan(bossId, playerId, 0, 10000, $"如虎添翼{bossId}_{playerId}",
            60f.DegToRad(), 0, 40f, 0f, draw: false);
        dp.Color = sa.Data.DefaultDangerColor.WithW(ColorAlpha);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "石头人：如虎添翼（绘图删除）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(14318|44005)$"],
        userControl: Debugging)]
    public void 如虎添翼绘图删除(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"如虎添翼.*");
    }
    
    [ScriptMethod(name: "泥巴人：石肤提示与本地标点", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(14324|44011)$"],
        userControl: true)]
    public void 石肤(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"检测到 {ev.SourceId} 正在释放 石肤 {ev.ActionId}。");
        var sid = ev.SourceId;
        var myIndex = sa.Data.PartyList.IndexOf(sa.Data.Me);
        var dp = sa.DrawCircle(sid, 0, 5000, $"石肤{sid}", 3f, draw: false);
        dp.Color = new Vector4(1f, 0f, 0f, ColorAlpha);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (myIndex == 0)
        {
            string msg = language == Language.Chinese ? "沉默打断被标记式鬼" : "Silence the marked mob!";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 3000, true);
            if (isTTS) sa.Method.TTS($"{msg}");
        }

        if (isMark) sa.Method.Mark((uint)sid, MarkType.Attack1, LocalMark);
    }
    
    [ScriptMethod(name: "泥巴人：石肤，技能打断或释放成功后恢复", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(14324|44011)$"],
        userControl: Debugging)]
    public void 石肤恢复1(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"检测到 {ev.SourceId} 释放完毕了 石肤 {ev.ActionId}。");
        var sid = ev.SourceId;
        sa.MarkClear(local: true);
        sa.Method.RemoveDraw($"石肤{sid}");
    }
    
    [ScriptMethod(name: "泥巴人：石肤，技能打断或释放成功后恢复", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:regex:^(14324|44011)$"],
        userControl: Debugging)]
    public void 石肤恢复2(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"检测到 {ev.SourceId} 释放完毕了 石肤 {ev.ActionId}。");
        var sid = ev.SourceId;
        sa.MarkClear(local: true);
        sa.Method.RemoveDraw($"石肤{sid}");
    }
    
    [ScriptMethod(name: "--- 灵气 阶段转换 ---", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(14281|43968)$"],
        userControl: Debugging)]
    public void 灵气_阶段转换(Event ev, ScriptAccessory sa)
    {
        // SummonShiki
        sa.Log.Debug($"检测到 灵气 {ev.ActionId}。");
        _seiryuPhase = SeiryuPhase.P3A_RainStorm;
        RefreshParams();
        sa.Log.Debug($"阶段转换为：{_seiryuPhase}");
    }
    
    [ScriptMethod(name: "后半：蛇崩击退", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(14327|44014)$"],
        userControl: true)]
    public void 蛇崩击退(Event ev, ScriptAccessory sa)
    {
        // CoursingRiver
        sa.Log.Debug($"检测到 蛇崩 {ev.ActionId}，释放位置 {ev.EffectPosition}");

        var dir = ev.EffectPosition.GetRadian(Center).RadianToRegion(4, isDiagDiv: true);
        // 1则蛇崩在右，向左击退；3则蛇崩在左，向右击退
        sa.Log.Debug($"蛇崩释放方位 {dir} （1右3左）");
        
        var dp = sa.DrawRect(sa.Data.Me, 0, 10000, $"蛇崩", 
            -dir * float.Pi/2, 5f, 25f, draw: false);
        dp.FixRotation = true;
        dp.Color = new Vector4(0f, 1f, 1f, ColorAlpha);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        var knockBackPos = new Vector3(100, 0, 107).RotateAndExtend(Center, dir * float.Pi / 2, 0);
        sa.Log.Debug($"蛇崩击退位置 {knockBackPos}");
        var dp0 = sa.DrawGuidance(knockBackPos, 0, 10000, $"蛇崩指路", draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
    }
    
    [ScriptMethod(name: "后半：蛇崩击退（绘图删除）", eventType: EventTypeEnum.KnockBack, eventCondition: ["Distance:regex:^(25.00)$"],
        userControl: Debugging)]
    public void 蛇崩击退删除(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"捕捉到 25.00 的击退距离。");
        sa.Method.RemoveDraw($"蛇崩.*");
    }
    

    [ScriptMethod(name: "山鬼：压杀掌", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(143(09|10)|43999)$"],
        userControl: true)]
    public void 压杀掌(Event ev, ScriptAccessory sa)
    {

        sa.Log.Debug($"检测到 压杀掌 {ev.ActionId}。");
        var aid = ev.ActionId;

        //var dp = sa.DrawFan(Center, 0, 4200, $"压杀掌{aid}",
        //    180f.DegToRad(), ev.SourceRotation, 20f, 0f, draw: false);

        //dp.Color = sa.Data.DefaultDangerColor.WithW(ColorAlpha); 
        //sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"压杀掌{aid}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Owner = ev.SourceId;
        dp.Scale = new Vector2(20f);
        dp.Radian = 180 * (float.Pi / 180);
        dp.DestoryAt = 4200;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    // TODO: which one?
    [ScriptMethod(name: "山鬼：压杀掌（绘图删除）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(14312)$"],
        userControl: Debugging)]
    public void 压杀掌绘图删除(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"压杀掌.*");
    }
    
    [ScriptMethod(name: "--- 荒魂 阶段转换 ---", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(14308|43995)$"],
        userControl: Debugging)]
    public void 荒魂_阶段转换(Event ev, ScriptAccessory sa)
    {
        // Blazing Aramitama
        sa.Log.Debug($"检测到 荒魂燃烧 {ev.ActionId}。");
        _seiryuPhase = SeiryuPhase.P3B_BrazenSoul;
        RefreshParams();
        sa.Log.Debug($"阶段转换为：{_seiryuPhase}");
    }
    
    [ScriptMethod(name: "荒魂：升龙集合提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(15397|44034)$"],
        userControl: true)]
    public void 升龙集合提示(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"检测到 升龙 {ev.ActionId}。");
        Vector3 midpos = new Vector3(100, 0, 100);
        var dp = sa.DrawGuidance(midpos, 0, 3000, $"集合点");

        string msg = language == Language.Chinese ? "集合引导放黄圈" : "Stack bait";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4000, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    public bool towered = true;

    [ScriptMethod(name: "荒魂：降蛇踩塔指路", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(00A9)$"],
    userControl: true)]
    public void towerUpdate(Event ev, ScriptAccessory sa)
    {
        if (ev.TargetId == sa.Data.Me)
        {
            towered = false;
        }
        else
        {
            towered = true;
        }
    }

    [ScriptMethod(name: "荒魂：降蛇踩塔指路", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:regex:^(2009660)$", "Operate:Add"],
        userControl: true, suppress: 10000)]
    public void 降蛇踩塔指路(Event ev, ScriptAccessory sa)
    {
        var myIndex = sa.GetMyIndex();
        List<Vector3> towerPos = [new(88.7f, 0, 88.7f), new(111.3f, 0, 88.7f), new(88.7f, 0, 111.3f), new (111.3f, 0, 111.3f)];
        List<Vector3> notowerPos = [new(100.28f, 0.01f, 86.13f), new(114.16f, 0.01f, 99.95f), new(86.70f, 0.01f, 100.50f), new(99.98f, 0.01f, 115.13f)];
        DebugMsg($"towered: {towered}", sa);
        if (towered)
        {
            var dp = sa.DrawGuidance(towerPos[myIndex % 4], 0, 10000, $"降蛇踩塔");
        } else
        {
            var dp = sa.DrawGuidance(notowerPos[myIndex % 4], 0, 10000, $"降蛇踩塔");
        }
        
    }
    
    [ScriptMethod(name: "荒魂：降蛇踩塔指路（绘图删除1）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(14301|43988)$"],
        userControl: Debugging)]
    public void 降蛇踩塔指路绘图删除1(Event ev, ScriptAccessory sa)
    {
        // 因被点名降蛇，删除踩塔指路
        var tid = ev.TargetId;
        var aid = ev.ActionId;
        sa.Log.Debug($"检测到 降蛇 {aid}，目标为 {tid} ({sa.GetPlayerIdIndex((uint)tid)})。");
        
        if (tid != sa.Data.Me) return;
        sa.Method.RemoveDraw($"降蛇踩塔.*");
    }
    
    [ScriptMethod(name: "荒魂：降蛇踩塔指路（绘图删除2）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(14301|43988)$"],
        userControl: Debugging)]
    public void 降蛇踩塔指路绘图删除2(Event ev, ScriptAccessory sa)
    {
        // 因降蛇判定，删除踩塔指路
        sa.Method.RemoveDraw($"降蛇踩塔.*");
    }
    
    [ScriptMethod(name: "荒魂：刀禁咒分摊", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(14277|43964)$"],
        userControl: true)]
    public void 刀禁咒分摊(Event ev, ScriptAccessory sa)
    {
        // ForbiddenArts
        var aid = ev.ActionId;
        var sid = ev.SourceId;
        var tid = ev.TargetId;
        sa.Log.Debug($"检测到 刀禁咒 {aid}，目标为 {tid} ({sa.GetPlayerIdIndex((uint)tid)})。");
        
        // TODO 如何确定第二个分摊目标？如果少人点谁？        // 干就完了，管他死没死
        var dp = sa.DrawRect(sid, sa.Data.PartyList[2], 0, 6000, $"青突进",
            0, 5f, 40, false, false, false, false);
        dp.Color = sa.Data.DefaultSafeColor.WithW(ColorAlpha);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        var dp1 = sa.DrawRect(sid, sa.Data.PartyList[3], 0, 6000, $"青突进",
            0, 5f, 40, false, false, false, false);
        dp1.Color = sa.Data.DefaultSafeColor.WithW(ColorAlpha);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);
    }
    
    private static IGameObject? GetBossObject(ScriptAccessory sa)
    {
        return sa.GetByDataId(BossDataId).FirstOrDefault();
    }
    
    #region 优先级字典 类
    public class PriorityDict
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public ScriptAccessory sa {get; set;} = null!;
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public Dictionary<int, int> Priorities {get; set;} = null!;
        public string Annotation { get; set; } = "";
        public int ActionCount { get; set; } = 0;
        
        public void Init(ScriptAccessory accessory, string annotation, int partyNum = 8, bool refreshActionCount = true)
        {
            sa = accessory;
            Priorities = new Dictionary<int, int>();
            for (var i = 0; i < partyNum; i++)
            {
                Priorities.Add(i, 0);
            }
            Annotation = annotation;
            if (refreshActionCount)
                ActionCount = 0;
        }

        /// <summary>
        /// 为特定Key增加优先级
        /// </summary>
        /// <param name="idx">key</param>
        /// <param name="priority">优先级数值</param>
        public void AddPriority(int idx, int priority)
        {
            Priorities[idx] += priority;
        }
        
        /// <summary>
        /// 从Priorities中找到前num个数值最小的，得到新的Dict返回
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public List<KeyValuePair<int, int>> SelectSmallPriorityIndices(int num)
        {
            return SelectMiddlePriorityIndices(0, num);
        }

        /// <summary>
        /// 从Priorities中找到前num个数值最大的，得到新的Dict返回
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public List<KeyValuePair<int, int>> SelectLargePriorityIndices(int num)
        {
            return SelectMiddlePriorityIndices(0, num, true);
        }
        
        /// <summary>
        /// 从Priorities中找到升序排列中间的数值，得到新的Dict返回
        /// </summary>
        /// <param name="skip">跳过skip个元素。若从第二个开始取，skip=1</param>
        /// <param name="num"></param>
        /// <param name="descending">降序排列，默认为false</param>
        /// <returns></returns>
        public List<KeyValuePair<int, int>> SelectMiddlePriorityIndices(int skip, int num, bool descending = false)
        {
            if (Priorities.Count < skip + num)
                return new List<KeyValuePair<int, int>>();

            IEnumerable<KeyValuePair<int, int>> sortedPriorities;
            if (descending)
            {
                // 根据值从大到小降序排序，并取前num个键
                sortedPriorities = Priorities
                    .OrderByDescending(pair => pair.Value) // 先根据值排列
                    .ThenBy(pair => pair.Key) // 再根据键排列
                    .Skip(skip) // 跳过前skip个元素
                    .Take(num); // 取前num个键值对
            }
            else
            {
                // 根据值从小到大升序排序，并取前num个键
                sortedPriorities = Priorities
                    .OrderBy(pair => pair.Value) // 先根据值排列
                    .ThenBy(pair => pair.Key) // 再根据键排列
                    .Skip(skip) // 跳过前skip个元素
                    .Take(num); // 取前num个键值对
            }
            
            return sortedPriorities.ToList();
        }
        
        /// <summary>
        /// 从Priorities中找到升序排列第idx位的数据，得到新的Dict返回
        /// </summary>
        /// <param name="idx"></param>
        /// <param name="descending">降序排列，默认为false</param>
        /// <returns></returns>
        public KeyValuePair<int, int> SelectSpecificPriorityIndex(int idx, bool descending = false)
        {
            var sortedPriorities = SelectMiddlePriorityIndices(0, Priorities.Count, descending);
            return sortedPriorities[idx];
        }

        /// <summary>
        /// 从Priorities中找到对应key的数据，得到其Value排序后位置返回
        /// </summary>
        /// <param name="key"></param>
        /// <param name="descending">降序排列，默认为false</param>
        /// <returns></returns>
        public int FindPriorityIndexOfKey(int key, bool descending = false)
        {
            var sortedPriorities = SelectMiddlePriorityIndices(0, Priorities.Count, descending);
            var i = 0;
            foreach (var dict in sortedPriorities)
            {
                if (dict.Key == key) return i;
                i++;
            }

            return i;
        }
        
        /// <summary>
        /// 一次性增加优先级数值
        /// 通常适用于特殊优先级（如H-T-D-H）
        /// </summary>
        /// <param name="priorities"></param>
        public void AddPriorities(List<int> priorities)
        {
            if (Priorities.Count != priorities.Count)
                throw new ArgumentException("输入的列表与内部设置长度不同");

            for (var i = 0; i < Priorities.Count; i++)
                AddPriority(i, priorities[i]);
        }

        /// <summary>
        /// 输出优先级字典的Key与优先级
        /// </summary>
        /// <returns></returns>
        public string ShowPriorities(bool showJob = true)
        {
            var str = $"{Annotation} ({ActionCount}-th) 优先级字典：\n";
            if (Priorities.Count == 0)
            {
                str += $"PriorityDict Empty.\n";
                return str;
            }
            foreach (var pair in Priorities)
            {
                str += $"Key {pair.Key} {(showJob ? $"({Role[pair.Key]})" : "")}, Value {pair.Value}\n";
            }

            return str;
        }

        public PriorityDict DeepCopy()
        {
            return JsonConvert.DeserializeObject<PriorityDict>(JsonConvert.SerializeObject(this)) ?? new PriorityDict();
        }

        public void AddActionCount(int count = 1)
        {
            ActionCount += count;
        }

    }

    #endregion 优先级字典 类

}

#region 函数集
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
        catch (Exception)
        {
            return false;
        }
    }

    public static uint Id0(this Event @event)
    {
        return ParseHexId(@event["Id"], out var id) ? id : 0;
    }
    
    public static uint Index(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["Index"]);
    }
}

public static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
    }
    
    public static IGameObject? GetMe(this ScriptAccessory sa)
    {
        return sa.Data.Objects.LocalPlayer;
    }

    public static IEnumerable<IGameObject?> GetByDataId(this ScriptAccessory sa, uint dataId)
    {
        return sa.Data.Objects.Where(x => x.DataId == dataId);
    }

    public static string GetPlayerJob(this ScriptAccessory sa, IPlayerCharacter? playerObject, bool fullName = false)
    {
        if (playerObject == null) return "None";
        return fullName ? playerObject.ClassJob.Value.Name.ToString() : playerObject.ClassJob.Value.Abbreviation.ToString();
    }

    public static float GetStatusRemainingTime(this ScriptAccessory sa, IBattleChara? battleChara, uint statusId)
    {
        if (battleChara == null || !battleChara.IsValid()) return 0;
        unsafe
        {
            BattleChara* charaStruct = (BattleChara*)battleChara.Address;
            var statusIdx = charaStruct->GetStatusManager()->GetStatusIndex(statusId);
            return charaStruct->GetStatusManager()->GetRemainingTime(statusIdx);
        }
    }
}
#region 计算函数

public static class MathTools
{
    public static float DegToRad(this float deg) => (deg + 360f) % 360f / 180f * float.Pi;
    public static float RadToDeg(this float rad) => (rad + 2 * float.Pi) % (2 * float.Pi) / float.Pi * 180f;
    
    /// <summary>
    /// 获得任意点与中心点的弧度值，以(0, 0, 1)方向为0，以(1, 0, 0)方向为pi/2。
    /// 即，逆时针方向增加。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetRadian(this Vector3 point, Vector3 center)
        => MathF.Atan2(point.X - center.X, point.Z - center.Z);

    /// <summary>
    /// 获得任意点与中心点的长度。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetLength(this Vector3 point, Vector3 center)
        => new Vector2(point.X - center.X, point.Z - center.Z).Length();
    
    /// <summary>
    /// 将任意点以中心点为圆心，逆时针旋转并延长。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <param name="radian">旋转弧度</param>
    /// <param name="length">基于该点延伸长度</param>
    /// <returns></returns>
    public static Vector3 RotateAndExtend(this Vector3 point, Vector3 center, float radian, float length)
    {
        var baseRad = point.GetRadian(center);
        var baseLength = point.GetLength(center);
        var rotRad = baseRad + radian;
        return new Vector3(
            center.X + MathF.Sin(rotRad) * (length + baseLength),
            center.Y,
            center.Z + MathF.Cos(rotRad) * (length + baseLength)
        );
    }
    
    /// <summary>
    /// 获得某角度所在划分区域
    /// </summary>
    /// <param name="radian">输入弧度</param>
    /// <param name="regionNum">区域划分数量</param>
    /// <param name="baseRegionIdx">0度所在区域的初始Idx</param>>
    /// <param name="isDiagDiv">是否为斜分割，默认为false</param>
    /// <param name="isCw">是否顺时针增加，默认为false</param>
    /// <returns></returns>
    public static int RadianToRegion(this float radian, int regionNum, int baseRegionIdx = 0, bool isDiagDiv = false, bool isCw = false)
    {
        var sepRad = float.Pi * 2 / regionNum;
        var inputAngle = radian * (isCw ? -1 : 1) + (isDiagDiv ? sepRad / 2 : 0);
        var rad = (inputAngle + 4 * float.Pi) % (2 * float.Pi);
        return ((int)Math.Floor(rad / sepRad) + baseRegionIdx + regionNum) % regionNum;
    }
    
    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerX">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
        => point with { X = 2 * centerX - point.X };

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerZ">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
        => point with { Z = 2 * centerZ - point.Z };

    /// <summary>
    /// 将输入点中心对称
    /// </summary>
    /// <param name="point">输入点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static Vector3 PointCenterSymmetry(this Vector3 point, Vector3 center) 
        => point.RotateAndExtend(center, float.Pi, 0);
    
    /// <summary>
    /// 获取给定数的指定位数
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="x">对应位数，个位为1</param>
    /// <returns></returns>
    public static int GetDecimalDigit(this int val, int x)
    {
        var valStr = val.ToString();
        var length = valStr.Length;
        if (x < 1 || x > length) return -1;
        var digitChar = valStr[length - x]; // 从右往左取第x位
        return int.Parse(digitChar.ToString());
    }
}

#endregion 计算函数

#region 位置序列函数
public static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataId，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="sa"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory sa, uint pid)
    {
        // 获得玩家 IDX
        return sa.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="sa"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int GetMyIndex(this ScriptAccessory sa)
    {
        return sa.Data.PartyList.IndexOf(sa.Data.Me);
    }

    /// <summary>
    /// 输入玩家dataId，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="sa"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string GetPlayerJobById(this ScriptAccessory sa, uint pid)
    {
        // 获得玩家职能简称，无用处，仅作DEBUG输出
        var idx = sa.Data.PartyList.IndexOf(pid);
        var str = sa.GetPlayerJobByIndex(idx);
        return str;
    }

    /// <summary>
    /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="idx">位置index</param>
    /// <param name="fourPeople">是否为四人迷宫</param>
    /// <param name="sa"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory sa, int idx, bool fourPeople = false)
    {
        List<string> role8 = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
        List<string> role4 = ["T", "H", "D1", "D2"];
        if (idx < 0 || idx >= 8 || (fourPeople && idx >= 4))
            return "Unknown";
        return fourPeople ? role4[idx] : role8[idx];
    }
    
    /// <summary>
    /// 将List内信息转换为字符串。
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="myList"></param>
    /// <param name="isJob">是职业，在转为字符串前调用转职业函数</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string BuildListStr<T>(this ScriptAccessory sa, List<T> myList, bool isJob = false)
    {
        return string.Join(", ", myList.Select(item =>
        {
            if (isJob && item != null && item is int i)
                return sa.GetPlayerJobByIndex(i);
            return item?.ToString() ?? "";
        }));
    }
}
#endregion 位置序列函数

#region 绘图函数

public static class DrawTools
{
    /// <summary>
    /// 返回绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">绘图基准，可为UID或位置</param>
    /// <param name="targetObj">绘图指向目标，可为UID或位置</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="radian">绘制图形弧度范围</param>
    /// <param name="rotation">绘制图形旋转弧度，以owner面前为基准，逆时针增加</param>
    /// <param name="width">绘制图形宽度，部分图形可保持与长度一致</param>
    /// <param name="length">绘制图形长度，部分图形可保持与宽度一致</param>
    /// <param name="innerWidth">绘制图形内宽，部分图形可保持与长度一致</param>
    /// <param name="innerLength">绘制图形内长，部分图形可保持与宽度一致</param>
    /// <param name="drawModeEnum">绘图方式</param>
    /// <param name="drawTypeEnum">绘图类型</param>
    /// <param name="isSafe">是否使用安全色</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="byY">动画效果随距离变更</param>
    /// <param name="draw">是否直接绘图</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnerBase(this ScriptAccessory sa, 
        object ownerObj, object targetObj, int delay, int destroy, string name, 
        float radian, float rotation, float width, float length, float innerWidth, float innerLength,
        DrawModeEnum drawModeEnum, DrawTypeEnum drawTypeEnum, bool isSafe = false,
        bool byTime = false, bool byY = false, bool draw = true)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.InnerScale = new Vector2(innerWidth, innerLength);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Color = isSafe ? sa.Data.DefaultSafeColor: sa.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        dp.ScaleMode |= byY ? ScaleMode.YByDistance : ScaleMode.None;
        
        switch (ownerObj)
        {
            case uint u:
                dp.Owner = u;
                break;
            case ulong ul:
                dp.Owner = ul;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
            default:
                throw new ArgumentException($"ownerObj {ownerObj} 的目标类型 {ownerObj.GetType()} 输入错误");
        }

        switch (targetObj)
        {
            case 0:
            case 0u:
                break;
            case uint u:
                dp.TargetObject = u;
                break;
            case ulong ul:
                dp.TargetObject = ul;
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos;
                break;
            default:
                throw new ArgumentException($"targetObj {targetObj} 的目标类型 {targetObj.GetType()} 输入错误");
        }
        
        if (draw)
            sa.Method.SendDraw(drawModeEnum, drawTypeEnum, dp);
        return dp;
    }

    /// <summary>
    /// 返回指路绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">出发点</param>
    /// <param name="targetObj">结束点</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">箭头旋转角度</param>
    /// <param name="width">箭头宽度</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        float rotation = 0, float width = 1f, bool isSafe = true, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width,
            width, 0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Displacement, isSafe, false, true, draw);
    
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float rotation = 0, float width = 1f, bool isSafe = true,
        bool draw = true)
        => sa.DrawGuidance((ulong)sa.Data.Me, targetObj, delay, destroy, name, rotation, width, isSafe, draw);

    /// <summary>
    /// 返回圆形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="scale">圆形径长</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float scale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, scale, scale,
            0, 0, DrawModeEnum.Default, DrawTypeEnum.Circle, isSafe, byTime,false, draw);

    /// <summary>
    /// 返回环形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="outScale">外径</param>
    /// <param name="innerScale">内径</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Donut, isSafe, byTime, false, draw);

    /// <summary>
    /// 返回扇形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="radian">弧度</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="outScale">外径</param>
    /// <param name="innerScale">内径</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, radian, rotation, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Fan, isSafe, byTime, false, draw);

    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawFan(ownerObj, 0, delay, destroy, name, radian, rotation, outScale, innerScale, isSafe, byTime, draw);

    /// <summary>
    /// 返回矩形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">矩形起始</param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">矩形宽度</param>
    /// <param name="length">矩形长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Rect, isSafe, byTime, byY, draw);
    
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawRect(ownerObj, 0, delay, destroy, name, rotation, width, length, isSafe, byTime, byY, draw);
    
    /// <summary>
    /// 返回背对绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, bool isSafe = true, bool draw = true)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, 0, 0, 0, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.SightAvoid, isSafe, false, false, draw);

    /// <summary>
    /// 返回击退绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="targetObj">击退源</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="width">箭头宽</param>
    /// <param name="length">箭头长</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float width, float length,
        bool isSafe = false, bool draw = true)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, float.Pi, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Displacement, isSafe, false, false, draw);

    /// <summary>
    /// 返回线型绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">线条起始</param>
    /// <param name="targetObj">线条目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">线条宽度</param>
    /// <param name="length">线条长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLine(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Line, isSafe, byTime, byY, draw);
    
    /// <summary>
    /// 返回两对象间连线绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">起始源</param>
    /// <param name="targetObj">目标源</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="width">线宽</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnection(this ScriptAccessory sa, object ownerObj, object targetObj,
        int delay, int destroy, string name, float width = 1f, bool isSafe = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, 0, width, width,
            0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Line, isSafe, false, true, draw);

    /// <summary>
    /// 赋予输入的dp以ownerId为源的远近目标绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="isNearOrder">从owner计算，近顺序或远顺序</param>
    /// <param name="orderIdx">从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersDistanceOrder(this DrawPropertiesEdit self, bool isNearOrder,
        uint orderIdx)
    {
        self.CentreResolvePattern = isNearOrder
            ? PositionResolvePatternEnum.PlayerNearestOrder
            : PositionResolvePatternEnum.PlayerFarestOrder;
        self.CentreOrderIndex = orderIdx;
        return self;
    }
    
    /// <summary>
    /// 赋予输入的dp以ownerId为源的仇恨顺序绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="orderIdx">仇恨顺序，从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersEnmityOrder(this DrawPropertiesEdit self, uint orderIdx)
    {
        self.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        self.CentreOrderIndex = orderIdx;
        return self;
    }
    
    /// <summary>
    /// 赋予输入的dp以position为源的远近目标绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="isNearOrder">从owner计算，近顺序或远顺序</param>
    /// <param name="orderIdx">从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetPositionDistanceOrder(this DrawPropertiesEdit self, bool isNearOrder,
        uint orderIdx)
    {
        self.TargetResolvePattern = isNearOrder
            ? PositionResolvePatternEnum.PlayerNearestOrder
            : PositionResolvePatternEnum.PlayerFarestOrder;
        self.TargetOrderIndex = orderIdx;
        return self;
    }
    
    /// <summary>
    /// 赋予输入的dp以ownerId施法目标为源的绘图
    /// </summary>
    /// <param name="self"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersTarget(this DrawPropertiesEdit self)
    {
        self.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        return self;
    }
}

#endregion 绘图函数

#region 标点函数

public static class MarkerHelper
{
    public static void LocalMarkClear(this ScriptAccessory sa)
    {
        sa.Log.Debug($"删除本地标点。");
        sa.Method.Mark(0xE000000, MarkType.Attack1, true);
        sa.Method.Mark(0xE000000, MarkType.Attack2, true);
        sa.Method.Mark(0xE000000, MarkType.Attack3, true);
        sa.Method.Mark(0xE000000, MarkType.Attack4, true);
        sa.Method.Mark(0xE000000, MarkType.Attack5, true);
        sa.Method.Mark(0xE000000, MarkType.Attack6, true);
        sa.Method.Mark(0xE000000, MarkType.Attack7, true);
        sa.Method.Mark(0xE000000, MarkType.Attack8, true);
        sa.Method.Mark(0xE000000, MarkType.Bind1, true);
        sa.Method.Mark(0xE000000, MarkType.Bind2, true);
        sa.Method.Mark(0xE000000, MarkType.Bind3, true);
        sa.Method.Mark(0xE000000, MarkType.Stop1, true);
        sa.Method.Mark(0xE000000, MarkType.Stop2, true);
        sa.Method.Mark(0xE000000, MarkType.Square, true);
        sa.Method.Mark(0xE000000, MarkType.Circle, true);
        sa.Method.Mark(0xE000000, MarkType.Cross, true);
        sa.Method.Mark(0xE000000, MarkType.Triangle, true);
    }

    public static void MarkClear(this ScriptAccessory sa,
        bool enable = true, bool local = false, bool localString = false)
    {
        if (!enable) return;
        sa.Log.Debug($"接收命令：删除标点");
        
        if (local)
        {
            if (localString)
                sa.Log.Debug($"[字符模拟] 删除本地标点。");
            else
                sa.LocalMarkClear();
        }
        else
            sa.Method.MarkClear();
    }

    public static void MarkPlayerByIdx(this ScriptAccessory sa, int idx, MarkType marker,
        bool enable = true, bool local = false, bool localString = false)
    {
        if (!enable) return;
        if (localString)
            sa.Log.Debug($"[本地字符模拟] 为{idx}({sa.GetPlayerJobByIndex(idx)})标上{marker}。");
        else
            sa.Method.Mark(sa.Data.PartyList[idx], marker, local);
    }

    public static void MarkPlayerById(ScriptAccessory sa, uint id, MarkType marker,
        bool enable = true, bool local = false, bool localString = false)
    {
        if (!enable) return;
        if (localString)
            sa.Log.Debug($"[本地字符模拟] 为{sa.GetPlayerIdIndex(id)}({sa.GetPlayerJobById(id)})标上{marker}。");
        else
            sa.Method.Mark(id, marker, local);
    }

    public static int GetMarkedPlayerIndex(this ScriptAccessory sa, List<MarkType> markerList, MarkType marker)
    {
        return markerList.IndexOf(marker);
    }
}

#endregion

#region 特殊函数

public static class SpecialFunction
{
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

    public static void ScaleModify(this ScriptAccessory sa, IGameObject? obj, float scale)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->Scale = scale;
            charaStruct->DisableDraw();
            charaStruct->EnableDraw();
        }
        sa.Log.Debug($"ScaleModify => {obj.Name.TextValue} | {obj} => {scale}");
    }

    public static void SetRotation(this ScriptAccessory sa, IGameObject? obj, float radian, bool show = false)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->SetRotation(radian);
        }
        sa.Log.Debug($"改变面向 {obj.Name.TextValue} | {obj.EntityId} => {radian.RadToDeg()}");
        
        if (!show) return;
        var ownerObj = sa.GetById(obj.EntityId);
        if (ownerObj == null) return;
        var dp = sa.DrawGuidance(ownerObj, 0, 0, 2000, $"改变面向 {obj.Name.TextValue}", radian, draw: false);
        dp.FixRotation = true;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
        
    }

    public static void SetPosition(this ScriptAccessory sa, IGameObject? obj, Vector3 position, bool show = false)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->SetPosition(position.X, position.Y, position.Z);
        }
        sa.Log.Debug($"改变位置 => {obj.Name.TextValue} | {obj.EntityId} => {position}");
        
        if (!show) return;
        var dp = sa.DrawCircle(position, 0, 2000, $"传送点 {obj.Name.TextValue}", 0.5f, true, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        
    }
}

#endregion 特殊函数


public static class NamazuHelper
{
    public class NamazuCommand(ScriptAccessory accessory, string url, string command, string param)
    {
        private ScriptAccessory accessory { get; set; } = accessory;
        private string _url = url;

        public void PostCommand()
        {
            var url = $"{_url}/{command}";
            accessory.Log.Debug($"向{url}发送{param}");
            accessory.Method.HttpPost(url, param);
        }
    }

    public class Waymark
    {
        public ScriptAccessory accessory { get; set; }
        private Dictionary<string, object> _jsonObj = new();
        private string? _jsonPayload;

        public Waymark(ScriptAccessory _accessory)
        {
            accessory = _accessory;
        }

        public void AddWaymarkType(string type, Vector3 pos, bool active = true)
        {
            string[] validTypes = ["A", "B", "C", "D", "One", "Two", "Three", "Four"];
            var waymarkType = type;
            if (!validTypes.Contains(type)) return;
            _jsonObj[waymarkType] = new Dictionary<string, object>
            {
                { "X", pos.X },
                { "Y", pos.Y },
                { "Z", pos.Z },
                { "Active", active }
            };
        }

        public void SetJsonPayload(bool local = true, bool log = true)
        {
            _jsonObj["LocalOnly"] = local;
            _jsonObj["Log"] = log;
            _jsonPayload = JsonConvert.SerializeObject(_jsonObj);
        }

        public string? GetJsonPayload()
        {
            if (_jsonPayload == null)
                SetJsonPayload();
            return _jsonPayload;
        }

        public void PostWaymarkCommand(int port)
        {
            var param = GetJsonPayload();
            if (param == null) return;
            var post = new NamazuCommand(accessory, $"http://127.0.0.1:{port}", "place", param);
            post.PostCommand();
        }

        public void ClearWaymarks(int port)
        {
            var post = new NamazuCommand(accessory, $"http://127.0.0.1:{port}", "place", "clear");
            post.PostCommand();
        }
    }
}

public static class DrawHelper
{
    public static void DrawBeam(ScriptAccessory accessory, Vector3 sourcePosition, Vector3 targetPosition, string name = "Light's Course", int duration = 6700, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = sourcePosition;
        dp.TargetPosition = targetPosition;
        dp.Scale = new Vector2(10, 50);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    public static void DrawCircle(ScriptAccessory accessory, Vector3 position, Vector2 scale, int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    public static void DrawDisplacement(ScriptAccessory accessory, Vector3 target, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = accessory.Data.Me;
        dp.Color = color ?? accessory.Data.DefaultSafeColor;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.TargetPosition = target;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    public static void DrawDisplacementby2points(ScriptAccessory accessory, Vector3 origin, Vector3 target, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Position = origin;
        dp.Color = color ?? accessory.Data.DefaultSafeColor;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.TargetPosition = target;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    public static void DrawRect(ScriptAccessory accessory, Vector3 position, Vector3 targetPos, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.TargetPosition = targetPos;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    public static void DrawFan(ScriptAccessory accessory, Vector3 position, float rotation, Vector2 scale, float angle, int duration, string name, Vector4? color = null, int delay = 0, bool fix = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.Rotation = rotation;
        dp.Scale = scale;
        dp.Radian = angle * (float.Pi / 180);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.FixRotation = fix;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    public static void DrawFanOwner(ScriptAccessory accessory, ulong owner, float rotation, Vector2 scale, float angle, int duration, string name, Vector4? color = null, int delay = 0, bool scaleByTime = true, bool fix = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.Rotation = rotation;
        dp.Scale = scale;
        dp.Radian = angle * (float.Pi / 180);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.FixRotation = fix;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    public static void DrawLine(ScriptAccessory accessory, Vector3 startPosition, Vector3 endPosition, float width, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = startPosition;
        dp.TargetPosition = endPosition;
        dp.Scale = new Vector2(width, 1);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
    }

    public static void DrawArrow(ScriptAccessory accessory, Vector3 startPosition, Vector3 endPosition, float x, float y, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = startPosition;
        dp.TargetPosition = endPosition;
        dp.Scale = new Vector2(x, y);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Arrow, dp);
    }

    public static void DrawCircleObject(ScriptAccessory accessory, ulong? ob, Vector2 scale, int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0)
    {
        if (ob == null) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = ob.Value;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
}
#endregion 函数集

