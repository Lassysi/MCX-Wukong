using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using b1;
using BtlShare;
using UnrealEngine.Engine;

namespace DqjCombat
{
    // Native hit reactions for enemies hit by MC weapons (Preview58, native dispatch since Preview60).
    // Our MC damage uses the absolute HP-ratio path, which clears AttackStiffEffectID, so no
    // reaction plays. The host then runs the native reaction step with a real Wukong staff hit:
    // PackageDamageDescParam -> PackageDamageDynamicParam -> DoAndSolveDamageExtraLogic, which
    // picks the highest-priority BeAttackedEvent (stiff, super-armor hit/break, block, ...) natively.
    internal static class NativeHitStun
    {
        private static readonly BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        // Damage over time and environment: no flinch (fire/poison would stun-lock).
        private static readonly HashSet<string> NoStiff=new HashSet<string>{
            "minecraft:on_fire","minecraft:in_fire","minecraft:campfire","minecraft:lava","minecraft:hot_floor",
            "minecraft:magic","minecraft:indirect_magic","minecraft:wither","minecraft:freeze","minecraft:drown",
            "minecraft:starve","minecraft:cactus","minecraft:sweet_berry_bush","minecraft:in_wall","minecraft:dragon_breath"};
        // Live58 probe: Wukong staff damage effects (skill 10804 flurry, 10801 combo, 10830 heavy).
        private static readonly int LightEffect=1080401,ComboEffect=1080101,HeavyEffect=1021001;
        private static MethodInfo package,packageDesc,dispatch;
        private static FieldInfo stiffLevel,fightBack;
        private static Type descType,dynamicType,eventType;

        internal static bool NoReaction(string reason){return reason!=null&&NoStiff.Contains(reason);}
        internal static void Try(string id,BGUCharacterCS victim,AActor attacker,FEffectInstReq request,FSkillDamageConfig unused,double amount,string reason,bool heavy)
        {
            if(NoReaction(reason))return;
            object comp=BeAttackedComp(victim);
            if(comp==null){Log.Write("world-stiff","id="+id+" no native BeAttacked component");return;}
            Bind(comp.GetType());
            // Preview60 (user: stagger only when Wukong would stagger): the MC hit is presented to
            // the native reaction dispatcher as a real Wukong staff hit (Live58 probe IDs). Native
            // event selection then decides: normal stiff, super-armor (poise) hit / break, block,
            // immobilize/frozen break, or nothing. HP damage stays the MC value (applied before).
            // Preview126: the host poise meter (StaggerPolicy) already decided that this attack staggers; damage no longer picks
            // the reaction (Live125: every hit >= 10 damage used the heavy knockback effect). Heavy only for heavy attacks.
            int effect=heavy?HeavyEffect:ComboEffect;
            var config=new FSkillDamageConfig{DmgReason=(EDamageReason)2,DamageCalcType=(EDamageCalcType)0,DmgReasonEffectID=effect,CameraShakeDA=""};
            float armorBefore=BGUFunctionLibraryCS.BGUGetFloatAttr(victim,(EBGUAttrFloat)160);
            object desc=Activator.CreateInstance(descType);
            var packed=new object[]{attacker,request,config,desc};
            if(!(bool)Call(packageDesc,comp,packed)){Log.Write("world-stiff","id="+id+" native desc rejected effect="+effect);return;}
            request=(FEffectInstReq)packed[1];config=(FSkillDamageConfig)packed[2];desc=packed[3];
            // Live64: FightBack puts the enemy into a pre-counter stance that waits for a close
            // Wukong attack; ranged MC hits never come close, so the enemy stood frozen. MC hits
            // do not arm counters; stiff, super armor and armor break stay native.
            fightBack.SetValue(desc,false);
            object dynamic=Activator.CreateInstance(dynamicType);
            var dyn=new object[]{attacker,desc,request,dynamic};
            Call(package,comp,dyn);
            desc=dyn[1];request=(FEffectInstReq)dyn[2];dynamic=dyn[3];
            // Damage value 0: HP was already settled by the MC path; lethal/life-saving events must not re-count it.
            var extra=new object[]{attacker,0f,dynamic,desc,config,request,Enum.ToObject(eventType,18)};
            Call(dispatch,comp,extra);
            Log.Write("world-stiff","id="+id+(heavy?" heavy":"")+" MC="+amount.ToString("0.##",CultureInfo.InvariantCulture)+" reason="+reason+" effect="+effect
                +" stiffLevel="+stiffLevel.GetValue(extra[2])+" event="+extra[6]+" armor="+armorBefore.ToString("0.#",CultureInfo.InvariantCulture)
                +"->"+BGUFunctionLibraryCS.BGUGetFloatAttr(victim,(EBGUAttrFloat)160).ToString("0.#",CultureInfo.InvariantCulture));
        }

        private static object BeAttackedComp(BGUCharacterCS victim)
        {
            var handlers=HookLedger.ReadHandlers(HookLedger.Resolve(GameApi.Events(victim),"Evt_TriggerNormalDamageEffect"));
            if(handlers==null)return null;
            foreach(var h in handlers)if(h.Target!=null&&h.Target.GetType().FullName=="b1.BUS_BeAttackedComp")return h.Target;
            return null;
        }

        private static void Bind(Type comp)
        {
            if(dispatch!=null)return;
            descType=comp.GetNestedType("FDamageDescParam",BindingFlags.Public|BindingFlags.NonPublic);
            dynamicType=comp.GetNestedType("FDamageDynamicParam",BindingFlags.Public|BindingFlags.NonPublic);
            eventType=comp.GetNestedType("EBeAttackedEvent",BindingFlags.Public|BindingFlags.NonPublic);
            if(descType==null||dynamicType==null||eventType==null)throw new MissingMemberException("b1.BUS_BeAttackedComp","damage param types");
            stiffLevel=Need(dynamicType.GetField("StiffLevel",Members),"StiffLevel");
            fightBack=Need(descType.GetField("bCanTriggerFightBackCounter",Members),"bCanTriggerFightBackCounter");
            packageDesc=Need(comp.GetMethod("PackageDamageDescParam",Members),"PackageDamageDescParam");
            package=Need(comp.GetMethod("PackageDamageDynamicParam",Members),"PackageDamageDynamicParam");
            dispatch=Need(comp.GetMethod("DoAndSolveDamageExtraLogic",Members),"DoAndSolveDamageExtraLogic");
        }
        private static T Need<T>(T member,string name) where T:class
        {if(member==null)throw new MissingMemberException("b1.BUS_BeAttackedComp",name);return member;}
        private static object Call(MethodInfo method,object target,object[] args)
        {
            try{return method.Invoke(target,args);}
            catch(TargetInvocationException e){throw e.InnerException??e;}
        }
    }
}
