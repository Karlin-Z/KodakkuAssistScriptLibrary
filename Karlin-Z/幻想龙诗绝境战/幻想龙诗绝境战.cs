using System.Globalization;
using System.Numerics;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw.Manager;
using Newtonsoft.Json;
using System;
using System.Runtime.Intrinsics.Arm;
using Dalamud.Memory.Exceptions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Dalamud.Bindings.ImGui;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Extensions;

namespace KarlinScriptNamespace;

[ScriptType(name: "绝龙诗绘图", territorys: [968], guid: "d9c97e91-9b59-432d-a3a1-42a8475b7e2a", version: "0.0.0.7",
    author: "Karlin")]
public partial class DragongSingDraw
{
    private readonly object lockObj = new();
    private uint darkDragonId;

    private int? firstTargetIcon;
    private uint p1AdelId;

    private bool p1Charge;
    private uint p1GrenoId;
    private List<int> p1sony = [];
    private Vector3 p2AdelPos = Vector3.Zero;
    private List<uint> p2BlueCircle = [];

    private (int, int) p2Jump = (-1, -1);
    private List<bool> p2SafeDir = [];
    private List<bool> p2Stone = [];
    private (int, int) p2StoneMem = (-1, -1);
    private List<int> p2StoneTeam = [];
    private List<bool> p2Tower = [];
    private Vector3 p2ZPos = Vector3.Zero;
    private List<bool> p3Boom = [];
    private uint p3BossId;
    private List<int> p3Tower = [];
    private bool p3TowerDeal;
    private Dictionary<string, HashSet<uint>> p3majong = new();
    private bool p5Deal;
    private Vector3 p5DivePos = Vector3.Zero;
    private Vector3 p5GreekPos = Vector3.Zero;
    private Vector3 p5GrenoPos = Vector3.Zero;
    private List<int> p5sony = [];
    private uint p6FireBallCount;
    private uint p6FireBallCount2;
    private Vector3 p6WhitePos = Vector3.Zero;
    private List<int> p6lightDark = [];
    private List<int> p6tether = [];
    private Vector3 p7Stone1 = Vector3.Zero;
    private Vector3 p7Stone2 = Vector3.Zero;

    private double parse;
    private uint tordanId;
    private uint whiteDragonId;

    [UserSetting("P5 一运连线冲锋显示延迟(ms)")] public int p5TetherCrashDelay { get; set; } = 3000;

    [UserSetting("P6 分散分摊标记")] public bool p6Mark { get; set; } = false;

    [UserSetting("P7 死亡轮回116分摊")] public bool p7_116 { get; set; } = true;


    public void Init(ScriptAccessory accessory)
    {
        parse = 0;

        p6FireBallCount = 0;
        p6FireBallCount2 = 0;

        firstTargetIcon = null;
        p1Charge = false;
        p3TowerDeal = false;
        p5Deal = false;

        p3majong = new Dictionary<string, HashSet<uint>>();
        p5DivePos = Vector3.Zero;
        p5GrenoPos = Vector3.Zero;
        p5GreekPos = Vector3.Zero;
        p5sony = [0, 0, 0, 0, 0, 0, 0, 0];
        p1sony = [0, 0, 0, 0, 0, 0, 0, 0];
        p3Tower = [0, 0, 0, 0];
        p6tether = [0, 0, 0, 0, 0, 0, 0, 0];
        p6lightDark = [0, 0, 0, 0, 0, 0, 0, 0];
        p2BlueCircle = [];
        p2SafeDir = [true, true, true, true, true, true, true, true];
        p2Stone = [false, false, false, false, false, false, false, false];
        p2Tower =
        [
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false
        ];
        p3Boom = [false, false, false, false];
        p2Jump = (-1, -1);

        accessory.Method.MarkClear();
    }

    [ScriptMethod(name: "TargetIconPrint", eventType: EventTypeEnum.TargetIcon)]
    public void TestTargetIcon(Event @event, ScriptAccessory accessory)
    {
        accessory.Log.Debug($"TargetIcon: {@event["TargetId"]} {ParsTargetIcon(@event["Id"])}");
    }

    private static bool ParseObjectId(string? idStr, out uint id)
    {
        id = 0;
        if (string.IsNullOrEmpty(idStr)) return false;
        try
        {
            var idStr2 = idStr.Replace("0x", "");
            id = uint.Parse(idStr2, NumberStyles.HexNumber);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private int ParsTargetIcon(string id)
    {
        firstTargetIcon ??= int.Parse(id, NumberStyles.HexNumber);
        return int.Parse(id, NumberStyles.HexNumber) - (int)firstTargetIcon;
    }

    private Vector3 RotatePoint(Vector3 point, Vector3 centre, float radian)
    {
        Vector2 v2 = new(point.X - centre.X, point.Z - centre.Z);

        var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
        var lenth = v2.Length();
        return new Vector3(centre.X + MathF.Sin(rot) * lenth, centre.Y, centre.Z - MathF.Cos(rot) * lenth);
    }

    /// <summary>
    ///     向下取
    /// </summary>
    /// <param name="point"></param>
    /// <param name="centre"></param>
    /// <returns></returns>
    private int PositionFloorTo4Dir(Vector3 point, Vector3 centre)
    {
        // Dirs: N = 0, NE = 1, ..., NW = 7
        var r = Math.Floor(2 - 2 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 4;
        return (int)r;
    }

    /// <summary>
    ///     向近的取
    /// </summary>
    /// <param name="point"></param>
    /// <param name="centre"></param>
    /// <returns></returns>
    private int PositionRoundTo4Dir(Vector3 point, Vector3 centre)
    {
        var r = Math.Round(2 - 2 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 4;
        return (int)r;
    }

    /// <summary>
    ///     向近的取
    /// </summary>
    /// <param name="point"></param>
    /// <param name="centre"></param>
    /// <returns></returns>
    private int PositionTo8Dir(Vector3 point, Vector3 centre)
    {
        // Dirs: N = 0, NE = 1, ..., NW = 7
        var r = Math.Round(4 - 4 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 8;
        return (int)r;
    }

    private int PositionTo12Dir(Vector3 point, Vector3 centre)
    {
        // Dirs: N = 0, NE = 1, ..., NW = 7
        var r = Math.Round(6 - 6 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 12;
        return (int)r;
    }

}
