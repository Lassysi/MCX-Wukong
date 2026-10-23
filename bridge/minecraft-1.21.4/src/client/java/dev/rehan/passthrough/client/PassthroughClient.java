package dev.rehan.passthrough.client;

import net.fabricmc.api.ClientModInitializer;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;
import dev.rehan.passthrough.CombatBridge;
import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.world.entity.LivingEntity;

public final class PassthroughClient implements ClientModInitializer {
    private static int mineTicks;
    private static boolean attackWas;
    @Override public void onInitializeClient() {
        BridgeRenderDistance.register();
        ItemToasts.register(); // Preview106: right-side pickup list
        HostLink.launch();
        ClientTickEvents.END_CLIENT_TICK.register(mc -> {
            HostState.refresh();
            StuckWatchdog.tick(mc); // Preview127
            NativeBlocks.tick(mc);NativeBlocks.logUse(mc);
            dev.rehan.passthrough.Passthrough.useHeld=mc.screen==null&&mc.player!=null&&mc.options.keyUse.isDown();
            // Preview107: holding attack with a pickaxe swings at Wukong's destructible scenery (host counts swings by tier).
            if(mc.screen==null&&mc.player!=null&&mc.options.keyAttack.isDown()&&dev.rehan.passthrough.Passthrough.active){
                var tool=net.minecraft.core.registries.BuiltInRegistries.ITEM.getKey(mc.player.getMainHandItem().getItem()).getPath();
                // Live112 polish: any other swing (press, not hold) can break pots/jars and light scenery.
                if(!attackWas&&!tool.endsWith("_pickaxe"))dev.rehan.passthrough.Passthrough.events.accept("{\"t\":\"swing\",\"tool\":\""+tool+"\"}");
                if(tool.endsWith("_pickaxe")&&++mineTicks%8==1){
                    // Live112: Efficiency level goes with the swing (host: fewer swings needed).
                    int eff=mc.level.registryAccess().lookupOrThrow(net.minecraft.core.registries.Registries.ENCHANTMENT).get(net.minecraft.world.item.enchantment.Enchantments.EFFICIENCY)
                        .map(h->net.minecraft.world.item.enchantment.EnchantmentHelper.getItemEnchantmentLevel(h,mc.player.getMainHandItem())).orElse(0);
                    dev.rehan.passthrough.Passthrough.events.accept("{\"t\":\"mine\",\"tool\":\""+tool+"\",\"eff\":"+eff+"}");
                }
            }else mineTicks=0;
            attackWas=mc.screen==null&&mc.player!=null&&mc.options.keyAttack.isDown();
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
