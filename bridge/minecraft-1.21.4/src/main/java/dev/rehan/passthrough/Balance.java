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

/** Preview119: stronger bridge weapons and explosives; ordered full armor sets.
 * Vanilla item attributes/saves are unchanged. Damage scaling is applied only at the enemy proxy hit boundary.
 * Attack cooldowns, criticals and enchantments remain proportional. See EquipmentBalance for material curves.
 * Config is read at world load. Existing installations must also migrate their old numeric overrides. */
public final class Balance {
    private Balance() {}
    private static final Map<String,Double> gunShot=new HashMap<>();
    private static double armorPerPoint=.0275,armorPerToughness=.01,armorMax=.75,bossScale=.6,bossHealth=400;
    private static double tntScale=2,tntCap=120,creeperCap=30,launcherScale=2.5,launcherCap=180,grenadeScale=1.8,grenadeCap=100;
    private static double meleeScale=1,bowScale=1.35,crossbowScale=1.5,tridentScale=1.5,magicScale=1.25,fireScale=1.25;
    private static boolean logged;
    // Preview105: durability wear probability (0..1) and Will needed per emerald.
    private static double wearRate=.2;
    public static double wearRate(){return wearRate;}
    // Preview107: hunger exhaustion scale, shield-block cost (before scaling), starvation damage on/off.
    // Preview122 (user: durability and hunger barely drain; attacks and blocks must cost stamina so a Boss fight nearly
    // empties the hunger bar): vanilla wear and exhaustion, +0.3 per landed melee hit (vanilla 0.1 -> 0.4), 1.0 per
    // shield block. A worn-out shield (1 durability left, it never breaks) still blocks but lets wornShieldDamage through.
    private static double hungerScale=1,shieldExhaustion=1;private static boolean starvation=false;
    private static double attackExhaustion=.3,wornShieldDamage=.5;
    public static double attackExhaustion(){return attackExhaustion;}
    public static double wornShieldDamage(){return wornShieldDamage;}
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
        armorPerPoint=.0275;armorPerToughness=.01;armorMax=.85;bossScale=.8;bossHealth=400;
        // Preview124 (user: shotguns and launchers hit too weakly): launcher blasts x3.5 (cap 260), other grenades x3 (cap 160).
        tntScale=2;tntCap=120;creeperCap=30;launcherScale=3.5;launcherCap=260;grenadeScale=3;grenadeCap=160;
        meleeScale=1;bowScale=1.35;crossbowScale=1.5;tridentScale=1.5;magicScale=1.25;fireScale=1.25;
        wearRate=1;hungerScale=1;shieldExhaustion=1;attackExhaustion=.3;wornShieldDamage=.5;starvation=false;regenDelaySeconds=60;regenIntervalTicks=80;
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
        gunShot.replaceAll((key,value)->value*1.5);
        gunShot.put("rpg_7",12.0);gunShot.put("m72_law",12.0);
        applyRevision124();
    }
    /** Preview124 values: shotgun pellets x1.5 (Beretta/Remington 9, Spas 10), grenade-launcher impacts 9, rocket impacts 16. */
    private static final int REVISION=124;
    private static void applyRevision124(){
        gunShot.put("beretta_486",9.0);gunShot.put("remington_870",9.0);gunShot.put("spas_12",10.0);
        gunShot.put("m79",9.0);gunShot.put("gl06",9.0);gunShot.put("milkor_mgl",9.0);gunShot.put("rpg_7",16.0);gunShot.put("m72_law",16.0);
        launcherScale=3.5;launcherCap=260;grenadeScale=3;grenadeCap=160;
    }
    public static void load(Path configDir){
        Path file=configDir.resolve("dqj-balance.json");
        try{
            if(!Files.exists(file)){Files.createDirectories(configDir);Files.writeString(file,defaultsJson(),StandardCharsets.UTF_8);}
            JsonObject j=JsonParser.parseString(Files.readString(file,StandardCharsets.UTF_8)).getAsJsonObject();
            defaults();
            if(j.has("gunShotDamage"))for(var e:j.getAsJsonObject("gunShotDamage").entrySet())gunShot.put(e.getKey(),num(j.getAsJsonObject("gunShotDamage"),e.getKey(),gunShot.getOrDefault(e.getKey(),1.0)));
            var a=j.has("armor")?j.getAsJsonObject("armor"):new JsonObject();
            armorPerPoint=num(a,"perPoint",armorPerPoint);armorPerToughness=num(a,"perToughness",armorPerToughness);armorMax=num(a,"max",armorMax);
            var x=j.has("explosion")?j.getAsJsonObject("explosion"):new JsonObject();
            tntScale=num(x,"tntScale",tntScale);tntCap=num(x,"tntCap",tntCap);creeperCap=num(x,"creeperCap",creeperCap);launcherScale=num(x,"rocketScale",launcherScale);
            launcherCap=num(x,"rocketCap",launcherCap);grenadeScale=num(x,"grenadeScale",grenadeScale);grenadeCap=num(x,"grenadeCap",grenadeCap);
            var b=j.has("boss")?j.getAsJsonObject("boss"):new JsonObject();
            bossScale=num(b,"gunAndExplosionScale",bossScale);bossHealth=num(b,"minProxyHealth",bossHealth);
            var w=j.has("weapons")?j.getAsJsonObject("weapons"):new JsonObject();
            meleeScale=num(w,"meleeScale",meleeScale);bowScale=num(w,"bowScale",bowScale);crossbowScale=num(w,"crossbowScale",crossbowScale);
            tridentScale=num(w,"tridentScale",tridentScale);magicScale=num(w,"magicScale",magicScale);fireScale=num(w,"fireScale",fireScale);
            var d=j.has("durability")?j.getAsJsonObject("durability"):new JsonObject();wearRate=Math.max(0,Math.min(1,num(d,"wearRate",wearRate)));
            var hu=j.has("hunger")?j.getAsJsonObject("hunger"):new JsonObject();hungerScale=Math.max(0,Math.min(1,num(hu,"exhaustionScale",hungerScale)));
            shieldExhaustion=Math.max(0,num(hu,"shieldBlockExhaustion",shieldExhaustion));starvation=hu.has("starvation")&&hu.get("starvation").getAsBoolean();
            regenDelaySeconds=Math.max(0,num(hu,"regenDelaySeconds",regenDelaySeconds));regenIntervalTicks=(int)Math.max(1,num(hu,"regenIntervalTicks",regenIntervalTicks));
            attackExhaustion=Math.max(0,num(hu,"attackExhaustion",attackExhaustion));wornShieldDamage=Math.max(0,Math.min(1,num(d,"wornShieldDamage",wornShieldDamage)));
            // Preview122: configs written before this version carry the old slow values (wear 0.2, hunger x0.1, block 0.05);
            // they are replaced once by the new defaults (marker: hunger.attackExhaustion). Other settings are kept.
            boolean migrate=!hu.has("attackExhaustion");
            if(migrate){wearRate=1;hungerScale=1;shieldExhaustion=1;attackExhaustion=.3;wornShieldDamage=.5;Passthrough.LOG.info("DQJ balance: pre-Preview122 durability/hunger values replaced by vanilla wear and stamina costs");}
            var sh=j.has("shop")?j.getAsJsonObject("shop"):new JsonObject();
            if(sh.has("willPerEmeraldByChapter")){var arr=sh.getAsJsonArray("willPerEmeraldByChapter");for(int i=0;i<Math.min(arr.size(),ShrineShop.willPerEmerald.length);i++)ShrineShop.willPerEmerald[i]=Math.max(1,arr.get(i).getAsDouble());}
            // Preview124: configs from before this revision get the stronger shotgun/launcher values once; other edits are kept.
            int revision=j.has("balanceRevision")?j.get("balanceRevision").getAsInt():0;
            if(revision<REVISION){
                applyRevision124();
                var g=j.has("gunShotDamage")?j.getAsJsonObject("gunShotDamage"):new JsonObject();
                for(String k:new String[]{"beretta_486","remington_870","spas_12","m79","gl06","milkor_mgl","rpg_7","m72_law"})g.addProperty(k,gunShot.get(k));
                j.add("gunShotDamage",g);
                var ex=j.has("explosion")?j.getAsJsonObject("explosion"):new JsonObject();
                ex.addProperty("rocketScale",launcherScale);ex.addProperty("rocketCap",launcherCap);ex.addProperty("grenadeScale",grenadeScale);ex.addProperty("grenadeCap",grenadeCap);
                j.add("explosion",ex);j.addProperty("balanceRevision",REVISION);
                Files.writeString(file,new GsonBuilder().setPrettyPrinting().create().toJson(j),StandardCharsets.UTF_8);
                Passthrough.LOG.info("DQJ balance: revision {} applied (stronger shotguns and launchers)",REVISION);
            }
            if(migrate||!j.has("durability")||!j.has("shop")||!j.has("hunger")||!j.getAsJsonObject("hunger").has("regenDelaySeconds")||!j.getAsJsonObject("shop").has("willPerEmeraldByChapter")){j.add("durability",dur());j.add("shop",shop());j.add("hunger",hunger());Files.writeString(file,new GsonBuilder().setPrettyPrinting().create().toJson(j),StandardCharsets.UTF_8);}
            Passthrough.LOG.info("DQJ balance loaded from {} ({} guns, wearRate {}, hunger x{}, Will/emerald {})",file,gunShot.size(),wearRate,hungerScale,java.util.Arrays.toString(ShrineShop.willPerEmerald));
        }catch(Exception e){defaults();Passthrough.LOG.warn("DQJ balance: {} unreadable, built-in defaults used: {}",file,e.toString());}
    }
    private static JsonObject hunger(){var h=new JsonObject();h.addProperty("exhaustionScale",hungerScale);h.addProperty("shieldBlockExhaustion",shieldExhaustion);h.addProperty("starvation",starvation);h.addProperty("regenDelaySeconds",regenDelaySeconds);h.addProperty("regenIntervalTicks",regenIntervalTicks);h.addProperty("attackExhaustion",attackExhaustion);return h;}
    private static JsonObject dur(){var d=new JsonObject();d.addProperty("wearRate",wearRate);d.addProperty("wornShieldDamage",wornShieldDamage);return d;}
    private static JsonObject shop(){var s=new JsonObject();var a=new com.google.gson.JsonArray();for(double v:ShrineShop.willPerEmerald)a.add(v);s.add("willPerEmeraldByChapter",a);return s;}
    private static double num(JsonObject o,String k,double d){double v=o.has(k)?o.get(k).getAsDouble():d;return Double.isFinite(v)&&v>=0?v:d;}
    private static JsonObject weapons(){var w=new JsonObject();w.addProperty("meleeScale",meleeScale);w.addProperty("bowScale",bowScale);w.addProperty("crossbowScale",crossbowScale);w.addProperty("tridentScale",tridentScale);w.addProperty("magicScale",magicScale);w.addProperty("fireScale",fireScale);return w;}
    private static String defaultsJson(){
        var j=new JsonObject();var g=new JsonObject();gunShot.forEach(g::addProperty);j.add("gunShotDamage",g);
        var a=new JsonObject();a.addProperty("perPoint",armorPerPoint);a.addProperty("perToughness",armorPerToughness);a.addProperty("max",armorMax);j.add("armor",a);
        var x=new JsonObject();x.addProperty("tntScale",tntScale);x.addProperty("tntCap",tntCap);x.addProperty("creeperCap",creeperCap);x.addProperty("rocketScale",launcherScale);
        x.addProperty("rocketCap",launcherCap);x.addProperty("grenadeScale",grenadeScale);x.addProperty("grenadeCap",grenadeCap);j.add("explosion",x);
        var b=new JsonObject();b.addProperty("gunAndExplosionScale",bossScale);b.addProperty("minProxyHealth",bossHealth);j.add("boss",b);
        j.add("durability",dur());j.add("shop",shop());j.add("hunger",hunger());j.add("weapons",weapons());j.addProperty("balanceRevision",REVISION);
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
            boolean tnt=source.getDirectEntity() instanceof net.minecraft.world.entity.item.PrimedTnt;
            // Gun Core's explosion.mcfunction spawns a tiny, silent, invulnerable creeper.
            // Its damage source has no player owner, so looking only at the held RPG misses real shots.
            var direct=source.getDirectEntity();
            boolean gunCoreBlast=direct instanceof net.minecraft.world.entity.monster.Creeper c
                &&c.getTags().contains("gbz.zombie")&&c.getTags().contains("zombie")&&c.isInvulnerable()&&c.isSilent()
                // 1.21.4 clamps the datapack's requested 0.01 scale to the 0.0625 attribute minimum.
                &&c.getAttributeValue(Attributes.SCALE)<=.06251;
            boolean rocket=gunCoreBlast||!tnt&&("rpg_7".equals(gun)||"m72_law".equals(gun)||"m202_flash".equals(gun)),grenade=!tnt&&gun!=null&&(gun.equals("milkor_mgl")||gun.equals("m79")||gun.equals("gl06"));
            boolean creeper=source.getDirectEntity()!=null&&source.getDirectEntity().getType()==net.minecraft.world.entity.EntityType.CREEPER;
            if(rocket){result=Math.min(amount*launcherScale,launcherCap);rule="rocket";}
            else if(grenade){result=Math.min(amount*grenadeScale,grenadeCap);rule="grenade";}
            else if(creeper){result=Math.min(amount,creeperCap);rule="creeper";}
            else{result=Math.min(amount*tntScale,tntCap);rule="tnt";}
            if(boss)result*=bossScale;
        }else if(type.startsWith("gbg:")&&gun!=null){
            Double shot=gunShot.get(gun);double raw=rawGun(held);
            var custom=held.get(DataComponents.CUSTOM_DATA);
            boolean flame=custom!=null&&custom.copyTag().getCompound("gbg").getInt("projectile_type")==7;
            if(flame){result=amount*1.5;rule="flamethrower";}
            else if(shot!=null&&raw>0){result=amount*shot/raw;rule="gun:"+gun;}
            if(boss)result*=bossScale;
        }else if(source.is(net.minecraft.world.damagesource.DamageTypes.PLAYER_ATTACK)&&source.getDirectEntity()==source.getEntity()&&!held.isEmpty()){
            result=amount*EquipmentBalance.meleeScale(held)*meleeScale;rule="melee";
        }else if(source.is(net.minecraft.world.damagesource.DamageTypes.TRIDENT)){
            result=amount*tridentScale;rule="trident";
        }else if(source.getDirectEntity() instanceof net.minecraft.world.entity.projectile.AbstractArrow arrow){
            ItemStack weapon=arrow.getWeaponItem();
            if(weapon!=null&&(weapon.is(net.minecraft.world.item.Items.BOW)||weapon.is(net.minecraft.world.item.Items.CROSSBOW))){
                result=amount*(weapon.is(net.minecraft.world.item.Items.CROSSBOW)?crossbowScale:bowScale);rule="arrow";
            }
        }else if(source.is(net.minecraft.world.damagesource.DamageTypes.MAGIC)||source.is(net.minecraft.world.damagesource.DamageTypes.INDIRECT_MAGIC)){
            result=amount*magicScale;rule="magic";
        }else if(source.is(net.minecraft.world.damagesource.DamageTypes.LAVA)||source.is(net.minecraft.world.damagesource.DamageTypes.IN_FIRE)||source.is(net.minecraft.world.damagesource.DamageTypes.ON_FIRE)){
            result=amount*fireScale;rule="fire";
        }
        if(!rule.equals("vanilla")&&!logged){logged=true;Passthrough.LOG.info("DQJ balance first scaled hit {} {} -> {} ({})",type,amount,result,rule);}
        return (float)result;
    }
    /** Fraction of a Wukong hit absorbed by the player's worn armor. */
    public static double armorReduction(ServerPlayer p){
        return EquipmentBalance.armor(p,armorPerPoint,armorPerToughness,armorMax);
    }
}
