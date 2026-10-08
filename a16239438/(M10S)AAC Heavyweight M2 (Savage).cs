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
using System.Collections.Concurrent;

using KodakkuAssist.Data;
using KodakkuAssist.Extensions;

namespace RyougiMioScriptNamespace
{
    [ScriptType(name: "(M10S)AAC Heavyweight M2 (Savage)", territorys: [1322, 1323], guid: "d244ff34-5a89-41e4-a937-7d5c7c6b1348", version: "0.1.0.1", author: "RyougiMio", note: "M10S，脚本同时在M10N/S中生效")]
    public class RyougiMio_1323
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
        [UserSetting("水蓝色")]
        public ScriptColor _waterColor { get; set; } = new ScriptColor() { V4 = new Vector4(0f, 1f, 1f, 0.5f) }; // 默认淡蓝


        [UserSetting("指路/引导颜色 (默认为青)")]
        public ScriptColor GuideColor { get; set; } = new ScriptColor() { V4 = new Vector4(0.0f, 1.0f, 1.0f, 0.01f) };
        #endregion

        #region Variables
        // M10S 火浪回切：存储点名目标与时间 {(ID, Time)}
        private List<(uint Id, DateTime Time)> _fireWaveMarkList = new();
        private ScriptAccessory _acc;
        private static int _waterMechanicParam = 0;
        // =========================================================
        // 辅助：坐标映射
        // =========================================================



        #endregion

        #region Methods
        // 封装：画死刑圆 (自动绑玩家)
        private void DrawCircle(ScriptAccessory accessory, string name, Vector3 pos, Vector4 color, int duration)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(6f);
            dp.Color = color;
            dp.DestoryAt = duration;

            dp.Position = pos; // 搜索基准点
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder; // 圆心=最近玩家
            dp.CentreOrderIndex = 1;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        // 封装：画扇形 (格子指向玩家)
        private void DrawFan(ScriptAccessory accessory, string name, Vector3 pos, Vector4 color, int duration, uint orderIndex)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Scale = new Vector2(60f);
            dp.Radian = float.Pi / 4;
            dp.Color = color;
            dp.DestoryAt = duration;

