package dev.rehan.passthrough;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import net.fabricmc.fabric.api.gametest.v1.FabricGameTest;
import net.minecraft.core.component.DataComponents;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.gametest.framework.GameTest;
import net.minecraft.gametest.framework.GameTestHelper;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.network.RegistryFriendlyByteBuf;
import net.minecraft.world.entity.EquipmentSlot;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.entity.ai.attributes.Attributes;
import net.minecraft.world.entity.projectile.Arrow;
import net.minecraft.world.entity.item.PrimedTnt;
import net.minecraft.world.item.Items;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.trading.MerchantOffers;
import net.minecraft.world.damagesource.DamageSource;
import net.minecraft.world.damagesource.DamageTypes;

/** Uses actual 1.21.4 entities, item components, trade codec and installed gun loot tables. No user save. */
public final class EquipmentShopTests implements FabricGameTest {
    private static ItemStack item(String name){return new ItemStack(BuiltInRegistries.ITEM.getValue(ResourceLocation.withDefaultNamespace(name)));}
    private static void close(GameTestHelper h,double actual,double expected,String name){h.assertTrue(Math.abs(actual-expected)<.001,name+" expected="+expected+" actual="+actual);}

    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=60)
    public void orderedDamageArmorAndTrade(GameTestHelper h)throws Exception{
        var p=h.makeMockServerPlayerInLevel();var level=h.getLevel();
        // Read generated defaults in this disposable test world, never the user's config.
        Balance.load(Files.createTempDirectory(Path.of("."),"balance-fixture-"));
        var target=EntityType.PIG.create(level,net.minecraft.world.entity.EntitySpawnReason.TRIGGERED);
        String[] tiers={"wooden","stone","golden","iron","diamond","netherite"};
        double[] swords={6,7.5,9,11,13,15},axes={8,10,12,14,16,19};
        int[] swordBase={4,5,4,6,7,8},axeBase={7,9,7,9,9,10};
        var melee=level.damageSources().playerAttack(p);
        for(String family:new String[]{"sword","axe","pickaxe","shovel","hoe"}){
            double previous=0;
            for(int i=0;i<tiers.length;i++){
                var stack=item(tiers[i]+"_"+family);p.setItemSlot(EquipmentSlot.MAINHAND,stack);
                float base=(float)(family.equals("sword")?swordBase[i]:family.equals("axe")?axeBase[i]:family.equals("pickaxe")?new double[]{2,3,2,4,5,6}[i]:family.equals("shovel")?new double[]{2.5,3.5,2.5,4.5,5.5,6.5}[i]:1);
                double damage=Balance.outgoing(target,melee,base);
                h.assertTrue(damage>previous,"strict material progression: "+tiers[i]+" "+family);previous=damage;
                if(family.equals("sword"))close(h,damage,swords[i],"sword curve");
                if(family.equals("axe"))close(h,damage,axes[i],"axe curve");
                close(h,Balance.outgoing(target,melee,base*1.5f),damage*1.5,"critical remains proportional");
                close(h,Balance.outgoing(target,melee,base*.25f),damage*.25,"partial attack cooldown remains proportional");
            }
        }
        String[] armor={"leather","golden","chainmail","iron","diamond","netherite"};double[] reduction={.25,.35,.45,.55,.70,.80};
        var slots=new EquipmentSlot[]{EquipmentSlot.HEAD,EquipmentSlot.CHEST,EquipmentSlot.LEGS,EquipmentSlot.FEET};
        String[] parts={"helmet","chestplate","leggings","boots"};
        for(int i=0;i<armor.length;i++){
            for(int j=0;j<4;j++)p.setItemSlot(slots[j],item(armor[i]+"_"+parts[j]));
            // Equipment attributes are updated by the normal living tick.
            p.tick();close(h,Balance.armorReduction(p),reduction[i],"armor full set "+armor[i]);
        }
        for(var slot:slots)p.setItemSlot(slot,ItemStack.EMPTY);p.tick();close(h,Balance.armorReduction(p),0,"unarmored");
        p.setItemSlot(EquipmentSlot.CHEST,item("diamond_chestplate"));p.setItemSlot(EquipmentSlot.FEET,item("iron_boots"));p.tick();
        close(h,Balance.armorReduction(p),.70*.4+.55*.15,"mixed armor weights");
        var bow=new Arrow(level,p,new ItemStack(Items.ARROW),new ItemStack(Items.BOW));
        p.setItemSlot(EquipmentSlot.MAINHAND,item("netherite_sword"));
        close(h,Balance.outgoing(target,level.damageSources().arrow(bow,p),10),13.5,"arrow uses launch weapon even after slot switch");
        var crossbow=new Arrow(level,p,new ItemStack(Items.ARROW),new ItemStack(Items.CROSSBOW));
        close(h,Balance.outgoing(target,level.damageSources().arrow(crossbow,p),10),15,"crossbow");
        var tnt=new PrimedTnt(level,0,0,0,p);var explosion=level.damageSources().explosion(tnt,p);
        close(h,Balance.outgoing(target,explosion,20),40,"TNT normal hit increased, not only cap");
        close(h,Balance.outgoing(target,explosion,1000),120,"TNT cap");
        var offers=ShrineShop.offers(p,1);h.assertTrue(offers.size()>140,"audited catalog size "+offers.size());
        var rows=new ArrayList<String>();var ammo=new java.util.HashSet<String>();var guns=new java.util.HashSet<String>();
        int gunCount=0;
        for(var offer:offers){
            var output=offer.getResult();h.assertFalse(output.isEmpty(),"no empty offer");
            h.assertTrue(output.getCount()<=output.getMaxStackSize(),"legal output stack");
            var data=output.get(DataComponents.CUSTOM_DATA);
            if(data!=null){var tag=data.copyTag();if(tag.contains("gbg")){
                gunCount++;
                var g=tag.getCompound("gbg");guns.add(g.getString("idle_model"));
                h.assertTrue(g.getDouble("damage")>0,"gun has raw damage");
                // Every gun must have an ammunition entry that passes the exact Gun Core reload predicate.
                boolean found=false;var predicate=new net.minecraft.nbt.CompoundTag();predicate.putByte(g.getString("ammo_item_tag"),(byte)1);
                for(var other:offers){var a=other.getResult().get(DataComponents.CUSTOM_DATA);if(a!=null&&other.getResult().is(BuiltInRegistries.ITEM.getValue(ResourceLocation.withDefaultNamespace(g.getString("ammo_base"))))&&net.minecraft.nbt.NbtUtils.compareNbt(predicate,a.copyTag(),true))found=true;}
                h.assertTrue(found,"matching reload ammo for "+g.getString("idle_model")+" tag="+g.getString("ammo_item_tag"));
            }else ammo.addAll(tag.getAllKeys());}
            var cost=offer.getCostA().copy();int before=cost.getCount();
            h.assertTrue(offer.take(cost,ItemStack.EMPTY),"offer can take valid emerald payment");close(h,cost.getCount(),0,"exact payment consumed");
            rows.add(output.getHoverName().getString()+"\t"+BuiltInRegistries.ITEM.getKey(output.getItem())+"\t"+output.getCount()+"\t"+before);
        }
        h.assertTrue(guns.size()>=23,"all ordinary gun models plus working secret weapons");
        // Preview121: per-shot ammo comes in usable quantities; removed items are gone.
        java.util.function.Function<String,Integer> count=key->offers.stream().map(o->o.getResult()).filter(r->{var c=r.get(DataComponents.CUSTOM_DATA);return c!=null&&c.copyTag().contains(key);}).mapToInt(ItemStack::getCount).max().orElse(0);
        h.assertTrue(count.apply("shotgun_shell")>=32&&count.apply("revolver_bullets")>=32,"shells and revolver bullets: 32 per purchase");
        h.assertTrue(count.apply("explosive_rounds")>=12&&count.apply("rocket")>=8,"launcher rounds 12, rockets 8 per purchase");
        h.assertTrue(count.apply("pistol_magazine")>=8&&count.apply("assault_rifle_magazine")>=6&&count.apply("sniper_rifle_magazine")>=6,"magazines in packs");
        for(var gone:new net.minecraft.world.item.Item[]{Items.CRAFTING_TABLE,Items.FURNACE,Items.ANVIL,Items.SMITHING_TABLE,Items.ENCHANTING_TABLE,Items.NETHERITE_UPGRADE_SMITHING_TEMPLATE,Items.NETHERITE_INGOT,Items.DIAMOND,Items.IRON_INGOT,Items.GOLD_INGOT,Items.EXPERIENCE_BOTTLE,Items.SHEARS,Items.NAME_TAG,Items.WOODEN_HOE,Items.NETHERITE_HOE,Items.IRON_SHOVEL,Items.COOKED_PORKCHOP,Items.BAKED_POTATO,Items.SAND,Items.OAK_FENCE,Items.COBBLESTONE_STAIRS,Items.COBBLESTONE_SLAB})
            {var g=gone;h.assertFalse(offers.stream().anyMatch(o->o.getResult().is(g)),"removed from shop: "+BuiltInRegistries.ITEM.getKey(g));}
        for(var need:new net.minecraft.world.item.Item[]{Items.OAK_BOAT,Items.MINECART,Items.RAIL,Items.FISHING_ROD,Items.ENCHANTED_GOLDEN_APPLE,Items.BOW,Items.ARROW})
            {var g=need;h.assertTrue(offers.stream().anyMatch(o->o.getResult().is(g)),"sold: "+BuiltInRegistries.ITEM.getKey(g));}
        // Each gun comes right after its own ammo (or after another gun of the same ammo), and that ammo names it.
        String currentAmmo=null;String currentLore="";int checkedGuns=0;
        for(var offer:offers){
            var r=offer.getResult();var c=r.get(DataComponents.CUSTOM_DATA);if(c==null)continue;var tag=c.copyTag();
            if(tag.contains("gbg")){
                var g=tag.getCompound("gbg");h.assertTrue(g.getString("ammo_item_tag").equals(currentAmmo),"gun "+r.getHoverName().getString()+" follows its ammo "+g.getString("ammo_item_tag")+" (preceding ammo "+currentAmmo+")");
                h.assertTrue(currentLore.contains(r.getHoverName().getString()),"ammo lore names "+r.getHoverName().getString());checkedGuns++;
            }else if(r.is(Items.COMMAND_BLOCK)){
                currentAmmo=null;for(var k:tag.getAllKeys())currentAmmo=k;
                var lore=r.get(DataComponents.LORE);currentLore=lore==null?"":lore.lines().stream().map(x->x.getString()).reduce("",(x,y)->x+"|"+y);
            }else currentAmmo=null;
        }
        h.assertTrue(checkedGuns>=23,"all sold guns checked for grouping: "+checkedGuns);
        // No duplicate offers (same item, components and count).
        for(int i=0;i<offers.size();i++)for(int j=i+1;j<offers.size();j++)
            h.assertFalse(ItemStack.matches(offers.get(i).getResult(),offers.get(j).getResult()),"duplicate offer: "+offers.get(i).getResult().getHoverName().getString());
        close(h,gunCount,27,"all 27 supported gun entries");
        h.assertTrue(ammo.contains("rocket")&&ammo.contains("fuel_tank"),"rocket and fuel are sold");
        for(String material:armor)for(String part:parts)h.assertTrue(offers.stream().anyMatch(o->o.getResult().is(item(material+"_"+part).getItem())),"full set available "+material+" "+part);
        var buffer=new RegistryFriendlyByteBuf(io.netty.buffer.Unpooled.buffer(),level.registryAccess());
        MerchantOffers.STREAM_CODEC.encode(buffer,offers);var decoded=MerchantOffers.STREAM_CODEC.decode(buffer);buffer.release();
        close(h,decoded.size(),offers.size(),"complete catalog network round trip");
        h.assertTrue(ItemStack.matches(decoded.getLast().getResult(),offers.getLast().getResult()),"last offer preserved");
        var bullet=new DamageSource(level.registryAccess().lookupOrThrow(net.minecraft.core.registries.Registries.DAMAGE_TYPE).getOrThrow(net.minecraft.resources.ResourceKey.create(net.minecraft.core.registries.Registries.DAMAGE_TYPE,ResourceLocation.fromNamespaceAndPath("gbg","bullet_standard"))),p);
        var ak=offers.stream().map(o->o.getResult()).filter(s->"ak47".equals(Balance.gun(s))).findFirst().orElseThrow();
        p.setItemSlot(EquipmentSlot.MAINHAND,ak);close(h,Balance.outgoing(target,bullet,5),6,"AK per-shot damage");
        var barrett=offers.stream().map(o->o.getResult()).filter(s->"barrett_m82".equals(Balance.gun(s))).findFirst().orElseThrow();
        p.setItemSlot(EquipmentSlot.MAINHAND,barrett);close(h,Balance.outgoing(target,bullet,38),60,"sniper per-shot damage");
        var nativeRocket=EntityType.CREEPER.create(level,net.minecraft.world.entity.EntitySpawnReason.TRIGGERED);
        nativeRocket.addTag("gbz.zombie");nativeRocket.addTag("zombie");nativeRocket.setInvulnerable(true);nativeRocket.setSilent(true);nativeRocket.getAttribute(Attributes.SCALE).setBaseValue(.01);
        var rocket=level.damageSources().explosion(nativeRocket,nativeRocket);
        p.setItemSlot(EquipmentSlot.MAINHAND,item("diamond_sword"));
        close(h,Balance.outgoing(target,rocket,40),140,"real Gun Core blast is not clamped as an ordinary creeper (Preview124 x3.5)");
        close(h,Balance.outgoing(target,rocket,1000),260,"real Gun Core blast cap (Preview124)");
        nativeRocket.getTags().clear();close(h,Balance.outgoing(target,rocket,40),30,"ordinary creeper keeps its own cap");
        nativeRocket.addTag("gbz.zombie");nativeRocket.addTag("zombie");
        target.getAttribute(Attributes.MAX_HEALTH).setBaseValue(500);
        close(h,Balance.outgoing(target,rocket,1000),208,"boss rocket cap after 80 percent retention (260 x 0.8)");
        close(h,Balance.outgoing(target,explosion,1000),96,"boss TNT cap");
        p.setItemSlot(EquipmentSlot.MAINHAND,ak);close(h,Balance.outgoing(target,bullet,5),4.8,"boss gun retention");
        // The mixin scales proxy hits only; ordinary mobs and the player retain vanilla outgoing rules.
        target.getAttribute(Attributes.MAX_HEALTH).setBaseValue(100);target.setHealth(100);
        p.setItemSlot(EquipmentSlot.MAINHAND,item("netherite_sword"));
        target.hurtServer(level,melee,8);close(h,target.getHealth(),92,"non-proxy hit stays vanilla");
        target.invulnerableTime=0;target.setHealth(100);
        var field=CombatBridge.class.getDeclaredField("knownProxyIds");field.setAccessible(true);
        ((java.util.Set<java.util.UUID>)field.get(null)).add(target.getUUID());
        try{target.hurtServer(level,melee,8);close(h,target.getHealth(),85,"real proxy mixin consumes scaled melee");}
        finally{((java.util.Set<java.util.UUID>)field.get(null)).remove(target.getUUID());}
        Files.createDirectories(Path.of("reports"));Files.write(Path.of("reports/shop.tsv"),rows);
        Passthrough.LOG.info("Preview119 equipment/shop PASS: {} offers, {} gun model ids, ammo tags {}",offers.size(),guns.size(),ammo);
        h.succeed();
    }

    /** Preview122: shop repair offers, shields that never break, vanilla wear/stamina defaults and old-config migration. */
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=60)
    public void repairWearAndStamina(GameTestHelper h)throws Exception{
        var level=h.getLevel();var p=h.makeMockServerPlayerInLevel();p.setGameMode(net.minecraft.world.level.GameType.SURVIVAL);
        p.getInventory().clearContent();
        var sword=new ItemStack(Items.DIAMOND_SWORD);
        var sharp=level.registryAccess().lookupOrThrow(net.minecraft.core.registries.Registries.ENCHANTMENT).getOrThrow(net.minecraft.world.item.enchantment.Enchantments.SHARPNESS);
        sword.enchant(sharp,3);sword.setDamageValue(800);p.getInventory().add(sword);
        sword=p.getInventory().items.stream().filter(x->x.is(Items.DIAMOND_SWORD)).findFirst().orElseThrow(); // add() moves the stack
        var gun=ShrineShop.offers(p,1).stream().map(o->o.getResult()).filter(Balance::isGun).findFirst().orElseThrow().copy();
        p.getInventory().add(gun);
        var repairs=ShrineShop.repairOffers(p);
        h.assertTrue(repairs.size()==1,"one repair offer for the damaged sword, none for guns: "+repairs.size());
        var offer=repairs.get(0);
        h.assertTrue(offer.getResult().is(Items.DIAMOND_SWORD)&&offer.getResult().getDamageValue()==0,"repair result is the same sword at full durability");
        h.assertTrue(net.minecraft.world.item.enchantment.EnchantmentHelper.getItemEnchantmentLevel(sharp,offer.getResult())==3,"enchantments kept");
        h.assertTrue(offer.getCostB().is(Items.DIAMOND_SWORD)&&offer.getItemCostB().orElseThrow().test(sword),"the damaged sword itself is the second cost");
        var fresh=new ItemStack(Items.DIAMOND_SWORD);h.assertFalse(offer.getItemCostB().orElseThrow().test(fresh),"another (undamaged) sword does not satisfy the repair cost");
        h.assertTrue(offer.getCostA().is(Items.EMERALD)&&offer.getCostA().getCount()==8,"diamond tier: 800 points / 100 per emerald = 8 emeralds");
        // shields stop at 1 durability
        var shield=new ItemStack(Items.SHIELD);p.setItemSlot(net.minecraft.world.entity.EquipmentSlot.OFFHAND,shield);
        shield.hurtAndBreak(shield.getMaxDamage()+50,p,net.minecraft.world.entity.EquipmentSlot.OFFHAND);
        h.assertTrue(!p.getOffhandItem().isEmpty()&&p.getOffhandItem().getDamageValue()==p.getOffhandItem().getMaxDamage()-1,"shield never breaks, stops at 1 durability");
        // vanilla wear for swords
        var worn=new ItemStack(Items.IRON_SWORD);p.setItemSlot(net.minecraft.world.entity.EquipmentSlot.MAINHAND,worn);
        for(int i=0;i<10;i++)worn.hurtAndBreak(1,p,net.minecraft.world.entity.EquipmentSlot.MAINHAND);
        h.assertTrue(p.getMainHandItem().getDamageValue()==10,"vanilla wear: 10 hits = 10 durability (was ~2)");
        h.assertTrue(Balance.attackExhaustion()>=.3&&Balance.shieldExhaustion()>=1&&Balance.hungerScale()==1&&Balance.wornShieldDamage()==.5,"stamina defaults");
        // old configs migrate once, other settings kept
        var dir=java.nio.file.Files.createTempDirectory("dqj-balance");
        java.nio.file.Files.writeString(dir.resolve("dqj-balance.json"),"{\"durability\":{\"wearRate\":0.2},\"hunger\":{\"exhaustionScale\":0.1,\"shieldBlockExhaustion\":0.05,\"starvation\":false,\"regenDelaySeconds\":45,\"regenIntervalTicks\":80},\"shop\":{\"willPerEmeraldByChapter\":[60,100,160,250,350,500]}}");
        Balance.load(dir);
        h.assertTrue(Balance.wearRate()==1&&Balance.hungerScale()==1&&Balance.shieldExhaustion()==1,"pre-Preview122 slow values replaced");
        h.assertTrue(Balance.regenDelayTicks()==45*20,"unrelated settings (regen delay) kept");
        var saved=java.nio.file.Files.readString(dir.resolve("dqj-balance.json"));
        h.assertTrue(saved.contains("attackExhaustion"),"migrated config written once");
        java.nio.file.Files.writeString(dir.resolve("dqj-balance.json"),saved.replace("\"wearRate\": 1.0","\"wearRate\": 0.5"));
        Balance.load(dir);h.assertTrue(Balance.wearRate()==.5,"after migration the user's own edits are respected");
        // Preview124: stronger shotguns/launchers migrate once; prices rose with them.
        var saved124=java.nio.file.Files.readString(dir.resolve("dqj-balance.json"));
        h.assertTrue(saved124.contains("\"balanceRevision\": 124")&&saved124.contains("\"spas_12\": 10.0")&&saved124.contains("\"rocketScale\": 3.5"),"revision 124 written: Spas 10, launcher x3.5");
        var shop124=ShrineShop.offers(p,1);
        java.util.function.BiFunction<String,Integer,Boolean> priced=(key,price)->shop124.stream().anyMatch(o->{var c=o.getResult().get(DataComponents.CUSTOM_DATA);return c!=null&&c.copyTag().contains(key)&&o.getCostA().getCount()==price;});
        h.assertTrue(priced.apply("shotgun_shell",2)&&priced.apply("explosive_rounds",5)&&priced.apply("rocket",5),"shells 2, explosive rounds 5, rockets 5 emeralds");
        h.assertTrue(shop124.stream().anyMatch(o->"rpg_7".equals(Balance.gun(o.getResult()))&&o.getCostA().getCount()==26),"RPG-7 costs 26");
        java.util.function.BiFunction<net.minecraft.world.item.Item,Integer,Integer> cost=(item,count)->shop124.stream().filter(o->o.getResult().is(item)&&o.getResult().getCount()==count).mapToInt(o->o.getCostA().getCount()).findFirst().orElse(-1);
        h.assertTrue(cost.apply(Items.GOLDEN_APPLE,32)==2,"32 golden apples for 2");
        h.assertTrue(cost.apply(Items.OAK_BOAT,1)==12&&cost.apply(Items.MINECART,1)==15,"boat 12, minecart 15");
        // stronger = pricier: every tier of every family costs at least the tier below, netherite > diamond > iron
        for(String kind:new String[]{"sword","axe","pickaxe"}){int prev=0;for(String tier:new String[]{"wooden","stone","golden","iron","diamond","netherite"}){
            int c=cost.apply(net.minecraft.core.registries.BuiltInRegistries.ITEM.getValue(ResourceLocation.withDefaultNamespace(tier+"_"+kind)),1);
            h.assertTrue(c>=prev&&c>0,kind+" tier "+tier+" price "+c+" >= "+prev);prev=c;}}
        h.assertTrue(cost.apply(Items.NETHERITE_AXE,1)>cost.apply(Items.NETHERITE_SWORD,1)&&cost.apply(Items.NETHERITE_SWORD,1)>cost.apply(Items.NETHERITE_PICKAXE,1),"axe > sword > pickaxe at the same tier");
        Balance.load(java.nio.file.Files.createTempDirectory("dqj-balance-defaults")); // leave shared balance state at the defaults
        Passthrough.LOG.info("Preview122 repair/wear/stamina PASS: repair offer keeps enchantments, exact item cost, tier price, shield stops at 1, vanilla wear, migration");
        h.succeed();
    }
}
