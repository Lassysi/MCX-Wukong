package dev.rehan.passthrough;

import com.google.gson.GsonBuilder;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;
import net.minecraft.core.component.DataComponents;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.damagesource.DamageSource;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.ai.attributes.Attributes;
import net.minecraft.world.item.ItemStack;

/** Preview89 (user: balance armor tiers and weapons; explosives top tier, not absurd).
 *
 *  Outgoing (MC weapon -> Wukong enemy proxy), applied before the proxy takes the hit:
 *  - melee, bows, crossbows, potions, fire, lava: vanilla numbers (wood < stone < iron < diamond < netherite);
 *  - modern_guns: their bullets ignore MC's 0.5 s hit cooldown, so raw MP5 fire was ~100 damage/s (9x a diamond
 *    sword). Each gun gets a per-shot damage (table below), chosen for these sustained damage-per-second tiers:
 *    pistols 10-16, SMGs 15-18, rifles 18-22, minigun 28, sniper 20-36 (big single shots), shotguns per pellet;
 *  - explosions: TNT and launchers are the top tier per hit, capped (TNT 55, RPG 65, grenade launchers 45);
 *  - Boss-class enemies (proxy max health >= 400) take 60% from guns and explosions (melee/bow unchanged).
 *  Incoming (Wukong hit -> player): damage reduction from the worn armor's points and toughness,
 *  reduction = min(max, armor * perPoint + toughness * perToughness) (leather 19%, gold 30%, chain 33%,
 *  iron 41%, diamond 63%, netherite 67%; mod armor follows its attributes). It replaces vanilla's armor
 *  formula, which made low tiers almost useless against big Wukong hits; Protection enchantments and
 *  Resistance still apply on top, and worn armor still loses durability.
 *  All numbers live in config/dqj-balance.json (written with these defaults if missing, read at world load). */
