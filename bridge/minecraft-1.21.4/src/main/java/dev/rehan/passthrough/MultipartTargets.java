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
    /** Preview127 (Live126 01:29:49: one diamond-sword swing touched 7 Erlang Shen parts and 2 of them landed full damage on the root,
     *  so every swing counted twice): one attack lands once per enemy. Same attacker, same direct entity (player, arrow, TNT) and
     *  same damage type on the same root within 3 ticks is one attack; the first hit counts, the rest are dropped before damage.
     *  Separate swings, different enemies, every arrow and attacker-less damage (fire, lava) are unaffected. */
    private static final java.util.Map<String,Long> landed=new java.util.HashMap<>();
    static final int SAME_ATTACK_TICKS=3;
    public static boolean duplicate(LivingEntity root,net.minecraft.world.damagesource.DamageSource source,long tick){
        if(source.getEntity()==null&&source.getDirectEntity()==null)return false;
        String key=root.getUUID()+"|"+(source.getEntity()==null?"-":source.getEntity().getUUID())+"|"
            +(source.getDirectEntity()==null?"-":source.getDirectEntity().getUUID())+"|"+source.getMsgId();
        Long last=landed.get(key);
        if(last!=null&&tick>=last&&tick-last<SAME_ATTACK_TICKS)return true;
        landed.put(key,tick);
        if(landed.size()>1024)landed.values().removeIf(t->tick-t>SAME_ATTACK_TICKS);
        return false;
    }
    private static boolean routing;
    /** Server thread: a part hit forwarded to its root, already counted by duplicate(). */
    public static boolean routed(java.util.function.BooleanSupplier hurt){
        boolean previous=routing;routing=true;
        try{return hurt.getAsBoolean();}finally{routing=previous;}
    }
    public static boolean routing(){return routing;}
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
