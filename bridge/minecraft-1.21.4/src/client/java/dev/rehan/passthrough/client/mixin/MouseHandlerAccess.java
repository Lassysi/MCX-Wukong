package dev.rehan.passthrough.client.mixin;

import net.minecraft.client.MouseHandler;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
import org.spongepowered.asm.mixin.gen.Invoker;

/** Invoke vanilla mouse sensitivity, inversion, scope and smoothing calculations. */
@Mixin(MouseHandler.class)
public interface MouseHandlerAccess {
    @Accessor("accumulatedDX") double dqj$getDX();
    @Accessor("mouseGrabbed") boolean dqj$physicalGrabbed();
    @Accessor("accumulatedDY") double dqj$getDY();
    @Accessor("accumulatedDX") void dqj$setDX(double value);
    @Accessor("accumulatedDY") void dqj$setDY(double value);
    @Accessor("ignoreFirstMove") void dqj$setIgnoreFirstMove(boolean value);
    @Invoker("turnPlayer") void dqj$turn(double seconds);
    @Invoker("onMove") void dqj$move(long window,double x,double y);
    @Invoker("onPress") void dqj$press(long window,int button,int action,int modifiers);
    @Invoker("onScroll") void dqj$scroll(long window,double horizontal,double vertical);
}
