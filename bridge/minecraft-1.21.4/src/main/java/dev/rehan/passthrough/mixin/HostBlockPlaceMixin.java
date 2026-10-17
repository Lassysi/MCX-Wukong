package dev.rehan.passthrough.mixin;

import com.llamalad7.mixinextras.injector.wrapmethod.WrapMethod;
import com.llamalad7.mixinextras.injector.wrapoperation.Operation;
import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.world.InteractionResult;
import net.minecraft.world.item.BlockItem;
import net.minecraft.world.item.context.BlockPlaceContext;
import org.spongepowered.asm.mixin.Mixin;

@Mixin(BlockItem.class)
abstract class HostBlockPlaceMixin {
    @WrapMethod(method="place")
    private InteractionResult dqj$anchor(BlockPlaceContext context,Operation<InteractionResult> original){
        return HostBlockAnchors.place((BlockItem)(Object)this,context,()->original.call(context));
    }
}
