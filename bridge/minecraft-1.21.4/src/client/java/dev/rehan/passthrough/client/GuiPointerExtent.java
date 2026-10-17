package dev.rehan.passthrough.client;

/** Retain a logical GUI input space while the OS reports an iconified size0. */
public final class GuiPointerExtent {
    private GuiPointerExtent() {}
    public static int dimension(int raw,int lastPositive,int framebuffer,boolean bridgeOwnsInput) {
        if(raw>0||!bridgeOwnsInput)return raw;
        return lastPositive>0?lastPositive:Math.max(1,framebuffer);
    }
}
