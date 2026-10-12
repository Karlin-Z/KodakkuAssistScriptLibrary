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
    [ScriptType(name: "格莱杨拉波尔歼殛战 Hell on Rails(Extreme)", territorys: [1308], guid: "b9dfc210-add4-d464-3030-e9e294b384ff", version: "0.0.0.2", author: "RyougiMio", note: "Bug fix")]
    public class Script1308
    {
        // ==================== 用户设置区域 ====================
        
        [UserSetting("常用危险色")]
        public ScriptColor DangerColor { get; set; } = new ScriptColor() { V4 = new Vector4(1.0f, 0.0f, 0.0f, 1.0f) };

        [UserSetting("常用安全色")]
        public ScriptColor SafeColor { get; set; } = new ScriptColor() { V4 = new Vector4(0.0f, 1.0f, 0.0f, 1.0f) };
        [UserSetting("当前阶段 (PhaseCount) - 仅调试用")]
        public int phaseCount { get; set; } = 1;

        [UserSetting("技能计数 (SkillCount) - 仅调试用")]
        public int skillCount { get; set; } = 0;
        private int skillCount1 = 0;
    
        /// <summary>          
        /// </summary>
        public void Init(ScriptAccessory accessory)
        {
            // 重置变量
            phaseCount = 1;
            skillCount = 0; // 重置计数器
            skillCount1 = 0;
            accessory.Method.RemoveDraw(".*");
            
            accessory.Method.SendChat("/e HoR(EX) Initialized.");
        }

        [ScriptMethod(name: "计数更新_45663/45664", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45663|45664)$"], userControl: false)]
        public void UpdatePhaseBySkillCount(Event @event, ScriptAccessory accessory)
        {
            skillCount++;



            if (skillCount == 4)
            {
                this.phaseCount++;


            }
            if (skillCount >= 5)
            {
                this.phaseCount=6;


            }
        }

        [ScriptMethod(name: "雷光环(P1)", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19000"], userControl: true)]
        public void DrawLightningRect(Event @event, ScriptAccessory accessory)
        {
            // 过滤空中的雷光环
            if (@event.SourcePosition.Y > 4.0f || phaseCount >2) 
            {
                return; 
            }


            var dpGrow = accessory.Data.GetDefaultDrawProperties();
            dpGrow.Name = $"LightningRect_Grow_{@event.SourceId}";
            dpGrow.Owner = @event.SourceId;
            dpGrow.Color = accessory.Data.DefaultDangerColor;
            dpGrow.Offset = new Vector3(0, 0, -15); // Z轴偏移 -15
            dpGrow.DestoryAt = 7030;                // 生长耗时 5.7秒


            dpGrow.ScaleMode = ScaleMode.XByTime;
            dpGrow.Scale = new Vector2(5f, 30f); 

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dpGrow);


            var dpStatic = accessory.Data.GetDefaultDrawProperties();
            dpStatic.Name = $"LightningRect_Static_{@event.SourceId}";
            dpStatic.Owner = @event.SourceId;
            dpStatic.Color = accessory.Data.DefaultDangerColor;
            dpStatic.Offset = new Vector3(0, 0, -15); // 保持相同的偏移


            dpStatic.ScaleMode = ScaleMode.None; 
            dpStatic.Scale = new Vector2(5f, 30f); // 保持第一阶段结束时的大小

            // 时间控制：
            dpStatic.Delay = 7030;     // 等第一阶段结束（填满）后再显示
            dpStatic.DestoryAt = 970; // 持续显示 1秒

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dpStatic);
        }
        [ScriptMethod(name: "雷光环(P5)", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19000"], userControl: true)]
        public void DrawLightningRect1(Event @event, ScriptAccessory accessory)
        {
            // 过滤空中的雷光环
            if ((@event.SourcePosition.X > 95.0f&&@event.SourcePosition.X <100.0f )|| phaseCount <2) 
            {
                return; 
            }

 
            var dpGrow = accessory.Data.GetDefaultDrawProperties();
            dpGrow.Name = $"LightningRect_Grow_{@event.SourceId}";
            dpGrow.Owner = @event.SourceId;
            dpGrow.Color = accessory.Data.DefaultDangerColor;
            dpGrow.Offset = new Vector3(0, 0, -15); // Z轴偏移 -15
            dpGrow.DestoryAt = 7030;                // 生长耗时 5.7秒

   
            dpGrow.ScaleMode = ScaleMode.XByTime;
            dpGrow.Scale = new Vector2(5f, 30f); 

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dpGrow);


     
            var dpStatic = accessory.Data.GetDefaultDrawProperties();
            dpStatic.Name = $"LightningRect_Static_{@event.SourceId}";
            dpStatic.Owner = @event.SourceId;
            dpStatic.Color = accessory.Data.DefaultDangerColor;
            dpStatic.Offset = new Vector3(0, 0, -15); // 保持相同的偏移

   
            dpStatic.ScaleMode = ScaleMode.None; 
            dpStatic.Scale = new Vector2(5f, 30f); // 保持第一阶段结束时的大小

            // 时间控制：
            dpStatic.Delay = 7030;     // 等第一阶段结束（填满）后再显示
            dpStatic.DestoryAt = 970; // 持续显示 1秒

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dpStatic);
        }
        
        [ScriptMethod(name: "超增压急行", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45670"], userControl: true)]
        public void DrawBossArrow(Event @event, ScriptAccessory accessory)
        {

            var dp = accessory.Data.GetDefaultDrawProperties();
            
            dp.Name = $"Boss_Arrow_{@event.SourceId}";
            dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f); // 使用默认危险色
            
            // 固定中心位置 (100, 0, 100)
            dp.Position = new Vector3(100, 0, 40+phaseCount*50);
            
  
            dp.Rotation = 0; 

            // 持续 6500 毫秒
            dp.DestoryAt = 7000;


            dp.ScaleMode = ScaleMode.YByTime;

            dp.Scale = new Vector2(10f, 20f);
            // ====================================================

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Arrow, dp);
        }
        [ScriptMethod(name: "超增压抽雾", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45696"], userControl: true)]
        public void DrawBossArrow_f(Event @event, ScriptAccessory accessory)
        {

            var dp = accessory.Data.GetDefaultDrawProperties();
            
            dp.Name = $"Boss_Arrow_{@event.SourceId}";
            dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f); // 使用默认危险色
            
  
            dp.Position = new Vector3(100, 0, 60+phaseCount*50);
            

            dp.Rotation = 3.14159f; 

            // 持续 6500 毫秒
            dp.DestoryAt = 7000;

            dp.ScaleMode = ScaleMode.YByTime;

            // X = 箭头的宽度 = 10
            // Y = 增长速度 = 总长度(10) / 时间(6.5秒)
            dp.Scale = new Vector2(10f, 20f);
            // ====================================================

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Arrow, dp);
        }

        [ScriptMethod(name: "超增压", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45663|45664)$"])]
        public void DrawPhase1Circles(Event @event, ScriptAccessory accessory)
        {
            // 1. 增加计数
            skillCount1 = skillCount1+1;
            long delay = 0;
            long duration = 5000;


            
            // 第一阶段
            if (skillCount1 == 1||skillCount1 == 2)
            {
                delay = 12000;
            }
            // 第二阶段 (phaseCount > 1)
            else
            {
                // 严格照搬你的数值要求
                if (skillCount1  == 3)
                {
                    delay = 19500;
                }
                else if (skillCount1 == 4)
                {
                    delay = 21500;
                }
                else if (skillCount1 == 5)
                {
                    delay = 52500;
                }
                else if (skillCount1 == 6)
                {
                    delay = 49500;
                }
                else
                {

                    return;
                }
            }


            if (@event.ActionId == 45663)
            {
                foreach (var playerId in accessory.Data.PartyList)
                {
                    var player = accessory.Data.Objects.SearchById(playerId);
                    if (player == null || player.IsDead) continue;

                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"Circle_45663_{playerId}_{@event.SourceId}";
                    dp.Owner = playerId;
                    dp.Color = accessory.Data.DefaultDangerColor; // 红色

                    dp.Delay = delay;
                    dp.DestoryAt = duration;


                    dp.ScaleMode = ScaleMode.ByTime;
                    dp.Scale = new Vector2(5.0f, 5.0f);

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
            }

            else if (@event.ActionId == 45664)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Circle_45664_{accessory.Data.Me}_{@event.SourceId}";
                dp.Owner = accessory.Data.Me;
                dp.Color = accessory.Data.DefaultSafeColor; // 绿色

                dp.Delay = delay;
                dp.DestoryAt = duration;

           
                dp.ScaleMode = ScaleMode.ByTime;
                dp.Scale = new Vector2(5.0f, 5.0f);

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }
        [ScriptMethod(name: "护卫炮塔", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(45681|45683|45686)$"], userControl: true)]
        public void DrawTurretLaser(Event @event, ScriptAccessory accessory)
        {
            // 基础参数
            uint actionId = @event.ActionId;
            Vector3 srcPos = @event.SourcePosition;
            float duration = 7000f; // 7秒
            

            bool isLeftTurret = Math.Abs(srcPos.X - 85) < 2;
            bool isRightTurret = Math.Abs(srcPos.X - 115) < 2;



            if (!isLeftTurret && !isRightTurret) return; 

            // 绘图属性初始化
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Turret_{actionId}_{@event.SourceId}";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = (long)duration;
            
  

            float rotation = isLeftTurret ? -float.Pi / 2 : float.Pi / 2; 
            dp.Rotation = rotation;
            dp.ScaleMode = ScaleMode.XByTime; // 开启 Y 轴(长度) 渐变

           

            // 1. Action 45681: X 从 90 射到 110 (覆盖场中)
            if (actionId == 45681)
            {
            
                float length = 5f;
                float width = 20f; 

              
                if(isLeftTurret){dp.Offset = new Vector3(0f, 0, -15); }
                if(isRightTurret){dp.Offset = new Vector3(0, 0, -15); }

                dp.Scale = new Vector2(width, length); // Y = 速度
                dp.Owner = @event.SourceId; // 绑在炮塔上
                
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
            }
           
            else if (actionId == 45683)
            {
            
                float length = 5f;
                float width = 15f;

              
                if(isLeftTurret){dp.Offset = new Vector3(0, 0, -12.5f); }
                if(isRightTurret){dp.Offset = new Vector3(0, 0, -12.5f); }

                dp.Scale = new Vector2(width, length);
                dp.Owner = @event.SourceId;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
            }
            // 3. Action 45686: Z是他的sourceposition+-2.5 (不射出来)
            
        }
        [ScriptMethod(name: "格莱杨拉波尔分身_标记", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19329"], userControl: true)]
        public void MarkClone(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Clone_Mark_{@event.SourceId}";
            dp.Owner = @event.SourceId; // 绑定到分身上，怪动圈也动
            dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f); 
            dp.Scale = new Vector2(2.0f); // 设置圆圈半径为 2米
            dp.DestoryAt = 120000; // 
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "格莱杨拉波尔分身_扇形", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:19329"], userControl: true)]
        public void DrawCloneFanToTanks(Event @event, ScriptAccessory accessory)
        {
            // 1. 查找坦克
            var tankIds = accessory.Data.PartyList.Where(id =>
            {
                var obj = accessory.Data.Objects.SearchById(id);
                
              
                return obj is IBattleChara bc && bc.IsTank();
            }).ToList();

            // 2. 绘制扇形
            foreach (var tankId in tankIds)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Clone_Fan_To_Tank_{tankId}_{@event.SourceId}";
                
                dp.Owner = @event.SourceId;    
                dp.TargetObject = tankId;     
                
                dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 0.1f); // 红色半透明
                
                dp.Radian = float.Pi / 5.143f; // 22.5度
                
                dp.Scale = new Vector2(60.0f); 
                dp.DestoryAt = 120000; 

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            }
        }
        [ScriptMethod(name: "格莱杨拉波尔分身_分散分摊", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(027D|027E)$"], userControl: true)]
        public void DrawSpreadStackByIcon(Event @event, ScriptAccessory accessory)
        {
            // 基础参数
            string iconId = @event["Id"];
            long duration = 6500; // 6.5秒

       
            if (iconId == "027E")
            {
                foreach (var playerId in accessory.Data.PartyList)
                {
                    var player = accessory.Data.Objects.SearchById(playerId);
                    if (player == null || player.IsDead) continue;

                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"Icon_Spread_{playerId}_{@event.SourceId}";
                    dp.Owner = playerId; 
                    dp.Color = accessory.Data.DefaultDangerColor; // 红色

                    dp.DestoryAt = duration;
                    dp.ScaleMode = ScaleMode.ByTime;
                    dp.Scale = new Vector2(5.0f, 5.0f);

                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
            }
            // 2. 处理分摊 (027E) -> 仅奶妈绿圈
            else if (iconId == "027D")
            {
                foreach (var playerId in accessory.Data.PartyList)
                {
                    var player = accessory.Data.Objects.SearchById(playerId);
                    
                    // 筛选条件：对象存在 且 是战斗角色 且 是治疗职业
                    if (player is IBattleChara bc && bc.IsHealer())
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"Icon_Stack_Healer_{playerId}_{@event.SourceId}";
                        dp.Owner = playerId; 
                        dp.Color = accessory.Data.DefaultSafeColor; // 绿色

                        dp.DestoryAt = duration;
                        dp.ScaleMode = ScaleMode.ByTime;
                        dp.Scale = new Vector2(5.0f, 5.0f);

                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    }
                }
            }
        }
        [ScriptMethod(name: "雷光一闪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45666"], userControl: true)]
        public void DrawLightningFlash(Event @event, ScriptAccessory accessory)
        {
            // 获取 EffectPosition
            var pos = @event.EffectPosition;
            
          
            var drawPos = new Vector3(pos.X, 0, pos.Z);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"LightningFlash_{@event.SourceId}_{@event.Id}"; 
            dp.Position = drawPos;
            dp.Color = DangerColor.V4; // 使用用户设置的危险色
            dp.Scale = new Vector2(4.0f); // 半径 5
            
            dp.Delay = 0; // 延时 3秒
            dp.DestoryAt = 3000; 

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "前照光", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45690"], userControl: true)]
        public void DrawHeadlightRects(Event @event, ScriptAccessory accessory)
        {
            var srcPos = @event.SourcePosition;
            var srcRot = @event.SourceRotation;
            
            // 矩形基础参数
            float width = 20f;
            float length = 30f;
            
          
            Vector3 rectOffset = new Vector3(0, 0, -10f);

           Vector4 blueColor = new Vector4(0.0f, 1.0f, 1.0f, 0.2f);
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = $"Headlight_High_{@event.SourceId}";
            dp1.Position = new Vector3(srcPos.X, 5.0f, srcPos.Z); 
            dp1.Rotation = srcRot;
            dp1.Color = accessory.Data.DefaultDangerColor;
            
            dp1.Offset = rectOffset; 
            dp1.DestoryAt = 6700;

            // --- 渐变设置 ---
            dp1.ScaleMode = ScaleMode.YByTime; // 随时间增长长度
            
            dp1.Scale = new Vector2(width, length); 

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);

          
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = $"Headlight_Low_{@event.SourceId}";
            dp2.Position = new Vector3(srcPos.X, 0.0f, srcPos.Z); 
            dp2.Rotation = srcRot;
            dp2.Color = blueColor;

            dp2.Offset = rectOffset;
            
            dp2.Delay = 6700;     // 等待第一阶段结束
            dp2.DestoryAt = 2700; 

            // --- 静态设置 ---
            dp2.ScaleMode = ScaleMode.YByTime; 
      
            dp2.Scale = new Vector2(width, length); 

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
        }
        [ScriptMethod(name: "雷鸣", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45687"], userControl: true)]
        public void DrawHeadlightRects1(Event @event, ScriptAccessory accessory)
        {
            var srcPos = @event.SourcePosition;
            var srcRot = @event.SourceRotation;
            
            // 矩形基础参数
            float width = 20f;
            float length = 30f;
            
            // 偏移量 (保持你的设置)
            Vector3 rectOffset = new Vector3(0, 0, -10f);

            Vector4 blueColor = new Vector4(0.0f, 1.0f, 1.0f, 0.2f);
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = $"Headlight_High_{@event.SourceId}";
            dp1.Position = new Vector3(srcPos.X, 0.0f, srcPos.Z); 
            dp1.Rotation = srcRot;
            dp1.Color = blueColor;
            
            dp1.Offset = rectOffset; 
            dp1.DestoryAt = 6700;

            // --- 渐变设置 ---
            dp1.ScaleMode = ScaleMode.YByTime; // 随时间增长长度
         
            dp1.Scale = new Vector2(width, length); 

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);

         
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = $"Headlight_Low_{@event.SourceId}";
            dp2.Position = new Vector3(srcPos.X, 5.0f, srcPos.Z); 
            dp2.Rotation = srcRot;
            dp2.Color = accessory.Data.DefaultDangerColor;

            dp2.Offset = rectOffset;
            
            dp2.Delay = 6700;     // 等待第一阶段结束
            dp2.DestoryAt = 2700; 

          
            dp2.ScaleMode = ScaleMode.YByTime; 
         
            dp2.Scale = new Vector2(width, length); 

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
        }

    }
}