            dp.Position = pos;       // 扇形根部在格子
            dp.TargetPosition = pos; // 搜索基准点也在格子 (找离格子最近的人)

            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder; // 目标=最近玩家
            dp.TargetOrderIndex = orderIndex; // 第几个最近

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }


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
            _acc = accessory;
            _waterMechanicParam = 0;


            lock (_fireWaveMarkList)
            {
                _fireWaveMarkList.Clear();
            }

            accessory.Method.SendChat("/e M10S Initialized.");
        }

        #endregion
        #region TTSonly 
        [ScriptMethod(name: "斗志昂扬TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46466|46520|46467|46521)$"])]
        public void Elemental_AOE_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 46466(N), 46520(S) -> 火 AOE
            if (aid == 46466 || aid == 46520)
            {
                QTTS("火AOE");
                QText("火 AOE", 5000, true);
            }
            // 46467(N), 46521(S) -> 水 AOE
            else if (aid == 46467 || aid == 46521)
            {
                QTTS("水AOE");
                QText("水 AOE", 5000, true);
            }
        }
        [ScriptMethod(name: "浪花飞溅_浪涛翻涌TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46543|46544|46551|46552)$"])]
        public void SpreadStack_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            if (aid == 46543 || aid == 46551)
            {
                QTTS("分散");
                QText("分散", 5000, true);
            }
            else if (aid == 46544 || aid == 46552)
            {
                QTTS("分摊");
                QText("分摊", 5000, true);
            }
        }
        [ScriptMethod(name: "双重旋水_交错旋水TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46557|46560)$"])]
        public void SpreadStack_Alert1(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            if (aid == 46557 || aid == 46551)
            {
                QTTS("水波后躲避");
                QText("水波后躲避", 5000, true);
            }
            else if (aid == 46560 || aid == 46552)
            {
                QTTS("水波后不动");
                QText("水波后不动", 5000, true);
            }
        }
        [ScriptMethod(name: "浪尖转体TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47249|47250)$"])]
        public void DelayedMechanic_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            if (aid == 47249)
            {
                QTTS("稍后扇形");
                QText("稍后扇形", 5000, true);
            }
            else if (aid == 47250)
            {
                QTTS("稍后击退");
                QText("稍后击退", 5000, true);
            }
        }
        [ScriptMethod(name: "炽焰冲击_深海冲击TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46464|46518|46465|46519)$"])]
        public void TankBuster_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 46464(N), 46518(S) -> 分摊死刑
            if (aid == 46464 || aid == 46518)
            {
                QTTS("分摊死刑");
                QText("分摊死刑", 5000, true);
            }
            // 46465(N), 46519(S) -> 单体死刑
            else if (aid == 46465 || aid == 46519)
            {
                QTTS("死刑");
                QText("死刑", 5000, true);
            }
        }
        [ScriptMethod(name: "极限炫技TTS", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46499|46500)$"])]
        public void LimitCut_AOE_Alert(Event @event, ScriptAccessory accessory)
        {
            // 46499(N), 46500(S)
            QTTS("连续AOE");
            QText("连续AOE", 5000, true);
        }
        [ScriptMethod(name: "腾水踏浪_腾火踏浪TTS（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46532|46563)$"])]
        public void JumpAndTower_Alert(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 46532 -> 炽红4连跳
            if (aid == 46532)
            {
                QTTS("炽红4连跳");
                QText("炽红4连跳", 3000, true);
            }
            // 46563 -> 双人塔
            else if (aid == 46563)
            {
                QTTS("双人塔");
                QText("双人塔", 3000, true);
            }
        }

        [ScriptMethod(name: "深蓝分散分摊", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056"])]
        public void Status_2056_Add(Event @event, ScriptAccessory accessory)
        {
            // 1. 获取 StackCount
            if (!int.TryParse(@event["Param"], out var stack)) return;

            // 2. 仅处理机制预告 (1005-1008)
            if (stack == 1007 || stack == 1005 || stack == 1008 || stack == 1006)
            {
                // 记录到全局变量
                _waterMechanicParam = stack;

                // 3. 根据 StackCount 触发 TTS 和 文字提示
                // 1007: 全队分摊
                // 1005: 双组分摊
                // 1008: 两人分摊
                // 1006: 全体分散

                if (stack == 1007)
                {
                    QTTS("全队分摊");
                    QText("全队分摊", 5000, true);
                }
                else if (stack == 1005)
                {
                    QTTS("双组分摊");
                    QText("双组分摊", 5000, true);
                }
                else if (stack == 1008)
                {
                    QTTS("水组分散");
                    QText("水组分散", 5000, true);
                }
                else if (stack == 1006)
                {
                    QTTS("全体分散");
                    QText("全体分散", 5000, true);
                }
            }
        }
        #endregion

        #region 火浪回切
        [ScriptMethod(name: "M10S：火浪回切点名记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0298)$"], userControl: false)]
        public void FireWave_MarkRecord(Event @event, ScriptAccessory accessory)
        {
            var idStr = @event["TargetId"];
            if (string.IsNullOrEmpty(idStr)) return;
            if (!uint.TryParse(idStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var tid)) return;
            lock (_fireWaveMarkList)
            {
                _fireWaveMarkList.Add((tid, DateTime.Now));
            }
        }

        [ScriptMethod(name: "火浪回切", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46478|46537)$"])]
        public void FireWave_Draw(Event @event, ScriptAccessory accessory)
        {
            // 1. 解析 SourceId (Boss ID)
            var srcIdStr = @event["SourceId"];
            if (string.IsNullOrEmpty(srcIdStr)) return;
            if (!uint.TryParse(srcIdStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var sid)) return;
            // 2. 解析 ActionId
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            // 3. 解析 Duration
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
            // 获取 Boss 位置
            var boss = accessory.Data.Objects.SearchById(sid);
            if (boss == null) return;
            var spos = boss.Position;
            var now = DateTime.Now;
            float fanAngle = (aid == 46478) ? float.Pi / 3 : float.Pi * 2 * 330 / 360;
            lock (_fireWaveMarkList)
            {
                // 清理 10秒 以前的过期数据
                _fireWaveMarkList.RemoveAll(x => (now - x.Time).TotalSeconds > 10);
                foreach (var mark in _fireWaveMarkList)
                {
                    var tObj = accessory.Data.Objects.SearchById(mark.Id);
                    if (tObj == null) continue;
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"火浪回切-{mark.Id}-{aid}";
                    dp.Scale = new Vector2(60f);
                    dp.Radian = fanAngle;
                    dp.Owner = sid;
                    dp.TargetObject = mark.Id;
                    dp.Rotation = 0;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = dur;
                    dp.ScaleMode = ScaleMode.ByTime;

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }
            }
        }

        #endregion




        #region 瞎猜环节
        [ScriptMethod(name: "浪尖转体（猜测）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46547|46488|46550)$"])]
        public void FanAndKnockback_Draw(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Draw_{aid}";
            dp.Position = @event.SourcePosition;
            dp.Rotation = @event.SourceRotation;
            dp.DestoryAt = dur;
            dp.Color = accessory.Data.DefaultDangerColor;
            if (aid == 46547 || aid == 46488)
            {
                dp.Radian = float.Pi * 2 / 3;
                dp.Scale = new Vector2(60f);
                dp.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }
            // 击退 (箭头)
            else if (aid == 46550)
            {
                // 箭头 Scale: X=宽度, Y=长度
                dp.Scale = new Vector2(5f, 30f);
                dp.ScaleMode = ScaleMode.YByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Arrow, dp);
            }
        }
        [ScriptMethod(name: "炽焰冲击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46518|46464)$"])]
        public void Action_46518_ColorCheck(Event @event, ScriptAccessory accessory)
        {
            // 1. 获取持续时间
            if (!int.TryParse(@event["DurationMilliseconds"], out var duration)) return;

            // 2. 获取目标ID (StartCasting 的 TargetId 是十六进制字符串)
            var tidStr = @event["TargetId"];
            if (string.IsNullOrEmpty(tidStr) ||
                !uint.TryParse(tidStr, System.Globalization.NumberStyles.HexNumber, null, out var tid))
                return;

            // 3. 获取自己的索引，判断自己是否为坦克
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

            // 默认小队列表顺序：0,1 是坦克
            bool amITank = (myIndex == 0 || myIndex == 1);

            // 4. 颜色逻辑
            // 如果我是T -> 绿色(安全)；否则 -> 红色(危险)
            var drawColor = amITank ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;

            // 5. 绘图
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Action_46518_{tid}_{DateTime.Now.Ticks}";

            dp.Owner = tid;              // 绑在目标身上
            dp.Scale = new Vector2(6f);  // 半径 6m
            dp.Color = drawColor;        // 根据职能决定的颜色
            dp.DestoryAt = duration;     // 持续时间
            dp.ScaleMode = ScaleMode.ByTime; // 渐变填充

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "交错旋水/双重旋水_修正版", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(46560|46557)$"])]
        public void Boss_Tracking_Fan(Event @event, ScriptAccessory accessory)
        {
            if (!int.TryParse(@event["DurationMilliseconds"], out var duration)) return;
            float rad30 = float.Pi / 6;

            // 遍历小队列表
            var partyIds = accessory.Data.PartyList;
            foreach (var tid in partyIds)
            {
                // 1. 先获取基础对象 (IGameObject)
                var obj = accessory.Data.Objects.FirstOrDefault(x => x.EntityId == tid);

                // 2. 【核心修正】必须转换为 IBattleChara 才能访问 StatusList
                if (obj is IBattleChara player)
                {
                    // 3. 现在可以安全地检查 StatusList 了
                    bool hasBuff = player.StatusList.Any(s => s.StatusId == 4974);

                    // 如果有这个 Buff，跳过不画
                    if (hasBuff) continue;

                    // --- 绘图逻辑 ---
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"TrackFan_{@event["ActionId"]}_{tid}";
                    dp.Owner = @event.SourceId;
                    dp.TargetObject = tid;
                    dp.Radian = rad30;
                    dp.Scale = new Vector2(60f);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = duration;
                    dp.ScaleMode = ScaleMode.ByTime;

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }
            }
        }
        [ScriptMethod(name: "交错旋水/双重旋水2", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(46561|46558)$"])]
        public void Action_FollowUp_Fans(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["ActionId"], out var aid)) return;

            // 1. 定义常量
            int duration = 2600; // 2.6秒
            float rad30 = float.Pi / 6;             // 30度
            float rad24 = 22.5f * (float.Pi / 180f);  // 24度 (偏移量)
            float rad15 = 15f * (float.Pi / 180f);  // 15度 (扇形大小)

            // 2. 获取 Boss 当时的位置和朝向
            var pos = @event.SourcePosition;
            var rot = @event.SourceRotation;

            if (aid == 46558)
            {
                // --- 情况 A: 46558 (保持原方向 30度) ---
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Fan_46558_{DateTime.Now.Ticks}";

                dp.Position = pos;
                dp.Rotation = rot;            // 保持原方向
                dp.Radian = rad30;            // 30度扇形

                dp.Scale = new Vector2(60f);  // 半径 60m
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = duration;
                dp.ScaleMode = ScaleMode.ByTime; // 渐变

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }
            else if (aid == 46561)
            {
                // --- 情况 B: 46561 (左右偏转 24度，各画一个 15度扇形) ---

                // 左侧扇形 (+24度)
                var dpLeft = accessory.Data.GetDefaultDrawProperties();
                dpLeft.Name = $"Fan_46561_Left_{DateTime.Now.Ticks}";
                dpLeft.Position = pos;
                dpLeft.Rotation = rot + rad24;  // 向左偏 24度
                dpLeft.Radian = rad15;          // 15度扇形
                dpLeft.Scale = new Vector2(60f);
                dpLeft.Color = accessory.Data.DefaultDangerColor;
                dpLeft.DestoryAt = duration;
                dpLeft.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpLeft);

                // 右侧扇形 (-24度)
                var dpRight = accessory.Data.GetDefaultDrawProperties();
                dpRight.Name = $"Fan_46561_Right_{DateTime.Now.Ticks}";
                dpRight.Position = pos;
                dpRight.Rotation = rot - rad24; // 向右偏 24度
                dpRight.Radian = rad15;         // 15度扇形
                dpRight.Scale = new Vector2(60f);
                dpRight.Color = accessory.Data.DefaultDangerColor;
                dpRight.DestoryAt = duration;
                dpRight.ScaleMode = ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpRight);
            }
        }
        [ScriptMethod(name: "EnvControl15-21", eventType: EventTypeEnum.EnvControl, eventCondition: ["Index:regex:^\\d+$"])]
        public void EnvControl_Grid_Mechanic(Event @event, ScriptAccessory accessory)
        {
            if (!int.TryParse(@event["Index"], out var index)) return;
            if (index < 14 || index > 22) return;
            if (!int.TryParse(@event["Flag"], out var flag)) return;

            Vector3 gridPos = index switch
            {
                14 => new Vector3(87f, 0f, 87f),
                15 => new Vector3(100f, 0f, 87f),
                16 => new Vector3(113f, 0f, 87f),
                17 => new Vector3(87f, 0f, 100f),
                18 => new Vector3(100f, 0f, 100f),
                19 => new Vector3(113f, 0f, 100f),
                20 => new Vector3(87f, 0f, 113f),
                21 => new Vector3(100f, 0f, 113f),
                22 => new Vector3(113f, 0f, 113f),
                _ => Vector3.Zero
            };

            string baseName = $"Grid_{index}";

            // =========================================================
            // 【修正】只有 Flag 是 4 或 8 时，才执行清除逻辑
            // =========================================================
            if (flag == 4 || flag == 8)
            {
                accessory.Method.RemoveDraw($"{baseName}_TB");
                accessory.Method.RemoveDraw($"{baseName}_Stack");
                for (int i = 1; i <= 4; i++) accessory.Method.RemoveDraw($"{baseName}_Spread_{i}");
                return; // 清除完直接退出
            }

            // =========================================================
            // 下面是画图逻辑 (只有 Flag 为机制值时才执行)
            // =========================================================

            bool isWater = (flag == 2 || flag == 32 || flag == 128);
            // 直接用变量，不重复 GetUserSetting
            Vector4 drawColor = isWater ? _waterColor.V4 : accessory.Data.DefaultDangerColor;

            // 1. 死刑 (圆)
            if (flag == 128 || flag == 8192)
            {
                // 保险措施：虽然不是4/8，但为了防止同一格子机制切换产生重叠，画新图前先把旧的同名图顶掉
                // (Kodakku底层会自动替换同名绘图，所以这里不需要手动Remove同名的，直接Send即可)

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"{baseName}_TB";
                dp.Scale = new Vector2(6f);
                dp.Color = drawColor;
                dp.DestoryAt = 9999999;
                dp.ScaleMode = ScaleMode.None;

                dp.Position = gridPos;
                dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                dp.CentreOrderIndex = 1;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            // 2. 分摊 (扇形)
            else if (flag == 32 || flag == 2048)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"{baseName}_Stack";
                dp.Scale = new Vector2(60f);
                dp.Radian = float.Pi / 4;
                dp.Color = drawColor;
                dp.DestoryAt = 9999999;
                dp.ScaleMode = ScaleMode.None;

                dp.Position = gridPos;
                dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                dp.TargetOrderIndex = 1;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }
            // 3. 分散 (扇形 x 4)
            else if (flag == 2 || flag == 512)
            {
                for (uint i = 1; i <= 4; i++)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"{baseName}_Spread_{i}";
                    dp.Scale = new Vector2(60f);
                    dp.Radian = float.Pi / 4;
                    dp.Color = drawColor;
                    dp.DestoryAt = 9999999;
                    dp.ScaleMode = ScaleMode.None;

                    dp.Position = gridPos;
                    dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dp.TargetOrderIndex = i;

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }
            }
        }
        [ScriptMethod(name: "Env_2-5", eventType: EventTypeEnum.EnvControl, eventCondition: ["Index:regex:^[2-5]$"])]
        public void Env_Buff_Draw(Event @event, ScriptAccessory accessory)
        {
            // 1. 解析 Index (2, 3, 4, 5)
            if (!int.TryParse(@event["Index"], out var index)) return;
            if (index < 2 || index > 5) return;

            // 2. 解析 Flag
            if (!int.TryParse(@event["Flag"], out var flag)) return;

            // 定义常量
            uint buffId = 4975;
            int duration = 10000; // 10秒

            // =========================================================
            // 情况 A: Flag 128 -> 检测【自己】身上有没有 Buff -> 画安全圆
            // =========================================================
            if (flag == 128)
            {
                var myId = accessory.Data.Me;
                // 获取自己的对象
                var myObj = accessory.Data.Objects.FirstOrDefault(x => x.EntityId == myId);

                // 转类型查 Buff
                if (myObj is IBattleChara me)
                {
                    if (me.StatusList.Any(s => s.StatusId == buffId))
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"Safe_{index}_{myId}";
                        dp.Owner = myId;               // 绑在自己身上
                        dp.Scale = new Vector2(6f);    // 6m
                        dp.Color = accessory.Data.DefaultSafeColor; // 安全色
                        dp.DestoryAt = duration;
                        dp.ScaleMode = ScaleMode.ByTime; // 渐变

                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    }
                }
            }
            // =========================================================
            // 情况 B: Flag 2048 -> 检测【所有人】身上有没有 Buff -> 画危险圆
            // =========================================================
            else if (flag == 2048)
            {
                var partyIds = accessory.Data.PartyList;

                foreach (var tid in partyIds)
                {
                    var obj = accessory.Data.Objects.FirstOrDefault(x => x.EntityId == tid);

                    // 转类型查 Buff
                    if (obj is IBattleChara player)
                    {
                        if (player.StatusList.Any(s => s.StatusId == buffId))
                        {
                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"Danger_{index}_{tid}";
                            dp.Owner = tid;                // 绑在该玩家身上
                            dp.Scale = new Vector2(5f);    // 5m
                            dp.Color = accessory.Data.DefaultDangerColor; // 危险色
                            dp.DestoryAt = duration;
                            dp.ScaleMode = ScaleMode.ByTime; // 渐变

                            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                        }
                    }
                }
            }
        }
        [ScriptMethod(name: "混合爆炸", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46587"])]
        public void Action_46587_Circle(Event @event, ScriptAccessory accessory)
        {
            // 1. 获取技能持续时间
            if (!int.TryParse(@event["DurationMilliseconds"], out var duration)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            // 使用唯一名字防止冲突
            dp.Name = $"Circle_46587_{@event.SourceId}_{DateTime.Now.Ticks}";

            // 2. 位置：SourcePosition (施法者脚下)
            dp.Position = @event.SourcePosition;

            // 3. 大小：9m 圆
            dp.Scale = new Vector2(9f);

            // 4. 样式：危险色、渐变填充
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode = ScaleMode.ByTime; // 渐变
            dp.DestoryAt = duration;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "连线矩形_Rect版", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(027B|027C)$"])]
        public void Draw_Link_Rect(Event @event, ScriptAccessory accessory)
        {
           


            string iconId = @event["Id"];
            uint targetPlayerId = (uint)@event.TargetId; // 强转 uint

            // A. 确定要找哪个 NPC (DataId)
            uint searchDataId = 0;
            if (iconId == "027B") searchDataId = 19288;
            else if (iconId == "027C") searchDataId = 19287;

            if (searchDataId == 0) return;

            // B. 查找 NPC
            var sourceNpc = accessory.Data.Objects.FirstOrDefault(x => x.DataId == searchDataId);
            if (sourceNpc == null) return;

            // C. 确定颜色
            // 027B -> 水色， 027C -> 危险红
            Vector4 drawColor = (iconId == "027B") ? _waterColor.V4 : accessory.Data.DefaultDangerColor;

            // D. 绘图逻辑
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Link_Rect_{iconId}_{targetPlayerId}_{DateTime.Now.Ticks}";

            // 起点：NPC
            dp.Owner = sourceNpc.EntityId;

            // 终点：玩家 (矩形会自动旋转朝向玩家)
            dp.TargetObject = targetPlayerId;

            // 尺寸：X=宽度(8), Y=长度(初始给1就行，会被ScaleMode覆盖)
            dp.Scale = new Vector2(8f, 1f);

            dp.Color = drawColor;
            dp.DestoryAt = 4700; // 4.7s

            // 【核心关键】
            // 使用 Rect + YByDistance
            // 效果：画一个矩形，宽度固定为8，长度自动拉伸直到填满 NPC 到 玩家 的距离
            dp.ScaleMode = ScaleMode.YByDistance;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        #endregion


    }
}