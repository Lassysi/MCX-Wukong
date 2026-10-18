package dev.rehan.passthrough;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Optional;
import net.minecraft.core.registries.Registries;
import net.minecraft.network.chat.Component;
import net.minecraft.resources.ResourceKey;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.sounds.SoundEvent;
import net.minecraft.sounds.SoundEvents;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.item.Item;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.Items;
import net.minecraft.world.item.alchemy.PotionContents;
import net.minecraft.world.item.alchemy.Potions;
import net.minecraft.world.item.enchantment.EnchantmentHelper;
import net.minecraft.world.item.enchantment.EnchantmentInstance;
import net.minecraft.world.item.enchantment.Enchantments;
import net.minecraft.world.item.trading.ItemCost;
import net.minecraft.world.item.trading.Merchant;
import net.minecraft.world.item.trading.MerchantOffer;
import net.minecraft.world.item.trading.MerchantOffers;
import net.minecraft.world.level.storage.loot.LootParams;
import net.minecraft.world.level.storage.loot.parameters.LootContextParamSets;

/** Preview105 (user, chapter 2 shrine): Wukong's Will becomes emeralds; resting at a shrine repairs MC gear for
 *  emeralds; at an activated shrine, right-click with an empty hand or emeralds opens a villager-style trader whose
 *  offers grow with the chapter. Quest/story items stay native: only Will is converted (Phase E material table later).
 *  State comes from the host: "will" (Will gained), "shrine" (near an activated shrine), "shrine_rest", "chapter". */
public final class ShrineShop {
    private ShrineShop() {}
    public static volatile boolean nearShrine;
    public static volatile int shrineId,chapter=1;
    private static long willCarry;
    /** Preview106 (user: generous, let players worldwide finish happily): later chapters pay far more Will per kill,
     *  so the Will needed per emerald rises with the chapter. Balance config "shop.willPerEmeraldByChapter". */
    static double[] willPerEmerald={60,100,160,250,350,500};
    static double rate(){return willPerEmerald[Math.max(0,Math.min(willPerEmerald.length-1,chapter-1))];}

    private static void onServer(java.util.function.Consumer<ServerPlayer> work){
        MinecraftServer s=WorldBridge.server();if(s==null)return;
        s.execute(()->{for(var p:s.getPlayerList().getPlayers())if(p.isAlive())work.accept(p);});
    }
    private static void bar(ServerPlayer p,String text){p.displayClientMessage(Component.literal(text),true);}

    // ---------------------------------------------------------------- Will -> emeralds
    public static void will(long gain){
        if(gain<=0||gain>10_000_000)return;
        onServer(p->{
            // Live112: Looting on the main-hand weapon pays more emeralds (+25% per level).
            double loot=Enchants.lootingScale(p);double r=rate()/loot;long total=willCarry+gain;int emeralds=(int)Math.min(64*36,total/(long)r);
            willCarry=total-(long)(emeralds*r);
            if(emeralds<=0)return;
            give(p,new ItemStack(Items.EMERALD,emeralds));
            PickupToasts.push(new ItemStack(Items.EMERALD),emeralds);
            Passthrough.LOG.info("DQJ shop: Will +{} -> {} emeralds (carry {}, looting x{})",gain,emeralds,willCarry,loot);
        });
    }
    private static void give(ServerPlayer p,ItemStack stack){
        while(!stack.isEmpty()){
            var part=stack.split(Math.min(stack.getMaxStackSize(),stack.getCount()));
            if(!p.getInventory().add(part))p.drop(part,false);
        }
    }

