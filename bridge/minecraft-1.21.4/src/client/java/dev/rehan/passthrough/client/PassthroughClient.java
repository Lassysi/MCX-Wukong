package dev.rehan.passthrough.client;

import net.fabricmc.api.ClientModInitializer;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;
import dev.rehan.passthrough.CombatBridge;
import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.world.entity.LivingEntity;

public final class PassthroughClient implements ClientModInitializer {
    @Override public void onInitializeClient() {
        BridgeRenderDistance.register();
        HostLink.launch();
        ClientTickEvents.END_CLIENT_TICK.register(mc -> {
            HostState.refresh();
            NativeBlocks.tick(mc);NativeBlocks.logUse(mc);
            dev.rehan.passthrough.Passthrough.useHeld=mc.screen==null&&mc.player!=null&&mc.options.keyUse.isDown();
            net.minecraft.core.BlockPos dirty;
            while((dirty=HostBlockAnchors.pollRenderDirty())!=null)if(mc.level!=null&&HostBlockAnchors.world(mc.level))
                mc.levelRenderer.setBlocksDirty(dirty.getX()-1,dirty.getY()-1,dirty.getZ()-1,dirty.getX()+1,dirty.getY()+1,dirty.getZ()+1);
            // Spawn packets set position before UUID. Refresh once ownership/size
            // arrives; the server hitbox alone cannot fix the client's target search.
            if(mc.level!=null)for(var entity:mc.level.entitiesForRendering())if(entity instanceof LivingEntity living){
                var expected=CombatBridge.dimensions(living);
                if(expected!=null&&(Math.abs(living.getBbWidth()-expected.width())>.001||Math.abs(living.getBbHeight()-expected.height())>.001))living.refreshDimensions();
            }
        });
    }
}
