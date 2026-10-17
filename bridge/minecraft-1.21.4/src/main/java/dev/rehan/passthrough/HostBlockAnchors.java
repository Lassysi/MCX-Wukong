package dev.rehan.passthrough;

import com.google.gson.JsonObject;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.function.Supplier;
import net.minecraft.core.BlockPos;
import net.minecraft.core.Direction;
import net.minecraft.core.HolderLookup;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.nbt.CompoundTag;
import net.minecraft.nbt.ListTag;
import net.minecraft.nbt.Tag;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.util.datafix.DataFixTypes;
import net.minecraft.world.InteractionResult;
import net.minecraft.world.item.BlockItem;
import net.minecraft.world.item.context.BlockPlaceContext;
import net.minecraft.world.level.BlockGetter;
import net.minecraft.world.level.ClipContext;
import net.minecraft.world.level.Level;
import net.minecraft.world.level.block.Block;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.level.saveddata.SavedData;
import net.minecraft.world.phys.BlockHitResult;
import net.minecraft.world.phys.HitResult;
import net.minecraft.world.phys.Vec3;
import net.minecraft.world.phys.shapes.Shapes;
import net.minecraft.world.phys.shapes.VoxelShape;

/** Stable per-construction height, shared by MC model, shape, picking and host box export. */
public final class HostBlockAnchors {
    /** Preview85: a construction offset has X/Z too (root centred on the native aim point), not only dy. */
    /** kind (Preview88): 0 = rests on Wukong terrain (also every older ledger entry), 1 = part of a construction
     *  (placed against another anchored block), 2 = attached to a Wukong wall. Only 1 and 2 can fall. */
    private record Anchor(Block block,double dx,double dy,double dz,String group,int kind) {
        Vec3 offset(){return new Vec3(dx,dy,dz);}
        Anchor with(Block b){return new Anchor(b,dx,dy,dz,group,kind);}
        Anchor child(Block b){return new Anchor(b,dx,dy,dz,group,1);}
    }
    private record Plan(BlockPos pos,Anchor anchor) {}
    private static volatile Data data;
    private static final ThreadLocal<Plan> placing=new ThreadLocal<>();
    private static final ConcurrentLinkedQueue<BlockPos> renderDirty=new ConcurrentLinkedQueue<>();
    private HostBlockAnchors() {}

