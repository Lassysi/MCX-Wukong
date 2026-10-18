package dev.rehan.passthrough;

import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import net.minecraft.core.BlockPos;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.entity.item.FallingBlockEntity;
import net.minecraft.world.level.block.Blocks;
import net.minecraft.world.level.block.FallingBlock;
import net.minecraft.world.level.block.state.BlockState;

/** Item 6b (user 2026-10-16): every ordinary MC block falls like sand/gravel when nothing
 *  supports it, so fewer blocks float. Excluded: block entities (chests, barrels keep their
 *  contents), host terrain proxies, fluids, vanilla falling blocks, barrier/bedrock and blocks
 *  without collision.
 *  Preview88 (user: no block may float): anchored blocks fall too, except those resting on Wukong
 *  terrain (kind 0: the native ground is not an MC block, so MC cannot see that support). Support of
 *  an offset block is any block in the cells under its shifted footprint. A fall keeps the offset, and
 *  the landed block is re-anchored onto the native ground or the anchored block below it. */
public final class BlockGravity {
    private BlockGravity() {}
    private static final Set<BlockPos> queue=ConcurrentHashMap.newKeySet();
    private static final int PerTick=64;
    private static int logged;
    private record Fall(FallingBlockEntity entity,BlockState state) {}
    private static final List<Fall> falling=new ArrayList<>();
    private static final java.util.Map<BlockPos,Integer> waiting=new ConcurrentHashMap<>();
    private static final Set<BlockPos> deferred=ConcurrentHashMap.newKeySet();
    /** Any block or host proxy within 48 cells straight below. */
    private static boolean landingBelow(ServerLevel level,BlockPos pos){
        for(int dy=1;dy<=48;dy++){
            var p=pos.below(dy);if(p.getY()<level.getMinY())return false;
            if(WorldBridge.isHostProxy(p)||!level.getBlockState(p).isAir())return true;
        }
        return false;
    }
    /** Preview103: game tick of the last block change per cell, except changes made by landings() itself.
     *  A falling block was placed by vanilla only if its own landing cell changed in the last tick. */
    private static final java.util.Map<BlockPos,Long> lastSet=new ConcurrentHashMap<>();
    private static boolean placingSelf;
    static void changed(ServerLevel level,BlockPos pos){
        if(!placingSelf){if(lastSet.size()>4096)lastSet.clear();lastSet.put(pos.immutable(),level.getGameTime());}
        if(queue.size()>8192)return;
        queue.add(pos.immutable());queue.add(pos.above().immutable());
    }
    static void tick(MinecraftServer server){
        ServerLevel level=server.overworld();
        landings(level);
        if(!deferred.isEmpty()&&server.getTickCount()%20==0){queue.addAll(deferred);deferred.clear();}
        if(queue.isEmpty())return;
        // BUG-05: while the bridge is detached or re-preparing, host support under user blocks is
        // missing or half-rastered; changes queued then must not drop blocks after the next commit.
        if(!WorldBridge.groundCommitted||WorldBridge.isPreparing()){queue.clear();return;}
        int n=0;
        for(Iterator<BlockPos> it=queue.iterator();it.hasNext()&&n<PerTick;n++){
            BlockPos pos=it.next();it.remove();
            try{fallIfFree(level,pos);}catch(RuntimeException e){Passthrough.LOG.warn("DQJ gravity {}: {}",pos,e.toString());}
        }
    }
    private static void landings(ServerLevel level){
        if(falling.isEmpty())return;
        for(Iterator<Fall> it=falling.iterator();it.hasNext();){
            var f=it.next();var e=f.entity();
            if(!e.isRemoved())continue;
            it.remove();
            var reason=e.getRemovalReason();
            if(reason!=null&&reason.shouldSave())continue; // chunk unloaded mid-fall: the saved entity resumes later (rare; not re-tracked)
            BlockPos at=e.blockPosition();
            // Preview103 (user: a broken block's column should drop and land as blocks, like felling a tree, not
            // turn into items): vanilla drops the block as an item when its cell cannot hold it, which on Wukong
            // ground is usually the partial-height ground barrier. Our falling blocks never drop; we place them here.
            // Live103 self-test: a block landing on an earlier, sunk (offset) block rests inside that block's cell, so
            // "a block of this type is here" is not proof; only a vanilla change of this cell in the last tick is.
            Long set=lastSet.remove(at);
            boolean vanillaPlaced=level.getBlockState(at).is(f.state().getBlock())&&set!=null&&level.getGameTime()-set<=2;
            placingSelf=true;
            try{
            if(!vanillaPlaced){
                BlockPos free=null;
                for(int up=0;up<=3&&free==null;up++){
                    var c=at.above(up);var s=level.getBlockState(c);
                    if(!WorldBridge.isHostProxy(c)&&(s.isAir()||s.canBeReplaced())&&s.getFluidState().isEmpty())free=c;
                }
                if(free==null){
                    net.minecraft.world.level.block.Block.popResource(level,at,new net.minecraft.world.item.ItemStack(f.state().getBlock()));
                    Passthrough.LOG.info("DQJ gravity: {} found no free cell at {}; dropped as item",f.state().getBlock(),at.toShortString());
                    continue;
                }
                level.setBlock(free,f.state(),3);at=free;
            }
            try{HostBlockAnchors.landed(level,at,level.getBlockState(at),e.getX(),e.getZ());}catch(RuntimeException ex){Passthrough.LOG.warn("DQJ gravity landing {}: {}",at,ex.toString());}
            liftPlayers(level,at);
            }finally{placingSelf=false;}
        }
    }
    /** Live109 (user: standing on a stack whose bottom breaks, Steve fell into a landed block and got stuck): a player
     *  whose body overlaps a just-landed block (its anchored, possibly sunk shape) is lifted onto its top, so riding a
     *  falling stack ends standing on it. */
    private static void liftPlayers(ServerLevel level,BlockPos at){
        var shape=level.getBlockState(at).getCollisionShape(level,at);
        if(shape.isEmpty())return;
        var box=shape.bounds().move(at);
        for(var p:level.players()){
            if(!p.isAlive()||p.isSpectator()||!p.getBoundingBox().deflate(.01).intersects(box))continue;
            double top=box.maxY+.001;
            if(top-p.getY()>2.5)continue; // a block far above the feet is a ceiling, not a floor
            p.teleportTo(p.getX(),top,p.getZ());p.resetFallDistance();p.setDeltaMovement(p.getDeltaMovement().multiply(1,0,1));
            Passthrough.LOG.info("DQJ gravity: lifted {} onto landed {} at {} (top {})",p.getScoreboardName(),level.getBlockState(at).getBlock(),at.toShortString(),String.format(java.util.Locale.ROOT,"%.2f",top));
        }
    }
    private static void fallIfFree(ServerLevel level,BlockPos pos){
        if(!level.isLoaded(pos))return;
        BlockState state=level.getBlockState(pos);
        if(!eligible(level,pos,state))return;
        var offset=HostBlockAnchors.offset(state,pos);
        if(supported(level,pos,offset))return;
        // Preview92b (live: 18 of 24 falling blocks never landed): MC only has ground where the host measured the
        // native terrain, so a fall into an unmeasured column went through the native ground into the void. Fall only
        // when something below can catch the block; otherwise ask the host to measure that column and retry.
        if(!landingBelow(level,pos)){
            int tries=waiting.merge(pos.immutable(),1,Integer::sum);
            if(tries<=20){deferred.add(pos.immutable());
                if(tries%4==1)Passthrough.events.accept(String.format(java.util.Locale.ROOT,"{\"t\":\"ally_ground\",\"p\":[%.3f,%.3f,%.3f]}",pos.getX()+.5,(double)pos.getY(),pos.getZ()+.5));
                if(tries==1&&logged++<200)Passthrough.LOG.info("DQJ gravity: {} at {} has no measured ground below; waiting for host terrain",state.getBlock(),pos.toShortString());}
            return;
        }
        waiting.remove(pos);
        var entity=FallingBlockEntity.fall(level,pos,state);
        if(offset!=net.minecraft.world.phys.Vec3.ZERO)entity.setPos(pos.getX()+.5+offset.x,pos.getY()+offset.y,pos.getZ()+.5+offset.z);
        // Preview103: only tracked falls skip the vanilla item drop; landings() places them as blocks.
        if(falling.size()<512){entity.dropItem=false;falling.add(new Fall(entity,state));}
        if(logged++<200)Passthrough.LOG.info("DQJ gravity: {} at {} falls (offset {})",state.getBlock(),pos.toShortString(),offset);
    }
    /** Any block or host proxy in a cell under the (shifted) footprint holds the block up. */
    private static boolean supported(ServerLevel level,BlockPos pos,net.minecraft.world.phys.Vec3 offset){
        double bottom=pos.getY()+offset.y-.01;
        if(bottom<level.getMinY())return true;
        for(int sx=0;sx<=1;sx++)for(int sz=0;sz<=1;sz++){
            var cell=BlockPos.containing(pos.getX()+offset.x+(sx==0?.02:.98),bottom,pos.getZ()+offset.z+(sz==0?.02:.98));
            if(cell.equals(pos))continue;
            if(WorldBridge.isHostProxy(cell))return true;
            var s=level.getBlockState(cell);
            if(!s.isAir()&&!FallingBlock.isFree(s))return true;
        }
        return false;
    }
    private static boolean eligible(ServerLevel level,BlockPos pos,BlockState state){
        if(state.isAir()||!state.getFluidState().isEmpty()||state.hasBlockEntity())return false;
        if(state.getBlock() instanceof FallingBlock||state.is(Blocks.BARRIER)||state.is(Blocks.BEDROCK))return false;
        if(state.getDestroySpeed(level,pos)<0||state.getCollisionShape(level,pos).isEmpty())return false;
        if(WorldBridge.isHostProxy(pos))return false;
        return HostBlockAnchors.kind(state,pos)!=0;
    }
}
