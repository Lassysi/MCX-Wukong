using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using b1;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // Preview107 (user, chapter 2 breakable pillar): Wukong's own destructible scenery (walls, pillars, barricades) can be
    // broken by MC pickaxes (held attack, swings counted by tool tier) and MC explosions (TNT, RPG, launchers). Swords,
    // fists and other attacks never break them; Indestructible objects never break. The break is the game's own
    // Evt_HitDestructible, so the native effect, its drop and the saved "destroyed" state stay native. Every probe and
    // break is logged ("scene-break") so other scene interactions can be added from the same evidence.
    internal sealed class SceneBreak
    {
        private readonly BGUPlayerCharacterCS player;
        private readonly Func<double,double,double,FVector> convert;
        private readonly Dictionary<AActor,int> progress=new Dictionary<AActor,int>();
        private readonly HashSet<string> probed=new HashSet<string>();
        private int blastsLogged;
        internal SceneBreak(BGUPlayerCharacterCS player,Func<double,double,double,FVector> convert){this.player=player;this.convert=convert;}

        /// Swings needed by tool tier (wood..netherite) for a Light object; stronger objects need more (pure, tested).
        internal static int SwingsNeeded(string tool,int strength){return SwingsNeeded(tool,strength,0);}
        /// Live112: Efficiency I-V cuts the swings (x1/(1+0.3*level), at least 1).
        internal static int SwingsNeeded(string tool,int strength,int efficiency)
        {
            int baseHits=tool.Contains("netherite")?3:tool.Contains("diamond")?4:tool.Contains("iron")||tool.Contains("golden")?6:tool.Contains("stone")?9:12;
            double factor=strength<=1?1:strength==2?1.5:strength==3?2:3;   // Light/Medium/Heavy/Special
            return Math.Max(1,(int)Math.Ceiling(baseHits*factor/(1+.3*Math.Max(0,Math.Min(5,efficiency)))));
        }

        internal void Mine(string tool,int efficiency,AActor camera,List<AActor> ignore)
        {
            if(string.IsNullOrEmpty(tool)||!tool.EndsWith("_pickaxe",StringComparison.Ordinal))return;
            if(!NativeObjectLifetime.Live(camera))return;
            var start=camera.GetActorLocation();var end=start+camera.GetActorForwardVector()*550f;
            FHitResult hit;
            if(!USystemLibrary.LineTraceSingle(player,start,end,ETraceTypeQuery.TraceTypeQuery1,true,ignore,EDrawDebugTrace.None,out hit,true,default(FLinearColor),default(FLinearColor),0))return;
            var component=hit.Component.Get();var owner=component==null?null:component.GetOwner();
            var point=new FVector(hit.ImpactPoint.X,hit.ImpactPoint.Y,hit.ImpactPoint.Z);
            var target=Destructible(owner)??Nearest(point,200);
            if(target==null){Probe(owner,"not destructible");return;}
            int strength=Strength(target);
            if(strength>=99){Probe(target,"indestructible");return;}
            int done;progress.TryGetValue(target,out done);done++;
            int need=SwingsNeeded(tool,strength,efficiency);
            if(done==1)Log.Write("scene-break","mining "+target.GetClass().GetName()+" strength="+StrengthName(strength)+" tool="+tool+(efficiency>0?" efficiency="+efficiency:"")+" swings needed="+need);
            if(done<need){progress[target]=done;return;}
            progress.Remove(target);
            Break(target,point,"pickaxe "+tool);
        }

        /// Live112 polish (user: pots break with any hit): any MC swing (sword, fist, axe...) breaks Light-strength destructibles
        /// and the game's droppable pots/jars; Medium and stronger scenery (pillars, walls) still needs a pickaxe or explosion.
        internal void Hit(string tool,AActor camera,List<AActor> ignore)
        {
            if(!NativeObjectLifetime.Live(camera))return;
            var start=camera.GetActorLocation();var end=start+camera.GetActorForwardVector()*450f;
            FHitResult hit;
            if(!USystemLibrary.LineTraceSingle(player,start,end,ETraceTypeQuery.TraceTypeQuery1,true,ignore,EDrawDebugTrace.None,out hit,true,default(FLinearColor),default(FLinearColor),0))return;
            var component=hit.Component.Get();var owner=component==null?null:component.GetOwner();
            var point=new FVector(hit.ImpactPoint.X,hit.ImpactPoint.Y,hit.ImpactPoint.Z);
            bool pot=Droppable(owner);
            var target=pot?owner:Destructible(owner)??Nearest(point,120);
            if(target==null)return;
            if(!pot&&!Droppable(target)&&Strength(target)>1){Probe(target,"needs a pickaxe or explosion ("+StrengthName(Strength(target))+")");return;}
            progress.Remove(target);
            Break(target,point,"hit "+(string.IsNullOrEmpty(tool)?"hand":tool));
        }
        /// The game's breakable pots/jars (BGUDroppableDestructionActorBase, not referenced by the SDK wrapper): native class
        /// hierarchy or actor name.
        internal static bool Droppable(AActor actor)
        {
            if(!NativeObjectLifetime.Live(actor))return false;
            try{for(var c=actor.GetClass();c!=null;c=c.GetSuperClass())if((c.GetName()??"").IndexOf("DroppableDestruction",StringComparison.OrdinalIgnoreCase)>=0)return !Destroyed(actor);}catch{}
            return (actor.GetName()??"").IndexOf("DroppableDestruction",StringComparison.OrdinalIgnoreCase)>=0&&!Destroyed(actor);
        }

        internal void Blast(double x,double y,double z,double radius,string source)
        {
            if(!(radius>0)||radius>16)return;
            var centre=convert(x,y,z);float reach=(float)(radius*100*1.25);int broken=0;
            foreach(AActor actor in UObject.GetObjects<AActor>()){
                // Live107: a big wall's pivot can be metres from the blast; measure to its bounding box instead.
                if(!NativeObjectLifetime.Live(actor)||(actor.GetActorLocation()-centre).Size()>reach+3000)continue;
                if(BoxDistance(actor,centre)>reach)continue;
                if(NativeObjectLifetime.ActorWorld(actor)!=NativeObjectLifetime.ActorWorld(player)||!IsDestructible(actor)||Destroyed(actor))continue;
                if(Strength(actor)>=99){Probe(actor,"indestructible (blast)");continue;}
                Break(actor,centre,"explosion "+source);if(++broken>=8)break;
            }
            if(broken==0&&blastsLogged++<10)Log.Write("scene-break","explosion "+source+" r="+radius.ToString("0.0",CultureInfo.InvariantCulture)+": no destructible within "+(reach/100).ToString("0.0",CultureInfo.InvariantCulture)+" m");
        }

        /// Distance from a point to an actor's bounding box (0 inside); falls back to the pivot distance.
        internal static float BoxDistance(AActor actor,FVector p)
        {
            try{
                FVector o,e;actor.GetActorBounds(false,out o,out e,false);
                float dx=Math.Max(0,Math.Abs(p.X-o.X)-e.X),dy=Math.Max(0,Math.Abs(p.Y-o.Y)-e.Y),dz=Math.Max(0,Math.Abs(p.Z-o.Z)-e.Z);
                return (float)Math.Sqrt(dx*dx+dy*dy+dz*dz);
            }catch{return (actor.GetActorLocation()-p).Size();}
        }
        private void Break(AActor target,FVector at,string by)
        {
            try{
                var events=GameApi.Events(target);
                NativeEventInvoker.Invoke(NativeEventInvoker.Signal(events,"Evt_HitDestructible"),
                    player,EGSHitDestructibleStrengthLevel.Special,default(EGSHitDestructibleDirection),new FEffectInstReq(player),2000f);
            }catch(Exception e){Log.Write("scene-break","native hit event failed on "+target.GetClass().GetName()+": "+e.GetType().Name+": "+e.Message);}
            if(!Destroyed(target)&&target is IBGUDestructibleActor){
                try{var dir=target.GetActorLocation()-player.GetActorLocation();
                    ((IBGUDestructibleActor)target).OnDestroyDestructible(at,dir,2000f,0f);
                    Log.Write("scene-break","native event did not destroy "+target.GetClass().GetName()+"; used OnDestroyDestructible fallback");}
                catch(Exception e){Log.Write("scene-break","fallback failed: "+e.GetType().Name+": "+e.Message);}
            }
            Log.Write("scene-break","BROKEN by "+by+": "+target.GetClass().GetName()+" "+target.GetName()+" destroyedFlag="+Destroyed(target));
        }

        // Live107: the chapter 2 pillar is a blueprint class (SM_HFM_Build_ShiBei_FuShu_2_C, actor name
        // DestructibleActor_GEN_VARIABLE_...) whose managed wrapper is not BGUDestructibleActorBase; check the native class
        // hierarchy, the actor name and the destructible data instead.
        private static UClass destructibleClass;
        internal static bool IsDestructible(AActor actor)
        {
            if(!NativeObjectLifetime.Live(actor))return false;
            if(actor is BGUDestructibleActorBase)return true;
            try{if(destructibleClass==null)destructibleClass=UClass.GetClass<BGUDestructibleActorBase>();
                if(destructibleClass!=null&&actor.GetClass().IsChildOf(destructibleClass))return true;}catch{}
            if((actor.GetName()??"").IndexOf("DestructibleActor",StringComparison.OrdinalIgnoreCase)>=0)return true;
            return Prop(Data(actor),"StrengthLevel")!=null;
        }
        private static AActor Destructible(AActor actor){return IsDestructible(actor)&&!Destroyed(actor)?actor:null;}
        private AActor Nearest(FVector point,float within)
        {
            AActor best=null;float bestDistance=within;
            foreach(AActor actor in UObject.GetObjects<AActor>()){
                if(!NativeObjectLifetime.Live(actor))continue;
                float d=(actor.GetActorLocation()-point).Size();if(d>=bestDistance)continue;
                if(NativeObjectLifetime.ActorWorld(actor)!=NativeObjectLifetime.ActorWorld(player)||!IsDestructible(actor)||Destroyed(actor))continue;
                bestDistance=d;best=actor;
            }
            return best;
        }
        private void Probe(AActor actor,string verdict)
        {
            string key=(actor==null?"none":actor.GetClass().GetName())+"|"+verdict;
            if(probed.Count<64&&probed.Add(key))Log.Write("scene-break","probe: "+(actor==null?"no actor":actor.GetClass().GetName()+" "+actor.GetName())+" -> "+verdict);
        }

        // ---------------------------------------------------------------- native destructible data (reflection, like shrines)
        private static readonly BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static MethodInfo reader;
        // BUC_DestructibleData is internal to the game assembly; resolved by name.
        private static readonly Type DataType=typeof(BUS_DestructibleComp).Assembly.GetType("b1.BUC_DestructibleData");
        private static object Data(AActor actor)
        {
            try{
                if(reader==null&&DataType!=null){
                    foreach(var name in new[]{"GetUnPersistentReadOnlyData","GetReadOnlyData"}){
                        foreach(var m in typeof(BGU_DataUtil).GetMethods(Any)){
                            var p=m.GetParameters();
                            if(m.Name==name&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==1&&p.Length==1&&p[0].ParameterType==typeof(AActor)){reader=m.MakeGenericMethod(DataType);break;}
                        }
                        if(reader!=null)break;
                    }
                }
                return reader==null?null:reader.Invoke(null,new object[]{actor});
            }catch{return null;}
        }
        private static object Prop(object o,string name)
        {
            if(o==null)return null;var p=o.GetType().GetProperty(name,Any);
            try{return p==null?null:p.GetValue(o,null);}catch{return null;}
        }
        internal static int Strength(AActor actor)
        {
            var v=Prop(Data(actor),"StrengthLevel");
            if(v==null)return 1;
            int raw=Convert.ToInt32(v,CultureInfo.InvariantCulture);   // DestructibleStrengthLevel: Light0 Medium1 Heavy2 Special3 Indestructible99
            return raw>=99?99:raw+1;
        }
        private static bool Destroyed(AActor actor)
        {
            var v=Prop(Data(actor),"IsDestroyed");
            return v is bool&&(bool)v;
        }
        private static string StrengthName(int s){return s>=99?"Indestructible":s<=1?"Light":s==2?"Medium":s==3?"Heavy":"Special";}
    }
}
