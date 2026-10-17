package dev.rehan.passthrough.client;

import com.google.gson.JsonObject;
import dev.rehan.passthrough.Passthrough;

public final class HostState {
    public record Pose(long hostFrame,double x,double y,double z,float yaw,float pitch,float roll,float fov,
        boolean firstPerson,double px,double py,double pz,float bodyYaw,long receivedNanos,
        boolean drive,float lookYaw,float lookPitch,boolean gun,boolean minecraftAuthority) {}
    private static volatile long received,frame;
    private static volatile boolean owned;
    static void update(JsonObject m) {
        frame=m.has("f")?m.get("f").getAsLong():0;
        owned=m.has("authority")&&m.get("authority").getAsString().equals("minecraft");
        received=System.nanoTime();
    }
    public static void refresh() {
        Passthrough.active=System.nanoTime()-received<2_000_000_000L;
        Passthrough.minecraftAuthority=Passthrough.active&&owned;
    }
    static Pose rendered(double x,double y,double z,float yaw,float pitch,float fov,boolean fp,double px,double py,double pz) {
        return new Pose(frame,x,y,z,yaw,pitch,0,fov,fp,px,py,pz,yaw,received,false,yaw,pitch,false,owned);
    }
}
