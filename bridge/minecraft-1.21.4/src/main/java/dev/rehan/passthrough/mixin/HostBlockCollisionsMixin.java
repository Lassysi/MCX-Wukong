package dev.rehan.passthrough.mixin;

import com.llamalad7.mixinextras.sugar.Local;
import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.level.BlockCollisions;
import net.minecraft.world.phys.shapes.CollisionContext;
import net.minecraft.world.phys.shapes.EntityCollisionContext;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.ModifyArg;

/** Preview50 (BUGS-OPEN 03): vanilla only checks the outer cell layer for "large"
 *  shapes and skips edge/corner cells, because a vanilla shape never reaches more
 *  than one cell into a neighbour. Anchored blocks are drawn up to 2 cells lower
 *  (dy in [-2,0]), so a block above the player's head and one column over sat on a
 *  skipped edge cell and could be walked into. For players, extend the cursor two
 *  cells up so those cells become face/interior cells; shapes still decide contact. */
@Mixin(BlockCollisions.class)
abstract class HostBlockCollisionsMixin {
    @ModifyArg(method="<init>(Lnet/minecraft/world/level/CollisionGetter;Lnet/minecraft/world/phys/shapes/CollisionContext;Lnet/minecraft/world/phys/AABB;ZLjava/util/function/BiFunction;)V",
        at=@At(value="INVOKE",target="Lnet/minecraft/core/Cursor3D;<init>(IIIIII)V"),index=4)
    private int dqj$reachShiftedAnchors(int endY,@Local(argsOnly=true) CollisionContext context){
        if(!HostBlockAnchors.hasAny())return endY;
        return context instanceof EntityCollisionContext entity&&entity.getEntity() instanceof Player?endY+2:endY;
    }
}
