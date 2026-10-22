package dev.rehan.passthrough;

import java.util.ArrayList;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import net.fabricmc.fabric.api.gametest.v1.FabricGameTest;
import net.minecraft.gametest.framework.GameTest;
import net.minecraft.gametest.framework.GameTestHelper;
import net.minecraft.world.entity.EquipmentSlot;
import net.minecraft.world.item.Items;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.effect.MobEffects;
import net.minecraft.world.effect.MobEffectInstance;
import net.minecraft.world.level.GameType;

/** Actual finishUsingItem, health ACK/replay, gold hearts, mitigation and status-effect lifecycle. */
public final class GoldenAppleTests implements FabricGameTest {
    private static void close(GameTestHelper h,double actual,double expected,String name){h.assertTrue(Math.abs(actual-expected)<.002,name+" expected="+expected+" actual="+actual);}
    private static void snapshot(String session,float hp,long ack)throws Exception{
        var m=new JsonObject();m.addProperty("session",session);m.addProperty("hp",hp);m.addProperty("ack",ack);m.add("entities",new com.google.gson.JsonArray());
        var apply=CombatBridge.class.getDeclaredMethod("apply",JsonObject.class);apply.setAccessible(true);apply.invoke(null,m);
    }
    /** Preview120 (user: an Infinity bow still used up the arrows): any arrow type is spared, one arrow still required. */
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=40)
    public void infinitySparesTippedArrows(GameTestHelper h){
        var level=h.getLevel();var p=h.makeMockServerPlayerInLevel();p.setGameMode(GameType.SURVIVAL);p.getAbilities().invulnerable=false;
        boolean wasActive=Passthrough.active;Passthrough.active=true;
        try{
            var bow=new ItemStack(Items.BOW);
            var infinity=level.registryAccess().lookupOrThrow(net.minecraft.core.registries.Registries.ENCHANTMENT).getOrThrow(net.minecraft.world.item.enchantment.Enchantments.INFINITY);
            bow.enchant(infinity,1);p.setItemSlot(EquipmentSlot.MAINHAND,bow);p.getInventory().clearContent();p.setItemSlot(EquipmentSlot.MAINHAND,bow);
            var tipped=net.minecraft.world.item.alchemy.PotionContents.createItemStack(Items.TIPPED_ARROW,net.minecraft.world.item.alchemy.Potions.SLOWNESS);
            p.getInventory().add(tipped);
            int before=level.getEntitiesOfClass(net.minecraft.world.entity.projectile.AbstractArrow.class,p.getBoundingBox().inflate(64)).size();
            bow.getItem().releaseUsing(bow,level,p,bow.getUseDuration(p)-25);
            int after=level.getEntitiesOfClass(net.minecraft.world.entity.projectile.AbstractArrow.class,p.getBoundingBox().inflate(64)).size();
            h.assertTrue(after==before+1,"Infinity bow fires with one tipped arrow");
            h.assertTrue(p.getInventory().countItem(Items.TIPPED_ARROW)==1,"tipped arrow is not consumed");
            p.getInventory().clearContent();p.setItemSlot(EquipmentSlot.MAINHAND,bow);
            bow.getItem().releaseUsing(bow,level,p,bow.getUseDuration(p)-25);
            int none=level.getEntitiesOfClass(net.minecraft.world.entity.projectile.AbstractArrow.class,p.getBoundingBox().inflate(64)).size();
            h.assertTrue(none==after,"with no arrow at all the Infinity bow does not fire (one arrow required)");
            var plain=new ItemStack(Items.BOW);p.setItemSlot(EquipmentSlot.MAINHAND,plain);p.getInventory().add(new ItemStack(Items.ARROW,2));
            plain.getItem().releaseUsing(plain,level,p,plain.getUseDuration(p)-25);
            h.assertTrue(p.getInventory().countItem(Items.ARROW)==1,"a bow without Infinity still uses arrows");
            Passthrough.LOG.info("Preview120 infinity PASS: tipped arrow spared, one arrow required, plain bow consumes");
            h.succeed();
        }finally{Passthrough.active=wasActive;}
    }
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=70)
    public void consumeAndSyncRealItem(GameTestHelper h)throws Exception{
        var level=h.getLevel();var p=h.makeMockServerPlayerInLevel();p.setGameMode(GameType.SURVIVAL);p.setClientLoaded(true);p.getAbilities().invulnerable=false;
        Passthrough.active=Passthrough.minecraftAuthority=true;CombatBridge.attach(level.getServer());
        var messages=new ArrayList<JsonObject>();var previous=Passthrough.events;Passthrough.events=s->messages.add(JsonParser.parseString(s).getAsJsonObject());
        try{
            p.removeAllEffects();p.setAbsorptionAmount(0);p.setHealth(6);snapshot("apple-test",6,0);
            p.addEffect(new MobEffectInstance(MobEffects.POISON,400,1));p.addEffect(new MobEffectInstance(MobEffects.MOVEMENT_SLOWDOWN,400,1));
            p.addEffect(new MobEffectInstance(MobEffects.MOVEMENT_SPEED,400,0));
            var last=new ItemStack(Items.ENCHANTED_GOLDEN_APPLE,1);p.setItemSlot(EquipmentSlot.MAINHAND,last);
            var result=last.finishUsingItem(level,p);p.setItemSlot(EquipmentSlot.MAINHAND,result);
            close(h,result.getCount(),0,"last apple consumed exactly once");close(h,p.getHealth(),20,"instant full red health");
            snapshot("apple-test",6,0);close(h,p.getHealth(),20,"stale native snapshot cannot erase unacknowledged full heal");
            close(h,p.getAbsorptionAmount(),16,"absorption IV gives 16 gold health");
            h.assertTrue(GoldenApple.protectedFromStatus(p),"guard active");
            close(h,GoldenApple.remainingTicks(p),2400,"120 second lifetime");
            close(h,p.getEffect(MobEffects.DAMAGE_RESISTANCE).getAmplifier(),0,"vanilla resistance I (no stronger override)");
            h.assertFalse(p.hasEffect(MobEffects.POISON),"existing poison cleared");h.assertFalse(p.hasEffect(MobEffects.MOVEMENT_SLOWDOWN),"existing slowness cleared");
            h.assertTrue(p.hasEffect(MobEffects.MOVEMENT_SPEED),"unrelated beneficial effect kept");
            int heals=0;long seq=0;
            for(var m:messages)if(m.get("t").getAsString().equals("health_delta")&&m.get("id").getAsString().equals("player")&&m.get("amount").getAsFloat()<0){
                heals++;close(h,m.get("amount").getAsFloat(),-14,"full heal enters acknowledged health protocol");seq=m.get("seq").getAsLong();
            }
            close(h,heals,1,"exactly one heal delta");h.assertTrue(seq>0,"heal gets sequence number");
            snapshot("apple-test",6,0);close(h,p.getHealth(),20,"stale native snapshot cannot erase unacknowledged full heal");
            CombatBridge.tick(level.getServer());close(h,p.getAbsorptionAmount(),16,"bridge tick retains gold hearts");
            snapshot("apple-test",20,seq);close(h,p.getHealth(),20,"native heal ack settles without duplicate healing");
            h.assertFalse(p.addEffect(new MobEffectInstance(MobEffects.POISON,200,0)),"new poison rejected before application");
            h.assertFalse(p.addEffect(new MobEffectInstance(MobEffects.WITHER,200,0)),"new wither rejected");
            h.assertFalse(p.addEffect(new MobEffectInstance(MobEffects.WEAKNESS,200,0)),"new weakness rejected");
            h.assertTrue(p.addEffect(new MobEffectInstance(MobEffects.DIG_SPEED,200,0)),"positive effects still accepted");
            p.igniteForSeconds(10);p.setTicksFrozen(130);GoldenApple.protectTick(p);
            h.assertFalse(p.isOnFire(),"burning cleared");close(h,p.getTicksFrozen(),0,"freeze accumulation cleared");
            var source=new net.minecraft.world.damagesource.DamageSource(level.registryAccess().lookupOrThrow(net.minecraft.core.registries.Registries.DAMAGE_TYPE).getOrThrow(net.minecraft.resources.ResourceKey.create(net.minecraft.core.registries.Registries.DAMAGE_TYPE,net.minecraft.resources.ResourceLocation.fromNamespaceAndPath("dqj","wukong_attack"))));
            p.invulnerableTime=0;p.hurtServer(level,source,10);
            close(h,p.getHealth(),20,"native hit first spends gold health");close(h,p.getAbsorptionAmount(),8,"resistance I reduces 10 to 8 then absorption takes it");
            var second=new ItemStack(Items.ENCHANTED_GOLDEN_APPLE,2);second.finishUsingItem(level,p);
            close(h,second.getCount(),1,"stacked apple consumes one");close(h,p.getAbsorptionAmount(),16,"re-eating replenishes rather than infinitely stacks gold health");
            var milk=new ItemStack(Items.MILK_BUCKET);var bucket=milk.finishUsingItem(level,p);
            h.assertTrue(bucket.is(Items.BUCKET),"milk container preserved");h.assertFalse(GoldenApple.protectedFromStatus(p),"milk removes guard");
            close(h,p.getAbsorptionAmount(),0,"milk removes temporary gold health");h.assertTrue(p.addEffect(new MobEffectInstance(MobEffects.POISON,100,0)),"normal poison returns after milk");
            p.removeAllEffects();p.setHealth(7);snapshot("apple-test",7,seq);
            new ItemStack(Items.GOLDEN_APPLE).finishUsingItem(level,p);h.assertFalse(GoldenApple.protectedFromStatus(p),"ordinary apple does not grant special immunity");
            close(h,p.getHealth(),7,"ordinary apple does not instantly fill red health");close(h,p.getAbsorptionAmount(),4,"ordinary apple gold hearts remain real");
            CombatBridge.tick(level.getServer());close(h,p.getAbsorptionAmount(),4,"ordinary apple gold hearts not converted");
            p.removeAllEffects();p.setHealth(7);snapshot("apple-test",7,seq);Passthrough.active=false;
            new ItemStack(Items.ENCHANTED_GOLDEN_APPLE).finishUsingItem(level,p);
            close(h,p.getHealth(),7,"bridge disabled retains vanilla food behavior");h.assertFalse(GoldenApple.protectedFromStatus(p),"no custom immunity outside bridge");
            Passthrough.active=true;p.removeAllEffects();GoldenApple.consumed(p);
            var guard=p.getActiveEffects().stream().filter(e->e.getEffect().unwrapKey().map(k->k.location().toString().equals("dqj:golden_apple_guard")).orElse(false)).findFirst().orElseThrow();
            p.removeEffect(guard.getEffect());p.addEffect(new MobEffectInstance(guard.getEffect(),1,0,false,false,false));
            var effectTick=net.minecraft.world.entity.LivingEntity.class.getDeclaredMethod("tickEffects");effectTick.setAccessible(true);
            for(int t=0;t<3&&GoldenApple.protectedFromStatus(p);t++)effectTick.invoke(p); // expiry may take one tick after the last duration tick
            h.assertFalse(GoldenApple.protectedFromStatus(p),"guard expires through native effect ticking");
            h.assertTrue(p.addEffect(new MobEffectInstance(MobEffects.WEAKNESS,100,0)),"negative effect accepted after expiry");
            Passthrough.LOG.info("Preview120 golden apple PASS: actual consume, full heal ACK/stale snapshot, 16 gold HP, 20 percent mitigation, status immunity, milk, expiry and ordinary-apple controls");
            h.succeed();
        }finally{Passthrough.events=previous;CombatBridge.clear();Passthrough.active=Passthrough.minecraftAuthority=false;}
    }
}
