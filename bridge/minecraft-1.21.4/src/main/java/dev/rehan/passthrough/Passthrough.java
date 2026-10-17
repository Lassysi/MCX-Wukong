package dev.rehan.passthrough;

import java.util.function.Consumer;
import net.fabricmc.api.ModInitializer;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerLifecycleEvents;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerTickEvents;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public final class Passthrough implements ModInitializer {
    public static final Logger LOG = LoggerFactory.getLogger("dqjmcbridge");
    public static volatile boolean active, minecraftAuthority;
    public static volatile Consumer<String> events = message -> {};
    /** Preview82: client use key held in gameplay (no screen), written by the client tick of the
     *  integrated client; the server shield rule reads it because isUsingItem can drop for a few ticks. */
    public static volatile boolean useHeld;
    @Override public void onInitialize() {
        CombatBridge.register();
        Allies.register();
        ServerLifecycleEvents.SERVER_STARTED.register(WorldBridge::attach);
        ServerLifecycleEvents.SERVER_STOPPING.register(WorldBridge::detach);
        ServerTickEvents.END_SERVER_TICK.register(WorldBridge::tick);
        PlayerWorldGuard.register();
    }
}
