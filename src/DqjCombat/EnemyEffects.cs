using System;
using System.Collections.Generic;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // Potions & items D1 (POTIONS-ITEMS-PLAN.md): MC effects on an enemy's proxy act on the real Wukong monster.
    // Slowness / freeze / cobweb / soul sand -> native time dilation (movement and attacks slower); weakness -> its hits on
    // the player scaled down (McPlayerHealthGate); knockback and levitation -> LaunchCharacter. Bosses get capped effects
    // and no launches. Every time dilation is a lease restored when the effect ends, the monster dies or the link resets.
    internal static class EnemyEffects
    {
        /// Native time dilation for an enemy (1 = normal). Pure, tested offline.
        internal static float Dilation(int slowAmp,bool freeze,bool web,bool soul,bool boss)
        {
            // Live111 (user: a Slowness splash made little visible difference; x0.85 for Slowness I): stronger curve.
            double d=1;
            if(slowAmp>=0)d=Math.Min(d,Math.Max(.3,.6-.15*slowAmp));
            if(soul)d=Math.Min(d,.5);
            if(web)d=Math.Min(d,.25);
            if(freeze)d=Math.Min(d,.15);
            if(boss)d=Math.Max(d,.65);
            return (float)d;
        }
        /// Multiplier on this enemy's hits on the player while it has Weakness. Pure, tested offline.
        internal static float WeaknessScale(int weakAmp,bool boss)
        {
            if(weakAmp<0)return 1;
            double s=Math.Pow(.75,weakAmp+1);
            return (float)Math.Max(boss?.7:.4,s);
        }
        /// MC velocity (blocks/tick, MC axes) -> native launch velocity (cm/s, Unreal axes), capped. Pure, tested offline.
        internal static void LaunchVelocity(double vx,double vy,double vz,out float x,out float y,out float z)
        {
            double scale=1500;   // 0.4 b/t sword knockback -> 600 cm/s (no const: loader rule)
            double ux=vz*scale,uy=-vx*scale,uz=Math.Max(0,vy)*scale;
            double len=Math.Sqrt(ux*ux+uy*uy+uz*uz);if(len>2500){ux*=2500/len;uy*=2500/len;uz*=2500/len;}
            x=(float)ux;y=(float)uy;z=(float)uz;
        }

        private static readonly Dictionary<AActor,float> leased=new Dictionary<AActor,float>();
        internal static void SetDilation(AActor actor,float value)
        {
            if(!NativeObjectLifetime.Live(actor))return;
            float original;
            if(value>=.999f){
                if(leased.TryGetValue(actor,out original)){actor.CustomTimeDilation=original;leased.Remove(actor);}
                return;
            }
            if(!leased.ContainsKey(actor))leased[actor]=actor.CustomTimeDilation;
            actor.CustomTimeDilation=value;
        }
        internal static void RestoreAll()
        {
            foreach(var pair in leased)try{if(NativeObjectLifetime.Live(pair.Key))pair.Key.CustomTimeDilation=pair.Value;}catch{}
            leased.Clear();McPlayerHealthGate.WeakScale.Clear();
        }
        internal static void Forget(AActor actor){float o;if(leased.TryGetValue(actor,out o)){try{if(NativeObjectLifetime.Live(actor))actor.CustomTimeDilation=o;}catch{}leased.Remove(actor);}}
        internal static int Count{get{return leased.Count;}}

        // Live112 (user: enchantments must really work): Smite/Bane of Arthropods need an enemy category; MC proxies are pigs,
        // so the host tags each enemy by class-name tokens. Unknown classes stay "" (logged once per class by McWorldLink).
        private static readonly string[] UndeadTokens={"ganshi","guyao","kulou","wutou","youhun","guhun","baigu","jiangshi","zombie","skeleton","shiti","guihun","yougui","lingji"};
        private static readonly string[] ArthropodTokens={"zhizhu","wugong","xiezi","tanglang","mayi","chong","cangying","mifeng","huangfeng","spider","centipede","scorpion","zhanglang"};
        internal static string Kind(string className)
        {
            if(string.IsNullOrEmpty(className))return "";
            var c=className.ToLowerInvariant();
            foreach(var t in UndeadTokens)if(c.Contains(t))return "undead";
            foreach(var t in ArthropodTokens)if(c.Contains(t))return "arthropod";
            return "";
        }
    }
}
