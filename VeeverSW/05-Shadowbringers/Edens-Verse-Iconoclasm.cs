using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.UI;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using Lumina.Data.Structs;
using Newtonsoft.Json;
using System;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace Veever.Shadowbringers.Edens_Verse_Iconoclasm;
//^(?!.*((武僧|机工士|龙骑士|学者|舞者|蝰蛇剑士|暗黑骑士|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast|Cancel|Add))).*$

[ScriptType(name: "LV.80 伊甸希望乐园 共鸣之章3", territorys: [904], guid: "7732767d-bfb3-4c96-9719-962ba10cec08",
    version: "0.0.0.3", author: "Veever", note: noteStr, updateInfo: UpdateInfo)]

public class Edens_Verse_Iconoclasm
{
    const string noteStr =
    """
    v0.0.0.3:
    1. 此脚本只用了2个arr回放进行测试，如果出现错画的问题，请dc@我并提供arr回放
    鸭门。
    """;

    const string UpdateInfo =
    $"""
    v0.0.0.3
    1. 修复了传送门的一些绘制错误问题
    2. 修复了光暗buff不绘制的问题
    3. 新增支持时间轴
    鸭门
    ----------------------------------

    Duckmen.
    """;

    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;

    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = false;

    [UserSetting("Debug开关, 非开发用请关闭")]
    public bool isDebug { get; set; } = false;

    public int LightsCourseCount;
    public int StackCount;

    #region TimeLine
    [ScriptTimeline("T1")]
    const string T1Timeline = """
            11.145 info text="StartCasting 虚无波动" duration=0
            22.343 info text="StartCasting 暗光钉" duration=0
            37.603 info text="StartCasting 波状号令" duration=0
            44.807 info text="StartCasting 白光奔流" duration=0
            47.833 info text="StartCasting 白光奔流" duration=0
            68.017 info text="StartCasting 白光奔流" duration=0
            68.017 info text="StartCasting 白光奔流" duration=0
            71.099 info text="StartCasting 白光奔流" duration=0
            71.099 info text="StartCasting 白光奔流" duration=0
            100.217 info text="StartCasting 白光奔流" duration=0
            100.217 info text="StartCasting 白光奔流" duration=0
            103.279 info text="StartCasting 白光奔流" duration=0
            103.279 info text="StartCasting 白光奔流" duration=0
            124.139 info text="StartCasting 强制传送" duration=0
            141.253 info text="StartCasting 薄暮机动" duration=0
            144.315 info text="StartCasting 黑暗之剑" duration=0
            148.383 info text="StartCasting 白光之锤" duration=0
            156.395 info text="StartCasting 虚无波动" duration=0
            163.009 info text="StartCasting 暗光钉" duration=0
            174.137 info text="StartCasting 属性变动" duration=0
            184.287 info text="StartCasting 夜袭号令" duration=0
            191.547 info text="StartCasting 黑暗奔流" duration=0
            191.547 info text="StartCasting 白光奔流" duration=0
            203.583 info text="StartCasting 黑暗奔流" duration=0
            203.583 info text="StartCasting 白光奔流" duration=0
            210.661 info text="StartCasting 黑暗奔流" duration=0
            210.661 info text="StartCasting 白光奔流" duration=0
            236.215 info text="StartCasting 强制传送" duration=2.7
            243.157 info text="StartCasting 攻击机动" duration=0
            261.855 info text="StartCasting 白光之矢" duration=0
            264.993 info text="StartCasting 白光之矢" duration=0
            278.993 info text="StartCasting 虚无泛滥" duration=6.7
            296.499 info text="StartCasting 属性变动" duration=0
            308.079 info text="StartCasting 夜袭号令" duration=0
            315.867 info text="StartCasting 黑暗奔流" duration=0
            315.867 info text="StartCasting 白光奔流" duration=0
            """;

