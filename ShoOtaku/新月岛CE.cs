using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using Newtonsoft.Json;

namespace KodakkuAssistXSZYYS
{
    [ScriptType(
        name: "新月岛CE",
        guid: "15725518-8F8E-413A-BEA8-E19CC861CF93",
        territorys: [1252],
        version: "0.2.2",
        author: "XSZYYS",
        note: "新月岛CE绘制已完成",
        updateInfo: "统一绘图命名与清理范围；修复火冰陷阱、风石光、加鲁拉冲锋及怒潮断头台的并发状态和冲锋失败恢复；陷阱使用可取消的异步延迟重绘并在卸载时释放；加强事件数值解析。"
    )]
    public class 新月岛CE : IDisposable
    {
        private const string DRAW_PREFIX = "CrescentIslandCE_";
        private readonly object _surgeLock = new();
        private readonly object _trapLock = new();
        private readonly object _garulaLock = new();
        private readonly object _tidalLock = new();
        private CancellationTokenSource _trapCts = new();
        private bool _disposed;
        private long _trapRevision;
        private ulong _bossId;
        private Vector3? _noiseComplaintArenaCenter;
        private bool _noiseComplaintCenterRecorded;
        private bool? _lightningIsCardinal; // true 为基本方向，false 为斜角方向
        private readonly Queue<ulong> _activeBirds = new();
        private int _activeMechanicId;
        private bool _isRampageSequenceRunning;
        private int _rampageChargeIndex;
        private Vector3 _rampageNextChargeStartPos;
        private static readonly Vector3 DeathclawArenaCenter = new(681f, 74f, 534f);
        private readonly List<ulong> _spheres = new(12);
        private readonly List<ulong> _spheresStone = new(6);
        private readonly List<ulong> _spheresWind = new(6);
        private readonly Dictionary<ulong, string> _surgeAoes = new();
        private bool _isHolyCasting;
        private readonly List<FireIceTrapInfo> _fireIceTraps = new();
        private readonly Dictionary<ulong, bool> _playerElements = new();

        private class FireIceTrapInfo
        {
            public ulong NpcId { get; init; }
            public Vector3 Position { get; set; }
            public bool IsFire { get; init; }
        }
        private static readonly Vector3 SharkArenaCenter = new(-117f, 1, -850f); // 鲨鱼战斗场地中心
        private readonly List<(string Name, int Delay)> _tidalGuillotineAoes = new(3); // 存储“怒潮断头台”系列AOE信息
        private static readonly Vector3 OnTheHuntAreaCenter = new Vector3(636f, 108f, -54f); // 跃立狮战斗场地中心
        private const float GARULA_ARENA_RADIUS = 23f;
        private const int GARULA_CHARGE_WIDTH = 8;
        private const int GARULA_CIRCLE_RADIUS = 30;
        private const int GARULA_FAN_RADIUS = 70;
        private const int ACTION_RUSHING_RUMBLE = 41175;
        private const int ACTION_BIRDSERK_RUSH = 41176;
        private const int ACTION_RAMPAGE = 41177;

        public void Init(ScriptAccessory accessory)
        {
            accessory.Log.Debug("新月岛CE脚本已加载。");
            lock (_garulaLock)
            {
                _noiseComplaintCenterRecorded = false;
                _noiseComplaintArenaCenter = null;
                ResetState();
            }
            lock (_tidalLock)
            {
                _tidalGuillotineAoes.Clear();
            }
            ResetWindStoneLightSurgeState();
            lock (_trapLock)
            {
                if (_disposed) return;
                _trapRevision++;
                _trapCts.Cancel();
                _trapCts.Dispose();
                _trapCts = new CancellationTokenSource();
                _fireIceTraps.Clear();
                _playerElements.Clear();
                accessory.Method.RemoveDraw($"^{DRAW_PREFIX}.*");
            }
        }

        public void Dispose()
        {
            lock (_trapLock)
            {
                if (_disposed) return;
                _disposed = true;
                _trapRevision++;
                _trapCts.Cancel();
                _trapCts.Dispose();
            }
        }

