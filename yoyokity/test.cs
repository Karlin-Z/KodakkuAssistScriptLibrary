using System;
using System.Numerics;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using KodakkuAssist.Extensions;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Script;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkTimer.Delegates;

namespace MyScriptNamespace
{
    /// <summary>
    /// name and version affect the script name and version number displayed in the user interface.
    /// territorys specifies the regions where this trigger is effective. If left empty, it will be effective in all regions.
    /// Classes with the same GUID will be considered the same trigger. Please ensure your GUID is unique and does not conflict with others.
    /// </summary>
    [ScriptType(name: "SimpleScript", territorys: [179, 979], guid: "d3b6a9b4-1e0e-4e0c-b7b7-ff1fce0e6cf1", version: "0.0.0.2", author: "yoyokity", note: noteStr, updateInfo: updateInfoStr)]
    public class SimpleScript : IDisposable
    {
        const string noteStr =
        """
        这是一个提示文本.
        他有多行显示.
        请在这里放置你的提示文本
        """;
        const string updateInfoStr =
        """
        这里是更新信息.
        他有多行显示.
        请在这里放置你的更新信息
        """;
        /// <summary>
        /// note will be displayed to the user as a tooltip.
        /// </summary>
        [UserSetting(note: "This is a test Property")]
        public int prop1 { get; set; } = 1;
        [UserSetting("Another Test Property")]
        public bool prop2 { get; set; } = false;

        [UserSetting("UserColorSetting")]
        public ScriptColor color { get; set; } = new();

        [UserSetting("EnumSetting")]
        public TestEnum enumSetting { get; set; }
        int n = 0;


        public enum TestEnum
        {
            First,
            Second
        }
        /// <summary>
        /// This method is called at the start of each battle reset.
        /// If this method is not defined, the program will execute an empty method.
        /// </summary>
        public void Init(ScriptAccessory accessory)
        {
            n = 0;
        }

