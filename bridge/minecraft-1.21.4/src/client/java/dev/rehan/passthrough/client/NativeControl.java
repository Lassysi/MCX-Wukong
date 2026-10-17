package dev.rehan.passthrough.client;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.WorldBridge;
import dev.rehan.passthrough.client.mixin.MouseHandlerAccess;
import dev.rehan.passthrough.client.mixin.ContainerScreenAccess;
import dev.rehan.passthrough.client.mixin.KeyMappingAccessor;
import net.minecraft.client.gui.screens.inventory.AbstractContainerScreen;
import dev.rehan.passthrough.CombatBridge;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.screens.inventory.InventoryScreen;
import net.minecraft.client.gui.screens.PauseScreen;
import net.minecraft.core.BlockPos;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.core.component.DataComponents;
import org.java_websocket.WebSocket;

/** Input transport and observation only: all movement and item rules remain vanilla. */
public final class NativeControl {
    private static double dx, dy;
    private static long lastTurn;
    private static long renderFrame;
    private static boolean owned;
    private static boolean originalToggleSprint;
    private static boolean wasSprinting;
    public static long sprintPresses,sprintLatchResets;
    public static boolean transportTurn;
    public static boolean transportScroll;
    public static boolean transportPointer,transportButton;
    public static boolean shiftHeld,controlHeld;
    private static long guiPointerEvents,guiButtonPresses;
    private static int guiPointerWidth,guiPointerHeight;
    private static double guiPointerNx,guiPointerNy,guiPointerX,guiPointerY;
    public static long weatherPassesSuppressed, rainParticleTicksSuppressed;
    public static long skyPassesSuppressed,cloudPassesSuppressed,proxyRendersSuppressed,scrollEvents;

    private NativeControl() {}
    static void pointer(double nx,double ny){
        if(!Double.isFinite(nx)||!Double.isFinite(ny))return;
        var mc=Minecraft.getInstance();if(mc.screen==null)return;
        var access=(MouseHandlerAccess)mc.mouseHandler;
        var window=mc.getWindow();long handle=window.getWindow();
        // Use the same logical extent vanilla onPress/drag/render divides by.
        // GLFW's live size is0x0 when minimized, while this renderer still runs.
        int w=window.getScreenWidth(),h=window.getScreenHeight();if(w<=0||h<=0)return;
        guiPointerNx=Math.clamp(nx,0,1);guiPointerNy=Math.clamp(ny,0,1);
        guiPointerWidth=w;guiPointerHeight=h;guiPointerX=guiPointerNx*w;guiPointerY=guiPointerNy*h;
        try{
            transportPointer=true;access.dqj$setIgnoreFirstMove(false);
            access.dqj$move(handle,guiPointerX,guiPointerY);guiPointerEvents++;
            // onMove normally accumulates movement for this original method.
            // Background MC otherwise skips mouseMoved and mouseDragged entirely.
            mc.mouseHandler.handleAccumulatedMovement();
        }finally{transportPointer=false;}
    }
    static void button(int button,boolean down){
        var mc=Minecraft.getInstance();
        if(down)guiButtonPresses++;
        try{transportButton=true;((MouseHandlerAccess)mc.mouseHandler).dqj$press(mc.getWindow().getWindow(),button,
                down?org.lwjgl.glfw.GLFW.GLFW_PRESS:org.lwjgl.glfw.GLFW.GLFW_RELEASE,0);}
        finally{transportButton=false;}
    }
    static void releaseInput(){
        var mc=Minecraft.getInstance();
        wasSprinting=false;
        shiftHeld=controlHeld=false;
        for(var key:new net.minecraft.client.KeyMapping[]{mc.options.keyUp,mc.options.keyDown,mc.options.keyLeft,mc.options.keyRight,
                mc.options.keyJump,mc.options.keyShift,mc.options.keySprint,mc.options.keyAttack,mc.options.keyUse,mc.options.keyPickItem,
                mc.options.keyInventory,mc.options.keyDrop,mc.options.keySwapOffhand,mc.options.keyTogglePerspective}){
            var access=(KeyMappingAccessor)key;access.dqj$clearDown(false);access.passthrough$setClickCount(0);
        }
        if(mc.screen!=null)for(int button=0;button<3;button++)button(button,false);
        if(mc.player!=null)mc.player.setSprinting(false);
    }

