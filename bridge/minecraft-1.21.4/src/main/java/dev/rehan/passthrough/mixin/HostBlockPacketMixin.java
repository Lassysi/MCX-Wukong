package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.network.protocol.game.ServerboundUseItemOnPacket;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.server.network.ServerGamePacketListenerImpl;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Redirect;

@Mixin(ServerGamePacketListenerImpl.class)
abstract class HostBlockPacketMixin {
    @Shadow public ServerPlayer player;
    @Redirect(method="handleUseItemOn",at=@At(value="INVOKE",target="Lnet/minecraft/world/phys/Vec3;subtract(Lnet/minecraft/world/phys/Vec3;)Lnet/minecraft/world/phys/Vec3;"))
    private Vec3 dqj$canonicalBounds(Vec3 point,Vec3 center,ServerboundUseItemOnPacket packet){
        var pos=packet.getHitResult().getBlockPos();
        var offset=HostBlockAnchors.world(player.level())?HostBlockAnchors.offset(player.level().getBlockState(pos),pos):Vec3.ZERO;
        return point.subtract(center).subtract(offset);
    }
}
