package dev.rehan.passthrough;

import java.util.Locale;
import net.minecraft.core.registries.Registries;
import net.minecraft.resources.ResourceKey;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.damagesource.DamageSource;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.projectile.AbstractArrow;
import net.minecraft.world.entity.projectile.ThrowableItemProjectile;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.enchantment.Enchantment;
import net.minecraft.world.item.enchantment.EnchantmentHelper;
import net.minecraft.world.item.enchantment.Enchantments;

/** Live112 (user: make sure enchantment values really work on Wukong enemies, adapt them). Vanilla already applies
 *  Sharpness, Power, Sweeping Edge, Fire Aspect, Flame, Thorns, Protection and Unbreaking to the pig proxies / the player.
 *  What the proxies cannot carry is adapted here:
 *  - Smite / Bane of Arthropods: the host tags each enemy "undead" or "arthropod" by class; +2.5 damage per level (vanilla).
 *  - Knockback / Punch: proxies keep full knockback resistance (ordinary hits must not push), so the enchantment's impulse
 *    is handed to ProxyFx, which launches the real monster (Bosses are capped host-side). Snowballs/eggs push a little.
 *  - Looting: more emeralds from Will (+25% per level, weapon in the main hand). Efficiency: fewer pickaxe swings (host). */
public final class Enchants {
    private Enchants() {}
    private static int logged;

    static int level(ServerPlayer p,ResourceKey<Enchantment> key,ItemStack stack){
        if(stack==null||stack.isEmpty())return 0;
        var holder=p.serverLevel().registryAccess().lookupOrThrow(Registries.ENCHANTMENT).get(key);
        return holder.isPresent()?EnchantmentHelper.getItemEnchantmentLevel(holder.get(),stack):0;
    }

    /** Extra host damage for a player hit on a proxy (already includes vanilla enchantments) and the knockback impulse. */
    static float onProxyHit(LivingEntity target,String id,String kind,DamageSource source){
        if(!(source.getEntity() instanceof ServerPlayer p))return 0;
        var direct=source.getDirectEntity();float bonus=0;double push=0;String why="";
        if(direct==p&&source.is(net.minecraft.world.damagesource.DamageTypes.PLAYER_ATTACK)){ // not Thorns
            var weapon=p.getMainHandItem();
            if("undead".equals(kind)){int l=level(p,Enchantments.SMITE,weapon);if(l>0){bonus=2.5f*l;why="smite "+l;}}
            if("arthropod".equals(kind)){int l=level(p,Enchantments.BANE_OF_ARTHROPODS,weapon);if(l>0){bonus=2.5f*l;why="bane "+l;}}
            int kb=level(p,Enchantments.KNOCKBACK,weapon);if(kb>0){push=.5*kb;why+=(why.isEmpty()?"":", ")+"knockback "+kb;}
        }else if(direct instanceof AbstractArrow arrow){
            int punch=level(p,Enchantments.PUNCH,arrow.getWeaponItem());if(punch>0){push=.6*punch;why="punch "+punch;}
        }else if(direct instanceof ThrowableItemProjectile){push=.3;why="thrown";}
        if(push>0){
            var from=direct==null?p.position():direct.position();
            double dx=target.getX()-from.x,dz=target.getZ()-from.z,len=Math.sqrt(dx*dx+dz*dz);
            if(len<1e-3){var look=p.getViewVector(1f);dx=look.x;dz=look.z;len=Math.max(1e-3,Math.sqrt(dx*dx+dz*dz));}
            ProxyFx.capture(id,new net.minecraft.world.phys.Vec3(dx/len*push,.1,dz/len*push));
        }
        if(!why.isEmpty()&&logged++<40)
            Passthrough.LOG.info("DQJ enchant: {} on enemy {} kind={} bonus={} push={}",why,id,kind.isEmpty()?"-":kind,bonus,String.format(Locale.ROOT,"%.2f",push));
        return bonus;
    }

    /** Looting on the main-hand weapon: Will -> emeralds multiplier. */
    static double lootingScale(ServerPlayer p){return 1+.25*level(p,Enchantments.LOOTING,p.getMainHandItem());}

    /** Short hint for a refused book: which kind of item it is for. */
    static String fitsHint(Enchantment e){
        var samples=new Object[][]{{net.minecraft.world.item.Items.DIAMOND_SWORD,"swords"},{net.minecraft.world.item.Items.BOW,"bows"},
            {net.minecraft.world.item.Items.CROSSBOW,"crossbows"},{net.minecraft.world.item.Items.TRIDENT,"tridents"},
            {net.minecraft.world.item.Items.DIAMOND_PICKAXE,"tools"},{net.minecraft.world.item.Items.DIAMOND_CHESTPLATE,"armor"},
            {net.minecraft.world.item.Items.DIAMOND_BOOTS,"boots"},{net.minecraft.world.item.Items.DIAMOND_HELMET,"helmets"},
            {net.minecraft.world.item.Items.MACE,"maces"}};
        var fits=new java.util.ArrayList<String>();
        for(var s:samples)if(e.canEnchant(new ItemStack((net.minecraft.world.item.Item)s[0])))fits.add((String)s[1]);
        return fits.isEmpty()?"":" - only for "+String.join("/",fits.subList(0,Math.min(3,fits.size())));
    }
}
