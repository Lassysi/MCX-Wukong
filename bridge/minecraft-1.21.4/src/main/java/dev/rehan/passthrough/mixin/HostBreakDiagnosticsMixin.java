package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.HostBlockAnchors;
import dev.rehan.passthrough.Passthrough;
import net.minecraft.core.BlockPos;
import net.minecraft.core.Direction;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.network.protocol.game.ServerboundPlayerActionPacket;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.server.level.ServerPlayerGameMode;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Preview48 diagnostics (BUG-2026-10-16-02): every block break request the server sees. */
@Mixin(ServerPlayerGameMode.class)
abstract class HostBreakDiagnosticsMixin {
    @Shadow protected ServerLevel level;
    @Shadow public abstract net.minecraft.world.level.GameType getGameModeForPlayer();
    @Inject(method="handleBlockBreakAction",at=@At("HEAD"))
    private void dqj$break(BlockPos pos,ServerboundPlayerActionPacket.Action action,Direction face,int maxBuildHeight,int sequence,CallbackInfo ci){
        try{
            var state=level.getBlockState(pos);
            Passthrough.LOG.info("DQJ break-diag action={} block={} at {} anchorDy={} mode={} seq={}",action,
                BuiltInRegistries.BLOCK.getKey(state.getBlock()),pos.toShortString(),HostBlockAnchors.shift(state,pos),getGameModeForPlayer().getName(),sequence);
        }catch(RuntimeException e){Passthrough.LOG.warn("DQJ break-diag failed",e);}
    }
}
