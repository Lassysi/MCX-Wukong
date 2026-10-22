using System;

namespace DqjCombat
{
    // Preview122 (user: flying with the elytra toward a Boss room killed Steve; MC's freedom of movement must be kept):
    // the hidden native body follows Steve and entered the map's out-of-bounds kill volumes (DeadZone_C); they dealt
    // attacker-less hits of 50% and 100% max HP. Live logs since Preview102 hold no other attacker-less native hits.
    // While MC owns movement such hits are map-boundary/kill-volume (or native fall) damage, so they are dropped. MC keeps
    // its own fall, void and hazard damage. Small attacker-less ticks (hazard ground, status) are left to the other rules.
    internal static class BoundaryDamage
    {
        internal static float Share=.2f;
        internal static bool Drop(bool sourceless,float delta,float maxHp,bool mcOwnsMovement)
        {
            return mcOwnsMovement&&sourceless&&delta<0&&maxHp>0&&!float.IsNaN(delta)&&-delta>=Share*maxHp;
        }
    }
}
