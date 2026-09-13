using System;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using System.Reflection.Metadata;
using System.Net;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using System.Runtime.Intrinsics.Arm;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace Veever.EndWalker.theTowerofZot;

[ScriptType(name: "LV.81 异形楼阁佐特塔", territorys: [952], guid: "98a97134-f87b-4386-aad9-2a99e81794ab",
    version: "0.0.0.5", author: "Veever", note: noteStr)]

public class the_Tower_of_Zot
{
    const string noteStr =
    """
    v0.0.0.5:
    1. 现在支持文字横幅/TTS开关
    2. 标点开关以及本地开关都在用户设置里面，可自行选择关闭或者开启（默认本地开启）
    3. Boss1击杀后会生成门指路，绿圈显示为击杀小怪后会打开的门（防止晕头转向）
    4. Boss2标记目前有bug删除不掉，但是正常dps的话到那里boss也快死了（不正常dps一个攻击1的标点也不影响），所以有缘会修
    5. 强烈建议默认本地标点，因为是秒标记(不拟人)
    6. 建议关闭cactbot播报功能以防止重复播报
    鸭门。
    """;
    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = false;

    [UserSetting("标点开关")]
    public bool isMark { get; set; } = true;

    [UserSetting("本地标点开关(打开则为本地开关，关闭则为小队)")]
    public bool LocalMark { get; set; } = true;

    [UserSetting("Debug开关, 非开发用请关闭")]
    public bool isDebug { get; set; } = false;

    public int berserkerSpheresCount;
    public int PrakamyaSiddhiCount;
    public int DeltaFireIIICount;

