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

public partial class DragongSingDraw
{
    #region P4

    [ScriptMethod(name: "P4 记录", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:29750"],
        userControl: false)]
    public void P4_记录(Event @event, ScriptAccessory accessory)
    {
        parse = 4;
    }

    #endregion
}
