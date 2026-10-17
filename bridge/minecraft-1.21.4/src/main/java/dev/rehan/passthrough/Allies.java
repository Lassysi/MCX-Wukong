package dev.rehan.passthrough;

import dev.rehan.passthrough.mixin.MobGoalAccess;
import java.util.Collections;
import java.util.EnumSet;
import java.util.Set;
import java.util.WeakHashMap;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerEntityEvents;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.InteractionHand;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.Mob;
import net.minecraft.world.entity.PathfinderMob;
import net.minecraft.world.entity.ai.attributes.Attributes;
import net.minecraft.world.entity.ai.goal.Goal;
import net.minecraft.world.entity.ai.goal.MeleeAttackGoal;
import net.minecraft.world.entity.ai.goal.target.NearestAttackableTargetGoal;
import net.minecraft.world.entity.animal.Pig;
import net.minecraft.world.entity.monster.Creeper;
import net.minecraft.world.entity.monster.RangedAttackMob;

/** Item 7 (user 2026-10-16): mobs spawned from spawn eggs fight for the player. They hunt
 *  the invisible host proxies (Wukong enemies, so their damage reaches Wukong) and never
 *  target the player. The tag survives saves; goals are re-added when the mob loads.
 *  Preview88 (user: creepers did not chase, skeletons walked up and hit in melee):
 *  - follow range 48 (vanilla 16 for creepers/skeletons);
 *  - ranged mobs keep ~14 blocks and fire every 1.5 s (vanilla bow goal needs a line of sight that
 *    host ground proxies can block; the old code also gave them a melee goal, which won);
 *  - creepers run at the enemy and ignite within ~3 blocks of its body;
 *  - only melee mobs get the melee goal;
 *  - ally positions go to the host, which lays native ground proxies around them so they can walk
 *    outside the player's own raster. */
public final class Allies {
    private Allies() {}
    public static final String Tag="dqj.ally";
    private static final Set<Mob> adopted=Collections.newSetFromMap(new WeakHashMap<>());
    public static void register(){
        ServerEntityEvents.ENTITY_LOAD.register((entity,level)->{if(entity instanceof Mob mob&&mob.getTags().contains(Tag))adopt(mob);});
    }
    public static void adopt(Mob mob){
        if(!(mob instanceof PathfinderMob pathfinder)||!adopted.add(mob))return;
        mob.addTag(Tag);
        var follow=mob.getAttribute(Attributes.FOLLOW_RANGE);
        if(follow!=null&&follow.getBaseValue()<48)follow.setBaseValue(48);
        var access=(MobGoalAccess)mob;
        access.dqj$targets().removeAllGoals(goal->goal instanceof NearestAttackableTargetGoal<?>);
        access.dqj$targets().addGoal(1,new HostEnemyTarget(mob));
        String style;
        if(mob instanceof Creeper creeper){access.dqj$goals().addGoal(1,new CreeperCharge(creeper));style="charge";}
        else if(mob instanceof RangedAttackMob){access.dqj$goals().addGoal(1,new RangedVolley(pathfinder));style="ranged";}
        else if(mob.getAttribute(Attributes.ATTACK_DAMAGE)!=null){access.dqj$goals().addGoal(2,new MeleeAttackGoal(pathfinder,1.2,false));style="melee";}
        else style="vanilla";
        Passthrough.LOG.info("DQJ ally: {} now hunts Wukong enemies ({})",mob.getType(),style);
    }
    private static boolean enemy(LivingEntity target){return target!=null&&target.isAlive()&&CombatBridge.isProxy(target);}
    /** Distance from a mob to the nearest point of the target's body (Boss proxies are wide). */
    private static double reach(Mob mob,LivingEntity target){return Math.sqrt(target.getBoundingBox().distanceToSqr(mob.position()));}
    private static final class HostEnemyTarget extends NearestAttackableTargetGoal<Pig> {
        HostEnemyTarget(Mob mob){
            super(mob,Pig.class,5,false,false,(target,level)->CombatBridge.isProxy(target));
            // Host proxies are invisible and may stand inside terrain proxies.
            this.targetConditions=this.targetConditions.ignoreInvisibilityTesting().ignoreLineOfSight();
        }
    }
    private static final class RangedVolley extends Goal {
        private final PathfinderMob mob;private int cooldown;
        RangedVolley(PathfinderMob mob){this.mob=mob;setFlags(EnumSet.of(Flag.MOVE,Flag.LOOK));}
        @Override public boolean canUse(){return enemy(mob.getTarget());}
        @Override public boolean canContinueToUse(){return canUse();}
        @Override public boolean requiresUpdateEveryTick(){return true;}
        @Override public void stop(){mob.getNavigation().stop();}
        @Override public void tick(){
            var target=mob.getTarget();if(target==null)return;
            double d=reach(mob,target);
            mob.getLookControl().setLookAt(target,30,30);
            if(d>14)mob.getNavigation().moveTo(target,1.1);else mob.getNavigation().stop();
            if(--cooldown<=0&&d<=20){((RangedAttackMob)mob).performRangedAttack(target,1f);mob.swing(InteractionHand.MAIN_HAND);cooldown=30;}
        }
    }
    private static final class CreeperCharge extends Goal {
        private final Creeper creeper;
        CreeperCharge(Creeper creeper){this.creeper=creeper;setFlags(EnumSet.of(Flag.MOVE,Flag.LOOK));}
        @Override public boolean canUse(){return enemy(creeper.getTarget());}
        @Override public boolean canContinueToUse(){return canUse()&&!creeper.isIgnited();}
        @Override public boolean requiresUpdateEveryTick(){return true;}
        @Override public void tick(){
            var target=creeper.getTarget();if(target==null)return;
            creeper.getLookControl().setLookAt(target,30,30);
            if(reach(creeper,target)<3){creeper.getNavigation().stop();creeper.ignite();}
            else creeper.getNavigation().moveTo(target,1.25);
        }
    }
    /** Every 10 ticks: up to 6 living allies near a player, as {"t":"ally_ground","p":[x,y,z,...]}. */
    static void tick(MinecraftServer server){
        if(!Passthrough.active||!WorldBridge.groundCommitted||server.getTickCount()%10!=0||adopted.isEmpty())return;
        var out=new StringBuilder();int n=0;
        for(var mob:adopted){
            if(n>=6)break;
            if(mob==null||!mob.isAlive()||mob.level()!=server.overworld())continue;
            boolean near=false;for(var p:server.getPlayerList().getPlayers())if(p.distanceToSqr(mob)<96*96)near=true;
            if(!near)continue;
            if(n++>0)out.append(',');
            out.append(String.format(java.util.Locale.ROOT,"%.3f,%.3f,%.3f",mob.getX(),mob.getY(),mob.getZ()));
        }
        if(n>0)Passthrough.events.accept("{\"t\":\"ally_ground\",\"p\":["+out+"]}");
    }
}
