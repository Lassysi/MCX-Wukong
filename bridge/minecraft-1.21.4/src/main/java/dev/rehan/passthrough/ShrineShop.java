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
 *  offers include every equipment tier; prices follow strength. Quest/story items stay native: only Will is converted (Phase E material table later).
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
    /** Preview122 (user: resting must not auto-repair; repair is a shop option): rest only reminds the player. */
    public static void rest(){
        onServer(p->{
            int damaged=0;var inv=p.getInventory();
            for(var list:List.of(inv.items,inv.armor,inv.offhand))for(var s:list)if(!s.isEmpty()&&s.isDamageableItem()&&s.getDamageValue()>0&&Balance.gun(s)==null)damaged++;
            if(damaged>0)bar(p,damaged+" damaged item"+(damaged==1?"":"s")+" - repair at the Shrine Trader (right-click with an empty hand)");
        });
    }
    /** One repair offer per damaged vanilla item the player carries (guns never wear): emeralds + that exact item ->
     *  the same item with full durability, enchantments and name kept. Price by tier (pointsPerEmerald). Worn armor and
     *  the off-hand item must be moved into the inventory (or the trade slot) first. */
    static List<MerchantOffer> repairOffers(ServerPlayer p){
        var out=new ArrayList<MerchantOffer>();var inv=p.getInventory();var seen=new ArrayList<ItemStack>();
        for(var list:List.of(inv.armor,inv.offhand,inv.items))for(var s:list){
            if(s.isEmpty()||!s.isDamageableItem()||s.getDamageValue()<=0||Balance.gun(s)!=null)continue;
            if(seen.stream().anyMatch(x->ItemStack.isSameItemSameComponents(x,s)))continue;
            seen.add(s.copy());
            int per=pointsPerEmerald(s),price=Math.max(1,Math.min(64,(s.getDamageValue()+per-1)/per));
            var cost=new ItemCost(s.getItemHolder(),1,net.minecraft.core.component.DataComponentPredicate.allOf(s.getComponents()));
            var fixed=s.copyWithCount(1);fixed.setDamageValue(0);
            out.add(new MerchantOffer(new ItemCost(Items.EMERALD,price),Optional.of(cost),fixed,0,1,0,0f));
            if(out.size()>=12)break;
        }
        return out;
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
            var repairs=repairOffers(p);merchant.getOffers().addAll(0,repairs);
            if(!repairs.isEmpty())bar(p,"Repair offers are at the top: emeralds + the damaged item");
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
        return new MerchantOffer(new ItemCost(Items.EMERALD,Math.max(1,Math.min(64,price))),Optional.empty(),result,0,maxUses,0,0f);
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
            // Secret weapon tables are nested in this exact installed Modern Guns version.
            if(path.startsWith("guns/")&&java.util.Set.of("m72_law","weed_burner","m2_flamethrower","xm42_flamethrower").contains(path.substring(5)))path="guns/secret_guns/"+path.substring(5);
            var key=ResourceKey.create(Registries.LOOT_TABLE,ResourceLocation.fromNamespaceAndPath("modern_guns",path));
            var table=p.server.reloadableRegistries().getLootTable(key);
            var items=table.getRandomItems(new LootParams.Builder(p.serverLevel()).create(LootContextParamSets.EMPTY));
            return items.isEmpty()?ItemStack.EMPTY:items.get(0);
        }catch(RuntimeException e){return ItemStack.EMPTY;}
    }
    private static void add(MerchantOffers o,int price,ItemStack s,int maxUses){if(!s.isEmpty())o.add(offer(price,s,maxUses));}
    /** Preview121 (user: ammo feels missing): Modern Guns' loot tables give ONE ammo item. Shell/bullet/launcher guns
     *  load one item per shot (reload types 2 and 6, or a 1-round magazine), so one item per purchase was one shot. */
    private static ItemStack ammo(ServerPlayer p,String path,int count){var s=gunLoot(p,path);if(!s.isEmpty())s.setCount(Math.min(s.getMaxStackSize(),count));return s;}
    private static void add(MerchantOffers o,int price,ItemStack s){add(o,price,s,16);}
    private static ItemStack n(Item item,int count){return new ItemStack(item,count);}
    /** Preview126: building blocks say how many Wukong enemy hits they take (a Boss hit counts double). */
    static ItemStack wall(ItemStack s){
        if(!(s.getItem() instanceof net.minecraft.world.item.BlockItem b))return s;
        var state=b.getBlock().defaultBlockState();int hits=BlockToughness.hitsToBreak(state,false),boss=BlockToughness.hitsToBreak(state,true);
        String text="Holds "+hits+(hits==1?" enemy hit":" enemy hits")+", "+boss+(boss==1?" Boss hit":" Boss hits");
        var lore=s.getOrDefault(net.minecraft.core.component.DataComponents.LORE,net.minecraft.world.item.component.ItemLore.EMPTY);
        s.set(net.minecraft.core.component.DataComponents.LORE,lore.withLineAdded(Component.literal(text).withStyle(net.minecraft.ChatFormatting.GRAY)));
        return s;
    }

    /** Gun Core reloads by base item and custom-data tag, not by the display name. */
    private static ItemStack taggedAmmo(String key,String name,ResourceLocation model,int count){
        var s=n(Items.COMMAND_BLOCK,count);var tag=new net.minecraft.nbt.CompoundTag();tag.putByte(key,(byte)1);
        s.set(net.minecraft.core.component.DataComponents.CUSTOM_DATA,net.minecraft.world.item.component.CustomData.of(tag));
        s.set(net.minecraft.core.component.DataComponents.ITEM_NAME,Component.literal(name));
        s.set(net.minecraft.core.component.DataComponents.ITEM_MODEL,model);return s;
    }

    /** Preview121 (user: some bullets seem not to be on sale): every ammo type is followed directly by the guns that use
     *  it, and each ammo item lists those guns. m202_flash stays excluded (broken model). Reload facts (Gun Core): magazines
     *  refill the whole magazine per item; shells, revolver bullets, grenade rounds and rockets load one round per item. */
    private static final String[][] FIREARMS={
        // ammo loot path or #tagged key, name, count per purchase, price, then gun:price
        {"ammo/pistol_magazine","Pistol Magazine","8","1","luger_p08:3","glock_17:4","desert_eagle:6","mp5:6","thompson_machine_gun:6","p90:7"},
        {"ammo/revolver_bullets","Revolver Bullets","32","1","smith_and_wesson_model_29:5","colt_python:6"},
        {"ammo/shotgun_shell","Shotgun Shell","32","2","beretta_486:8","remington_870:12","spas_12:14"}, // Preview124: stronger, pricier
        {"ammo/assault_rifle_magazine","Assault Rifle Magazine","6","2","austeyr_ef88:9","ak47:10","m4_carbine:10","fn_scar:11"},
        {"ammo/sniper_rifle_magazine","Sniper Rifle Magazine","6","2","m1a:14","winchester_model_70:14","barrett_m82:20"},
        {"ammo/explosive_rounds","Explosive Rounds","12","5","m79:16","gl06:16","milkor_mgl:24"},
        {"#rocket","RPG Rocket","8","5","m72_law:20","rpg_7:26"},
        {"#fuel_tank","Flamethrower Fuel","8","2","weed_burner:6","m2_flamethrower:12","xm42_flamethrower:14"},
        {"#minigun_canister","Minigun Canister","6","2","m134_minigun:22"}}; // highest sustained damage
    private static void firearms(MerchantOffers o,ServerPlayer p){
        for(var row:FIREARMS){
            int count=Integer.parseInt(row[2]),price=Integer.parseInt(row[3]);
            var guns=new java.util.ArrayList<ItemStack>();var gunPrices=new java.util.ArrayList<Integer>();var names=new java.util.ArrayList<String>();
            for(int i=4;i<row.length;i++){
                var kv=row[i].split(":");var g=gunLoot(p,"guns/"+kv[0]);
                if(!g.isEmpty()){guns.add(g);gunPrices.add(Integer.parseInt(kv[1]));names.add(g.getHoverName().getString());}
            }
            ItemStack ammo=switch(row[0]){
                case "#rocket"->taggedAmmo("rocket",row[1],ResourceLocation.fromNamespaceAndPath("modern_guns","ammo/rocket"),count);
                case "#fuel_tank"->taggedAmmo("fuel_tank",row[1],ResourceLocation.withDefaultNamespace("fire_charge"),count); // the mod's fuel model is missing
                case "#minigun_canister"->taggedAmmo("minigun_canister",row[1],ResourceLocation.fromNamespaceAndPath("modern_guns","ammo/minigun_canister"),count);
                default->ammo(p,row[0],count);
            };
            if(!ammo.isEmpty()&&!names.isEmpty()){
                var lore=ammo.getOrDefault(net.minecraft.core.component.DataComponents.LORE,net.minecraft.world.item.component.ItemLore.EMPTY);
                ammo.set(net.minecraft.core.component.DataComponents.LORE,lore.withLineAdded(Component.literal("For: "+String.join(", ",names)).withStyle(net.minecraft.ChatFormatting.GOLD)));
                add(o,price,ammo,64);
            }
            for(int i=0;i<guns.size();i++)add(o,gunPrices.get(i),guns.get(i),2);
        }
    }
    static MerchantOffers offers(ServerPlayer p,int ch){
        // Preview121 full audit (user: nothing extra, nothing missing). Sections in this order; prices from Preview106/119.
        var o=new MerchantOffers();int many=64;
        // 1. Healing and food
        add(o,2,potion(Items.POTION,Potions.HEALING),many);add(o,4,potion(Items.POTION,Potions.STRONG_HEALING),many);
        add(o,3,potion(Items.POTION,Potions.REGENERATION),many);add(o,2,n(Items.GOLDEN_APPLE,32),many); // Preview124 (user): used a lot - half a stack for 2
        add(o,32,n(Items.ENCHANTED_GOLDEN_APPLE,1),2);add(o,20,n(Items.TOTEM_OF_UNDYING,1),4);
        add(o,1,n(Items.MILK_BUCKET,1),many);add(o,1,n(Items.HONEY_BOTTLE,2),many);
        add(o,1,n(Items.COOKED_BEEF,16),many);add(o,1,n(Items.BREAD,16),many);add(o,2,n(Items.GOLDEN_CARROT,16),many);
        // 2. Buff potions
        add(o,3,potion(Items.POTION,Potions.STRENGTH),many);add(o,6,potion(Items.POTION,Potions.STRONG_STRENGTH),many);
        add(o,2,potion(Items.POTION,Potions.SWIFTNESS),many);add(o,3,potion(Items.POTION,Potions.FIRE_RESISTANCE),many);
        add(o,5,potion(Items.POTION,Potions.TURTLE_MASTER),many);add(o,2,potion(Items.POTION,Potions.SLOW_FALLING),many);
        // 3. Melee weapons and tools, six quality tiers (wood -> netherite)
        // Preview124 (user: stronger = pricier): prices follow EquipmentBalance damage (sword 6..15, axe 8..19, pickaxe 3..8).
        String[] tiers={"wooden","stone","golden","iron","diamond","netherite"};
        String[] kinds={"sword","axe","pickaxe"};int[][] cost={{1,2,3,5,9,14},{1,2,4,6,11,17},{1,1,2,3,6,10}};
        for(int k=0;k<kinds.length;k++)for(int i=0;i<tiers.length;i++){
            var item=net.minecraft.core.registries.BuiltInRegistries.ITEM.getValue(ResourceLocation.withDefaultNamespace(tiers[i]+"_"+kinds[k]));
            add(o,cost[k][i],n(item,1),8);
        }
        add(o,20,n(Items.MACE,1),4);add(o,14,n(Items.TRIDENT,1),4);add(o,3,n(Items.SHIELD,1),8);
        // 4. Armor, six quality tiers (leather -> netherite), plus flight
        String[] armor={"leather","golden","chainmail","iron","diamond","netherite"};
        int[][] prices={{1,2,2,1},{2,4,3,2},{3,5,4,3},{4,6,5,3},{10,16,14,10},{18,28,24,18}};
        String[] slots={"helmet","chestplate","leggings","boots"};
        for(int i=0;i<armor.length;i++)for(int j=0;j<slots.length;j++){
            var item=net.minecraft.core.registries.BuiltInRegistries.ITEM.getValue(ResourceLocation.withDefaultNamespace(armor[i]+"_"+slots[j]));
            add(o,prices[i][j],n(item,1),4);
        }
        add(o,16,n(Items.ELYTRA,1),2);add(o,3,n(Items.FIREWORK_ROCKET,64),many);
        // 5. Bows and arrows
        add(o,2,n(Items.BOW,1),8);add(o,3,n(Items.CROSSBOW,1),8);add(o,1,n(Items.ARROW,32),many);
        add(o,2,arrows(16,Potions.SLOWNESS),many);add(o,2,arrows(16,Potions.WEAKNESS),many);
        add(o,2,arrows(16,Potions.POISON),many);add(o,3,arrows(16,Potions.HARMING),many);
        // 6. Firearms: each ammo, then the guns that use it
        firearms(o,p);
        // 7. Throwables and traps (all act on Wukong monsters)
        add(o,3,n(Items.TNT,8),many);
        add(o,3,potion(Items.SPLASH_POTION,Potions.HARMING),many);add(o,5,potion(Items.SPLASH_POTION,Potions.STRONG_HARMING),many);
        add(o,3,potion(Items.SPLASH_POTION,Potions.POISON),many);add(o,3,potion(Items.SPLASH_POTION,Potions.SLOWNESS),many);
        add(o,3,potion(Items.SPLASH_POTION,Potions.WEAKNESS),many);
        add(o,4,potion(Items.LINGERING_POTION,Potions.HARMING),many);add(o,5,potion(Items.LINGERING_POTION,Potions.POISON),many);
        add(o,5,potion(Items.LINGERING_POTION,Potions.SLOWNESS),many);add(o,5,potion(Items.LINGERING_POTION,Potions.WEAKNESS),many);
        add(o,2,n(Items.WIND_CHARGE,16),many);add(o,1,n(Items.SNOWBALL,16),many);add(o,2,n(Items.COBWEB,8),many);
        add(o,3,n(Items.POWDER_SNOW_BUCKET,1),many);add(o,2,n(Items.MAGMA_BLOCK,16),many);add(o,1,n(Items.CAMPFIRE,2),many);
        add(o,2,n(Items.LAVA_BUCKET,1),8);add(o,1,n(Items.FLINT_AND_STEEL,1),8);
        // capture: boats and minecarts hold small monsters for 8 s; the fishing rod pulls them
        add(o,12,n(Items.OAK_BOAT,1),4);add(o,15,n(Items.MINECART,1),4);add(o,1,n(Items.RAIL,16),many);add(o,6,n(Items.FISHING_ROD,1),4); // Preview124: reusable capture tools
        // 8. Enchanted books for free shrine enchanting: one per useful enchantment, at its top level
        add(o,16,book(p,Enchantments.SHARPNESS,5),4);add(o,10,book(p,Enchantments.SMITE,5),4);add(o,8,book(p,Enchantments.BANE_OF_ARTHROPODS,5),4);
        add(o,5,book(p,Enchantments.KNOCKBACK,2),4);add(o,8,book(p,Enchantments.FIRE_ASPECT,2),4);add(o,6,book(p,Enchantments.SWEEPING_EDGE,3),4);
        add(o,12,book(p,Enchantments.LOOTING,3),4);add(o,8,book(p,Enchantments.UNBREAKING,3),4);add(o,8,book(p,Enchantments.EFFICIENCY,5),4);
        add(o,14,book(p,Enchantments.POWER,5),4);add(o,5,book(p,Enchantments.PUNCH,2),4);add(o,8,book(p,Enchantments.FLAME,1),4);
        add(o,10,book(p,Enchantments.INFINITY,1),4);add(o,6,book(p,Enchantments.QUICK_CHARGE,3),4);add(o,6,book(p,Enchantments.MULTISHOT,1),4);
        add(o,6,book(p,Enchantments.PIERCING,4),4);add(o,6,book(p,Enchantments.LOYALTY,3),4);
        add(o,14,book(p,Enchantments.PROTECTION,4),4);add(o,6,book(p,Enchantments.FEATHER_FALLING,4),4);add(o,8,book(p,Enchantments.THORNS,3),4);
        // 9. Allies (spawn eggs fight for Steve) and leads
        add(o,4,n(Items.ZOMBIE_SPAWN_EGG,1),3);add(o,4,n(Items.SKELETON_SPAWN_EGG,1),3);add(o,3,n(Items.WOLF_SPAWN_EGG,1),3);
        add(o,5,n(Items.CREEPER_SPAWN_EGG,1),3);add(o,12,n(Items.IRON_GOLEM_SPAWN_EGG,1),1);add(o,1,n(Items.LEAD,4),many);
        // 10. Building and movement. Preview126: priced by how many Wukong enemy hits a block takes (BlockToughness):
        // soft blocks 64 for 1, stone-like 64 for 3, obsidian 16 for 5; the lore says how long each one holds.
        add(o,1,wall(n(Items.DIRT,64)),many);add(o,1,wall(n(Items.OAK_PLANKS,64)),many);add(o,1,wall(n(Items.GLASS,64)),many);
        add(o,1,wall(n(Items.OAK_STAIRS,32)),many);add(o,1,wall(n(Items.OAK_SLAB,32)),many);
        add(o,3,wall(n(Items.COBBLESTONE,64)),many);add(o,3,wall(n(Items.STONE_BRICKS,64)),many);add(o,5,wall(n(Items.OBSIDIAN,16)),many);
        add(o,1,n(Items.LADDER,32),many);add(o,1,n(Items.SCAFFOLDING,32),many);add(o,1,n(Items.TORCH,32),many);
        add(o,2,n(Items.CHEST,4),many);add(o,1,n(Items.WATER_BUCKET,1),8);add(o,1,n(Items.BUCKET,1),8);add(o,3,n(Items.ENDER_PEARL,4),many);
        return o;
    }
}
