using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent.Struct;
using Dalamud.Utility.Numerics;

using KodakkuAssist.Data;
using KodakkuAssist.Extensions;

namespace RyougiMioScriptNamespace
{
    [ScriptType(name: "(M11S)AAC Heavyweight M3 (Savage)", territorys: [1324, 1325], guid: "725bcd38-1173-420e-a248-b3e11a1ff1b3", version: "0.1.5.0", author: "RyougiMio", note: "M11S，脚本同时在M11N/S中生效。")]
    public class RyougiMio_1325
    {
        #region Settings
        // ==================== 用户设置区域 ====================
        [UserSetting("是否开启屏幕文字提示")]
        public bool EnableText { get; set; } = true;
        [UserSetting("是否开启TTS语音提示")]
        public bool EnableTTS { get; set; } = true;

        [UserSetting("常用危险色")]
        public ScriptColor DangerColor { get; set; } = new ScriptColor() { V4 = new Vector4(1.0f, 0.0f, 0.0f, 0.01f) };
        [UserSetting("常用安全色")]
        public ScriptColor SafeColor { get; set; } = new ScriptColor() { V4 = new Vector4(0.0f, 1.0f, 0.0f, 0.01f) };
        [UserSetting("陨石颜色")]
        public ScriptColor SafeColor1 { get; set; } = new ScriptColor() { V4 = new Vector4(0.0f, 0.0f, 0.6f, 1.0f) };

        [UserSetting("指路/引导颜色 (默认为青)")]
        public ScriptColor GuideColor { get; set; } = new ScriptColor() { V4 = new Vector4(0.0f, 1.0f, 1.0f, 0.01f) };
        #endregion

        #region Variables
        // 定义一个类用来存物体信�?
        public class ObjectState
        {
            public uint DataId;
            public Vector3 Position;
            public float Rotation;
            public int GroupId; // 【新增】直接存储它�?1, 2 还是 3 �?
        }
        public class ObjectStateSix
        {
            public uint DataId;
            public Vector3 Position;
            public float Rotation;
            public int GroupId;
            public int Index;
            public bool IsDrawn; // 【新增】标记是否已经画�?
        }
        private ScriptAccessory _acc;


        [UserSetting("P4踩塔 攻略")]
        public Phase4_Towers Phase4_Towers1 { get; set; } = Phase4_Towers.全引导;

        public enum Phase4_Towers
        {
            全引导,
            近战顺远程逆
        }

        [UserSetting("铸兵之令：统治 指路配置")]
        public DominionGuidance DominionGuidance1 { get; set; } = DominionGuidance.标准22分摊;

        public enum DominionGuidance
        {
            标准22分摊,
            近战固定分摊
        }

        #endregion

        #region Methods


        // 自定义TTS方法：自动检�?EnableTTS 开�?
        private void QTTS(string text, int rate = 0)
        {
            if (!EnableTTS) return;
            _acc.Method.TTS(text, rate);
        }
        // 自定义文字提示方法：自动检�?EnableText 开�?
        private void QText(string text, int duration, bool isWarning = false)
        {
            if (!EnableText) return;
            _acc.Method.TextInfo(text, duration, isWarning);
        }
        // 1. 定义存储�?(Key: SourceId, Value: 物体状�?
        private Dictionary<uint, ObjectState> _objStorage = new Dictionary<uint, ObjectState>();
        // 【修改】字典类型也跟着�?
        private Dictionary<uint, ObjectStateSix> _objStorage1 = new Dictionary<uint, ObjectStateSix>();
        private int _setPosCount = 0;
        // 全局计数器，用于记录是第几个出现�?
        private int _orderCounter = 0;
        // 【新增】记�?47086 机制的开始时�?
        private long _mechanic47086StartTime = 0;
        // 在类成员变量区域定义计数器，用于记录该技能出现了多少�?
        private int _castCount_46131 = 0;
        private bool _hasCast46148 = false;
        // 存储被点�?001E 的玩�?ID
        private HashSet<uint> _markedPlayers = new HashSet<uint>();

        // 存储读条物体的列�?
        private List<MechanicObject> _castingObjects = new List<MechanicObject>();
        // 默认 false (没读�?
        private bool _hasCast46162 = false;
        // 变量定义区域添加
        private Dictionary<uint, long> _tether0039DrawnTime = new Dictionary<uint, long>();
        private HashSet<uint> _targetIcon001EPlayers = new HashSet<uint>();
        private List<(uint SourceId, uint ActionId, int Quadrant)> _castingObjects46166_46167 = new List<(uint, uint, int)>();
        private HashSet<int> _dominion46112Regions = new HashSet<int>();
        private long _dominion46112LastEventTicks = 0;
        private long _dominion46112LastDrawTicks = 0;
        private long _starTrackLastCastTicks = 0;
        private Dictionary<uint, (long Ticks, Vector3 Position, float Rotation)> _starTrackLastCastBySource
            = new Dictionary<uint, (long, Vector3, float)>();
        private long _starTrackFirstCastTicks = 0;
        private HashSet<int> _starTrackFirstBlocks = new HashSet<int>();
        // 定义物体结构
        private class MechanicObject
        {
            public uint ActionId;   // 46166 �?46167
            public uint SourceId;
            public int Quadrant;    // 1, 2, 3, 4
            public int Duration;
        }
        // --- 坐标定义 (忽略Y�? ---
        // Group 1 (0 ~ pi/2) 对应的坐�?
        private readonly List<Vector2> _group1Coords = new List<Vector2>
        {
            new Vector2(103.11f, 111.59f),
            new Vector2(111.59f, 103.11f)
        };

        // Group 2 (pi/2 ~ pi & -3pi/4 ~ -pi) 对应的坐�?
        private readonly List<Vector2> _group2Coords = new List<Vector2>
        {
            new Vector2(108.49f, 91.51f),
            new Vector2(96.89f, 88.41f)
        };

        // Group 3 (0 ~ -3pi/4) 对应的坐�?
        private readonly List<Vector2> _group3Coords = new List<Vector2>
        {
            new Vector2(88.41f, 96.90f),
            new Vector2(91.51f, 108.49f)
        };
        // 固定�?1-3-2 循环顺序
        private readonly int[] _fixedSequence = new int[] { 1, 3, 2 };

        // 合并所有合法坐标用�?SetObjPos 校验
        private List<Vector2> _allValidCoords;
        private readonly List<Vector2> _validCoords = new List<Vector2>
        {
            new Vector2(101.42f, 112.14f), new Vector2(106.00f, 110.39f),
            new Vector2(110.60f, 100.12f), new Vector2(108.92f, 99.15f),
            new Vector2(110.40f, 93.97f),  new Vector2(100.00f, 87.97f),
            new Vector2(89.60f, 93.97f),   new Vector2(91.08f, 99.15f),
            new Vector2(89.40f, 100.12f),  new Vector2(94.00f, 110.39f),
            new Vector2(98.58f, 112.14f)
        };

        // 定义 DataId 集合
        private readonly HashSet<uint> _targetDataIds = new HashSet<uint> { 19184, 19185, 19186 };

        // --- E. 绘图辅助方法 ---
        private void DrawMechanic(ObjectState obj, uint objId, int delay, int duration, uint bossId, ScriptAccessory accessory)
        {
            string baseName = $"Triple_{obj.DataId}_{delay}_{DateTime.Now.Ticks}";

            // 1. 物体范围
            if (obj.DataId == 19184) // 钢铁
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = baseName + "_Iron_Obj";
                dp.Position = obj.Position;
                dp.Scale = new Vector2(8f);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delay; dp.DestoryAt = duration; dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            else if (obj.DataId == 19185) // 月环
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = baseName + "_Moon_Obj";
                dp.Position = obj.Position;
                dp.Scale = new Vector2(60f); dp.InnerScale = new Vector2(5f);
                dp.Radian = float.Pi * 2;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delay; dp.DestoryAt = duration; dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
            }
            else if (obj.DataId == 19186) // 十字
            {
                for (int k = 0; k < 4; k++)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"{baseName}_Cross_Obj_{k}";
                    dp.Position = obj.Position;
                    dp.Rotation = obj.Rotation + (float)(Math.PI / 2 * k);
                    dp.Scale = new Vector2(10f, 40f);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Delay = delay; dp.DestoryAt = duration; dp.ScaleMode = ScaleMode.YByTime;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                }
            }

