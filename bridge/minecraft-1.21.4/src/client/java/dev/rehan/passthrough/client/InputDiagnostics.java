package dev.rehan.passthrough.client;

import dev.rehan.passthrough.HostBlockAnchors;
import dev.rehan.passthrough.Passthrough;
import net.minecraft.client.Minecraft;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.world.phys.BlockHitResult;
import net.minecraft.world.phys.HitResult;

/** Preview48 diagnostics (BUG-2026-10-16-02): what the attack button actually targets. */
final class InputDiagnostics {
    private InputDiagnostics() {}
    static void attack(Minecraft mc,boolean down){
        try{
            HitResult hit=mc.hitResult;String target="none";
            if(hit instanceof BlockHitResult b&&hit.getType()==HitResult.Type.BLOCK&&mc.level!=null){
                var state=mc.level.getBlockState(b.getBlockPos());
                target="block "+BuiltInRegistries.BLOCK.getKey(state.getBlock())+" at "+b.getBlockPos().toShortString()
                    +" face="+b.getDirection().getName()+" anchorDy="+HostBlockAnchors.shift(state,b.getBlockPos());
            }else if(hit!=null)target=hit.getType().name();
            Passthrough.LOG.info("DQJ input-diag attack down={} target={} mode={} authority={} grabbed={} destroying={}",down,target,
                mc.gameMode==null?"none":mc.gameMode.getPlayerMode().getName(),Passthrough.minecraftAuthority,
                mc.mouseHandler.isMouseGrabbed(),mc.gameMode!=null&&mc.gameMode.isDestroying());
        }catch(RuntimeException e){Passthrough.LOG.warn("DQJ input-diag attack failed",e);}
    }
}
