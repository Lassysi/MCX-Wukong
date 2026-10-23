package dev.rehan.passthrough;

import java.util.HashMap;
import java.util.Map;
import net.minecraft.core.BlockPos;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.block.Blocks;
import net.minecraft.world.level.block.state.BlockState;

/** Preview126 (user: blocks are too hard to break; planks/dirt one hit, obsidian-like blocks 2-3, a Boss breaks them at once).
 *  The host reports each Wukong enemy hit on an MC block (one per enemy per 1.2 s); the block's vanilla blast resistance picks
 *  its tier. A bottom-health-bar Boss hit counts double: soft and stone blocks fall to one Boss hit, obsidian to two.
 *  Cracks show the progress; a block left alone for 30 s heals. */
public final class BlockToughness {
    private BlockToughness() {}
    public static final int BOSS_WEIGHT=2;
    private static final long FORGET_TICKS=600;
    private record Damage(int hits,long tick) {}
    private static final Map<Long,Damage> damage=new HashMap<>();
    /** 1 = soft (dirt, planks, logs, glass, wool, sand, ladders; resistance <= 3), 2 = stone-like (stone, bricks, deepslate,
     *  metal blocks; <= 9), 3 = obsidian-class (obsidian, netherite, ancient debris, anvils). */
    public static int tier(BlockState state){
        float r=state.getBlock().getExplosionResistance();
        return r<=3f?1:r<=9f?2:3;
    }
    public static int hitsToBreak(BlockState state,boolean boss){
        int t=tier(state);return boss?(t+BOSS_WEIGHT-1)/BOSS_WEIGHT:t;
    }
    public static void enemyHit(MinecraftServer s,int x,int y,int z,boolean boss){
        if(s!=null)s.execute(()->hit(s.overworld(),new BlockPos(x,y,z),boss));
    }
    /** Server thread. Returns true when the block broke. */
    static boolean hit(ServerLevel level,BlockPos pos,boolean boss){
        var state=level.getBlockState(pos);long now=level.getGameTime();
        damage.values().removeIf(d->now-d.tick()>FORGET_TICKS);
        if(state.isAir()||state.is(Blocks.BARRIER)||state.is(Blocks.BEDROCK)||WorldBridge.isHostProxy(pos)||state.getDestroySpeed(level,pos)<0){damage.remove(pos.asLong());return false;}
        var before=damage.get(pos.asLong());int need=tier(state);
        int hits=(before==null||now-before.tick()>FORGET_TICKS?0:before.hits())+(boss?BOSS_WEIGHT:1);
        int breaker=-1-(int)(pos.asLong()&0x3fffffff); // stable fake breaker id per cell for the crack overlay
        if(hits>=need){
            damage.remove(pos.asLong());level.destroyBlockProgress(breaker,pos,-1);
            level.destroyBlock(pos,true);
            Passthrough.LOG.info("DQJ enemy-break: {} at {} broken by a Wukong {} ({} hits needed, tier {})",state.getBlock(),pos,boss?"Boss":"enemy",hitsToBreak(state,boss),need);
            return true;
        }
        damage.put(pos.asLong(),new Damage(hits,now));
        level.destroyBlockProgress(breaker,pos,Math.min(9,hits*10/need-1));
        Passthrough.LOG.info("DQJ enemy-break: {} at {} hit {}/{} by a Wukong {}",state.getBlock(),pos,hits,need,boss?"Boss":"enemy");
        return false;
    }
    public static void clear(){damage.clear();}
}
