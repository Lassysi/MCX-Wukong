package dev.rehan.passthrough.mixin;
import dev.rehan.passthrough.Passthrough;
import net.minecraft.world.level.ServerExplosion;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.EntityType;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
import java.util.Locale;
@Mixin(ServerExplosion.class)
abstract class ExplosionMixin {
    @Inject(method="explode",at=@At("TAIL"))
    private void dqj$exploded(CallbackInfo ci) {
        ServerExplosion explosion=(ServerExplosion)(Object)this;
        var center=explosion.center();Entity source=explosion.getDirectSourceEntity();
        if(Passthrough.active) Passthrough.events.accept(String.format(Locale.ROOT,
            "{\"t\":\"explosion\",\"pos\":[%.3f,%.3f,%.3f],\"r\":%.2f,\"src\":\"%s\"}",center.x,center.y,center.z,explosion.radius(),
            source==null?"unknown":EntityType.getKey(source.getType()).getPath()));
    }
}
