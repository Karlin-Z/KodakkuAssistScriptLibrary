using System;
using System.Numerics;
using System.Threading.Tasks;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;

namespace KodakkuAssistXSZYYS
{
    [ScriptType(
        name: "复活位置",
        guid: "874D3ECF-BD6B-448F-BB42-AE7F082E4809",
        territorys: [],
        version: "0.0.2",
        author: "XSZYYS",
        note: "查看自己将会复活在哪里"
    )]
    public class 复活位置
    {
        [ScriptMethod(
            name: "复活",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:148"]
        )]
        public async void 复活(Event @event, ScriptAccessory accessory)
        {
            try
            {
                if (@event.TargetId != accessory.Data.Me) return;
                accessory.Method.RemoveDraw("RebornPosition");
                await Task.Delay(50);
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "RebornPosition";
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = @event.SourcePosition;
                dp.Scale = new Vector2(2);
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.ScaleMode = ScaleMode.YByDistance;
                dp.DestoryAt = 60000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
            catch (Exception ex)
            {
                accessory.Log.Error($"绘制复活位置失败：{ex}");
            }
        }
        [ScriptMethod(
            name: "移除复活",
            eventType: EventTypeEnum.StatusRemove,
            eventCondition: ["StatusID:148"],
            userControl: false
        )]
        public void RemoveReborn(Event @event, ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw("RebornPosition");
        }
    }
}
