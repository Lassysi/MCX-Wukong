package dev.rehan.passthrough;

import java.nio.file.Files;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerLifecycleEvents;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerTickEvents;
import net.fabricmc.fabric.api.networking.v1.ServerPlayConnectionEvents;
import net.minecraft.world.level.storage.LevelResource;

/** Hold only the installer-created empty world until the host commits measured ground.
 * No inventory grants, automatic world entry, terrain removal or player teleport. */
public final class PlayerWorldGuard {
    private static boolean marked;
    private PlayerWorldGuard() {}
    public static void register() {
        ServerLifecycleEvents.SERVER_STARTED.register(server ->
            marked=Files.isRegularFile(server.getWorldPath(LevelResource.ROOT).resolve("mcx-world.json")));
        ServerLifecycleEvents.SERVER_STOPPING.register(server -> marked=false);
        ServerPlayConnectionEvents.JOIN.register((handler,sender,server) -> {
            if(marked){handler.player.setNoGravity(true);handler.player.setDeltaMovement(0,0,0);handler.player.fallDistance=0;}
        });
        ServerTickEvents.END_SERVER_TICK.register(server -> {
            if(!marked)return;
            boolean waiting=!WorldBridge.groundCommitted||!Passthrough.active;
            for(var player:server.getPlayerList().getPlayers()) {
                player.setNoGravity(waiting);
                if(waiting){player.setDeltaMovement(0,0,0);player.fallDistance=0;}
            }
        });
    }
}
