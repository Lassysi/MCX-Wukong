package dev.rehan.passthrough.client;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import dev.rehan.passthrough.Passthrough;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import net.minecraft.client.Minecraft;
import net.minecraft.core.BlockPos;

/** Preview84 (VIS-01): cells the host draws natively in Wukong. Chunk meshing skips them, so the
 *  composited MC layer no longer contains (and can no longer drift) those blocks. The host link
 *  closing clears the set, so a vanished host never leaves blocks invisible. */
public final class NativeBlocks {
    private static final Set<Long> drawn=ConcurrentHashMap.newKeySet();
    private NativeBlocks() {}
    public static boolean drawn(BlockPos pos){return !drawn.isEmpty()&&drawn.contains(pos.asLong());}
    private static long lastTarget=Long.MIN_VALUE;
    private static boolean useWasDown;private static int useLogs;
    /** Preview87 (BUG-12): every use press with a placeable item in hand logs what the crosshair hit and why a
     *  native target was not used, so failed placements can be counted by cause. */
    static void logUse(Minecraft mc){
        boolean down=mc.options.keyUse.isDown();
        if(down&&!useWasDown&&useLogs<400&&mc.player!=null&&dev.rehan.passthrough.Passthrough.minecraftAuthority){
            var item=mc.player.getMainHandItem().getItem();
            if(item instanceof net.minecraft.world.item.BlockItem||item instanceof net.minecraft.world.item.BucketItem){
                useLogs++;var hit=mc.hitResult;
                String what=hit==null?"none":hit.getType().toString();
                if(hit instanceof net.minecraft.world.phys.BlockHitResult b&&hit.getType()==net.minecraft.world.phys.HitResult.Type.BLOCK)
                    what+=" "+b.getBlockPos().toShortString()+" "+b.getDirection().getName()+" proxy="+dev.rehan.passthrough.WorldBridge.isHostProxy(b.getBlockPos());
                dev.rehan.passthrough.Passthrough.LOG.info("DQJ place-diag press item={} hit={} lastMiss={} reaimed={}",
                    net.minecraft.core.registries.BuiltInRegistries.ITEM.getKey(item),what,dev.rehan.passthrough.HostTerrainTarget.lastMiss(),dev.rehan.passthrough.HostTerrainTarget.reaimedCount());
            }
        }
        useWasDown=down;
    }
    /** Preview86: the aimed block goes to the host, which draws its outline natively (MC skips it). */
    static void tick(Minecraft mc){
        long target=Long.MIN_VALUE;
        if(dev.rehan.passthrough.Passthrough.minecraftAuthority&&mc.hitResult instanceof net.minecraft.world.phys.BlockHitResult b
            &&b.getType()==net.minecraft.world.phys.HitResult.Type.BLOCK&&drawn(b.getBlockPos()))target=b.getBlockPos().asLong();
        if(target==lastTarget)return;lastTarget=target;
        if(target==Long.MIN_VALUE){dev.rehan.passthrough.Passthrough.events.accept("{\"t\":\"target_block\"}");return;}
        var p=BlockPos.of(target);
        dev.rehan.passthrough.Passthrough.events.accept("{\"t\":\"target_block\",\"p\":["+p.getX()+","+p.getY()+","+p.getZ()+"]}");
    }
    static void handle(JsonObject m){
        var mc=Minecraft.getInstance();
        mc.execute(()->{
            if(m.has("clear")&&m.get("clear").getAsBoolean())clear();
            if(m.has("add"))apply(m.getAsJsonArray("add"),true);
            if(m.has("del"))apply(m.getAsJsonArray("del"),false);
        });
    }
    private static void apply(JsonArray a,boolean add){
        for(int i=0;i+2<a.size();i+=3){
            var pos=new BlockPos(a.get(i).getAsInt(),a.get(i+1).getAsInt(),a.get(i+2).getAsInt());
            if(add?drawn.add(pos.asLong()):drawn.remove(pos.asLong()))dirty(pos);
        }
    }
    static void clear(){
        if(drawn.isEmpty())return;
        var copy=new java.util.ArrayList<>(drawn);drawn.clear();
        Minecraft.getInstance().execute(()->{for(long p:copy)dirty(BlockPos.of(p));});
        Passthrough.LOG.info("DQJ native blocks cleared: {} back to the MC layer",copy.size());
    }
    private static void dirty(BlockPos p){
        var mc=Minecraft.getInstance();
        if(mc.level!=null)mc.levelRenderer.setBlocksDirty(p.getX(),p.getY(),p.getZ(),p.getX(),p.getY(),p.getZ());
    }
}