    private readonly object berserkerSpheresLock = new object();
    private readonly object DeltaFireIIILock = new object();
    private bool isDRTTS = false;

    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        accessory.Method.MarkClear();
        berserkerSpheresCount = 0;
        PrakamyaSiddhiCount = 0;
        DeltaFireIIICount = 0;
    }

    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    #region 小怪
    [ScriptMethod(name: "安神毒气", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27874"])]
    public async void SoporificGas(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"安神毒气";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(9f);
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp1 = accessory.Data.GetDefaultDrawProperties();
        dp1.Name = $"安神毒气描边";
        dp1.Scale = new(9f);
        dp1.InnerScale = new(8.98f);
        dp1.Radian = float.Pi * 2;
        dp1.Color = new Vector4(178 / 255.0f, 34 / 255.0f, 34 / 255.0f, 10.0f);
        dp1.Owner = @event.SourceId();
        dp1.DestoryAt = 4000;
        dp1.Radian = 2 * float.Pi;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp1);

        if (isText) accessory.Method.TextInfo("钢铁, 可打断", duration: 4000, true);
        accessory.TTS("钢铁, 可打断", isTTS, isDRTTS);
    }



    [ScriptMethod(name: "魔导汽油弹", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:24138"])]
    public void GarleanFire(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "魔导汽油弹";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(8f);
        dp.Radian = float.Pi / 180 * 90;
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "左臂斩击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:24140"])]
    public void LeftArmSlash(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "左臂斩击";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(10f);
        dp.Radian = float.Pi / 180 * 90;
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Boss1 - Boss2 指路", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:13294"])]
    public async void Navi(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(50);
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Navi1";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Position = new Vector3(-300.14f, -185.02f, 159.79f);
        dp.Scale = new Vector2(1.55f);
        dp.DestoryAt = long.MaxValue;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp1 = accessory.Data.GetDefaultDrawProperties();
        dp1.Name = "Navi2";
        dp1.Color = accessory.Data.DefaultSafeColor;
        dp1.Position = new Vector3(-330.10f, -181.00f, 82.42f);
        dp1.Scale = new Vector2(1.55f);
        dp1.DestoryAt = long.MaxValue;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = "Navi3";
        dp2.Color = accessory.Data.DefaultSafeColor;
        dp2.Position = new Vector3(-238.35f, -172.03f, 60.46f);
        dp2.Scale = new Vector2(1.55f);
        dp2.DestoryAt = long.MaxValue;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
    }

    [ScriptMethod(name: "删除 Boss1 - Boss2 指路", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:13295"])]
    public void DelNavi(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "扩散射线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:24147"])]
    public void DiffusionRay(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"扩散射线";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = @event.EffectPosition(); 
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }


    #endregion

    #region Boss1
    [ScriptMethod(name: "Boss1死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25248"])]
    public void Boss1Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("带毒死刑准备", duration: 4000, true);
        accessory.TTS("带毒死刑准备", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "人趣冰封", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25238"])]
    public void ManusyaBlizzardIII(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "人趣冰封";
        //dp.Color = accessory.Data.DefaultDangerColor;
        dp.Color = new Vector4(0 / 255.0f, 255 / 255.0f, 255 / 255.0f, 1f);
        //dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(40f);
        dp.Radian = float.Pi / 180 * 20;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "人趣爆炎", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25237"])]
    public void ManusyaFireIII(Event @event, ScriptAccessory accessory)
    {
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = $"人趣爆炎(单月环)";
        dp2.Color = accessory.Data.DefaultSafeColor;
        dp2.Owner = @event.SourceId();
        dp2.Scale = new Vector2(5);
        dp2.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);

        var dp3 = accessory.Data.GetDefaultDrawProperties();
        dp3.Name = $"人趣爆炎指路";
        dp3.Owner = accessory.Data.Me;
        dp3.Color = accessory.Data.DefaultSafeColor;
        dp3.ScaleMode |= ScaleMode.YByDistance;
        dp3.TargetPosition = @event.SourcePosition();
        dp3.Scale = new(2);
        dp3.DestoryAt = 2500;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
    }

    [ScriptMethod(name: "人趣暴雷", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25239"])]
    public void ManusyaThunderIII(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"人趣暴雷";
        dp.Color = new Vector4(123 / 255.0f, 104 / 255.0f, 238 / 255.0f, 2f);
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(3);
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "人趣剧毒菌", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25240"])]
    public void ManusyaBioIII(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"人趣剧毒菌";
        dp.Color = new Vector4(50 / 255.0f, 205 / 255.0f, 50 / 255.0f, 1.5f);
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(40);
        dp.Radian = float.Pi / 180 * 180;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        if (isText) accessory.Method.TextInfo("去Boss身后", duration: 4000, true);
        accessory.TTS("去Boss身后", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "Boss1死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25242"])]
    public void TransmuteFireIII(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("准备目标点月环, 先靠近Boss注意下一个机制", duration: 3000, true);
        accessory.TTS("准备目标点月环, 先靠近Boss注意下一个机制", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "德鲁帕德", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25244"])]
    public void Dhrupad(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("类AOE, 奶妈注意团血", duration: 4000, true);
        accessory.TTS("类AOE, 奶妈注意团血", isTTS, isDRTTS);
    }

    #endregion

    #region Boss2
    [ScriptMethod(name: "Boss2死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25257"])]
    public void Boss2Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 3700, true);
        accessory.TTS("死刑准备", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "身所达", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25256"])]
    public void PraptiSiddhi(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "身所达";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(4, 40);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Delay = 400;
        dp.DestoryAt = 1600;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "狂暴晶球钢铁", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:13296"])]
    public void berserkerSpheres(Event @event, ScriptAccessory accessory)
    {
        lock (berserkerSpheresLock)
        {
            if (berserkerSpheresCount == 0)
            {
                if (isText) accessory.Method.TextInfo("去安全区", duration: 6000, true);
                accessory.TTS("去安全区", isTTS, isDRTTS);
            }
            if (berserkerSpheresCount == 5)
            {
                if (isText) accessory.Method.TextInfo("去安全区, 准备攻击Boss", duration: 6000, true);
                accessory.TTS("去安全区, 准备攻击Boss", isTTS, isDRTTS);
            }
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "狂暴晶球钢铁";
            //dp.Color = accessory.Data.DefaultDangerColor;
            dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            dp.Position = @event.SourcePosition();
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Scale = new Vector2(15f);
            dp.DestoryAt = (berserkerSpheresCount <= 4) ? 11501 : 20300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            DebugMsg($"berserkerSpheresCount: {berserkerSpheresCount}", accessory);
            berserkerSpheresCount++;
        }
    }

    [ScriptMethod(name: "标记Boss", eventType: EventTypeEnum.MorelogCompat, eventCondition: ["Id:0197", "SourceDataId:13295"])]
    public async void markBoss(Event @event, ScriptAccessory accessory)
    {
        DebugMsg("markBoss", accessory);
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
    }

    [ScriptMethod(name: "大愿成", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25251"])]
    public void PrakamyaSiddhi(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("钢铁, 远离Boss", duration: 3700, true);
        accessory.TTS("钢铁, 远离Boss", isTTS, isDRTTS);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "狂暴晶球钢铁";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(5f);
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (PrakamyaSiddhiCount > 0)
        {
            DebugMsg("clear markBoss", accessory);
            accessory.Method.MarkClear();
            DebugMsg("Finish clear markBoss", accessory);
        }
        PrakamyaSiddhiCount++;
    }

    [ScriptMethod(name: "人趣停止", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25255"])]
    public async void ManusyaStop(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(3500);
        if (isText) accessory.Method.TextInfo("分散，不要站在同一条直线上", duration: 3700, true);
        accessory.TTS("分散，不要站在同一条直线上", isTTS, isDRTTS);
    }
    #endregion

    #region Boss3
    [ScriptMethod(name: "三角爆炎分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25265"])]
    public void DeltaFireIII(Event @event, ScriptAccessory accessory)
    {
        lock (DeltaFireIIILock)
        {
            if (DeltaFireIIICount == 0)
            {
                if (isText) accessory.Method.TextInfo("先月环, 再分散", duration: 4000, true);
                accessory.TTS("先月环, 再分散", isTTS, isDRTTS);
            }

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"三角爆炎分散";
            dp.Color = new Vector4(255 / 255.0f, 0 / 255.0f, 251 / 255.0f, 1.0f);
            dp.Owner = @event.TargetId();
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Scale = new Vector2(6);
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "Boss3身所达", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25275"])]
    public void Boss3PraptiSiddhi(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Boss3身所达";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(4, 40);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Delay = 400;
        dp.DestoryAt = 1600;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "Boss3死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(25274|25280)$"])]
    public void Boss3Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑 + 类AOE, 奶妈注意团血", duration: 4000, true);
        accessory.TTS("死刑加类AOE, 奶妈注意团血", isTTS, isDRTTS);
    }

    // 瞎眼睛, 删掉
    //[ScriptMethod(name: "三角暴雷", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(25272|25269|25270)$"])]
    //public void DeltaThunderIII(Event @event, ScriptAccessory accessory)
    //{
    //    var dp = accessory.Data.GetDefaultDrawProperties();
    //    dp.Name = $"三角暴雷";
    //    dp.Color = new Vector4(123 / 255.0f, 104 / 255.0f, 238 / 255.0f, 2f);
    //    dp.Position = @event.EffectPosition();
    //    dp.Scale = new Vector2(3);
    //    dp.DestoryAt = 4000;
    //    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    //}

    // 每次aoe和死刑都在一起，so合并
    //[ScriptMethod(name: "Boss3德鲁帕德", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25281"])]
    //public void Boss3Dhrupad(Event @event, ScriptAccessory accessory)
    //{
    //    if (isText) accessory.Method.TextInfo("类AOE, 奶妈注意团血", duration: 4000, true);
    //    accessory.TTS("类AOE, 奶妈注意团血", isTTS, isDRTTS);
    //}

    [ScriptMethod(name: "三角爆炎月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25263"])]
    public void DeltaFire1III(Event @event, ScriptAccessory accessory)
    {
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = $"三角爆炎(单月环)";
        dp2.Color = accessory.Data.DefaultSafeColor;
        dp2.Position = @event.EffectPosition();
        dp2.Scale = new Vector2(5);
        dp2.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);

        var dp3 = accessory.Data.GetDefaultDrawProperties();
        dp3.Name = $"三角爆炎指路";
        dp3.Owner = accessory.Data.Me;
        dp3.Color = accessory.Data.DefaultSafeColor;
        dp3.ScaleMode |= ScaleMode.YByDistance;
        dp3.TargetPosition = @event.EffectPosition();
        dp3.Scale = new(2);
        dp3.DestoryAt = 2500;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
    }

    [ScriptMethod(name: "Boss3分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25272"])]
    public void Boss3Stack(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Boss3分摊";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 5000;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (isText) accessory.Method.TextInfo($"与{tname}分摊", duration: 5000, true);
        accessory.TTS($"与{tname}分摊", isTTS, isDRTTS);
    }
    #endregion
}


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

    public static uint ActionId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
    }

    public static uint SourceId(this Event @event)
    {
        return ParseHexId(@event["SourceId"], out var id) ? id : 0;
    }

    public static uint SourceDataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["SourceDataId"]);
    }

    public static uint DataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DataId"]);
    }

    public static uint Command(this Event @event)
    {
        return ParseHexId(@event["Command"], out var cid) ? cid : 0;
    }

    public static string DurationMilliseconds(this Event @event)
    {
        return JsonConvert.DeserializeObject<string>(@event["DurationMilliseconds"]) ?? string.Empty;
    }

    public static float SourceRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
    }

    public static float TargetRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["TargetRotation"]);
    }

    public static byte Index(this Event @event)
    {
        return (byte)(ParseHexId(@event["Index"], out var index) ? index : 0);
    }

    public static uint State(this Event @event)
    {
        return ParseHexId(@event["State"], out var state) ? state : 0;
    }

    public static string SourceName(this Event @event)
    {
        return @event["SourceName"];
    }

    public static string TargetName(this Event @event)
    {
        return @event["TargetName"];
    }

    public static uint TargetId(this Event @event)
    {
        return ParseHexId(@event["TargetId"], out var id) ? id : 0;
    }

    public static Vector3 SourcePosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    public static Vector3 TargetPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
    }

    public static Vector3 EffectPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
    }

    public static uint DirectorId(this Event @event)
    {
        return ParseHexId(@event["DirectorId"], out var id) ? id : 0;
    }

    public static uint StatusId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StatusId"]);
    }

    public static uint StackCount(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StackCount"]);
    }

    public static uint Param(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["Param"]);
    }
}


public static class Extensions
{
    public static void TTS(this ScriptAccessory accessory, string text, bool isTTS, bool isDRTTS)
    {
        accessory.Method.TTS(text);
    }
}