    static void sprintKey(boolean down){
        // Physical press state is separate from ToggleKeyMapping's latched
        // state. OS repeats or a duplicated transition must not toggle twice.
        if(controlHeld==down)return;
        controlHeld=down;
        var mc=Minecraft.getInstance();if(mc.screen!=null)return;
        var key=mc.options.keySprint;
        if(down){
            sprintPresses++;
            var access=(KeyMappingAccessor)key;
            access.passthrough$setClickCount(access.passthrough$getClickCount()+1);
        }
        key.setDown(down);
        if(down&&Passthrough.minecraftAuthority&&mc.options.toggleSprint().get()&&!key.isDown()&&mc.player!=null)
            mc.player.setSprinting(false);
    }
    private static void clearSprintLatch(Minecraft mc){
        if(mc.options.keySprint.isDown())sprintLatchResets++;
        var key=(KeyMappingAccessor)mc.options.keySprint;
        key.dqj$clearDown(false);key.passthrough$setClickCount(0);
    }
    static void forwardReleased(){
        var mc=Minecraft.getInstance();
        if(!Passthrough.minecraftAuthority||!mc.options.toggleSprint().get())return;
        clearSprintLatch(mc);wasSprinting=false;
        if(mc.player!=null)mc.player.setSprinting(false);
    }
    public static void afterMovementTick(net.minecraft.client.player.LocalPlayer player){
        var mc=Minecraft.getInstance();
        if(!Passthrough.minecraftAuthority||mc.player!=player){wasSprinting=false;return;}
        boolean sprinting=player.isSprinting();
        // Native MC cancelled a real sprint (wall, sneak, item slowdown, etc.).
        // Retire its input latch so the next Ctrl starts a fresh sprint. An
        // intentionally armed Ctrl before W is preserved until movement starts.
        if(wasSprinting&&!sprinting&&mc.options.toggleSprint().get())clearSprintLatch(mc);
        wasSprinting=sprinting;
    }

    static void scroll(double amount) {
        if(!Double.isFinite(amount)||amount==0)return;
        var mc=Minecraft.getInstance();
        if(mc.player==null)return;
        try{transportScroll=true;((MouseHandlerAccess)mc.mouseHandler).dqj$scroll(mc.getWindow().getWindow(),0,Math.clamp(amount,-128,128));scrollEvents++;}
        finally{transportScroll=false;}
    }

    static void look(double x, double y) {
        if (!Double.isFinite(x) || !Double.isFinite(y)) return;
        dx += Math.clamp(x, -4096, 4096);
        dy += Math.clamp(y, -4096, 4096);
    }

    public static void beforeCamera() {
        var mc = Minecraft.getInstance();
        if(Passthrough.active&&((MouseHandlerAccess)mc.mouseHandler).dqj$physicalGrabbed())mc.mouseHandler.releaseMouse();
        boolean nowOwned = Passthrough.minecraftAuthority;
        if (nowOwned != owned) {
            owned = nowOwned;
            if(owned){
                mc.mouseHandler.releaseMouse();originalToggleSprint=mc.options.toggleSprint().get();
                ((KeyMappingAccessor)mc.options.keySprint).dqj$clearDown(false);
                mc.options.toggleSprint().set(true);
            }
            dx = dy = 0;
            Passthrough.LOG.info("DQJ motion authority: {}", owned ? "Minecraft vanilla" : "host");
            if (!owned) {
                releaseInput();
                mc.options.keyUp.setDown(false); mc.options.keyDown.setDown(false);
                mc.options.keyLeft.setDown(false); mc.options.keyRight.setDown(false);
                mc.options.keyJump.setDown(false); mc.options.keyShift.setDown(false);
                ((KeyMappingAccessor)mc.options.keySprint).dqj$clearDown(false);
                mc.options.toggleSprint().set(originalToggleSprint);
                if(mc.player!=null)mc.player.setSprinting(false);
                mc.options.keyAttack.setDown(false); mc.options.keyUse.setDown(false);
                mc.options.keyPickItem.setDown(false); mc.options.keyInventory.setDown(false);
                mc.options.keyDrop.setDown(false); mc.options.keySwapOffhand.setDown(false);
            }
        }
        long now = System.nanoTime();
        double seconds = lastTurn == 0 ? 1.0 / 60 : (now - lastTurn) / 1e9;
        lastTurn = now;
        if (!owned || mc.player == null || mc.screen != null) { dx = dy = 0; return; }
        if (dx == 0 && dy == 0) return;
        var access = (MouseHandlerAccess) mc.mouseHandler;
        double originalX = access.dqj$getDX(), originalY = access.dqj$getDY();
        try {
            transportTurn = true;
            access.dqj$setDX(dx); access.dqj$setDY(dy);
            access.dqj$turn(seconds);
        } finally {
            transportTurn = false;
            dx = dy = 0;
            access.dqj$setDX(originalX); access.dqj$setDY(originalY);
        }
    }

