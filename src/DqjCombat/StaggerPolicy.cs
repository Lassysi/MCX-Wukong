using System;
using System.Collections.Generic;

namespace DqjCombat
{
    // Preview126 (user: one combo staggers and knocks an enemy back on every hit, so it can be chained to death; poise must not be
    // judged by damage). Live125: 2928 native reaction dispatches, 1735 NormalStiff, median 0.14 s between staggers of one enemy,
    // 45 staggers in 9.6 s; 1316 hits used the heavy (knockback) effect because MC damage was >= 10.
    // Poise meter per enemy, by hit count and time only: MC damage messages within MergeWindow are one attack; each attack adds
    // 1 (heavy attack 2); a full meter staggers, then the enemy is immune for Immunity seconds and the next stagger needs
    // Escalation times more hits until StreakReset seconds pass without one. Heavy (knockback) reactions need a heavy attack and
    // HeavyCooldown seconds since the last one. Bottom-health-bar enemies (Bosses, chiefs) need BossHits.
    internal sealed class StaggerPolicy
    {
        internal enum Reaction {None,Light,Heavy}
        internal static double MergeWindow=.25,Immunity=2,StreakReset=6,HeavyCooldown=4,Escalation=1.5;
        internal static int BaseHits=3,BossHits=6,HeavyWeight=2;
        private sealed class State {internal double Last=-99,ImmuneUntil=-99,LastStagger=-99,LastHeavy=-99,Meter;internal int Streak;}
        private readonly Dictionary<string,State> enemies=new Dictionary<string,State>();
        internal int Merged,Staggers,HeavyReactions;
        internal Reaction Hit(string id,double now,bool heavy,bool boss)
        {
            State s;if(!enemies.TryGetValue(id,out s)){if(enemies.Count>512)enemies.Clear();s=new State();enemies[id]=s;}
            if(now-s.Last<MergeWindow){Merged++;return Reaction.None;}
            s.Last=now;
            if(now-s.LastStagger>StreakReset)s.Streak=0;
            if(now<s.ImmuneUntil)return Reaction.None;
            s.Meter+=heavy?HeavyWeight:1;
            if(s.Meter<Needed(s.Streak,boss))return Reaction.None;
            s.Meter=0;s.Streak++;s.LastStagger=now;s.ImmuneUntil=now+Immunity;Staggers++;
            if(heavy&&now-s.LastHeavy>=HeavyCooldown){s.LastHeavy=now;HeavyReactions++;return Reaction.Heavy;}
            return Reaction.Light;
        }
        internal static double Needed(int streak,bool boss){return (boss?BossHits:BaseHits)*Math.Pow(Escalation,streak);}
        internal void Forget(string id){enemies.Remove(id);}
    }
}
