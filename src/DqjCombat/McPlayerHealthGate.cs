using System;
using System.Collections.Generic;
using System.Globalization;
using b1;
using BtlShare;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // Preserve native calculation/FX; route its final HP delta through MC armor/shield/food.
    // Reentrant bridge settlement bypasses the gate exactly once, avoiding feedback loops.
    internal sealed class McPlayerHealthGate : IDisposable
    {
        private readonly BGUPlayerCharacterCS player;
        private readonly McBridgeWire wire;
        private readonly string session;
        private readonly Func<FVector,BridgePoint> toMc;
        private readonly List<EventHandle> handles=new List<EventHandle>();
        private readonly List<Delegate> originals=new List<Delegate>(), replacements=new List<Delegate>();
        private AActor attacker;
        private long sequence;
        internal bool Applying;
        internal float Unit=1;
        internal bool Active {get;private set;}
        internal McPlayerHealthGate(BGUPlayerCharacterCS player,McBridgeWire wire,string session,Func<FVector,BridgePoint> toMc)
        {this.player=player;this.wire=wire;this.session=session;this.toMc=toMc;}
        internal bool Install()
        {
            try{
                var collection=GameApi.Events(player);
                var damage=HookLedger.Resolve(collection,"Evt_TriggerNormalDamageEffect");
                var hp=HookLedger.Resolve(collection,"Evt_IncreaseAttrFloat");
                var damageHandlers=HookLedger.ReadHandlers(damage);var hpHandlers=HookLedger.ReadHandlers(hp);
                if(damageHandlers==null||damageHandlers.Length!=1||hpHandlers==null||hpHandlers.Length!=2)throw new InvalidOperationException("Native health baseline differs");
                if(damageHandlers[0].Target==null||damageHandlers[0].Target.GetType().FullName!="b1.BUS_BeAttackedComp")throw new InvalidOperationException("Unknown native damage owner");
                foreach(var h in hpHandlers)if(h.Target==null||(h.Target.GetType().FullName!="b1.BUS_AttrComp"&&h.Target.GetType().FullName!="b1.BUS_TransGuideComp"))throw new InvalidOperationException("Unknown native HP owner");
                Replace(damage,damageHandlers,args=>{
                    AActor previous=attacker;attacker=args.Length>0?args[0] as AActor:null;
                    try{originals[0].DynamicInvoke(args);}finally{attacker=previous;}
                });
                Replace(hp,hpHandlers,Gate);
                Active=true;Log.Write("world-health","player HP gate installed; native damage -> MC mitigation -> native HP/death");return true;
            }catch(Exception e){Log.Write("world-error","HP gate: "+e.Message);Dispose();return false;}
        }
        private void Replace(EventHandle handle,Delegate[] native,Action<object[]> callback)
        {
            var combined=Delegate.Combine(native);var mine=DelegateFactory.Create(native[0].GetType(),callback);string error;
            var removed=new List<Delegate>();
            try{
                foreach(var d in native){if(!HookLedger.Uninstall(handle,d,out error))throw new InvalidOperationException(error);removed.Add(d);}
                if(!HookLedger.Install(handle,mine,out error))throw new InvalidOperationException(error);
            }catch{foreach(var d in removed)HookLedger.Install(handle,d,out error);throw;}
            handles.Add(handle);originals.Add(combined);replacements.Add(mine);
        }
        private void Gate(object[] args)
        {
            if(!Active||Applying||!wire.Connected||args.Length!=2||(EBGUAttrFloat)args[0]!=EBGUAttrFloat.Hp){originals[1].DynamicInvoke(args);return;}
            float delta=(float)args[1];if(float.IsNaN(delta)||float.IsInfinity(delta)||Math.Abs(delta)<.0001f)return;
            string id=attacker==null||attacker.IsDestroyed?"":attacker.GetUniqueID().ToString(CultureInfo.InvariantCulture);
            if(id.Length==0&&delta<0){
                double now=Clock();
                float max=BGUFunctionLibraryCS.BGUGetFloatAttr(player,EBGUAttrFloat.HpMax);
                if(now-DecoyMovedAt<1.5&&max>0&&-delta>=.9f*max){Log.Write("world-health","source-less native kill ("+delta+") right after a decoy move dropped");return;}
                if(BoundaryDamage.Drop(true,delta,max,Active)){
                    if(++boundaryDropped<=50)Log.Write("world-health","map-boundary damage dropped: attacker-less "+delta+" of "+max+" max HP (kill volume / out-of-bounds; MC keeps movement and its own fall/void damage)");
                    return;
                }
            }
            if(delta<0&&StatusTickDropped(id,delta,attacker==player))return;
            float weak;if(delta<0&&id.Length>0&&WeakScale.TryGetValue(id,out weak))delta*=weak;
            // C1: attacker position lets MC apply shield rules even when the attacker
            // (projectile, spawned AoE actor) has no tracked proxy.
            string position="";
            if(attacker!=null&&!attacker.IsDestroyed){
                var p=toMc(attacker.GetActorLocation());
                if(p.Finite)position=",\"sp\":["+F(p.X)+","+F(p.Y)+","+F(p.Z)+"]";
                // Preview88: on a block tower the native body is a ground decoy; an attacker whose centre is more
                // than 2 m below the real MC feet cannot reach the player, so its hit is dropped.
                double feet=ElevatedFeetY;
                if(delta<0&&!double.IsNaN(feet)&&p.Finite&&feet-p.Y>2){
                    if(++droppedHits<=50)Log.Write("world-health","elevated player: hit from "+id+" dropped (attacker y="+F(p.Y)+", feet y="+F(feet)+")");
                    return;
                }
            }
            wire.Send("{\"t\":\"host_health\",\"session\":\""+session+"\",\"hn\":"+(++sequence)+",\"amount\":"+F(-delta/Unit)+",\"source\":\""+id+"\""+position+"}");
            Log.Write("world-health","host HP intent="+delta+" routed to MC; hn="+sequence+" source="+id);
        }
        // Preview107 (user: one fire/poison hit starts a damage-over-time drain; Steve keeps losing HP and the camera shakes):
        // a native status tick (burn/poison/freeze DOT) is a small HP loss (<= 4% max) repeating from the same source at a
        // steady 0.7-2.5 s rhythm with about the same amount, or one credited to the player itself. Such ticks only apply
        // while the player is really swarmed (>= 8 direct hits in the last 20 s); otherwise they are dropped, so neither
        // MC nor native HP falls and MC plays no hurt shake. The first tick of a new effect can still land.
        private readonly Dictionary<string,double[]> lastTick=new Dictionary<string,double[]>();
        private readonly Queue<double> directHits=new Queue<double>();
        private int droppedTicks;
        private int boundaryDropped;
        // Potions & items D1: weakness on an enemy scales its hits on the player; fire resistance and milk/honey block status ticks.
        internal static readonly Dictionary<string,float> WeakScale=new Dictionary<string,float>();
        internal static bool FireResistant;internal static double CureUntil=-100;
        internal static int SwarmHits=20;internal static double SwarmWindow=20,TickShare=.04;
        internal static bool LooksLikeStatusTick(double interval,float previousAmount,float amount,bool selfCredited,float share)
        {
            if(share>TickShare)return false;
            return selfCredited||interval>=.7&&interval<=2.5&&Math.Abs(previousAmount-amount)<=2;
        }
        private bool StatusTickDropped(string id,float delta,bool selfCredited)
        {
            double now=Clock();
            bool shielded=FireResistant||now<CureUntil; /* D1: fire resistance / milk / honey block native status ticks */float max=BGUFunctionLibraryCS.BGUGetFloatAttr(player,EBGUAttrFloat.HpMax);
            float amount=-delta,share=max>0?amount/max:1;
            double[] previous;bool tick=false;
            if(lastTick.TryGetValue(id,out previous))tick=LooksLikeStatusTick(now-previous[0],(float)previous[1],amount,selfCredited,share);
            else tick=selfCredited&&share<=TickShare;
            lastTick[id]=new[]{now,amount};if(lastTick.Count>256)lastTick.Clear();
            while(directHits.Count>0&&now-directHits.Peek()>SwarmWindow)directHits.Dequeue();
            if(!tick){if(id.Length>0)directHits.Enqueue(now);return false;}
            if(directHits.Count>=SwarmHits&&!shielded)return false;
            droppedTicks++;
            if(droppedTicks<=3||droppedTicks%25==0)Log.Write("world-health","status tick dropped (-"+amount+" from "+(id.Length==0?"none":id)+"); applies only after "+SwarmHits+" direct hits in "+SwarmWindow+" s; total dropped="+droppedTicks);
            return true;
        }
        // MC feet Y while the decoy is active, NaN otherwise (set by McBridgeHost every frame).
        internal static double ElevatedFeetY=double.NaN;
        // Preview94: game time when the decoy last moved the native body; a source-less near-total HP loss right after it
        // is a native fall/out-of-bounds kill caused by that teleport, not by the game world, and is dropped.
        internal static double DecoyMovedAt=-100;
        internal static double Clock(){return DateTime.UtcNow.Ticks/1e7;}
        private static int droppedHits;
        private static string F(double value){return value.ToString("0.######",CultureInfo.InvariantCulture);}
        public void Dispose()
        {
            Active=false;
            for(int i=handles.Count-1;i>=0;i--){string error;HookLedger.Uninstall(handles[i],replacements[i],out error);
                var current=HookLedger.ReadHandlers(handles[i]);foreach(var native in originals[i].GetInvocationList()){
                    bool present=false;if(current!=null)foreach(var d in current)if(d.Equals(native))present=true;
                    if(!present)HookLedger.Install(handles[i],native,out error);
                }
            }
            handles.Clear();originals.Clear();replacements.Clear();
        }
    }
}
