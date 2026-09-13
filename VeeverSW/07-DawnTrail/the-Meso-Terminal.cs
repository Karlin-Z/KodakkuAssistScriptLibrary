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

namespace Veever.DawnTrail.TheMesoTerminal;

[ScriptType(name: Name, territorys: [1292], guid: "c74e8153-46e1-4a18-97bd-250463781ed2",
    version: Version, author: "Veever", note: NoteStr, updateInfo: UpdateInfo)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$
// ^\[\w+\|[^|]+\|E\]\s\w+


public class TheMesoTerminal
{
    const string NoteStr =
    """
    v0.0.0.5
    1. 如果需要某个机制的绘画或者哪里出了问题请在dc@我或者私信我
    2. Boss3 NPC聚集在一起的圆圈绘画如果掉线可能会导致不画
    鸭门
    ------------------------------
    1. If you need a draw for a mechanic or notice any issues, @ me on DC or DM me.
    2. Boss 3: The circle draw for Bombardment may fail to draw if you disconnect
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

    private const string Name = "LV.100 永久幽界中央终端 [the Meso Terminal]";
    private const string Version = "0.0.0.5";
    private const string DebugVersion = "a";

    private const bool Debugging = true;

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

    //[UserSetting("是否自动使用防击退(Auto anti-knockback)")]
    //public bool useaction { get; set; } = true;

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

    private Dictionary<ulong, ulong> _tethersDict = new();
    private static bool _initHint = false;

    private List<ulong> _terrorList = new();
    private readonly object CountLock = new object();

    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    public void Init(ScriptAccessory sa)
    {
        sa.Log.Debug($"脚本 {Name} v{Version}{DebugVersion} 完成初始化.");
        sa.Method.RemoveDraw(".*");

        _terrorList.Clear();
    }




    private static IGameObject? GetBossObject(ScriptAccessory sa)
    {
        return sa.GetByDataId(BossDataId).FirstOrDefault();
    }

    #region 小怪
    [ScriptMethod(name: "---- 小怪-Mobs ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
    userControl: Debugging)]
    public void mobs(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "电流冲击-Electric Shock", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44040"])]
    public void ElectricShock(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircleObject(sa, ev.SourceId, new Vector2(5f), 4000, $"ElectricShock-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "DarkMist Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:44040"], userControl: false)]
    public void ElectricShockClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"ElectricShock-{ev.SourceId}");
    }

    [ScriptMethod(name: "王国重力-Alexandrian Gravity", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44041"])]
    public void AlexandrianGravity(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(6f), 2700, $"Alexandrian Gravity-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Alexandrian Gravity Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:44041"], userControl: false)]
    public void AlexandrianGravityClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Alexandrian Gravity-{ev.SourceId}");
    }

    [ScriptMethod(name: "振动波-Pressure Wave", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44044"])]
    public void PressureWave(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawFanOwner(sa, ev.SourceId, 0, new Vector2(6f), 120, 2700, $"PressureWave-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Pressure Wave Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:44044"], userControl: false)]
    public void PressureWaveClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"PressureWave-{ev.SourceId}");
    }

    [ScriptMethod(name: "飞刀乱掷 & 超压斧-Rusted Knives & Overpower", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4404[56])$"])]
    public void RustedKnives_Overpower(Event ev, ScriptAccessory sa)
    {
        DebugMsg($"In RustedKnives_Overpower, ActionId: {ev.ActionId}", sa);
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(5f), 2700, $"Rusted Knives & Overpower-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "Rusted Knives & Overpower Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:regex:^(4404[56])$"], userControl: false)]
    public void RustedKnives_OverpowerClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Rusted Knives & Overpower-{ev.SourceId}");
    }

    [ScriptMethod(name: "雷转质射线-Electray", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44048)$"])]
    public void Electray(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(4, 14), 3700, $"Electray-{ev.SourceId}", scalemode: ScaleMode.ByTime);
    }

    [ScriptMethod(name: "Electray Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:regex:^(44048)$"], userControl: false)]
    public void ElectrayClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Electray-{ev.SourceId}");
    }

    [ScriptMethod(name: "信仰之剑-Steelforged Belief", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45042)$"])]
    public void SteelforgedBelief(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(6f), 3700, $"Steelforged Belief-{ev.SourceId}", sa.Data.DefaultDangerColor);
    }

    [ScriptMethod(name: "SteelforgedBelief Clear", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:regex:^(45042)$"], userControl: false)]
    public void SteelforgedBeliefClear(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"Steelforged Belief-{ev.SourceId}");
    }
    #endregion


    #region Boss1
    [ScriptMethod(name: "---- Boss1 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void Boss1(Event ev, ScriptAccessory sa)
    {
    }
    [ScriptMethod(name: "广域撒药-Medicine Field", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43798"])]
    public void MedicineField(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "大AOE" : "Heavy AOE";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "爆炸药液-Pungent Aerosol", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43807"])]
    public void PungentAerosol(Event ev, ScriptAccessory sa)
    {
        //DrawHelper.DrawDisplacementObject(sa, ev.TargetId, new Vector2(1, 24f), 5200, $"Pungent Aerosol-{ev.SourceId}", , fix: true, drawmode: DrawModeEnum.Default);
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "MesmerizingMelody";
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.TargetPosition = ev.EffectPosition;
        dp.Rotation = float.Pi;
        dp.Scale = new Vector2(2, 24f);
        dp.DestoryAt = 4000;
        if (isLead) sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

    }

    [ScriptMethod(name: "正面喷雾-Biochemical Front", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43802"])]
    public void BiochemicalFront(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "远离正面" : "Avoid front";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.Method.TTS($"{msg}");

        DrawHelper.DrawFanOwner(sa, ev.SourceId, 0, new Vector2(40), 180, 4700, $"Biochemical Front-{ev.SourceId}");

    }

    [ScriptMethod(name: "高压缩药弹-Concentrated Dose", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43799"])]
    public void ConcentratedDose(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "Dot死刑点名" : "Dot Tankbuster";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "圆状散布-Sterile Sphere", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4380[56])$"])]
    public void SterileSphere(Event ev, ScriptAccessory sa)
    {
        // 43805 big 15f
        // 43806 small 8f
        DebugMsg($"In SterileSphere, ActionId: {ev.ActionId}", sa);
        if (ev.ActionId == 43805)
        {
            DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(15f), 5200, $"Sterile Sphere-{ev.SourceId}", new Vector4(1, 0, 0, ColorAlpha), drawmode: DrawModeEnum.Imgui);
        }

        if (ev.ActionId == 43806)
        {
            DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(8f), 5200, $"Sterile Sphere-{ev.SourceId}", new Vector4(1, 0, 0, ColorAlpha), drawmode: DrawModeEnum.Imgui);
        }
    }




    #endregion


    #region Boss2
    [ScriptMethod(name: "---- Boss2 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
    userControl: Debugging)]
    public void Boss2(Event ev, ScriptAccessory sa)
    {
    }


    [ScriptMethod(name: "AOE提示 - AOE Notify", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43578"], suppress: 5000)]
    public void boss2AOE(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "AOE" : "AOE";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 2700, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "Boss连线检测和标记 - Boss Thther Check & mark", eventType: EventTypeEnum.Tether, eventCondition: ["Id:00F9"])]
    public void bossTetherCheck(Event ev, ScriptAccessory sa)
    {
        if (ev.TargetId == sa.Data.Me)
        {
            string msg = language == Language.Chinese ? "攻击连线Boss" : "Attack tethered Boss";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 2700, true);
            if (isTTS) sa.Method.TTS($"{msg}");
            if (isMark) sa.Method.MarkClear();
            if (isMark) sa.Method.Mark((uint)ev.SourceId, MarkType.Attack1, LocalMark);
        }
    }

    [ScriptMethod(name: "斩足刃 - Dismemberment", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43587"])]
    public void Dismemberment(Event ev, ScriptAccessory sa)
    {
        DebugMsg($"In Dismemberment, SourceId: {ev.SourceId}, TargetId: {ev.TargetId}", sa);
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"Dismemberment: {ev.SourceId}";
        dp.Color = new Vector4(1, 1, 0, 1);
        dp.Owner = ev.SourceId;
        dp.Scale = new Vector2(4, 16);
        dp.DestoryAt = 5700;
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "断罪的雷剑绘制 - Peal of Judgment Draw", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:43594"])]
    public void PealofJudgment(Event ev, ScriptAccessory sa)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"Peal of Judgment: {ev.SourceId}";
        dp.Color = new Vector4(1, 0, 1, 1);
        dp.Owner = ev.SourceId;
        dp.Scale = new Vector2(4, 2);
        dp.DestoryAt = 500;
        dp.Offset = new Vector3(0, 0, 1);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        // TODO
        var dp1 = sa.Data.GetDefaultDrawProperties();
        dp1.Name = $"Peal of Judgment arraw: {ev.SourceId}";
        dp1.Color = sa.Data.DefaultSafeColor;
        dp1.Owner = ev.SourceId;
        dp1.Scale = new Vector2(1, 2);
        dp1.DestoryAt = 500;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp1);
    }


    [ScriptMethod(name: "月环断 - Execution Wheel", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43596"])]
    public void ExecutionWheel(Event ev, ScriptAccessory sa)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"Execution Wheel: {ev.SourceId}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Owner = ev.SourceId;
        dp.Scale = new Vector2(9);
        dp.InnerScale = new Vector2(4);
        dp.Radian = 2 * float.Pi;
        dp.DestoryAt = 3200;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }


    [ScriptMethod(name: "圆月断 - Execution Wheel", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43595"])]
    public void ChoppingBlock(Event ev, ScriptAccessory sa)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"Chopping Block: {ev.SourceId}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Owner = ev.SourceId;
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 3200;
        dp.ScaleMode = ScaleMode.ByTime;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "断罪的铁球 - Flaying Flail", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43592"])]
    public void FlayingFlail(Event ev, ScriptAccessory sa)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = $"Flaying Flail: {ev.SourceId}";
        dp.Color = sa.Data.DefaultDangerColor;
        dp.Position = ev.EffectPosition;
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 4700;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "破碎弹 - Will Breaker", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44856"])]
    public void WillBreaker(Event ev, ScriptAccessory sa)
    {
        if (ev.TargetId == sa.Data.Me)
        {
            string msg = language == Language.Chinese ? "打断Boss" : "Interrupt!";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 4000, true);
            if (isTTS) sa.Method.TTS($"{msg}");
        }

    }

    [ScriptMethod(name: "死刑宣告 - Death Penalty", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43588"])]
    public async void DeathPenalty(Event ev, ScriptAccessory sa)
    {
        if (ev.TargetId == sa.Data.Me)
        {
            await Task.Delay(4500);
            string msg = language == Language.Chinese ? "驱散自身死宣" : "Esuna Doom";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 4000, true);
            if (isTTS) sa.Method.TTS($"{msg}");
        }

    }

    [ScriptMethod(name: "焦热刑具标记 - Hellmaker mark", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:18642"])]
    public async void Hellmakermark(Event ev, ScriptAccessory sa)
    {
        Vector3 pos = ev.SourcePosition;
        Vector3 mypos = sa.Data.MyObject?.Position ?? Vector3.Zero;

        Vector3 testpos = new Vector3(69.15f, -490.00f, -258.32f);

        var distance = Vector3.Distance(pos, mypos);
        
        var testdistance = Vector3.Distance(pos, testpos);
        DebugMsg($"In Hellmakermark, distance: {testdistance}", sa);

        DebugMsg($"pos: {pos}, mypos: {mypos}", sa);
        DebugMsg($"In Hellmakermark, distance: {distance}", sa);

        if (distance <= 18)
        {
            string msg = language == Language.Chinese ? "攻击焦热刑具" : "Attack Hellmaker";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 4000, true);
            if (isTTS) sa.Method.TTS($"{msg}");
            if (isMark) sa.Method.MarkClear();
            if (isMark) sa.Method.Mark((uint)ev.SourceId, MarkType.Attack1, LocalMark);
        }

    }


    #endregion

    #region Boss3
    [ScriptMethod(name: "---- Boss3 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
    userControl: Debugging)]
    public void Boss3(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "AOE提示 - AOE Notify", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43825"], suppress: 5000)]
    public void boss3AOE(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "AOE" : "AOE";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "雷转质射线 - Electray", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43810"])]
    public void Boss3Electray(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(8, 45), 4700, $"Electray-{ev.SourceId}", scalemode: ScaleMode.ByTime);
        sa.Method.RemoveDraw($"Bombardment");
    }


    [ScriptMethod(name: "小怪出现 - Terror Spawn", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:18624"])]
    public void TerrorSpawn(Event ev, ScriptAccessory sa)
    {
        lock (CountLock)
        {
            if (!_terrorList.Contains(ev.SourceId))
            {
                _terrorList.Add(ev.SourceId);
                DebugMsg($"Terror spawned: {ev.SourceId}, total: {_terrorList.Count}", sa);
            }
        }
    }


    [ScriptMethod(name: "小怪消失 - Terror Despawn", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:18624"])]
    public void TerrorDespawn(Event ev, ScriptAccessory sa)
    {
        _terrorList.Remove(ev.SourceId);
        DebugMsg($"Terror despawned: {ev.SourceId}, total: {_terrorList.Count}", sa);
    }


    public static bool AlmostEqual(float a, float b, float eps) => Math.Abs(a - b) <= eps;
    public static bool AlmostEqual(Vector3 a, Vector3 b, float eps) => Vector3.Distance(a, b) <= eps;

    [ScriptMethod(name: "轰炸机制 - Bombardment", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(1870[5-8])$"])]
    public void Bombardment(Event ev, ScriptAccessory sa)
    {
        DebugMsg($"In Bombardment, terrorList count: {_terrorList.Count}", sa);
        //if (BombardmentLock) return;
        if (ExtensionVisibleMethod.IsCharacterVisible((ICharacter)IbcHelper.GetByDataId(sa, 18623).FirstOrDefault())) return;
        DebugMsg($"No return", sa);
        if (_terrorList.Count > 6)
        {
            var count = _terrorList.Count;
            var pos = ev.SourcePosition;
            var big = false;

            for (int i = 0; i < count; i++)
            {
                var t = IbcHelper.GetById(sa, _terrorList[i]);
                if (t?.EntityId == ev.SourceId)
                {
                    continue;
                }
                if (t != null && AlmostEqual(t.Position, pos, 6f))
                {
                    sa.Log.Debug($"tid: {t.GameObjectId}, tpos: {t.Position}, pos: {pos}, visible : {ExtensionVisibleMethod.IsCharacterVisible((ICharacter)t)}");
                    if (!ExtensionVisibleMethod.IsCharacterVisible((ICharacter)t)) continue;
                    big = true;
                    sa.Log.Debug($"isbig: tid: {t.GameObjectId}, tpos: {t.Position}, pos: {pos}, visible : {ExtensionVisibleMethod.IsCharacterVisible((ICharacter)t)}");
                    break;
                }
            }

            DebugMsg($"In Bombardment, count: {count}, pos: {pos}, big: {big}", sa);

            Vector2 size = big ? new Vector2(14f) : new Vector2(3f);

            Vector3 position = big ? pos + 3.5f * ExtensionMethods.ToDirection(ExtensionMethods.Round(ev.SourceRotation, 1f)) : pos;

            var dp = sa.Data.GetDefaultDrawProperties();
            dp.Name = "Bombardment";
            dp.Color = sa.Data.DefaultDangerColor;
            dp.Position = ExtensionMethods.Quantized(position);
            dp.Rotation = float.Pi;
            dp.Scale = size;
            dp.DestoryAt = 9700;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
    }

    // // 清理Bombardment AOE
    // [ScriptMethod(name: "Bombardment Clear", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43811"])]
    // public void BombardmentClear(Event ev, ScriptAccessory sa)
    // {
    //     sa.Method.RemoveDraw("Bombardment.*");
    //     _bombardmentAOEs.Clear();
    //     _bombardmentBig.Clear();
    //     _terrorList.Clear();

    //     DebugMsg("Bombardment cleared", sa);
    // }


    [ScriptMethod(name: "印象-Impression", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43818"])]
    public void Impression(Event ev, ScriptAccessory sa)
    {
        //DrawHelper.DrawDisplacementObject(sa, ev.TargetId, new Vector2(1, 24f), 5200, $"Pungent Aerosol-{ev.SourceId}", , fix: true, drawmode: DrawModeEnum.Default);
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = "Impression-knock";
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.TargetPosition = ev.EffectPosition;
        dp.Rotation = float.Pi;
        dp.Scale = new Vector2(2, 11f);
        dp.DestoryAt = 4700;
        if (isLead) sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        DrawHelper.DrawCircle(sa, ev.EffectPosition, new Vector2(10f), 4700, "Impression-circle", color: new Vector4(1, 0, 0, ColorAlpha));
    }


    [ScriptMethod(name: "记忆爆炸-Memory of the Pyre", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43824"])]
    public void MemoryofthePyre(Event ev, ScriptAccessory sa)
    {
        string msg = language == Language.Chinese ? "死刑点名" : "Tankbuster";
        if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
        if (isTTS) sa.Method.TTS($"{msg}");
    }

    [ScriptMethod(name: "雷光-Keraunography", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43813"])]
    public void Keraunography(Event ev, ScriptAccessory sa)
    {
        DrawHelper.DrawRectObjectNoTarget(sa, ev.SourceId, new Vector2(20, 60), 3700, $"Keraunography-{ev.SourceId}");
    }

    [ScriptMethod(name: "慌乱-Turmoil", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4381[45])$"])]
    public void Turmoil(Event ev, ScriptAccessory sa)
    {
        switch (ev.ActionId)
        {
            // 左
            case 43814:
                {
                    DrawHelper.DrawFanOwner(sa, ev.SourceId, (-float.Pi / 2), new Vector2(50f), 180, 4700, $"Turmoil-{ev.SourceId}", scaleByTime: false);
                    string msg = language == Language.Chinese ? "去右侧" : "Go Right";
                    if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
                    if (isTTS) sa.Method.TTS($"{msg}");
                    break;
                }
            // 右
            case 43815:
                {
                    DrawHelper.DrawFanOwner(sa, ev.SourceId, (float.Pi / 2), new Vector2(50f), 180, 4700, $"Turmoil-{ev.SourceId}", scaleByTime: false);
                    string msg = language == Language.Chinese ? "去左侧" : "Go Left";
                    if (isText) sa.Method.TextInfo($"{msg}", duration: 4700, true);
                    if (isTTS) sa.Method.TTS($"{msg}");
                    break;
                }
        }

    }

    [ScriptMethod(name: "分摊", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(020D)$"])]
    public void MemoryoftheStorm(Event ev, ScriptAccessory sa)
    {
        string tname = ev["TargetName"]?.ToString() ?? "未知目标";
        if (ev.TargetId == sa.Data.Me)
        {
            string msg = language == Language.Chinese ? "分摊点名" : "Stack";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 4000, true);
            if (isTTS) sa.Method.TTS($"{msg}");
        } else
        {
            string msg = language == Language.Chinese ? $"与{tname}分摊" : $"Stack with {tname}";
            if (isText) sa.Method.TextInfo($"{msg}", duration: 4000, true);
            if (isTTS) sa.Method.TTS($"{msg}");
        }

        DrawHelper.DrawRectObjectTarget(sa, ev.SourceId, ev.TargetId, new Vector2(8, 40), 5000, "MemoryoftheStorm", color: sa.Data.DefaultSafeColor);

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

    public static void DrawRectObjectNoTarget(ScriptAccessory accessory, ulong owner, Vector2 scale, int duration, string name, Vector4? color = null, int delay = 0, ScaleMode scalemode = ScaleMode.None)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.Scale = scale;
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = scalemode;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
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