    static void publish(HostState.Pose pose) {
        JsonObject out = state();
        out.addProperty("t", "mcstate");
        out.addProperty("f", ++renderFrame);
        out.addProperty("terrainRayReach",HostRayDiagnostics.reach(Minecraft.getInstance()));
        var rayDirection=net.minecraft.world.phys.Vec3.directionFromRotation(pose.pitch(),pose.yaw()).normalize();
        out.add("terrainRayDirection",array(rayDirection.x,rayDirection.y,rayDirection.z));
        HostRayDiagnostics.remember(renderFrame,pose);
        out.addProperty("tn", System.nanoTime());
        out.add("eye", array(pose.x(), pose.y(), pose.z()));
        out.add("rot", array(pose.yaw(), pose.pitch(), pose.roll()));
        out.addProperty("fov", pose.fov());
        Passthrough.events.accept(out.toString());
    }

    static void report(WebSocket conn, JsonObject request) {
        JsonObject out = state();
        out.addProperty("t", "state");
        if (request.has("req")) out.add("req", request.get("req"));
        var mc = Minecraft.getInstance();
        if (request.has("block") && mc.level != null) {
            JsonArray at = request.getAsJsonArray("block");
            if (at.size() == 3) {
                var pos=new BlockPos(at.get(0).getAsInt(),at.get(1).getAsInt(),at.get(2).getAsInt());
                var state=mc.level.getBlockState(pos);
                out.addProperty("block", BuiltInRegistries.BLOCK.getKey(state.getBlock()).toString());
                out.add("blockAnchor",dev.rehan.passthrough.HostBlockAnchors.report(pos));
                if(request.has("shapes")&&request.get("shapes").getAsBoolean()){
                    out.add("collisionShape",describeShape(state.getCollisionShape(mc.level,pos)));
                    out.add("selectionShape",describeShape(state.getShape(mc.level,pos)));
                    out.add("interactionShape",describeShape(state.getInteractionShape(mc.level,pos)));
                    // Compare against the real vanilla barrier path at an
                    // unregistered position without writing any block there.
                    var fallback=pos.above(8);
                    if(WorldBridge.surfaceShape(fallback)==null)
                        out.add("vanillaBarrierShape",describeShape(net.minecraft.world.level.block.Blocks.BARRIER.defaultBlockState().getShape(mc.level,fallback)));
                }
            }
        }
        if(request.has("collisionProbe"))out.add("collisionProbe",CollisionProbe.probe(mc));
        if(request.has("ray")&&mc.level!=null&&mc.player!=null){
            var a=request.getAsJsonArray("ray");
            if(a.size()!=6)throw new IllegalArgumentException("Ray requires six coordinates");
            var from=new net.minecraft.world.phys.Vec3(a.get(0).getAsDouble(),a.get(1).getAsDouble(),a.get(2).getAsDouble());
            var to=new net.minecraft.world.phys.Vec3(a.get(3).getAsDouble(),a.get(4).getAsDouble(),a.get(5).getAsDouble());
            if(!Double.isFinite(from.x+from.y+from.z+to.x+to.y+to.z)||from.distanceToSqr(to)>144
                ||from.distanceToSqr(mc.player.position())>1024||to.distanceToSqr(mc.player.position())>1024)
                throw new IllegalArgumentException("Ray diagnostic outside local budget");
            var rays=new JsonObject();
            rays.add("selection",describeHit(mc.level.clip(new net.minecraft.world.level.ClipContext(from,to,net.minecraft.world.level.ClipContext.Block.OUTLINE,net.minecraft.world.level.ClipContext.Fluid.NONE,mc.player))));
            rays.add("collision",describeHit(mc.level.clip(new net.minecraft.world.level.ClipContext(from,to,net.minecraft.world.level.ClipContext.Block.COLLIDER,net.minecraft.world.level.ClipContext.Fluid.NONE,mc.player))));
            out.add("ray",rays);
        }
        if(request.has("pick")&&request.get("pick").getAsBoolean()&&mc.hitResult instanceof net.minecraft.world.phys.BlockHitResult hit)
            out.add("pick",describeHit(hit));
        if(request.has("hostRay")&&request.get("hostRay").getAsBoolean())out.add("hostRay",HostRayDiagnostics.report());
        if(request.has("windowView")&&request.get("windowView").getAsBoolean())out.add("windowView",BridgeWindow.report());
        if(conn.isOpen())conn.send(out.toString());
    }
    private static JsonObject describeShape(net.minecraft.world.phys.shapes.VoxelShape shape){
        var parts=shape.toAabbs();var out=new JsonObject();var boxes=new JsonArray();
        for(int i=0;i<Math.min(128,parts.size());i++){var b=parts.get(i);boxes.add(array(b.minX,b.minY,b.minZ,b.maxX,b.maxY,b.maxZ));}
        out.add("boxes",boxes);out.addProperty("parts",parts.size());out.addProperty("exact",parts.size()<=128);return out;
    }
    private static JsonObject describeHit(net.minecraft.world.phys.BlockHitResult hit){
        var out=new JsonObject();var p=hit.getBlockPos();var at=hit.getLocation();
        out.addProperty("type",hit.getType().name());out.add("block",array(p.getX(),p.getY(),p.getZ()));
        out.add("point",array(at.x,at.y,at.z));out.addProperty("face",hit.getDirection().getName());
        out.addProperty("proxy",WorldBridge.isHostProxy(p));return out;
    }

