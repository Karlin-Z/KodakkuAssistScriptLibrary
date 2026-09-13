using System;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using System.Reflection.Metadata;
using System.Net;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using System.Runtime.Intrinsics.Arm;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using FFXIVClientStructs;
using System;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Veever.Shadowbringers.theGrandCosmos;

[ScriptType(name: "LV.80 魔法宫殿宇宙宫", territorys: [884], guid: "b3a2febd-73ff-44a9-a897-22fa50c74ff3",
    version: "0.0.0.4", author: "Veever", note: noteStr)]

public class the_Grand_Cosmos
{
    const string noteStr =
    """
    v0.0.0.4:
    1. 现在支持文字横幅/TTS开关
    2. 标点开关以及本地开关都在用户设置里面，可自行选择关闭或者开启（默认本地开启）
    3. 有生之年!可能会!添加新的扫帚紫圈的判定方式
    4. 有生之年!可能会!添加新的Boss2种子的判定方式
    5. Boss3标记物品水晶灯没标, 目标太大了都能看得见
    6. 支持副本点位指路，红线为引战范围
    7. 如果感觉指路点位不够的话请在dc频道说一下或者dc私聊我(24个点位总能够了吧（x）)
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

    public int DarkShockTTSCount;
    public int magicBroomPosCount;
    public int Boss2SeedsNotifyCount;

    public bool Boss2WindisWest;

    private readonly object DarkShockTTSLock = new object();
    private readonly object magicBroomPosLock = new object();
    private bool isDRTTS = false;

    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        accessory.Method.MarkClear();
        DarkShockTTSCount = 0;
        magicBroomPosCount = 0;
        Boss2WindisWest = false;
        Boss2SeedsNotifyCount = 0;
    }

    public void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!isDebug) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }


    #region 指路
    [ScriptMethod(name: "指路1组", eventType: EventTypeEnum.Director, eventCondition: ["Command:40000001", "Instance:80030049"])]
    public async void Group1(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(1000);
        DebugMsg("In Group1", accessory);
        var pos = new Vector3(0, 0, 329.23f);
        List<Vector3> List1 = new List<Vector3>
        {
            new Vector3(-13.92f, 0.00f, 326.36f),
            new Vector3(-74.22f, 0.00f, 320.13f),
            new Vector3(-105.53f, 0.05f, 300.47f),
            new Vector3(-107.94f, 0.00f, 284.64f),
            new Vector3(-101.72f, 0.00f, 274.01f),
            new Vector3(-58.48f, 0.00f, 280.89f),
            new Vector3(-51.44f, 0.00f, 292.14f),
            new Vector3(-23.44f, 0.00f, 285.24f),
            new Vector3(-0.30f, 0.00f, 237.67f),
        };
        var toPosColor = new Vector4(1.0f, 1.0f, 0.0f, 4.0f);

        FastDp(accessory, "1-1", List1[0], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "1-2", List1[1], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "1-3", List1[2], toPosColor);
        FastDp(accessory, "1-4", List1[3], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "1-5", List1[4], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "1-6", List1[5], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "1-7", List1[6], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "1-8", List1[7], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "1-9", List1[8], toPosColor);

    }
    [ScriptMethod(name: "指路1组删除+指路2组", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:11290"])]
    public async void delGroup1Add2(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        await Task.Delay(1000);
        var toPosColor = new Vector4(1.0f, 1.0f, 0.0f, 4.0f);

        List<Vector3> List2 = new List<Vector3>
        {
            new Vector3(13.34f, -7.00f, 115.41f),
            new Vector3(64.77f, -13.98f, 35.54f),
            new Vector3(-0.40f, -14.00f, -7.85f),
        };

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "Line1";
        dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 5.0f);
        //dp.Color = new Vector4();
        dp.Position = new Vector3(20, -7, 100);
        dp.TargetPosition = new Vector3(20, -7, 130);
        dp.Scale = new Vector2(6, 30f);
        dp.DestoryAt = long.MaxValue;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);

        var dp1 = accessory.Data.GetDefaultDrawProperties();
        dp1.Name = "Line2";
        dp1.Color = new Vector4(1.0f, 0.0f, 0.0f, 5.0f);
        //dp.Color = new Vector4();
        dp1.Position = new Vector3(92, -14, 23);
        dp1.TargetPosition = new Vector3(104, -14, 23);
        dp1.Scale = new Vector2(6, 15f);
        dp1.DestoryAt = long.MaxValue;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp1);

        FastDp(accessory, "2-1", List2[0], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "2-2", List2[1], toPosColor);
        FastDp(accessory, "2-2", List2[2], toPosColor);
    }

    [ScriptMethod(name: "指路2组删除+指路3组", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:11268"])]
    public async void delGroup2Add3(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        await Task.Delay(1000);
        var toPosColor = new Vector4(1.0f, 1.0f, 0.0f, 4.0f);

        List<Vector3> List3 = new List<Vector3>
        {
            new Vector3(-0.14f, -4.00f, -125.28f),
            new Vector3(28.01f, -3.99f, -159.18f),
            new Vector3(-27.77f, -3.99f, -158.92f),
            new Vector3(9.72f, 8.00f, -198.45f),
            new Vector3(42.29f, 8.00f, -192.16f),
            new Vector3(47.17f, 8.00f, -182.42f),
            new Vector3(78.04f, 8.00f, -186.34f),
            new Vector3(78.41f, 8.00f, -207.17f),
            new Vector3(32.57f, 8.00f, -219.26f),
            new Vector3(30.73f, 8.05f, -231.61f),
            new Vector3(13.86f, 8.00f, -239.76f),
            new Vector3(0.66f, 8.00f, -288.16f),
        };
        FastDp(accessory, "3-1", List3[0], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "3-2", List3[1], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "3-3", List3[2], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "3-4", List3[3], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "3-5", List3[4], toPosColor);
        FastDp(accessory, "3-6", List3[5], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "3-7", List3[6], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "3-8", List3[7], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "3-9", List3[8], toPosColor);
        FastDp(accessory, "3-10", List3[9], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "3-11", List3[10], accessory.Data.DefaultSafeColor);
        FastDp(accessory, "3-12", List3[11], accessory.Data.DefaultSafeColor);
    }

    public void FastDp(ScriptAccessory accessory, string name, Vector3 position, Vector4 color)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color;
        //dp.Color = new Vector4();
        dp.Position = position;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(2);
        dp.DestoryAt = long.MaxValue;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "删除绘制0", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:1223", "SourcePosition:{\"X\":-0.02,\"Y\":7.98,\"Z\":-355.00}"])]
    public async void delDraw0(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "删除绘制1", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:11283"])]
    public async void delDraw1(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
    }
    #endregion
    #region 小怪
    [ScriptMethod(name: "浓云密布", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18720"])]
    public void Cloudcover(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "浓云密布";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Position = @event.EffectPosition();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "愤怒一击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18721"])]
    public void SmiteofRage(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "愤怒一击";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(5f,4f);
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "钢铁正义", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18719"])]
    public void IronJustice(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "钢铁正义";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(11f);
        dp.Radian = float.Pi / 180 * 120;
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "自爆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18725"])]
    public void SelfDestruct(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "自爆";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(7);
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "忘忧毒液", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18728"])]
    public void NepenthicPlunge(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "忘忧毒液";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(10f);
        dp.Radian = float.Pi / 180 * 90;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "鼻息风暴", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18729"])]
    public void BrewingStorm(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "鼻息风暴";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Owner = @event.SourceId();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(5f);
        dp.Radian = float.Pi / 180 * 60;
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "幻影骑士哈蒙斯救疗眩晕提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18737"]),]
    public async void RonkanCureII(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("眩晕哈蒙斯", duration: 4000, true);
        accessory.TTS("眩晕哈蒙斯", isTTS, isDRTTS);
        if (isMark) accessory.Method.Mark(@event.SourceId(), KodakkuAssist.Module.GameOperate.MarkType.Bind1, LocalMark);
        await Task.Delay(5000);
        accessory.Method.MarkClear();
    }
    #endregion


    #region Boss1
    [ScriptMethod(name: "Boss1死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18281"])]
    public void Boss1Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4000, true);
        accessory.TTS("死刑准备", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "Boss1AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18851"])]
    public void Boss1AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 3000, true);
        accessory.TTS("AOE", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "Boss1分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18282"])]
    public void Boss1Stack(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Boss1分摊";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 5000;
        //dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (isText) accessory.Method.TextInfo($"与{tname}分摊", duration: 5000, true);
        accessory.TTS($"与{tname}分摊", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "黑暗爆碎", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0060"])]
    public void DarkWell(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            if (isText) accessory.Method.TextInfo("全体分散", duration: 5000, true);
            accessory.TTS("全体分散", isTTS, isDRTTS);
        }

        //var dp = accessory.Data.GetDefaultDrawProperties();
        //dp.Name = $"黑暗爆碎";
        ////dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        //dp.Owner = @event.TargetId();
        //dp.ScaleMode = ScaleMode.ByTime;
        //dp.Scale = new Vector2(5);
        //dp.DestoryAt = 5000;
        //accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "黑暗冲击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18287"])]
    public void DarkShock(Event @event, ScriptAccessory accessory)
    {
        if (DarkShockTTSCount == 10)
        {
            if (isText) accessory.Method.TextInfo("躲避黄圈", duration: 3000, true);
            accessory.TTS("躲避黄圈", isTTS, isDRTTS);
        }
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"黑暗冲击{DarkShockTTSCount}";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = @event.EffectPosition();
        //dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    //List<Vector3> TribulationList = new List<Vector3>();
    [ScriptMethod(name: "苦难", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18852"])]
    public void Tribulation(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"苦难";
        dp.Color = new Vector4(148 / 255.0f, 0 / 255.0f, 211 / 255.0f, 1.0f);
        dp.Position = @event.EffectPosition();  
        //dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(3);
        dp.DestoryAt = 8500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp1 = accessory.Data.GetDefaultDrawProperties();
        dp1.Name = $"苦难2";
        dp1.Color = accessory.Data.DefaultDangerColor;
        dp1.Position = @event.EffectPosition();
        //dp1.ScaleMode = ScaleMode.ByTime;
        dp1.Scale = new Vector2(6.5f);
        dp1.Delay = 13000;
        dp1.DestoryAt = 9500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);

        Task.Delay(9000);
        lock (DarkShockTTSLock)
        {
            DebugMsg($"DarkShockTTSCount = {DarkShockTTSCount}", accessory);
            if (DarkShockTTSCount == 0)
            {
                if (isText) accessory.Method.TextInfo("集合引导黄圈，随后躲避", duration: 3000, true);
                accessory.TTS("集合引导黄圈，随后躲避", isTTS, isDRTTS);
            }
            DarkShockTTSCount++;
        }

        ////DebugMsg($"苦难pos: {@event.EffectPosition()}", accessory);
        //List<float> TribulationZAxisList = new List<float>
        //{
        //    198.99f,    //z[0]   from S to N
        //    192.98f,    //z[1]
        //    187f,       //z[2]
        //    180.99f,    //z[3]
        //    174.98f     //z[4]
        //};

        //DebugMsg($"start check", accessory);
        //DebugMsg($"z: {@event.EffectPosition().Z}", accessory);
        //if (@event.EffectPosition().Z == TribulationZAxisList[0])
        //{
        //    TribulationList[0] = @event.EffectPosition();
        //} 
        //else if (@event.EffectPosition().Z == TribulationZAxisList[1])
        //{
        //    TribulationList[1] = @event.EffectPosition();
        //} 
        //else if (@event.EffectPosition().Z == TribulationZAxisList[2])
        //{
        //    TribulationList[2] = @event.EffectPosition();
        //}
        //else if (@event.EffectPosition().Z == TribulationZAxisList[3])
        //{
        //    TribulationList[3] = @event.EffectPosition();
        //}
        //else if (@event.EffectPosition().Z == TribulationZAxisList[4])
        //{
        //    TribulationList[4] = @event.EffectPosition();
        //}
        //DebugMsg($"after check", accessory);
        //for (int i = 0; i < TribulationList.Count; i++)
        //{
        //    DebugMsg($"苦难pos[{i}]: {TribulationList[i]}", accessory);
        //}
    }

    //[ScriptMethod(name: "debug 扫帚pos", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:getpos"])]
    //public async void debugPos(Event @event, ScriptAccessory accessory)
    //{
    //    if (isDebug)
    //    {
    //        DebugMsg("geting pos", accessory);
    //        for(var i = 0; i <= 1; i++)
    //        {
    //            await Task.Delay(1000);
    //            var pos1 = IbcHelper.GetById(0x40001EF3);
    //            DebugMsg($"{i}pos[0]: {pos1.Position}", accessory);
    //        }

    //        for (var i = 0; i <= 1; i++)
    //        {
    //            await Task.Delay(1000);
    //            var pos1 = IbcHelper.GetById(0x40001EF4);
    //            DebugMsg($"{i}pos[1]: {pos1.Position}", accessory);
    //        }

    //        for (var i = 0; i <= 1; i++)
    //        {
    //            await Task.Delay(1000);
    //            var pos1 = IbcHelper.GetById(0x40001EF5);
    //            DebugMsg($"{i}pos[2]: {pos1.Position}", accessory);
    //        }

    //        for (var i = 0; i <= 1; i++)
    //        {
    //            await Task.Delay(1000);
    //            var pos1 = IbcHelper.GetById(0x40001EF6);
    //            DebugMsg($"{i}pos[3]: {pos1.Position}", accessory);
    //        }

    //        for (var i = 0; i <= 1; i++)
    //        {
    //            await Task.Delay(1000);
    //            var pos1 = IbcHelper.GetById(0x40001EF7);
    //            DebugMsg($"{i}pos[4]: {pos1.Position}", accessory);
    //        }
    //    }
    //}
    // List<Vector3> BroomList = new List<Vector3>();
    [ScriptMethod(name: "扫帚pos", eventType: EventTypeEnum.SetObjPos, eventCondition: ["Id:003E", "MorlogId:106"])]
    public async void magicBroomPos(Event @event, ScriptAccessory accessory)
    {
        lock (magicBroomPosLock)
        {
            DebugMsg($"magicBroomPosCount: {magicBroomPosCount}", accessory);
            if (magicBroomPosCount <= 4 || (magicBroomPosCount >= 10 && magicBroomPosCount <= 14 ))
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"扫帚面向";
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Owner = @event.SourceId();
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Scale = new Vector2(1, 5.5f);
                dp.DestoryAt = 14500;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                var dp1 = accessory.Data.GetDefaultDrawProperties();
                dp1.Name = $"扫帚范围pos";
                //dp1.Color = accessory.Data.DefaultDangerColor;
                dp1.Color = new Vector4(255 / 255.0f, 215 / 255.0f, 0 / 255.0f, 1.0f);
                dp1.Owner = @event.SourceId();
                dp1.Scale = new Vector2(3f);
                dp1.DestoryAt = 14500;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
            }
            magicBroomPosCount++;
        }


        //List<float> TribulationZAxisList = new List<float>
        //{
        //    198.99f,    //z[0]   from S to N
        //    192.98f,    //z[1]
        //    187f,       //z[2]
        //    180.99f,    //z[3]
        //    174.98f     //z[4]
        //};
        ////////////
        //List<float> BroomZAxisList = new List<float>
        //{
        //    198.23f,    //z[0]   from S to N
        //    192.25f,    //z[1]
        //    187.03f,     //z[2]
        //    181.81f,     //z[3]
        //    175.74f      //z[4]
        //};

        //if (@event.SourcePosition().Z == BroomZAxisList[0])
        //{

        //}

    }



    #endregion


    #region Boss2
    [ScriptMethod(name: "Boss2死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18203"])]
    public void Boss2Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4000, true);
        accessory.TTS("死刑准备", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "Boss2AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18204"])]
    public void Boss2AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 3000, true);
        accessory.TTS("AOE", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "Boss2种子搬运提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18206"])]
    public void Boss2SeedsNotify(Event @event, ScriptAccessory accessory)
    {
        if (Boss2SeedsNotifyCount == 0)
        {
            if (isText) accessory.Method.TextInfo("将种子搬离草地", duration: 8000, true);
            accessory.TTS("将种子搬离草地", isTTS, isDRTTS);
        } else
        {
            if (isText) accessory.Method.TextInfo("种子将被击退一格, 将种子搬至安全区", duration: 8000, true);
            accessory.TTS("种子将被击退一格, 将种子搬至安全区", isTTS, isDRTTS);
        }

    }

    [ScriptMethod(name: "Boss2花雨之歌", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18768"])]
    public void OdetoFallenPetals(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("月环，去Boss脚下", duration: 4000, true);
        accessory.TTS("月环，去Boss脚下", isTTS, isDRTTS);

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = $"花雨之歌(单月环)";
        dp2.Color = accessory.Data.DefaultSafeColor;
        dp2.Owner = @event.SourceId();
        dp2.Scale = new Vector2(5);
        dp2.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);

        var dp3 = accessory.Data.GetDefaultDrawProperties();
        dp3.Name = $"花雨之歌指路";
        dp3.Owner = accessory.Data.Me;
        dp3.Color = accessory.Data.DefaultSafeColor;
        dp3.ScaleMode |= ScaleMode.YByDistance;
        dp3.TargetPosition = @event.SourcePosition();
        dp3.Scale = new(2);
        dp3.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
    }

    [ScriptMethod(name: "强风击退提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18209"])]
    public async void IrefulWind(Event @event, ScriptAccessory accessory)
    {
        var westPos = new Vector3(-21.57f, -12.50f, -60.00f);
        //DebugMsg($"SourcePosition: {@event.SourcePosition()}", accessory);
        //DebugMsg($"westPos: {westPos}", accessory);

        if (@event.SourcePosition() == westPos)
        {
            Boss2WindisWest = true;
        }

        for (int i = 0; i <= 24; i++)
        {
            await Task.Delay(500);
            var offset = Boss2WindisWest ? -1f : 1f;
            foreach (var item in IbcHelper.GetByDataId(accessory, 11269))
            {
                DebugMsg($"itemid: {item}", accessory);
                //DebugMsg($"itemGameObjectId: {item.GameObjectId}", accessory);
                //DebugMsg($"itemOwnerId: {item.OwnerId}", accessory);
                //DebugMsg($"itemEntityId: {item.EntityId}", accessory);
                //DebugMsg($"itemTargerId: {item.TargetObjectId}", accessory);
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "强风种子击退提示";
                dp.Scale = new(1.5f, 10);
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Owner = item.EntityId;

                var targetPosition = item.Position;
                targetPosition.X += offset;
                DebugMsg($"offset: {offset}", accessory);
                dp.TargetPosition = targetPosition;
                DebugMsg($"item.Position: {item.Position}", accessory);
                DebugMsg($"TargetPosition: {dp.TargetPosition}", accessory);

                dp.Rotation = float.Pi + item.Rotation * float.Pi / 180;
                dp.DestoryAt = 500;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }

    }
    #endregion


    #region Boss3
    [ScriptMethod(name: "左/右炎狱斩", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(1827[45])$"])]
    public void Boss3Knout(Event @event, ScriptAccessory accessory)
    {
        var isR = @event.ActionId() == 18274;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"{(isR ? "左" : "右")}鞭打";
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.DestoryAt = 5500;
        dp.Owner = @event.SourceId();
        dp.Scale = new Vector2(40);
        dp.Radian = float.Pi / 180 * 180;
        dp.Rotation = float.Pi / 180 * 90 * (isR ? -1 : 1);

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        if (isText) accessory.Method.TextInfo($"去Boss{(isR ? "左" : "右")}面", duration: 4000, true);
        accessory.TTS($"去Boss{(isR ? "左" : "右")}面", isTTS, isDRTTS);
    }

    [ScriptMethod(name: "火点名", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0019"])]
    public async void fireIcon(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            if (isText) accessory.Method.TextInfo($"注意分散, 远离家具，引导火圈 + 十字", duration: 4000, true);
            accessory.TTS($"注意分散, 远离家具，引导火圈加十字", isTTS, isDRTTS);
        }

        var dp1 = accessory.Data.GetDefaultDrawProperties();
        dp1.Name = "火点名圆形";
        dp1.Color = new Vector4(255 / 255.0f, 127 / 255.0f, 80 / 255.0f, 1.0f);
        //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp1.Owner = @event.TargetId();
        //dp1.ScaleMode = ScaleMode.ByTime;
        dp1.Scale = new Vector2(7);
        dp1.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);

        //// 加入后角色会动，导致一直在旋转
        //await Task.Delay(5000);
        //for (int i = 0; i < 4; i++)
        //{
        //    float rotation = 0;
        //    switch (i)
        //    {
        //        case 0: rotation = float.Pi / 180 * -90; break;
        //        case 1: rotation = float.Pi / 180 * 90; break;
        //        case 2: rotation = float.Pi; break;
        //        case 3: rotation = 0; break;
        //    }
        //    var dp = accessory.Data.GetDefaultDrawProperties();
        //    dp.Name = "火点名";
        //    dp.Color = new Vector4(255 / 255.0f, 255 / 255.0f, 0 / 255.0f, 1.0f);
        //    //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        //    dp.Owner = @event.TargetId();
        //    dp.Scale = new Vector2(4.4f, 10.3f);
        //    dp.DestoryAt = 10000;
        //    //dp.Rotation = rotation - @event.TargetRotation();
        //    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        //}
    }

    [ScriptMethod(name: "鬼炎斩", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18268"])]
    public void OtherworldlyHeat(Event @event, ScriptAccessory accessory)
    {
        var posCenter = @event.EffectPosition();
        var posLeftStart = posCenter;
        var posLeftEnd = posCenter;
        //var posUpStart = posCenter;
        //var posUpEnd = posCenter;

        posLeftStart.X += 10.2f / 2;
        posLeftStart.X -= 10.2f / 2;
        //posUpStart.Z += 10.1f / 2;
        //posUpEnd.Z += 10.1f / 2;

        for (var i = 0; i < 4; i++)
        {
            float rotation = 0;
            switch (i)
            {
                case 0: rotation = float.Pi / 180 * -90; break;
                case 1: rotation = float.Pi / 180 * 90; break;
                case 2: rotation = float.Pi; break;
                case 3: rotation = 0; break;
            }
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "火点名left";
            dp.Color = new Vector4(128 / 255.0f, 0 / 255.0f, 128 / 255.0f, 1.0f);
            //dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            dp.Position = posLeftStart;
            dp.TargetPosition = posLeftEnd;
            dp.Rotation = rotation;
            dp.Scale = new Vector2(4f, 10.3f);
            dp.DestoryAt = 3500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    [ScriptMethod(name: "蓝火传火", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:00C3"])]
    public async void BluefireIcon(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            if (isText) accessory.Method.TextInfo($"找家具传火", duration: 4000, true);
            accessory.TTS($"找家具传火", isTTS, isDRTTS);
        }

        List<uint> dataIds = new List<uint> { 11278, 11279, 11281, 11280 };
        foreach (var dataId in dataIds)
        {
            foreach (var item in IbcHelper.GetByDataId(accessory, dataId))
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "蓝火传火";
                dp.Color = new Vector4(0 / 255.0f, 191 / 255.0f, 255 / 255.0f, 1.0f);
                //dp.Owner = item.EntityId;
                dp.Position = item.Position;
                dp.ScaleMode = ScaleMode.ByTime;
                dp.Scale = new Vector2(2);
                dp.DestoryAt = 15000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }
    }

    [ScriptMethod(name: "炎狱杀点名", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:003[2345]"])]
    public async void FiresDomainIcon(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            if (isText) accessory.Method.TextInfo($"注意远离Boss分散, 将连线拉成紫色, 同时不要将Boss引导到家具", duration: 7500, true);
            accessory.TTS($"注意远离Boss分散, 将连线拉成紫色, 同时不要将Boss引导到家具", isTTS, isDRTTS);
        }

        //var idToMarkTypeMap = new Dictionary<uint, KodakkuAssist.Module.GameOperate.MarkType>
        //{
        //    { 0032, KodakkuAssist.Module.GameOperate.MarkType.Attack1 },
        //    { 0033, KodakkuAssist.Module.GameOperate.MarkType.Attack2 },
        //    { 0034, KodakkuAssist.Module.GameOperate.MarkType.Attack3 },
        //    { 0035, KodakkuAssist.Module.GameOperate.MarkType.Attack4 }
        //};

        //if (idToMarkTypeMap.TryGetValue(@event.Id(), out var markType) && isMark)
        //{
        //    DebugMsg("Marking", accessory);
        //    accessory.Method.Mark(@event.TargetId(), markType, LocalMark);
        //}
        DebugMsg($"Start Marking, Id: {@event.Id()}", accessory);
        if (@event.Id() == 26)
        {
            DebugMsg("Start 0032", accessory);
            if (isMark) accessory.Method.Mark(@event.TargetId(), KodakkuAssist.Module.GameOperate.MarkType.Attack1, LocalMark);
        }
        if (@event.Id() == 27)
        {
            DebugMsg("Start 0033", accessory);
            if (isMark) accessory.Method.Mark(@event.TargetId(), KodakkuAssist.Module.GameOperate.MarkType.Attack2, LocalMark);
        }
        if (@event.Id() == 28)
        {
            DebugMsg("Start 0034", accessory);
            if (isMark) accessory.Method.Mark(@event.TargetId(), KodakkuAssist.Module.GameOperate.MarkType.Attack3, LocalMark);
        }
        if (@event.Id() == 29)
        {
            DebugMsg("Start 0035", accessory);
            if (isMark) accessory.Method.Mark(@event.TargetId(), KodakkuAssist.Module.GameOperate.MarkType.Attack4, LocalMark);
        }
        DebugMsg("End Marking", accessory);

    }

    [ScriptMethod(name: "炎狱杀连线", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0039"])]
    public async void FiresDomainTether(Event @event, ScriptAccessory accessory)
    {
        //if (@event.TargetId() == accessory.Data.Me)
        //{
        //    if (isText) accessory.Method.TextInfo($"找家具传火", duration: 4000, true);
        //    accessory.TTS($"找家具传火", isTTS, isDRTTS);
        //}
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "蓝火传火";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Owner = item.EntityId;
        dp.Owner = @event.SourceId();
        dp.TargetObject= @event.TargetId();
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Scale = new Vector2(5);
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        // 没必要
        //if (@event.TargetId() == accessory.Data.Me)
        //{
        //    await Task.Delay(3300);
        //    if (isText) accessory.Method.TextInfo($"远离顺劈", duration: 4000, true);
        //    accessory.TTS($"远离顺劈", isTTS, isDRTTS);
        //}
    }

    [ScriptMethod(name: "炎狱闪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18273"])]
    public async void FiresIre(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "炎狱闪";
        dp.Color = accessory.Data.DefaultDangerColor;
        //dp.Owner = item.EntityId;
        dp.Owner = @event.SourceId();
        dp.ScaleMode |= ScaleMode.ByTime;
        dp.Scale = new Vector2(20);
        dp.DestoryAt = 2000;
        dp.Radian = float.Pi / 180 * 90;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Boss3AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18277"])]
    public void Boss3AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 5700, true);
        accessory.TTS("AOE", isTTS, isDRTTS);
        accessory.Method.MarkClear();
    }

    [ScriptMethod(name: "掉落", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18279"])]
    public async void fall(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "掉落";
        dp.Color = new Vector4(0 / 255.0f, 191 / 255.0f, 255 / 255.0f, 1.0f);
        dp.Position = @event.EffectPosition();
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Scale = new Vector2(3);
        dp.DestoryAt = 1600;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Boss3死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:18276"])]
    public void Boss3Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4000, true);
        accessory.TTS("死刑准备", isTTS, isDRTTS);
        accessory.Method.MarkClear();
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
}


public static class Extensions
{
    public static void TTS(this ScriptAccessory accessory, string text, bool isTTS, bool isDRTTS)
    {
        accessory.Method.TTS(text);
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
}

public static unsafe class IBattleCharaExtensions
{
    public static bool HasStatus(this IBattleChara ibc, uint statusId, float remaining = -1)
    {
        return ibc.StatusList.Any(s => s.StatusId == statusId && s.RemainingTime > remaining);
    }

    public static unsafe uint Tethering(this IBattleChara ibc, int index = 0)
    {
        return ((BattleChara*)ibc.Address)->Vfx.Tethers[index].TargetId.ObjectId;
    }
}

