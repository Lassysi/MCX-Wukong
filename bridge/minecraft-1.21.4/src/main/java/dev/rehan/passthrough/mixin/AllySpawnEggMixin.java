package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.Allies;
import net.minecraft.world.DifficultyInstance;
import net.minecraft.world.entity.EntitySpawnReason;
import net.minecraft.world.entity.Mob;
import net.minecraft.world.entity.SpawnGroupData;
import net.minecraft.world.level.ServerLevelAccessor;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Item 7: a mob created from a spawn egg becomes an ally against Wukong enemies. */
@Mixin(Mob.class)
abstract class AllySpawnEggMixin {
    @Inject(method="finalizeSpawn",at=@At("RETURN"))
    private void dqj$ally(ServerLevelAccessor level,DifficultyInstance difficulty,EntitySpawnReason reason,SpawnGroupData data,CallbackInfoReturnable<SpawnGroupData> ci){
        if(reason==EntitySpawnReason.SPAWN_ITEM_USE)Allies.adopt((Mob)(Object)this);
    }
}
