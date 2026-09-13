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

namespace Veever.Heavensward.AetherochemicalResearchFacility;

[ScriptType(name: "LV.60 血战苍穹魔科学研究所", territorys: [1110], guid: "dd08165c-b709-4100-a96e-65f2c7ae4f3b",
    version: "0.0.0.8", author: "Veever", note: noteStr)]

public class AetherochemicalResearchFacility
{
    public int fireCount;
    public int iceCount;
    public int Away0061Count;
    public int TetherCount;

    private readonly object fireLock = new object();
    private readonly object iceLock = new object();
    private readonly object tetherLock = new object();
    const string noteStr =
    """
    v0.0.0.8:
    1. 现在支持文字横幅/TTS开关
    2. 以前的这几个脚本的底层扩展目前懒得重构（就能加啥随便加了）
    鸭门。
    """;
    [UserSetting("文字横幅提示开关")]
    public bool isText { get; set; } = true;
    [UserSetting("TTS开关")]
    public bool isTTS { get; set; } = false;

    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
        fireCount = 0;
        iceCount = 0;
        Away0061Count = 0;
        TetherCount = 0;
    }

    #region Boss1
    [ScriptMethod(name: "Boss1魔导炮塔", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(373[67])$"])]
    public void Boss1MagitekTurret(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("集火魔导炮塔", duration: 10000, true);
        if (isTTS) accessory.Method.TTS("集火魔导炮塔");
        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "魔导炮塔Pos";
        dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
        dp.Position = @event.SourcePosition();
        dp.Scale = new Vector2(2);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Boss1魔导激光", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:4321"])]
    public void Boss1MagitekRay(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("躲避激光", duration: 2700, true);
        if (isTTS) accessory.Method.TTS("躲避激光");
    }

    [ScriptMethod(name: "Boss1魔导扩散弹", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:4316"])]
    public void Boss1MagitekSpread(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("去boss身后", duration: 4200, true);
        if (isTTS) accessory.Method.TTS("去boss身后");
    }
    #endregion




    #region Boss2
    [ScriptMethod(name: "Boss2顺劈播报", eventType: EventTypeEnum.Chat, eventCondition: ["Message:启动合成生物性能评测系统——赫鲁玛奇斯。"])]
    public void Boss2Notification(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("此Boss大多数技能均为顺劈，非坦克职业不要站在Boss正面", duration: 5000, true);
        if (isTTS) accessory.Method.TTS("此Boss大多数技能均为顺劈，非坦克职业不要站在Boss正面");
    }

    [ScriptMethod(name: "Boss2石化", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:4331"])]
    public void Boss2Petrifaction(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("背对Boss", duration: 2700, true);
        if (isTTS) accessory.Method.TTS("背对Boss");
    }


    [ScriptMethod(name: "Boss2弹道导弹", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:4771"])]
    public void Boss2BallisticMissile(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("双人分摊，不要超过两个人", duration: 4000, true);
        if (isTTS) accessory.Method.TTS("双人分摊，不要超过两个人");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "弹道导弹";
        dp.Color = new Vector4(1.0f, 0.4f, 1.0f, 1.0f);
        dp.Position = @event.EffectPosition();
        dp.Scale = new Vector2(4);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }


    [ScriptMethod(name: "Boss2分摊", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:005D"])]
    public void Boss2Stack(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Boss2分摊";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 5500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (isText) accessory.Method.TextInfo($"与{tname}分摊", duration: 4000, true);
        if (isTTS) accessory.Method.TTS($"与{tname}分摊");
    }
    #endregion


    #region Boss3-P1
    [ScriptMethod(name: "Boss3AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31885"])]
    public void Boss3AOE(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("AOE");
    }

    [ScriptMethod(name: "Boss3烈火魔力球", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:15782"])]
    public void Boss3Fireball(Event @event, ScriptAccessory accessory)
    {
        lock (fireLock)
        {
            if (fireCount <= 4)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"{fireCount}烈火魔力球";
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Position = @event.SourcePosition();
                dp.Scale = new Vector2(8);
                dp.DestoryAt = 6200;
                dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                //accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {fireCount}, TetherCount: {TetherCount}");
            }
            fireCount++;
        }
    }


    [ScriptMethod(name: "Boss3黑夜波", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:32790"])]
    public void Boss3GripofNight(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("去boss身后", duration: 5700, true);
        if (isTTS) accessory.Method.TTS("去boss身后");
    }

    [ScriptMethod(name: "Boss3寒冰魔力球", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:15781"])]
    public void Boss3Iceball(Event @event, ScriptAccessory accessory)
    {
        lock (iceLock)
        {
            if (iceCount <= 3)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"{iceCount}寒冰魔力球";
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Position = @event.SourcePosition();
                dp.Scale = new Vector2(5);
                dp.DestoryAt = 7000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                //accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {fireCount}, TetherCount: {TetherCount}");
                if (iceCount == 1)
                {
                    if (isText) accessory.Method.TextInfo("进入绿色安全区", duration: 5700, true);
                    if (isTTS) accessory.Method.TTS("进入绿色安全区");
                }
            }
            iceCount++;
        }
    }


    [ScriptMethod(name: "Boss3分摊", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31892"])]
    public void Boss3EndofDays(Event @event, ScriptAccessory accessory)
    {
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";
        if (isText) accessory.Method.TextInfo($"与 {tname} 分摊", duration: 4700, true);

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "EndofDays";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.SourceId();
        dp.TargetObject = @event.TargetId();
        dp.Scale = new Vector2(6, 25);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        if (isTTS) accessory.Method.TTS($"与 {tname} 分摊");
    }

    #endregion


    #region Boss3-P2
    [ScriptMethod(name: "Boss3-P2清理变量", eventType: EventTypeEnum.Chat, eventCondition: ["Message:regex:^暗之力在涌动……\\r?\\n如火炎般热烈，如冰霜般寂静！$"])]
    public void Boss3P2Clean(Event @event, ScriptAccessory accessory)
    {
        iceCount = 10;
        fireCount = 10;
        TetherCount = 10;
        //accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {fireCount}, TetherCount: {TetherCount}");
    }


    [ScriptMethod(name: "Boss3AOE2", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(319[10]0)$"])]
    public void Boss3AOE2(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("AOE", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("AOE");
    }

    [ScriptMethod(name: "Boss3大AOE", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33024"])]
    public void Boss3Annihilation(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("大AOE", duration: 6000, true);
        if (isTTS) accessory.Method.TTS("大AOE");
    }


    [ScriptMethod(name: "Boss3死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31911"])]
    public void Boss3Tankbuster(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("死刑准备", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("死刑准备");
    }

    [ScriptMethod(name: "Boss3分摊2", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:00A1"])]
    public void Boss3Stack(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "Boss3分摊";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.TargetId();
        dp.Scale = new Vector2(6);
        dp.DestoryAt = 5500;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (isText) accessory.Method.TextInfo($"与{tname}分摊", duration: 5000, true);
        if (isTTS) accessory.Method.TTS($"与{tname}分摊");
    }

    [ScriptMethod(name: "Boss3拉线", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3505"])]
    public async void Boss3Away3505(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetId() == accessory.Data.Me)
        {
            await Task.Delay(36000 - 2000);
            if (isText) accessory.Method.TextInfo("集合，等连线判定后拉线", duration: 1500, true);
            if (isTTS) accessory.Method.TTS("集合，等连线判定后拉线");

            await Task.Delay(2500);
            if (isText) accessory.Method.TextInfo("拉开连线", duration: 4000, true);
            if (isTTS) accessory.Method.TTS("拉开连线");
        }
    }


    [ScriptMethod(name: "Boss3分摊2-2", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31907"])]
    public void Boss3EntropicFlame(Event @event, ScriptAccessory accessory)
    {
        string tname = @event["TargetName"]?.ToString() ?? "未知目标";
        if (isText) accessory.Method.TextInfo($"与 {tname} 分摊", duration: 4700, true);

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = "EntropicFlame";
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = @event.SourceId();
        dp.TargetObject = @event.TargetId();
        dp.Scale = new Vector2(6, 20);
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        if (isTTS) accessory.Method.TTS($"与 {tname} 分摊");
    }

    [ScriptMethod(name: "Boss3集火立体魔法阵提醒", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:15788"])]
    public void Boss3ArcaneSphere(Event @event, ScriptAccessory accessory)
    {
        if (isText) accessory.Method.TextInfo("集中攻击立体魔法阵", duration: 4700, true);
        if (isTTS) accessory.Method.TTS("集中攻击立体魔法阵");
    }


    #region Tether Basic Logic
    //[ScriptMethod(name: "Boss3连线处理", eventType: EventTypeEnum.Tether, eventCondition: ["Id:006E"])]
    //public async void Boss3Tether(Event @event, ScriptAccessory accessory)
    //{
    //    bool case1 = false, case2 = false;

    //    lock (tetherLock)
    //    {
    //        if (TetherCount <= 1)
    //        {
    //            var pos1 = @event.SourcePosition();
    //            var pos2 = @event.TargetPosition();
    //            Vector3 midPoint = new Vector3();

    //            if (pos1.X == pos2.X)
    //            {
    //                if (pos1.Y == pos2.Y)
    //                {
    //                    midPoint = new Vector3(pos1.X, pos1.Y, (pos1.Z + pos2.Z) / 2.0f);
    //                } else
    //                {
    //                    midPoint = new Vector3(pos1.X, (pos1.Y + pos2.Y) / 2.0f, pos1.Z);
    //                }
    //            } else
    //            {
    //                midPoint = new Vector3((pos1.X + pos2.X) / 2.0f, pos1.Y, pos1.Z);
    //            }

    //            var dp = accessory.Data.GetDefaultDrawProperties();
    //            dp.Name = $"1烈火魔力球连线1(双大钢铁){TetherCount}";
    //            dp.Color = accessory.Data.DefaultDangerColor;
    //            dp.Position = midPoint;
    //            dp.Scale = new Vector2(16);
    //            dp.DestoryAt = 10800;
    //            dp.ScaleMode = ScaleMode.ByTime;
    //            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    //            accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {fireCount}, TetherCount: {TetherCount}");
    //        }
    //        if (TetherCount == 2)
    //        {
    //            var pos1 = @event.SourcePosition();
    //            var pos2 = @event.TargetPosition();
    //            Vector3 midPoint = new Vector3();
    //            if (pos1.X == pos2.X)
    //            {
    //                if (pos1.Y == pos2.Y)
    //                {
    //                    midPoint = new Vector3(pos1.X, pos1.Y, (pos1.Z + pos2.Z) / 2.0f);
    //                }
    //                else
    //                {
    //                    midPoint = new Vector3(pos1.X, (pos1.Y + pos2.Y) / 2.0f, pos1.Z);
    //                }
    //            }
    //            else
    //            {
    //                midPoint = new Vector3((pos1.X + pos2.X) / 2.0f, pos1.Y, pos1.Z);
    //            }

    //            var dp = accessory.Data.GetDefaultDrawProperties();
    //            dp.Name = $"2寒冰魔力球连线1(单月环){TetherCount}";
    //            dp.Color = accessory.Data.DefaultSafeColor;
    //            dp.Position = midPoint;
    //            dp.Scale = new Vector2(5);
    //            dp.DestoryAt = 10000;
    //            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    //            accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {qqfireCount}, TetherCount: {TetherCount}");

    //            var dp2 = accessory.Data.GetDefaultDrawProperties();
    //            dp2.Name = $"{iceCount}寒冰魔力球连线1指路";
    //            dp2.Owner = accessory.Data.Me;
    //            dp2.Color = accessory.Data.DefaultSafeColor;
    //            dp2.ScaleMode |= ScaleMode.YByDistance;
    //            dp2.TargetPosition = midPoint;
    //            dp2.Scale = new(2);
    //            dp2.DestoryAt = 6000;
    //            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
    //        }
    //        if (TetherCount > 2 && TetherCount <= 4)
    //        {
    //            var pos1 = @event.SourcePosition();
    //            var pos2 = @event.TargetPosition();
    //            Vector3 midPoint = new Vector3();
    //            if (pos1.X == pos2.X)
    //            {
    //                if (pos1.Y == pos2.Y)
    //                {
    //                    midPoint = new Vector3(pos1.X, pos1.Y, (pos1.Z + pos2.Z) / 2.0f);
    //                }
    //                else
    //                {
    //                    midPoint = new Vector3(pos1.X, (pos1.Y + pos2.Y) / 2.0f, pos1.Z);
    //                }
    //            }
    //            else
    //            {
    //                midPoint = new Vector3((pos1.X + pos2.X) / 2.0f, pos1.Y, pos1.Z);
    //            }

    //            var dp = accessory.Data.GetDefaultDrawProperties();
    //            dp.Name = $"3烈火魔力球连线2(双大钢铁){TetherCount}";
    //            dp.Color = accessory.Data.DefaultDangerColor;
    //            dp.Position = midPoint;
    //            dp.Scale = new Vector2(16);
    //            dp.DestoryAt = 10000;
    //            dp.ScaleMode = ScaleMode.ByTime;
    //            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    //            accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {fireCount}, TetherCount: {TetherCount}");
    //        }

    //        if (TetherCount == 10 || TetherCount == 13)
    //        {
    //            var spos = @event.SourcePosition();
    //            var tpos = @event.TargetPosition();
    //            float spostotal = spos.X + spos.Y + spos.Z;
    //            float tpostotal = tpos.X + tpos.Y + tpos.Z;
    //            float difference = Math.Abs(spostotal - tpostotal);
    //            int roundedDifference = (int)Math.Round(difference);

    //            switch (roundedDifference)
    //            {
    //                case 14:
    //                    case1 = true;
    //                    break;

    //                case 26:
    //                    case2 = true;
    //                    break;

    //                default:

    //                    break;
    //            }
    //        }

    //        TetherCount++;
    //    }

    //    if (case1)
    //    {
    //        var pos1 = new Vector3(219.00f, -456.46f, 79.00f);
    //        var pos2 = new Vector3(241.00f, -456.46f, 79.00f);
    //        Vector3[] posStr = { pos1, pos2 };

    //        for (var i = 0; i <= 1; i++)
    //        {
    //            var dp1 = accessory.Data.GetDefaultDrawProperties();
    //            dp1.Name = $"4烈火魔力球连线P2-1(双大钢铁){TetherCount}";
    //            dp1.Color = accessory.Data.DefaultDangerColor;
    //            dp1.Position = posStr[i];
    //            dp1.Scale = new Vector2(16);
    //            dp1.DestoryAt = 10000;
    //            dp1.ScaleMode = ScaleMode.ByTime;
    //            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
    //            accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {fireCount}, TetherCount: {TetherCount}");
    //        }
    //        var pos3 = new Vector3(230f, -456.46f, 79.00f);

    //        var dp2 = accessory.Data.GetDefaultDrawProperties();
    //        dp2.Name = $"5寒冰魔力球连线P2-1(单月环){TetherCount}";
    //        dp2.Color = accessory.Data.DefaultSafeColor;
    //        dp2.Position = pos3;
    //        dp2.Scale = new Vector2(5);
    //        dp2.Delay = 10000;
    //        dp2.DestoryAt = 6000;
    //        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
    //        accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {fireCount}, TetherCount: {TetherCount}");

    //        var dp3 = accessory.Data.GetDefaultDrawProperties();
    //        dp3.Name = $"{iceCount}寒冰魔力球连线P2-1指路";
    //        dp3.Owner = accessory.Data.Me;
    //        dp3.Color = accessory.Data.DefaultSafeColor;
    //        dp3.ScaleMode |= ScaleMode.YByDistance;
    //        dp3.TargetPosition = pos3;
    //        dp3.Scale = new(2);
    //        dp2.Delay = 10000;
    //        dp3.DestoryAt = 5000;
    //        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);

    //    }

    //    if (case2)
    //    {
    //        var pos1 = new Vector3(219.00f, -456.46f, 79.00f);
    //        var pos2 = new Vector3(241.00f, -456.46f, 79.00f);
    //        Vector3[] posStr = { pos1, pos2 };
    //        var pos3 = new Vector3(230f, -456.46f, 79.00f);

    //        var dp2 = accessory.Data.GetDefaultDrawProperties();
    //        dp2.Name = $"6寒冰魔力球连线P2-2(单月环){TetherCount}";
    //        dp2.Color = accessory.Data.DefaultSafeColor;
    //        dp2.Position = pos3;
    //        dp2.Scale = new Vector2(5);
    //        dp2.DestoryAt = 10000;
    //        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
    //        accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {fireCount}, TetherCount: {TetherCount}");

    //        var dp3 = accessory.Data.GetDefaultDrawProperties();
    //        dp3.Name = $"{iceCount}寒冰魔力球连线P2-2指路";
    //        dp3.Owner = accessory.Data.Me;
    //        dp3.Color = accessory.Data.DefaultSafeColor;
    //        dp3.ScaleMode |= ScaleMode.YByDistance;
    //        dp3.TargetPosition = pos3;
    //        dp3.Scale = new(2);
    //        dp3.DestoryAt = 10000;
    //        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);


    //        for (var i = 0; i <= 1; i++)
    //        {
    //            var dp1 = accessory.Data.GetDefaultDrawProperties();
    //            dp1.Name = $"7烈火魔力球连线P2-2(双大钢铁){TetherCount}";
    //            dp1.Color = accessory.Data.DefaultDangerColor;
    //            dp1.Position = posStr[i];
    //            dp1.Scale = new Vector2(16);
    //            dp1.Delay = 10000;
    //            dp1.DestoryAt = 8000;
    //            dp1.ScaleMode = ScaleMode.ByTime;
    //            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
    //            accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {fireCount}, TetherCount: {TetherCount}");
    //        }
    //    }
    //}
    #endregion


    [ScriptMethod(name: "Boss3连线处理", eventType: EventTypeEnum.Tether, eventCondition: ["Id:006E"])]
    public void Boss3Tether(Event @event, ScriptAccessory accessory)
    {
        bool case1 = false, case2 = false;

        lock (tetherLock)
        {
            var pos1 = @event.SourcePosition();
            var pos2 = @event.TargetPosition();
            var midPoint = CalculateMidPoint(pos1, pos2);

            if (TetherCount <= 1)
            {
                DrawCircle(accessory, midPoint, 16, 10800, $"1烈火魔力球连线1(双大钢铁){TetherCount}", accessory.Data.DefaultDangerColor, false);
            }
            else if (TetherCount == 2)
            {
                DrawCircle(accessory, midPoint, 5, 10000, $"2寒冰魔力球连线1(单月环){TetherCount}", accessory.Data.DefaultSafeColor, false);
                DrawDisplacement(accessory, midPoint, 2, 6000, $"{iceCount}寒冰魔力球连线1指路", delay: 0);
            }
            else if (TetherCount > 2 && TetherCount <= 4)
            {
                DrawCircle(accessory, midPoint, 16, 10000, $"3烈火魔力球连线2(双大钢铁){TetherCount}", accessory.Data.DefaultDangerColor, false);
            }
            else if (TetherCount == 10 || TetherCount == 13)
            {
                float spostotal = pos1.X + pos1.Y + pos1.Z;
                float tpostotal = pos2.X + pos2.Y + pos2.Z;
                float difference = Math.Abs(spostotal - tpostotal);
                int roundedDifference = (int)Math.Round(difference);

                switch (roundedDifference)
                {
                    case 14:
                        case1 = true;
                        break;
                    case 26:
                        case2 = true;
                        break;
                }
            }

            TetherCount++;
        }

        if (case1)
        {
            DrawCase1(accessory);
        }

        if (case2)
        {
            DrawCase2(accessory);
        }
    }

    private Vector3 CalculateMidPoint(Vector3 pos1, Vector3 pos2)
    {
        if (pos1.X == pos2.X)
        {
            return pos1.Y == pos2.Y
                ? new Vector3(pos1.X, pos1.Y, (pos1.Z + pos2.Z) / 2.0f)
                : new Vector3(pos1.X, (pos1.Y + pos2.Y) / 2.0f, pos1.Z);
        }
        return new Vector3((pos1.X + pos2.X) / 2.0f, pos1.Y, pos1.Z);
    }

    private void DrawCircle(ScriptAccessory accessory, Vector3 position, float scale, int duration, string name, Vector4 color, bool isDebug = false, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = color;
        dp.Position = position;
        dp.Scale = new Vector2(scale);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        isDebug = false;
        if (isDebug)
        {
            accessory.Method.SendChat($"iceCount:{iceCount}, fireCount: {fireCount}, TetherCount: {TetherCount}");
        }
    }

    private void DrawDisplacement(ScriptAccessory accessory, Vector3 target, float scale, int duration, string name, int delay = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = accessory.Data.Me;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.TargetPosition = target;
        dp.Scale = new Vector2(scale);
        dp.Delay = delay;
        dp.DestoryAt = duration;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private void DrawCase1(ScriptAccessory accessory)
    {
        var pos1 = new Vector3(219.00f, -456.46f, 79.00f);
        var pos2 = new Vector3(241.00f, -456.46f, 79.00f);
        var pos3 = new Vector3(230f, -456.46f, 79.00f);
        Vector3[] positions = { pos1, pos2 };

        foreach (var position in positions)
        {
            DrawCircle(accessory, position, 16, 10000, $"4烈火魔力球连线P2-1(双大钢铁){TetherCount}", accessory.Data.DefaultDangerColor, false);
        }

        DrawCircle(accessory, pos3, 5, 5000, $"5寒冰魔力球连线P2-1(单月环){TetherCount}", accessory.Data.DefaultSafeColor, delay: 10000);
        DrawDisplacement(accessory, pos3, 2, 5000, $"{iceCount}寒冰魔力球连线P2-1指路", delay: 10000);
    }

    private void DrawCase2(ScriptAccessory accessory)
    {
        var pos1 = new Vector3(219.00f, -456.46f, 79.00f);
        var pos2 = new Vector3(241.00f, -456.46f, 79.00f);
        var pos3 = new Vector3(230f, -456.46f, 79.00f);
        Vector3[] positions = { pos1, pos2 };

        DrawCircle(accessory, pos3, 5, 9500, $"6寒冰魔力球连线P2-2(单月环){TetherCount}", accessory.Data.DefaultSafeColor);
        DrawDisplacement(accessory, pos3, 2, 9000, $"{iceCount}寒冰魔力球连线P2-2指路");

        foreach (var position in positions)
        {
            DrawCircle(accessory, position, 16, 2500, $"7烈火魔力球连线P2-2(双大钢铁){TetherCount}", accessory.Data.DefaultDangerColor, false, delay: 10000);
        }
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

    public static string SourceName(this Event @event)
    {
        return JsonConvert.DeserializeObject<string>(@event["SourceName"]) ?? string.Empty;
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
}