    [ScriptTimelineI18n(ScriptTimelineLang.ChineseSimplified)]
    static readonly Dictionary<string, string> T1I18n = new()
    {
        ["StartCasting 夜袭号令"] = "StartCasting 夜袭号令",
        ["StartCasting 属性变动"] = "StartCasting 属性变动",
        ["StartCasting 强制传送"] = "StartCasting 强制传送",
        ["StartCasting 攻击机动"] = "StartCasting 攻击机动",
        ["StartCasting 暗光钉"] = "StartCasting 暗光钉",
        ["StartCasting 波状号令"] = "StartCasting 波状号令",
        ["StartCasting 白光之矢"] = "StartCasting 白光之矢",
        ["StartCasting 白光之锤"] = "StartCasting 白光之锤",
        ["StartCasting 白光奔流"] = "StartCasting 白光奔流",
        ["StartCasting 薄暮机动"] = "StartCasting 薄暮机动",
        ["StartCasting 虚无泛滥"] = "StartCasting 虚无泛滥",
        ["StartCasting 虚无波动"] = "StartCasting 虚无波动",
        ["StartCasting 黑暗之剑"] = "StartCasting 黑暗之剑",
        ["StartCasting 黑暗奔流"] = "StartCasting 黑暗奔流",
    };

    [ScriptTimelineI18n(ScriptTimelineLang.ChineseTraditional)]
    static readonly Dictionary<string, string> T1I18n_ChineseTraditional = new()
    {
        ["StartCasting 夜袭号令"] = "StartCasting 夜袭号令",
        ["StartCasting 属性变动"] = "StartCasting 属性变动",
        ["StartCasting 强制传送"] = "StartCasting 强制传送",
        ["StartCasting 攻击机动"] = "StartCasting 攻击机动",
        ["StartCasting 暗光钉"] = "StartCasting 暗光钉",
        ["StartCasting 波状号令"] = "StartCasting 波状号令",
        ["StartCasting 白光之矢"] = "StartCasting 白光之矢",
        ["StartCasting 白光之锤"] = "StartCasting 白光之锤",
        ["StartCasting 白光奔流"] = "StartCasting 白光奔流",
        ["StartCasting 薄暮机动"] = "StartCasting 薄暮机动",
        ["StartCasting 虚无泛滥"] = "StartCasting 虚无泛滥",
        ["StartCasting 虚无波动"] = "StartCasting 虚无波动",
        ["StartCasting 黑暗之剑"] = "StartCasting 黑暗之剑",
        ["StartCasting 黑暗奔流"] = "StartCasting 黑暗奔流",
    };

    [ScriptTimelineI18n(ScriptTimelineLang.Japanese)]
    static readonly Dictionary<string, string> T1I18n_Japanese = new()
    {
        ["StartCasting 夜袭号令"] = "StartCasting 夜袭号令",
        ["StartCasting 属性变动"] = "StartCasting 属性变动",
        ["StartCasting 强制传送"] = "StartCasting 强制传送",
        ["StartCasting 攻击机动"] = "StartCasting 攻击机动",
        ["StartCasting 暗光钉"] = "StartCasting 暗光钉",
        ["StartCasting 波状号令"] = "StartCasting 波状号令",
        ["StartCasting 白光之矢"] = "StartCasting 白光之矢",
        ["StartCasting 白光之锤"] = "StartCasting 白光之锤",
        ["StartCasting 白光奔流"] = "StartCasting 白光奔流",
        ["StartCasting 薄暮机动"] = "StartCasting 薄暮机动",
        ["StartCasting 虚无泛滥"] = "StartCasting 虚无泛滥",
        ["StartCasting 虚无波动"] = "StartCasting 虚无波动",
        ["StartCasting 黑暗之剑"] = "StartCasting 黑暗之剑",
        ["StartCasting 黑暗奔流"] = "StartCasting 黑暗奔流",
    };
    #endregion
    public List<Vector3> BallCheckList = new List<Vector3>
    {
        //From N (left to Right)  
        new Vector3(85.00f, 0.00f, 75.00f),    //[0] yes
        new Vector3(95.00f, 0.00f, 75.00f),    //[1] yes
        new Vector3(105.00f, 0.00f, 75.00f),    //[2] yes
        new Vector3(115.00f, 0.00f, 75.00f),    //[3] yes
    };

    public List<Vector3> BallStartList = new List<Vector3>
    {
        //From N (left to Right)  
        new Vector3(85.00f, 0.00f, 80.00f),    //[0] yes
        new Vector3(95.00f, 0.00f, 80.00f),    //[1] yes
        new Vector3(105.00f, 0.00f, 80.00f),    //[2] yes
        new Vector3(115.00f, 0.00f, 80.00f),    //[3] yes

        new Vector3(80.00f, 0.00f, 115.00f),    //[4]
        new Vector3(80.00f, 0.00f, 105.00f),    //[5]
        new Vector3(80.00f, 0.00f, 95.00f),    //[6]
        new Vector3(80.00f, 0.00f, 85.00f),    //[7]
    };

