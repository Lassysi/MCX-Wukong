package dev.rehan.passthrough.mixin;

import net.minecraft.world.entity.Mob;
import net.minecraft.world.entity.ai.goal.GoalSelector;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;

@Mixin(Mob.class)
public interface MobGoalAccess {
    @Accessor("targetSelector") GoalSelector dqj$targets();
    @Accessor("goalSelector") GoalSelector dqj$goals();
}
