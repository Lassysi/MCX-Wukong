package dev.rehan.passthrough.client.mixin;

import net.minecraft.client.renderer.ItemInHandRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;

@Mixin(ItemInHandRenderer.class)
public interface ItemInHandAccess {
    @Accessor("mainHandHeight") float dqj$mainHeight();
    @Accessor("mainHandHeight") void dqj$setMainHeight(float value);
    @Accessor("oMainHandHeight") void dqj$setOldMainHeight(float value);
}
