package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.TerrainLease;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.ModifyVariable;

@Mixin(Entity.class)
abstract class HostTerrainLeaseMixin {
    @ModifyVariable(method="move",at=@At("HEAD"),argsOnly=true)
    private Vec3 dqj$streamedSupport(Vec3 movement){return TerrainLease.beforeMove((Entity)(Object)this,movement);}
}
