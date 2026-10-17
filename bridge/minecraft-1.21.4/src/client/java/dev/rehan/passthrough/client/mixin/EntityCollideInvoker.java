package dev.rehan.passthrough.client.mixin;

import net.minecraft.world.entity.Entity;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Invoker;

/** Read-only probe access to vanilla Entity.collide (block collision plus step-up). */
@Mixin(Entity.class)
public interface EntityCollideInvoker {
    @Invoker("collide") Vec3 dqj$collide(Vec3 movement);
}
