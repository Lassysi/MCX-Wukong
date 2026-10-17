using System;
using System.Collections.Generic;
using b1;
using BtlShare;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // 空手攻击的最小闭环：独立挥手/冷却、镜头命中查询、原生受击结算。
    // 不调用天命人的技能或动画；不直接改目标 HP，不重写 Boss 状态。
    internal sealed class McCombat : IDisposable
    {
        private static readonly int PunchEffectId = 2070016001;
        private static readonly double CooldownSeconds = 0.25;
        private static readonly double SwingSeconds = 0.3;
        private static readonly float ReachCm = 300f;
        private readonly BGUPlayerCharacterCS player;
        private readonly McCameraRig camera;
        private readonly BUEffectNormalDamage damageTemplate = new BUEffectNormalDamage();
        private Dictionary<int, FUStSkillEffectDesc> effects;
        private FUStSkillEffectDesc effect;
        private double now, lastAttack = -100, swingStarted = -100;
        internal bool Active { get; private set; }
        internal double Swing
        {
            get { double t = (now - swingStarted) / SwingSeconds; return t >= 0 && t < 1 ? t : 0; }
        }

        internal McCombat(BGUPlayerCharacterCS player, McCameraRig camera)
        {
            this.player = player;
            this.camera = camera;
        }

        internal bool Enable()
        {
            try
            {
                effects = BGW_GameDB.GetAllSkillEffectDesc();
                if (effects.ContainsKey(PunchEffectId)) throw new InvalidOperationException("空手效果 ID 已被占用");
                effect = new FUStSkillEffectDesc
                {
                    ID = PunchEffectId,
                    EffectType = EBuffAndSkillEffectType.SkillDamage,
                    EffectCategory = EBuffAndSkillEffectCategory.Negative
                };
                effect.EffectParamsInt.Add(new int[] { 0, 0, (int)ESkillDamageType.BluntAtk, 0, 0, 0, 0 });
                // 暂以宿主攻击力的 25% 映射空手伤害；不伪称已经完成 MC 物品数值体系。
                effect.EffectParamsFloat.Add(new float[] { 0f, 0f, 2500f, 0f, 0f, 0f, 0f });
                effects.Add(PunchEffectId, effect);
                Active = true;
                Log.Write("combat", "空手接管就绪 reach=300cm cooldown=0.25s damage=nativeAtk*0.25");
                return true;
            }
            catch (Exception e) { Log.Write("combat-error", "启用失败: " + e.Message); return false; }
        }

        internal void Update(double time, bool gameplay)
        {
            now = time;
            if (!gameplay) swingStarted = -100;
        }

        internal void Attack()
        {
            if (!Active || camera == null || !camera.Active || now - lastAttack < CooldownSeconds) return;
            lastAttack = swingStarted = now;
            try
            {
                FVector start = camera.Position;
                FVector direction = camera.Rotation.RotateVector(new FVector(1f, 0f, 0f));
                FVector end = start + direction * ReachCm;
                FHitResult hit;
                bool blocked = USystemLibrary.LineTraceSingle(player, start, end,
                    ETraceTypeQuery.TraceTypeQuery1, false, new List<AActor> { player },
                    EDrawDebugTrace.None, out hit, true, default(FLinearColor), default(FLinearColor), 0f);
                UPrimitiveComponent component = blocked ? hit.Component.Get() : null;
                BGUCharacterCS target = component == null ? null : component.GetOwner() as BGUCharacterCS;
                if (target == null || target == player || target.IsDestroyed || target.IsPendingKill
                    || BGUFunctionLibraryCS.BGUIsUnitDead(target))
                {
                    Log.Write("combat", "空手挥击 " + (blocked ? "命中场景，无敌人伤害" : "未命中"));
                    return;
                }
                FVector point = new FVector(hit.ImpactPoint.X, hit.ImpactPoint.Y, hit.ImpactPoint.Z);
                var request = new FEffectInstReq(player)
                {
                    HitLocation = point, HitComp = component, HitBoneName = hit.BoneName,
                    SweepCheckBegin = start, SweepCheckEnd = end, HitDiretionRealDir = direction,
                    HitPointNormalDir = direction.Rotation(), FXWeight_HitChr = 1
                };
                float before = BGUFunctionLibraryCS.BGUGetFloatAttr(target, EBGUAttrFloat.Hp);
                damageTemplate.ApplyBySkill(PunchEffectId, player, target, in request);
                float after = BGUFunctionLibraryCS.BGUGetFloatAttr(target, EBGUAttrFloat.Hp);
                Log.Write("combat", "空手命中 " + target.GetClass().GetName() + " hp=" + before + "->" + after
                    + (after < before ? " damage-confirmed" : " unchanged/blocked"));
            }
            catch (Exception e) { Log.Write("combat-error", "空手攻击: " + e.GetType().Name + ": " + e.Message); }
        }

        public void Dispose()
        {
            Active = false;
            FUStSkillEffectDesc registered;
            if (effects != null && effects.TryGetValue(PunchEffectId, out registered)
                && ReferenceEquals(registered, effect)) effects.Remove(PunchEffectId);
        }
    }
}
