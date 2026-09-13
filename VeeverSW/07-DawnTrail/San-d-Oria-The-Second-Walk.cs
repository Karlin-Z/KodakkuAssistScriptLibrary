using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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

namespace Veever.DawnTrail.San_d_Oria_The_Second_Walk;

[ScriptType(name: Name, territorys: [1304], guid: "26a812eb-b8e2-4f31-8568-affe775bd4c6",
    version: Version, author: "Veever", note: NoteStr, updateInfo: UpdateInfo)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$
// ^\[\w+\|[^|]+\|E\]\s\w+


public class San_d_Oria_The_Second_Walk
{
    const string NoteStr =
    """
    v0.0.0.5
    1. 如果需要某个机制的绘画或者哪里出了问题请在dc@我或者私信我
    2. Boss1可能会遇到双手直线时间偏长的问题（懒得改了影响不是很大）
    3. Boss3并没有对坦克职业的击退死刑执行自动防击退
    4. 初版Boss2的反射激光和Boss4的复制可能会出现绘制问题（画错，不画）, 请发现后携带arr在dc反馈
    5. Boss2的地板绘制会有一点点小问题，请无视
    6. 游戏崩溃了请务必在频道说，如果没有arr请跟我说一下大概机制和时间并提供[dalamud.log]
    鸭门
    ------------------------------
    1. If you need a draw for a mechanic or notice any issues, @ me on DC or DM me.
    2. Boss 1 may have an issue where the double-arm Rect AOE lasts a bit too long.  
       (Not a big deal so I’m too lazy to fix it.)
    3. Boss 3 does not auto-use anti-knockback for tankbuster knockbacks. 
    4. In the initial version, Boss 2’s Energy Ray (reflect laser) and Boss 4’s Duplicate may have drawing issues (wrong/missing).  
       If you encounter this, please provide an ARR for feedback on DC.
    5. Boss 2’s floor drawing has a small issue — just ignore it.
    6. If the game crashes, please report it in the dc channel.  
       If no ARR is available, let me know roughly which mechanic and the time it happened, and provide your [dalamud.log].
    Duckmen
    """;

    const string UpdateInfo =
    $"""
    v{Version}
    修复了TTS
    鸭门
    ----------------------------------
    Fixed TTS
    Duckmen.
    """;

    private const string Name = "LV.100 桑多利亚：第二巡行 [San d Oria The Second Walk]";
    private const string Version = "0.0.0.5";
    private const string DebugVersion = "a";

    private const bool Debugging = true;

    private static readonly List<string> Role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
    private static readonly Vector3 Center = new Vector3(100, 0, 100);

    [UserSetting("播报语言(language)")]
    public Language language { get; set; } = Language.Chinese;

    [UserSetting("绘图不透明度，数值越大越显眼(Draw opacity — higher value = more visible)")]
    public static float ColorAlpha { get; set; } = 1f;

    [UserSetting("文字横幅提示开关(Banner text toggle)")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关(TTS toggle)")]
    public bool isTTS { get; set; } = true;

    [UserSetting("是否自动使用防击退(Auto anti-knockback)")]
    public bool useAntiKnockback { get; set; } = true;

    [UserSetting("指路开关(Guide arrow toggle)")]
    public bool isLead { get; set; } = true;

    [UserSetting("目标标记开关(Target Marker toggle)")]
    public bool isMark { get; set; } = true;

    [UserSetting("本地目标标记开关(打开则为本地开关，关闭则为小队) - Local target marker toggle (ON = local only, OFF = party shared)")]
    public bool LocalMark { get; set; } = true;

    //[UserSetting("是否进行场地标点引导(Waymark guide toggle)")]
    //public bool PostNamazuPrint { get; set; } = true;

    //[UserSetting("鲶鱼精邮差端口设置(PostNamazuPort Setting)")]
    //public int PostNamazuPort { get; set; } = 2019;

    //[UserSetting("场地标点是否为本地标点(如果选择非本地标点，脚本只会在非战斗状态下进行标点) - Waymarks: local toggle(off = party shared, OOC only)")]
    //public bool PostNamazuisLocal { get; set; } = true;

    [UserSetting("Debug开关, 非开发用请关闭 - Debug on/off (don't touch unless you know what you're doing)")]
    public bool isDebug { get; set; } = false;

    public enum Language
    {
        Chinese,
        English
    }

    private static bool _initHint = false;

    private int MoontideFontCount = 0;
    public int SynchronizedStrikeCount = 0;

    private readonly object MoontideFontLock = new object();
    private readonly object CountLock = new object();
    private readonly object SynchronizedStrikeLock = new object();
    private readonly object Boss1QuadraRecordDataLock = new object();
    private readonly object TetherDataLock = new object();

    private Dictionary<uint, (ulong, Vector3)> tetherData = new Dictionary<uint, (ulong, Vector3)>();
    private Dictionary<uint, (ulong, Vector3, int)> Boss1QuadraRecordData = new Dictionary<uint, (ulong, Vector3, int)>();

    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }
     
    public void Init(ScriptAccessory sa)
    {
        sa.Log.Debug($"脚本 {Name} v{Version}{DebugVersion} 完成初始化.");
        sa.Method.RemoveDraw(".*");

        MoontideFontCount = 0;
        SynchronizedStrikeCount = 0;

        isSecondTankBuster = false;
        duplicatePhase2 = false;
        hasKnockback = false;
        frameworkRegistered = false;

        sa.Method.ClearFrameworkUpdateAction(this);
        Boss1QuadraRecordGuid = "";

        tetherData.Clear();
        Boss1QuadraRecordData.Clear();
        surfaceMissiles.Clear();
        boss3TankBusterList.Clear();
        boss4TankBusterList.Clear();
    }


    #region 小怪
    [ScriptMethod(name: "---- 小怪-Mobs ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
    userControl: Debugging)]
    public void mobs(Event ev, ScriptAccessory sa)
    {
    }

    #region boss1前
    [ScriptMethod(name: "土烟 - Dust Cloud", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43554"])]
    public void DustCloud(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(10f), 120, 2700, $"DustCloud-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Dust Cloud Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:43554"], userControl: false)]
    public void DustCloudClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"DustCloud-{ev.SourceId}");
    }
    #endregion

    #region boss2前
    [ScriptMethod(name: "雷质横扫 - Electroswipe", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43559"])]
    public void Electroswipe(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(50f), 120, 5700, $"Electroswipe-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Pressure Wave Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:43559"], userControl: false)]
    public void ElectroswipeClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Electroswipe-{ev.SourceId}");
    }

    [ScriptMethod(name: "伤头&插言 打断销毁", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^75(38|51)$"], userControl: false)]
    public void destoryCancelAction(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($".*{ev.TargetId}");
    }
    #endregion

    #region boss3前
    [ScriptMethod(name: "强麻痹 - Paralyze III", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43570"])]
    public void ParalyzeIII(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(6f), 5700, $"ParalyzeIII-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Paralyze III Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:43570"], userControl: false)]
    public void ParalyzeIIIClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"ParalyzeIII-{ev.SourceId}");
    }

    [ScriptMethod(name: "吸蚀 - Sucker", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43791"])]
    public void Sucker(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircleObject(sa, ev.SourceId, new Vector2(8f), 6000, $"Sucker-{ev.SourceId}", scaleByTime: false, color: new Vector4(1, 0, 0, ColorAlpha));
        if (useAntiKnockback)
        {
            sa.Method.UseAction(sa.Data.Me, 7559);
            sa.Method.UseAction(sa.Data.Me, 7548);
        }
    }

    [ScriptMethod(name: "冲击之吼 - Impact Roar", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43566"])]
    public void ImpactRoar(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "AOE" : "AOE";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "强放逐 & 投射 - Banish III & Catapult", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(43562|43568|43567)$"])]
    public void BanishIIICatapult(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(6f), 2700, $"BanishIIIorCatapult-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Banish III & Catapult Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:regex:^(43562|43568|43567)$"], userControl: false)]
    public void BanishIIICatapultClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"BanishIIIorCatapult-{ev.SourceId}");
    }

    
    [ScriptMethod(name: "强力冲击 - Mighty Shatter", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43575"])]
    public void MightyShatter(Event ev, ScriptAccessory sa)
    {
        string msg = "";
        if (isMark)
        {
            msg = language == Language.Chinese ? "打断被标记目标" : "Interrupt marked target";
        } else
        {
            msg = language == Language.Chinese ? "打断阿尔库俄纽斯" : "Interrupt Alkyoneus";
        }

        if (isText) sa.Method.TextInfo($"{msg}", duration: 3000, true);
        if (isTTS) sa.Method.TTS($"{msg}");
        if (isMark) sa.Method.Mark((uint)ev.TargetId, KodakkuAssist.Module.GameOperate.MarkType.Bind1, LocalMark);
    }

    [ScriptMethod(name: "强力攻击 - Power Attack", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43569"])]
    public void PowerAttack(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(20f), 120, 2700, $"PowerAttack-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Dust Cloud Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:43569"], userControl: false)]
    public void PowerAttackClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"PowerAttack-{ev.SourceId}");
    }

    #endregion

    #endregion


    #region Boss1
    private static IGameObject? GetBossObject(ScriptAccessory sa, uint BossDataId)
    {
        return sa.GetByDataId(BossDataId).FirstOrDefault();
    }
    private uint westHandDataId = 18753;
    private uint eastHandDataId = 18754;

    [ScriptMethod(name: "---- Boss1 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void Boss1(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "垒石IV-Stonega IV", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44490"])]
    public void MedicineField(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "AOE" : "AOE";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "双手死亡猛击-Synchronized Smite", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44444|44443)$"])]
    public void SynchronizedSmite(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(32, 60), 4700, $"SynchronizedSmite-{ev.SourceId}", sa.Data.DefaultDangerColor, offset: new Vector3(0, 0, 30));
    }

    [ScriptMethod(name: "深红谜煞-Crimson Riddle", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45045|45044)$"])]
    public void CrimsonRiddle(Event ev, ScriptAccessory sa)
    {
        float rot = 0;
        if (ev.ActionId == 45045) rot = float.Pi;
        DrawHelper.DrawFanObject(sa, ev.SourceId, rot, new Vector2(30f), 180, 4700, $"CrimsonRiddle-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    #region 四神
    [ScriptMethod(name: "Boss1_4godClear", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44414)$"], userControl: false)]
    public void Boss1_4godClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.UnregistFrameworkUpdateAction(Boss1QuadraRecordGuid);
        frameworkRegistered = false;

        lock (Boss1QuadraRecordDataLock)
        {
            Boss1QuadraRecordData.Clear();
        }
        lock (TetherDataLock)
        {
            tetherData.Clear();
        }
    }

    #region 青龙
    // anticlock 44417
    // clock 44416
    [ScriptMethod(name: "青龙旋息-Eastwind Wheel", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44416|44417)$"])]
    public void EastwindWheel(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(18, 60), 7700, $"EastwindWheelRect-{ev.SourceId}", new Vector4(0, 1, 1, ColorAlpha));

        if (ev.ActionId == 44417)
        {
            DrawHelper.DrawFanObject(sa, ev.SourceId, float.Pi / 4, new Vector2(65f), 90, 7700, $"EastwindWheelFan-{ev.SourceId}", new Vector4(0, 1, 1, ColorAlpha), offset: new Vector3(9, 0, 0));
        } else if (ev.ActionId == 44416)
        {
            DrawHelper.DrawFanObject(sa, ev.SourceId, -float.Pi / 4, new Vector2(65f), 90, 7700, $"EastwindWheelFan-{ev.SourceId}", new Vector4(0, 1, 1, ColorAlpha), offset: new Vector3(-9, 0, 0));
        }
    }

    #endregion

    #region 白虎
    // 44432 circle efpos
    // 44431 rect boss
    [ScriptMethod(name: "白虎腾跃-Gloaming Gleam", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44431)$"])]
    public void GloamingGleam(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(12, 50), 3000, $"GloamingGleam-{ev.SourceId}", new Vector4(1, 1, 0, ColorAlpha));
    }

    [ScriptMethod(name: "剔骨獠牙-Razor Fang", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44432)$"])]
    public void RazorFang(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(20f), 4500, $"RazorFang-{ev.SourceId}", new Vector4(1, 1, 0, ColorAlpha), scaleByTime: false);
    }
    #endregion

    #region 玄武
    [ScriptMethod(name: "喷水流-MoontideFont", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44421|44422)$"])]
    public void MoontideFont(Event ev, ScriptAccessory sa)
    {
        lock (MoontideFontLock)
        {
            MoontideFontCount++;
            DebugMsg($"MoontideFontCount: {MoontideFontCount}", sa);
        }

        int duration = int.TryParse(ev["DurationMilliseconds"]?.ToString(), out var d) ? d : 8000;
        var delay = 0;

        DebugMsg($"time: {duration}", sa);

        if (MoontideFontCount >= 10)
        {
            delay = 3000;
            duration -= 3000;
        }

        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(9f), duration, $"MoontideFont-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: delay);
    }

    [ScriptMethod(name: "玄武突进-Midwinter March", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44337)$"])]
    public async void MidwinterMarch(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "靠近危险区域准备穿进月环" : "Stay near danger zone, then move in";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.Method.TTS($"{msg}");

        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(12f), 6700, $"MidwinterMarch-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);
        await Task.Delay(7000);

        string msg1 = language == Language.Chinese ? "进入月环" : "Move in (Dynamo)";
        if (isText) sa.Method.TextInfo($"{msg1}", duration: 3500, true);
        if (isTTS) sa.Method.TTS($"{msg1}");

        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(12f), 3500, $"MidwinterMarchDountSafe-{ev.SourceId}", sa.Data.DefaultSafeColor, scaleByTime: false);
        DrawHelper.DrawDount(sa, ev.EffectPosition, new Vector2(60f), new Vector2(12f), 3500, $"MidwinterMarchDount-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);
    }

    [ScriptMethod(name: "死亡旋转-Dead Wringer", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44439)$"])]
    public void DeadWringer(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "进入月环" : "Move in (Dynamo)";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 3500, true);
        if (isTTS) sa.Method.TTS($"{msg}");

        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(14f), 4700, $"DeadWringerDountSafe-{ev.SourceId}", sa.Data.DefaultSafeColor, scaleByTime: false);
        DrawHelper.DrawDount(sa, ev.EffectPosition, new Vector2(60f), new Vector2(14f), 4700, $"DeadWringerDount-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);
    }

    #endregion

    #region 朱雀
    [ScriptMethod(name: "延烧-Arm of Purgatory", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44796|44794)$"])]
    public void ArmofPurgatory(Event ev, ScriptAccessory sa)
    {
        if (ev.ActionId == 44796)
        {
            DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(15f), 5200, $"ArmofPurgatory-{ev.SourceId}", new Vector4(1, 0, 0, ColorAlpha), scaleByTime: false);
        }
        else
        {
            DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(15f), 10700, $"ArmofPurgatory-{ev.SourceId}", new Vector4(1, 0, 0, ColorAlpha), scaleByTime: false);
        }

    }

    [ScriptMethod(name: "朱雀翔空-Vermilion Flight", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44795)$"])]
    public void VermilionFlight(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(20, 60), 7700, $"VermilionFlight-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }



    #endregion

    #endregion


    [ScriptMethod(name: "连线记录0156", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0156)$"], userControl: false)]
    public async void tetherRecorder0156(Event ev, ScriptAccessory sa)
    {
        DebugMsg($"0156 record {ev.SourceId}", sa);
        lock (TetherDataLock)
        {
            tetherData[ev.Id] = (ev.SourceId, ev.SourcePosition);
        }

        await Task.Delay(4700);
        lock (TetherDataLock)
        {
            tetherData.Clear();
        }
    }


    [ScriptMethod(name: "右手猛击-Striking Right", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44435|44462)$"])]
    public void StrikingRight(Event ev, ScriptAccessory sa)
    {
        Vector3 tetherPosition;
        lock (TetherDataLock)
        {
            if (!tetherData.ContainsKey(0156))
            {
                DebugMsg($"Tether data for 0156 not found", sa);
                return;
            }
            tetherPosition = tetherData[0156].Item2;
        }

        if (ev.ActionId == 44435)
        {
            DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(10f), 4700, $"StrikingRight-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);

            DrawHelper.DrawCircle(sa, tetherPosition, new Vector2(30f), 4700, $"StrikingRightBig-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);
        }
        else
        {
            DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(10f), 4700, $"StrikingRight-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: 3000);

            DrawHelper.DrawCircle(sa, tetherPosition, new Vector2(30f), 4700, $"StrikingRightBig-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: 3000);
        }

    }

    [ScriptMethod(name: "左手猛击-Striking Left", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44436|44463)$"])]
    public void StrikingLeft(Event ev, ScriptAccessory sa)
    {
        Vector3 tetherPosition;
        lock (TetherDataLock)
        {
            if (!tetherData.ContainsKey(0156))
            {
                DebugMsg($"Tether data for 0156 not found", sa);
                return;
            }
            tetherPosition = tetherData[0156].Item2;
        }

        if (ev.ActionId == 44436)
        {
            DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(10f), 4700, $"StrikingLeft-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);

            DrawHelper.DrawCircle(sa, tetherPosition, new Vector2(30f), 4700, $"StrikingLeftBig-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);
        }
        else
        {
            DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(10f), 4700, $"StrikingLeft-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: 3000);

            DrawHelper.DrawCircle(sa, tetherPosition, new Vector2(30f), 4700, $"StrikingLeftBig-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: 3000);
        }

    }

    [ScriptMethod(name: "右手死亡猛击-Striking Right Large", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44441)$"])]
    public void SmitingRightLarge(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(30f), 4700, $"SmitingRightLarge-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);
    }


    [ScriptMethod(name: "旋转连击-Double Wringer", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44445)$"])]
    public void DoubleWringer(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(14f), 9700, $"SmitingRightLarge-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);
    }


    [ScriptMethod(name: "双手猛击-Synchronized Strike", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44464)$"])]
    public async void SynchronizedStrike(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(10, 60), 8200, $"SynchronizedStrike-{ev.SourceId}", new Vector4(1, 0, 0, ColorAlpha), offset: new Vector3(0, 0, 30));
        lock (SynchronizedStrikeLock)
        {
            SynchronizedStrikeCount++;
            DebugMsg($"SynchronizedStrikeCount: {SynchronizedStrikeCount}", sa);
        }

        if (SynchronizedStrikeCount == 2)
        {
            unsafe
            {
                var x = AgentMap.Instance()->CurrentTerritoryId;
            }
            
            DebugMsg($"SynchronizedStrikeCount == 2", sa);
            var westHand = GetBossObject(sa, westHandDataId);
            var eastHand = GetBossObject(sa, eastHandDataId);
            if (westHand == null || eastHand == null) return;

            DrawHelper.DrawRectObjectNoTarget(sa, westHand.EntityId, new Vector2(32, 60), 4700, $"SynchronizedStrike2-{ev.SourceId}", sa.Data.DefaultDangerColor, offset: new Vector3(0, 0, 30), delay: 12000);
            DrawHelper.DrawRectObjectNoTarget(sa, eastHand.EntityId, new Vector2(32, 60), 4700, $"SynchronizedStrike2-{ev.SourceId}", sa.Data.DefaultDangerColor, offset: new Vector3(0, 0, 30), delay: 12000);
            await Task.Delay(5000);
            SynchronizedStrikeCount = 0;
        }

    }

    private string Boss1QuadraRecordGuid = "";
    public bool frameworkRegistered = false;
    // actionId, (sid, efpos, count)

    [ScriptMethod(name: "Boss1四连记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44433)$"])]
    public void Boss1QuRecord(Event ev, ScriptAccessory sa)
    {
        lock (Boss1QuadraRecordDataLock)
        {
            Boss1QuadraRecordData.Clear();
        }

        if (!string.IsNullOrEmpty(Boss1QuadraRecordGuid))
        {
            sa.Method.UnregistFrameworkUpdateAction(Boss1QuadraRecordGuid);
        }

        DebugMsg($"Register new framework for Boss1QuRecord", sa);
        sa.Log.Debug($"Register new framework for Boss1QuRecord");
        Boss1QuadraRecordGuid = sa.Method.RegistFrameworkUpdateAction(() =>
        {
            try
            {
                Dictionary<uint, (ulong, Vector3, int)> localData;
                lock (Boss1QuadraRecordDataLock)
                {
                    if (Boss1QuadraRecordData.Count != 2) return;
                    localData = new Dictionary<uint, (ulong, Vector3, int)>(Boss1QuadraRecordData);
                    Boss1QuadraRecordData.Clear();
                }

                var westHand = GetBossObject(sa, westHandDataId);
                var eastHand = GetBossObject(sa, eastHandDataId);
                if (westHand == null || eastHand == null) return;

                foreach (var kv in localData)
                {
                    switch (kv.Value.Item3)
                    {
                        case 0:
                            HandleBoss1Quadra(sa, kv.Key, kv.Value.Item1, kv.Value.Item2, westHand, eastHand, 0);
                            break;
                        case 1:
                            HandleBoss1Quadra(sa, kv.Key, kv.Value.Item1, kv.Value.Item2, westHand, eastHand, 6500);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                sa.Log.Error($"Framework callback error: {ex.Message}");
                lock (Boss1QuadraRecordDataLock)
                {
                    Boss1QuadraRecordData.Clear();
                }
            }
        });
        frameworkRegistered = true;
    }


    public void HandleBoss1Quadra(ScriptAccessory sa, uint actionId, ulong sid, Vector3 efpos, IGameObject? westHand, IGameObject? eastHand, int delay)
    {
        // 44446 - westhand 30f + efpos 10f circle draw
        // 44461 - Boss Chariot with later Dynamo
        // 44464 - single rect stick with later large rect with 2 hands
        DebugMsg($"HandleBoss1Quadra In, ActionId: {actionId}", sa);
        sa.Log.Debug($"HandleBoss1Quadra In, ActionId: {actionId}");
        switch (actionId)
        {
            // 44446
            case 44462:
                DebugMsg($"44462 case", sa);
                sa.Log.Debug($"HandleBoss1Quadra In, ActionId: {actionId}");
                DrawHelper.DrawCircle(sa, efpos, new Vector2(10f), 4700, $"44462s-{efpos}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: delay);

                if (westHand != null)
                {
                    DrawHelper.DrawCircle(sa, westHand.Position, new Vector2(30f), 6500, $"44462L-{westHand.EntityId}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: delay);
                }

                break;
            // 44447
            case 44463:
                DebugMsg($"44463 case", sa);
                sa.Log.Debug($"HandleBoss1Quadra In, ActionId: {actionId}");
                DrawHelper.DrawCircle(sa, efpos, new Vector2(10f), 4700, $"44463s-{efpos}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: delay);

                if (eastHand != null)
                {
                    DrawHelper.DrawCircle(sa, eastHand.Position, new Vector2(30f), 6500, $"44463L-{eastHand.EntityId}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: delay);
                }

                break;
            case 44461:
                DebugMsg($"44461 case", sa);
                sa.Log.Debug($"HandleBoss1Quadra In, ActionId: {actionId}");
                DrawHelper.DrawCircle(sa, efpos, new Vector2(14f), 5000, $"44461Danger-{sid}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: delay);

                DrawHelper.DrawCircle(sa, efpos, new Vector2(14f), 4700, $"44461safe-{sid}", sa.Data.DefaultSafeColor, scaleByTime: false, delay: 5000 + delay);
                DrawHelper.DrawDount(sa, efpos, new Vector2(60f), new Vector2(14f), 4700, $"44461dount-{sid}", sa.Data.DefaultDangerColor, scaleByTime: false, delay: 5000 + delay);

                break;
            //case 44464:
            //    DebugMsg($"44464 case", sa);
            //    sa.Log.Debug($"44464 case");
            //    //DrawHelper.DrawRectObjectNoTarget(sa, sid, new Vector2(10, 60), 8000, $"44464stick-{efpos}", new Vector4(1, 0, 0, 1), offset: new Vector3(0, 0, 30), delay: delay);
            //    if (westHand != null && eastHand != null)
            //    {
            //        RemoveSynchronizedStrike2(sa);
            //        DrawHelper.DrawRectPosNoTarget(sa, westHand.Position, new Vector2(32, 60), 4700, $"44464west", sa.Data.DefaultDangerColor, offset: new Vector3(0, 0, 30), delay: 12000 + delay);
            //        DrawHelper.DrawRectPosNoTarget(sa, eastHand.Position, new Vector2(32, 60), 4700, $"44464east", sa.Data.DefaultDangerColor, offset: new Vector3(0, 0, 30), delay: 12000 + delay);

            //    }
            //    break;
        }
    }

    public async void RemoveSynchronizedStrike2(ScriptAccessory sa)
    {
        await Task.Delay(1000);
        sa.Method.RemoveDraw($"SynchronizedStrike2.*");
    }

    [ScriptMethod(name: "Boss1ActionMonitor", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44462|44463|44464|44461)$"], userControl: false, suppress: 2000)]
    public void Boss1ActionMonitor(Event ev, ScriptAccessory sa)
    {
        DebugMsg($"RecordAction {ev.ActionId}", sa);
        sa.Log.Debug($"RecordAction {ev.ActionId}");

        lock (Boss1QuadraRecordDataLock)
        {
            var item3 = Boss1QuadraRecordData.Count;

            if (!Boss1QuadraRecordData.ContainsKey(ev.ActionId))
            {
                Boss1QuadraRecordData.Add(ev.ActionId, (ev.SourceId, ev.EffectPosition, item3));
                DebugMsg($"Boss1QuadraRecordData.Add post: ActionId:{ev.ActionId}; SourceId: {ev.SourceId}; EffectPosition: {ev.EffectPosition}, item3: {item3}", sa);
                sa.Log.Debug($"Boss1QuadraRecordData.Add post: ActionId:{ev.ActionId}; SourceId: {ev.SourceId}; EffectPosition: {ev.EffectPosition}, item3: {item3}");
            }
            else
            {
                DebugMsg($"ActionId {ev.ActionId} already exists in Boss1QuadraRecordData, skipping", sa);
                sa.Log.Debug($"ActionId {ev.ActionId} already exists in Boss1QuadraRecordData, skipping");
            }
        }
    }

    #region 沙地
    [ScriptMethod(name: "致命拥抱-Deadly Hold", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44466)$"])]
    public void DeadlyHold(Event ev, ScriptAccessory sa)
    {
        sa.Method.UnregistFrameworkUpdateAction(Boss1QuadraRecordGuid);
        frameworkRegistered = false;

        lock (Boss1QuadraRecordDataLock)
        {
            Boss1QuadraRecordData.Clear();
        }
        lock (TetherDataLock)
        {
            tetherData.Clear();
        }

        bool isTank = ExtensionMethods.IsTank(sa.Data.MyObject);
        if (isTank)
        {
            DebugMsg($"isTank: {isTank}", sa);
            string msg = language == Language.Chinese ? "坦克踩塔" : "Tanks take tower";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 8000, true);
            if (isTTS) sa.Method.TTS($"{msg}");
        }
        else
        {
            DebugMsg($"isTank: {isTank}", sa);
            string msg = language == Language.Chinese ? "攻击手臂" : "Attack Arms";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 8000, true);
            if (isTTS) sa.Method.TTS($"{msg}");
        }
    }
    #endregion
    [ScriptMethod(name: "Boss1Clear", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:regex:^(18754)$"], userControl: false)]
    public void Boss1Clear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(".*");
        MoontideFontCount = 0;
        SynchronizedStrikeCount = 0;

        sa.Method.ClearFrameworkUpdateAction(this);
        Boss1QuadraRecordGuid = "";
        frameworkRegistered = false;

        lock (Boss1QuadraRecordDataLock)
        {
            Boss1QuadraRecordData.Clear();
        }
        lock (TetherDataLock)
        {
            tetherData.Clear();
        }
        lock (SurfaceMissileLock)
        {
            surfaceMissiles.Clear();
        }
    }
    #endregion


    #region Boss2
    [ScriptMethod(name: "---- Boss2 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
    userControl: Debugging)]
    public void Boss2(Event ev, ScriptAccessory sa)
    {
    }


    [ScriptMethod(name: "AOE提示 - AOE Notify", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44331"])]
    public void boss2AOE(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "AOE" : "AOE";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4500, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "死刑提示 - Tankbuster Notify", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44305"])]
    public void boss2Tankbuster(Event ev, ScriptAccessory sa)
    {
        if (ev.TargetId == sa.Data.Me) 
        {
            string msg = language == Language.Chinese ? "死刑点名, 注意减伤" : "Targeted Buster";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 4500, true);
            if (isTTS) sa.Method.TTS($"{msg}");
        }
    }

    // TODO: Check correctness
    [ScriptMethod(name: "能量射线 - Energy Ray", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44338"])]
    public void EnergyRay(Event ev, ScriptAccessory sa)
    {
        var casterPos = ev.SourcePosition;
        var rotation = ev.SourceRotation;

        // initial rect
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(16f, 40f), 4700, $"EnergyRay-Initial-{ev.SourceId}", sa.Data.DefaultDangerColor);

        // initial origin
        var initialPosMap = new Dictionary<Vector3, int[]>
        {
            { new Vector3(755f, 320, 816f), new int[] { 0, 1 } },
            { new Vector3(755f, 320, 800f), new int[] { 2, 3 } },
            { new Vector3(820f, 380, 784f), new int[] { 4 } },
            { new Vector3(820f, 380, 816f), new int[] { 5 } },
            { new Vector3(755f, 320, 784f), new int[] { 6, 7 } }
        };

        // reflect pos and angle
        var aoeMap = new (Vector3 firstReflect, int angleFirst, Vector3 secondReflect, int angleSecond)[]
        {
            (new Vector3(725f, 320, 816f), 2, new Vector3(725f, 320, 784f), 1),         // checked
            (new Vector3(745f, 320, 816f), 2, new Vector3(745f, 320, 800f), 3),         // checked
            (new Vector3(745f, 320, 800f), 0, new Vector3(745f, 320, 816f), 3),         // checked
            (new Vector3(725f, 320, 800f), 2, new Vector3(725f, 320, 784f), 1),
            (new Vector3(810f, 380f, 784f), 0, Vector3.Zero, 0),                        // checked
            (new Vector3(810f, 380f, 816f), 2, Vector3.Zero, 0),                        // checked
            (new Vector3(745f, 320, 784f), 0, new Vector3(745f, 320, 816f), 3),         // checked
            (new Vector3(725f, 320, 784f), 0, new Vector3(725f, 320, 800f), 1)          // checked
        };

        // Dir   (0 = 东, 1 = 北, 2 = 西, 3 = 南)
        var cardinalAngles = new float[] { 0f, float.Pi / 2, float.Pi, -float.Pi / 2 };


        var manaScreens = sa.Data.Objects.Where(obj => obj.DataId == 0x1EBE8B || obj.DataId == 0x1EBE8C).ToList();
        
        if (manaScreens.Count == 0) return;
        
        DebugMsg($"Found Screen, count: {manaScreens.Count}", sa);
        
        foreach (var posMapping in initialPosMap)
        {
            if (Vector3.Distance(casterPos, posMapping.Key) < 2f) 
            {
                DebugMsg($"Caster position matched: {posMapping.Key}", sa);
                var indices = posMapping.Value;
                DebugMsg($"Possible indices: {string.Join(", ", indices)}", sa);
                foreach (var index in indices)
                {
                    var aoe = aoeMap[index];

                    // Check Screen existence
                    var matchingScreen = manaScreens.FirstOrDefault(screen =>
                        Vector3.Distance(screen.Position, aoe.firstReflect) < 2f);

                    if (matchingScreen != null)
                    {
                        var firstAngle = cardinalAngles[aoe.angleFirst];
                        var dp1 = sa.Data.GetDefaultDrawProperties();
                        dp1.Name = $"EnergyRay-Reflect1-{index}";
                        dp1.Color = sa.Data.DefaultDangerColor;
                        dp1.Position = aoe.firstReflect;
                        dp1.Scale = new Vector2(20f, 48f);
                        dp1.Rotation = firstAngle;
                        dp1.Delay = 700;
                        dp1.DestoryAt = 4700;
                        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);
                        DebugMsg($"First reflection drawn at {aoe.firstReflect} with angle {firstAngle} rad ({firstAngle * 180 / float.Pi} deg)", sa);

                        if (aoe.secondReflect != Vector3.Zero)
                        {
                            var secondAngle = cardinalAngles[aoe.angleSecond];
                            var dp2 = sa.Data.GetDefaultDrawProperties();
                            dp2.Name = $"EnergyRay-Reflect2-{index}";
                            dp2.Color = sa.Data.DefaultDangerColor;
                            dp2.Position = aoe.secondReflect;
                            dp2.Scale = new Vector2(20f, 40f);
                            dp2.Rotation = secondAngle;
                            dp2.Delay = 1200;
                            dp2.DestoryAt = 4700;
                            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
                            DebugMsg($"Second reflection drawn at {aoe.secondReflect} with angle {secondAngle} rad ({secondAngle * 180 / float.Pi} deg)", sa);
                        }
                        return;
                    }
                }
            }
        }
    }

    [ScriptMethod(name: "能量射线清理 - Energy Ray Clean", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44338|44339|44340|44341|44342)$"], userControl: false)]
    public void EnergyRayClean(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"EnergyRay-.*-{ev.SourceId}");
    }

    [ScriptMethod(name: "齐射记录-FireRecorder", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44327|44325)$"])]
    public void FireRecorder(Event ev, ScriptAccessory sa)
    {
        // 44327 后前齐射
        // 44325 前后齐射
        string msg = "";
        if (ev.ActionId == 44327)
        {
            msg = language == Language.Chinese ? "先去前面再去后面" : "Front then back!";
        } else
        {
            msg = language == Language.Chinese ? "先去后面再去前面" : "Back then Front!";
        }

        if (isText) sa.Method.TextInfo($"{msg}", duration: 4500, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "欧米茄冲击波 - Omega Blaster", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44329|44330)$"])]
    public void OmegaBlaster(Event ev, ScriptAccessory sa)
    {
        // 44329 first
        // 44330 second
        //var rot = 0f;
        //if (ev.ActionId == 44329)
        //DrawHelper.DrawFanObject(sa, ev.SourceId, ev.SourceRotation + (50 * float.Pi), new Vector2(50f), 180, 6500, $"OmegaBlaster-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);
        //DrawHelper.DrawFanObject(sa, ev.SourceId, new Vector2(50f), 180, 6500, $"OmegaBlaster-{ev.SourceId}", sa.Data.DefaultDangerColor, scaleByTime: false);
        int delay = 0;
        int destory = 6500;
        if (ev.ActionId == 44330)
        {
            delay = 6500;
            destory = 2500;
        }
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"OmegaBlaster-{ev.SourceId}";
        dp.Color = new Vector4(0, 1, 1, ColorAlpha);
        dp.Owner = ev.SourceId;
        dp.Scale = new Vector2(50f);
        dp.Radian = 180 * (float.Pi / 180);
        dp.Delay = delay;
        dp.DestoryAt = destory;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "冲撞 - Crash", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44295"])]
    public void Crash(Event ev, ScriptAccessory sa)
    {
        var pos = ev.SourcePosition;
        bool isWest = false;

        if (pos.X < 781)
        {
            isWest = true;
        }

        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(24, 40), 10200, $"Crash-{ev.SourceId}", new Vector4(1, 0, 0, ColorAlpha));

        if (isWest)
        {
            //var targetPosition = mypos.Value with { X = mypos.Value.X + 5 };
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = "CrashDisplacementWest";
            dp.Color = sa.Data.DefaultSafeColor;
            dp.Owner = sa.Data.Me;
            //dp.TargetPosition = targetPosition;
            dp.Rotation = float.Pi / 2;
            dp.Scale = new Vector2(2, 25f);
            dp.FixRotation = true;
            dp.DestoryAt = 10200;
            if (isLead) sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        } else if (!isWest)
        {
            //var targetPosition = mypos.Value with { X = mypos.Value.X - 5 };
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = "CrashDisplacementEast";
            dp.Color = sa.Data.DefaultSafeColor;
            dp.Owner = sa.Data.Me;
            //dp.TargetPosition = targetPosition;
            dp.Rotation = -float.Pi / 2;
            dp.Scale = new Vector2(2, 25f);
            dp.FixRotation = false;
            dp.DestoryAt = 10200;
            if (isLead) sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        surfaceMissiles.Clear();
    }

    private List<(Vector3 position, ulong tid)> surfaceMissiles = new();
    private readonly object SurfaceMissileLock = new object();

    //Todo： 地板有问题
    [ScriptMethod(name: "地板记录 - Surface Missile Record", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0268"])]
    public void surfaceMissileRecord(Event ev, ScriptAccessory sa)
    {
        var targetObj = IbcHelper.GetById(sa, ev.TargetId);
        if (targetObj == null) return;
        var pos = targetObj.Position;
        if (pos == Vector3.Zero) return;

        DebugMsg($"Surface Missile Record {ev.TargetId}, pos: {pos}", sa);
        sa.Log.Debug($"Surface Missile Record {ev.TargetId}, pos: {pos}");
        lock (SurfaceMissileLock)
        {
            surfaceMissiles.Add((pos.Quantized(), ev.TargetId));
            DebugMsg($"surfaceMissiles Count: {surfaceMissiles.Count}", sa);
            sa.Log.Debug($"surfaceMissiles Count: {surfaceMissiles.Count}");
        }

        if (surfaceMissiles.Count == 12)
        {
            DebugMsg($"----------surfaceMissiles == 12--------------", sa);
            sa.Log.Debug($"----------surfaceMissiles == 12--------------");

            int index = 1;

            foreach (var t in surfaceMissiles)
            {
                Vector3 position = t.position;
                ulong targetId = t.tid;

                DebugMsg($"index: {index}; pos: {position}; tid: {targetId}", sa);
                sa.Log.Debug($"index: {index}; pos: {position}; tid: {targetId}");
                index++;
            }

            var delay = 0;
            var destory = 0;

            for (int i = 0; i < surfaceMissiles.Count; i++)
            {
                var (position, tid) = surfaceMissiles[i];

                if (i < 4)
                {
                    destory = 2800;
                }
                if (i >= 4 && i < 8)
                {
                    delay = 2800;
                    destory = 2800;
                }
                if (i >= 8)
                {
                    delay = 5600;
                    destory = 3000;
                }

                DrawHelper.DrawRectObjectNoTarget(sa, tid, new Vector2(20, 12), destory, $"{i}: surfaceMissiles - delay:{delay} - destory:{destory}", offset: new Vector3(0, 0, 6f), delay: delay);
            }
        }
    }

    [ScriptMethod(name: "制导导弹 - Guided Missile", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44324"])]
    public void GuidedMissile(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(6f), 1000, $"GuidedMissile-{ev.EffectPosition}", scaleByTime: false, color: new Vector4(1, 0, 0, ColorAlpha));
    }

    [ScriptMethod(name: "多重导弹 - Multi-missile", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45037|45036)$"])]
    public void Multimissile(Event ev, ScriptAccessory sa)
    {
        // 45037 small 3700
        // 45036 large 3800
        var destory = 3700;
        var size = 6f;
        if (ev.ActionId == 45036)
        {
            destory = 3800;
            size = 10f;
        }
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(size), destory, $"Multimissile-{ev.EffectPosition}");
    }

    [ScriptMethod(name: "堡垒围攻 - Citadel Siege", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44312"])]
    public void CitadelSiege(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(10f, 48f), 4700, $"Citadel Siege-{ev.EffectPosition}", color: new Vector4(1, 0, 0, ColorAlpha), scalemode: ScaleMode.ByTime);
    }

    [ScriptMethod(name: "化学炸弹 - Chemical Bomb", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44303"])]
    public void ChemicalBomb(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(20f), 4700, $"Chemical Bomb-{ev.EffectPosition}", color: new Vector4(1, 0, 0, ColorAlpha), delay: 2700, scaleByTime: false);
    }


    [ScriptMethod(name: "Boss2 Clear & reset", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:18759"], userControl: false, suppress: 3000)]
    public void Boss2Clear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(".*");
        lock (SurfaceMissileLock)
        {
            surfaceMissiles.Clear();
        }
    }
    #endregion

    #region Boss3
    [ScriptMethod(name: "---- Boss3 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
    userControl: Debugging)]
    public void Boss3(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "AOE提示 - AOE Notify", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:(44221|44212)"])]
    public void boss3AOE(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "AOE" : "AOE";
        if (ev.ActionId == 44212)
        {
            msg = language == Language.Chinese ? "大AOE" : "Heavy AOE";
        }
        
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4500, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "决战之地 - Proving Ground", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45065"])]
    public void ProvingGround(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "远离Boss脚下" : "Don't stand under boss";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 2700, true);
        if (isTTS) sa.Method.TTS($"{msg}");
        DrawHelper.DrawCircle(sa, ev.SourcePosition, new Vector2(5f), 2700, $"ProvingGround-{ev.SourceId}", color: new Vector4(1, 0, 0, ColorAlpha), scaleByTime: false);
    }

    [ScriptMethod(name: "魔法剑 - Elemental Blade", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44(19[1-9]|20[0-2]|179|18[0-9]|190))$"])]
    public void ElementalBlade(Event ev, ScriptAccessory sa)
    {
        // Rect        width    length   
        // 44191 火     5        80
        // 44192 土     5        80
        // 44193 水     5        80
        // 44194 冰     5        80
        // 44195 雷     5        80
        // 44196 风     5        80
        // -----------------------------------
        // 44197 火     20       80
        // 44198 土     20       80
        // 44199 水     20       80
        // 44200 冰     20       80
        // 44201 雷     20       80
        // 44202 风     20       80
        DebugMsg($"Action Id: {ev.ActionId}", sa);
        var width = 5f;

        if (ev.ActionId >= 44197)
        {
            width = 20f;
        }

        if (ev.ActionId >= 44191 && ev.ActionId <= 44202)
        {
            DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(width, 80f), 8700, $"ElementalBlade-{ev.SourceId}", color: new Vector4(1, 0, 0, ColorAlpha));
        }

        // Fan          Angle
        // 44179 火      20
        // 44180 土      20
        // 44181 水      20
        // 44182 冰      20
        // 44183 雷      20
        // 44184 风      20
        // -----------------------------------
        // 44185 火      100
        // 44186 土      100
        // 44187 水      100
        // 44188 冰      100
        // 44189 雷      100
        // 44190 风      100
        var angle = 20f;

        if (ev.ActionId >= 44185)
        {
            angle = 100f;
        }

        if (ev.ActionId >= 44179 && ev.ActionId <= 44190)
        {
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = $"ElementalBladeFan-{ev.SourceId}";
            dp.Color = new Vector4(1, 0, 0, ColorAlpha);
            dp.Owner = ev.SourceId;
            dp.Scale = new Vector2(45f);
            dp.Radian = angle * (float.Pi / 180);
            dp.DestoryAt = 8700;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }

    public bool isSecondTankBuster = false;
    List<ulong> boss3TankBusterList = new List<ulong>();
    private readonly object boss3TankBusterLock = new object();
    [ScriptMethod(name: "坦克击退死刑提示 - Tank knock-back buster Notify", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0265"])]
    public void boss3TankBuster(Event ev, ScriptAccessory sa)
    {
        lock (boss3TankBusterLock)
        {
            boss3TankBusterList.Add(ev.TargetId);
            DebugMsg($"boss3TankBusterList Count: {boss3TankBusterList.Count}, isSecondTankBuster: {isSecondTankBuster}", sa);
            if (boss3TankBusterList.Count == 3)
            {
                DebugMsg($"Contains me :{boss3TankBusterList.Contains((ulong)sa.Data.Me)}", sa);
                sa.Log.Debug($"me: {sa.Data.Me}, list1: {boss3TankBusterList[0]}; List2: {boss3TankBusterList[1]}, List3: {boss3TankBusterList[2]}");

                string msg = "";
                if (boss3TankBusterList.Contains((ulong)sa.Data.Me))
                {
                    msg = language == Language.Chinese ? "击退死刑, 注意减伤" : "Tankbuster with knockback, Use mits";
                }
                else
                {
                    msg = language == Language.Chinese ? "远离范围AOE死刑" : "AOE Tankbuster — Stay Away";
                }
                if (isText) sa.Method.TextInfo($"{msg}", duration: 6900, true);
                if (isTTS) sa.Method.TTS($"{msg}");
                boss3TankBusterList.Clear();
            }
        }


        DrawHelper.DrawRectObjectTarget(sa, ev.SourceId, ev.TargetId, new Vector2(10f, 40f), 6900, $"TankBusterKnockback-{ev.TargetId}", color: new Vector4(1, 0, 0, ColorAlpha));

        if (ev.TargetId == sa.Data.Me && isLead)
        {
            var knockbackDistance = 20f;
            if (isSecondTankBuster) knockbackDistance = 30f;
            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = "boss3TankBuster Knockback";
            dp.Color = sa.Data.DefaultSafeColor;
            dp.Owner = sa.Data.Me;
            dp.TargetObject = ev.SourceId;
            dp.Rotation = float.Pi;
            dp.Scale = new Vector2(2, knockbackDistance);
            dp.DestoryAt = 6900;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
    }

    [ScriptMethod(name: "崇高之剑 - Sublime Estoc", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:9352"])]
    public void SublimeEstoc(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(5f, 40f), 4500, $"SublimeEstoc-{ev.SourceId}", color: new Vector4(1, 1, 0, ColorAlpha));
    }

    [ScriptMethod(name: "回环连斩 - Great Wheel", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44207|44205|44206)$"])]
    public void GreatWheel(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "钢铁远离" : "Chariot (Out)";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 1000, true);
        if (isTTS) sa.Method.TTS($"{msg}");

        DrawHelper.DrawCircleObject(sa, ev.SourceId, new Vector2(10f), 2700, $"GreatWheel-{ev.SourceId}", color: new Vector4(1, 0, 0, ColorAlpha));
    }

    [ScriptMethod(name: "回环连斩正面 - Great Wheel Front", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44209"])]
    public void GreatWheelFan(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(40f), 180, 5500, $"GreatWheelFan-{ev.SourceId}", color: new Vector4(1, 0, 0, ColorAlpha), scaleByTime: false);
    }

    [ScriptMethod(name: "属性反应 - Elemental Resonance", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44216"])]
    public void ElementalResonance(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircleObject(sa, ev.SourceId, new Vector2(18f), 6700, $"ElementalResonance-{ev.SourceId}", color: new Vector4(1, 0, 0, ColorAlpha), scaleByTime: false);
    }

    [ScriptMethod(name: "幻光剑 - Illumed Estoc", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44218"])]
    public void IllumedEstoc(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(13f, 120f), 7700, $"IllumedEstoc-{ev.SourceId}", color: new Vector4(1, 0, 0, ColorAlpha), offset: new Vector3(0, 0, 30));
    }

    [ScriptMethod(name: "盾牌猛击 - Shield Bash", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44222"])]
    public void ShieldBash(Event ev, ScriptAccessory sa)
    {
        isSecondTankBuster = true;
        string msg = language == Language.Chinese ? "击退至安全通道" : "Knockback to safe lane";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 6700, true);
        if (isTTS) sa.Method.TTS($"{msg}");

        List<Vector3> safePos = new List<Vector3>
        {
            new Vector3(-199.95f, -900.00f, 144.75f),
            new Vector3(-195.50f, -900.00f, 152.50f),
            new Vector3(-205.20f, -900.00f, 152.88f)
        };

        foreach (var pos in safePos)
        {
            DrawHelper.DrawCircle(sa, pos, new Vector2(2f), 6700, $"ShieldBashSafe-{pos}", color: sa.Data.DefaultSafeColor, scaleByTime: false);
        }

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "ShieldBash Knockback";
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.TargetObject = ev.SourceId;
        dp.Rotation = float.Pi;
        dp.Scale = new Vector2(2, 30f);
        dp.DestoryAt = 6700;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "放逐IV - Empyreal Banish IV", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44224"])]
    public void EmpyrealBanishIV(Event ev, ScriptAccessory sa)
    {
        string tname = ev["TargetName"]?.ToString() ?? "未知目标";

        string msg = language == Language.Chinese ? $"与{tname}分摊" : $"Stack with {tname}";

        if (ev.TargetId == sa.Data.Me)
        {
            msg = language == Language.Chinese ? "分摊点名" : "Stack";
        }

        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.Method.TTS($"{msg}");

        DrawHelper.DrawCircleObject(sa, ev.TargetId, new Vector2(5f), 4700, $"EmpyrealBanishIV-{ev.SourceId}", color: sa.Data.DefaultSafeColor, scaleByTime: false);
    }

    #endregion
    #region Boss4
    [ScriptMethod(name: "---- Boss4 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
    userControl: true)]
    public void Boss4(Event ev, ScriptAccessory sa)
    {
    }



    List<ulong> boss4TankBusterList = new List<ulong>();
    private readonly object boss4TankBusterLock = new object();
    [ScriptMethod(name: "坦克击退死刑提示 - Tank knock-back buster Notify", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0158"])]
    public void boss4TankBuster(Event ev, ScriptAccessory sa)
    {
        lock (boss4TankBusterLock)
        {
            boss4TankBusterList.Add(ev.TargetId);
            DebugMsg($"boss4TankBusterList Count: {boss4TankBusterList.Count}", sa);
            if (boss4TankBusterList.Count == 3)
            {
                DebugMsg($"Contains me :{boss4TankBusterList.Contains((ulong)sa.Data.Me)}", sa);
                if (isDebug) sa.Log.Debug($"me: {sa.Data.Me}, list1: {boss4TankBusterList[0]}; List2: {boss4TankBusterList[1]}, List3: {boss4TankBusterList[2]}");

                string msg = "";
                if (boss4TankBusterList.Contains((ulong)sa.Data.Me))
                {
                    msg = language == Language.Chinese ? "范围死刑, 注意减伤" : "AOE Tankbuster, Use mits";
                }
                else
                {
                    msg = language == Language.Chinese ? "远离范围AOE死刑" : "AOE Tankbuster — Stay Away";
                }
                if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
                if (isTTS) sa.Method.TTS($"{msg}");
                boss4TankBusterList.Clear();
            }
        }

        DrawHelper.DrawCircleObject(sa, ev.TargetId, new Vector2(6f), 4700, $"TankBusterKnockback-{ev.TargetId}", color: new Vector4(1, 0, 0, ColorAlpha));
    }


    [ScriptMethod(name: "时神宿星落(月环) - Cronos Sling(Dynamo)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44366"])]
    public void CronosSlingDynamo(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? $"月环靠近" : $"Dynamo (In) ";

        if (isText) sa.Method.TextInfo($"{msg}", duration: 7200, true);
        if (isTTS) sa.Method.TTS($"{msg}");

        DrawHelper.DrawCircleObject(sa, ev.TargetId, new Vector2(6f), 7200, $"CronosSlingDynamoSafe-{ev.SourceId}", color: sa.Data.DefaultSafeColor, scaleByTime: false);
        DrawHelper.DrawDount(sa, ev.EffectPosition, new Vector2(70f), new Vector2(6f), 7200, $"CronosSlingDynamoSafe-{ev.SourceId}", color: sa.Data.DefaultDangerColor, scaleByTime: false);
    }

    [ScriptMethod(name: "时神宿星落(钢铁) - Cronos Sling(Chariot)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44365"])]
    public void CronosSlingChariot(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? $"钢铁远离" : $"Chariot (Out) ";

        if (isText) sa.Method.TextInfo($"{msg}", duration: 7200, true);
        if (isTTS) sa.Method.TTS($"{msg}");

        DrawHelper.DrawCircleObject(sa, ev.TargetId, new Vector2(9f), 7200, $"CronosSlingDynamoSafe-{ev.SourceId}", color: sa.Data.DefaultDangerColor, scaleByTime: false);   
    }

    [ScriptMethod(name: "时神宿星落(半场刀) - Cronos Sling(Haircut left/right)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44367|44368)$"])]
    public void CronosSlingHaircut(Event ev, ScriptAccessory sa)
    {
        // 44367 right
        // 44368 left
        var rot = float.Pi / 2;
        if (ev.ActionId == 44367)
        {
            rot = -float.Pi / 2;
        } 

        DrawHelper.DrawFanObject(sa, ev.SourceId, rot, new Vector2(70f), 180, 13000, $"CronosSlingHaircutRight-{ev.SourceId}", color: new Vector4(1, 0, 0, ColorAlpha), scaleByTime: false);
    }

    [ScriptMethod(name: "高天气旋 - Empyreal Vortex", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44400"])]
    public void EmpyrealVortex(Event ev, ScriptAccessory sa)
    {
        if (ev.TargetId == sa.Data.Me)
        {
            string msg = language == Language.Chinese ? $"分散" : $"Spread";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
            if (isTTS) sa.Method.TTS($"{msg}");
        }

        DrawHelper.DrawCircleObject(sa, ev.TargetId, new Vector2(5f), 4700, $"EmpyrealVortex-{ev.SourceId}", color: sa.Data.DefaultDangerColor, scaleByTime: true);
    }


    [ScriptMethod(name: "翘曲 - Warp", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44375"])]
    public void Warp(Event ev, ScriptAccessory sa)
    {
        Vector3 Centerpos = new Vector3(800.00f, -900.00f, -800.00f);
        var tarobj1 = IbcHelper.GetById(sa, ev.TargetId);
        if (tarobj1 == null) return;

        DebugMsg($"distance: {Vector3.Distance(tarobj1.Position, Centerpos)})", sa);
        if (Vector3.Distance(tarobj1.Position, Centerpos) < 15f) return;

        string msg = language == Language.Chinese ? $"前往传送点" : $"Move to the Portal";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 3700, true);
        if (isTTS) sa.Method.TTS($"{msg}");

        DrawHelper.DrawDisplacement(sa, tarobj1.Position, new Vector2(2, 4), 5500, $"Warp-{ev.TargetId}", color: sa.Data.DefaultSafeColor);
    }


    [ScriptMethod(name: "强催眠 - Sleepga", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44376"])]
    public void Sleepga(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanObject(sa, ev.SourceId, 0, new Vector2(70), 180, 2700, $"Sleepga-{ev.SourceId}", color: sa.Data.DefaultDangerColor, scaleByTime: false);
    }

    [ScriptMethod(name: "地神光尘流 - Gaea Stream", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44373"])]
    public void GaeaStream(Event ev, ScriptAccessory sa)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"GaeaStream-{ev.SourceId}";
        dp.Color = new Vector4(1, 1, 0, ColorAlpha);
        dp.Owner = ev.SourceId;
        dp.Scale = new Vector2(4, 24);
        dp.DestoryAt = 1700;
        dp.Rotation = float.Pi / 2;
        dp.Offset = new Vector3(12, 0, 0);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    // 偷懒不画了
    [ScriptMethod(name: "欧米茄投枪 - Omega Javelin", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44382"], suppress: 5000)]
    public void OmegaJavelin(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? $"远离标枪" : $"Away from Javelin";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4200, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    //0000002C|0000002D|0000002E|0000002F
    private bool duplicatePhase2 = false;
    [ScriptMethod(name: "复制 - Duplicate", eventType: EventTypeEnum.EnvControl, eventCondition: ["Index:regex:^(0000002[C-F]|000000(3[0-9])|0000003[A-D]|00000024)$"])]
    public void Duplicate(Event ev, ScriptAccessory sa)
    {
        var index = ev.Index();
        var state = uint.Parse(ev["State"]?.ToString() ?? "0", System.Globalization.NumberStyles.HexNumber);

        DebugMsg($"EnvControl - Index: 0x{index:X} ({index}), State: 0x{state:X} ({state})", sa);
        sa.Log.Debug($"EnvControl - Index: 0x{index:X} ({index}), State: 0x{state:X} ({state})");

        DebugMsg($"con: {state == 0x00020001u}", sa);
        sa.Log.Debug("$con: {state == 0x00020001u}");

        switch (index)
        {
            case >= 0x2Cu and <= 0x34u when !duplicatePhase2 && state == 0x00020001u || duplicatePhase2 && state == 0x00080010u:

                DebugMsg($"1. Duplicate AOE at tile index: 0x{index:X}, duplicatePhase2: {duplicatePhase2}", sa);
                sa.Log.Debug($"1. Duplicate AOE at tile index: 0x{index:X}, duplicatePhase2: {duplicatePhase2}");

                var tile = index - 0x2Cu;

                AddDuplicateAOEs((int)(tile / 3), (int)(tile % 3), sa);
                break;

            case >= 0x35u and <= 0x3Du:
                DebugMsg($"2. Duplicate AOE at tile index: 0x{index:X}", sa);
                sa.Log.Debug($"2. Duplicate AOE at tile index: 0x{index:X}");
                switch (state)
                {
                    case 0x00080010u:
                        DebugMsg($"case 0x00080010u activated", sa);
                        sa.Log.Debug($"case 0x00080010u activated");
                        var tile2 = index - 0x35u;
                        AddDuplicateAOEs((int)(tile2 / 3), (int)(tile2 % 3), sa);
                        break;
                    case 0x00020001u:
                        DebugMsg($"case 0x00020001u activated", sa);
                        sa.Log.Debug($"case 0x00020001u activated");
                        var tile3 = index - 0x35u;
                        var tilePos = new Vector3(784f + tile3 % 3 * 16f, -900, -816f + tile3 / 3 * 16f) + new Vector3(0, 0, -8f);
                        DebugMsg($"Draw Duplicate at tile index: 0x{index:X}, pos: {tilePos}", sa);
                        sa.Log.Debug($"Draw Duplicate at tile index: 0x{index:X}, pos: {tilePos}");
                        DrawHelper.DrawRectPosNoTarget(sa, tilePos, new Vector2(16f, 16f), 7800, $"Duplicate-{index}", new Vector4(1, 0, 0, ColorAlpha), drawMode: DrawModeEnum.Default);

                        break;
                }
                break;
            case 0x24u when state == 0x00200040u:
                duplicatePhase2 = true;
                DebugMsg("Duplicate Phase 2 activated", sa);
                sa.Log.Debug("Duplicate Phase 2 activated");
                break;
        }
    }

    private void AddDuplicateAOEs(int row, int col, ScriptAccessory sa)
    {
        DebugMsg($"AddDuplicateAOEs at row: {row}, col: {col}", sa);
        (int dr, int dc)[] offsets =
        [
            (0, 0),
            (-1, 0),
            (1, 0),
            (0, -1),
            (0, 1),
        ];

        for (var i = 0; i < 5; ++i)
        {
            var nRow = row + offsets[i].dr;
            var nCol = col + offsets[i].dc;

            sa.Log.Debug($"Checking offset {i}: nRow: {nRow}, nCol: {nCol}");
            if (nRow is >= 0 and < 3 && nCol is >= 0 and < 3)
            {
                var aoePos = new Vector3(784f + nCol * 16f, -900, -816f + nRow * 16f) + new Vector3(0, 0, -8f);
                var duration = duplicatePhase2 ? 11100 : 10400;

                //DrawHelper.DrawRectPosNoTarget(sa, aoePos, , duration, $"DuplicateAOE-{nRow}-{nCol}", sa.Data.DefaultDangerColor);
                var dp = sa.Data.GetDefaultDrawProperties();
                dp.Name = $"offset {i} - DuplicateAOE-{nRow}-{nCol}-pos: {aoePos}";
                dp.Color = new Vector4(1, 0, 0, ColorAlpha);
                dp.Position = aoePos;
                //dp.Rotation = -float.Pi / 2; 
                dp.Scale = new Vector2(16f, 16f);
                dp.DestoryAt = duration;
                dp.Offset = new Vector3(0, 0, 0);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
    }

    [ScriptMethod(name: "复制清理 - Duplicate Clear", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44369|44370)$"], userControl: false)]
    public void DuplicateClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw("Duplicate.*");
        sa.Method.RemoveDraw("DuplicateAOE.*");
        DebugMsg("Duplicate AOEs cleared", sa);
    }

    [ScriptMethod(name: "星体破裂 - Stellar Burst", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44403)$"])]
    public void StellarBurst(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? $"前往中间分摊" : $"Stack Mid";
        if (ev.TargetId == sa.Data.Me)
        {
            msg = language == Language.Chinese ? $"分摊点名" : $"Stack";
        }
         
        if (isText) sa.Method.TextInfo($"{msg}", duration: 6000, true);
        if (isTTS) sa.Method.TTS($"{msg}");

        DrawHelper.DrawCircleObject(sa, ev.TargetId, new Vector2(6f), 12000, $"StellarBurst-{ev.SourceId}", color: sa.Data.DefaultSafeColor, scaleByTime: false);
    }

    string Tornadoguid = "";
    private bool hasKnockback = false;
    [ScriptMethod(name: "龙卷 - Tornado", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44395)$"])]
    public async void Tornado(Event ev, ScriptAccessory sa)
    {
        Tornadoguid = sa.Method.RegistFrameworkUpdateAction(() =>
        {
            //DebugMsg($"{IbcHelper.HasStatus(sa, sa.Data.MyObject, 1209)}", sa);
            List<uint> knockBackStatus = new List<uint> { 1209, 160, 2663 };

            for (int i = 0; i < knockBackStatus.Count; i++)
            {
                var hasStatus = IbcHelper.HasStatus(sa, sa.Data.MyObject, knockBackStatus[i]);
                //DebugMsg($"Checking Status {knockBackStatus[i]}: {hasStatus}", sa);
                if (hasStatus)
                {
                    hasKnockback = true;
                    sa.Method.RemoveDraw($"Tornado-Danger:{ev.SourceId}");
                    break;
                }
            }

        });


        if (!hasKnockback)
        {
            if (useAntiKnockback) sa.Method.UseAction(sa.Data.Me, 7559);
            if (useAntiKnockback) sa.Method.UseAction(sa.Data.Me, 7548);
            DrawHelper.DrawCircleObject(sa, ev.SourceId, new Vector2(21f), 6000, $"Tornado-Danger:{ev.SourceId}", scaleByTime: false, color: new Vector4(1, 0, 0, ColorAlpha));
        }

        await Task.Delay(5700);
        if (Tornadoguid != "")
        {
            sa.Method.UnregistFrameworkUpdateAction(Tornadoguid);
            Tornadoguid = "";
            hasKnockback = false;
        }
    }


    [ScriptMethod(name: "旋绕之火 & 旋绕之雷 - orbital flame & orbital levin", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(18747|18746)$"])]
    public void orbitalFlameLevin(Event ev, ScriptAccessory sa)
    {
        // 18746 thunder
        // 18747 fire
        var scale = 3f;
        var color = new Vector4(1, 1, 0, ColorAlpha);
        var arrowScale = new Vector2(1, 3);
        if (uint.Parse(ev["DataId"]?.ToString() ?? "0") == 18746)
        {
            scale = 1.5f;
            color = new Vector4(0, 1, 1, ColorAlpha);
            arrowScale = new Vector2(1, 2); 
        }

        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"orbitalflame-{ev.SourceId}";
        dp.Color = color;
        dp.Owner = ev.SourceId;
        dp.Scale = new Vector2(scale);
        dp.DestoryAt = int.MaxValue;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

        var dp1 = sa.Data.GetDefaultDrawProperties();
        dp1.Name = $"orbitalflame arraw-{ev.SourceId}";
        dp1.Color = sa.Data.DefaultSafeColor;
        dp1.Owner = ev.SourceId;
        dp1.Scale = arrowScale;
        dp1.DestoryAt = int.MaxValue;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp1);
    }

    [ScriptMethod(name: "旋绕之火 - orbital flame", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:regex:^(18747|18746)$"], userControl: false)]
    public void orbitalflameClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"orbitalflame-{ev.SourceId}");
        sa.Method.RemoveDraw($"orbitalflame arraw-{ev.SourceId}");
    }


    [ScriptMethod(name: "洪水 - Flood", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44390)$"])]
    public void Flood(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircleObject(sa, ev.SourceId, new Vector2(20f), 6000, $"Tornado-Danger:{ev.SourceId}", scaleByTime: false, color: new Vector4(1, 0, 0, ColorAlpha));
    }

    #endregion

    #region 优先级字典 类
    public class PriorityDict
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public ScriptAccessory sa { get; set; } = null!;
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public Dictionary<int, int> Priorities { get; set; } = null!;
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
        return ParseHexId(ev["Index"], out var index) ? index : 0;
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

    public static bool HasStatus(this ScriptAccessory sa, IBattleChara? battleChara, uint statusId)
    {
        if (battleChara == null || !battleChara.IsValid()) return false;
        unsafe
        {
            BattleChara* charaStruct = (BattleChara*)battleChara.Address;
            var statusIdx = charaStruct->GetStatusManager()->GetStatusIndex(statusId);
            return statusIdx != -1;
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
        dp.Color = isSafe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
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
            0, 0, DrawModeEnum.Default, DrawTypeEnum.Circle, isSafe, byTime, false, draw);

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

    public static void DrawCircle(ScriptAccessory accessory, Vector3 position, Vector2 scale, int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0, DrawModeEnum drawmode = DrawModeEnum.Default)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Circle, dp);
    }

    public static void DrawDisplacement(ScriptAccessory accessory, Vector3 targetPos, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = accessory.Data.Me;
        dp.Color = color ?? accessory.Data.DefaultSafeColor;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.TargetPosition = targetPos;
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

    public static void DrawDisplacementObject(ScriptAccessory accessory, ulong target, Vector2 scale, int duration, string name, float rotation, Vector4? color = null, int delay = 0, bool fix = false, DrawModeEnum drawmode = DrawModeEnum.Imgui)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = accessory.Data.Me;
        dp.Color = color ?? accessory.Data.DefaultSafeColor;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.TargetObject = target;
        dp.Scale = scale;
        dp.Rotation = rotation;
        dp.Delay = delay;
        dp.FixRotation = fix;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Displacement, dp);
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

    public static void DrawRectObjectNoTarget(ScriptAccessory accessory, ulong owner, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0, ScaleMode scalemode = ScaleMode.None, Vector3? offset = null)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scalemode;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    public static void DrawRectPosNoTarget(ScriptAccessory accessory, Vector3 pos, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0, ScaleMode scalemode = ScaleMode.None, Vector3? offset = null, DrawModeEnum drawMode = DrawModeEnum.Default)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scalemode;
        dp.Offset = offset ?? new Vector3(0, 0, 0);
        accessory.Method.SendDraw(drawMode, DrawTypeEnum.Rect, dp);
    }
    public static void DrawRectObjectTarget(ScriptAccessory accessory, ulong owner, ulong target, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0, ScaleMode scalemode = ScaleMode.None)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.TargetObject = target;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scalemode;
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

    public static void DrawFanObject(ScriptAccessory accessory, ulong owner, float rotation, Vector2 scale, float angle, int duration, string name, Vector4? color = null, int delay = 0, bool scaleByTime = true, bool fix = false, Vector3? offset = null)
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
        dp.Offset = offset ?? new Vector3(0, 0, 0);
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

    public static void DrawDount(ScriptAccessory accessory, Vector3 position, Vector2 scale, Vector2 innerscale, int duration, string name, Vector4? color = null, bool scaleByTime = true, int delay = 0, DrawModeEnum drawmode = DrawModeEnum.Default)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.Radian = 2 * float.Pi;
        dp.Scale = scale;
        dp.InnerScale = innerscale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        if (scaleByTime) dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(drawmode, DrawTypeEnum.Donut, dp);
    }
}


#endregion 函数集

#region 扩展方法
public static class ExtensionMethods
{
    public static float Round(this float value, float precision) => MathF.Round(value / precision) * precision;

    public static Vector3 ToDirection(this float rotation)
    {
        return new Vector3(
            MathF.Sin(rotation),
            0f,
            MathF.Cos(rotation)
        );
    }

    public static Vector3 Quantized(this Vector3 position, float gridSize = 1f)
    {
        return new Vector3(
            MathF.Round(position.X / gridSize) * gridSize,
            MathF.Round(position.Y / gridSize) * gridSize,
            MathF.Round(position.Z / gridSize) * gridSize
        );
    }

    /// <summary>
    /// 获取玩家的职业名称或简称
    /// </summary>
    public static string GetPlayerJob(
        this ScriptAccessory sa,
        IPlayerCharacter? playerObject,
        bool fullName = false
    )
    {
        if (playerObject == null) return "None";
        return fullName
            ? playerObject.ClassJob.Value.Name.ToString()
            : playerObject.ClassJob.Value.Abbreviation.ToString();
    }

    /// <summary>
    /// 判断玩家是否是坦克职业
    /// </summary>
    public static bool IsTank(IPlayerCharacter? playerObject)
    {
        if (playerObject == null) return false;
        return playerObject.ClassJob.Value.Role == 1;
    }
}

public unsafe static class ExtensionVisibleMethod
{
    public static bool IsCharacterVisible(this ICharacter chr)
    {
        var v = (IntPtr)(((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)chr.Address)->GameObject.DrawObject);
        if (v == IntPtr.Zero) return false;
        return Bitmask.IsBitSet(*(byte*)(v + 136), 0);
    }

    public static class Bitmask
    {
        public static bool IsBitSet(ulong b, int pos)
        {
            return (b & (1UL << pos)) != 0;
        }

        public static void SetBit(ref ulong b, int pos)
        {
            b |= 1UL << pos;
        }

        public static void ResetBit(ref ulong b, int pos)
        {
            b &= ~(1UL << pos);
        }

        public static bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }

        public static bool IsBitSet(short b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }
    }
}
#endregion