    private static final Map<Block,Boolean> eligibleCache=new ConcurrentHashMap<>();
    /** Preview46: any plain full cube (vanilla or mod) whose model, shape and picking all
     *  follow the shared dy. Preview48 adds slabs, stairs, chests/barrels and TNT: each
     *  keeps its own shape moved by dy; chests render through a shifted block-entity
     *  renderer and primed TNT spawns at the shifted height. */
    public static boolean eligible(Block block){return eligibleCache.computeIfAbsent(block,HostBlockAnchors::supported);}
    private static boolean supported(Block block){
        if(block instanceof net.minecraft.world.level.block.SlabBlock||block instanceof net.minecraft.world.level.block.StairBlock
            ||block instanceof net.minecraft.world.level.block.ChestBlock||block instanceof net.minecraft.world.level.block.BarrelBlock
            ||block instanceof net.minecraft.world.level.block.TntBlock)return true;
        return plainFullCube(block)||singleCellModel(block);
    }
    /** Preview84 (user: props float or cannot be placed on constructions): any single-cell block drawn
     *  from a baked model (torches, flowers, carpets, fences, walls, panes, snow layers, mod decor) keeps its
     *  own shape and model moved by the construction dy. Still excluded: block entities other than the
     *  chest/barrel/TNT above, falling blocks (their entity would lose dy), fluids, and two-cell blocks
     *  whose second half is set outside placement (doors, beds, tall plants, pistons). */
    private static boolean singleCellModel(Block block){
        try{
            if(block instanceof net.minecraft.world.level.block.EntityBlock||block instanceof net.minecraft.world.level.block.FallingBlock
                ||block instanceof net.minecraft.world.level.block.DoorBlock||block instanceof net.minecraft.world.level.block.BedBlock
                ||block instanceof net.minecraft.world.level.block.DoublePlantBlock||block instanceof net.minecraft.world.level.block.piston.PistonBaseBlock
                ||block instanceof net.minecraft.world.level.block.piston.PistonHeadBlock||block instanceof net.minecraft.world.level.block.LiquidBlock
                ||block==net.minecraft.world.level.block.Blocks.BARRIER)return false;
            var state=block.defaultBlockState();
            return !state.isAir()&&state.getFluidState().isEmpty()&&state.getRenderShape()==net.minecraft.world.level.block.RenderShape.MODEL;
        }catch(RuntimeException e){return false;}
    }
    private static boolean plainFullCube(Block block){
        try{
            if(block instanceof net.minecraft.world.level.block.EntityBlock||block instanceof net.minecraft.world.level.block.FallingBlock
                ||block==net.minecraft.world.level.block.Blocks.BARRIER)return false;
            var state=block.defaultBlockState();
            if(state.isAir()||!state.getFluidState().isEmpty()||state.getRenderShape()!=net.minecraft.world.level.block.RenderShape.MODEL)return false;
            var empty=net.minecraft.world.level.EmptyBlockGetter.INSTANCE;
            return Block.isShapeFullBlock(state.getCollisionShape(empty,BlockPos.ZERO))&&Block.isShapeFullBlock(state.getShape(empty,BlockPos.ZERO));
        }catch(RuntimeException e){return false;}
    }
    private static Anchor anchor(BlockState state,BlockPos pos){
        Plan plan=placing.get();
        if(plan!=null&&plan.pos().equals(pos)&&state.is(plan.anchor().block()))return plan.anchor();
        Data store=data;Anchor a=store==null?null:store.anchors.get(pos);
        if(a!=null&&state.is(a.block()))return a;
        if(!pending.isEmpty()){
            var p=pending.get(pos);
            if(p!=null&&System.nanoTime()-p.time()<2_000_000_000L&&state.is(p.anchor().block()))return p.anchor();
        }
        return null;
    }
    /** Preview86 (BUG-09: jump-placing under the feet shoved the player): between the client's own
     *  placement and the server ledger entry, client collision used the unshifted cell and pushed the
     *  player out of it. The client keeps its placement anchor for up to 2 s. */
    private record Pending(Anchor anchor,long time) {}
    private static final Map<BlockPos,Pending> pending=new ConcurrentHashMap<>();
    public static boolean anchored(BlockState state,BlockPos pos){return anchor(state,pos)!=null;}
    public static double shift(BlockState state,BlockPos pos){var a=anchor(state,pos);return a==null?0:a.dy();}
    public static Vec3 offset(BlockState state,BlockPos pos){var a=anchor(state,pos);return a==null?Vec3.ZERO:a.offset();}
    /** -1 = not anchored; else the anchor kind (see Anchor). */
    public static int kind(BlockState state,BlockPos pos){var a=anchor(state,pos);return a==null?-1:a.kind();}
    /** Preview88: a block that fell lands centred where it fell (x/z kept) and rests on the native ground of the
     *  proxy below it, or snaps onto the anchored block below it; no floating after a fall. */
    public static void landed(ServerLevel level,BlockPos pos,BlockState state,double x,double z){
        Data store=data;if(store==null||!world(level)||!eligible(state.getBlock()))return;
        BlockPos below=pos.below();var under=anchor(level.getBlockState(below),below);
        Anchor value;
        if(under!=null)value=new Anchor(state.getBlock(),under.dx(),under.dy(),under.dz(),under.group(),1);
        else{
            double drop=WorldBridge.isHostProxy(below)?WorldBridge.surfaceDrop(below):0;
            double dx=Math.max(-.5,Math.min(.5,x-(pos.getX()+.5))),dz=Math.max(-.5,Math.min(.5,z-(pos.getZ()+.5)));
            if(Math.abs(dx)+Math.abs(drop)+Math.abs(dz)<.0005)return;
            value=new Anchor(state.getBlock(),dx,-drop,dz,UUID.randomUUID().toString().replace("-",""),WorldBridge.isHostProxy(below)?0:1);
        }
        if(store.anchors.size()>=2048&&!store.anchors.containsKey(pos))return;
        store.anchors.put(pos.immutable(),value);store.setDirty();renderDirty.add(pos.immutable());
        WorldBridge.changed(level,pos,level.getBlockState(pos));
        Passthrough.LOG.info("DQJ gravity: {} landed at {} offset {},{},{} kind {}",state.getBlock(),pos.toShortString(),value.dx(),value.dy(),value.dz(),value.kind());
    }
    /** Recently removed anchors: a TNT block is gone before wasExploded spawns its entity. */
    private record Removed(Vec3 offset,long time) {}
    private static final Map<BlockPos,Removed> removed=new ConcurrentHashMap<>();
    /** dy for an entity spawned from a block at pos: live anchor, else removed within 2 s. */
    public static Vec3 recentOffset(Level level,BlockPos pos){
        Data store=data;if(store==null||!world(level))return Vec3.ZERO;
        var a=store.anchors.get(pos);if(a!=null)return a.offset();
        var r=removed.get(pos);return r!=null&&level.getGameTime()-r.time()<=40?r.offset():Vec3.ZERO;
    }
    // Entity collision queries pass the LevelChunk, not the Level (BlockCollisions ->
    // getChunkForCollisions). Live48: without this, collision used the unshifted cell
    // while the model was drawn dy lower, so stacked blocks could be walked into.
    public static boolean world(BlockGetter level){
        if(level instanceof Level l)return l.dimension()==Level.OVERWORLD;
        return level instanceof net.minecraft.world.level.chunk.LevelChunk chunk&&chunk.getLevel().dimension()==Level.OVERWORLD;
    }
    public static VoxelShape shape(BlockState state,BlockGetter level,BlockPos pos){
        if(!world(level))return null;
        var a=anchor(state,pos);return a==null?null:Shapes.block().move(a.dx(),a.dy(),a.dz());
    }
    /** Live51 (BUGS-OPEN 03): BlockStateBase shape getters call each other
     *  (getCollisionShape(ctx) -> Block -> getShape -> getShape(ctx)), so shifting at
     *  every return stacked dy two or three times on the entity-collision path and
     *  sank collision below the ground. Shift only in the outermost getter. */
    private static final ThreadLocal<int[]> shapeDepth=ThreadLocal.withInitial(()->new int[1]);
    public static VoxelShape outermost(java.util.function.Supplier<VoxelShape> call,BlockState state,BlockGetter level,BlockPos pos){
        if(!hasAny())return call.get();
        int[] depth=shapeDepth.get();VoxelShape shape;
        depth[0]++;
        try{shape=call.get();}finally{depth[0]--;}
        return depth[0]==0?moved(shape,state,level,pos):shape;
    }
    /** Preview48: the block's own (vanilla/mod) shape moved by its construction dy. */
    public static VoxelShape moved(VoxelShape original,BlockState state,BlockGetter level,BlockPos pos){
        if(original==null||!hasAny()||!world(level))return original;
        var a=anchor(state,pos);return a==null||a.dx()==0&&a.dy()==0&&a.dz()==0||original.isEmpty()?original:original.move(a.dx(),a.dy(),a.dz());
    }
    public static boolean hasAny(){Data store=data;return placing.get()!=null||!pending.isEmpty()||store!=null&&!store.anchors.isEmpty();}
    public static BlockPos pollRenderDirty(){return renderDirty.poll();}
    public static void attach(MinecraftServer server){
        // DimensionDataStorage falls back to a new object if a reader throws.
        // Validate an existing ledger first, so corrupt data can never be
        // silently replaced by the next successful placement.
        var path=server.getWorldPath(net.minecraft.world.level.storage.LevelResource.ROOT).resolve("data/dqj_block_anchors.dat");
        if(java.nio.file.Files.exists(path))try{
            Data.load(net.minecraft.nbt.NbtIo.readCompressed(path,net.minecraft.nbt.NbtAccounter.unlimitedHeap()).getCompound("data"),server.overworld().registryAccess());
        }catch(Exception e){
            data=null;Passthrough.LOG.error("DQJ block anchors disabled: existing ledger could not be read; file preserved",e);return;
        }
        data=server.overworld().getDataStorage().computeIfAbsent(new SavedData.Factory<>(Data::new,Data::load,DataFixTypes.LEVEL),"dqj_block_anchors");
        renderDirty.addAll(data.anchors.keySet());
        Passthrough.LOG.info("DQJ anchored full blocks loaded: {}",data.anchors.size());
    }
    public static void detach(){data=null;placing.remove();renderDirty.clear();removed.clear();}
    public static void changed(ServerLevel level,BlockPos pos,BlockState state){
        Data store=data;if(store==null||level.dimension()!=Level.OVERWORLD)return;
        Anchor old=store.anchors.get(pos);if(old==null)return;
        if(!eligible(state.getBlock())){
            store.anchors.remove(pos);
            if(removed.size()>256)removed.entrySet().removeIf(e->level.getGameTime()-e.getValue().time()>40);
            removed.put(pos.immutable(),new Removed(old.offset(),level.getGameTime()));
        }
        else if(!state.is(old.block()))store.anchors.put(pos.immutable(),old.with(state.getBlock()));
        else return;
        store.setDirty();renderDirty.add(pos.immutable());
    }
    public static InteractionResult place(BlockItem item,BlockPlaceContext context,Supplier<InteractionResult> action){
        if(!world(context.getLevel())||data==null)return action.get();
        BlockPos pos=context.getClickedPos().immutable(),parent=pos.relative(context.getClickedFace().getOpposite());
        Anchor inherited=anchor(context.getLevel().getBlockState(parent),parent);
        // Merging a slab into an anchored slab (or re-placing the same block) keeps its own dy.
        Anchor existing=data.anchors.get(pos);
        if(existing!=null&&context.getLevel().getBlockState(pos).is(item.getBlock()))inherited=existing;
        if(!eligible(item.getBlock())){
            // Partial blocks/entities require a separate shape/render contract.
            // Do not create an unshifted child on a shifted construction.
            return inherited!=null?InteractionResult.FAIL:action.get();
        }
        double[] root=inherited==null?HostTerrainTarget.rootOffset(context):null;
        Anchor value=inherited!=null?(inherited==existing?inherited.with(item.getBlock()):inherited.child(item.getBlock())):
            root!=null&&Math.abs(root[0])+Math.abs(root[1])+Math.abs(root[2])>.0005?new Anchor(item.getBlock(),root[0],root[1],root[2],UUID.randomUUID().toString().replace("-",""),(int)root[3]):null;
        if(value==null)return action.get();
        // Preview85: an offset block reaches into neighbour cells; refuse (consume nothing) rather than
        // interpenetrate another construction or a plain MC block there.
        if(overlapsOthers(context.getLevel(),pos,value,item.getBlock().defaultBlockState()))return InteractionResult.FAIL;
        if(data.anchors.size()>=2048&&!data.anchors.containsKey(pos))return InteractionResult.FAIL;
        Plan old=placing.get();placing.set(new Plan(pos,value));
        try{
            InteractionResult result=action.get();
            if(context.getLevel().isClientSide&&result.consumesAction()){
                if(pending.size()>64)pending.entrySet().removeIf(e->System.nanoTime()-e.getValue().time()>2_000_000_000L);
                pending.put(pos,new Pending(value,System.nanoTime()));renderDirty.add(pos);
            }
            if(!context.getLevel().isClientSide&&result.consumesAction()&&context.getLevel().getBlockState(pos).is(value.block())){
                data.anchors.put(pos,value);data.setDirty();renderDirty.add(pos);
                // setBlock occurred while the provisional shape was active;
                // export after commit so snapshot and future updates agree too.
                WorldBridge.changed((ServerLevel)context.getLevel(),pos,context.getLevel().getBlockState(pos));
            }
            return result;
        }finally{if(old==null)placing.remove();else placing.set(old);}
    }
    private static boolean overlapsOthers(Level level,BlockPos pos,Anchor value,BlockState placed){
        if(Math.abs(value.dx())<1e-4&&Math.abs(value.dz())<1e-4&&value.dy()<=0)return false;
        VoxelShape own;
        try{own=placed.getCollisionShape(level,pos);if(own.isEmpty())own=placed.getShape(level,pos);}catch(RuntimeException e){own=Shapes.block();}
        if(own.isEmpty())return false;
        var box=own.bounds().move(pos).move(value.dx(),value.dy(),value.dz()).deflate(.01);
        for(var c:BlockPos.betweenClosed(BlockPos.containing(box.minX,box.minY,box.minZ),BlockPos.containing(box.maxX,box.maxY,box.maxZ))){
            if(c.equals(pos))continue;
            var state=level.getBlockState(c);
            if(state.isAir()||state.is(net.minecraft.world.level.block.Blocks.BARRIER)&&WorldBridge.isHostProxy(c))continue;
            var other=anchor(state,c);if(other!=null&&other.group().equals(value.group()))continue;
            var shape=state.getCollisionShape(level,c);if(shape.isEmpty())shape=state.getShape(level,c);
            for(var part:shape.toAabbs())if(part.move(c).intersects(box))return true;
        }
        return false;
    }
    public static BlockHitResult clip(BlockGetter level,ClipContext context,BlockHitResult original){
        Data store=data;if(store==null||!world(level))return original;
        Vec3 from=context.getFrom(),to=context.getTo();
        double distance=original.getType()==HitResult.Type.MISS?from.distanceToSqr(to)+.000001:from.distanceToSqr(original.getLocation());
        BlockHitResult closest=original;
        for(var e:store.anchors.entrySet()){
            var pos=e.getKey();
            if(pos.getX()<Math.min(from.x,to.x)-1||pos.getX()>Math.max(from.x,to.x)+1
                ||pos.getZ()<Math.min(from.z,to.z)-1||pos.getZ()>Math.max(from.z,to.z)+1
                ||pos.getY()<Math.min(from.y,to.y)-1||pos.getY()>Math.max(from.y,to.y)+2)continue;
            var state=level.getBlockState(pos);if(!state.is(e.getValue().block()))continue;
            var hit=context.getBlockShape(state,level,pos).clip(from,to,pos);
            if(hit!=null&&from.distanceToSqr(hit.getLocation())<distance){distance=from.distanceToSqr(hit.getLocation());closest=hit;}
        }
        return closest;
    }
    public static void addStandingCandidates(Level level,double x,double y,double z,java.util.List<Double> candidates){
        Data store=data;if(store==null||!world(level))return;
        for(var e:store.anchors.entrySet()){
            var p=e.getKey();var a=e.getValue();double top=p.getY()+1+a.dy(),px=p.getX()+a.dx(),pz=p.getZ()+a.dz();
            if(px+1>x-.299&&px<x+.299&&pz+1>z-.299&&pz<z+.299&&top>=y&&top<=y+2
                &&level.getBlockState(p).is(e.getValue().block()))candidates.add(top);
        }
    }
    public static boolean standingOnAnchor(Level level,double x,double y,double z){
        var tops=new java.util.ArrayList<Double>();addStandingCandidates(level,x,y-2,z,tops);
        return tops.stream().anyMatch(top->Math.abs(top-y)<.00001)&&GroundSpawn.supported(level,x,y,z);
    }
    public static JsonObject report(BlockPos pos){
        var out=new JsonObject();Data store=data;out.addProperty("count",store==null?0:store.anchors.size());
        if(store!=null&&pos!=null){var a=store.anchors.get(pos);if(a!=null){out.addProperty("dx",a.dx());out.addProperty("dy",a.dy());out.addProperty("dz",a.dz());out.addProperty("group",a.group());
            out.addProperty("block",BuiltInRegistries.BLOCK.getKey(a.block()).toString());}}
        return out;
    }
    private static final class Data extends SavedData {
        final Map<BlockPos,Anchor> anchors=new ConcurrentHashMap<>();
        static Data load(CompoundTag tag,HolderLookup.Provider lookup){
            var result=new Data();var entries=tag.getList("anchors",Tag.TAG_COMPOUND);
            // Version 2 only widens dy to [-2,0] for slope roots (Preview46); a ledger
            // without such roots is still written as version 1 for older JAR rollback.
            // Version 3 (Preview85) adds dx/dz in [-1,1] and lets wall roots move up to +1.
            int version=tag.getInt("version");
            if(version<1||version>3)throw new IllegalArgumentException("Unsupported DQJ block anchor data version");
            double minDy=version>=2?-2:-1,maxDy=version==3?1:.001;
            if(entries.size()>2048)throw new IllegalArgumentException("DQJ block anchor capacity exceeded");
            for(var entry:entries){var e=(CompoundTag)entry;var id=ResourceLocation.tryParse(e.getString("block"));
                if(id==null)throw new IllegalArgumentException("Invalid DQJ block anchor id");
                var block=BuiltInRegistries.BLOCK.getValue(id);double dy=e.getDouble("dy"),dx=version==3?e.getDouble("dx"):0,dz=version==3?e.getDouble("dz"):0;String group=e.getString("group");
                if(!eligible(block)||!Double.isFinite(dx+dy+dz)||dy<minDy||dy>maxDy||Math.abs(dx)>1||Math.abs(dz)>1||!group.matches("[a-f0-9]{32}"))throw new IllegalArgumentException("Invalid DQJ block anchor entry");
                var pos=new BlockPos(e.getInt("x"),e.getInt("y"),e.getInt("z"));
                if(pos.getY()< -50||pos.getY()>300||Math.abs((long)pos.getX())>29_999_900||Math.abs((long)pos.getZ())>29_999_900
                    ||result.anchors.putIfAbsent(pos,new Anchor(block,dx,dy,dz,group,Math.max(0,Math.min(2,e.getInt("kind")))))!=null)throw new IllegalArgumentException("Invalid or duplicate DQJ block anchor position");
            }
            return result;
        }
        @Override public CompoundTag save(CompoundTag tag,HolderLookup.Provider lookup){
            // Older versions are still written when possible, so a JAR rollback can read the ledger.
            boolean flat=anchors.values().stream().anyMatch(a->a.dx()!=0||a.dz()!=0||a.dy()>.001);
            boolean deep=anchors.values().stream().anyMatch(a->a.dy()< -1);
            tag.putInt("version",flat?3:deep?2:1);var entries=new ListTag();
            for(var pair:anchors.entrySet()){var pos=pair.getKey();var a=pair.getValue();var e=new CompoundTag();
                e.putInt("x",pos.getX());e.putInt("y",pos.getY());e.putInt("z",pos.getZ());e.putDouble("dy",a.dy());if(flat){e.putDouble("dx",a.dx());e.putDouble("dz",a.dz());}if(a.kind()!=0)e.putInt("kind",a.kind());e.putString("group",a.group());
                e.putString("block",BuiltInRegistries.BLOCK.getKey(a.block()).toString());entries.add(e);}
            tag.put("anchors",entries);return tag;
        }
    }
}