            // 2. 玩家机制
            if (obj.DataId == 19184) // 钢铁 -> 玩家�?
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = baseName + "_Iron_Player";
                dp.Owner = accessory.Data.Me;
                dp.Scale = new Vector2(6f);
                dp.Color = GuideColor.V4;
                dp.Delay = delay; dp.DestoryAt = duration; dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            else if (obj.DataId == 19185) // 月环 -> 扇形
            {
                var party = accessory.Data.PartyList;
                foreach (var tid in party)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"{baseName}_Moon_Player_{tid}";
                    dp.Owner = objId; dp.TargetObject = tid;
                    dp.Radian = float.Pi / 6; dp.Scale = new Vector2(60f);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Delay = delay; dp.DestoryAt = duration; dp.ScaleMode = ScaleMode.ByTime;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }
            }
            else if (obj.DataId == 19186) // 十字 -> T连线
            {
                var party = accessory.Data.PartyList;
                for (int i = 2; i <= 3; i++)
                {
                    if (i >= party.Count) break;
                    var tid = party[i];
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"{baseName}_Cross_Tank_{tid}";
                    dp.Owner = objId; dp.TargetObject = tid;
                    dp.Scale = new Vector2(6f); dp.ScaleMode = ScaleMode.YByDistance;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Delay = delay; dp.DestoryAt = duration;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                }
            }
        }
        // 【修改】参数类型改�?ObjectStateSix
        private void DrawMechanic(ObjectStateSix obj, uint objId, int delay, int duration, uint bossId, ScriptAccessory accessory)
        {
            string baseName = $"SixCombo_{obj.DataId}_{delay}_{DateTime.Now.Ticks}";

            // 1. 钢铁 (19184)
            if (obj.DataId == 19184)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = baseName + "_Iron_Obj";
                dp.Position = obj.Position;
                dp.Scale = new Vector2(8f);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delay; dp.DestoryAt = duration; dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                // 玩家安全�?
                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = baseName + "_Iron_Player";
                dp2.Owner = accessory.Data.Me;
                dp2.Scale = new Vector2(6f);
                dp2.Color = accessory.Data.DefaultSafeColor; // �?
                dp2.Delay = delay; dp2.DestoryAt = duration; dp2.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
            }
            // 2. 月环 (19185)
            else if (obj.DataId == 19185)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = baseName + "_Moon_Obj";
                dp.Position = obj.Position;
                dp.Scale = new Vector2(60f); dp.InnerScale = new Vector2(5f);
                dp.Radian = float.Pi * 2;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delay; dp.DestoryAt = duration; dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

                // 玩家扇形
                var party = accessory.Data.PartyList;
                foreach (var tid in party)
                {
                    var dpP = accessory.Data.GetDefaultDrawProperties();
                    dpP.Name = $"{baseName}_Moon_Player_{tid}";
                    dpP.Owner = objId; dpP.TargetObject = tid;
                    dpP.Radian = float.Pi / 6; dpP.Scale = new Vector2(60f);
                    dpP.Color = accessory.Data.DefaultDangerColor;
                    dpP.Delay = delay; dpP.DestoryAt = duration; dpP.ScaleMode = ScaleMode.ByTime;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpP);
                }
            }
            // 3. 十字 (19186)
            else if (obj.DataId == 19186)
            {
                for (int k = 0; k < 4; k++)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"{baseName}_Cross_Obj_{k}";
                    dp.Position = obj.Position;
                    dp.Rotation = obj.Rotation + (float)(Math.PI / 2 * k);
                    dp.Scale = new Vector2(10f, 40f);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Delay = delay; dp.DestoryAt = duration; dp.ScaleMode = ScaleMode.YByTime;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                }

                // 奶妈连线 (索引 2, 3)
                var party = accessory.Data.PartyList;
                for (int hi = 2; hi <= 3; hi++)
                {
                    if (hi >= party.Count) break;
                    var tid = party[hi];
                    var dpH = accessory.Data.GetDefaultDrawProperties();
                    dpH.Name = $"{baseName}_Cross_Healer_{tid}";
                    dpH.Owner = objId; dpH.TargetObject = tid;
                    dpH.Scale = new Vector2(6f); dpH.ScaleMode = ScaleMode.YByDistance;
                    dpH.Color = accessory.Data.DefaultDangerColor;
                    dpH.Delay = delay; dpH.DestoryAt = duration;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpH);
                }
            }

            uint myId = accessory.Data.Me;
            var partyList = accessory.Data.PartyList;
            int myIndex = partyList.IndexOf(myId);
            if (myIndex < 0) return;

            Vector3 targetPos;
            bool canGuide = true;
            if (obj.DataId == 19184)
            {
                float guideRot = obj.Rotation + MathF.PI;
                targetPos = GetPointByRotation(obj.Position, guideRot, 11f);
            }
            else if (obj.DataId == 19185)
            {
                int step = myIndex switch
                {
                    0 => 0, // MT
                    7 => 1, // D4
                    3 => 2, // H2
                    5 => 3, // D2
                    1 => 4, // ST
                    4 => 5, // D1
                    2 => 6, // H1
                    6 => 7, // D3
                    _ => -1
                };
                if (step < 0) return;
                float guideRot = obj.Rotation - (MathF.PI / 4f * step);
                targetPos = GetPointByRotation(obj.Position, guideRot, 3.5f);
            }
            else if (obj.DataId == 19186)
            {
                bool isStGroup = myIndex == 1 || myIndex == 3 || myIndex == 5 || myIndex == 7;
                float angle = isStGroup
                    ? obj.Rotation - (MathF.PI * 3f / 4f)
                    : obj.Rotation - (MathF.PI * 5f / 4f);
                targetPos = GetPointByRotation(obj.Position, angle, 11f);
            }
            else
            {
                canGuide = false;
                targetPos = default;
            }

            if (!canGuide) return;

            var dpGuide = accessory.Data.GetDefaultDrawProperties();
            dpGuide.Name = baseName + "_Guide";
            dpGuide.Owner = myId;
            dpGuide.TargetPosition = targetPos;
            dpGuide.Scale = new Vector2(0.5f);
            dpGuide.ScaleMode = ScaleMode.YByDistance;
            dpGuide.Color = GuideColor.V4;
            dpGuide.Delay = delay;
            dpGuide.DestoryAt = duration;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dpGuide);
        }
        private void TryDrawSingleObject(ObjectStateSix obj, uint objId, uint bossId, ScriptAccessory accessory)
        {
            if (obj.IsDrawn) return; // 避免重复�?

            // 计算理论上的时间�?
            // Index �?1 开始，所�?i = Index - 1
            int i = obj.Index - 1;

            int plannedDelayFromStart = 0;
            int duration = 0;

            if (i == 0)
            {
                plannedDelayFromStart = 0;
                duration = 7050;
            }
            else
            {
                // �?个物�?i=1) -> 延迟 7050 + 5140*1
                plannedDelayFromStart = 7050 + 5140 * (i - 1);
                duration = 5140;
            }

            // 计算实际需�?Delay 多久
            long now = DateTime.Now.Ticks;
            long targetTick = _mechanic47086StartTime + (plannedDelayFromStart * 10000); // 1ms = 10000 ticks

            long remainingDelayMs = (targetTick - now) / 10000;

            // 如果结果 < 0，说明时间已经过了，立刻画出来（Delay=0�?
            if (remainingDelayMs < 0) remainingDelayMs = 0;

            // 调用底层绘图
            DrawMechanic(obj, objId, (int)remainingDelayMs, duration, bossId, accessory);

            // 标记已绘�?
            obj.IsDrawn = true;
        }
        // ==================== 3. 核心处理逻辑 ====================
        private void ProcessMechanicLogic(ScriptAccessory accessory)
        {
            // 1. 获取自己的索�?
            var myId = accessory.Data.Me;
            var party = accessory.Data.PartyList;
            int myIndex = -1;

            for (int i = 0; i < party.Count; i++)
            {
                if (party[i] == myId) { myIndex = i; break; }
            }
            if (myIndex == -1)
            {
                _castingObjects46166_46167.Clear();
                _targetIcon001EPlayers.Clear();
                return;
            }

            // 2. 对列表中的物体进行分类和排序 (按象限从小到�?
            // 46166 列表
            var objs46166 = _castingObjects
                .Where(x => x.ActionId == 46166)
                .OrderBy(x => x.Quadrant)
                .ToList();

            // 46167 列表
            var objs46167 = _castingObjects
                .Where(x => x.ActionId == 46167)
                .OrderBy(x => x.Quadrant)
                .ToList();

            MechanicObject targetObj = null;

            // 3. 职能分配逻辑

            // --- MT (Index 0) ---
            if (myIndex == 0)
            {
                // 找第 1 �?46166
                if (objs46166.Count >= 1) targetObj = objs46166[0];
            }
            // --- ST (Index 1) ---
            else if (myIndex == 1)
            {
                // 找第 2 �?46166
                if (objs46166.Count >= 2) targetObj = objs46166[1];
            }
            // --- DPS & H (Index 2~7) ---
            else
            {
                // 先检查有没有 001E 点名，有则不�?
                if (_markedPlayers.Contains(myId)) return;

                // Index 4, 5 -> 找第 1 �?46167
                if (myIndex == 4 || myIndex == 5)
                {
                    if (objs46167.Count >= 1) targetObj = objs46167[0];
                }
                // Index 2, 3, 6, 7 -> 找第 2 �?46167
                else if (myIndex == 2 || myIndex == 3 || myIndex == 6 || myIndex == 7)
                {
                    if (objs46167.Count >= 2) targetObj = objs46167[1];
                }
            }

            // 4. 绘图
            if (targetObj != null)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Displace_Link_{targetObj.SourceId}_{DateTime.Now.Ticks}";

                // Displacement: Owner=物体, Target=玩家 -> 击退/指向效果
                dp.Owner = targetObj.SourceId;
                dp.TargetObject = myId;

                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Scale = new Vector2(0.5f);
                dp.DestoryAt = targetObj.Duration;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            }
        }


        #endregion

        #region Initialization 

        public void Init(ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw(".*");
            _acc = accessory;
            _setPosCount = 0;
            _hasCast46148 = false;
            _tripleComboSetPosCount = 0;
            _tripleComboRecordedIds.Clear(); // 新增
            _tether0039DrawnTime.Clear();
            _targetIcon001EPlayers.Clear();
            _castingObjects46166_46167.Clear();
            _dominion46112Regions.Clear();
            _dominion46112LastEventTicks = 0;
            _dominion46112LastDrawTicks = 0;
            _starTrackLastCastTicks = 0;
            _starTrackLastCastBySource.Clear();
            _starTrackFirstCastTicks = 0;
            _starTrackFirstBlocks.Clear();


            // 清空存储
            _objStorage.Clear();
            _objStorage1.Clear();
            _tripleComboStorage.Clear(); // 新增这行

            _hasCast46162 = false;
            _orderCounter = 0;
            _castCount_46131 = 0;
            _mechanic47086StartTime = 0; // 也建议重置这�?
            _allValidCoords = _group1Coords.Concat(_group2Coords).Concat(_group3Coords).ToList();
            _markedPlayers.Clear();
            _castingObjects.Clear();

            accessory.Method.SendChat("/e M11S Initialized.");
        }

        #endregion
        #region TTSonly 

        [ScriptMethod(name: "铸兵猛攻", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46087|46088|46089|46010|46012|46014)$"])]
        public void WeaponCall_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 46087 -> 斧子
            if (aid == 46087 || aid == 46010)
            {
                QTTS("钢铁");
                QText("钢铁", 3000, true);
            }
            // 46088 -> 镰刀
            else if (aid == 46088 || aid == 46012)
            {
                QTTS("月环");
                QText("月环", 3000, true);
            }
            // 46089 -> 大剑
            else if (aid == 46089 || aid == 46014)
            {
                QTTS("十字");
                QText("十字", 3000, true);
            }
        }
        [ScriptMethod(name: "历战之兵武TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46028|46102)$"])]
        public void TripleCharge_Alert(Event @event, ScriptAccessory accessory)
        {
            // 46028, 46102 -> 准备三连冲锋
            QTTS("准备三连冲锋");
            QText("准备三连冲锋", 3000, true);
        }
        [ScriptMethod(name: "铸兵之令：轰击TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46037|46114|46115)$"])]
        public void TankBuster_Combo_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 46037(N), 46114(S) -> 圆形分散 + 死刑
            if (aid == 46037 || aid == 46114)
            {
                QTTS("圆形分散加死刑");
                QText("圆形分散 + 死刑", 3000, true);
            }
            // 46115(S) -> 扇形分摊 + 死刑
            else if (aid == 46115)
            {
                QTTS("扇形分摊加死刑");
                QText("扇形分摊 + 死刑", 3000, true);
            }
        }
        [ScriptMethod(name: "霸王大漩涡TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46039|46117)$"])]
        public void HPtoOne_Alert(Event @event, ScriptAccessory accessory)
        {
            QTTS("清1血");
            QText("清1血", 3000, true);
        }

        [ScriptMethod(name: "万劫不朽的统治TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46042|46120)$"])]
        public void AOE_Alert_46042(Event @event, ScriptAccessory accessory)
        {
            QTTS("AOE");
            QText("AOE", 3000, true);
        }

        [ScriptMethod(name: "重陨石TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46152"])]
        public void Stack_Alert_46152(Event @event, ScriptAccessory accessory)
        {
            QTTS("分摊");
            QText("分摊", 3000, true);
        }
        [ScriptMethod(name: "冲击波TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46140"])]
        public void Meteor_Alert_46140(Event @event, ScriptAccessory accessory)
        {
            QTTS("大陨石");
            QText("大陨石", 3000, true);
        }
        [ScriptMethod(name: "回旋火TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47037|46170)$"])]
        public void RotatingFire_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 47038 双向回旋�?-> 双向 22分摊
            if (aid == 47038)
            {
                QTTS("双向 22分摊");
                // 如果需要文字提示可以把下面这行注释解开
                // QText("双向 22分摊", 4000, true);
            }
            // 46171 四向回旋�?-> 四向 四人分散
            else if (aid == 46171)
            {
                QTTS("四向 四人分散");
                // 如果需要文字提示可以把下面这行注释解开
                // QText("四向 四人分散", 4000, true);
            }
        }

        #endregion


        #region 铸兵之令
        [ScriptMethod(name: "铸兵之令：统治", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46035|46112)$"])]
        public void DoubleRectCleave_Draw(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
            float baseRotation = @event.SourceRotation;
            var tidStr = @event["TargetId"];
            if (!string.IsNullOrEmpty(tidStr) &&
                ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid))
            {
                var tObj = accessory.Data.Objects.SearchById(tid);
                if (tObj != null)
                {
                    baseRotation = tObj.Rotation;
                }
            }
            // 2. 循环�?2 条矩�?(0�?�?180�?
            for (int i = 0; i < 2; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Rect_Cleave_{aid}_{i}_{DateTime.Now.Ticks}";
                dp.Position = @event.SourcePosition;
                // i=0 -> baseRotation
                // i=1 -> baseRotation + PI (180�?
                dp.Rotation = baseRotation + (float)(Math.PI * i);
                dp.Scale = new Vector2(10f, 60f);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = dur;
                dp.ScaleMode = ScaleMode.YByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }

            if (aid == 46112)
            {
                TrackDominionGuidance(@event.SourcePosition, dur, accessory);
            }
        }



        #endregion





        #region 瞎猜环节



        [ScriptMethod(name: "重斩击（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46096"])]
        public void TrackingFan_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
            var tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) ||
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid))
                return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Track_Fan_{aid}_{DateTime.Now.Ticks}";
            dp.Owner = @event.SourceId;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.TargetObject = tid;
            dp.Scale = new Vector2(60f);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = dur;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(name: "轰击（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46133"])]
        public void TargetCircle_46133(Event @event, ScriptAccessory accessory)
        {
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            // 解析 TargetId
            var tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) ||
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid))
                return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Target_Circle_46133_{tid}_{DateTime.Now.Ticks}";
            dp.Owner = tid;
            dp.Scale = new Vector2(4f);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "兽焰连尾击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46072|46128|46073|46129)$"])]
        public void FrontBackFan_Draw(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Fan_FB_{aid}_{DateTime.Now.Ticks}";
            dp.Position = @event.SourcePosition;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Scale = new Vector2(60f);    // 半径 60m
            dp.Radian = float.Pi / 2;       // 90�?(π/2)
            dp.DestoryAt = dur;
            dp.ScaleMode = ScaleMode.ByTime;
            // 1. 前扇�?(46072, 46128)
            if (aid == 46072 || aid == 46128)
            {
                dp.Rotation = @event.SourceRotation;
            }
            // 2. 后扇�?(46073, 46129)
        
            else if (aid == 46073 || aid == 46129)
            {
                dp.Rotation = @event.SourceRotation + float.Pi; // �?80�?
            }
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(name: "登天碎地", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46064|46066|46068|46070|46155|46157|46159|46161)$"])]
        public void Rect_Gradient_40x40(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            // 1. 画原来的：正�?40x40
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Rect_Front_40x40_{aid}_{DateTime.Now.Ticks}";
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(40f, 40f); // �?0 �?0
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = dur;
            dp.ScaleMode = ScaleMode.YByTime; // 随时间填充长�?
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

            // 2. 画新增的：反�?60x60
            var dpBack = accessory.Data.GetDefaultDrawProperties();
            dpBack.Name = $"Rect_Back_60x60_{aid}_{DateTime.Now.Ticks}"; // 名字区分一下
            dpBack.Position = @event.SourcePosition;

            // 反方�?= 原方�?+ PI (180�?
            dpBack.Rotation = @event.SourceRotation + (float)Math.PI;

            dpBack.Scale = new Vector2(60f, 60f); // �?0 �?0
            dpBack.Color = accessory.Data.DefaultDangerColor;
            dpBack.DestoryAt = dur; // 时长相同
            dpBack.ScaleMode = ScaleMode.YByTime; // 也是渐变

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpBack);
        }
        [ScriptMethod(name: "铸兵之令：轰击圆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46114"])]
        public void Action_46114_Index(Event @event, ScriptAccessory accessory)
        {
            // 1. 获取持续时间�?+ 1�?
            if (!int.TryParse(@event["DurationMilliseconds"], out var castDuration)) return;
            int finalDuration = castDuration + 7300;

            // 2. 获取自己的索引，判断自己是否为坦�?(0, 1)
            uint myId = accessory.Data.Me;
            var partyIds = accessory.Data.PartyList;
            int myIndex = -1;

            for (int i = 0; i < partyIds.Count; i++)
            {
                if (partyIds[i] == myId)
                {
                    myIndex = i;
                    break;
                }
            }

            // 默认小队列表顺序�?,1 是坦�?
            bool amITank = (myIndex == 0 || myIndex == 1);

            // =========================================================
            // 第一部分：对仇恨列表�?2 位画�?
            // =========================================================

            var dpAggro = accessory.Data.GetDefaultDrawProperties();
            dpAggro.Name = $"Aggro2_{@event.SourceId}_{DateTime.Now.Ticks}";
            dpAggro.Owner = @event.SourceId; // 绑在BOSS身上

            // 使用 OwnerEnmityOrder (BOSS的仇恨列�?
            dpAggro.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
            dpAggro.CentreOrderIndex = 2; // �?�?

            dpAggro.Scale = new Vector2(6f);
            dpAggro.DestoryAt = finalDuration;
            dpAggro.ScaleMode = ScaleMode.ByTime; // 渐变

            // 颜色逻辑：我是T(0,1) -> 绿色(安全)，我是H/D -> 红色(危险)
            dpAggro.Color = amITank ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpAggro);


            // =========================================================
            // 第二部分：对所�?H �?D (索引 2-7) 画图
            // =========================================================

            for (int i = 0; i < partyIds.Count; i++)
            {
                // 如果索引 > 1，说明是 H (2,3) �?D (4,5,6,7)
                if (i > 1)
                {
                    var tid = partyIds[i];

                    var dpHD = accessory.Data.GetDefaultDrawProperties();
                    dpHD.Name = $"HD_Danger_{tid}_{DateTime.Now.Ticks}";

                    dpHD.Owner = tid; // 绑在该玩家身�?
                    dpHD.Scale = new Vector2(6f);
                    dpHD.Color = accessory.Data.DefaultDangerColor; // 永远是危险红
                    dpHD.DestoryAt = finalDuration;
                    dpHD.ScaleMode = ScaleMode.ByTime; // 渐变

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpHD);
                }
            }
        }
        [ScriptMethod(name: "铸兵之令：轰击扇", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46115"])]
        public void Action_46115_Logic(Event @event, ScriptAccessory accessory)
        {
            // 1. 获取原本持续时间�?+ 7.3�?(7300ms)
            if (!int.TryParse(@event["DurationMilliseconds"], out var castDuration)) return;
            int finalDuration = castDuration + 7300;

            // 2. 获取自己的索�?(0,1=T, >1=H/D)
            uint myId = accessory.Data.Me;
            var partyIds = accessory.Data.PartyList;
            int myIndex = -1;

            for (int i = 0; i < partyIds.Count; i++)
            {
                if (partyIds[i] == myId)
                {
                    myIndex = i;
                    break;
                }
            }

            // 定义弧度
            float rad90 = float.Pi / 2;
            float rad45 = float.Pi / 4;

            // =========================================================
            // 逻辑 A: 无论我是谁，两个 T (索引0�?) 身上都要�?90度危险扇�?
            // =========================================================
            for (int i = 0; i <= 1; i++)
            {
                // 防止小队人数不足报错
                if (i < partyIds.Count)
                {
                    var targetId = partyIds[i];

                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"Tank_Fan_90_{targetId}_{DateTime.Now.Ticks}";

                    // Boss 指向并绑定到玩家
                    dp.Owner = @event.SourceId;    // 起点：Boss
                    dp.TargetObject = targetId;    // 终点/朝向：玩�?

                    dp.Radian = rad90;             // 90�?
                    dp.Scale = new Vector2(60f);   // 长度 60
                    dp.Color = accessory.Data.DefaultDangerColor; // 危险�?

                    dp.DestoryAt = finalDuration;
                    dp.ScaleMode = ScaleMode.ByTime; // 渐变

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }
            }

            // =========================================================
            // 逻辑 B: 如果我是 H/D (索引 > 1)，对自己�?45度安全扇�?
            // =========================================================
            if (myIndex > 1)
            {
                var dpSafe = accessory.Data.GetDefaultDrawProperties();
                dpSafe.Name = $"HD_Safe_Fan_45_{myId}_{DateTime.Now.Ticks}";

                // Boss 指向并绑定到玩家(自己)
                dpSafe.Owner = @event.SourceId;
                dpSafe.TargetObject = myId;

                dpSafe.Radian = rad45;             // 45�?
                dpSafe.Scale = new Vector2(60f);   // 长度 60
                dpSafe.Color = accessory.Data.DefaultSafeColor; // 安全�?

                dpSafe.DestoryAt = finalDuration;
                dpSafe.ScaleMode = ScaleMode.ByTime; // 渐变

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpSafe);
            }
        }
        #region 三连斧镰剑改进版

        // ==================== 变量定义 ====================
        private Dictionary<uint, ObjectState> _tripleComboStorage = new Dictionary<uint, ObjectState>();
        private int _tripleComboSetPosCount = 0; // SetObjPos 计数�?
        private HashSet<uint> _tripleComboRecordedIds = new HashSet<uint>(); // 新增：记录已处理�?SourceId

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 检查坐标是否在列表中的某个点附�?
        /// </summary>
        private bool IsCloseToAny(List<Vector2> coords, Vector2 pos, float threshold = 1.0f)
        {
            foreach (var v in coords)
            {
                if (Vector2.Distance(pos, v) < threshold) return true;
            }
            return false;
        }

        /// <summary>
        /// 检查坐标是否在三连机制的有效范围内
        /// </summary>
        private bool IsValidTripleComboPosition(Vector2 pos)
        {
            return IsCloseToAny(_group1Coords, pos) ||
                IsCloseToAny(_group2Coords, pos) ||
                IsCloseToAny(_group3Coords, pos);
        }

        /// <summary>
        /// 根据 Boss 朝向，将物体按顺时针排序
        /// </summary>
        private List<IGameObject> SortWeaponsClockwiseWithTolerance(List<IGameObject> weapons, Vector3 center, float bossRotationRad)
        {
            if (weapons == null || weapons.Count == 0)
            {
                return new List<IGameObject>();
            }

            // Boss 朝向转角�?
            float bossRotationDeg = bossRotationRad * 180f / MathF.PI;
            if (bossRotationDeg < 0) bossRotationDeg += 360f;

            // 计算每个物体相对于中心的角度
            var weaponsWithAngle = weapons
                .Where(w => w != null)
                .Select(w => new
                {
                    Weapon = w,
                    Angle = GetAbsoluteAngle(w.Position, center)
                })
                .ToList();

            // 找到�?Boss 面向最接近的物体作为第一�?
            var firstWeapon = weaponsWithAngle
                .OrderBy(w => GetAngleDifference(w.Angle, bossRotationDeg))
                .First();

            float startAngle = firstWeapon.Angle;

            // 从第一个物体开始，按顺时针排序
            return weaponsWithAngle
                .OrderBy(w => GetRelativeAngleFrom(w.Angle, startAngle))
                .Select(w => w.Weapon)
                .ToList();
        }
        /// <summary>
        /// 计算点相对于中心和起始角度的顺时针角�?
        /// </summary>


        /// <summary>
        /// 计算两个角度之间的最小差�?(0-180)
        /// </summary>
        private float GetAngleDifference(float angle1, float angle2)
        {
            float diff = MathF.Abs(angle1 - angle2);
            if (diff > 180f) diff = 360f - diff;
            return diff;
        }

        /// <summary>
        /// 计算从起始角度开始的顺时针相对角�?(0-360)
        /// </summary>
        private float GetRelativeAngleFrom(float angle, float startAngle)
        {
            float relative = angle - startAngle;
            if (relative < 0) relative += 360f;
            return relative;
        }

        // ==================== 事件处理 ====================


        [ScriptMethod(name: "三连斧镰剑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46103"])]
        public async void Triple_Combo_Draw_Improved(Event @event, ScriptAccessory accessory)
        {
            uint bossId = (uint)@event.SourceId;
            
            await Task.Delay(2000);

            Vector3 center = new Vector3(100f, 0f, 100f);
            
            var weapons = accessory.Data.Objects
                .Where(obj => obj.DataId == 19184 || obj.DataId == 19185 || obj.DataId == 19186)
                .Where(obj => Vector2.Distance(new Vector2(obj.Position.X, obj.Position.Z), new Vector2(100f, 100f)) > 5f)
                .ToList();

            if (weapons.Count < 3)
            {
                accessory.Method.SendChat($"/e [警告] 物体数量不足: {weapons.Count}");
                return;
            }

            float bossRotation = @event.SourceRotation;
            try
            {
                var bossObj = accessory.Data.Objects.SearchById(bossId);
                if (bossObj != null)
                {
                    bossRotation = bossObj.Rotation;
                }
            }
            catch { }

            // 按顺时针排序
            var sortedWeapons = weapons
                .OrderBy(w => {
                    float dx = w.Position.X - center.X;
                    float dz = w.Position.Z - center.Z;
                    float angle = MathF.Atan2(dx, dz);
                    
                    float relative = angle - bossRotation;
                    // 归一化到 (-2π, 0]
                    while (relative > 0) relative -= MathF.PI * 2;
                    while (relative <= -MathF.PI * 2) relative += MathF.PI * 2;
                    
                    // �?-2π (�?-360°) 当作 0 处理
                    if (MathF.Abs(relative + MathF.PI * 2) < 0.01f) relative = 0;
                    
                    return -relative;
                })
                .ToList();

            int[] delays = { 0, 6300, 11400 };
            int[] durations = { 6300, 5100, 5100 };

            for (int i = 0; i < 3; i++)
            {
                var weapon = sortedWeapons[i];
                
                var obj = new ObjectState
                {
                    DataId = weapon.DataId,
                    Position = weapon.Position,
                    Rotation = weapon.Rotation,
                    GroupId = 0
                };

                DrawTripleComboMechanic(obj, weapon.EntityId, delays[i], durations[i], bossId, accessory);
            }
        }



        private float GetAbsoluteAngle(Vector3 point, Vector3 center)
        {
            float dx = point.X - center.X;
            float dz = point.Z - center.Z;
            
            float angleRad = MathF.Atan2(dx, dz);
            float angleDeg = angleRad * 180f / MathF.PI;
            if (angleDeg < 0) angleDeg += 360f;

            return angleDeg;
        }

        /// <summary>
        /// 直接对游戏物体按顺时针排�?
        /// </summary>
        private List<IGameObject> SortWeaponsClockwise(List<IGameObject> weapons, Vector3 center, float bossRotationRad)
        {
            if (weapons == null || weapons.Count == 0)
            {
                return new List<IGameObject>();
            }

            float bossRotationDeg = bossRotationRad * 180f / MathF.PI;
            if (bossRotationDeg < 0) bossRotationDeg += 360f;

            float startRotationDeg = bossRotationDeg;

            return weapons
                .Where(w => w != null)
                .OrderBy(w => GetRelativeAngle(w.Position, center, startRotationDeg))
                .ToList();
        }
        private float GetRelativeAngle(Vector3 point, Vector3 center, float startRotationDeg)
        {
            float dx = point.X - center.X;
            float dz = point.Z - center.Z;

            float angleRad = MathF.Atan2(dx, -dz);
            float angleDeg = angleRad * 180f / MathF.PI;
            if (angleDeg < 0) angleDeg += 360f;

            float relative = angleDeg - startRotationDeg;
            if (relative < 0) relative += 360f;

            return relative;
        }

        private static Vector3 GetPointByRotation(Vector3 origin, float rotation, float distance)
        {
            return new Vector3(
                origin.X + MathF.Sin(rotation) * distance,
                origin.Y,
                origin.Z + MathF.Cos(rotation) * distance
            );
        }

        private static int? GetDominionRegion(Vector3 position)
        {
            float dx = position.X - 100f;
            float dz = position.Z - 100f;

            if (MathF.Abs(dx) <= 1f && MathF.Abs(dz) <= 1f)
            {
                return null;
            }

            float absDx = MathF.Abs(dx);
            float absDz = MathF.Abs(dz);

            if (absDz > absDx)
            {
                return dz >= 0 ? 2 : 0; // south or north
            }

            return dx >= 0 ? 1 : 3; // east or west
        }
        private static float NormalizeAngle(float angle)
        {
            float twoPi = MathF.PI * 2f;
            while (angle > MathF.PI) angle -= twoPi;
            while (angle < -MathF.PI) angle += twoPi;
            return angle;
        }

        private static readonly Vector3[] _starTrackCenterBlockCenters = new[]
        {
            new Vector3(95f, 0f, 95f),   // NW
            new Vector3(105f, 0f, 95f),  // NE
            new Vector3(95f, 0f, 105f),  // SW
            new Vector3(105f, 0f, 105f)  // SE
        };

        private static HashSet<int> GetStarTrackHitBlocks(Vector3 lineOrigin, float lineRotation)
        {
            var result = new HashSet<int>();
            float sin = MathF.Sin(lineRotation);
            float cos = MathF.Cos(lineRotation);

            const float halfWidth = 5f;
            const float length = 60f;
            const float halfBlock = 5f;
            const float tolerance = 0.5f;
            float xLimit = halfWidth + halfBlock + tolerance;
            float zMin = -halfBlock - tolerance;
            float zMax = length + halfBlock + tolerance;

            for (int i = 0; i < _starTrackCenterBlockCenters.Length; i++)
            {
                Vector3 blockCenter = _starTrackCenterBlockCenters[i];
                float dx = blockCenter.X - lineOrigin.X;
                float dz = blockCenter.Z - lineOrigin.Z;

                float localX = dx * cos - dz * sin;
                float localZ = dx * sin + dz * cos;

                if (MathF.Abs(localX) <= xLimit &&
                    localZ >= zMin &&
                    localZ <= zMax)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private struct DominionSafePositions
        {
            public Vector3 NearRight;
            public Vector3 NearLeft;
            public Vector3 FarRight;
            public Vector3 FarLeft;
        }

        private static DominionSafePositions GetDominionSafePositions(int region)
        {
            Vector3 center = new Vector3(100f, 0f, 100f);
            Vector3 vertex = region switch
            {
                0 => new Vector3(100f, 0f, 80f),  // north
                1 => new Vector3(120f, 0f, 100f), // east
                2 => new Vector3(100f, 0f, 120f), // south
                3 => new Vector3(80f, 0f, 100f),  // west
                _ => new Vector3(100f, 0f, 100f)
            };

            bool axisIsX = MathF.Abs(vertex.X - 100f) > 0.01f;
            float axisBase = axisIsX ? vertex.X : vertex.Z;
            float axisDir = axisBase > 100f ? -1f : 1f;
            float nearAxis = axisBase + axisDir * 3.5f;
            float farAxis = axisBase + axisDir * 20f;
            float perpBase = axisIsX ? vertex.Z : vertex.X;

            Vector3 nearA;
            Vector3 nearB;
            Vector3 farA;
            Vector3 farB;

            if (axisIsX)
            {
                nearA = new Vector3(nearAxis, 0f, perpBase + 7f);
                nearB = new Vector3(nearAxis, 0f, perpBase - 7f);
                farA = new Vector3(farAxis, 0f, perpBase + 8.5f);
                farB = new Vector3(farAxis, 0f, perpBase - 8.5f);
            }
            else
            {
                nearA = new Vector3(perpBase + 7f, 0f, nearAxis);
                nearB = new Vector3(perpBase - 7f, 0f, nearAxis);
                farA = new Vector3(perpBase + 8.5f, 0f, farAxis);
                farB = new Vector3(perpBase - 8.5f, 0f, farAxis);
            }

            Vector3 forward = Vector3.Normalize(new Vector3(vertex.X - center.X, 0f, vertex.Z - center.Z));
            Vector3 right = new Vector3(-forward.Z, 0f, forward.X);

            bool nearAIsRight = Vector3.Dot(new Vector3(nearA.X - center.X, 0f, nearA.Z - center.Z), right) > 0f;
            bool farAIsRight = Vector3.Dot(new Vector3(farA.X - center.X, 0f, farA.Z - center.Z), right) > 0f;

            return new DominionSafePositions
            {
                NearRight = nearAIsRight ? nearA : nearB,
                NearLeft = nearAIsRight ? nearB : nearA,
                FarRight = farAIsRight ? farA : farB,
                FarLeft = farAIsRight ? farB : farA
            };
        }

        private static float GetDominionAxisValue(Vector3 point)
        {
            return MathF.Abs(point.X - 100f) > 0.01f ? point.X : point.Z;
        }

        private static IEnumerable<Vector3> GetDominionSafePoints(int region)
        {
            DominionSafePositions safe = GetDominionSafePositions(region);
            return new[] { safe.NearRight, safe.NearLeft, safe.FarRight, safe.FarLeft };
        }

        private void TrackDominionGuidance(Vector3 position, int duration, ScriptAccessory accessory)
        {
            long now = DateTime.Now.Ticks;
            long resetTicks = 150000000L; // 15s

            if (now - _dominion46112LastEventTicks > resetTicks)
            {
                _dominion46112Regions.Clear();
                _dominion46112LastDrawTicks = 0;
            }

            _dominion46112LastEventTicks = now;

            if (_dominion46112LastDrawTicks != 0 && now - _dominion46112LastDrawTicks < resetTicks)
            {
                return;
            }

            var region = GetDominionRegion(position);
            if (!region.HasValue)
            {
                return;
            }

            _dominion46112Regions.Add(region.Value);

            if (_dominion46112Regions.Count == 3)
            {
                int missing = Enumerable.Range(0, 4).First(r => !_dominion46112Regions.Contains(r));

                uint myId = accessory.Data.Me;
                var party = accessory.Data.PartyList;
                int myIndex = party.IndexOf(myId);

                if (myIndex >= 0)
                {
                    DominionSafePositions safe = GetDominionSafePositions(missing);
                    Vector3 targetPos = default;
                    bool hasTarget = true;

                    if (myIndex == 2 || myIndex == 6)
                    {
                        targetPos = safe.NearRight;
                    }
                    else if (myIndex == 3 || myIndex == 7)
                    {
                        targetPos = safe.NearLeft;
                    }
                    else if (DominionGuidance1 == DominionGuidance.标准22分摊)
                    {
                        if (myIndex == 0 || myIndex == 4)
                        {
                            targetPos = safe.FarRight;
                        }
                        else if (myIndex == 1 || myIndex == 5)
                        {
                            targetPos = safe.FarLeft;
                        }
                        else
                        {
                            hasTarget = false;
                        }
                    }
                    else
                    {
                        Vector3 farNegative = GetDominionAxisValue(safe.FarRight) < 100f ? safe.FarRight : safe.FarLeft;
                        Vector3 farPositive = GetDominionAxisValue(safe.FarRight) > 100f ? safe.FarRight : safe.FarLeft;

                        if (myIndex == 0 || myIndex == 4)
                        {
                            targetPos = farNegative;
                        }
                        else if (myIndex == 1 || myIndex == 5)
                        {
                            targetPos = farPositive;
                        }
                        else
                        {
                            hasTarget = false;
                        }
                    }

                    if (hasTarget)
                    {
                        var dpGuide = accessory.Data.GetDefaultDrawProperties();
                        dpGuide.Name = $"Dominion_46112_GuideLine_{myId}_{DateTime.Now.Ticks}";
                        dpGuide.Owner = myId;
                        dpGuide.TargetPosition = targetPos;
                        dpGuide.Scale = new Vector2(0.5f);
                        dpGuide.ScaleMode = ScaleMode.YByDistance;
                        dpGuide.Color = GuideColor.V4;
                        dpGuide.DestoryAt = duration;

                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dpGuide);
                    }
                }

                _dominion46112LastDrawTicks = now;
                _dominion46112Regions.Clear();
            }
            else if (_dominion46112Regions.Count > 3)
            {
                _dominion46112Regions.Clear();
            }
        }

        private void DrawTripleComboMechanic(ObjectState obj, uint objId, int delay, int duration, uint bossId, ScriptAccessory accessory)
        {
            string baseName = $"Triple_{obj.DataId}_{delay}_{DateTime.Now.Ticks}";

            // ========== 19184 = 斧子 = 钢铁 (圆形AOE) ==========
            if (obj.DataId == 19184)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = baseName + "_Iron_Obj";
                dp.Position = obj.Position;
                dp.Scale = new Vector2(8f);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delay;
                dp.DestoryAt = duration;
                dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                var dpPlayer = accessory.Data.GetDefaultDrawProperties();
                dpPlayer.Name = baseName + "_Iron_Player";
                dpPlayer.Owner = accessory.Data.Me;
                dpPlayer.Scale = new Vector2(6f);
                dpPlayer.Color = accessory.Data.DefaultSafeColor;
                dpPlayer.Delay = delay;
                dpPlayer.DestoryAt = duration;
                dpPlayer.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpPlayer);
            }
            // ========== 19185 = 镰刀 = 月环 (甜甜圈AOE) ==========
            else if (obj.DataId == 19185)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = baseName + "_Moon_Obj";
                dp.Position = obj.Position;
                dp.Scale = new Vector2(60f);
                dp.InnerScale = new Vector2(5f);
                dp.Radian = float.Pi * 2;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delay;
                dp.DestoryAt = duration;
                dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

                var party = accessory.Data.PartyList;
                foreach (var tid in party)
                {
                    var dpFan = accessory.Data.GetDefaultDrawProperties();
                    dpFan.Name = $"{baseName}_Moon_Fan_{tid}";
                    dpFan.Owner = objId;
                    dpFan.TargetObject = tid;
                    dpFan.Radian = float.Pi / 6;
                    dpFan.Scale = new Vector2(60f);
                    dpFan.Color = accessory.Data.DefaultDangerColor;
                    dpFan.Delay = delay;
                    dpFan.DestoryAt = duration;
                    dpFan.ScaleMode = ScaleMode.ByTime;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpFan);
                }
            }
            // ========== 19186 = 大剑 = 十字 (十字AOE) ==========
            else if (obj.DataId == 19186)
            {
                for (int k = 0; k < 4; k++)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"{baseName}_Cross_Obj_{k}";
                    dp.Position = obj.Position;
                    dp.Rotation = obj.Rotation + (float)(Math.PI / 2 * k);
                    dp.Scale = new Vector2(10f, 40f);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Delay = delay;
                    dp.DestoryAt = duration;
                    dp.ScaleMode = ScaleMode.YByTime;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                }

                var party = accessory.Data.PartyList;
                for (int hi = 2; hi <= 3; hi++)
                {
                    if (hi >= party.Count) break;
                    var tid = party[hi];

                    var dpRect = accessory.Data.GetDefaultDrawProperties();
                    dpRect.Name = $"{baseName}_Cross_Healer_Rect_{tid}";
                    dpRect.Owner = objId;
                    dpRect.TargetObject = tid;
                    dpRect.Scale = new Vector2(6f, 60f);
                    dpRect.Color = accessory.Data.DefaultDangerColor;
                    dpRect.Delay = delay + 2500;
                    dpRect.DestoryAt = Math.Max(duration - 2500, 1000);
                    dpRect.ScaleMode = ScaleMode.YByTime;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpRect);
                }
            }

            uint myId = accessory.Data.Me;
            var partyList = accessory.Data.PartyList;
            int myIndex = partyList.IndexOf(myId);
            if (myIndex < 0) return;

            Vector3 targetPos;
            bool canGuide = true;
            if (obj.DataId == 19184)
            {
                float guideRot = obj.Rotation + MathF.PI;
                targetPos = GetPointByRotation(obj.Position, guideRot, 11f);
            }
            else if (obj.DataId == 19185)
            {
                int step = myIndex switch
                {
                    0 => 0, // MT
                    7 => 1, // D4
                    3 => 2, // H2
                    5 => 3, // D2
                    1 => 4, // ST
                    4 => 5, // D1
                    2 => 6, // H1
                    6 => 7, // D3
                    _ => -1
                };
                if (step < 0) return;
                float guideRot = obj.Rotation - (MathF.PI / 4f * step);
                targetPos = GetPointByRotation(obj.Position, guideRot, 3.5f);
            }
            else if (obj.DataId == 19186)
            {
                bool isStGroup = myIndex == 1 || myIndex == 3 || myIndex == 5 || myIndex == 7;
                float angle = isStGroup
                    ? obj.Rotation - (MathF.PI * 3f / 4f)
                    : obj.Rotation - (MathF.PI * 5f / 4f);
                targetPos = GetPointByRotation(obj.Position, angle, 11f);
            }
            else
            {
                canGuide = false;
                targetPos = default;
            }

            if (!canGuide) return;

            var dpGuide = accessory.Data.GetDefaultDrawProperties();
            dpGuide.Name = baseName + "_Guide";
            dpGuide.Owner = myId;
            dpGuide.TargetPosition = targetPos;
            dpGuide.Scale = new Vector2(0.5f);
            dpGuide.ScaleMode = ScaleMode.YByDistance;
            dpGuide.Color = GuideColor.V4;
            dpGuide.Delay = delay;
            dpGuide.DestoryAt = duration;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dpGuide);
        }

        #endregion

        [ScriptMethod(name: "记录6连斧镰剑", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:regex:^(19184|19185|19186)$"])]
        public void Record_Obj_Pos1(Event @event, ScriptAccessory accessory)
        {
            Vector3 rawPos = @event.SourcePosition;
            Vector2 checkPos = new Vector2(rawPos.X, rawPos.Z);

            bool isValid = false;
            foreach (var v in _validCoords)
            {
                if (Vector2.Distance(checkPos, v) < 1.0f)
                {
                    isValid = true;
                    break;
                }
            }
            if (!isValid) return;

            uint sid = (uint)@event.SourceId;

            if (_objStorage1.ContainsKey(sid))
            {
                _objStorage1.Remove(sid);
            }
            else
            {
                if (uint.TryParse(@event["SourceDataId"], out var did))
                {
                    _orderCounter++;

                    var newObj = new ObjectStateSix
                    {
                        DataId = did,
                        Position = rawPos,
                        Rotation = @event.SourceRotation,
                        Index = _orderCounter,
                        IsDrawn = false
                    };
                    _objStorage1[sid] = newObj;

                    // 【关键逻辑】如果机制已经开�?在最�?0秒内)，且该物体还没画，立刻补�?
                    // 这种情况属于：BOSS先读条，物体后刷出来
                    long now = DateTime.Now.Ticks;
                    if (_mechanic47086StartTime > 0 && (now - _mechanic47086StartTime < 20 * 10000000))
                    {
                        TryDrawSingleObject(newObj, sid, (uint)@event.SourceId, accessory);
                    }
                }
            }
        }
        [ScriptMethod(name: "6连斧镰剑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47086"])]
        public void Action_47086_Draw(Event @event, ScriptAccessory accessory)
        {
            // 1. 记录机制开始时�?
            _mechanic47086StartTime = DateTime.Now.Ticks;

            // 2. 遍历当前已有的物体进行绘�?
            // (防止物体是先刷出来，BOSS后读条的情况)
            if (_objStorage1.Count == 0) return;

            foreach (var kvp in _objStorage1)
            {
                TryDrawSingleObject(kvp.Value, kvp.Key, (uint)@event.SourceId, accessory);
            }
        }
        [ScriptMethod(name: "大漩涡", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46120"])]
        public void Action_46120_Fan(Event @event, ScriptAccessory accessory)
        {
            // 1. 找到场上所�?DataId �?19183 的物�?
            var towers = accessory.Data.Objects.Where(x => x.DataId == 19183).ToList();

            if (towers.Count == 0) return;

            // 2. 遍历每一个物�?
            foreach (var tower in towers)
            {
                // 3. 对最近的 2 个玩家画�?(Index 1 �?2)
                for (uint i = 1; i <= 2; i++)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"Fan_19183_{tower.EntityId}_{i}_{DateTime.Now.Ticks}";

                    // 【关键】Owner 设为物体，这样“最近”就是相对于物体的距�?
                    dp.Owner = tower.EntityId;

                    // 使用你要求的方法：自动解析最近的玩家
                    dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dp.TargetOrderIndex = i; // 1 = 最近的, 2 = 第二近的

                    dp.Scale = new Vector2(60f);   // �?60
                    dp.Radian = float.Pi / 2;      // 90�?
                    dp.Color = accessory.Data.DefaultDangerColor; // 渐变危险
                    dp.DestoryAt = 2300;           // 4�?
                    dp.ScaleMode = ScaleMode.ByTime; // 渐变填充

                    // Fan 类型会自动追�?Target (即解析出的玩�?
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }
            }
        }
        [ScriptMethod(name: "19183危险圆", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19183"])]
        public void OnAddCombatant_19183_Circle(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Circle_19183_{@event.SourceId}";
            dp.Position = @event.SourcePosition;
            dp.Scale = new Vector2(4f);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 120000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "19183危险圆移除", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:19183"])]
        public void OnRemoveCombatant_19183_Circle(Event @event, ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw($"Circle_19183_{@event.SourceId}");
        }
        [ScriptMethod(name: "星轨链", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46131"])]
        public void OnCast_46131(Event @event, ScriptAccessory accessory)
        {
            long nowTicks = DateTime.Now.Ticks;
            uint sourceId = (uint)@event.SourceId;
            Vector3 sourcePos = @event.SourcePosition;
            float sourceRot = @event.SourceRotation;
            const int duplicateWindowMs = 300;
            const float duplicatePosTolerance = 0.2f;
            const float duplicateRotTolerance = 0.05f;
            if (_starTrackLastCastBySource.TryGetValue(sourceId, out var lastCast))
            {
                bool withinTime = nowTicks - lastCast.Ticks < duplicateWindowMs * 10000L;
                bool samePos = Vector3.Distance(lastCast.Position, sourcePos) <= duplicatePosTolerance;
                bool sameRot = MathF.Abs(NormalizeAngle(sourceRot - lastCast.Rotation)) <= duplicateRotTolerance;
                if (withinTime && samePos && sameRot)
                {
                    return;
                }
            }
            _starTrackLastCastBySource[sourceId] = (nowTicks, sourcePos, sourceRot);

            int castIndex = System.Threading.Interlocked.Increment(ref _castCount_46131);
            _starTrackLastCastTicks = nowTicks;

            int roundIndex = ((castIndex - 1) / 2) % 8 + 1;
            bool isFirstInRound = castIndex % 2 == 1;
            int delayDanger = 3800;
            int dangerDuration = 2200;
            bool isStartRound = roundIndex == 1 || roundIndex == 5;
            if (isStartRound)
            {
                delayDanger = 0;
                dangerDuration = 6000;
            }

            // 4. 构建绘图
            var dpDanger = accessory.Data.GetDefaultDrawProperties();
            dpDanger.Name = $"Rect_46131_Danger_{castIndex}_{DateTime.Now.Ticks}";
            dpDanger.Color = accessory.Data.DefaultDangerColor;
            dpDanger.Scale = new Vector2(10f, 60f);
            dpDanger.Position = sourcePos;
            dpDanger.Rotation = sourceRot;
            dpDanger.ScaleMode = ScaleMode.ByTime;
            dpDanger.Delay = delayDanger;
            dpDanger.DestoryAt = dangerDuration;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpDanger);

            if (isFirstInRound)
            {
                _starTrackFirstCastTicks = nowTicks;
                _starTrackFirstBlocks = new HashSet<int>(GetStarTrackHitBlocks(sourcePos, sourceRot));
                return;
            }

            if (_starTrackFirstBlocks.Count == 0)
            {
                _starTrackFirstCastTicks = 0;
                _starTrackFirstBlocks.Clear();
                return;
            }

            _starTrackFirstCastTicks = 0;
            _starTrackFirstBlocks.Clear();
        }
        // ==================== 3. 监听 46148 读条并记�?====================
        [ScriptMethod(name: "记录状态", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46148"])]
        public void Record_46148(Event @event, ScriptAccessory accessory)
        {
            // 一旦监测到这个读条，就标记�?true
            _hasCast46148 = true;

            // (可�? 可以在屏幕打印一句调试信息，确认脚本记录到了
            // accessory.Method.SendChat("/e Detected 46148, Flag set to true."); 
        }


        [ScriptMethod(name: "彗星/火焰吐息", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:00F4"])]
        public void OnTargetIcon_00F4(Event @event, ScriptAccessory accessory)
        {
            // 通用步骤：解析被点名玩家 ID
            string tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) ||
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var targetId))
            {
                return;
            }

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Color = accessory.Data.DefaultDangerColor;
            // 渐变模式都是一样的
            dp.ScaleMode = ScaleMode.ByTime;

            // ================= 分支逻辑 =================

            if (!_hasCast46148)
            {
                // Case A: 46148 还没读过 -> �?8.2s 的圆 (4m)
                dp.Name = $"Icon_00F4_Circle_{targetId}_{DateTime.Now.Ticks}";
                dp.Owner = targetId; // 绑在玩家身上
                dp.Scale = new Vector2(4f); // 半径 4m
                dp.DestoryAt = 8200; // 持续 8.2s

                // 只有这里需�?ScaleMode �?ByTime (圆扩�?，下面矩形需�?YByTime
                dp.ScaleMode = ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            else
            {
                // Case B: 46148 已经读过 -> 延迟 3s，从 19180 连线 (�?m)

                // 1. 寻找场上�?19180 物体
                // (如果有多个，这里默认找第一个；如果需要找最近的，可以用 OrderByDistance)
                var sourceObj = accessory.Data.Objects.FirstOrDefault(x => x.DataId == 19180);
                if (sourceObj == null) return; // 没找到物体就不画

                dp.Name = $"Link_Delay3s_19180_{targetId}_{DateTime.Now.Ticks}";

                // 2. 连线关系：起�?19180 -> 终点 玩家
                dp.Owner = sourceObj.EntityId;
                dp.TargetObject = targetId;

                // 3. 尺寸：宽 6m，长 60m
                dp.Scale = new Vector2(6f, 60f);

                // 4. 时间控制：延�?3s，持�?5s
                dp.Delay = 3000;
                dp.DestoryAt = 6500;

                // 5. 动画：矩形伸�?
                dp.ScaleMode = ScaleMode.YByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
        [ScriptMethod(name: "王者陨石震", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0039"])]
        public void OnTether_0039(Event @event, ScriptAccessory accessory)
        {
            // 【关键修改】如果还没读�?46162，直接结束，不画�?
            if (!_hasCast46162)
            {
                return;
            }

            // --- 以下是原本的绘图逻辑 ---

            // 1. 解析目标玩家 (TargetId)
            string tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) ||
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var targetId))
            {
                return;
            }

            // 2. 构建绘图属�?
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = $"Tether_0039_Rect_{targetId}_{DateTime.Now.Ticks}";

            // 颜色：危�?
            dp.Color = accessory.Data.DefaultDangerColor;

            // 尺寸：宽 10m，长 60m
            dp.Scale = new Vector2(10f, 60f);

            // 起点：连线的发起�?(Source)
            dp.Owner = @event.SourceId;

            // 终点/朝向：连线的接受�?(Target玩家)
            dp.TargetObject = targetId;

            // 持续时间 7.5�?
            dp.DestoryAt = 7500;

            // 动画：随时间填充 (渐变效果)
            dp.ScaleMode = ScaleMode.YByTime;

            // 3. 发送绘�?
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "回旋火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47037|46170)$"])]
        public void RotatingFire_Draw(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 获取读条持续时间
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            // 1. 根据 ID 判断连线人数
            // 47038 (双向) -> 最�?2 �?
            // 46171 (四向) -> 最�?4 �?
            int targetCount = (aid == 47037) ? 2 : 4;

            // 2. 循环绘制每一条矩�?
            for (uint i = 1; i <= targetCount; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();

                dp.Name = $"Fire_Rect_Link_{aid}_{i}_{DateTime.Now.Ticks}";
                dp.Color = GuideColor.V4;

                // 尺寸：宽 6m，长 60m (给长一点以保证覆盖)
                dp.Scale = new Vector2(6f, 60f);

                // 起点：BOSS (SourcePosition)
                dp.Owner = @event.SourceId;

                // 终点/朝向：自动解析最近的玩家
                // i=1 为最近的�?个，i=2 为最近的�?�?..
                dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                dp.TargetOrderIndex = i;

                // 持续时间
                dp.DestoryAt = dur;

                // 动画：随时间填充 (渐变)
                dp.ScaleMode = ScaleMode.YByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
        // ==================== 1. 记录 TargetIcon 001E ====================
        [ScriptMethod(name: "记录点名001E", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:001E"])]
        public void OnTargetIcon_001E(Event @event, ScriptAccessory accessory)
        {
            string tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) ||
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var targetId))
            {
                return;
            }
            _markedPlayers.Add((uint)targetId);
        }

        // ==================== 2. 处理读条 + 计数触发 ====================
        // [ScriptMethod(name: "象限连线机制_计数触发", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46166|46167)$"])]
        // public void OnCast_Mechanic_Count(Event @event, ScriptAccessory accessory)
        // {
        //     if (!uint.TryParse(@event["ActionId"], out var aid)) return;
        //     if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

        //     Vector3 pos = @event.SourcePosition;

        //     // --- A. 计算象限 ---
        //     int quadrant = 0;
        //     // 按照您的定义�?
        //     if (pos.Z < 100 && pos.X > 100) quadrant = 1;      // 右上
        //     else if (pos.X > 100 && pos.Z > 100) quadrant = 2; // 右下
        //     else if (pos.X < 100 && pos.Z > 100) quadrant = 3; // 左下
        //     else if (pos.X < 100 && pos.Z < 100) quadrant = 4; // 左上

        //     if (quadrant == 0) return;

        //     // --- B. 加入列表 ---
        //     _castingObjects.Add(new MechanicObject
        //     {
        //         ActionId = aid,
        //         SourceId = (uint)@event.SourceId,
        //         Quadrant = quadrant,
        //         Duration = dur
        //     });

        //     // --- C. 计数判定 ---
        //     // 当且仅当收集到第 4 个物体时，触发逻辑
        //     if (_castingObjects.Count == 4)
        //     {
        //         ProcessMechanicLogic(accessory);
        //     }
        // }
        [ScriptMethod(name: "ENV22-25", eventType: EventTypeEnum.EnvControl, eventCondition: ["Index:regex:^(22|23|24|25)$"])]
        public void OnEnvControl_Rect_Draw(Event @event, ScriptAccessory accessory)
        {
            // ============================================================
            // 1. 直接读取 Flag
            // ============================================================
            // 不用 ContainsKey，直接信你用 Flag
            string flagStr = @event["Flag"];

            // 解析十六进制字符�?
            if (string.IsNullOrEmpty(flagStr) ||
                !uint.TryParse(flagStr, System.Globalization.NumberStyles.HexNumber, null, out uint flagValue))
            {
                return;
            }

            // 2. 核心判定：Flag 必须�?2
            if (flagValue != 2) return;

            // ============================================================
            // 3. 解析 Index 并确�?X 坐标
            // ============================================================
            if (!int.TryParse(@event["Index"], out int index)) return;

            float posX = 0;
            switch (index)
            {
                case 22: posX = 79f; break;
                case 23: posX = 89f; break;
                case 24: posX = 111f; break;
                case 25: posX = 121f; break;
                default: return;
            }

            // ============================================================
            // 4. 绘图执行
            // ============================================================
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Env_Rect_{index}_{DateTime.Now.Ticks}";
            dp.Color = SafeColor1.V4;

            // 尺寸: 40x5 (X=5, Y=40)
            dp.Scale = new Vector2(10f, 40f);

            // 位置与朝�?
            // Z范围 80~120 (全长40)
            // 设起�?Z=80，朝�?0 (正南/Z增加方向)，长 40 -> 完美覆盖
            dp.Position = new Vector3(posX, 0, 80f);
            dp.Rotation = 0f;

            // 时间控制: 延时 23秒，持续 5�?
            dp.Delay = 23000;
            dp.DestoryAt = 5600;

            // 动画: 渐变
            dp.ScaleMode = ScaleMode.YByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "直线连线", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0039|00F9)$"])]
        public void OnTether_0039_00F9(Event @event, ScriptAccessory accessory)
        {
            // 如果已经读过 46162，不处理
            if (_hasCast46162)
            {
                return;
            }

            // 解析 TargetId
            string tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) ||
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var targetId))
            {
                return;
            }

            uint tid = (uint)targetId;
            long now = DateTime.Now.Ticks;
            long cooldown = 28 * 10000000L; // 28秒，单位�?ticks (1�?= 10000000 ticks)

            // 检查是否在冷却时间�?
            if (_tether0039DrawnTime.TryGetValue(tid, out long lastTime))
            {
                if (now - lastTime < cooldown)
                {
                    // 还在冷却中，不重复画
                    return;
                }
            }

            // 记录当前时间
            _tether0039DrawnTime[tid] = now;

            // 画矩�?
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"0039_{tid}";
            dp.Owner = @event.SourceId;
            dp.TargetObject = tid;
            dp.Scale = new Vector2(10f, 60f);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 23000;
            dp.DestoryAt = 5600;
            dp.ScaleMode = ScaleMode.YByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "检测46162", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46162"])]
        public void OnCast_46162(Event @event, ScriptAccessory accessory)
        {
            _hasCast46162 = true;
        }
        #endregion
        #region �?
        [ScriptMethod(name: "记录点名001E", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:001E"])]
        public void OnTargetIcon_001E_Record(Event @event, ScriptAccessory accessory)
        {
            string tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) ||
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var targetId))
            {
                return;
            }

            _targetIcon001EPlayers.Add((uint)targetId);
        }

        private object _lock46166_46167 = new object();

        [ScriptMethod(name: "1122塔", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46166|46167)$"])]
        public void OnCast_46166_46167_Record(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var actionId)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var duration)) return;

            Vector3 pos = @event.SourcePosition;
            uint sourceId = (uint)@event.SourceId;

            int quadrant = 0;
            if (pos.X > 100 && pos.Z < 100) quadrant = 1;
            else if (pos.X > 100 && pos.Z > 100) quadrant = 2;
            else if (pos.X < 100 && pos.Z > 100) quadrant = 3;
            else if (pos.X < 100 && pos.Z < 100) quadrant = 4;

            if (quadrant == 0) return;

            int currentCount = 0;
            bool shouldDraw = false;

            lock (_lock46166_46167)
            {
                if (_castingObjects46166_46167.Any(x => x.SourceId == sourceId))
                {
                    return;
                }

                _castingObjects46166_46167.Add((sourceId, actionId, quadrant));
                currentCount = _castingObjects46166_46167.Count;
                
                if (currentCount >= 4)
                {
                    shouldDraw = true;
                }
            }

            if (shouldDraw)
            {
                if (Phase4_Towers1 == Phase4_Towers.近战顺远程逆)
                {
                    DrawDisplacementLogic(accessory, duration);
                }
                if (Phase4_Towers1 == Phase4_Towers.全引导)
                {
                    DrawDisplacementLogic1(accessory, duration);
                }
            }
        }
        private void DrawDisplacementLogic1(ScriptAccessory accessory, int duration)
        {
            uint myId = accessory.Data.Me;
            var party = accessory.Data.PartyList;
            int myIndex = -1;

            for (int i = 0; i < party.Count; i++)
            {
                if (party[i] == myId)
                {
                    myIndex = i;
                    break;
                }
            }

            if (myIndex == -1)
            {
                _castingObjects46166_46167.Clear();
                _targetIcon001EPlayers.Clear();
                return;
            }

            var objs46166 = _castingObjects46166_46167
                .Where(x => x.ActionId == 46166)
                .OrderBy(x => x.Quadrant)
                .ToList();

            var objs46167 = _castingObjects46166_46167
                .Where(x => x.ActionId == 46167)
                .OrderBy(x => x.Quadrant)
                .ToList();

            uint targetSourceId = 0;
            bool skipDraw = false;

            if (myIndex == 0)
            {
                if (objs46166.Count >= 1)
                {
                    targetSourceId = objs46166[0].SourceId;
                }
            }
            else if (myIndex == 1)
            {
                if (objs46166.Count >= 2)
                {
                    targetSourceId = objs46166[1].SourceId;
                }
            }
            else if (myIndex == 4)
            {
                if (objs46167.Count >= 1)
                {
                    targetSourceId = objs46167[0].SourceId;
                }
            }
            else if (myIndex == 5)
            {
                if (objs46167.Count >= 2)
                {
                    targetSourceId = objs46167[1].SourceId;
                }
            }
            else if (myIndex == 2 || myIndex == 3 || myIndex == 6 || myIndex == 7)
            {
                if (_targetIcon001EPlayers.Contains(myId))
                {
                    skipDraw = true;
                }
                else
                {
                    int[] priorityOrder = { 2, 6, 7, 3 };

                    var availablePlayers = priorityOrder
                        .Where(idx => idx < party.Count && !_targetIcon001EPlayers.Contains(party[idx]))
                        .ToList();

                    int myRank = -1;
                    for (int i = 0; i < availablePlayers.Count; i++)
                    {
                        if (availablePlayers[i] == myIndex)
                        {
                            myRank = i;
                            break;
                        }
                    }

                    if (myRank == -1)
                    {
                        skipDraw = true;
                    }
                    else if (myRank == 0)
                    {
                        if (objs46167.Count >= 1)
                        {
                            targetSourceId = objs46167[0].SourceId;
                        }
                    }
                    else if (myRank == 1)
                    {
                        if (objs46167.Count >= 2)
                        {
                            targetSourceId = objs46167[1].SourceId;
                        }
                    }
                    else
                    {
                        skipDraw = true;
                    }
                }
            }

            if (!skipDraw && targetSourceId != 0)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Displacement_{myId}_{targetSourceId}_{DateTime.Now.Ticks}";
                dp.Owner = myId;
                dp.TargetObject = targetSourceId;
                dp.Scale = new Vector2(0.5f);
                dp.ScaleMode = ScaleMode.YByDistance;
                dp.Color = GuideColor.V4;
                dp.DestoryAt = duration;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            }

            _castingObjects46166_46167.Clear();
            _targetIcon001EPlayers.Clear();
        }


        private void DrawDisplacementLogic(ScriptAccessory accessory, int duration)
        {
            uint myId = accessory.Data.Me;
            var party = accessory.Data.PartyList;
            int myIndex = -1;

            for (int i = 0; i < party.Count; i++)
            {
                if (party[i] == myId)
                {
                    myIndex = i;
                    break;
                }
            }

            if (myIndex == -1)
            {
                _castingObjects46166_46167.Clear();
                _targetIcon001EPlayers.Clear();
                return;
            }

            var objs46166 = _castingObjects46166_46167
                .Where(x => x.ActionId == 46166)
                .OrderBy(x => x.Quadrant)
                .ToList();

            var objs46167 = _castingObjects46166_46167
                .Where(x => x.ActionId == 46167)
                .OrderBy(x => x.Quadrant)
                .ToList();

            uint targetSourceId = 0;
            bool skipDraw = false;

            if (myIndex == 0)
            {
                if (objs46166.Count >= 1)
                {
                    targetSourceId = objs46166[0].SourceId;
                }
            }
            else if (myIndex == 1)
            {
                if (objs46166.Count >= 2)
                {
                    targetSourceId = objs46166[1].SourceId;
                }
            }
            else if (myIndex >= 2 && myIndex <= 7)
            {
                if (_targetIcon001EPlayers.Contains(myId))
                {
                    skipDraw = true;
                }
                else
                {
                    if (myIndex == 4 || myIndex == 5)
                    {
                        if (objs46167.Count >= 1)
                        {
                            targetSourceId = objs46167[0].SourceId;
                        }
                    }
                    else if (myIndex == 2 || myIndex == 3 || myIndex == 6 || myIndex == 7)
                    {
                        if (objs46167.Count >= 2)
                        {
                            targetSourceId = objs46167[1].SourceId;
                        }
                    }
                }
            }

            if (!skipDraw && targetSourceId != 0)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Displacement_{myId}_{targetSourceId}_{DateTime.Now.Ticks}";
                dp.Owner = myId;
                dp.TargetObject = targetSourceId;
                dp.Scale = new Vector2(0.5f);
                dp.ScaleMode = ScaleMode.YByDistance;
                dp.Color = GuideColor.V4;
                dp.DestoryAt = duration;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            }

            _castingObjects46166_46167.Clear();
            _targetIcon001EPlayers.Clear();
        }
        [ScriptMethod(name: "玩家死亡移除连线", eventType: EventTypeEnum.Death)]
        public void OnPlayerDeath(Event @event, ScriptAccessory accessory)
        {
            // 解析死亡�?TargetId
            string tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) ||
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var targetId))
            {
                return;
            }

            uint tid = (uint)targetId;

            // 移除对应的画图元�?
            accessory.Method.RemoveDraw($"0039_{tid}");

            // 同时从冷却记录中移除，以便复活后可以重新�?
            if (_tether0039DrawnTime.ContainsKey(tid))
            {
                _tether0039DrawnTime.Remove(tid);
            }
        }
                #endregion


    }
}



