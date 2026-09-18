using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Data;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;


namespace MyScriptNamespace;

/// <summary>
///     name and version affect the script name and version number displayed in the user interface.
///     territorys specifies the regions where this trigger is effective. If left empty, it will be effective in all
///     regions.
///     Classes with the same GUID will be considered the same trigger. Please ensure your GUID is unique and does not
///     conflict with others.
/// </summary>
[ScriptType(name: "PiPiScript", guid: "5ce7ebbd-b47e-d61f-3d94-5423e8e80753", version: "0.0.0.2", author: "Karlin", note: noteStr)]
public class PiPiScript
{
    private const string noteStr =
        """
        这是一个提示文本.
        他有多行显示.
        请在这里放置你的提示文本
        """;

    private CancellationTokenSource countdownCts = new();

    [UserSetting("DK伤残时间(ms)")] public int dkTime { get; set; } = 1000;

    [UserSetting("DK伤残后跳斩")] public bool dkCharge { get; set; }

    public void Init(ScriptAccessory accessory)
    {
    }


    [ScriptMethod(name: "舞者倒数跳舞", eventType: EventTypeEnum.Countdown, eventCondition: ["Type:Start"])]
    public void 舞者倒数跳舞(Event @event, ScriptAccessory accessory)
    {
        if (!int.TryParse(@event["Duration"], out var dur)) return;
        var me = accessory.Data.Objects.SearchById(accessory.Data.Me);
        if (me == null) return;
        if (((IBattleChara)me).ClassJob.RowId != 38) return;
        var delay = dur - 15;
        countdownCts = new CancellationTokenSource();
        ThreadPool.QueueUserWorkItem(async c =>
        {
            try
            {
                await Task.Delay(delay * 1000, countdownCts.Token);
                accessory.Method.HttpPost("http://127.0.0.1:9909/DoAction",
                    "{\"Id\":15997,\"Target\":0xE0000000,\"Delay\":0,\"Force\":true}");
                accessory.Method.SendChat("/PiPiPlugin AttackOn");
                await Task.Delay(2200, countdownCts.Token);
                accessory.Method.SendChat("/PiPiPlugin AttackOff");
                await Task.Delay(12300, countdownCts.Token);
                accessory.Method.SendChat("/PiPiPlugin AttackOn");
            }
            catch (OperationCanceledException)
            {
                accessory.Method.SendChat("/PiPiPlugin AttackOff");
            }
        });
    }

    [ScriptMethod(name: "DK倒数开怪", eventType: EventTypeEnum.Countdown, eventCondition: ["Type:Start"])]
    public void DK倒数开怪(Event @event, ScriptAccessory accessory)
    {
        if (!int.TryParse(@event["Duration"], out var dur)) return;
        var me = accessory.Data.Objects.SearchById(accessory.Data.Me);
        if (me == null) return;
        if (((IBattleChara)me).ClassJob.RowId != 32) return;
        countdownCts = new CancellationTokenSource();
        ThreadPool.QueueUserWorkItem(async c =>
        {
            try
            {
                await Task.Delay(dur * 1000 - dkTime, countdownCts.Token);
                accessory.Method.HttpPost("http://127.0.0.1:9909/DoAction",
                    "{\"Id\":3624,\"Target\":0xE0000001,\"Delay\":0,\"Force\":true}");
                await Task.Delay(200);
                if (dkCharge)
                    accessory.Method.HttpPost("http://127.0.0.1:9909/DoAction",
                        "{\"Id\":36926,\"Target\":0xE0000001,\"Delay\":0,\"Force\":true}");
                await Task.Delay(2000);
                accessory.Method.SendChat("/PiPiPlugin AttackOn");
            }
            catch (OperationCanceledException)
            {
                accessory.Method.SendChat("/PiPiPlugin AttackOff");
            }
        });
    }

    [ScriptMethod(name: "取消倒数重置", eventType: EventTypeEnum.Countdown, eventCondition: ["Type:Stop"])]
    public void 取消倒数重置(Event @event, ScriptAccessory accessory)
    {
        countdownCts.Cancel();
    }
}