    private static JsonObject state() {
        JsonObject out = new JsonObject();
        var mc = Minecraft.getInstance();
        var player = mc.player;
        out.addProperty("ready", player != null && WorldBridge.ready());
        out.addProperty("authority", Passthrough.minecraftAuthority ? "minecraft" : "host");
        out.addProperty("iconified", mc.getWindow().isIconified());
        out.addProperty("paused", mc.isPaused());
        out.addProperty("windowActive", mc.isWindowActive());
        out.addProperty("crosshairEntity",mc.crosshairPickEntity==null?"":mc.crosshairPickEntity.getUUID().toString());
        JsonArray travelConflicts=new JsonArray();
        for(var key:mc.options.keyMappings)if(key.matches(org.lwjgl.glfw.GLFW.GLFW_KEY_F6,0))travelConflicts.add(key.getName());
        out.addProperty("hostTravelKey","F6");out.addProperty("hostTravelKeyAvailable",travelConflicts.isEmpty());out.add("hostTravelKeyConflicts",travelConflicts);
        out.addProperty("groundEpoch",WorldBridge.groundEpoch);
        out.add("groundSpawn",array(WorldBridge.spawnX,WorldBridge.spawnY,WorldBridge.spawnZ));
        out.addProperty("groundFailure",WorldBridge.groundFailure);
        out.addProperty("groundServerReady",WorldBridge.groundReady);
        out.addProperty("groundReady",WorldBridge.groundReady&&(WorldBridge.groundCommitted||clientSupportReady(mc)));
        out.addProperty("groundCommitted",WorldBridge.groundCommitted);
        out.addProperty("groundAnchorResolved",WorldBridge.groundAnchorResolved);
        out.addProperty("worldId",WorldBridge.worldId);
        out.addProperty("terrainGuard",dev.rehan.passthrough.TerrainLease.enabled);
        out.addProperty("terrainWaiting",dev.rehan.passthrough.TerrainLease.waiting);
        out.addProperty("terrainWaits",dev.rehan.passthrough.TerrainLease.waits);
        out.addProperty("terrainRequests",dev.rehan.passthrough.TerrainLease.requests);
        out.addProperty("renderLimit", mc.getFramerateLimitTracker().getFramerateLimit());
        out.addProperty("mouseGrabbed", mc.mouseHandler.isMouseGrabbed());
        out.addProperty("physicalMouseGrabbed",((MouseHandlerAccess)mc.mouseHandler).dqj$physicalGrabbed());
        var guiInput=new JsonObject();guiInput.addProperty("logicalWidth",mc.getWindow().getScreenWidth());guiInput.addProperty("logicalHeight",mc.getWindow().getScreenHeight());
        guiInput.addProperty("framebufferWidth",mc.getWindow().getWidth());guiInput.addProperty("framebufferHeight",mc.getWindow().getHeight());
        guiInput.addProperty("pointerEvents",guiPointerEvents);guiInput.addProperty("buttonPresses",guiButtonPresses);
        guiInput.addProperty("lastWidth",guiPointerWidth);guiInput.addProperty("lastHeight",guiPointerHeight);
        guiInput.add("normalized",array(guiPointerNx,guiPointerNy));guiInput.add("position",array(guiPointerX,guiPointerY));out.add("guiInputSpace",guiInput);
        out.addProperty("hostWeatherOnly", Passthrough.minecraftAuthority);
        out.addProperty("weatherPassesSuppressed", weatherPassesSuppressed);
        out.addProperty("rainParticleTicksSuppressed", rainParticleTicksSuppressed);
        out.addProperty("skyPassesSuppressed",skyPassesSuppressed);out.addProperty("cloudPassesSuppressed",cloudPassesSuppressed);
        out.addProperty("proxyRendersSuppressed",proxyRendersSuppressed);out.addProperty("scrollEvents",scrollEvents);
        if(mc.level!=null) out.addProperty("rainLevel", mc.level.getRainLevel(1));
        JsonArray proxies=new JsonArray();
        if(mc.level!=null)for(var entity:mc.level.entitiesForRendering())if(entity instanceof LivingEntity living && CombatBridge.dimensions(living)!=null){
            JsonObject p=new JsonObject();p.addProperty("uuid",living.getUUID().toString());p.addProperty("w",living.getBbWidth());p.addProperty("h",living.getBbHeight());p.add("pos",array(living.getX(),living.getY(),living.getZ()));proxies.add(p);
        }out.add("clientHostTargets",proxies);
        out.addProperty("tn", System.nanoTime());
        if (player == null) return out;
        out.addProperty("tick", player.tickCount);
        out.add("pos", array(player.getX(), player.getY(), player.getZ()));
        out.add("vel", array(player.getDeltaMovement().x, player.getDeltaMovement().y, player.getDeltaMovement().z));
        out.add("look", array(player.getYRot(), player.getXRot()));
        out.addProperty("onGround", player.onGround());
        out.addProperty("noPhysics", player.noPhysics);
        out.addProperty("flying", player.getAbilities().flying);
        out.addProperty("health", player.getHealth());
        out.addProperty("food",player.getFoodData().getFoodLevel());out.addProperty("saturation",player.getFoodData().getSaturationLevel());
        out.addProperty("foodLocked",Passthrough.active);out.addProperty("sprinting",player.isSprinting());
        out.addProperty("toggleSprint",mc.options.toggleSprint().get());out.addProperty("sprintKeyDown",mc.options.keySprint.isDown());
        out.addProperty("sprintPresses",sprintPresses);out.addProperty("sprintLatchResets",sprintLatchResets);
        out.addProperty("forwardImpulse",player.input.forwardImpulse);out.addProperty("leftImpulse",player.input.leftImpulse);
        out.addProperty("horizontalCollision",player.horizontalCollision);out.addProperty("minorHorizontalCollision",player.minorHorizontalCollision);
        out.addProperty("usingItem",player.isUsingItem());out.addProperty("crouching",player.isCrouching());
        out.addProperty("blindness",player.hasEffect(net.minecraft.world.effect.MobEffects.BLINDNESS));
        out.addProperty("shiftHeld",shiftHeld);out.addProperty("controlHeld",controlHeld);
        out.addProperty("slot", player.getInventory().selected);
        out.addProperty("held", BuiltInRegistries.ITEM.getKey(player.getMainHandItem().getItem()).toString());
        out.addProperty("heldName", player.getMainHandItem().getHoverName().getString());
        out.addProperty("heldCount", player.getMainHandItem().getCount());
        var custom=player.getMainHandItem().get(DataComponents.CUSTOM_DATA);
        if(custom!=null){
            var tag=custom.copyTag();
            if(tag.getInt("gbg_is_gun")==1){
                var gun=tag.getCompound("gbg");out.addProperty("gunAmmo",gun.getInt("current_ammo"));
                out.addProperty("gunMaxAmmo",gun.getInt("max_ammo"));
            }
        }
        out.addProperty("mode", mc.gameMode == null ? "" : mc.gameMode.getPlayerMode().getName());
        out.addProperty("screen", mc.screen == null ? "" : mc.screen instanceof InventoryScreen ? "InventoryScreen"
            : mc.screen instanceof PauseScreen ? "PauseScreen" : mc.screen.getClass().getSimpleName());
        if(mc.screen instanceof HostTravelScreen travel)out.add("travelMenu",travel.diagnostics());
        if(mc.screen instanceof AbstractContainerScreen<?> screen){
            var access=(ContainerScreenAccess)screen;
            out.addProperty("guiScale",mc.getWindow().getGuiScale());out.addProperty("guiWidth",mc.getWindow().getGuiScaledWidth());out.addProperty("guiHeight",mc.getWindow().getGuiScaledHeight());
            out.addProperty("menuLeft",access.dqj$left());out.addProperty("menuTop",access.dqj$top());
            JsonArray slots=new JsonArray();for(var slot:screen.getMenu().slots){
                JsonObject entry=new JsonObject();entry.addProperty("index",slot.index);entry.addProperty("x",slot.x);entry.addProperty("y",slot.y);
                entry.addProperty("id",BuiltInRegistries.ITEM.getKey(slot.getItem().getItem()).toString());entry.addProperty("count",slot.getItem().getCount());slots.add(entry);
            }out.add("menuSlots",slots);
            var carried=screen.getMenu().getCarried();out.addProperty("carriedName",BuiltInRegistries.ITEM.getKey(carried.getItem()).toString());out.addProperty("carriedCount",carried.getCount());
        }
        if(mc.screen instanceof InventoryScreen){
            out.addProperty("guiScale",mc.getWindow().getGuiScale());
            out.addProperty("guiWidth",mc.getWindow().getGuiScaledWidth());out.addProperty("guiHeight",mc.getWindow().getGuiScaledHeight());
            var carried=player.containerMenu.getCarried();out.addProperty("carriedName",BuiltInRegistries.ITEM.getKey(carried.getItem()).toString());out.addProperty("carriedCount",carried.getCount());
            JsonArray slots=new JsonArray();for(var item:player.getInventory().items){
                JsonObject entry=new JsonObject();entry.addProperty("id",BuiltInRegistries.ITEM.getKey(item.getItem()).toString());entry.addProperty("count",item.getCount());slots.add(entry);
            }out.add("inventory",slots);
        }
        return out;
    }

    private static boolean clientSupportReady(Minecraft mc){
        if(mc.level==null||mc.player==null||!mc.player.isAlive())return false;
        double x=WorldBridge.spawnX,y=WorldBridge.spawnY,z=WorldBridge.spawnZ;
        if(Math.abs(mc.player.getX()-x)>.15||Math.abs(mc.player.getY()-y)>.1||Math.abs(mc.player.getZ()-z)>.15)return false;
        return dev.rehan.passthrough.GroundSpawn.supported(mc.level,x,y,z);
    }

    private static JsonArray array(double... values) {
        JsonArray array = new JsonArray();
        for (double value : values) array.add(value);
        return array;
    }
}