    // ---------------------------------------------------------------- repair on rest
    /** Emerald cost per durability point by tier (points repaired per emerald). */
    static int pointsPerEmerald(ItemStack s){
        String id=net.minecraft.core.registries.BuiltInRegistries.ITEM.getKey(s.getItem()).getPath();
        if(id.contains("netherite")||s.is(Items.ELYTRA))return 80;
        if(id.contains("diamond")||s.is(Items.TRIDENT)||s.is(Items.MACE))return 100;
        if(id.contains("iron")||id.contains("chainmail")||id.contains("golden")||s.is(Items.CROSSBOW)||s.is(Items.SHIELD))return 200;
        return 400;
    }
    public static void rest(){
        onServer(p->{
            var inv=p.getInventory();List<ItemStack> damaged=new ArrayList<>();
            for(var list:List.of(inv.items,inv.armor,inv.offhand))for(var s:list)
                if(!s.isEmpty()&&s.isDamageableItem()&&s.getDamageValue()>0&&Balance.gun(s)==null)damaged.add(s);
            if(damaged.isEmpty())return;
            damaged.sort(Comparator.comparingDouble((ItemStack s)->(double)s.getDamageValue()/s.getMaxDamage()).reversed());
            int have=inv.countItem(Items.EMERALD),spent=0,repaired=0,partial=0;
            for(var s:damaged){
                int per=pointsPerEmerald(s),need=(s.getDamageValue()+per-1)/per,left=have-spent;
                if(left<=0)break;
                if(need<=left){s.setDamageValue(0);spent+=need;repaired++;}
                else{s.setDamageValue(Math.max(0,s.getDamageValue()-left*per));spent+=left;partial++;}
            }
            if(spent>0)inv.clearOrCountMatchingItems(st->st.is(Items.EMERALD),spent,p.inventoryMenu.getCraftSlots());
            int unpaid=damaged.size()-repaired-partial;
            bar(p,spent==0?"Gear is damaged - no emeralds to repair it":"Repaired "+repaired+" item"+(repaired==1?"":"s")
                +(partial>0?" ("+partial+" partly)":"")+(unpaid>0?", "+unpaid+" left damaged":"")+", -"+spent+" emerald"+(spent==1?"":"s"));
            Passthrough.LOG.info("DQJ shop: rest repair items={} full={} partial={} emeralds={}",damaged.size(),repaired,partial,spent);
        });
    }

    // ---------------------------------------------------------------- shrine enchanting
    /** Live111 (user: books from the trader could not be applied - no anvil, almost no XP): at an activated shrine,
     *  main hand item + enchanted book in the off hand + right-click applies the book for free. Anvil rules: equal levels
     *  of the same enchantment combine to the next level (up to the maximum), otherwise the higher level stays;
     *  incompatible enchantments are skipped. Guns are excluded (user: mod weapons don't need enchanting). */
    public static void enchant(){
        if(!nearShrine)return;
        onServer(p->{
            var item=p.getMainHandItem();var book=p.getOffhandItem();
            if(item.isEmpty()||!book.is(Items.ENCHANTED_BOOK))return;
            if(Balance.gun(item)!=null){bar(p,"Guns can't be enchanted");return;}
            var stored=book.get(net.minecraft.core.component.DataComponents.STORED_ENCHANTMENTS);
            if(stored==null||stored.isEmpty())return;
            var applied=new java.util.ArrayList<String>();var skipped=new java.util.ArrayList<String>();
            EnchantmentHelper.updateEnchantments(item,mutable->{
                for(var entry:stored.entrySet()){
                    var holder=entry.getKey();int lvl=entry.getIntValue();var e=holder.value();
                    String name=e.description().getString();
                    if(!e.canEnchant(item)){skipped.add(name+Enchants.fitsHint(e));continue;}
                    var others=new java.util.ArrayList<>(mutable.keySet());others.remove(holder);
                    if(!EnchantmentHelper.isEnchantmentCompatible(others,holder)){skipped.add(name);continue;}
                    int have=mutable.getLevel(holder);
                    int next=have==lvl?Math.min(lvl+1,e.getMaxLevel()):Math.max(have,lvl);
                    if(next<=have){skipped.add(name);continue;}
                    mutable.set(holder,next);applied.add(name+" "+next);
                }
            });
            if(applied.isEmpty()){bar(p,"This book can't improve that item"+(skipped.isEmpty()?"":" ("+String.join(", ",skipped)+")"));return;}
            book.shrink(1);
            p.serverLevel().playSound(null,p.blockPosition(),SoundEvents.ENCHANTMENT_TABLE_USE,net.minecraft.sounds.SoundSource.PLAYERS,1f,1f);
            bar(p,"Enchanted: "+String.join(", ",applied));
            Passthrough.LOG.info("DQJ shop: shrine enchant {} -> {} (skipped {})",item.getItem(),applied,skipped);
        });
    }

