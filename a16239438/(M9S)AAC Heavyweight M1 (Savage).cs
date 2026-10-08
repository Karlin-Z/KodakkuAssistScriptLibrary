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
    [ScriptType(name: "(M9S)AAC Heavyweight M1 (Savage)", territorys: [1320, 1321], guid: "ced5c285-484c-4750-bc85-241e927848f1", version: "1.0.0.0", author: "RyougiMio", note: "M9S，脚本同时在M9N/S中生效，注明TTS的机制仅有播报。")]
    public class RyougiMio_1321
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

        [UserSetting("指路/引导颜色 (默认为青)")]
        public ScriptColor GuideColor { get; set; } = new ScriptColor() { V4 = new Vector4(0.0f, 1.0f, 1.0f, 0.01f) };
        #endregion

        #region Variables
        private ScriptAccessory _acc;
        private static long _chainsawTime = 0;
        private static int _chainsawDelay = 0;
        private int _moonCount = 0;
        // 【修正】45969 扇形多组控制
        private static int _fan45969Count = 0;      
        private static long _fan45969LastTime = 0;
        // 用字典记录每一组(GroupIndex)的预计结束时间
        private static Dictionary<int, long> _fanGroupEndTimes = new Dictionary<int, long>();

        #endregion

        #region Methods

        // 静态字典：用来跨方法记录第一刀的时间
        private static Dictionary<uint, int> _moonPhaseDurations = new Dictionary<uint, int>();
        // 【新增】定义一个集合，用来记录当前场上所有有BUFF 4729 的玩家ID
        private HashSet<uint> _active4729Tids = new HashSet<uint>();
        // 自定义TTS方法：自动检查 EnableTTS 开关
        private void QTTS(string text, int rate = 0)
        {
            if (!EnableTTS) return;
            _acc.Method.TTS(text, rate);
        }
        // 自定义文字提示方法：自动检查 EnableText 开关
        private void QText(string text, int duration, bool isWarning = false)
        {
            if (!EnableText) return;
            _acc.Method.TextInfo(text, duration, isWarning);
        }



        #endregion

        #region Initialization 

        public void Init(ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw(".*");
            _moonPhaseDurations.Clear();
            _acc = accessory;
            _chainsawTime = 0;
            _chainsawDelay = 0;
            _moonCount = 0;
            // 【修正】重置
            _fan45969Count = 0;
            _fan45969LastTime = 0;
            _fanGroupEndTimes.Clear();
            accessory.Method.SendChat("/e M9S Initialized.");
        }

        #endregion
        #region TTSonly 

        [ScriptMethod(name: "魅亡之音TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45921|45956)$"])]
        public void AOE_Alert(Event @event, ScriptAccessory accessory)
        {
            QTTS("AOE");
            QText("AOE", 4700, true);
        }
        [ScriptMethod(name: "音速流散TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45980)$"])]
        public void AOE_Alert1(Event @event, ScriptAccessory accessory)
        {
            QTTS("分散");
        }
        [ScriptMethod(name: "音速集聚TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45981)$"])]
        public void AOE_Aler2t(Event @event, ScriptAccessory accessory)
        {
            QTTS("分摊");
        }

        [ScriptMethod(name: "硬核之声TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45915|45951|45916|45952)$"])]
        public void Hardcore_Voice(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 45916 和 45952 是强化版
            if (aid == 45916 || aid == 45952)
            {
                QTTS("强化双T死刑");
                QText("强化双T死刑", 5000, true);
            }
            else
            {
                // 45915 和 45951 是普通版
                QTTS("双T死刑");
                QText("双T死刑", 5000, true);
            }
        }
        [ScriptMethod(name: "剥蚀的低嗓_尖锐的音调TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45957|45958)$"])]
        public void Erosion_Voice_Pitch(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 45957 全体分散
            if (aid == 45957)
            {
                QTTS("分散");
                QText("全体分散", 5000, true);
            }
            // 45958 分组分摊
            else if (aid == 45958)
            {
                QTTS("分摊");
                QText("分组分摊", 5000, true);
            }
        }
        #endregion





        #region 左右刀

        [ScriptMethod(name: "月之半相_左右半场刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45906|45907|45910|45911|45943|45944|45947|45948)$"])]
        public void Half_Moon(Event @event, ScriptAccessory accessory)
        {
            // 1. 获取时间
            var totalDuration = int.Parse(@event["DurationMilliseconds"]);
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 2. ID 分组
            var leftSideIds = new HashSet<uint> { 45906, 45911, 45943, 45948 }; // 左组
            var secondHitIds = new HashSet<uint> { 45907, 45911, 45944, 45948 }; // 后手组
            var pairMap = new Dictionary<uint, uint>
            {
                { 45907, 45906 }, { 45911, 45910 },
                { 45944, 45943 }, { 45948, 45947 }
            };

            bool isLeft = leftSideIds.Contains(aid);
            bool isSecond = secondHitIds.Contains(aid);

            // 【关键修改】计数逻辑：只有是第一刀的时候，才算作新的一次机制，计数+1

                _moonCount++;
            

            // 3. 基础参数与位置判断
            Vector3 drawPos;

            // 【关键修改】如果是第3次或第4次，使用 SourcePosition；否则使用场地中心
            if (_moonCount == 3 || _moonCount == 4)
            {
                drawPos = @event.SourcePosition;
            }
            else
            {
                drawPos = new Vector3(100f, 0f, 100f);
            }

            var srcRot = @event.SourceRotation;
            float width = 40f;
            float length = 40f;

            // 4. 角度旋转逻辑
            // 左组：转 90 度 (+PI/2)
            // 右组：转 -90 度 (-PI/2)
            float rotOffset = isLeft ? (MathF.PI / 2) : -(MathF.PI / 2);
            float finalRot = srcRot + rotOffset;

            // 5. 时间控制逻辑
            int delay = 0;
            int destory = totalDuration;
            
            if (!isSecond)
            {
                _moonPhaseDurations[aid] = totalDuration;
            }
            else
            {
                // 如果是第二刀，尝试获取第一刀的时长
                if (pairMap.TryGetValue(aid, out var firstId))
                {
                    var firstDuration = _moonPhaseDurations.ContainsKey(firstId) ? _moonPhaseDurations[firstId] : 5000;
                    delay = firstDuration;
                    destory = totalDuration - delay;
                }
            }

            // 6. 绘制
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"月之半相_{aid}_{DateTime.Now.Ticks}"; // 加个时间戳防止重名

            dp.Position = drawPos;
            dp.Rotation = finalRot;
            dp.Scale = new Vector2(width, length);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay;
            dp.DestoryAt = destory;
            dp.ScaleMode = ScaleMode.YByTime;
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "强化月之半相_左右刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45908|45909|45912|45913|45945|45946|45949|45950)$"])]
        public void Enhanced_Half_Moon(Event @event, ScriptAccessory accessory)
        {
            // 1. 获取时间
            var totalDuration = int.Parse(@event["DurationMilliseconds"]);
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 左组：转 +90 度
            var leftSideIds = new HashSet<uint> { 45908, 45913, 45945, 45950 };
            // 后手组：需要延迟显示 (Wait for First)
            var secondHitIds = new HashSet<uint> { 45909, 45913, 45946, 45950 };
            // 配对表：Key = 后手ID, Value = 先手ID
            var pairMap = new Dictionary<uint, uint>
            {
                { 45909, 45908 },
                { 45913, 45912 },
                { 45946, 45945 },
                { 45950, 45949 }
            };

            // 3. 坐标处理 (EffectPosition 四舍五入取整, Y=0)
            // 这里使用 EffectPosition 并取整
            float px = MathF.Round(@event.EffectPosition.X);
            float pz = MathF.Round(@event.EffectPosition.Z);
            var drawPos = new Vector3(px, 0f, pz);
            // 基础参数
            var srcRot = @event.SourceRotation;
            bool isLeft = leftSideIds.Contains(aid);
            bool isSecond = secondHitIds.Contains(aid);
            float width = 40f;
            float length = 40f;
            // 4. 角度旋转逻辑
            // 左组：转 90 度 (+PI/2)
            // 右组：转 -90 度 (-PI/2)
            float rotOffset = isLeft ? (MathF.PI / 2) : -(MathF.PI / 2);
            float finalRot = srcRot + rotOffset;
            // 5. 时间控制逻辑
            int delay = 0;
            int destory = totalDuration;
            // 如果是先手刀，记录 Duration 供后手查询
            if (!isSecond)
            {
                _moonPhaseDurations[aid] = totalDuration;
            }
            // 如果是后手刀，查询先手刀的 Duration 设定延迟
            else
            {
                if (pairMap.TryGetValue(aid, out var firstId))
                {
                    // 尝试获取先手的时间，默认5000
                    var firstDuration = _moonPhaseDurations.ContainsKey(firstId) ? _moonPhaseDurations[firstId] : 5000;
                    delay = firstDuration;
                    destory = totalDuration - delay; // 保证同时结束
                }
            }
            // 6. 绘制
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"强化月之半相_{aid}";

            dp.Position = drawPos;
            dp.Rotation = finalRot;
            dp.Scale = new Vector2(width, length);
            dp.Color = accessory.Data.DefaultDangerColor;

            dp.Delay = delay;
            dp.DestoryAt = destory;

            dp.ScaleMode = ScaleMode.YByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        #endregion
        #region 蝙蝠机制
        [ScriptMethod(name: "蝙蝠", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45940"])]
        public void Bat_Auto_Rotate_Direction(Event @event, ScriptAccessory accessory)
        {
            var bats = accessory.Data.Objects.Where(obj => obj.DataId == 19503);
            var center = new Vector3(100f, 0, 100f);

            foreach (var bat in bats)
            {
                var batPos = bat.Position;
                var batRot = bat.Rotation;

                // --- 2. 基础向量计算 ---
                // 半径向量：从中心指向蝙蝠
                Vector3 vecRadius = batPos - center;
                double dist = vecRadius.Length();

                // 蝙蝠的朝向向量
                Vector3 vecBatFace = new Vector3((float)Math.Sin(batRot), 0, (float)Math.Cos(batRot));

                // --- 3. 核心：计算两个候选点 (顺时针 vs 逆时针) ---
                
                // 候选点 A (顺时针旋转 90度): (z, -x)
                Vector3 vecCW = new Vector3(vecRadius.Z, 0, -vecRadius.X);
                
                // 候选点 B (逆时针旋转 90度): (-z, x)
                Vector3 vecCCW = new Vector3(-vecRadius.Z, 0, vecRadius.X);

                // --- 4. 决策：选哪一个？ ---
                // 计算点乘：如果 (向量A 与 蝙蝠朝向) 的夹角更小，点乘结果就越大
                // 意思是：蝙蝠脸朝向哪边，我们就往哪边转
                
                Vector3 finalVec;
                if (Vector3.Dot(vecCW, vecBatFace) > Vector3.Dot(vecCCW, vecBatFace))
                {
                    finalVec = vecCW; // 蝙蝠是顺时针跑的，选顺时针点
                }
                else
                {
                    finalVec = vecCCW; // 蝙蝠是逆时针跑的，选逆时针点
                }

                // 计算最终圆心坐标 = 中心 + 旋转后的向量
                Vector3 drawPos = center + finalVec;


                // --- 5. 时序逻辑 (保持不变) ---
                int delay = 0;
                int duration = 0;

                if (dist > 5 && dist < 10)
                {
                    delay = 0; duration = 8200;
                }
                else if (dist >= 10 && dist < 15)
                {
                    delay = 8200; duration = 3500;
                }
                else if (dist >= 15 && dist < 30)
                {
                    delay = 11700; duration = 3500;
                }
                else continue;

                // --- 6. 绘图 ---
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Bat_SmartRot_{bat.EntityId}_{DateTime.Now.Ticks}";
                
                dp.Owner = 0; // 不绑定
                dp.Position = drawPos; // 修正后的位置
                
                dp.Scale = new Vector2(8f); 
                dp.Color = accessory.Data.DefaultDangerColor;
                
                dp.Delay = delay;
                dp.DestoryAt = duration;
                dp.ScaleMode = ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }
        // 1. 获得Buff：画圈 (持续时间设为无限，名字固定)
        [ScriptMethod(name: "蝙蝠BUFF", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:4729"])]
        public void Status_4729_Add(Event @event, ScriptAccessory accessory)
        {
            var tidStr = @event["TargetId"];
            if (!ulong.TryParse(tidStr, System.Globalization.NumberStyles.HexNumber, null, out var tid)) 
            {
                if (!ulong.TryParse(tidStr, out tid)) return;
            }

            var dp = accessory.Data.GetDefaultDrawProperties();
            
            dp.Name = $"Buff_4729_Ring_{tid}";
            
            dp.Owner = tid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 600000; 
            
            // 空心月环
            dp.Scale = new Vector2(8.0f);      
            dp.InnerScale = new Vector2(7.95f); 
            dp.Radian = float.Pi * 2; 

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }

        // 2. 移除Buff：删除圈
        [ScriptMethod(name: "蝙蝠BUFF2", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:4729"])]
        public void Status_4729_Remove(Event @event, ScriptAccessory accessory)
        {
            var tidStr = @event["TargetId"];
            if (!ulong.TryParse(tidStr, System.Globalization.NumberStyles.HexNumber, null, out var tid)) 
            {
                if (!ulong.TryParse(tidStr, out tid)) return;
            }

            // 拼出和 Add 时候一样的名字
            string drawName = $"Buff_4729_Ring_{tid}";
            
            // 手动删除
            accessory.Method.RemoveDraw(drawName);
        }

        #endregion
        #region 锤锯刃
        [ScriptMethod(name: "致命刑锯_前进", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45877|45927)$"])]
        public void Chainsaw_Forward(Event @event, ScriptAccessory accessory)
        {
            var duration = int.Parse(@event["DurationMilliseconds"]);
            var rawPos = @event.EffectPosition;
            // 四舍五入取整
            var drawPos = new Vector3(
                MathF.Round(rawPos.X),
                0,
                MathF.Round(rawPos.Z)
            );
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "致命刑锯_前进";
            dp.Position = drawPos;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(20f, 10f);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.YByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "致命刑锯_冲出", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45878|45879|45880|45928|45929|45930)$"])]
        public void Chainsaw_RushOut(Event @event, ScriptAccessory accessory)
        {
            var duration = int.Parse(@event["DurationMilliseconds"]);
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 1. 长度判断
            float length = 32f;
            if (aid == 45879 || aid == 45929) length = 22f;
            if (aid == 45880 || aid == 45930) length = 12f;
            // 2. 时间逻辑
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long diff = now - _chainsawTime;
            int finalDelay = 0;
            int finalDuration = duration;
            // 情况A: 初始化 或 间隔太久(>5s) -> 新的一轮机制
            if (_chainsawTime == 0 || Math.Abs(diff) > 5000)
            {
                finalDelay = 0;
                _chainsawDelay = 0;
                _chainsawTime = now;
            }
            // 情况B: 时差 < 1s -> 同一波次
            else if (diff < 1000)
            {
                finalDelay = _chainsawDelay;
                finalDuration = duration - _chainsawDelay;
            }
            // 情况C: 时差 > 1s -> 连续波次 (第二波、第三波...)
            else
            {
                finalDelay = duration - (int)diff;
                finalDuration = (int)diff;
                _chainsawDelay = finalDelay;
                _chainsawTime = now;
            }
            // 3. 绘制
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"致命刑锯_{aid}";
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(5f, length);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = finalDelay;
            dp.DestoryAt = finalDuration;
            dp.ScaleMode = ScaleMode.YByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "致命刑轮锯", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(19189|19190)$"])]
        public void Straight_Entity_Spawn(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["DataId"], out var id)) return;
            int duration = 120000;
            float width = 0f;
            float length = 0f;
            if (id == 19189)
            {
                width = 6f;
                length = 8f;
            }
            else if (id == 19190)
            {
                width = 12f;
                length = 5f;
            }
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"致命刑轮锯_{@event.SourceId}";
            dp.Owner = @event.SourceId;
            dp.Rotation = @event.SourceRotation;
            dp.Scale = new Vector2(width, length);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = duration;
            dp.ScaleMode = ScaleMode.None;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        }
        #endregion
        #region 以太流失
        [ScriptMethod(name: "以太流失_十字", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45897|45971)$"])]
        public void Fatal_Cross_AOE(Event @event, ScriptAccessory accessory)
        {
            var duration = int.Parse(@event["DurationMilliseconds"]);
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            float radius = 40f;
            float width = (aid == 45971) ? 10f : 6f;
            // 2. 循环 4 次，画 4 个方向
            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"以太流失_{aid}_{i}";
                dp.Position = @event.SourcePosition;
                dp.Rotation = @event.SourceRotation + (float)(Math.PI / 2 * i);
                dp.Scale = new Vector2(width, radius);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = duration;
                dp.ScaleMode = ScaleMode.YByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
        #endregion
        #region 瞎猜环节

 
        [ScriptMethod(name: "标记蝙蝠", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(19502)$"])]
        public void Mark_19502_Circle(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Mark_19502_{@event.SourceId}";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(0.5f);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 60000;
            dp.ScaleMode = ScaleMode.None;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        // 定义防抖变量
        private long _lastTriggerTime_45989_Single = 0;

        [ScriptMethod(name: "M11S：旋转扇形 (单向6连/随动)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45989"])]
        public void RotateFan_Single_Sequence(Event @event, ScriptAccessory accessory)
        {
            // --- 基础参数 ---
            Vector3 centerPos = @event.SourcePosition;
            
            // 【修正1】起始方向：使用读条者当前的面向 (SourceRotation)
            float startAngle = @event.SourceRotation;

            // 每一轮的旋转增量：22.5度 (Pi/8)
            // 假设顺时针 (如方向反了请把下面计算的 + 改为 -)
            float rotationStep = (float)(Math.PI / 8); 

            // 扇形大小
            float fanRadian = (float)(Math.PI / 6); // 30度
            float fanRadius = 60f;

            // 累积延迟
            int currentDelay = 0;

            // --- 循环：6轮 (第1轮~第6轮) ---
            // 只保留时间维度的循环，不再画空间上的8方(交给8个读条来源各自处理)
            for (int i = 0; i < 6; i++)
            {
                // 计算当前轮持续时间
                int duration = (i == 0) ? 2700 : 2400;

                // 计算当前轮的角度 (基础面向 + 轮次 * 22.5度)
                float finalRot = startAngle + (i * rotationStep);

                var dp = accessory.Data.GetDefaultDrawProperties();
                
                // 【关键命名】必须包含 SourceId，因为有8个怪同时读条，不能重名
                dp.Name = $"RotFan_Single_{@event.SourceId}_{i}";
                
                dp.Position = centerPos;
                dp.Rotation = finalRot;
                
                dp.Radian = fanRadian; // 30度
                dp.Scale = new Vector2(fanRadius); // 60米
                
                dp.Color = accessory.Data.DefaultDangerColor;
                
                // 时序控制
                dp.Delay = currentDelay;     // 这一轮什么时候开始
                dp.DestoryAt = duration;     // 这一轮存在多久
                dp.ScaleMode = ScaleMode.ByTime; // 渐变填充

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

                // 累加延迟，让下一轮在这一轮结束后开始
                currentDelay += duration;
            }
        }
        [ScriptMethod(name: "硬核之声", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45951"])]
        public void Circle_Target_6m(Event @event, ScriptAccessory accessory)
        {
            // --- 1. 获取时间 ---
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            // --- 2. 获取目标 (TargetId) ---
            var tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) || 
                !ulong.TryParse(tidStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid)) 
                return;

            // --- 3. 绘图 ---
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Circle_6m_{tid}_{DateTime.Now.Ticks}";
            
            dp.Owner = tid; // 跟随目标
            dp.Scale = new Vector2(6f); // 半径 6m
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = dur;
            dp.ScaleMode = ScaleMode.ByTime; // 渐变

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "强化硬核之声", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45952"])]
        public void Circle_15m(Event @event, ScriptAccessory accessory)
        {
            // --- 1. 获取时间 ---
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            // --- 2. 绘图 ---
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Circle_15m_{@event.SourceId}_{DateTime.Now.Ticks}";
            
            // 45952 通常是 Boss 自身释放的 AOE，所以用 SourcePosition
            // 如果它也是点名，改用 dp.Owner = TargetId 即可
            dp.Position = @event.SourcePosition;
            
            dp.Scale = new Vector2(15f); // 半径 15m
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = dur;
            dp.ScaleMode = ScaleMode.ByTime; // 渐变

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "以太流失_扇形", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45969"])]
        public void Fan_45_AOE(Event @event, ScriptAccessory accessory)
        {
            var duration = int.Parse(@event["DurationMilliseconds"]);
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // 1. 计数器重置逻辑 (防止灭团后计数不对)
            // 如果距离上次读条超过15秒，视为新的一轮
            if (now - _fan45969LastTime > 15000)
            {
                _fan45969Count = 0;
                _fanGroupEndTimes.Clear();
            }
            _fan45969LastTime = now;
            
            // 计数 +1
            _fan45969Count++;

            // 2. 组别计算
            // 1, 2 -> Group 0 (第一组)
            // 3, 4 -> Group 1 (第二组)
            // 5, 6 -> Group 2 (第三组) ...
            int groupIndex = (_fan45969Count - 1) / 2;

            // 3. 记录这一组的【绝对结束时间】
            long myEndTime = now + duration;
            if (!_fanGroupEndTimes.ContainsKey(groupIndex) || myEndTime > _fanGroupEndTimes[groupIndex])
            {
                _fanGroupEndTimes[groupIndex] = myEndTime;
            }

            // 4. 计算延迟
            int delay = 0;
            if (groupIndex > 0)
            {
                // 如果不是第一组，需要查上一组的结束时间
                if (_fanGroupEndTimes.TryGetValue(groupIndex - 1, out long prevEndTime))
                {
                    delay = (int)Math.Max(0, prevEndTime - now);
                }
            }

            // 5. 计算剩余显示的持续时间 (Duration - Delay)
            int finalDuration = duration - delay;
            if (finalDuration <= 0) finalDuration = 100;

            // 6. 绘图
            var dp = accessory.Data.GetDefaultDrawProperties();
            
            // 【关键修改】名字加上 Ticks，保证绝对不会因为重名而“只画一个”
            dp.Name = $"Fan45_{@event.SourceId}_{_fan45969Count}_{DateTime.Now.Ticks}";
            
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;
            
            // 45度扇形 (X=半径60, Y=角度PI/4)
            // 注意：你的框架好像是用 Scale.Y 来表示角度，Scale.X 表示半径
            // 请根据你之前给的正确范例：Radian=角度，Scale=半径
            dp.Radian = (float)Math.PI / 4;  // 45度
            dp.Scale = new Vector2(60f);     // 半径60

            dp.Color = accessory.Data.DefaultDangerColor;
            
            dp.Delay = delay;
            dp.DestoryAt = finalDuration;
            dp.ScaleMode = ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(name: "音速", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45980|45981)$"])]
        public void Dual_Spell_Mechanic(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            var duration = int.Parse(@event["DurationMilliseconds"]);
            
            // 1. 获取自己的ID
            uint myId = accessory.Data.Me;
            var partyIds = accessory.Data.PartyList;
            
            // 2. 预先检查“自己”是否有 4730 BUFF (这对分摊逻辑很重要)
            bool iHave4730 = false;
            var myObj = accessory.Data.Objects.FirstOrDefault(x => x.EntityId == myId);
            if (myObj is IBattleChara me)
            {
                iHave4730 = me.StatusList.Any(s => s.StatusId == 4730);
            }

            // 3. 确定自己的职能索引 (用于分散时的颜色判断)
            int myIndex = -1;
            for (int i = 0; i < partyIds.Count; i++)
            {
                if (partyIds[i] == myId)
                {
                    myIndex = i;
                    break;
                }
            }

            // 4. 定义弧度常量
            float rad100 = 100f * (float.Pi / 180f); 
            float rad45 = float.Pi / 4;          
            
            // 5. 遍历小队列表
            for (int i = 0; i < partyIds.Count; i++)
            {
                var tid = partyIds[i];
                var obj = accessory.Data.Objects.FirstOrDefault(x => x.EntityId == tid);
                
                if (obj is IBattleChara player)
                {
                    // --- 【规则1】全局过滤：如果目标玩家有 4730，直接不画 ---
                    bool targetHas4730 = player.StatusList.Any(s => s.StatusId == 4730);
                    if (targetHas4730) continue;

                    // 判断是否是“我”
                    bool isMe = (i == myIndex);

                    // --- 【规则2】分摊逻辑控制 ---
                    if (aid == 45981)
                    {
                        // 如果“不是我” 且 “我自己没有4730”，则跳过不画
                        // 意思是：只有当我有了4730，我才看得到别人的圈；否则我只看我自己的
                        if (!isMe && !iHave4730) continue;
                    }

                    // --- 角度逻辑 ---
                    float angle = 0f;
                    if (aid == 45980) 
                    {
                        // 45980 (分散): T(0,1) 100度, 其他 45度
                        bool isTank = (i == 0 || i == 1); 
                        angle = isTank ? rad100 : rad45;
                    }
                    else
                    {
                        // 45981 (分摊): 全员 100度
                        angle = rad100;
                    }

                    // --- 颜色逻辑 ---
                    var drawColor = accessory.Data.DefaultDangerColor; // 默认红

                    if (aid == 45980) // 分散
                    {
                        // 如果我是 D (索引>=4)，看其他的 D (索引>=4) 为绿色
                        if (myIndex >= 4)
                        {
                            if (i >= 4 && !isMe)
                            {
                                drawColor = accessory.Data.DefaultSafeColor;
                            }
                        }
                    }
                    else // 分摊
                    {
                        // 如果画出来的不是我 (能进来说明我有4730)，画绿色
                        if (!isMe)
                        {
                            drawColor = accessory.Data.DefaultSafeColor;
                        }
                    }

                    // --- 绘图 ---
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"DualSpell_{aid}_{player.EntityId}"; 
                    
                    // 【关键】原点 Boss，目标 玩家
                    dp.Owner = @event.SourceId; 
                    dp.TargetObject = player.EntityId; 
                    
                    dp.Radian = angle;           
                    dp.Scale = new Vector2(60f); 
                    
                    dp.Color = drawColor;
                    dp.DestoryAt = duration;
                    dp.ScaleMode = ScaleMode.ByTime;

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }
            }
        }
        [ScriptMethod(name: "Status2056绘图", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056"])]
        public void Status_2056_Add(Event @event, ScriptAccessory accessory)
        {
            // 1. 获取 StackCount (Buff层数)
            if (!int.TryParse(@event["StackCount"], out var stack)) return;
            
            // 2. 获取 TargetId (目标ID)
            var tidStr = @event["TargetId"];
            // StatusAdd 事件中的 TargetId 通常是 Hex 字符串，需要解析
            if (!uint.TryParse(tidStr, System.Globalization.NumberStyles.HexNumber, null, out var tid)) return;

            // 3. 定义画图名字 (格式：前缀_ID)，方便后续移除
            string drawName = $"Status_2056_{tid}";

            // 4. 判断逻辑
            if (stack == 38)
            {
                // --- 画 7m 危险圆形 ---
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = drawName;
                dp.Owner = tid;                 // 绑定在玩家身上
                dp.Scale = new Vector2(7f);     // 半径 7m
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 99999;           // 持续很久，直到手动移除
                dp.ScaleMode = ScaleMode.None;  // 大小不变
                
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            else if (stack == 39)
            {
                // --- 画 内径4 外径15 月环 ---
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = drawName;
                dp.Owner = tid;                 // 绑定在玩家身上
                
                dp.Scale = new Vector2(15f);    // 外径 15
                dp.InnerScale = new Vector2(4f);// 内径 4
                dp.Radian = float.Pi * 2;       // 360度整圆
                
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 99999;
                dp.ScaleMode = ScaleMode.None;
                
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
            }
            // 如果不是 38 或 39，什么都不做
        }

        [ScriptMethod(name: "Status2056移除", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:2056"])]
        public void Status_2056_Remove(Event @event, ScriptAccessory accessory)
        {
            // 解析 TargetId
            var tidStr = @event["TargetId"];
            if (!uint.TryParse(tidStr, System.Globalization.NumberStyles.HexNumber, null, out var tid)) return;

            // 使用和 Add 时完全一样的名字规则
            string drawName = $"Status_2056_{tid}";
            
            // 移除画图
            accessory.Method.RemoveDraw(drawName);
        }
        #endregion


    }
}