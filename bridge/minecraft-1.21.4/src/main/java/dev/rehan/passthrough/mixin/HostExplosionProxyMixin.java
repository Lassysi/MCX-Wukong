package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.WorldBridge;
import java.util.List;
import java.util.Optional;
import net.minecraft.core.BlockPos;
import net.minecraft.world.level.BlockGetter;
import net.minecraft.world.level.Explosion;
import net.minecraft.world.level.ExplosionDamageCalculator;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.level.material.FluidState;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Preview69 (live68: TNT did not chain): host terrain proxy barriers have barrier blast
 *  resistance and stopped explosion rays before nearby TNT. Proxies now absorb nothing. */
@Mixin(ExplosionDamageCalculator.class)
abstract class HostExplosionProxyMixin {
    @Inject(method="getBlockExplosionResistance",at=@At("HEAD"),cancellable=true)
    private void dqj$proxyResistance(Explosion explosion,BlockGetter level,BlockPos pos,BlockState state,FluidState fluid,CallbackInfoReturnable<Optional<Float>> ci){
        if(WorldBridge.isHostProxy(pos))ci.setReturnValue(Optional.empty());
    }
}
