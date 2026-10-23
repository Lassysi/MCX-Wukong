package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.CombatBridge;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.GameRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** C1: before the per-frame crosshair pick, move client proxies to the server's latest
 *  host position (same JVM), so melee/Better Combat target where the enemy is drawn. */
@Mixin(GameRenderer.class)
abstract class HostProxySnapMixin {
    @Inject(method="pick(F)V",at=@At("HEAD"))
    private void dqj$snapProxies(float partial,CallbackInfo ci){
        var level=Minecraft.getInstance().level;
        if(level==null||!CombatBridge.hasPositions())return;
        for(var entity:level.entitiesForRendering()){
            if(entity.isPassenger())continue;
            double[] p=CombatBridge.position(entity.getUUID());
            if(p!=null){entity.setPos(p[0],p[1],p[2]);entity.setOldPosAndRot();entity.blocksBuilding=false;} // Preview126: client copies too
        }
    }
}
