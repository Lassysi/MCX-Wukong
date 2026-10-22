package dev.rehan.passthrough;

import java.lang.reflect.Constructor;
import java.lang.reflect.Field;
import java.util.Map;
import net.fabricmc.fabric.api.gametest.v1.FabricGameTest;
import net.minecraft.core.BlockPos;
import net.minecraft.gametest.framework.GameTest;
import net.minecraft.gametest.framework.GameTestHelper;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.entity.item.FallingBlockEntity;
import net.minecraft.world.level.block.Blocks;
import net.minecraft.world.level.block.Block;
import net.minecraft.world.phys.AABB;

/** Real collision/anchor/physics regression; isolated GameTest world, no user world or connection. */
public final class BlockSupportTests implements FabricGameTest {
    private static void anchor(BlockPos pos,Block block,double dx,double dy,double dz,int kind) {
        try {
            Field data=HostBlockAnchors.class.getDeclaredField("data");data.setAccessible(true);Object store=data.get(null);
            Field anchors=store.getClass().getDeclaredField("anchors");anchors.setAccessible(true);
            Class<?> type=Class.forName("dev.rehan.passthrough.HostBlockAnchors$Anchor");
            Constructor<?> ctor=type.getDeclaredConstructors()[0];ctor.setAccessible(true);
            ((Map<BlockPos,Object>)anchors.get(store)).put(pos,ctor.newInstance(block,dx,dy,dz,"11111111111111111111111111111111",kind));
        } catch(ReflectiveOperationException e){throw new RuntimeException(e);}
    }
    private static boolean supported(ServerLevel level,BlockPos p){
        try{
            for(var method:BlockGravity.class.getDeclaredMethods())if(method.getName().equals("supported")){
                method.setAccessible(true);
                Object third=method.getParameterTypes()[2]==net.minecraft.world.level.block.state.BlockState.class
                    ?level.getBlockState(p):HostBlockAnchors.offset(level.getBlockState(p),p);
                return (boolean)method.invoke(null,level,p,third);
            }
            throw new IllegalStateException("Support consumer missing");
        }catch(ReflectiveOperationException e){throw new RuntimeException(e);}
    }

    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=100)
    public void undersideSupportAndFalling(GameTestHelper h) {
        var level=h.getLevel();
        var base=h.absolutePos(new BlockPos(2,40,2));
        Passthrough.active=Passthrough.minecraftAuthority=true;WorldBridge.groundCommitted=true;
        // Excluded bedrock remains a stable catcher for every test column.
        for(int x=-2;x<=12;x++)for(int z=-2;z<=4;z++)level.setBlock(base.offset(x,-2,z),Blocks.BEDROCK.defaultBlockState(),3);
        var root=base;var adjacent=base.east();var separated=base.east(3);
        for(var p:new BlockPos[]{root,adjacent,separated})level.setBlock(p,Blocks.OAK_PLANKS.defaultBlockState(),3);
        for(var p:new BlockPos[]{root,adjacent,separated})level.setBlock(p.below(),Blocks.OAK_SLAB.defaultBlockState(),3);
        anchor(root,Blocks.OAK_PLANKS,.3,-.4,.2,0);
        anchor(adjacent,Blocks.OAK_PLANKS,.3,-.4,.2,1);
        anchor(separated,Blocks.OAK_PLANKS,.3,-.4,.2,1);
        // The old probe saw an occupied slab cell despite the gap above its actual collision.
        h.assertFalse(supported(level,adjacent),"touching the same construction sideways must not support an overhang");
        h.assertFalse(supported(level,separated),"separated and side-connected overhangs obey the same rule");
        h.assertFalse(supported(level,root),"legacy terrain-root kind is not permanent anti-gravity");

        var onFull=base.east(5);level.setBlock(onFull,Blocks.OAK_PLANKS.defaultBlockState(),3);level.setBlock(onFull.below(),Blocks.BEDROCK.defaultBlockState(),3);
        h.assertTrue(supported(level,onFull),"a full block directly below supports the block");
        var gap=base.east(7);level.setBlock(gap,Blocks.OAK_PLANKS.defaultBlockState(),3);
        level.setBlock(gap.below(),Blocks.OAK_SLAB.defaultBlockState(),3);
        h.assertFalse(supported(level,gap),"a lower slab with a half-block gap is not support");
        var onShift=base.east(9);level.setBlock(onShift,Blocks.OAK_PLANKS.defaultBlockState(),3);
        level.setBlock(onShift.below(),Blocks.OAK_PLANKS.defaultBlockState(),3);
        anchor(onShift,Blocks.OAK_PLANKS,.25,-.4,0,1);anchor(onShift.below(),Blocks.OAK_PLANKS,.25,-.4,0,1);
        h.assertTrue(supported(level,onShift),"matching shifted blocks stacked vertically retain support");
        var side=base.east(11);level.setBlock(side,Blocks.OAK_PLANKS.defaultBlockState(),3);
        level.setBlock(side.east(),Blocks.OAK_PLANKS.defaultBlockState(),3);level.setBlock(side.below(),Blocks.AIR.defaultBlockState(),3);
        h.assertFalse(supported(level,side),"a flush unshifted side neighbour is not support");
        var terrainRoot=base.offset(7,0,3);var falseTerrain=base.offset(10,0,3);
        try{
            Field field=WorldBridge.class.getDeclaredField("surfaces");field.setAccessible(true);
            var surfaces=(Map<BlockPos,Integer>)field.get(null);
            for(var p:new BlockPos[]{terrainRoot,falseTerrain}){
                level.setBlock(p.below(),Blocks.BARRIER.defaultBlockState(),3);surfaces.put(p.below(),350);
                level.setBlock(p,Blocks.OAK_PLANKS.defaultBlockState(),3);
            }
        }catch(ReflectiveOperationException e){throw new RuntimeException(e);}
        anchor(terrainRoot,Blocks.OAK_PLANKS,0,-.65,0,0);anchor(falseTerrain,Blocks.OAK_PLANKS,0,-.4,0,1);
        h.assertTrue(supported(level,terrainRoot),"a terrain root touching measured partial-height native ground stays supported");
        h.assertFalse(supported(level,falseTerrain),"a proxy in the cell below does not support a block floating above its surface");

        // Tick the actual gravity consumer, rather than only asserting the support helper.
        try{var q=BlockGravity.class.getDeclaredField("queue");q.setAccessible(true);((java.util.Set<?>)q.get(null)).clear();}
        catch(ReflectiveOperationException e){throw new RuntimeException(e);}
        BlockGravity.changed(level,root);BlockGravity.changed(level,adjacent);BlockGravity.changed(level,separated);
        for(int tick=0;tick<8;tick++)BlockGravity.tick(level.getServer());
        h.assertTrue(level.getBlockState(adjacent).isAir(),"side-connected unsupported block becomes a falling entity");
        h.assertTrue(level.getBlockState(separated).isAir(),"separated unsupported block becomes a falling entity");
        h.assertTrue(level.getBlockState(root).isAir(),"legacy root without underside contact also falls");
        var testBox=new AABB(base.getX()-1,base.getY()-2,base.getZ()-1,base.getX()+5,base.getY()+3,base.getZ()+4);
        h.assertFalse(level.getEntitiesOfClass(FallingBlockEntity.class,testBox).isEmpty(),"gravity creates real falling entities");
        h.runAtTickTime(40,()->{
            h.assertTrue(level.getEntitiesOfClass(FallingBlockEntity.class,testBox).isEmpty(),"falling blocks settle on measured collision");
            Passthrough.active=Passthrough.minecraftAuthority=false;WorldBridge.groundCommitted=false;h.succeed();
        });
    }
}
