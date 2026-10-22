package dev.rehan.passthrough;

import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.effect.MobEffectInstance;

/** Hit-assist geometry belongs to one enemy: health, hurt cooldown and effects live on its root. */
public final class MultipartTargets {
    private MultipartTargets() {}
    public static String parentId(String id){
        if(id==null)return null;
        int split=id.indexOf('#');
        if(split<=0||split==id.length()-1)return id;
        for(int i=split+1;i<id.length();i++)if(!Character.isDigit(id.charAt(i)))return id;
        return id.substring(0,split);
    }
    public static boolean isPart(LivingEntity e){
        String id=CombatBridge.proxyId(e);return id!=null&&!id.equals(parentId(id));
    }
    public static LivingEntity root(LivingEntity e){
        String id=CombatBridge.proxyId(e);if(id==null)return e;
        var root=CombatBridge.proxies().get(parentId(id));
        return root==null||root.isRemoved()?null:root;
    }
    static void mergeEffects(){
        for(var part:CombatBridge.proxies().values()){
            if(!isPart(part))continue;
            var root=root(part);if(root==null)continue;
            for(var effect:new java.util.ArrayList<>(part.getActiveEffects())){
                root.addEffect(new MobEffectInstance(effect));part.removeEffect(effect.getEffect());
            }
            if(part.getRemainingFireTicks()>root.getRemainingFireTicks())root.setRemainingFireTicks(part.getRemainingFireTicks());
            part.clearFire();
            if(part.getTicksFrozen()>root.getTicksFrozen())root.setTicksFrozen(part.getTicksFrozen());
            part.setTicksFrozen(0);
        }
    }
}
