package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.CombatBridge;
import dev.rehan.passthrough.Passthrough;
import net.minecraft.world.level.entity.EntitySectionStorage;
import net.minecraft.world.phys.AABB;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.ModifyArg;

/** Vanilla broad phase assumes a centre within 2 m horizontally. Our visible-body proxies can be wider.
 * Only expand section discovery: callbacks retain the original AABB, predicates and narrow-phase ray test. */
@Mixin(EntitySectionStorage.class)
abstract class LargeProxyQueryMixin {
    @ModifyArg(method={"getEntities(Lnet/minecraft/world/phys/AABB;Lnet/minecraft/util/AbortableIterationConsumer;)V",
        "getEntities(Lnet/minecraft/world/level/entity/EntityTypeTest;Lnet/minecraft/world/phys/AABB;Lnet/minecraft/util/AbortableIterationConsumer;)V"},
        at=@At(value="INVOKE",target="Lnet/minecraft/world/level/entity/EntitySectionStorage;forEachAccessibleNonEmptySection(Lnet/minecraft/world/phys/AABB;Lnet/minecraft/util/AbortableIterationConsumer;)V"),index=0,require=2)
    private AABB dqj$largeSections(AABB box){
        return Passthrough.active&&CombatBridge.hasPositions()?box.inflate(8,32,8):box;
    }
}