        /// <summary>
        /// name is the name of the method as presented to the user.
        /// eventType is the type of event that triggers this method.
        /// eventCondition is an array of strings specifying the properties that the event must have,
        /// in the format name:value,For specific details, please refer to the GameEvent of the plugin.
        /// userControl set to false will make the method not be shown to the user
        /// and cannot be disabled by the user.
        /// Please note, the method will be executed asynchronously.
        /// </summary>
        /// <param name="event">The event instance that triggers this method.</param>
        /// <param name="accessory">Pass the instances of methods and data that might be needed.</param>
        [ScriptMethod(name: "Test StartCasting", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:133"])]
        public void PrintInfo(Event @event, ScriptAccessory accessory)
        {
            n++;
            accessory.Method.SendChat($"/e {@event["SourceId"]} {n}-th use the Medica II");
            accessory.Log.Debug($"Prop2 is {prop2}");
            accessory.Log.Debug($"enum is {enumSetting}");
        }

        [ScriptMethod(eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:124"])]
        public void DrawCircle(Event @event, ScriptAccessory accessory)
        {
            var sid = @event.SourceId;

            var prop = accessory.Data.GetDefaultDrawProperties();
            prop.Owner = accessory.Data.Me;
            prop.DestoryAt = 10000;
            prop.Color = accessory.Data.DefaultDangerColor;
            prop.Scale = new(5);
            prop.InnerScale = new(3);
            var start = DateTime.Now;
            var n = 0;
            //accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, prop);

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, prop, (dpc) =>
            {
                var elapsedSeconds = (float)(DateTime.Now - start).TotalSeconds;
                float hue = (elapsedSeconds * 0.5f) % 1f;
                dpc.Color = HueToRGB(hue);
                if (elapsedSeconds > 2)
                {
                    start = DateTime.Now;
                    n++;
                    dpc.Radian = MathF.PI / 4 * n;
                }
                dpc.Scale = new(3 + hue * 3);
            });


        }
        [ScriptMethod(eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:999999"])]
        public void TestTTS(Event @event, ScriptAccessory accessory)
        {
            accessory.Method.TTS($"黏黏{DateTime.Now:T}");

        }

        [ScriptMethod(name: "Test Suppress", eventType: EventTypeEnum.Waymark)]
        public void TestSuppress(Event @event, ScriptAccessory accessory)
        {
            n++;
            accessory.Log.Debug($"WayMark {n} {@event.EventTriggerSource}");
            uint id = 0x40000001;
            if (accessory.Data.EnmityList.TryGetValue(id, out var objs))
            {
                var firstId = objs[0];
            }
        }
        [ScriptMethod(name: "Test Action", eventType: EventTypeEnum.Waymark)]
        public void TestAction(Event @event, ScriptAccessory accessory)
        {
            accessory.Method.UseAction(accessory.Data.Me, 37010);
        }
        [ScriptMethod(name: "Test ActionLocation", eventType: EventTypeEnum.Waymark)]
        public void TestActionLocation(Event @event, ScriptAccessory accessory)
        {
            //accessory.Method.UseActionLocation(new System.Numerics.Vector3(-750, -30, -560), 3569, 1);
        }

        [ScriptMethod(name: "Unconfigurable Method", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:124"], userControl: false)]
        public void UnconfigurableMethod(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"The unconfigurable method has been triggered.");
        }
        string guid = "";
        [ScriptMethod(eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37010"])]
        public void TestRunOnFrame(Event @event, ScriptAccessory accessory)
        {
            guid = accessory.Method.RegistFrameworkUpdateAction(() =>
            {
                var a = 123;
            });

        }
        [ScriptMethod(eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:124"])]
        public void TestRunOnFrame2(Event @event, ScriptAccessory accessory)
        {
            accessory.Method.RegistFrameworkUpdateAction(TestRunOnFrame22);
        }
        [ScriptMethod(eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:124"])]
        public void TestFadeOut(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"TestFadeOut";
            dp.Scale = new(5);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = 0x400003B8;
            dp.FadeCentreObject = 0x400003B8;
            dp.FadeDistance = 5;
            dp.FadeMode = FadeMode.OmenCentre;
            dp.DestoryAt = 15000;
            accessory.Method.SendDraw(DrawModeEnum.Vfx, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:99999"])]
        public void TestCreateLockOn(Event @event, ScriptAccessory accessory)
        {
            var objHandle = accessory.Method.ObjectMethod.CreateEmptyChara(0x40001234);
            Task.Delay(50).ContinueWith(t =>
            {
                var start = DateTime.Now;
                var handle = accessory.Method.VfxMethod.CreateLockOn(1, 0x40001234, new Vector4(1, 0, 0, 1), 10000,
                    (ref Vector4 color, ref Vector4 pos, ref Vector3 scale) =>
                    {
                        var elapsedSeconds = (float)(DateTime.Now - start).TotalSeconds;
                        float hue = (elapsedSeconds * 0.5f) % 1f;
                        scale = new(hue * 1 + 0.5f);
                        color = HueToRGB(hue);
                        //pos = new(hue);
                        unsafe
                        {
                            var obj = (BattleChara*)objHandle;
                            obj->Position = new(hue);
                        }

                    });
            });
            Task.Delay(5000).ContinueWith(t => { accessory.Method.ObjectMethod.DestoryChara(objHandle); });



        }

        [ScriptMethod(eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:99999"])]
        public void TestCreateOmen(Event @event, ScriptAccessory accessory)
        {
            var start = DateTime.Now;
            var pos = accessory.Data.MyObject.Position;
            var handle = accessory.Method.VfxMethod.CreateOmen(3, new(3), accessory.Data.MyObject.Position, 0, new Vector4(1, 0, 0, 1), 10000,
                (ref Vector4 color, ref Vector4 pos, ref Vector3 scale) =>
                {
                    var elapsedSeconds = (float)(DateTime.Now - start).TotalSeconds;
                    float hue = (elapsedSeconds * 0.5f) % 1f;
                    color = HueToRGB(hue);//算一个rgb跑马灯颜色并赋值给颜色
                    pos = new(accessory.Data.MyObject.Position, pos.W + MathF.PI / 100);//追踪游戏人物的坐标
                    scale = new(2 + hue * 3);//随时间大小变化
                });
        }
        [ScriptMethod(eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:99999"])]
        public void TestMark(Event @event, ScriptAccessory accessory)
        {
            accessory.Method.Mark(accessory.Data.Me, KodakkuAssist.Module.GameOperate.MarkType.Attack1);
            accessory.Method.Mark((uint)accessory.Data.MyObject.TargetObjectId, KodakkuAssist.Module.GameOperate.MarkType.Attack2, true);
        }
        [ScriptMethod(eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:99999"])]
        public void TestClearMark(Event @event, ScriptAccessory accessory)
        {
            accessory.Method.MarkClear();
        }
        System.Threading.Timer t;
        [ScriptMethod(eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:99999"])]
        public void TestVarDestory(Event @event, ScriptAccessory accessory)
        {
            t = new(_ =>
            {
                accessory.Log.Debug($"来自版本F的timer，每5秒钟提示一次");


            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }
        public void TestRunOnFrame22()
        {
            var a = 111;
        }
        private static Vector4 HueToRGB(float hue)
        {
            float r = 0, g = 0, b = 0;
            float h = hue * 6f;
            int segment = (int)h;
            float f = h - segment;

            float p = 0f;
            float q = 0.5f * (1f - f);
            float t = 0.5f * f;

            switch (segment)
            {
                case 0: r = 1; g = t; b = p; break;
                case 1: r = q; g = 1; b = p; break;
                case 2: r = p; g = 1; b = t; break;
                case 3: r = p; g = q; b = 1; break;
                case 4: r = t; g = p; b = 1; break;
                case 5: r = 1; g = p; b = q; break;
            }

            return new Vector4(r, g, b, 1f);
        }

        public void Dispose()
        {
            t.Dispose();
        }
    }
}