    // ---------------------------------------------------------------- trader
    public static void open(){
        if(!nearShrine)return;
        onServer(p->{
            if(p.containerMenu!=p.inventoryMenu)return;
            var merchant=new ShrineMerchant(offers(p,chapter));
            merchant.setTradingPlayer(p);
            merchant.openTradingScreen(p,Component.literal("Shrine Trader - chapter "+chapter),1);
            Passthrough.LOG.info("DQJ shop: trader opened at shrine {} chapter {} offers {}",shrineId,chapter,merchant.getOffers().size());
        });
    }
    private static final class ShrineMerchant implements Merchant {
        private MerchantOffers offers;private Player customer;
        ShrineMerchant(MerchantOffers offers){this.offers=offers;}
        @Override public void setTradingPlayer(Player p){customer=p;}
        @Override public Player getTradingPlayer(){return customer;}
        @Override public MerchantOffers getOffers(){return offers;}
        @Override public void overrideOffers(MerchantOffers o){offers=o;}
        @Override public void notifyTrade(MerchantOffer o){o.increaseUses();}
        @Override public void notifyTradeUpdated(ItemStack s){}
        @Override public int getVillagerXp(){return 0;}
        @Override public void overrideXp(int xp){}
        @Override public boolean showProgressBar(){return false;}
        @Override public SoundEvent getNotifyTradeSound(){return SoundEvents.VILLAGER_YES;}
        @Override public boolean isClientSide(){return false;}
        @Override public boolean stillValid(Player p){return customer==p;}
    }
    private static MerchantOffer offer(int price,ItemStack result,int maxUses){
        return new MerchantOffer(new ItemCost(Items.EMERALD,Math.min(64,price)),Optional.empty(),result,0,maxUses,0,0f);
    }
    private static ItemStack potion(Item kind,net.minecraft.core.Holder<net.minecraft.world.item.alchemy.Potion> p){return PotionContents.createItemStack(kind,p);}
    private static ItemStack book(ServerPlayer p,ResourceKey<net.minecraft.world.item.enchantment.Enchantment> key,int level){
        var holder=p.serverLevel().registryAccess().lookupOrThrow(Registries.ENCHANTMENT).getOrThrow(key);
        return EnchantmentHelper.createBook(new EnchantmentInstance(holder,level));
    }
    private static ItemStack arrows(int n,net.minecraft.core.Holder<net.minecraft.world.item.alchemy.Potion> p){var s=PotionContents.createItemStack(Items.TIPPED_ARROW,p);s.setCount(n);return s;}
    /** First item of a gun-mod loot table ("ammo/pistol_magazine", "guns/glock_17"), or empty if absent. */
    private static ItemStack gunLoot(ServerPlayer p,String path){
        try{
            var key=ResourceKey.create(Registries.LOOT_TABLE,ResourceLocation.fromNamespaceAndPath("modern_guns",path));
            var table=p.server.reloadableRegistries().getLootTable(key);
            var items=table.getRandomItems(new LootParams.Builder(p.serverLevel()).create(LootContextParamSets.EMPTY));
            return items.isEmpty()?ItemStack.EMPTY:items.get(0);
        }catch(RuntimeException e){return ItemStack.EMPTY;}
    }
    private static void add(MerchantOffers o,int price,ItemStack s,int maxUses){if(!s.isEmpty())o.add(offer(price,s,maxUses));}
    private static void add(MerchantOffers o,int price,ItemStack s){add(o,price,s,16);}
    private static ItemStack n(Item item,int count){return new ItemStack(item,count);}

