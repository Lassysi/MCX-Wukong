package dev.rehan.passthrough.client;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.WorldBridge;
import dev.rehan.passthrough.CombatBridge;
import java.net.InetSocketAddress;
import net.minecraft.client.Minecraft;
import org.java_websocket.WebSocket;
import org.java_websocket.handshake.ClientHandshake;
import org.java_websocket.server.WebSocketServer;

public final class HostLink extends WebSocketServer {
    private HostLink() { super(new InetSocketAddress("127.0.0.1",Integer.getInteger("passthrough.port",25599)));setReuseAddr(true);setDaemon(true); }
    static void launch() { var link=new HostLink();link.start();Passthrough.events=link::broadcast; }
    @Override public void onStart() { Passthrough.LOG.info("DQJ real MC 1.21.4 link ready on {}",getPort()); }
    @Override public void onOpen(WebSocket conn,ClientHandshake hello) {
        conn.send("{\"t\":\"hello\",\"v\":1,\"bridge\":16,\"mc\":\"1.21.4\",\"pid\":"+ProcessHandle.current().pid()+",\"shm\":\"Local\\\\MCPassthroughFrame\"}");
    }
    @Override public void onClose(WebSocket conn,int code,String reason,boolean remote) {NativeBlocks.clear();}
    @Override public void onError(WebSocket conn,Exception e) { Passthrough.LOG.warn("bridge connection",e); }
    @Override public void onMessage(WebSocket conn,String data) {
        try {
            JsonObject m=JsonParser.parseString(data).getAsJsonObject();
            String type=m.get("t").getAsString();
            if (type.equals("cam")) {HostState.update(m);return;}
            if(type.equals("native_control")){dev.rehan.passthrough.NativePause.renew(m.get("hold").getAsBoolean());return;}
            // Preview124: a takeover releases the idle hold first (the hold would pull Steve back from the new spawn).
            if(type.equals("bridge_prepare")){dev.rehan.passthrough.NativePause.renew(false);WorldBridge.prepare(m);return;}
            if(type.equals("respawn")){
                // Preview124: Steve died while MC was not shown; press Respawn for the player so MC can take over again.
                var mc=net.minecraft.client.Minecraft.getInstance();
                mc.execute(()->{if(mc.player!=null&&mc.player.isDeadOrDying()){mc.player.respawn();mc.setScreen(null);Passthrough.LOG.info("DQJ life: respawn requested by host (Steve died out of view)");}});
                return;
            }
            if(type.equals("bridge_commit")){WorldBridge.commit(m.get("epoch").getAsString());return;}
            if(type.equals("bridge_release")){WorldBridge.release(m.get("epoch").getAsString());return;}
            if(type.equals("ground_surface")){
                var array=m.getAsJsonArray("c");int[] c=new int[array.size()];for(int i=0;i<c.length;i++)c[i]=array.get(i).getAsInt();WorldBridge.groundSurfaces(c,m.has("epoch")?m.get("epoch").getAsString():null);return;
            }
            if (type.equals("native_blocks")) {NativeBlocks.handle(m);return;}
            if (type.equals("cmd")) {WorldBridge.command(m.get("c").getAsString());return;}
            // Preview105: shrine shop state from the host (Will gained, shrine proximity, rest, chapter).
            if (type.equals("will")) {dev.rehan.passthrough.ShrineShop.will(m.get("gain").getAsLong());return;}
            if (type.equals("shrine")) {dev.rehan.passthrough.ShrineShop.nearShrine=m.get("near").getAsBoolean();dev.rehan.passthrough.ShrineShop.shrineId=m.has("id")?m.get("id").getAsInt():0;return;}
            if (type.equals("pearl_hit")) {var p=m.getAsJsonArray("p");dev.rehan.passthrough.Pearls.hit(m.get("id").getAsInt(),p.get(0).getAsDouble(),p.get(1).getAsDouble(),p.get(2).getAsDouble());return;}
            if (type.equals("shrine_rest")) {dev.rehan.passthrough.ShrineShop.rest();return;}
            if (type.equals("chapter")) {dev.rehan.passthrough.ShrineShop.chapter=Math.max(1,Math.min(6,m.get("n").getAsInt()));return;}
            if (type.equals("enemy_block_hit")) {var p=m.getAsJsonArray("p");WorldBridge.enemyHit(p.get(0).getAsInt(),p.get(1).getAsInt(),p.get(2).getAsInt(),m.has("boss")&&m.get("boss").getAsBoolean());return;}
            if (type.equals("enemy_break")) {var p=m.getAsJsonArray("p");WorldBridge.enemyBreak(p.get(0).getAsInt(),p.get(1).getAsInt(),p.get(2).getAsInt());return;}
            if (type.equals("host_entities")) {CombatBridge.sync(m);return;}
            if (type.equals("host_health")) {CombatBridge.hostHealth(m);return;}
            if (type.equals("combat_state")) {CombatBridge.report(conn,m);return;}
            if (type.equals("block_snapshot")) {WorldBridge.snapshot();return;}
            if (type.equals("voxels")) {
                var array=m.getAsJsonArray("c");int[] c=new int[array.size()];
                for(int i=0;i<c.length;i++)c[i]=array.get(i).getAsInt();WorldBridge.voxels(c);return;
            }
            if (type.equals("ground")) {
                var array=m.getAsJsonArray("c");int[] c=new int[array.size()];
                for(int i=0;i<c.length;i++) c[i]=array.get(i).getAsInt();WorldBridge.solid(c);return;
            }
            var mc=Minecraft.getInstance();
            mc.execute(() -> {
                if(type.equals("state")) NativeControl.report(conn,m);
                else if(type.equals("host_ray"))HostRayDiagnostics.accept(m);
                else if(type.equals("shutdown")&&Boolean.getBoolean("passthrough.devTest")) mc.stop();
                else ClientInput.handle(mc,m);
            });
        } catch(RuntimeException e) { Passthrough.LOG.warn("invalid bridge message",e); }
    }
}
