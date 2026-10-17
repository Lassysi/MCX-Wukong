package dev.rehan.passthrough.mixin;

import com.llamalad7.mixinextras.sugar.Local;
import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.core.BlockPos;
import net.minecraft.world.level.Level;
import net.minecraft.world.level.block.TntBlock;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.ModifyArg;

/** Preview48: primed TNT starts where the anchored TNT block was drawn (pos.y + dy). */
@Mixin(TntBlock.class)
abstract class HostPrimedTntMixin {
    @ModifyArg(method="explode(Lnet/minecraft/world/level/Level;Lnet/minecraft/core/BlockPos;Lnet/minecraft/world/entity/LivingEntity;)V",
        at=@At(value="INVOKE",target="Lnet/minecraft/world/entity/item/PrimedTnt;<init>(Lnet/minecraft/world/level/Level;DDDLnet/minecraft/world/entity/LivingEntity;)V"),index=2)
    private static double dqj$ignited(double y,@Local(argsOnly=true) Level level,@Local(argsOnly=true) BlockPos pos){
        return y+HostBlockAnchors.recentOffset(level,pos).y;
    }
    @ModifyArg(method="explode(Lnet/minecraft/world/level/Level;Lnet/minecraft/core/BlockPos;Lnet/minecraft/world/entity/LivingEntity;)V",
        at=@At(value="INVOKE",target="Lnet/minecraft/world/entity/item/PrimedTnt;<init>(Lnet/minecraft/world/level/Level;DDDLnet/minecraft/world/entity/LivingEntity;)V"),index=1)
    private static double dqj$ignitedX(double x,@Local(argsOnly=true) Level level,@Local(argsOnly=true) BlockPos pos){
        return x+HostBlockAnchors.recentOffset(level,pos).x;
    }
    @ModifyArg(method="explode(Lnet/minecraft/world/level/Level;Lnet/minecraft/core/BlockPos;Lnet/minecraft/world/entity/LivingEntity;)V",
        at=@At(value="INVOKE",target="Lnet/minecraft/world/entity/item/PrimedTnt;<init>(Lnet/minecraft/world/level/Level;DDDLnet/minecraft/world/entity/LivingEntity;)V"),index=3)
    private static double dqj$ignitedZ(double z,@Local(argsOnly=true) Level level,@Local(argsOnly=true) BlockPos pos){
        return z+HostBlockAnchors.recentOffset(level,pos).z;
    }
    @ModifyArg(method="wasExploded",
        at=@At(value="INVOKE",target="Lnet/minecraft/world/entity/item/PrimedTnt;<init>(Lnet/minecraft/world/level/Level;DDDLnet/minecraft/world/entity/LivingEntity;)V"),index=2)
    private double dqj$chain(double y,@Local(argsOnly=true) net.minecraft.server.level.ServerLevel level,@Local(argsOnly=true) BlockPos pos){
        return y+HostBlockAnchors.recentOffset(level,pos).y;
    }
    @ModifyArg(method="wasExploded",
        at=@At(value="INVOKE",target="Lnet/minecraft/world/entity/item/PrimedTnt;<init>(Lnet/minecraft/world/level/Level;DDDLnet/minecraft/world/entity/LivingEntity;)V"),index=1)
    private double dqj$chainX(double x,@Local(argsOnly=true) net.minecraft.server.level.ServerLevel level,@Local(argsOnly=true) BlockPos pos){
        return x+HostBlockAnchors.recentOffset(level,pos).x;
    }
    @ModifyArg(method="wasExploded",
        at=@At(value="INVOKE",target="Lnet/minecraft/world/entity/item/PrimedTnt;<init>(Lnet/minecraft/world/level/Level;DDDLnet/minecraft/world/entity/LivingEntity;)V"),index=3)
    private double dqj$chainZ(double z,@Local(argsOnly=true) net.minecraft.server.level.ServerLevel level,@Local(argsOnly=true) BlockPos pos){
        return z+HostBlockAnchors.recentOffset(level,pos).z;
    }
}