    public List<Vector3> centerPoint = new List<Vector3>
    {
        //From N (left to Right)  
        new Vector3(85.00f, 0.00f, 100.00f),    //[0]   yes
        new Vector3(95.00f, 0.00f, 100.00f),    //[1] yes
        new Vector3(105.00f, 0.00f, 100.00f),    //[2] yes
        new Vector3(115.00f, 0.00f, 100.00f),    //[3] yes

        //From W (left to Right) 
        new Vector3(100.00f, 0.00f, 115.00f),    //[4]
        new Vector3(100.00f, 0.00f, 105.00f),    //[5]
        new Vector3(100.00f, 0.00f, 95.00f),    //[6]
        new Vector3(100.00f, 0.00f, 85.00f),    //[7]
    };

    private readonly object LightsCourseLock = new object();
    private int DarkStatus;

    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        LightsCourseCount = 0;
        StackCount = 0;
        DarkStatus = 0;
    }
 
    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    [ScriptMethod(name: "debug", eventType: EventTypeEnum.Chat, eventCondition: ["Message:debug"])]
    public void debug(Event @event, ScriptAccessory accessory) {
        DebugMsg($"Dark: {DarkStatus}", accessory);
        DebugMsg($"RealLightsCourseCount: {LightsCourseCount}", accessory);
    }


    [ScriptMethod(name: "虚无波动", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(19538|20052)$"])]
    public void EmptyWave(Event @event, ScriptAccessory accessory)
    {
        if (@event.ActionId() == 19538) 
        {
            if (isText) accessory.Method.TextInfo("AOE", duration: 3500, true);
        } else
        {
            if (isText) accessory.Method.TextInfo("大AOE", duration: 3500, true);
        }

    }

    [ScriptMethod(name: "暗光钉", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0025"])]
    public void UnshadowedStake(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            if (isText) accessory.Method.TextInfo($"引导激光至场外", duration: 4500, true);
        } else
        {
            if (isText) accessory.Method.TextInfo($"远离连线点名", duration: 4500, true);
        }

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Unshadowed Stake";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = @event.SourceId();
        dp.TargetObject = @event.TargetId();
        dp.Scale = new Vector2(4, 80);
        dp.DestoryAt = 4700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "白光&黑暗奔流", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(19516|20067|19518|19517|19491|19490|19521)$"])]
    public void LightsAndDarknessCourse(Event @event, ScriptAccessory accessory)
    {
        lock (LightsCourseLock)
        {
            DebugMsg($"LightsCourseCount: {LightsCourseCount}", accessory);
            if (LightsCourseCount == 0 || LightsCourseCount == 1)
            {
                var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 0 & 1");
                DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 4000, 
                                    "Light's Course 0 & 1 SAFE", new Vector4(0, 1, 0, 1), 6800);
            }

            if (LightsCourseCount == 2 || LightsCourseCount == 3)
            {
                var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 2 & 3");
            }

            if (LightsCourseCount == 4 || LightsCourseCount == 5)
            {
                DebugMsg($"{LightsCourseCount}: {@event.ActionId()}", accessory);
                if (@event["ActionId"] == "20067") 
                {
                    DebugMsg($"{LightsCourseCount}: In 4 & 5 20067", accessory);
                    var index0 = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                    DebugMsg($"{index0}: index", accessory);
                    if (index0 == 1) 
                    {
                        DrawHelper.DrawRect(accessory, BallStartList[5], centerPoint[5], new Vector2(10, 50), 5500, "1 Light's Course 4 & 5", delay: 6700);
                    } else if (index0 == 2)
                    {
                        DrawHelper.DrawRect(accessory, BallStartList[4], centerPoint[4], new Vector2(10, 50), 6700, "2 Light's Course 4 & 5", delay: 6700);
                    }
                    DebugMsg($"is drawing 20067", accessory);
                    DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index0], new Vector2(10, 25), 6700, "3 Light's Course 4 & 5");
                    DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index0], new Vector2(10, 25), 4000, 
                                    "1 Light's Course 4 & 5 SAFE", new Vector4(0, 1, 0, 1), 6800);
                } else 
                {
                    DebugMsg($"{LightsCourseCount}: In 4 & 5 else", accessory);
                    var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                    DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "4 Light's Course 4 & 5");
                    DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 4000, 
                                    "2 Light's Course 4 & 5 SAFE", new Vector4(0, 1, 0, 1), 6800);
                }
            }

            if (LightsCourseCount == 6 || LightsCourseCount == 7)
            {
                DebugMsg($"{LightsCourseCount}: {@event.ActionId()}", accessory);
                if (@event["ActionId"] == "20067") 
                {
                    DebugMsg($"{LightsCourseCount}: In 20067", accessory);
                    var index0 = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                    if (index0 == 1) 
                    {
                        DrawHelper.DrawRect(accessory, BallStartList[5], centerPoint[5], new Vector2(10, 50), 5500, "Light's Course 6 & 7", delay: 6700);
                    } else if (index0 == 2)
                    {
                        DrawHelper.DrawRect(accessory, BallStartList[4], centerPoint[4], new Vector2(10, 50), 6700, "Light's Course 6 & 7", delay: 6700);
                    }
                    DebugMsg($"is drawing 20067", accessory);
                    DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index0], new Vector2(10, 25), 6700, "Light's Course 6 & 7");
                } else 
                {
                    var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                    DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 6 & 7");
                }
            }

            if (LightsCourseCount == 8 || LightsCourseCount == 9 || LightsCourseCount == 10 || LightsCourseCount == 11)
            {
                DebugMsg($"{LightsCourseCount}: {@event.ActionId()}", accessory);
                if (LightsCourseCount == 11)
                {
                    accessory.Method.TextInfo("优先躲避红色", duration: 4700, true);
                }
                if (@event["ActionId"] == "19518") 
                {
                    DebugMsg($"{LightsCourseCount}: In 19518", accessory);
                    var index0 = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                    if (index0 == 1) 
                    {
                        if (LightsCourseCount == 8 || LightsCourseCount == 9)
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[5], centerPoint[5], new Vector2(10, 50), 6700, "1 - 5:5 Light's Course 8 & 9 & 10 & 11", new Vector4(1, 0, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[7], centerPoint[7], new Vector2(10, 50), 6700, "1 - 7:7 Light's Course 8 & 9 & 10 & 11", new Vector4(1, 0, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[5], centerPoint[5], new Vector2(10, 50), 2000, "1 - 5:5 Light's Course 8 & 9 & 10 & 11 SAFE", new Vector4(0, 1, 0, 1), delay: 13400);
                            DrawHelper.DrawRect(accessory, BallStartList[7], centerPoint[7], new Vector2(10, 50), 2000, "1 - 7:7 Light's Course 8 & 9 & 10 & 11 SAFE", new Vector4(0, 1, 0, 1), delay: 13400);
                        } else 
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[5], centerPoint[5], new Vector2(10, 50), 6700, "1 - 5:5 Light's Course 10 & 11", delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[7], centerPoint[7], new Vector2(10, 50), 6700, "1 - 7:7 Light's Course 10 & 11", delay: 6700);
                        }
                    } else if (index0 == 2)
                    {
                        if (LightsCourseCount == 8 || LightsCourseCount == 9)
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[4], centerPoint[4], new Vector2(10, 50), 6700, "2 - 4:4 Light's Course 8 & 9", new Vector4(1, 0, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[6], centerPoint[6], new Vector2(10, 50), 6700, "2 - 6:6 Light's Course 8 & 9", new Vector4(1, 0, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[4], centerPoint[4], new Vector2(10, 50), 2000, "2 - 4:4 Light's Course 8 & 9 SAFE", new Vector4(0, 1, 0, 1), delay: 13400);
                            DrawHelper.DrawRect(accessory, BallStartList[6], centerPoint[6], new Vector2(10, 50), 2000, "2 - 6:6 Light's Course 8 & 9 SAFE", new Vector4(0, 1, 0, 1), delay: 13400);
                        } else 
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[6], centerPoint[6], new Vector2(10, 50), 6700, "2 - 6:6 Light's Course 10 & 11", delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[4], centerPoint[4], new Vector2(10, 50), 6700, "2 - 4:4 Light's Course 10 & 11", delay: 6700);
                        }
                    }
                    var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                    DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 8 & 9 & 10 & 11");
                    if (LightsCourseCount == 8 || LightsCourseCount == 9) 
                    {
                        DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 2000, 
                                    "Light's Course 8 & 9 & 10 & 11 SAFE", new Vector4(0, 1, 0, 1), 6800);
                    }
                } else 
                {
                    var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                    DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 8 & 9 & 10 & 11");
                    if (LightsCourseCount == 8 || LightsCourseCount == 9) 
                    {
                        DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 2000, 
                                    "Light's Course 8 & 9 & 10 & 11 SAFE", new Vector4(0, 1, 0, 1), 6800);
                    }
                }
            }

            if (LightsCourseCount == 12 || LightsCourseCount == 13 || LightsCourseCount == 14 || 
                LightsCourseCount == 15 || LightsCourseCount == 16 || 
                LightsCourseCount == 17 || LightsCourseCount == 18 || LightsCourseCount == 19 ||
                LightsCourseCount == 20 || LightsCourseCount == 21 || LightsCourseCount == 22 || 
                LightsCourseCount == 23 || LightsCourseCount == 28 || LightsCourseCount == 29 ||
                LightsCourseCount == 30 || LightsCourseCount == 31)
            {
                // 2238 Light     //19516 & 19490     // 0x8BE      DarkStatus == 0
                // 2239 Darkness  //19517 & 19491     // 0x8BF      DarkStatus == 1
                DebugMsg($"{LightsCourseCount}: {@event.ActionId()}", accessory);

                DebugMsg($"DarkStatus: {DarkStatus}", accessory);
                if (@event["ActionId"] == "19516" || @event["ActionId"] == "19490") 
                {
                    DebugMsg($"{LightsCourseCount}: In 19516", accessory);
                    if (DarkStatus == 0)
                    {
                        var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                        DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 5000, "Light's Course 12|3456789", new Vector4(1, 0, 0, 1));
                    } else 
                    {
                        var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                        DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 5000, "Light's Course 12|3456789", new Vector4(0, 1, 0, 1));
                    }
                } else if (@event["ActionId"] == "19517" || @event["ActionId"] == "19491")
                {
                    DebugMsg($"{LightsCourseCount}: In 19517", accessory);
                    if (DarkStatus == 1)
                    {
                        var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                        DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 5000, "Light's Course 12|3456789", new Vector4(1, 0, 0, 1));
                    } else 
                    {
                        var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                        DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 5000, "Light's Course 12|3456789", new Vector4(0, 1, 0, 1));
                    }
                }
            }

            if (LightsCourseCount == 24 || LightsCourseCount == 25 || LightsCourseCount == 26 || LightsCourseCount == 27)
            {
                // 2238 Light     //19516 & 19490     // 0x8BE
                // 2239 Darkness  //19517 & 19491 & 19521     // 0x8BF
                DebugMsg($"{LightsCourseCount}: {@event.ActionId()}", accessory);

                DebugMsg($"DarkStatus: {DarkStatus}", accessory);
                if (@event["ActionId"] == "19521")
                {
                    if (DarkStatus == 1)
                    {
                        var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                        DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1));
                        if (index == 1) 
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[5], centerPoint[5], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[7], centerPoint[7], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1), delay: 6700);
                        } else if (index == 2)
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[4], centerPoint[4], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[6], centerPoint[6], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1), delay: 6700);
                        }
                    } else
                    {
                        var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                        DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1));
                        if (index == 1) 
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[5], centerPoint[5], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[7], centerPoint[7], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1), delay: 6700);
                        } else if (index == 2)
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[4], centerPoint[4], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[6], centerPoint[6], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1), delay: 6700);
                        }
                    }
                } else if (@event["ActionId"] == "19516")
                {
                    if (DarkStatus == 0)
                    {
                        var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                        DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1));
                        if (index == 1) 
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[5], centerPoint[5], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[7], centerPoint[7], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1), delay: 6700);
                        } else if (index == 2)
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[4], centerPoint[4], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[6], centerPoint[6], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1), delay: 6700);
                        }
                    } else 
                    {
                        var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                        DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1));
                        if (index == 1) 
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[5], centerPoint[5], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[7], centerPoint[7], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1), delay: 6700);
                        } else if (index == 2)
                        {
                            DrawHelper.DrawRect(accessory, BallStartList[4], centerPoint[4], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1), delay: 6700);
                            DrawHelper.DrawRect(accessory, BallStartList[6], centerPoint[6], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1), delay: 6700);
                        }
                    }
                } else 
                {
                    if (@event["ActionId"] == "19517")
                    {
                        if (DarkStatus == 1)
                        {
                            var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                            DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1));
                        } else 
                        {
                            var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                            DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1));
                        }
                    }
                    if (@event["ActionId"] == "19518")
                    {
                        if (DarkStatus == 1)
                        {
                            var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                            DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(0, 1, 0, 1));
                        } else 
                        {
                            var index = FindExactPositionIndex(@event.SourcePosition(), BallCheckList);
                            DrawHelper.DrawRect(accessory, @event.SourcePosition(), centerPoint[index], new Vector2(10, 50), 6700, "Light's Course 24, 25, 26, 27", new Vector4(1, 0, 0, 1));
                        }
                    }

                }
            }
            
            LightsCourseCount++;
            DebugMsg($"LightsCourseCount increased to: {LightsCourseCount}", accessory);
        }
    }

    [ScriptMethod(name: "偏属性记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(2239|2238)$"], userControl: false)]
    public void RecordStatus(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId != accessory.Data.Me) return;
        DarkStatus = @event["StatusID"].Equals(2239) ? 1 : 0;
    }




    [ScriptMethod(name: "强制传送", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(2242|2243|2240|2241)$"])]
    public void ForcedTransfer(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Forced Transfer";
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = @event.TargetId();
            dp.Scale = new Vector2(1, 15);
            switch (@event["StatusID"])
            {
                case "2240":
                    dp.Rotation = 0;
                    break;
                case "2241":
                    dp.Rotation = float.Pi;
                    break;
                case "2242":
                    dp.Rotation = float.Pi / 180 * 90;
                    break;
                case "2243":
                    dp.Rotation = float.Pi / 180 * 270;
                    break;
            }
            dp.DestoryAt = 10000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
        }
    }

    private int FindExactPositionIndex(Vector3 position, List<Vector3> positionList)
    {
        if (positionList == null || positionList.Count == 0)
            return -1;

        for (int i = 0; i < positionList.Count; i++)
        {
            if (position.X == positionList[i].X &&
                position.Y == positionList[i].Y &&
                position.Z == positionList[i].Z)
            {
                return i;
            }
        }
        return -1;
    }

    [ScriptMethod(name: "分组分摊", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:003E"])]
    public async void Stack(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(50);
        if (StackCount == 0) 
        {
            accessory.Method.TextInfo("分组分摊", duration: 4700, true);
            accessory.Method.TTS("分组分摊");
        }
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Stack";
        dp.Color = new Vector4(0, 1, 1, 1);
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        StackCount++;
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

    public static void DrawFan(ScriptAccessory accessory, Vector3 position, float rotation, Vector2 scale, float angle, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = position;
        dp.Rotation = rotation;
        dp.Scale = scale;
        dp.Radian = angle;
        dp.Delay = delay;
        dp.DestoryAt = duration;
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

    public static void DrawArrow(ScriptAccessory accessory, Vector3 startPosition, Vector3 endPosition, float width, int duration, string name, Vector4? color = null, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color ?? accessory.Data.DefaultDangerColor;
        dp.Position = startPosition;
        dp.TargetPosition = endPosition;
        dp.Scale = new Vector2(width, 1);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Arrow, dp);
    }
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

    public static uint Id(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["Id"]);
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

    public static string Operate(this Event @event)
    {
        return @event["Operate"];
    }
}

public static class IbcHelper
{
    public static IBattleChara? GetById(ScriptAccessory accessory, uint id)
    {
        return (IBattleChara?)accessory.Data.Objects.SearchByEntityId(id);
    }

    public static IBattleChara? GetMe(ScriptAccessory accessory)
    {
        return accessory.Data.Objects.SearchByEntityId(accessory.Data.Me) as IBattleChara;
    }

    public static KodakkuAssist.Data.IGameObject? GetFirstByDataId(ScriptAccessory accessory, uint dataId)
    {
        return accessory.Data.Objects.Where(x => x.DataId == dataId).FirstOrDefault();
    }

    public static IEnumerable<KodakkuAssist.Data.IGameObject> GetByDataId(ScriptAccessory accessory, uint dataId)
    {
        return accessory.Data.Objects.Where(x => x.DataId == dataId);
    }

    public static IBattleChara? GetByEntityId(ScriptAccessory accessory, uint id)
    {
        return (IBattleChara?)accessory.Data.Objects.SearchByEntityId(id);
    }
    public static bool HasStatus(this IBattleChara chara, uint statusId)
    {
        return chara.StatusList.Any(x => x.StatusId == statusId);
    }

    public static bool HasStatus(this IBattleChara chara, uint[] statusIds)
    {
        return chara.StatusList.Any(x => statusIds.Contains(x.StatusId));
    }
}

