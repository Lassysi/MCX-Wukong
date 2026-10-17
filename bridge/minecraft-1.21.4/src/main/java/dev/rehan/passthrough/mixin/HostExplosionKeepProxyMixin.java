package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.WorldBridge;
import java.util.List;
import net.minecraft.core.BlockPos;
import net.minecraft.world.level.ServerExplosion;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Preview69: explosion rays pass through host terrain proxies, but the proxies themselves
 *  (host ground stand-ins) are never destroyed or dropped. */
@Mixin(ServerExplosion.class)
abstract class HostExplosionKeepProxyMixin {
    @Inject(method="interactWithBlocks",at=@At("HEAD"))
    private void dqj$keepProxies(List<BlockPos> positions,CallbackInfo ci){
        try{positions.removeIf(WorldBridge::isHostProxy);}catch(UnsupportedOperationException ignored){}
    }
}
