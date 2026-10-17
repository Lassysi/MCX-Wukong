package dev.rehan.passthrough;

import java.util.LinkedHashMap;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import net.minecraft.world.phys.shapes.Shapes;
import net.minecraft.world.phys.shapes.VoxelShape;
import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.core.BlockPos;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.GameType;
import net.minecraft.world.level.block.Blocks;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.phys.AABB;

/** Terrain proxies and event transport; vanilla Minecraft handles physics and items. */
public final class WorldBridge {
    private static volatile MinecraftServer server;
    private static boolean proxyWrite;
    private static final Map<BlockPos,Boolean> changed = new LinkedHashMap<>();
    private static final Map<BlockPos,Integer> surfaces = new ConcurrentHashMap<>();
    private static final Map<Long,BlockPos> columns = new ConcurrentHashMap<>();
    private static final java.util.Set<BlockPos> proxyVoxels=ConcurrentHashMap.newKeySet();
    public static volatile String groundEpoch="";
    public static volatile String worldId="";
    public static volatile boolean groundReady,groundCommitted,groundAnchorResolved;
    public static volatile String groundFailure="";
    private static volatile boolean preparing;
    public static volatile double spawnX=.5,spawnY=64,spawnZ=.5;
    public static boolean isPreparing(){return preparing;}
    private static long prepareTick;
    static void attach(MinecraftServer value) {
        server=value;
        String folder=value.getWorldPath(net.minecraft.world.level.storage.LevelResource.ROOT).toAbsolutePath().normalize().toString();
        worldId=java.util.UUID.nameUUIDFromBytes(folder.getBytes(java.nio.charset.StandardCharsets.UTF_8)).toString().replace("-","");
        Balance.load(net.fabricmc.loader.api.FabricLoader.getInstance().getConfigDir());
        CombatBridge.attach(value);
        HostBlockAnchors.attach(value);
    }
    static void detach(MinecraftServer value) { HostTerrainTarget.clear();for(var p:value.getPlayerList().getPlayers())p.setNoGravity(false);retireProxies(value);HostBlockAnchors.detach();CombatBridge.clear();CombatBridge.forgetProxyIds();server = null; changed.clear();worldId="";groundEpoch="";groundFailure="";groundReady=groundCommitted=preparing=groundAnchorResolved=false; }
    public static boolean isHostProxy(BlockPos pos){return surfaces.containsKey(pos)||proxyVoxels.contains(pos);}
    private static void retireProxies(MinecraftServer s){
        var owned=new java.util.HashSet<BlockPos>(surfaces.keySet());owned.addAll(proxyVoxels);
        proxyWrite=true;try{for(var p:owned)if(s.overworld().getBlockState(p).is(Blocks.BARRIER))s.overworld().setBlock(p,Blocks.AIR.defaultBlockState(),18);}
        finally{proxyWrite=false;}
        surfaces.clear();columns.clear();proxyVoxels.clear();
    }
    /** Preview85: how far the native ground in a host surface proxy cell lies below its top (0 if none). */
    public static double surfaceDrop(BlockPos pos){Integer height=surfaces.get(pos);return height==null?0:1-height/1000.0;}
    public static VoxelShape surfaceShape(BlockPos pos){
        Integer height=surfaces.get(pos);return height==null?null:Shapes.box(0,0,0,1,height/1000.0,1);
    }
    static BlockPos supportAt(int x,int z){return columns.get(((long)x<<32)^(z&0xffffffffL));}
    private static void applySurfaces(int[] cells){
        proxyWrite=true;try{
            for(int i=0;i+3<cells.length;i+=4){
                var pos=new BlockPos(cells[i],cells[i+1],cells[i+2]);int height=cells[i+3];
                if(!server.overworld().isInWorldBounds(pos)||height<1||height>1000)continue;
                long column=((long)pos.getX()<<32)^(pos.getZ()&0xffffffffL);
                var old=columns.put(column,pos);
                if(old!=null&&!old.equals(pos)){
                    surfaces.remove(old);if(server.overworld().getBlockState(old).is(Blocks.BARRIER))server.overworld().setBlock(old,Blocks.AIR.defaultBlockState(),18);
                }
                var block=server.overworld().getBlockState(pos);
                if(block.isAir()||block.is(Blocks.BARRIER)){
                    surfaces.put(pos,height);server.overworld().setBlock(pos,Blocks.BARRIER.defaultBlockState(),18);
                }
            }
        }finally{proxyWrite=false;}
    }
    public static void groundSurfaces(int[] cells){groundSurfaces(cells,null);}
    /** Preview84: one full-height surface proxy at an aimed native ground cell, if that cell is air. */
    // Preview85 (BUG-09: jump-placing under the feet shoved the player): the proxy has the native
    // ground height inside its cell (was always full height, so a ground point in the player's own
    // feet cell put a full barrier around the player), and is never created inside a player.
    public static void ensureSurface(BlockPos pos,double groundInCell,String epoch){
        var s=server;if(s==null||!Double.isFinite(groundInCell))return;
        int height=(int)Math.round(Math.max(.001,Math.min(1,groundInCell))*1000);
        s.execute(()->{
            if(!groundCommitted||epoch==null||!epoch.equals(groundEpoch)||!s.overworld().isLoaded(pos)||!s.overworld().getBlockState(pos).isAir())return;
            var box=new net.minecraft.world.phys.AABB(pos.getX(),pos.getY(),pos.getZ(),pos.getX()+1,pos.getY()+height/1000.0,pos.getZ()+1);
            for(var p:s.getPlayerList().getPlayers())if(p.getBoundingBox().intersects(box.deflate(.001)))return;
            applySurfaces(new int[]{pos.getX(),pos.getY(),pos.getZ(),height});
        });
    }
    public static void groundSurfaces(int[] cells,String epoch){var s=server;if(s!=null&&cells.length<=4096)s.execute(()->{if(epoch==null||epoch.equals(groundEpoch))applySurfaces(cells);});}
    public static void prepare(JsonObject message){
        var s=server;if(s==null)return;
        String epoch=message.get("epoch").getAsString();if(epoch.length()!=32)return;
        var array=message.getAsJsonArray("c");if(array.size()>4096)return;
        int[] cells=new int[array.size()];for(int i=0;i<cells.length;i++)cells[i]=array.get(i).getAsInt();
        float yaw=message.get("yaw").getAsFloat(),pitch=message.get("pitch").getAsFloat();
        var spawn=message.has("spawn")?message.getAsJsonArray("spawn"):null;
        double sx=spawn==null?.5:spawn.get(0).getAsDouble(),sy=spawn==null?64:spawn.get(1).getAsDouble(),sz=spawn==null?.5:spawn.get(2).getAsDouble();
        if(!Double.isFinite(sx+sy+sz)||Math.abs(sx)>29_999_900||Math.abs(sz)>29_999_900||sy< -50||sy>300)return;
        boolean guarded=message.has("guard")&&message.get("guard").getAsBoolean();
        s.execute(()->{
            HostTerrainTarget.clear();
            groundReady=groundCommitted=groundAnchorResolved=false;groundFailure="";groundEpoch=epoch;preparing=true;prepareTick=s.overworld().getGameTime();
            spawnX=sx;spawnY=sy;spawnZ=sz;
            TerrainLease.enabled=guarded;
            // Old host surfaces have no meaning after a teleport/new anchor.
            retireProxies(s);applySurfaces(cells);
            // Preview99: nearest free column within 1.5 m when MC blocks fill the exact spawn column.
            double[] safe=GroundSpawn.resolveNear(s.overworld(),sx,sy,sz);
            boolean alive=safe!=null;
            double safeY=alive?safe[1]:Double.NaN,px=alive?safe[0]:sx,pz=alive?safe[2]:sz;
            if(alive){spawnX=px;spawnY=safeY;spawnZ=pz;}else groundFailure="no-safe-standing-height";
            if(alive&&(px!=sx||pz!=sz))Passthrough.LOG.info("DQJ ground: spawn column blocked by MC blocks; player placed {} m away at {},{},{}",String.format(java.util.Locale.ROOT,"%.2f",Math.hypot(px-sx,pz-sz)),px,safeY,pz);
            groundAnchorResolved=alive&&HostBlockAnchors.standingOnAnchor(s.overworld(),px,safeY,pz);
            for(var p:s.getPlayerList().getPlayers()){
                if(!p.isAlive()){alive=false;continue;}
                p.setNoGravity(true);p.setDeltaMovement(0,0,0);p.fallDistance=0;
                if(Double.isFinite(safeY))s.getCommands().performPrefixedCommand(s.createCommandSourceStack().withSuppressedOutput(),"tp "+p.getScoreboardName()+" "+px+" "+safeY+" "+pz+" "+yaw+" "+pitch);
            }
            groundReady=alive&&!s.getPlayerList().getPlayers().isEmpty()
                &&GroundSpawn.supported(s.overworld(),px,spawnY,pz);
            Passthrough.LOG.info("Ground prepared epoch={} ready={} requestedY={} resolvedY={} failure={} surfaces={}",epoch,groundReady,sy,spawnY,groundFailure,surfaces.size());
        });
    }
    public static void commit(String epoch){var s=server;if(s==null)return;s.execute(()->{
        if(!epoch.equals(groundEpoch)||!groundReady||!preparing)return;
        for(var p:s.getPlayerList().getPlayers()){p.setNoGravity(false);p.setDeltaMovement(0,0,0);p.fallDistance=0;}
        preparing=false;groundCommitted=true;enableVanillaMotion();Passthrough.LOG.info("Ground committed epoch={}",epoch);
    });}
    public static void release(String epoch){var s=server;if(s==null)return;s.execute(()->{
        if(!epoch.equals(groundEpoch))return;
        HostTerrainTarget.clear();
        // A rejected destination must not drop the idle MC player into a region
        // whose old support was retired. Hold it until the next valid prepare.
        boolean hold=preparing&&!groundReady;
        if(preparing&&!hold)for(var p:s.getPlayerList().getPlayers())p.setNoGravity(false);
        preparing=hold;groundReady=groundCommitted=groundAnchorResolved=false;
    });}
    public static boolean ready() { return server != null; }
    public static void command(String value) {
        var s = server;
        if (s != null) s.execute(() -> s.getCommands().performPrefixedCommand(s.createCommandSourceStack().withSuppressedOutput(), value));
    }
    /** Item 6a (user 2026-10-16): a Wukong enemy hit this MC block 3 times; it breaks and drops
     *  like a player break (chest contents spill). Host proxies and barriers are never broken. */
    public static void enemyBreak(int x,int y,int z){
        var s=server;if(s==null)return;
        s.execute(()->{
            var level=s.overworld();var pos=new BlockPos(x,y,z);var state=level.getBlockState(pos);
            if(state.isAir()||state.is(Blocks.BARRIER)||state.is(Blocks.BEDROCK)||isHostProxy(pos)||state.getDestroySpeed(level,pos)<0)return;
            level.destroyBlock(pos,true);
            Passthrough.LOG.info("DQJ enemy-break: {} at {} broken by a Wukong enemy",state.getBlock(),pos);
        });
    }
    public static void enableVanillaMotion() {
        var s = server;
        if (s == null) return;
        // Live48 (BUGS-OPEN 01/02): this ran on every MC reattach (ESC/F6 return) and
        // forced SURVIVAL on the server only, desyncing a creative client: commands
        // looked ignored and anchored blocks reappeared after an instant break.
        // Keep the player's chosen mode; only leave spectator, synced to the client.
        s.execute(() -> { for (var p:s.getPlayerList().getPlayers()) {
            if(p.gameMode.getGameModeForPlayer()==GameType.SPECTATOR)p.setGameMode(GameType.SURVIVAL);
            p.getAbilities().flying = false; p.noPhysics = false; p.onUpdateAbilities();
            // Preview69 (user: stairs hard to climb): host stairs arrive as 1-block voxel steps;
            // vanilla step height 0.6 cannot climb them. 1.1 walks up stairs and 1-block ledges.
            var step=p.getAttribute(net.minecraft.world.entity.ai.attributes.Attributes.STEP_HEIGHT);
            // Preview95: 1.26 (was 1.1) for a bit more tolerance on rastered native stairs.
            if(step!=null&&step.getBaseValue()<1.26){step.setBaseValue(1.26);Passthrough.LOG.info("DQJ motion: step height 1.26 for host stairs");}
        }});
    }
    public static void solid(int[] columns) {
        var s = server;
        if (s == null) return;
        s.execute(() -> {
            proxyWrite = true;
            try {
                for (int i=0;i+3<columns.length;i+=4) {
                    if (columns[i+3]-columns[i+2]>384) continue;
                    for (int y=columns[i+2];y<=columns[i+3];y++) {
                        var p=new BlockPos(columns[i],y,columns[i+1]);
                        if (s.overworld().isInWorldBounds(p) && s.overworld().getBlockState(p).isAir()){
                            s.overworld().setBlock(p,Blocks.BARRIER.defaultBlockState(),18);proxyVoxels.add(p.immutable());
                        }
                    }
                }
            } finally { proxyWrite=false; }
        });
    }
    public static void voxels(int[] cells){
        var s=server;if(s==null||cells.length>4096)return;
        s.execute(()->{proxyWrite=true;try{
            for(int i=0;i+3<cells.length;i+=4){
                var p=new BlockPos(cells[i],cells[i+1],cells[i+2]);if(!s.overworld().isInWorldBounds(p))continue;
                var current=s.overworld().getBlockState(p);
                if((current.isAir()||current.is(Blocks.BARRIER)) && (cells[i+3]!=0||!surfaces.containsKey(p))){
                    s.overworld().setBlock(p,cells[i+3]!=0?Blocks.BARRIER.defaultBlockState():Blocks.AIR.defaultBlockState(),18);
                    if(cells[i+3]!=0)proxyVoxels.add(p.immutable());else proxyVoxels.remove(p);
                }
            }
        }finally{proxyWrite=false;}});
    }
    private static JsonObject cell(ServerLevel level,BlockPos p){
        var state=level.getBlockState(p);JsonObject c=new JsonObject();
        JsonArray at=new JsonArray();at.add(p.getX());at.add(p.getY());at.add(p.getZ());c.add("p",at);
        c.addProperty("id",BuiltInRegistries.BLOCK.getKey(state.getBlock()).toString());
        var offset=HostBlockAnchors.offset(state,p);
        c.addProperty("anchorDy",offset.y);c.addProperty("anchorDx",offset.x);c.addProperty("anchorDz",offset.z);
        // Preview84: logs/pillars turn their end texture to the axis in the host's native mesh.
        if(state.hasProperty(net.minecraft.world.level.block.state.properties.BlockStateProperties.AXIS))
            c.addProperty("axis",state.getValue(net.minecraft.world.level.block.state.properties.BlockStateProperties.AXIS).getName());
        JsonArray boxes=new JsonArray();
        var shape=state.getCollisionShape(level,p);var parts=shape.toAabbs();
        boolean conservative=parts.size()>128;c.addProperty("conservative",conservative);
        // Do not silently drop geometry for unusually complex modded blocks.
        if(conservative)parts=java.util.List.of(shape.bounds());
        for(var b:parts){
            JsonArray a=new JsonArray();a.add(b.minX);a.add(b.minY);a.add(b.minZ);a.add(b.maxX);a.add(b.maxY);a.add(b.maxZ);boxes.add(a);
        }
        c.add("boxes",boxes);return c;
    }
    // Preview86 (user: distant/older constructions still drift): every non-air block within 24 blocks
    // horizontally and 12 vertically is sent (cap 4096), and the area is topped up whenever the player has
    // moved 8 blocks, so the host draws them natively instead of the MC layer.
    private static final java.util.Set<Long> sentCells=ConcurrentHashMap.newKeySet();
    private static BlockPos lastScan;
    public static void snapshot(){
        var s=server;if(s==null)return;s.execute(()->{
            Passthrough.events.accept("{\"t\":\"block_reset\"}");
            sentCells.clear();lastScan=null;scanAround(s);
        });
    }
    private static void scanAround(MinecraftServer s){
        JsonArray cells=new JsonArray();
        for(var player:s.getPlayerList().getPlayers()){
            var center=player.blockPosition();lastScan=center;
            for(var p:BlockPos.betweenClosed(center.offset(-24,-12,-24),center.offset(24,12,24))){
                if(sentCells.size()>=4096)break;
                if(!s.overworld().isLoaded(p))continue;
                var block=s.overworld().getBlockState(p);if(block.isAir()||block.is(Blocks.BARRIER))continue;
                if(!sentCells.add(p.asLong()))continue;
                cells.add(cell(s.overworld(),p));
                if(cells.size()==8){sendCells(cells);cells=new JsonArray();}
            }
        }
        if(!cells.isEmpty())sendCells(cells);
    }
    static void topUp(MinecraftServer s){
        if(!Passthrough.active||!groundCommitted||lastScan==null||s.getTickCount()%20!=0)return;
        for(var player:s.getPlayerList().getPlayers())
            if(player.blockPosition().distManhattan(lastScan)>=8){scanAround(s);return;}
    }
    private static void sendCells(JsonArray cells){
        JsonArray batch=new JsonArray();int bytes=0;
        for(var c:cells){int length=c.toString().length();
            if(bytes+length>30000 && !batch.isEmpty()){sendCellBatch(batch);batch=new JsonArray();bytes=0;}
            batch.add(c);bytes+=length;
        }
        if(!batch.isEmpty())sendCellBatch(batch);
    }
    private static void sendCellBatch(JsonArray cells){
        JsonObject out=new JsonObject();out.addProperty("t","block_cells");out.add("cells",cells);Passthrough.events.accept(out.toString());
    }
    public static void changed(ServerLevel level,BlockPos pos,BlockState state) {
        HostBlockAnchors.changed(level,pos,state);
        // Preview80 (self-check): host terrain proxies are retired on every detach and re-rastered on reattach;
        // those writes must never drop user blocks that rest on them (they fell into the void below).
        if(level==level.getServer().overworld()&&!proxyWrite)BlockGravity.changed(level,pos);
        if (!Passthrough.active || server==null || level!=server.overworld() || proxyWrite || state.is(Blocks.BARRIER)) return;
        changed.put(pos.immutable(),!state.getCollisionShape(level,pos).isEmpty());
    }
    static void tick(MinecraftServer value) {
        topUp(value);
        Allies.tick(value);
        if(preparing){
            for(var p:value.getPlayerList().getPlayers()){p.setDeltaMovement(0,0,0);p.fallDistance=0;}
            if(value.overworld().getGameTime()-prepareTick>200&&groundFailure.isEmpty()){groundFailure="prepare-timeout";groundReady=false;}
        }
        CombatBridge.tick(value);
        BlockGravity.tick(value);
        if (changed.isEmpty()) return;
        StringBuilder set=new StringBuilder(),clear=new StringBuilder();JsonArray cells=new JsonArray();
        for (var e:changed.entrySet()) {
            cells.add(cell(value.overworld(),e.getKey()));if(cells.size()==8){sendCells(cells);cells=new JsonArray();}
            var target=e.getValue()?set:clear;
            if (!target.isEmpty()) target.append(',');
            var p=e.getKey(); target.append(p.getX()).append(',').append(p.getY()).append(',').append(p.getZ());
        }
        changed.clear();
        if(!cells.isEmpty())sendCells(cells);
        Passthrough.events.accept("{\"t\":\"blocks\",\"set\":["+set+"],\"clear\":["+clear+"]}");
    }
}