    /** Offers by chapter. Items that only work on Wukong enemies after Phase D (slowness, weakness, knockback,
     *  cobweb) are left out until then, so nobody buys a dud. No Mending: shrine repair is the repair path. */
    /** Preview107 (user: add the missing mod weapons): every modern_guns gun except m202_flash (its model fails to
     *  load in 1.21.4). Price by power tier, unlocking by chapter; one or two per visit. */
    private static void guns(MerchantOffers o,ServerPlayer p,int ch){
        String[][] tiers={
            // chapter 1: pistols, SMGs, a double-barrel
            {"1","luger_p08:3","glock_17:4","colt_python:5","smith_and_wesson_model_29:5","desert_eagle:6","mp5:6","thompson_machine_gun:6","beretta_486:5"},
            // chapter 2: rifles, pump shotguns, a light flamethrower
            {"2","p90:7","remington_870:8","spas_12:9","austeyr_ef88:9","ak47:10","m4_carbine:10","fn_scar:11","m1a:12","weed_burner:6","m79:10","gl06:10"},
            // chapter 3: snipers, heavy weapons, launchers, flamethrowers
            {"3","winchester_model_70:12","barrett_m82:16","m134_minigun:14","m72_law:14","milkor_mgl:15","rpg_7:18","m2_flamethrower:12","xm42_flamethrower:14"}};
        for(var tier:tiers){
            if(ch<Integer.parseInt(tier[0]))continue;
            for(int i=1;i<tier.length;i++){var kv=tier[i].split(":");add(o,Integer.parseInt(kv[1]),gunLoot(p,"guns/"+kv[0]),2);}
        }
    }
    static MerchantOffers offers(ServerPlayer p,int ch){
        // Preview106 prices (user: generous; priced by usefulness/strength). Consumables restock every visit.
        var o=new MerchantOffers();int many=64;
        // healing and buffs
        add(o,2,potion(Items.POTION,Potions.HEALING),many);
        add(o,1,n(Items.COOKED_BEEF,16),many);
        add(o,2,potion(Items.POTION,Potions.SWIFTNESS),many);
        add(o,1,potion(Items.POTION,Potions.NIGHT_VISION),many);
        add(o,3,potion(Items.POTION,Potions.REGENERATION),many);
        add(o,4,n(Items.GOLDEN_APPLE,1),many);
        if(ch>=2){add(o,3,potion(Items.POTION,Potions.STRENGTH),many);add(o,3,potion(Items.POTION,Potions.FIRE_RESISTANCE),many);
            add(o,4,potion(Items.POTION,Potions.STRONG_HEALING),many);}
        if(ch>=3){add(o,5,potion(Items.POTION,Potions.TURTLE_MASTER),many);add(o,6,potion(Items.POTION,Potions.STRONG_STRENGTH),many);
            add(o,16,n(Items.TOTEM_OF_UNDYING,1),4);}
        // Potions & items D1 (user: enchanted golden apple, fairly expensive; full heal + resistance on eating)
        add(o,32,n(Items.ENCHANTED_GOLDEN_APPLE,1),2);
        add(o,2,potion(Items.POTION,Potions.SLOW_FALLING),many);
        add(o,1,n(Items.MILK_BUCKET,1),many);add(o,1,n(Items.HONEY_BOTTLE,2),many);
        // throwables against enemies (the damage path works today)
        add(o,3,potion(Items.SPLASH_POTION,Potions.POISON),many);
        add(o,3,potion(Items.SPLASH_POTION,Potions.HARMING),many);
        // D1: these now act on Wukong monsters (slowness = slower moves and attacks, weakness = weaker hits on you)
        add(o,3,potion(Items.SPLASH_POTION,Potions.SLOWNESS),many);add(o,3,potion(Items.SPLASH_POTION,Potions.WEAKNESS),many);
        add(o,2,n(Items.WIND_CHARGE,16),many);add(o,1,n(Items.SNOWBALL,16),many);add(o,2,n(Items.COBWEB,8),many);
        if(ch>=2){add(o,5,potion(Items.LINGERING_POTION,Potions.POISON),many);add(o,5,potion(Items.LINGERING_POTION,Potions.SLOWNESS),many);
            add(o,5,potion(Items.LINGERING_POTION,Potions.WEAKNESS),many);add(o,3,n(Items.POWDER_SNOW_BUCKET,1),many);
            add(o,2,arrows(16,Potions.SLOWNESS),many);add(o,2,arrows(16,Potions.WEAKNESS),many);}
        if(ch>=3)add(o,5,potion(Items.SPLASH_POTION,Potions.STRONG_HARMING),many);
        // ammo and explosives
        add(o,1,n(Items.ARROW,32),many);
        add(o,1,gunLoot(p,"ammo/pistol_magazine"),many);
        add(o,1,gunLoot(p,"ammo/shotgun_shell"),many);
        add(o,1,gunLoot(p,"ammo/revolver_bullets"),many);
        add(o,3,n(Items.TNT,8),many);
        add(o,2,n(Items.FIREWORK_ROCKET,32),many);add(o,3,n(Items.FIREWORK_ROCKET,64),many);
        add(o,6,n(Items.ELYTRA,1),2); // Preview107: always available, so a lost elytra can be replaced
        add(o,2,arrows(16,Potions.POISON),many);
        if(ch>=2){add(o,2,gunLoot(p,"ammo/assault_rifle_magazine"),many);add(o,2,gunLoot(p,"ammo/sniper_rifle_magazine"),many);}
        if(ch>=2)add(o,3,gunLoot(p,"ammo/explosive_rounds"),many);
        // weapons and armor, priced by strength
        add(o,3,n(Items.IRON_SWORD,1),8);add(o,3,n(Items.IRON_AXE,1),8);add(o,2,n(Items.IRON_PICKAXE,1),8);
        add(o,1,n(Items.SHIELD,1),8);add(o,2,n(Items.BOW,1),8);add(o,3,n(Items.CROSSBOW,1),8);
        add(o,4,n(Items.IRON_HELMET,1),4);add(o,6,n(Items.IRON_CHESTPLATE,1),4);add(o,5,n(Items.IRON_LEGGINGS,1),4);add(o,3,n(Items.IRON_BOOTS,1),4);
        if(ch>=2){add(o,8,n(Items.DIAMOND_PICKAXE,1),4);add(o,10,n(Items.DIAMOND_SWORD,1),4);add(o,8,n(Items.DIAMOND_AXE,1),4);
}
        if(ch>=3){add(o,10,n(Items.DIAMOND_HELMET,1),2);add(o,16,n(Items.DIAMOND_CHESTPLATE,1),2);add(o,14,n(Items.DIAMOND_LEGGINGS,1),2);
            add(o,10,n(Items.DIAMOND_BOOTS,1),2);add(o,14,n(Items.TRIDENT,1),2);
}
        if(ch>=4){add(o,20,n(Items.NETHERITE_UPGRADE_SMITHING_TEMPLATE,1),8);add(o,16,n(Items.NETHERITE_INGOT,1),8);}
        guns(o,p,ch);
        // enchanted books
        add(o,2,book(p,Enchantments.UNBREAKING,1),8);add(o,4,book(p,Enchantments.SHARPNESS,2),4);
        add(o,4,book(p,Enchantments.PROTECTION,2),4);add(o,4,book(p,Enchantments.POWER,2),4);
        // Live112: books whose effect is adapted to Wukong enemies (Enchants.java)
        add(o,3,book(p,Enchantments.SMITE,2),4);add(o,3,book(p,Enchantments.BANE_OF_ARTHROPODS,2),4);add(o,3,book(p,Enchantments.KNOCKBACK,1),4);
        add(o,3,book(p,Enchantments.PUNCH,1),4);add(o,3,book(p,Enchantments.EFFICIENCY,2),4);add(o,5,book(p,Enchantments.LOOTING,1),4);
        if(ch>=2){add(o,6,book(p,Enchantments.FIRE_ASPECT,1),4);add(o,6,book(p,Enchantments.UNBREAKING,3),4);add(o,5,book(p,Enchantments.FEATHER_FALLING,3),4);
            add(o,6,book(p,Enchantments.FLAME,1),4);add(o,5,book(p,Enchantments.SMITE,3),4);add(o,5,book(p,Enchantments.SWEEPING_EDGE,2),4);
            add(o,6,book(p,Enchantments.THORNS,1),4);add(o,8,book(p,Enchantments.LOOTING,2),4);add(o,5,book(p,Enchantments.EFFICIENCY,3),4);}
        if(ch>=3){add(o,10,book(p,Enchantments.SHARPNESS,4),4);add(o,10,book(p,Enchantments.PROTECTION,4),4);add(o,8,book(p,Enchantments.POWER,4),4);
            add(o,12,book(p,Enchantments.LOOTING,3),4);add(o,10,book(p,Enchantments.SMITE,5),4);add(o,10,book(p,Enchantments.EFFICIENCY,5),4);}
        // allies (spawn eggs are Steve's allies; a few per visit)
        add(o,4,n(Items.ZOMBIE_SPAWN_EGG,1),3);add(o,4,n(Items.SKELETON_SPAWN_EGG,1),3);add(o,3,n(Items.WOLF_SPAWN_EGG,1),3);
        if(ch>=2)add(o,5,n(Items.CREEPER_SPAWN_EGG,1),3);
        if(ch>=3)add(o,12,n(Items.IRON_GOLEM_SPAWN_EGG,1),1);
        // building and utility (a full stack of blocks for one emerald)
        add(o,1,n(Items.COBBLESTONE,64),many);add(o,1,n(Items.OAK_PLANKS,64),many);add(o,1,n(Items.STONE_BRICKS,64),many);
        add(o,1,n(Items.GLASS,64),many);add(o,1,n(Items.DIRT,64),many);
        add(o,1,n(Items.TORCH,32),many);add(o,1,n(Items.LADDER,32),many);add(o,1,n(Items.SCAFFOLDING,32),many);
        add(o,2,n(Items.ENDER_PEARL,4),many);add(o,1,n(Items.WATER_BUCKET,1),8);add(o,2,n(Items.LAVA_BUCKET,1),8);
        add(o,2,n(Items.MAGMA_BLOCK,16),many);add(o,1,n(Items.CAMPFIRE,2),many);add(o,2,n(Items.CHEST,4),many);
        return o;
    }
}