public final class Balance {
    private Balance() {}
    private static final Map<String,Double> gunShot=new HashMap<>();
    private static double armorPerPoint=.0275,armorPerToughness=.01,armorMax=.75,bossScale=.6,bossHealth=400;
    private static double tntCap=55,creeperCap=30,launcherScale=1.5,launcherCap=65,grenadeScale=1.2,grenadeCap=45;
    private static boolean logged;
    // Preview105: durability wear probability (0..1) and Will needed per emerald.
    private static double wearRate=.2;
    public static double wearRate(){return wearRate;}
    // Preview107: hunger exhaustion scale, shield-block cost (before scaling), starvation damage on/off.
    private static double hungerScale=.1,shieldExhaustion=.05;private static boolean starvation=false;
    public static double hungerScale(){return hungerScale;}
    public static double shieldExhaustion(){return shieldExhaustion;}
    public static boolean starvation(){return starvation;}
    private static double regenDelaySeconds=60;private static int regenIntervalTicks=80;
    public static long regenDelayTicks(){return (long)(regenDelaySeconds*20);}
    public static int regenIntervalTicks(){return regenIntervalTicks;}
    public static boolean isGun(ItemStack stack){return gun(stack)!=null;}
    /** Live114: bow, crossbow, trident, fishing rod or gun in the main hand -> the off-hand shield stays down. */
    public static boolean rangedMain(net.minecraft.world.entity.player.Player p){
        var item=p.getMainHandItem();var i=item.getItem();
        return i instanceof net.minecraft.world.item.BowItem||i instanceof net.minecraft.world.item.CrossbowItem||i instanceof net.minecraft.world.item.TridentItem
            ||i instanceof net.minecraft.world.item.FishingRodItem||isGun(item);
    }
    static{defaults();}
    private static void defaults(){
        gunShot.clear();
        // pistols
        gunShot.put("glock_17",3.5);gunShot.put("luger_p08",3.0);gunShot.put("desert_eagle",6.0);gunShot.put("colt_python",6.5);
        gunShot.put("smith_and_wesson_model_29",5.0);
        // SMGs (fire every tick)
        gunShot.put("mp5",.8);gunShot.put("p90",.9);gunShot.put("thompson_machine_gun",.75);
        // rifles
        gunShot.put("ak47",4.0);gunShot.put("m4_carbine",4.0);gunShot.put("fn_scar",4.5);gunShot.put("austeyr_ef88",1.0);gunShot.put("m1a",16.0);
        // heavy / sniper
        gunShot.put("m134_minigun",1.4);gunShot.put("winchester_model_70",24.0);gunShot.put("barrett_m82",40.0);
        // shotguns (per pellet)
        gunShot.put("remington_870",4.0);gunShot.put("spas_12",4.5);gunShot.put("beretta_486",4.0);
        // launchers: direct-impact damage; their explosions use launcherScale/grenadeScale
        gunShot.put("rpg_7",6.0);gunShot.put("m72_law",6.0);gunShot.put("milkor_mgl",4.0);gunShot.put("m79",4.0);gunShot.put("gl06",4.0);
    }
    public static void load(Path configDir){
        Path file=configDir.resolve("dqj-balance.json");
        try{
            if(!Files.exists(file)){Files.createDirectories(configDir);Files.writeString(file,defaultsJson(),StandardCharsets.UTF_8);}
            JsonObject j=JsonParser.parseString(Files.readString(file,StandardCharsets.UTF_8)).getAsJsonObject();
            defaults();
            if(j.has("gunShotDamage"))for(var e:j.getAsJsonObject("gunShotDamage").entrySet())gunShot.put(e.getKey(),e.getValue().getAsDouble());
            var a=j.has("armor")?j.getAsJsonObject("armor"):new JsonObject();
            armorPerPoint=num(a,"perPoint",armorPerPoint);armorPerToughness=num(a,"perToughness",armorPerToughness);armorMax=num(a,"max",armorMax);
            var x=j.has("explosion")?j.getAsJsonObject("explosion"):new JsonObject();
            tntCap=num(x,"tntCap",tntCap);creeperCap=num(x,"creeperCap",creeperCap);launcherScale=num(x,"rocketScale",launcherScale);
            launcherCap=num(x,"rocketCap",launcherCap);grenadeScale=num(x,"grenadeScale",grenadeScale);grenadeCap=num(x,"grenadeCap",grenadeCap);
            var b=j.has("boss")?j.getAsJsonObject("boss"):new JsonObject();
            bossScale=num(b,"gunAndExplosionScale",bossScale);bossHealth=num(b,"minProxyHealth",bossHealth);
            var d=j.has("durability")?j.getAsJsonObject("durability"):new JsonObject();wearRate=Math.max(0,Math.min(1,num(d,"wearRate",wearRate)));
            var hu=j.has("hunger")?j.getAsJsonObject("hunger"):new JsonObject();hungerScale=Math.max(0,Math.min(1,num(hu,"exhaustionScale",hungerScale)));
            shieldExhaustion=Math.max(0,num(hu,"shieldBlockExhaustion",shieldExhaustion));starvation=hu.has("starvation")&&hu.get("starvation").getAsBoolean();
            regenDelaySeconds=Math.max(0,num(hu,"regenDelaySeconds",regenDelaySeconds));regenIntervalTicks=(int)Math.max(1,num(hu,"regenIntervalTicks",regenIntervalTicks));
            var sh=j.has("shop")?j.getAsJsonObject("shop"):new JsonObject();
            if(sh.has("willPerEmeraldByChapter")){var arr=sh.getAsJsonArray("willPerEmeraldByChapter");for(int i=0;i<Math.min(arr.size(),ShrineShop.willPerEmerald.length);i++)ShrineShop.willPerEmerald[i]=Math.max(1,arr.get(i).getAsDouble());}
            if(!j.has("durability")||!j.has("shop")||!j.has("hunger")||!j.getAsJsonObject("hunger").has("regenDelaySeconds")||!j.getAsJsonObject("shop").has("willPerEmeraldByChapter")){j.add("durability",dur());j.add("shop",shop());j.add("hunger",hunger());Files.writeString(file,new GsonBuilder().setPrettyPrinting().create().toJson(j),StandardCharsets.UTF_8);}
            Passthrough.LOG.info("DQJ balance loaded from {} ({} guns, wearRate {}, hunger x{}, Will/emerald {})",file,gunShot.size(),wearRate,hungerScale,java.util.Arrays.toString(ShrineShop.willPerEmerald));
        }catch(Exception e){defaults();Passthrough.LOG.warn("DQJ balance: {} unreadable, built-in defaults used: {}",file,e.toString());}
    }
    private static JsonObject hunger(){var h=new JsonObject();h.addProperty("exhaustionScale",hungerScale);h.addProperty("shieldBlockExhaustion",shieldExhaustion);h.addProperty("starvation",starvation);h.addProperty("regenDelaySeconds",regenDelaySeconds);h.addProperty("regenIntervalTicks",regenIntervalTicks);return h;}
    private static JsonObject dur(){var d=new JsonObject();d.addProperty("wearRate",wearRate);return d;}
    private static JsonObject shop(){var s=new JsonObject();var a=new com.google.gson.JsonArray();for(double v:ShrineShop.willPerEmerald)a.add(v);s.add("willPerEmeraldByChapter",a);return s;}
    private static double num(JsonObject o,String k,double d){return o.has(k)?o.get(k).getAsDouble():d;}
    private static String defaultsJson(){
        var j=new JsonObject();var g=new JsonObject();gunShot.forEach(g::addProperty);j.add("gunShotDamage",g);
        var a=new JsonObject();a.addProperty("perPoint",armorPerPoint);a.addProperty("perToughness",armorPerToughness);a.addProperty("max",armorMax);j.add("armor",a);
        var x=new JsonObject();x.addProperty("tntCap",tntCap);x.addProperty("creeperCap",creeperCap);x.addProperty("rocketScale",launcherScale);
        x.addProperty("rocketCap",launcherCap);x.addProperty("grenadeScale",grenadeScale);x.addProperty("grenadeCap",grenadeCap);j.add("explosion",x);
        var b=new JsonObject();b.addProperty("gunAndExplosionScale",bossScale);b.addProperty("minProxyHealth",bossHealth);j.add("boss",b);
        j.add("durability",dur());j.add("shop",shop());j.add("hunger",hunger());
        return new GsonBuilder().setPrettyPrinting().create().toJson(j);
    }
    /** modern_guns model name of a held gun ("mp5"), or null. */
    static String gun(ItemStack stack){
        var data=stack.get(DataComponents.CUSTOM_DATA);if(data==null)return null;
        var tag=data.copyTag();if(!tag.contains("gbg"))return null;
        String model=tag.getCompound("gbg").getString("idle_model");
        int slash=model.lastIndexOf('/');return slash<0?null:model.substring(slash+1);
    }
    private static double rawGun(ItemStack stack){
        var data=stack.get(DataComponents.CUSTOM_DATA);if(data==null)return 0;
        return data.copyTag().getCompound("gbg").getDouble("damage");
    }
    /** Outgoing damage to a Wukong enemy proxy. */
    public static float outgoing(LivingEntity target,DamageSource source,float amount){
        if(!(amount>0)||!Float.isFinite(amount))return amount;
        String type=source.typeHolder().unwrapKey().map(k->k.location().toString()).orElse("");
        boolean boss=target.getMaxHealth()>=bossHealth;
        ItemStack held=source.getEntity() instanceof ServerPlayer p?p.getMainHandItem():ItemStack.EMPTY;
        String gun=held.isEmpty()?null:gun(held);
        double result=amount;String rule="vanilla";
        if(type.contains("explosion")||type.equals("minecraft:fireworks")){
            boolean rocket="rpg_7".equals(gun)||"m72_law".equals(gun)||"m202_flash".equals(gun),grenade=gun!=null&&(gun.equals("milkor_mgl")||gun.equals("m79")||gun.equals("gl06"));
            boolean creeper=source.getDirectEntity()!=null&&source.getDirectEntity().getType()==net.minecraft.world.entity.EntityType.CREEPER;
            if(rocket){result=Math.min(amount*launcherScale,launcherCap);rule="rocket";}
            else if(grenade){result=Math.min(amount*grenadeScale,grenadeCap);rule="grenade";}
            else if(creeper){result=Math.min(amount,creeperCap);rule="creeper";}
            else{result=Math.min(amount,tntCap);rule="tnt";}
            if(boss)result*=bossScale;
        }else if(type.startsWith("gbg:")&&gun!=null){
            Double shot=gunShot.get(gun);double raw=rawGun(held);
            if(shot!=null&&raw>0){result=amount*shot/raw;rule="gun:"+gun;}
            if(boss)result*=bossScale;
        }
        if(!rule.equals("vanilla")&&!logged){logged=true;Passthrough.LOG.info("DQJ balance first scaled hit {} {} -> {} ({})",type,amount,result,rule);}
        return (float)result;
    }
    /** Fraction of a Wukong hit absorbed by the player's worn armor. */
    public static double armorReduction(ServerPlayer p){
        double armor=p.getArmorValue(),tough=p.getAttributeValue(Attributes.ARMOR_TOUGHNESS);
        return Math.max(0,Math.min(armorMax,armor*armorPerPoint+tough*armorPerToughness));
    }
}
