package dev.rehan.passthrough;

import net.minecraft.core.Holder;
import net.minecraft.core.Registry;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.effect.MobEffect;
import net.minecraft.world.effect.MobEffectCategory;
import net.minecraft.world.effect.MobEffectInstance;
import net.minecraft.world.effect.MobEffects;
import net.minecraft.world.entity.LivingEntity;

/** Consumption, expiry and milk/death removal follow the ordinary saved MC effect lifecycle. */
public final class GoldenApple {
    private GoldenApple() {}
    private static final class GuardEffect extends MobEffect {
        GuardEffect(){super(MobEffectCategory.BENEFICIAL,0xffd54f);}
    }
    private static final Holder<MobEffect> GUARD=Registry.registerForHolder(BuiltInRegistries.MOB_EFFECT,
        ResourceLocation.fromNamespaceAndPath("dqj","golden_apple_guard"),new GuardEffect());
    public static void register(){}
    public static boolean protectedFromStatus(LivingEntity entity){return entity.hasEffect(GUARD);}
    public static int remainingTicks(LivingEntity entity){var effect=entity.getEffect(GUARD);return effect==null?0:effect.getDuration();}
    public static boolean blocks(LivingEntity entity,MobEffectInstance effect){
        return protectedFromStatus(entity)&&effect.getEffect().value().getCategory()==MobEffectCategory.HARMFUL;
    }
    public static void consumed(ServerPlayer player){
        if(!player.isAlive())return;
        // A real heal emits the acknowledged MC health delta before another native snapshot can overwrite it.
        player.heal(Math.max(0,player.getMaxHealth()-player.getHealth()));
        var harmful=new java.util.ArrayList<Holder<MobEffect>>();
        for(var effect:player.getActiveEffects())if(effect.getEffect().value().getCategory()==MobEffectCategory.HARMFUL)harmful.add(effect.getEffect());
        for(var effect:harmful)player.removeEffect(effect);
        player.addEffect(new MobEffectInstance(GUARD,2400,0,false,false,false));
        player.addEffect(new MobEffectInstance(MobEffects.ABSORPTION,2400,3));
        // Preview120 review (user: defence raised appropriately; Live110 called the apple's protection too extreme): the
        // vanilla Resistance I from the apple stays (20% less damage, on top of armor); no Resistance II override.
        player.addEffect(new MobEffectInstance(MobEffects.FIRE_RESISTANCE,2400,0));
        // Re-eating refreshes spent gold hearts without stacking an unbounded absorption pool.
        player.setAbsorptionAmount(Math.max(player.getAbsorptionAmount(),Math.min(16,player.getMaxAbsorption())));
        protectTick(player);
        ProxyFx.sendPlayerFlags(player);
        Passthrough.LOG.info("DQJ apple: full health {}/{}, absorption {}, vanilla resistance I and status guard for 120 s",player.getHealth(),player.getMaxHealth(),player.getAbsorptionAmount());
    }
    public static void protectTick(LivingEntity player){
        if(!protectedFromStatus(player))return;
        player.clearFire();player.setTicksFrozen(0);
        var harmful=new java.util.ArrayList<Holder<MobEffect>>();
        for(var effect:player.getActiveEffects())if(effect.getEffect().value().getCategory()==MobEffectCategory.HARMFUL)harmful.add(effect.getEffect());
        for(var effect:harmful)player.removeEffect(effect);
    }
}