        [ScriptMethod(
            name: "陆行鸟攻击(黑色连队)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41163"]
        )]
        public void ChocoBeak(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "BlackRegiment_ChocoBeak_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(4, 70);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        [ScriptMethod(
            name: "尾羽 (黑色连队)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41164"]
        )]
        public void ChocoMaelfeather(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "BlackRegiment_ChocoMaelfeather_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(8);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "陆行鸟风暴 (黑色连队)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41147"]
        )]
        public void ChocoWindstorm(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "BlackRegiment_ChocoWindstorm_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(16);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "陆行鸟旋风 (黑色连队)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41148"]
        )]
        public void ChocoCyclone(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "BlackRegiment_ChocoCyclone_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(30, 30);
            dp.InnerScale = new Vector2(8, 8);
            dp.Radian = MathF.PI * 2;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }

        [ScriptMethod(
            name: "陆行鸟杀戮 (黑色连队)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41151"]
        )]
        public void ChocoSlaughterFirst(Event @event, ScriptAccessory accessory)
        {
            if (!TryDeserializeVector3(@event["SourcePosition"], out var spos)) return;
            if (!TryDeserializeFloat(@event["SourceRotation"], out var srot)) return;
            var sinRot = (float)Math.Sin(srot);
            var cosRot = (float)Math.Cos(srot);

            var positions = new List<Vector3>();
            var currentPos = spos;
            for (int i = 0; i < 5; i++)
            {
                currentPos = new Vector3(
                    currentPos.X + sinRot * 5,
                    currentPos.Y,
                    currentPos.Z + cosRot * 5
                );
                positions.Add(currentPos);
            }

            const int firstExplosionTime = 5000;
            const int subsequentInterval = 1100;
            const int warningDuration = 2000;
            const int lingerDuration = 500;

            for (int i = 0; i < positions.Count; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = DRAW_PREFIX + $"BlackRegiment_ChocoSlaughter_Danger_Zone_{i}";
                dp.Position = positions[i];
                dp.Scale = new Vector2(5);
                dp.Color = accessory.Data.DefaultDangerColor;

                int explosionTime = firstExplosionTime + ((i + 1) * subsequentInterval);

                dp.Delay = explosionTime - warningDuration;
                dp.DestoryAt = warningDuration + lingerDuration;
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }

        [ScriptMethod(
            name: "魔射线 (神秘土偶)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41137"]
        )]
        public void MysticHeat(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = DRAW_PREFIX + "FromTimesBygone_MysticHeat_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(40);
            dp.Radian = DegToRad(60);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(
            name: "大爆炸 (神秘土偶)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41130"]
        )]
        public void BigBurst(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = DRAW_PREFIX + "FromTimesBygone_BigBurst_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(26);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 8000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "死亡射线 (神秘土偶)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41133"]
        )]
        public void DeathRay(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = DRAW_PREFIX + "FromTimesBygone_DeathRay_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60);
            dp.Radian = DegToRad(90);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 8000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(
            name: "飞剑强袭 (神秘土偶)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41131"]
        )]
        public void Steelstrike(Event @event, ScriptAccessory accessory)
        {
            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = DRAW_PREFIX + $"FromTimesBygone_Steelstrike_Danger_Zone_{i}";
                dp.Owner = @event.SourceId;
                dp.Scale = new Vector2(10, 100);
                dp.Rotation = i * (MathF.PI / 4);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 8000;
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
            }
        }

        [ScriptMethod(
            name: "奥术之球 (神秘土偶)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41135"]
        )]
        public void ArcaneOrbTelegraph(Event @event, ScriptAccessory accessory)
        {
            if (!TryDeserializeVector3(@event["EffectPosition"], out var pos)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = DRAW_PREFIX + $"FromTimesBygone_ArcaneOrb_Danger_Zone_{pos.X}_{pos.Z}";
            dp.Position = pos;
            dp.Scale = new Vector2(6);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 4200;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "指令 - 圆形 (目标) (指令罐)",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:012F"]
        )]
        public void RockSlideStoneSwell_CircleTarget(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"指令 (Tether 012F) 触发: 绘制圆形AOE, 目标: {@event.TargetId}");
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + $"ExtremePrejudice_Circle_{@event.TargetId}";
            dp.Owner = @event.TargetId;
            dp.Scale = new Vector2(16);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6100;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "指令 - 十字 (目标) (指令罐)",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:0130"]
        )]
        public void RockSlideStoneSwell_Cross(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"指令 (Tether 0130) 触发: 绘制十字AOE, 目标: {@event.TargetId}");
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = DRAW_PREFIX + $"ExtremePrejudice_Cross1_{@event.TargetId}";
            dp1.Owner = @event.TargetId;
            dp1.Scale = new Vector2(10, 80);
            dp1.Color = accessory.Data.DefaultDangerColor;
            dp1.DestoryAt = 6100;
            dp1.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = DRAW_PREFIX + $"ExtremePrejudice_Cross2_{@event.TargetId}";
            dp2.Owner = @event.TargetId;
            dp2.Scale = new Vector2(10, 80);
            dp2.Rotation = MathF.PI / 2;
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.DestoryAt = 6100;
            dp2.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
        }

        [ScriptMethod(
            name: "指令 - 圆形 (来源) (指令罐)",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:0132"]
        )]
        public void RockSlideStoneSwell_CircleSource(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"指令 (Tether 0132) 触发: 绘制圆形AOE, 目标: {@event.SourceId}");
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + $"ExtremePrejudice_Circle_{@event.SourceId}";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(16);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6100;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "指令-绘图移除",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(41420|39470|41421|39471)$"],
            userControl: false
        )]
        public void RemoveRockSlideStoneSwell(Event @event, ScriptAccessory accessory)
        {
            uint actionId = @event.ActionId;
            if (actionId == 41421 || actionId == 39471)
            {
                accessory.Method.RemoveDraw($"^{DRAW_PREFIX}ExtremePrejudice_Cross1_{@event.SourceId}$");
                accessory.Method.RemoveDraw($"^{DRAW_PREFIX}ExtremePrejudice_Cross2_{@event.SourceId}$");
            }
            else // StoneSwell (圆形)
            {
                accessory.Method.RemoveDraw($"^{DRAW_PREFIX}ExtremePrejudice_Circle_{@event.SourceId}$");
            }
        }
        #region 进化加鲁拉

        [ScriptMethod(
            name: "场地中心记录 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41188"],
            userControl: false
        )]
        public void AgitatedGroanVisual(Event @event, ScriptAccessory accessory)
        {
            lock (_garulaLock)
            {
                if (!_noiseComplaintCenterRecorded)
                {
                    if (TryDeserializeVector3(@event["SourcePosition"], out var center))
                    {
                        _noiseComplaintArenaCenter = center;
                        _noiseComplaintCenterRecorded = true;
                        accessory.Log.Debug($"进化加鲁拉 Arena Center recorded at: {_noiseComplaintArenaCenter}");
                    }
                }
            }
        }

        [ScriptMethod(
            name: "圆状放雷 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41186"]
        )]
        public void EpicenterShock(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "NoiseComplaint_EpicenterShock_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(12);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "大落雷 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41187"]
        )]
        public void MammothBolt(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "NoiseComplaint_MammothBolt_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(25);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "扇状放雷 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41184"]
        )]
        public void LightningCrossing(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "NoiseComplaint_LightningCrossing_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(70);
            dp.Radian = DegToRad(45);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 4000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(
            name: "掀地 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(43262|41180)$"]
        )]
        public void Heave(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + $"NoiseComplaint_Heave_Danger_Zone_{@event.ActionId}";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60);
            dp.Radian = DegToRad(120);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = (@event.ActionId == 43262) ? 4000 : 2000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(
            name: "冲锋-方向记录 (进化加鲁拉)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2193"],
            userControl: false
        )]
        public void RushingRumbleRampage_Status(Event @event, ScriptAccessory accessory)
        {
            lock (_garulaLock)
            {
                var boss = accessory.Data.Objects.SearchById(@event.TargetId);
                if (boss == null || boss.DataId != 18078) return;
                // Param 为十进制字符串：848 (0x350) 为基本方向，849 (0x351) 为斜角方向。
                _lightningIsCardinal = @event["Param"] == "848";
                TryDrawMechanics(accessory);
            }
        }
        [ScriptMethod(
            name: "冲锋-小鸟记录 (进化加鲁拉)",
            eventType: EventTypeEnum.TargetIcon,
            eventCondition: ["Id:0242"],
            userControl: false
        )]
        public void RushingRumbleRampage_Icon(Event @event, ScriptAccessory accessory)
        {
            lock (_garulaLock)
            {
                if (_isRampageSequenceRunning)
                {
                    accessory.Log.Debug($"检测到连续冲锋的小鸟标记。冲锋索引: {_rampageChargeIndex}。");
                    var bird = accessory.Data.Objects.SearchById(@event.TargetId);
                    if (bird == null)
                    {
                        accessory.Log.Error($"未能找到用于连续冲锋 {_rampageChargeIndex} 的小鸟 {@event.TargetId}。");
                        ResetState();
                        return;
                    }
                    DrawRampageCharge(accessory, _rampageNextChargeStartPos, bird, _rampageChargeIndex);
                    if (_rampageChargeIndex >= 3)
                    {
                        accessory.Log.Debug("连续冲锋序列完成。");
                        ResetState();
                    }
                }
                else
                {
                    _activeBirds.Enqueue(@event.TargetId);
                    accessory.Log.Debug($"小鸟ID: {@event.TargetId} 已为非连续冲锋机制入队。队列数量: {_activeBirds.Count}");
                    TryDrawMechanics(accessory);
                }
            }
        }

        [ScriptMethod(
            name: "突进跺地 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41175"]
        )]
        public void OnRushingRumbleStart(Event @event, ScriptAccessory accessory)
        {
            lock (_garulaLock)
            {
                _activeMechanicId = ACTION_RUSHING_RUMBLE;
                _bossId = @event.SourceId;
                TryDrawMechanics(accessory);
            }
        }

        [ScriptMethod(
            name: "突进掀地 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41176"]
        )]
        public void OnBirdserkRushStart(Event @event, ScriptAccessory accessory)
        {
            lock (_garulaLock)
            {
                _activeMechanicId = ACTION_BIRDSERK_RUSH;
                _bossId = @event.SourceId;
                TryDrawMechanics(accessory);
            }
        }

        [ScriptMethod(
            name: "连续突进跺地 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41177"]
        )]
        public void OnRampageStart(Event @event, ScriptAccessory accessory)
        {
            lock (_garulaLock)
            {
                _activeMechanicId = ACTION_RAMPAGE;
                _bossId = @event.SourceId;
                TryDrawMechanics(accessory);
            }
        }

        // 以下加鲁拉私有 helper 均由持有 _garulaLock 的入口调用，绘制与状态消费、重置不可交错。
        private void TryDrawMechanics(ScriptAccessory accessory)
        {
            if (_isRampageSequenceRunning) return;
            if (_activeMechanicId == 0) return;

            switch (_activeMechanicId)
            {
                // RushingRumble (单次冲锋): 需要方向和小鸟
                case ACTION_RUSHING_RUMBLE when _lightningIsCardinal != null && _activeBirds.Count > 0:
                    accessory.Log.Debug("条件满足，绘制 RushingRumble。");
                    DrawRushingRumble(accessory);
                    ResetState();
                    break;

                // BirdserkRush (狂鸟冲锋): 只需要小鸟
                case ACTION_BIRDSERK_RUSH when _activeBirds.Count > 0:
                    accessory.Log.Debug("条件满足，绘制 BirdserkRush。");
                    DrawBirdserkRush(accessory);
                    ResetState();
                    break;

                // Rushing Rumble Rampage (连续冲锋) 的启动: 需要方向和小鸟
                case ACTION_RAMPAGE when _lightningIsCardinal != null && _activeBirds.Count > 0:
                    accessory.Log.Debug("条件满足，启动 Rushing Rumble Rampage 序列。");
                    var boss = accessory.Data.Objects.SearchById(_bossId);
                    if (boss == null)
                    {
                        accessory.Log.Error("无法启动连续冲锋，未找到施法者。");
                        ResetState();
                        return;
                    }
                    _isRampageSequenceRunning = true;
                    _rampageChargeIndex = 0;
                    _rampageNextChargeStartPos = boss.Position;
                    if (!_activeBirds.TryDequeue(out var birdId))
                    {
                        accessory.Log.Error("无法启动连续冲锋，小鸟队列为空。");
                        ResetState();
                        return;
                    }
                    var bird = accessory.Data.Objects.SearchById(birdId);
                    if (bird == null)
                    {
                        accessory.Log.Error($"无法启动连续冲锋，未找到小鸟 {birdId}。");
                        ResetState();
                        return;
                    }
                    DrawRampageCharge(accessory, _rampageNextChargeStartPos, bird, _rampageChargeIndex);
                    break;
            }
        }

        private void ResetState()
        {
            _lightningIsCardinal = null;
            _activeBirds.Clear();
            _bossId = 0;
            _activeMechanicId = 0;
            _isRampageSequenceRunning = false;
            _rampageChargeIndex = 0;
        }

        private void DrawGarulaFourFans(ScriptAccessory accessory, Vector3 position, Vector3 directionFrom, ulong uniqueId, int chargeIndex = -1)
        {
            var initialAngle = MathF.Atan2(directionFrom.X, directionFrom.Z);
            if (_lightningIsCardinal == false) // 斜角方向调整
            {
                initialAngle += DegToRad(45);
            }

            for (int i = 0; i < 4; i++)
            {
                var dpCone = accessory.Data.GetDefaultDrawProperties();
                string nameSuffix = chargeIndex >= 0 ? $"_{chargeIndex}_{i}" : $"_{i}";
                dpCone.Name = DRAW_PREFIX + $"NoiseComplaint_Cone_{uniqueId}{nameSuffix}";
                dpCone.Position = position;
                dpCone.Scale = new Vector2(GARULA_FAN_RADIUS);
                dpCone.Radian = DegToRad(45);
                dpCone.Rotation = initialAngle + (i * DegToRad(90));
                dpCone.Color = accessory.Data.DefaultDangerColor;
                dpCone.Delay = 0;
                dpCone.DestoryAt = 10400;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpCone);
            }
        }

        private void DrawGarulaChargeWithCircle(ScriptAccessory accessory, Vector3 startPos, Vector3 endPos, string namePrefix, ulong? ownerId = null, ulong? targetId = null)
        {
            var dpCharge = accessory.Data.GetDefaultDrawProperties();
            dpCharge.Name = DRAW_PREFIX + $"{namePrefix}_Charge";
            dpCharge.Scale = new Vector2(GARULA_CHARGE_WIDTH, 100);
            dpCharge.ScaleMode |= ScaleMode.YByDistance;
            dpCharge.Color = accessory.Data.DefaultDangerColor;
            dpCharge.DestoryAt = 6300;

            if (ownerId.HasValue)
            {
                dpCharge.Owner = ownerId.Value;
                if (targetId.HasValue)
                    dpCharge.TargetObject = targetId.Value;
                else
                    dpCharge.TargetPosition = endPos;
            }
            else
            {
                dpCharge.Position = startPos;
                dpCharge.TargetPosition = endPos;
            }
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCharge);
            var dpCircle = accessory.Data.GetDefaultDrawProperties();
            dpCircle.Name = DRAW_PREFIX + $"{namePrefix}_Circle";
            dpCircle.Position = endPos;
            dpCircle.Scale = new Vector2(GARULA_CIRCLE_RADIUS);
            dpCircle.Color = accessory.Data.DefaultDangerColor;
            dpCircle.DestoryAt = 10400;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
        }

        private Vector3 GetArenaEdgePosition(Vector3 destination)
        {
            if (_noiseComplaintArenaCenter == null) return destination;

            var vectorFromCenter = destination - _noiseComplaintArenaCenter.Value;
            if (vectorFromCenter.LengthSquared() <= GARULA_ARENA_RADIUS * GARULA_ARENA_RADIUS)
            {
                return destination;
            }
            else
            {
                var direction = Vector3.Normalize(vectorFromCenter);
                return _noiseComplaintArenaCenter.Value + direction * GARULA_ARENA_RADIUS;
            }
        }

        private void DrawRushingRumble(ScriptAccessory accessory)
        {
            if (!_activeBirds.TryDequeue(out var birdId)) return;
            var bird = accessory.Data.Objects.SearchById(birdId);
            var boss = accessory.Data.Objects.SearchById(_bossId);
            if (bird == null || boss == null) return;

            var destination = GetArenaEdgePosition(bird.Position);
            DrawGarulaChargeWithCircle(accessory, boss.Position, destination, $"NoiseComplaint_Rumble_{bird.EntityId}", _bossId);
            var dirToBoss = boss.Position - destination;
            DrawGarulaFourFans(accessory, destination, dirToBoss, bird.EntityId);
        }

        private void DrawBirdserkRush(ScriptAccessory accessory)
        {
            if (!_activeBirds.TryDequeue(out var birdId)) return;
            var bird = accessory.Data.Objects.SearchById(birdId);
            if (bird == null) return;
            var dpCharge = accessory.Data.GetDefaultDrawProperties();
            dpCharge.Name = DRAW_PREFIX + $"NoiseComplaint_BirdserkRush_Charge_{_bossId}";
            dpCharge.Owner = _bossId;
            dpCharge.TargetObject = bird.EntityId;
            dpCharge.Scale = new Vector2(GARULA_CHARGE_WIDTH, 100);
            dpCharge.ScaleMode |= ScaleMode.YByDistance;
            dpCharge.Color = accessory.Data.DefaultDangerColor;
            dpCharge.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCharge);
            var dpCone = accessory.Data.GetDefaultDrawProperties();
            dpCone.Name = DRAW_PREFIX + $"NoiseComplaint_BirdserkRush_Cone_{_bossId}";
            dpCone.Owner = _bossId;
            dpCone.TargetObject = bird.EntityId;
            dpCone.Scale = new Vector2(60);
            dpCone.Radian = DegToRad(120);
            dpCone.Color = accessory.Data.DefaultDangerColor;
            dpCone.DestoryAt = 11000;
            dpCone.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpCone);
        }

        private void DrawRampageCharge(ScriptAccessory accessory, Vector3 chargeStartPos, IGameObject bird, int chargeIndex)
        {
            if (_lightningIsCardinal == null)
            {
                accessory.Log.Error("无法绘制连续冲锋，雷电方向未知。");
                ResetState();
                return;
            }

            // 小鸟在场外时沿场地中心到小鸟的方向投影至场边，场内则使用小鸟位置。
            var destination = GetArenaEdgePosition(bird.Position);
            DrawGarulaChargeWithCircle(accessory, chargeStartPos, destination, $"NoiseComplaint_Rampage_{chargeIndex}");
            var chargeDirectionVector = destination - chargeStartPos;
            DrawGarulaFourFans(accessory, destination, chargeDirectionVector, bird.EntityId, chargeIndex);

            _rampageNextChargeStartPos = destination;
            _rampageChargeIndex++;
        }

        #endregion

        #region 死亡爪
        [ScriptMethod(
            name: "死亡甲 (死亡爪)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41315|41316|41317)$"]
        )]
        public void LethalNails(Event @event, ScriptAccessory accessory)
        {
            var nailId = @event.ActionId;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + $"死亡爪_LethalNails_{nailId}";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(7, 60);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode |= ScaleMode.ByTime;
            switch (nailId)
            {
                case 41315:
                    dp.DestoryAt = 2000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
                case 41316:
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
                case 41317:
                    dp.Delay = 2000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
            }
        }

        [ScriptMethod(
            name: "纵向双重抓 (死亡爪)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41323"]
        )]
        public void VerticalCrosshatch(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"垂直交错 (VerticalCrosshatch) 触发. ActionId: {@event.ActionId}, SourceId: {@event.SourceId}");
            const int frontBackDelay = 0;
            const int leftRightDelay = 2500;
            const int duration = 5000;

            float[] rotations = { 0, MathF.PI, MathF.PI / 2, -MathF.PI / 2 };
            int[] delays = { frontBackDelay, frontBackDelay, leftRightDelay, leftRightDelay };
            string[] names = { "Front", "Back", "Right", "Left" };

            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = DRAW_PREFIX + $"死亡爪_Crosshatch_{names[i]}_{@event.SourceId}";
                dp.Owner = @event.SourceId;
                dp.Scale = new Vector2(50);
                dp.Radian = DegToRad(90);
                dp.Rotation = rotations[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delays[i];
                dp.DestoryAt = duration;
                dp.ScaleMode |= ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                accessory.Log.Debug($"  绘制扇形: {dp.Name}, Rotation: {dp.Rotation}, Delay: {dp.Delay}ms");
            }
        }
        [ScriptMethod(
            name: "纵向双重抓长 (死亡爪)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41330"]
        )]
        public void VerticalCrosslonghatch(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"垂直交错 (VerticalCrosshatch) 触发. ActionId: {@event.ActionId}, SourceId: {@event.SourceId}");
            const int frontBackDelay = 0;
            const int leftRightDelay = 2500;
            const int duration = 7500;

            float[] rotations = { 0, MathF.PI, MathF.PI / 2, -MathF.PI / 2 };
            int[] delays = { frontBackDelay, frontBackDelay, leftRightDelay, leftRightDelay };
            string[] names = { "Front", "Back", "Right", "Left" };

            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = DRAW_PREFIX + $"死亡爪_Crosshatch_{names[i]}_{@event.SourceId}";
                dp.Owner = @event.SourceId;
                dp.Scale = new Vector2(50);
                dp.Radian = DegToRad(90);
                dp.Rotation = rotations[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delays[i];
                dp.DestoryAt = duration;
                dp.ScaleMode |= ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                accessory.Log.Debug($"  绘制扇形: {dp.Name}, Rotation: {dp.Rotation}, Delay: {dp.Delay}ms");
            }
        }

        [ScriptMethod(
            name: "横向双重抓 (死亡爪)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41324"]
        )]
        public void HorizontalCrosshatch(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"水平交错 (HorizontalCrosshatch) 触发. ActionId: {@event.ActionId}, SourceId: {@event.SourceId}");
            const int frontBackDelay = 2500;
            const int leftRightDelay = 0;
            const int duration = 5000;

            float[] rotations = { 0, MathF.PI, MathF.PI / 2, -MathF.PI / 2 };
            int[] delays = { frontBackDelay, frontBackDelay, leftRightDelay, leftRightDelay };
            string[] names = { "Front", "Back", "Right", "Left" };

            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = DRAW_PREFIX + $"死亡爪_Crosshatch_{names[i]}_{@event.SourceId}";
                dp.Owner = @event.SourceId;
                dp.Scale = new Vector2(50);
                dp.Radian = DegToRad(90);
                dp.Rotation = rotations[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delays[i];
                dp.DestoryAt = duration;
                dp.ScaleMode |= ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                accessory.Log.Debug($"  绘制扇形: {dp.Name}, Rotation: {dp.Rotation}, Delay: {dp.Delay}ms");
            }
        }
        [ScriptMethod(
            name: "横向双重抓长 (死亡爪)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41331"]
        )]
        public void HorizontalCrosslonghatch(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"水平交错 (HorizontalCrosshatch) 触发. ActionId: {@event.ActionId}, SourceId: {@event.SourceId}");
            const int frontBackDelay = 2500;
            const int leftRightDelay = 0;
            const int duration = 7500;

            float[] rotations = { 0, MathF.PI, MathF.PI / 2, -MathF.PI / 2 };
            int[] delays = { frontBackDelay, frontBackDelay, leftRightDelay, leftRightDelay };
            string[] names = { "Front", "Back", "Right", "Left" };

            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = DRAW_PREFIX + $"死亡爪_Crosshatch_{names[i]}_{@event.SourceId}";
                dp.Owner = @event.SourceId;
                dp.Scale = new Vector2(50);
                dp.Radian = DegToRad(90);
                dp.Rotation = rotations[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delays[i];
                dp.DestoryAt = duration;
                dp.ScaleMode |= ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                accessory.Log.Debug($"  绘制扇形: {dp.Name}, Rotation: {dp.Rotation}, Delay: {dp.Delay}ms");
            }
        }
        [ScriptMethod(
            name: "利爪(死亡爪)",
            eventType: EventTypeEnum.ObjectEffect,
            eventCondition: ["Id1:16", "Id2:32"]
        )]
        public void LethalClaw(Event @event, ScriptAccessory accessory)
        {
            var source = accessory.Data.Objects.SearchById(@event.SourceId);
            // 检查释放单位是否在场地21m范围内
            if (source == null || Vector3.Distance(source.Position, DeathclawArenaCenter) > 21f)
            {
                accessory.Log.Debug("释放单位不在死亡爪场地范围内，忽略利爪机制。");
                return;
            }
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + $"死亡爪_LethalClaw_{@event.SourceId}";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(50);
            dp.Radian = DegToRad(90);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 10000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            accessory.Log.Debug($"绘制死亡爪 利爪 AOE: {@event.SourceId}");
        }

        private void DrawCrossAOE(ScriptAccessory accessory, string baseName, Vector3 position, float rotation, int delay, int duration)
        {
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = DRAW_PREFIX + $"{baseName}_1";
            dp1.Position = position;
            dp1.Rotation = rotation;
            dp1.Scale = new Vector2(15, 200);
            dp1.Color = accessory.Data.DefaultDangerColor;
            dp1.Delay = delay;
            dp1.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = DRAW_PREFIX + $"{baseName}_2";
            dp2.Position = position;
            dp2.Rotation = rotation + MathF.PI / 2;
            dp2.Scale = new Vector2(15, 100);
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.Delay = delay;
            dp2.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
        }

        #endregion

        #region 水晶龙
        [ScriptMethod(
            name: "水晶之翼(钢铁月环)(水晶龙)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(42766|42767|42768|42769)$"]
        )]
        public void PrismaticWing(Event @event, ScriptAccessory accessory)
        {
            var actionId = @event.ActionId;
            switch (actionId)
            {
                case 42766:
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = DRAW_PREFIX + $"水晶龙_PrismaticWing_{actionId}";
                        dp.Owner = @event.SourceId;
                        dp.Scale = new Vector2(22, 22);
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.DestoryAt = 7000;
                        dp.ScaleMode |= ScaleMode.ByTime;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                        break;
                    }
                case 42768:
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = DRAW_PREFIX + $"水晶龙_PrismaticWing_{actionId}";
                        dp.Owner = @event.SourceId;
                        dp.Scale = new Vector2(22, 22);
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.DestoryAt = 4500;
                        dp.ScaleMode |= ScaleMode.ByTime;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                        break;
                    }
                case 42767:
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = DRAW_PREFIX + $"水晶龙_PrismaticWing_{actionId}";
                        dp.Owner = @event.SourceId;
                        dp.Scale = new Vector2(31, 31);
                        dp.InnerScale = new Vector2(5, 5);
                        dp.Radian = DegToRad(360);
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.DestoryAt = 7000;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                        break;
                    }
                case 42769:
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = DRAW_PREFIX + $"水晶龙_PrismaticWing_{actionId}";
                        dp.Owner = @event.SourceId;
                        dp.Scale = new Vector2(31, 31);
                        dp.InnerScale = new Vector2(5, 5);
                        dp.Radian = DegToRad(360);
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.DestoryAt = 4500;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                        break;
                    }
            }
        }
        [ScriptMethod(
            name: "水晶波动/乱流 (水晶龙)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(42728|42729|42730|42731|42732|42733|42734|42735|41758|41759|41760|41761)$"]
        )]
        public void CrystallizedEnergyAndChaos(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + $"CrawlingDeath_Crystallized_{@event.ActionId}";
            dp.Owner = @event.SourceId;
            dp.Color = accessory.Data.DefaultDangerColor;

            switch (@event.ActionId)
            {

                case 42728:
                    dp.Scale = new Vector2(7, 7);
                    dp.Delay = 3000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    break;
                case 42729:
                    dp.Scale = new Vector2(13, 13);
                    dp.InnerScale = new Vector2(7, 7);
                    dp.Radian = DegToRad(360);
                    dp.Delay = 3000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 42730:
                    dp.Scale = new Vector2(19, 19);
                    dp.InnerScale = new Vector2(13, 13);
                    dp.Radian = DegToRad(360);
                    dp.Delay = 3000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 42731:
                    dp.Scale = new Vector2(25, 25);
                    dp.InnerScale = new Vector2(19, 19);
                    dp.Radian = DegToRad(360);
                    dp.Delay = 3000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;

                case 42732:
                    dp.Scale = new Vector2(7, 7);
                    dp.Delay = 6000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    break;
                case 42733:
                    dp.Scale = new Vector2(13, 13);
                    dp.InnerScale = new Vector2(7, 7);
                    dp.Radian = DegToRad(360);
                    dp.Delay = 6000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 42734:
                    dp.Scale = new Vector2(19, 19);
                    dp.InnerScale = new Vector2(13, 13);
                    dp.Radian = DegToRad(360);
                    dp.Delay = 6000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 42735:
                    dp.Scale = new Vector2(25, 25);
                    dp.InnerScale = new Vector2(19, 19);
                    dp.Radian = DegToRad(360);
                    dp.Delay = 6000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;

                case 41758:
                    dp.Scale = new Vector2(7, 7);
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    break;
                case 41759:
                    dp.Scale = new Vector2(13, 13);
                    dp.InnerScale = new Vector2(7, 7);
                    dp.Radian = DegToRad(360);
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 41760:
                    dp.Scale = new Vector2(19, 19);
                    dp.InnerScale = new Vector2(13, 13);
                    dp.Radian = DegToRad(360);
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 41761:
                    dp.Scale = new Vector2(25, 25);
                    dp.InnerScale = new Vector2(19, 19);
                    dp.Radian = DegToRad(360);
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
            }
        }

        #endregion

        #region 新月狂战士
        [ScriptMethod(
            name: "横砍 (新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:42691"]
        )]
        public void ScathingSweep(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "新月狂战士_Sweep_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60, 60);
            dp.Color = new Vector4(1f, 0f, 0f, 1f);
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        [ScriptMethod(
            name: "震击怒涛1(新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:37323"]
        )]
        public void 新月狂战士Rage1(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "新月狂战士_Rage1_Danger_Zone";
            dp.Position = @event.TargetPosition;
            dp.Scale = new Vector2(8, 8);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 8000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = DRAW_PREFIX + "新月狂战士_BedrockUplift1_Danger_Zone";
            dp2.Position = @event.TargetPosition;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(8, 8);
            dp2.Color = new Vector4(1f, 0f, 0f, 1f);
            dp2.Radian = DegToRad(360);
            dp2.Delay = 6500;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "震击怒涛2(新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30872"]
        )]
        public void 新月狂战士Rage2(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "新月狂战士_Rage2_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(24, 24);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 9000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = DRAW_PREFIX + "新月狂战士_BedrockUplift2_Danger_Zone";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(24, 24);
            dp2.Radian = DegToRad(360);
            dp2.Color = new Vector4(1f, 0f, 0f, 1f);
            dp2.Delay = 7500;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "震击怒涛3(新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30873"]
        )]
        public void 新月狂战士Rage3(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "新月狂战士_Rage3_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(16, 16);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 9000;
            dp.DestoryAt = 6500;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = DRAW_PREFIX + "新月狂战士_BedrockUplift3_Danger_Zone";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(16, 16);
            dp2.Radian = DegToRad(360);
            dp2.Color = new Vector4(1f, 0f, 0f, 1f);
            dp2.Delay = 14000;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "震击怒涛4(新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30874"]
        )]
        public void 新月狂战士Rage4(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "新月狂战士_Rage4_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(8, 8);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 12000;
            dp.DestoryAt = 6500;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = DRAW_PREFIX + "新月狂战士_BedrockUplift4_Danger_Zone";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(16, 16);
            dp2.Color = new Vector4(1f, 0f, 0f, 1f);
            dp2.Radian = DegToRad(360);
            dp2.Delay = 21000;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "气焰(新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:37804"]
        )]
        public void 新月狂战士Fury(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "新月狂战士_Fury_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(13, 13);
            dp.Color = new Vector4(1f, 0f, 0f, 1f);
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        #endregion
        #region 回廊恶魔
        [ScriptMethod(
            name: "爆炸 (回廊恶魔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41357"]
        )]
        public void Explosion(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "回廊恶魔_Explosion_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(22, 22);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(
            name: "怒潮吐息(回廊恶魔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41360"]
        )]
        public void TidalBreath(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "回廊恶魔_TidalBreath_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(40);
            dp.Radian = DegToRad(180);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        #endregion
        #region 骑士团
        [ScriptMethod(
            name: "重拳崩 (骑士团)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41828"]
        )]
        public void DualfistFlurry(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null)
            {
                accessory.Log.Error($"无法找到双拳连击的施法者: {@event.SourceId}");
                return;
            }
            if (!TryDeserializeVector3(@event["EffectPosition"], out var currentPos)) return;
            var direction = new Vector3(MathF.Sin(caster.Rotation), 0, MathF.Cos(caster.Rotation));
            const int totalExplosions = 9;
            const float radius = 6f;
            const float stepDistance = 7f;      // 每次前进距离
            const int firstExplosionTime = 10000;// 第一次爆炸时间 (基于6秒咏唱)
            const int subsequentInterval = 1000;// 后续爆炸间隔
            const int warningDuration = 2000;   // 警告显示时间
            const int lingerDuration = 500;     // 爆炸后残留时间
            for (int i = 0; i < totalExplosions; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = DRAW_PREFIX + $"CompanyOfStone_DualfistFlurry_Danger_Zone_{i}";
                dp.Position = currentPos;
                dp.Scale = new Vector2(radius);
                dp.Color = accessory.Data.DefaultDangerColor;
                int explosionTime = firstExplosionTime + (i * subsequentInterval);
                dp.Delay = explosionTime - warningDuration;
                dp.DestoryAt = warningDuration + lingerDuration;
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                currentPos += direction * stepDistance;
            }
        }
        [ScriptMethod(
            name: "回旋炮 (骑士团)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41823|41822)$"]
        )]
        public void SpinningSiege(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null)
            {
                accessory.Log.Error($"旋转攻城：找不到施法者 {@event.SourceId}");
                return;
            }
            float initialRotation = caster.Rotation;

            // 41822 = 顺时针(CW), 41823 = 逆时针(CCW)
            int rotationDirection = (@event.ActionId == 41822) ? -1 : 1;

            const float crossLength = 120f;
            const float crossWidth = 6f;
            const int rotationInterval = 1700;  // 旋转间隔 1.7s
            const float rotationAngleDegrees = 9f; // 每次旋转 9 度
            float rotationAngleRad = DegToRad(rotationAngleDegrees);
            const int numberOfSteps = 6;
            for (int i = 0; i < numberOfSteps; i++)
            {
                int delay = 3000 + i * rotationInterval;
                int lifeSpan = 5000;
                if (lifeSpan <= 0) continue;
                float currentRotation = initialRotation + (i * rotationDirection * rotationAngleRad);
                var dp1 = accessory.Data.GetDefaultDrawProperties();
                dp1.Name = DRAW_PREFIX + $"SpinningSiege_{@event.ActionId}_Cross1_{i}";
                dp1.Position = @event.SourcePosition;
                dp1.Scale = new Vector2(crossWidth, crossLength);
                dp1.Rotation = currentRotation;
                dp1.Color = accessory.Data.DefaultDangerColor;
                dp1.Delay = delay;
                dp1.DestoryAt = lifeSpan;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);
                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = DRAW_PREFIX + $"SpinningSiege_{@event.ActionId}_Cross2_{i}";
                dp2.Position = @event.SourcePosition;
                dp2.Scale = new Vector2(crossWidth, crossLength);
                dp2.Rotation = currentRotation + (MathF.PI / 2);
                dp2.Color = accessory.Data.DefaultDangerColor;
                dp2.Delay = delay;
                dp2.DestoryAt = lifeSpan;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
            }
        }

        #endregion
        #region 鬼火苗
        [ScriptMethod(
            name: "分身机制(鬼火苗)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41397|42033|42035)$"]
        )]
        public void 分身机制Draw(Event @event, ScriptAccessory accessory)
        {
            var actionId = @event.ActionId;
            var dp = accessory.Data.GetDefaultDrawProperties();
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + $"鬼火苗_分身机制_{actionId}";
            dp2.Name = DRAW_PREFIX + $"鬼火苗_分身机制_{actionId}_2";
            switch (actionId)
            {
                case 41397:
                    dp.Scale = new Vector2(1.5f, 20f);
                    dp.Color = new(0.3f, 1.0f, 0f, 1.5f);
                    dp.Owner = accessory.Data.Me;
                    dp.TargetPosition = @event.SourcePosition;
                    dp.Rotation = MathF.PI;
                    dp.DestoryAt = 3000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
                    accessory.Log.Debug($"绘制击退: Name={dp.Name}s");
                    break;
                case 42033:
                    dp.Owner = @event.SourceId;
                    dp.Scale = new Vector2(50);
                    dp.InnerScale = new Vector2(7);
                    dp.Radian = DegToRad(360);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 3000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    accessory.Log.Debug($"绘制月环AOE: Name={dp.Name}s");
                    break;
                case 42035:
                    dp.Owner = @event.SourceId;
                    dp.Scale = new Vector2(15, 200);
                    dp.Rotation = DegToRad(90);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 3000;
                    dp2.Owner = @event.SourceId;
                    dp2.Scale = new Vector2(15, 200);
                    dp2.Color = accessory.Data.DefaultDangerColor;
                    dp2.DestoryAt = 3000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
                    accessory.Log.Debug($"绘制十字AOE: Name={dp.Name}s");
                    accessory.Log.Debug($"绘制十字AOE: Name={dp2.Name}s");
                    break;

            }
        }
        [ScriptMethod(
            name: "Boss直接AOE(鬼火苗)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(42034|42032)$"]
        )]
        public void OnBossAOE(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            if (@event.ActionId == 42034)
            {
                var baseName = "鬼火苗_Boss_Cross";
                DrawCrossAOE(accessory, baseName, caster.Position, caster.Rotation, 0, 5800);
                accessory.Log.Debug($"绘制Boss直接十字AOE: {baseName}");
            }
            else // ShadesNestBoss
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = DRAW_PREFIX + "鬼火苗_Boss_Donut";
                dp.Owner = @event.SourceId;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Radian = DegToRad(360);
                dp.DestoryAt = 5800;
                dp.Scale = new Vector2(50);
                dp.InnerScale = new Vector2(7);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                accessory.Log.Debug($"绘制Boss直接月环AOE: {dp.Name}");
            }
        }

        #endregion
        #region 尼姆瓣齿鲨
        [ScriptMethod(
            name: "深水切割者 (尼姆瓣齿鲨)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:43149"]
        )]
        public void HydrocleaveDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "SharkAttack_Hydrocleave_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(50);
            dp.Radian = DegToRad(60);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(
            name: "怒潮断头台(尼姆瓣齿鲨)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41723"]
        )]
        public void OnTidalGuillotineFirstCast(Event @event, ScriptAccessory accessory)
        {
            lock (_tidalLock)
            {
                _tidalGuillotineAoes.Clear();

                if (!TryDeserializeVector3(@event["EffectPosition"], out var pos)) return;
                var castTime = 8000;
                var name = DRAW_PREFIX + $"TidalGuillotine_AOE_0";
                _tidalGuillotineAoes.Add((name, castTime));
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = name;
                dp.Position = pos;
                dp.Scale = new Vector2(20);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = castTime + 1000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                accessory.Log.Debug($"记录了第一个潮汐断头台AOE，位置: {pos}");
            }
        }
        [ScriptMethod(
            name: "怒潮断头台 - 记录后续AOE(尼姆瓣齿鲨)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:41682"]
        )]
        public void OnTidalGuillotineTeleport(Event @event, ScriptAccessory accessory)
        {
            lock (_tidalLock)
            {
                if (_tidalGuillotineAoes.Count >= 3) return;

                if (!TryDeserializeVector3(@event["EffectPosition"], out var pos)) return;
                var index = _tidalGuillotineAoes.Count;
                var name = DRAW_PREFIX + $"TidalGuillotine_AOE_{index}";
                var delay = index == 1 ? 8700 : 9900;
                _tidalGuillotineAoes.Add((name, delay));
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = name;
                dp.Position = pos;
                dp.Scale = new Vector2(20);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delay - 5000;
                dp.DestoryAt = 5000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                accessory.Log.Debug($"记录了第 {index + 1} 个潮汐断头台AOE，位置: {pos}");
            }
        }
        [ScriptMethod(
            name: "开放水域-地火(尼姆瓣齿鲨)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41687"]
        )]
        public void OnOpenWaterRefactored(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            // 内外圈以15码为界，分别使用下方的次数、间隔和转角。
            var dirToCaster = caster.Position - SharkArenaCenter;
            var isInnerMode = dirToCaster.LengthSquared() < 15f * 15f;

            int maxCasts;
            int intervalMs;
            float rotationIncrementRad;
            float aoeRadius;

            if (isInnerMode)
            {
                maxCasts = 35;
                intervalMs = 1200;
                rotationIncrementRad = DegToRad(22.5f);
                aoeRadius = 4f;
            }
            else
            {
                maxCasts = 59;
                intervalMs = 690;
                rotationIncrementRad = DegToRad(12f);
                aoeRadius = 4f;
            }

            // 径向向量与施法者朝向的叉积符号决定沿圆周推进的方向。
            var forwardVector = new Vector3(MathF.Sin(caster.Rotation), 0, MathF.Cos(caster.Rotation));
            var rotationDirection = (Vector3.Cross(dirToCaster, forwardVector).Y < 0) ? 1.0f : -1.0f;
            var signedRotationIncrement = rotationDirection * rotationIncrementRad;

            var aoePositions = new List<Vector3>();
            for (int i = 0; i < maxCasts; i++)
            {
                var rotationAngle = signedRotationIncrement * i;
                var cosAngle = MathF.Cos(rotationAngle);
                var sinAngle = MathF.Sin(rotationAngle);
                var rotatedDir = new Vector3(
                    dirToCaster.X * cosAngle - dirToCaster.Z * sinAngle,
                    dirToCaster.Y,
                    dirToCaster.X * sinAngle + dirToCaster.Z * cosAngle
                );
                aoePositions.Add(SharkArenaCenter + rotatedDir);
            }

            for (int i = 0; i < aoePositions.Count; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = DRAW_PREFIX + $"OpenWater_Refactored_AOE_{i}";
                dp.Position = aoePositions[i];
                dp.Scale = new Vector2(aoeRadius);

                dp.Color = (i == 0)
                    ? new Vector4(1, 0, 0, 0.8f)
                    : accessory.Data.DefaultDangerColor;

                // 首次爆炸在5秒后，后续按内外圈间隔推进，各圈提前5秒显示。
                int explosionTime = 5000 + (i * intervalMs);
                int appearanceTime = explosionTime - 5000;

                dp.Delay = (appearanceTime > 0) ? appearanceTime : 0;
                dp.DestoryAt = 5000;
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }

            accessory.Log.Debug($"旋转月环(重构)已启动。模式: {(isInnerMode ? "内圈" : "外圈")}, 数量: {maxCasts}, 方向: {(rotationDirection > 0 ? "顺时针" : "逆时针")}");
        }

        #endregion
        #region 城塞守卫
        private const uint AID_AncientAeroIII_5s = 41287;  // 古代疾风III (5s咏唱)
        private const uint AID_AncientAeroIII_12s = 41292; // 古代疾风III (12s咏唱)
        private const uint AID_AncientStoneIII_5s = 41289; // 古代垒石III (5s咏唱)
        private const uint AID_AncientStoneIII_12s = 41293; // 古代垒石III (12s咏唱)

        [ScriptMethod(
            name: "圣焰(城塞守卫)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41297"]
        )]
        public void 圣焰Draw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "城塞守卫_圣焰_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(5, 60);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 4000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(
            name: "风石光 - 记录能量球(城塞守卫)",
            eventType: EventTypeEnum.AddCombatant,
            eventCondition: ["DataId:18125"],
            userControl: false
        )]
        public void OnSphereCreated(Event @event, ScriptAccessory accessory)
        {
            var sphere = accessory.Data.Objects.SearchById(@event.SourceId);
            if (sphere != null)
            {
                lock (_surgeLock)
                {
                    if (_spheres.Contains(sphere.EntityId) || _spheresWind.Contains(sphere.EntityId)
                        || _spheresStone.Contains(sphere.EntityId) || _surgeAoes.ContainsKey(sphere.EntityId))
                        return;
                    _spheres.Add(sphere.EntityId);
                }
                accessory.Log.Debug($"记录一个新的能量球: {sphere.Name}, ID: {sphere.EntityId}");
            }
        }
        [ScriptMethod(
            name: "风石光 - 能量球属性分类(城塞守卫)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2536"],
            userControl: false
        )]
        public void OnSphereStatusGain(Event @event, ScriptAccessory accessory)
        {
            var sphere = accessory.Data.Objects.SearchById(@event.TargetId);
            if (sphere == null) return;

            // Param 为十进制字符串：548 (0x224) 为风，549 (0x225) 为石。
            var statusParam = @event["Param"];

            lock (_surgeLock)
            {
                if (!_spheres.Contains(sphere.EntityId)) return;

                if (statusParam == "548")
                {
                    _spheresWind.Add(sphere.EntityId);
                    _spheres.Remove(sphere.EntityId);
                    accessory.Log.Debug($"能量球 {sphere.EntityId} 被分类为 [风]");
                }
                else if (statusParam == "549")
                {
                    _spheresStone.Add(sphere.EntityId);
                    _spheres.Remove(sphere.EntityId);
                    accessory.Log.Debug($"能量球 {sphere.EntityId} 被分类为 [石]");
                }
            }
        }

        [ScriptMethod(
            name: "风石光 - 预测并绘制AOE(城塞守卫)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41287|41292|41289|41293)$"]
        )]
        public void OnAncientSpellCast(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            List<ulong> originalSphereList;
            int castTimeMs;

            switch (@event.ActionId)
            {
                case AID_AncientAeroIII_5s:
                    originalSphereList = _spheresWind;
                    castTimeMs = 5000;
                    break;
                case AID_AncientAeroIII_12s:
                    originalSphereList = _spheresWind;
                    castTimeMs = 12000;
                    break;
                case AID_AncientStoneIII_5s:
                    originalSphereList = _spheresStone;
                    castTimeMs = 5000;
                    break;
                case AID_AncientStoneIII_12s:
                    originalSphereList = _spheresStone;
                    castTimeMs = 12000;
                    break;
                default:
                    return;
            }

            accessory.Log.Debug($"检测到BOSS咏唱 {@event.ActionId} (咏唱时间: {castTimeMs}ms), 开始检测范围内的能量球。");

            List<ulong> sphereIdsToCheck;
            lock (_surgeLock)
            {
                if (originalSphereList.Count == 0) return;
                sphereIdsToCheck = new List<ulong>(originalSphereList);
            }

            // 定义一个前方的锥形判定区 (40码长, 60度角)
            var coneAngle = DegToRad(60);
            var coneLength = 40f;
            foreach (var sphereId in sphereIdsToCheck)
            {
                var sphere = accessory.Data.Objects.SearchById(sphereId);
                if (sphere == null) continue;
                var vectorToSphere = sphere.Position - caster.Position;
                var distance = vectorToSphere.Length();

                if (distance > 0 && distance < coneLength)
                {
                    var casterForward = new Vector3(MathF.Sin(caster.Rotation), 0, MathF.Cos(caster.Rotation));
                    var dotProduct = Vector3.Dot(Vector3.Normalize(vectorToSphere), Vector3.Normalize(casterForward));
                    var angleToSphere = MathF.Acos(Math.Clamp(dotProduct, -1.0f, 1.0f));

                    if (Math.Abs(angleToSphere) < coneAngle / 2)
                    {
                        accessory.Log.Debug($"能量球 {sphereId} 在攻击范围内，准备绘制AOE。");

                        var dp = accessory.Data.GetDefaultDrawProperties();
                        var drawName = DRAW_PREFIX + $"Surge_AOE_{sphereId}";

                        dp.Name = drawName;
                        dp.Position = sphere.Position;
                        dp.Scale = new Vector2(15);
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.Delay = castTimeMs - 5000;
                        dp.DestoryAt = 7500;
                        dp.ScaleMode |= ScaleMode.ByTime;

                        lock (_surgeLock)
                        {
                            if (!originalSphereList.Remove(sphereId)) continue;
                            _surgeAoes[sphereId] = drawName;
                            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                        }
                    }
                }
            }
        }
        [ScriptMethod(
            name: "风石光 - 标记神圣咏唱开始(城塞守卫)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41284"],
            userControl: false
        )]
        public void OnAncientHolyCast(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"检测到BOSS开始咏唱神圣 ({@event.ActionId})，延迟光球判定。");
            lock (_surgeLock)
            {
                _isHolyCasting = true;
            }
        }
        [ScriptMethod(
            name: "风石光 - 神圣咏唱完成并引爆光球(城塞守卫)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:41284"]
        )]
        public void OnAncientHolyFinish(Event @event, ScriptAccessory accessory)
        {
            lock (_surgeLock)
            {
                if (!_isHolyCasting) return;
                _isHolyCasting = false;
                var lightSphereIds = new List<ulong>(_spheres);
                _spheres.Clear();

                accessory.Log.Debug("神圣咏唱完成，开始处理剩余的光球。");

                // 爆炸发生在咏唱完成后约2.4秒。
                const int explosionDelay = 2400;
                foreach (var sphereId in lightSphereIds)
                {
                    var sphere = accessory.Data.Objects.SearchById(sphereId);
                    if (sphere == null) continue;

                    accessory.Log.Debug($"光球 {sphereId} 将在 {explosionDelay}ms 后爆炸。");

                    var dp = accessory.Data.GetDefaultDrawProperties();
                    var drawName = DRAW_PREFIX + $"Surge_AOE_Light_{sphereId}";

                    dp.Name = drawName;
                    dp.Position = sphere.Position;
                    dp.Scale = new Vector2(15);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = explosionDelay;
                    dp.ScaleMode |= ScaleMode.ByTime;

                    _surgeAoes[sphereId] = drawName;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
            }
        }

        [ScriptMethod(
            name: "风石光 - 清理已爆炸的AOE(城塞守卫)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(41296|41295|41294)$"],
            userControl: false
        )]
        public void OnSurgeExplosion(Event @event, ScriptAccessory accessory)
        {
            var explodingSphereId = @event.SourceId;
            lock (_surgeLock)
            {
                if (_surgeAoes.Remove(explodingSphereId, out var drawName))
                {
                    accessory.Method.RemoveDraw($"^{drawName}$");
                    accessory.Log.Debug($"清理AOE绘制: {drawName}");
                }
            }
        }
        public void ResetWindStoneLightSurgeState()
        {
            lock (_surgeLock)
            {
                _isHolyCasting = false;
                _surgeAoes.Clear();
                _spheres.Clear();
                _spheresStone.Clear();
                _spheresWind.Clear();
            }
        }
        #endregion
        #region 夺心魔
        [ScriptMethod(
            name: "昏暗(夺心魔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41170"]
        )]
        public void OnDarkIIDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "夺心魔_昏暗_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(65);
            dp.Radian = DegToRad(90);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(
            name: "触手(夺心魔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41257|41314|41256)$"]
        )]
        public void 触手Draw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "夺心魔_触手_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode |= ScaleMode.ByTime;
            switch (@event.ActionId)
            {
                case 41257:
                    dp.Scale = new Vector2(20, 60);
                    dp.DestoryAt = 11000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
                case 41314:
                    dp.Scale = new Vector2(10, 60);
                    dp.DestoryAt = 7000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
                case 41256:
                    dp.Scale = new Vector2(10, 60);
                    dp.DestoryAt = 11000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
            }
        }

        private const uint SID_PlayingWithFire = 4211;
        private const uint SID_PlayingWithIce = 4212;
        private const uint OID_JestingJackanapes = 18102;


        [ScriptMethod(
            name: "火冰陷阱 - 记录玩家元素(夺心魔)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:regex:^(4211|4212)$"],
            userControl: false
        )]
        public void OnPlayerElementGain(Event @event, ScriptAccessory accessory)
        {
            var isFire = @event.StatusId == SID_PlayingWithFire;
            lock (_trapLock)
            {
                _playerElements[@event.TargetId] = isFire;
                if (@event.TargetId == accessory.Data.Me)
                {
                    accessory.Log.Debug($"[状态记录] 成功记录你的元素状态为: {(isFire ? "火" : "冰")}");
                }
            }

            if (@event.TargetId == accessory.Data.Me)
            {
                DrawFireIceTraps(accessory);
            }
        }

        [ScriptMethod(
            name: "火冰陷阱 - 移除玩家元素(夺心魔)",
            eventType: EventTypeEnum.StatusRemove,
            eventCondition: ["StatusID:regex:^(4211|4212)$"],
            userControl: false
        )]
        public void OnPlayerElementLose(Event @event, ScriptAccessory accessory)
        {
            bool needsRedraw = false;
            lock (_trapLock)
            {
                if (_playerElements.Remove(@event.TargetId))
                {
                    needsRedraw = @event.TargetId == accessory.Data.Me;
                }
            }

            if (needsRedraw)
            {
                accessory.Log.Debug("你的元素已消失，更新陷阱提示。");
                DrawFireIceTraps(accessory);
            }
        }
        [ScriptMethod(
            name: "火冰陷阱 - 记录陷阱(夺心魔)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2193"]
        )]
        public void OnTrapCreated(Event @event, ScriptAccessory accessory)
        {
            var npc = accessory.Data.Objects.SearchById(@event.TargetId);
            if (npc == null || npc.DataId != OID_JestingJackanapes) return;
            accessory.Log.Debug($"陷阱NPC {@event.TargetId} 获得元素状态, 收到的Param值为: '{@event["Param"]}'");
            // Param 为十进制字符串：836 (0x344) 为火，其余沿用冰陷阱判定。
            var isFire = @event["Param"] == "836";

            var trap = new FireIceTrapInfo
            {
                NpcId = npc.EntityId,
                Position = npc.Position,
                IsFire = isFire
            };

            lock (_trapLock)
            {
                int trapIndex = _fireIceTraps.FindIndex(t => t.NpcId == trap.NpcId);
                if (trapIndex >= 0)
                    _fireIceTraps[trapIndex] = trap;
                else
                    _fireIceTraps.Add(trap);
            }
            accessory.Log.Debug($"发现一个新的{(isFire ? "火" : "冰")}陷阱，位于 {trap.Position}");
            DrawFireIceTraps(accessory);
        }
        [ScriptMethod(
            name: "火冰陷阱 - 更新陷阱位置(夺心魔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41254"]
        )]
        public void OnTrapMove(Event @event, ScriptAccessory accessory)
        {
            lock (_trapLock)
            {
                var casterPosition = @event.SourcePosition;

                int trapIndex = _fireIceTraps.FindIndex(t => t.NpcId == @event.SourceId);
                if (trapIndex < 0)
                    trapIndex = _fireIceTraps.FindIndex(t => Vector3.Distance(t.Position, casterPosition) < 1.0f);

                if (trapIndex != -1)
                {
                    var oldTrap = _fireIceTraps[trapIndex];
                    var newPosition = @event.EffectPosition;
                    accessory.Log.Debug($"陷阱 {oldTrap.NpcId} 从 {oldTrap.Position} 移动到 {newPosition}");
                    var updatedTrap = new FireIceTrapInfo
                    {
                        NpcId = oldTrap.NpcId,
                        Position = newPosition,
                        IsFire = oldTrap.IsFire
                    };
                    _fireIceTraps[trapIndex] = updatedTrap;
                }
                else
                {
                    accessory.Log.Debug($"在位置 {casterPosition} 附近未能匹配到任何已知陷阱。");
                }
            }
            DrawFireIceTraps(accessory);
        }
        [ScriptMethod(
            name: "火冰陷阱 - 机制结束清理(夺心魔)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(41250|41251)$"],
            userControl: false
        )]
        public void OnTrapExplosion(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug("陷阱已爆炸，清理所有绘图和状态。");
            lock (_trapLock)
            {
                if (_disposed) return;
                _trapRevision++;
                _trapCts.Cancel();
                _trapCts.Dispose();
                _trapCts = new CancellationTokenSource();
                _fireIceTraps.Clear();
                _playerElements.Clear();
                accessory.Method.RemoveDraw($"^{DRAW_PREFIX}FireIceTrap_.*");
            }
        }

        private void DrawFireIceTraps(ScriptAccessory accessory)
        {
            List<FireIceTrapInfo> trapsCopy;
            long revision;
            CancellationToken token;
            bool playerHasElement;
            bool playerIsFire;
            lock (_trapLock)
            {
                if (_disposed) return;
                revision = ++_trapRevision;
                _trapCts.Cancel();
                _trapCts.Dispose();
                _trapCts = new CancellationTokenSource();
                token = _trapCts.Token;
                accessory.Method.RemoveDraw($"^{DRAW_PREFIX}FireIceTrap_.*");
                trapsCopy = _fireIceTraps.ToList();
                playerHasElement = _playerElements.TryGetValue(accessory.Data.Me, out playerIsFire);
            }
            // 大于2只小丑为20秒，否则为10秒。
            int trapWarningDurationMs = trapsCopy.Count > 2 ? 20000 : 10000;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(50, token).ConfigureAwait(false);
                    lock (_trapLock)
                    {
                        // 检查与发图共用锁，防止清理后旧回调重新画出已结束的陷阱。
                        if (_disposed || token.IsCancellationRequested || revision != _trapRevision) return;
                        foreach (var trap in trapsCopy)
                        {
                            if (!playerHasElement)
                            {
                                var dp = accessory.Data.GetDefaultDrawProperties();
                                dp.Name = DRAW_PREFIX + $"FireIceTrap_Small_{trap.NpcId}";
                                dp.Position = trap.Position;
                                dp.Scale = new Vector2(8);
                                dp.Color = accessory.Data.DefaultDangerColor;
                                dp.DestoryAt = trapWarningDurationMs;
                                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                                continue;
                            }
                            bool isSameElement = (playerIsFire == trap.IsFire);
                            if (isSameElement)
                            {
                                var dp = accessory.Data.GetDefaultDrawProperties();
                                dp.Name = DRAW_PREFIX + $"FireIceTrap_Big_Danger_{trap.NpcId}";
                                dp.Position = trap.Position;
                                dp.Scale = new Vector2(38);
                                dp.Color = new Vector4(1.0f, 0.2f, 0.2f, 0.6f);
                                dp.DestoryAt = trapWarningDurationMs;
                                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                            }
                            else
                            {
                                var dp = accessory.Data.GetDefaultDrawProperties();
                                dp.Name = DRAW_PREFIX + $"FireIceTrap_Small_Safe_{trap.NpcId}";
                                dp.Position = trap.Position;
                                dp.Scale = new Vector2(8);
                                dp.Color = new Vector4(1.0f, 0.2f, 0.2f, 0.6f);
                                dp.DestoryAt = trapWarningDurationMs;
                                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // 重绘被新排程、Init、爆炸或卸载主动取消，属于正常失效，无需报错。
                }
                catch (Exception ex)
                {
                    accessory.Log.Error($"火冰陷阱延迟重绘失败 (revision={revision}): {ex}");
                }
            });
        }

        #endregion
        #region 跃立狮OnTheHuntAreaCenter

        [ScriptMethod(
            name: "裂魄惊芒爪(跃立狮)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41411"]
        )]
        public void 恐怖闪光Draw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "跃立狮_恐怖闪光_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60);
            dp.Radian = DegToRad(90);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            accessory.Log.Debug("绘制跃立狮的恐怖闪光AOE");
        }
        [ScriptMethod(
            name: "压缩爆炸(跃立狮)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41407"]
        )]
        public void OnDecompressDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "跃立狮_Decompress_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(12);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            accessory.Log.Debug("绘制跃立狮的Decompress AOE");
        }
        [ScriptMethod(
            name: "以太射线 (跃立狮)",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:0138"]
        )]
        public void OnAetherialRayTether(Event @event, ScriptAccessory accessory)
        {

            var target = accessory.Data.Objects.SearchById(@event.TargetId);
            if (target == null)
            {
                accessory.Log.Error($"找不到连线目标: {@event.TargetId}");
                return;
            }

            var directionVector = target.Position - OnTheHuntAreaCenter;

            // 从场地中心指向连线目标的方向固定旋转200度。
            var angle = MathF.Atan2(directionVector.X, directionVector.Z);
            var finalRotation = angle + DegToRad(200);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + $"CrystalDragon_AetherialRay_{target.EntityId}";
            dp.Position = OnTheHuntAreaCenter;
            dp.Scale = new Vector2(10, 28);
            dp.Rotation = finalRotation;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5200;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            accessory.Log.Debug($"绘制以太射线, 目标: {target.Name}, 旋转角度: {finalRotation}");
        }
        [ScriptMethod(
            name: "明亮脉冲 (跃立狮)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2193"]
        )]
        public void OnBrightPulseStatus(Event @event, ScriptAccessory accessory)
        {
            var roundel = accessory.Data.Objects.SearchById(@event.TargetId);
            if (roundel == null || roundel.DataId != 18142) return;

            var dir = roundel.Position - OnTheHuntAreaCenter;

            // 以15码半径区分内外圈，分别旋转280度、150度。
            var angleDegrees = dir.LengthSquared() < 225f ? 280f : 150f;

            // 右正交向量与光球朝向的点积符号决定旋转方向。
            var forwardVector = new Vector3(MathF.Sin(roundel.Rotation), 0, MathF.Cos(roundel.Rotation));
            var orthoR = new Vector3(dir.Z, 0, -dir.X);
            if (Vector3.Dot(orthoR, forwardVector) > 0f)
            {
                angleDegrees = -angleDegrees;
            }

            var angleRadians = DegToRad(angleDegrees);
            var rotatedDir = new Vector3(
                dir.X * MathF.Cos(angleRadians) - dir.Z * MathF.Sin(angleRadians),
                dir.Y,
                dir.X * MathF.Sin(angleRadians) + dir.Z * MathF.Cos(angleRadians)
            );
            var aoePosition = OnTheHuntAreaCenter + rotatedDir;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + $"CrystalDragon_BrightPulse_{roundel.EntityId}";
            dp.Position = aoePosition;
            dp.Scale = new Vector2(13);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5200;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            accessory.Log.Debug($"绘制明亮脉冲, 光球ID: {roundel.EntityId}, 最终位置: {aoePosition}");
        }

        [ScriptMethod(
            name: "绘图移除 (跃立狮)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(41402|41403)$"],
            userControl: false
        )]
        public void RemoveCrystalDragonAoes(Event @event, ScriptAccessory accessory)
        {
            if (@event.ActionId == 41402) // AetherialRay
            {
                accessory.Method.RemoveDraw($"^{DRAW_PREFIX}CrystalDragon_AetherialRay_.*");
                accessory.Log.Debug("清理以太射线绘图。");
            }
            else if (@event.ActionId == 41403) // BrightPulse
            {
                accessory.Method.RemoveDraw($"^{DRAW_PREFIX}CrystalDragon_BrightPulse_{@event.SourceId}$");
                accessory.Log.Debug($"清理明亮脉冲绘图: {@event.SourceId}");
            }
        }
        #endregion

        #region 金钱龟
        [ScriptMethod(
            name: "古币爆风(金钱龟)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41522"]
        )]
        public void OnCostOfLivingKB(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "CostOfLivingKBDraw";
            dp.Owner = accessory.Data.Me;
            dp.Scale = new Vector2(1.5f, 30f);
            dp.Rotation = 180f * MathF.PI / 180f;
            dp.Color = new(0.3f, 1.0f, 0f, 1.5f);
            dp.TargetPosition = @event.SourcePosition;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            accessory.Log.Debug($"绘制击退: Name={dp.Name}s");
        }
        [ScriptMethod(
            name: "强制移动(金钱龟)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:4344"]
        )]
        public void OnForcedMovement(Event @event, ScriptAccessory accessory)
        {
            if (@event.TargetId != accessory.Data.Me)
            {
                accessory.Log.Debug("强制移动事件不是针对玩家，忽略。");
                return;
            }
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "ForcedMovementDraw";
            dp.Owner = accessory.Data.Me;
            dp.Scale = new Vector2(1.5f, 35f);
            dp.Color = new(0.3f, 1.0f, 0f, 1.5f);
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            accessory.Log.Debug($"绘制强制移动: Name={dp.Name}");
        }
        #endregion

        #region Fate

        [ScriptMethod(
            name: "火基佬扇形",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(30351|30353)$"]
        )]
        public void OnFluidSwingDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "FluidSwing";
            dp.Radian = 90 * MathF.PI / 180f;
            dp.Scale = new Vector2(60);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = @event.SourceId;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(
            name: "火基佬钢铁",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(30340)$"]
        )]
        public void OnHeatVortexDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "HeatVortex";
            dp.Scale = new Vector2(10);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = @event.SourceId;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(
            name: "火基佬矩形",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(30341)$"]
        )]
        public void OnFireBlastDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = DRAW_PREFIX + "FireBlast";
            dp.Scale = new Vector2(6, 25);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = @event.SourceId;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        #endregion

        #region Helper

        private static float DegToRad(float degrees) => degrees * MathF.PI / 180.0f;

        private static bool TryDeserializeVector3(string json, out Vector3 result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var deserialized = JsonConvert.DeserializeObject<Vector3?>(json);
                if (!deserialized.HasValue)
                    return false;
                var value = deserialized.Value;
                if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
                    return false;
                result = value;
                return true;
            }
            catch (JsonException)
            {
                // 事件字段格式失败时允许忽略本次绘图，其他异常交由宿主记录。
                return false;
            }
        }

        private static bool TryDeserializeFloat(string json, out float result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var deserialized = JsonConvert.DeserializeObject<float?>(json);
                if (!deserialized.HasValue || !float.IsFinite(deserialized.Value))
                    return false;
                result = deserialized.Value;
                return true;
            }
            catch (JsonException)
            {
                // 事件字段格式失败时允许忽略本次绘图，其他异常交由宿主记录。
                return false;
            }
        }

        #endregion

    }
}
