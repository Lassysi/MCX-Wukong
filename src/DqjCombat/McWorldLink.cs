using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using b1;
using BtlShare;
using LitJson;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // All native access stays on the existing late-registered game ticker.
    // MC/mod outcomes are acknowledged; snapshots subtract unacknowledged deltas in MC.
    internal sealed class McWorldLink : IDisposable
    {
        private readonly BGUPlayerCharacterCS player;
        private readonly McBridgeWire wire;
        private readonly Func<double,double,double,FVector> convert;
        private readonly Func<FVector,BridgePoint> toMc;
        private readonly Dictionary<string,BGUCharacterCS> targets=new Dictionary<string,BGUCharacterCS>();
        private readonly Dictionary<string,float> targetUnits=new Dictionary<string,float>();
        private readonly HashSet<string> sizeLogged=new HashSet<string>();
        // Items 1/4: after a Boss-class kill the chapter-end cutscene and UI play natively.
        internal static DateTime LastBossDefeatUtc=DateTime.MinValue;
        private static readonly float BossHpMax=10000f;
        // Preview71 (user: Boss hard to hit): a Boss capsule is far smaller than its body (Niu: capsule
        // R120 vs bounds 327x279). Boss-class proxies use 75% of the narrower horizontal bounds
        // extent when larger than the capsule (max 6 m radius) and at least 90% of the bounds height.
        // Bounds include animation and weapons, so they are sampled once per second, not per frame.
        private readonly Dictionary<string,float[]> bossSize=new Dictionary<string,float[]>();
        private readonly Dictionary<string,int> bossSizeAt=new Dictionary<string,int>();
        private void BossProxySize(string id,BGUCharacterCS actor,float radius,float half,out float proxyRadius,out float proxyHalf)
        {
            proxyRadius=radius;proxyHalf=half;
            int now=Environment.TickCount,at;float[] size;
            if(!bossSize.TryGetValue(id,out size)||!bossSizeAt.TryGetValue(id,out at)||unchecked(now-at)>1000){
                try{
                    FVector origin,extent;actor.GetActorBounds(false,out origin,out extent,false);
                    float wide=Math.Min(600f,Math.Max(radius,.75f*Math.Min(extent.X,extent.Y)));
                    float tall=Math.Max(half,.9f*extent.Z);
                    if(Finite(wide)&&Finite(tall)){
                        bool first=!bossSize.ContainsKey(id);
                        size=new[]{wide,tall};bossSize[id]=size;bossSizeAt[id]=now;
                        if(first)Log.Write("target-size","id="+id+" Boss proxy radius "+F(radius)+"->"+F(wide)+" halfHeight "+F(half)+"->"+F(tall));
                    }
                }catch(Exception e){bossSizeAt[id]=now;Log.Write("target-size","id="+id+" Boss bounds error "+e.GetType().Name);}
            }
            if(size!=null){proxyRadius=size[0];proxyHalf=size[1];}
        }
        private readonly Dictionary<string,List<AActor>> blocks=new Dictionary<string,List<AActor>>();
        private readonly Dictionary<string,int> terrain=new Dictionary<string,int>();
        private readonly HashSet<string> nativeDrawn=new HashSet<string>();
        private void SendNative(string op,int x,int y,int z){wire.Send("{\"t\":\"native_blocks\",\""+op+"\":["+x+","+y+","+z+"]}");}
        private readonly string session=Guid.NewGuid().ToString("N");
        private long ack;
        private double nextEntities,nextTargets,nextTerrain,nextLog;
        private float healthUnit=1;
        private bool started;
        private int voxelIndex;
        private McPlayerHealthGate healthGate;
        internal McWorldLink(BGUPlayerCharacterCS player,McBridgeWire wire,Func<double,double,double,FVector> convert,Func<FVector,BridgePoint> toMc)
        {this.player=player;this.wire=wire;this.convert=convert;this.toMc=toMc;}
        private static string F(double n){return n.ToString("0.######",CultureInfo.InvariantCulture);}
        private static double N(JsonData d){return double.Parse(d.ToString(),CultureInfo.InvariantCulture);}
        private static bool Finite(double n){return !double.IsNaN(n)&&!double.IsInfinity(n);}
        private BridgePoint ToMc(FVector p){return toMc(p);}
        private static bool Alive(BGUCharacterCS actor){return NativeObjectLifetime.Live(actor)&&!BGUFunctionLibraryCS.BGUIsUnitDead(actor);}
        internal void IgnoreOwned(List<AActor> ignore){foreach(var actor in targets.Values)ignore.Add(actor);foreach(var owned in blocks.Values)ignore.AddRange(owned);}
        internal void ResetTerrain(){terrain.Clear();voxelIndex=0;voxelsSinceMove=0;}
        private int terrainCellX=int.MinValue,terrainCellY,terrainCellZ,voxelsSinceMove;
        internal void Update(double now,McBridgeWire.Frame frame,AActor camera)
        {
            float maximum=BGUFunctionLibraryCS.BGUGetFloatAttr(player,EBGUAttrFloat.HpMax);
            if(maximum<=0||!Finite(maximum))return;healthUnit=maximum/20f;
            if(!started){started=true;healthGate=new McPlayerHealthGate(player,wire,session,toMc);healthGate.Install();wire.Send("{\"t\":\"block_snapshot\"}");Log.Write("world-link","native MC world link session="+session+" healthUnit="+F(healthUnit));}
            healthGate.Unit=healthUnit;
            JsonData ev;int handled=0;
            while(handled++<96&&wire.TryEvent(out ev)){
                try{
                    string type=(string)ev["t"];
                    if(type=="health_delta")ApplyHealth(ev);
                    else if(type=="block_reset")ClearBlocks();
                    else if(type=="block_cells")foreach(JsonData cell in ev["cells"])ApplyCell(cell);
                    else if(type=="target_block")Outline(ev);
                }catch(Exception e){Log.Write("world-error",e.GetType().Name+": "+e.Message);}
            }
            if(now>=nextTargets){nextTargets=now+.5;RefreshTargets();}
            if(now>=nextNativeProbe){nextNativeProbe=now+5;NativeProbe();}
            // C1: every host frame (was 10 Hz) so MC proxies follow moving enemies.
            SendEntities();
            EnemyBlockHits(now);
            if(now>=nextTerrain){nextTerrain=now+.05;Terrain(frame,camera);}
            if(now>=nextLog){nextLog=now+5;Log.Write("world-link","targets="+targets.Count+" blocks="+blocks.Count+" ack="+ack+" hp="+F(BGUFunctionLibraryCS.BGUGetFloatAttr(player,EBGUAttrFloat.Hp)));}
        }
        private void RefreshTargets()
        {
            targets.Clear();var at=player.GetActorLocation();
            foreach(BGUCharacterCS actor in UObject.GetObjects<BGUCharacterCS>()){
                if(actor==player||!Alive(actor)||NativeObjectLifetime.ActorWorld(actor)!=NativeObjectLifetime.ActorWorld(player)||!BGUFunctionLibraryCS.BGUIsEnemyTeam(player,actor))continue;
                if((actor.GetActorLocation()-at).Size()>6400)continue;
                targets[actor.GetUniqueID().ToString(CultureInfo.InvariantCulture)]=actor;
                if(targets.Count>=128)break;
            }
        }
        private void SendEntities()
        {
            var json=new StringBuilder("{\"t\":\"host_entities\",\"session\":\"");json.Append(session).Append("\",\"ack\":").Append(ack)
                .Append(",\"hp\":").Append(F(BGUFunctionLibraryCS.BGUGetFloatAttr(player,EBGUAttrFloat.Hp)/healthUnit)).Append(",\"entities\":[");
            bool comma=false;
            foreach(var pair in targets){
                var actor=pair.Value;if(!Alive(actor))continue;
                float hp=BGUFunctionLibraryCS.BGUGetFloatAttr(actor,EBGUAttrFloat.Hp),max=BGUFunctionLibraryCS.BGUGetFloatAttr(actor,EBGUAttrFloat.HpMax);
                if(!Finite(hp)||!Finite(max)||max<=0)continue;
                var capsule=actor.CapsuleComponent;if(capsule==null||capsule.IsDestroyed)continue;
                float half=capsule.GetScaledCapsuleHalfHeight(),radius=capsule.GetScaledCapsuleRadius();
                float proxyRadius=radius,proxyHalf=half;
                if(max>=BossHpMax)BossProxySize(pair.Key,actor,radius,half,out proxyRadius,out proxyHalf);
                if(sizeLogged.Add(pair.Key)){
                    // Preview67 diagnostic for Boss hitboxes: capsule vs whole-actor bounds.
                    try{FVector origin,extent;actor.GetActorBounds(false,out origin,out extent,false);
                        Log.Write("target-size","id="+pair.Key+" class="+actor.GetClass().GetName()+" hpMax="+F(max)+" capsuleR="+F(radius)+" capsuleHalf="+F(half)+" boundsExtent="+F(extent.X)+","+F(extent.Y)+","+F(extent.Z));
                    }catch(Exception e){Log.Write("target-size","id="+pair.Key+" bounds error "+e.GetType().Name);}
                }
                var pos=ToMc(actor.GetActorLocation()-new FVector(0,0,half));
                if(comma)json.Append(',');comma=true;
                float unit=Math.Max(healthUnit,max/1024f);targetUnits[pair.Key]=unit;
                json.Append("{\"id\":\"").Append(pair.Key).Append("\",\"hp\":").Append(F(hp/unit))
                    .Append(",\"max\":").Append(F(max/unit)).Append(",\"w\":").Append(F(proxyRadius*.02))
                    .Append(",\"h\":").Append(F(proxyHalf*.02)).Append(",\"p\":[").Append(F(pos.X)).Append(',').Append(F(pos.Y)).Append(',').Append(F(pos.Z)).Append("]}");
            }
            wire.Send(json.Append("]}").ToString());
        }
        private void ApplyHealth(JsonData message)
        {
            if((string)message["session"]!=session)return;
            long seq=(long)N(message["seq"]);if(seq<=ack)return;
            // FIFO on one WebSocket; a sequence gap forces resync rather than silent damage loss.
            if(seq!=ack+1)throw new InvalidOperationException("MC damage sequence gap "+ack+"->"+seq);
            string id=(string)message["id"];double amount=N(message["amount"]);
            if(!Finite(amount)||Math.Abs(amount)>8192){ack=seq;return;}
            BGUCharacterCS victim=null;if(id=="player")victim=player;else targets.TryGetValue(id,out victim);
            bool finishing=false;var deathRequest=default(FEffectInstReq);
            if(Alive(victim)){
                float unit=healthUnit;if(id!="player")targetUnits.TryGetValue(id,out unit);if(unit<=0)unit=healthUnit;
                float before=BGUFunctionLibraryCS.BGUGetFloatAttr(victim,EBGUAttrFloat.Hp),max=BGUFunctionLibraryCS.BGUGetFloatAttr(victim,EBGUAttrFloat.HpMax);
                if(amount<0){BGUFunctionLibraryCS.BGUSetAttrValue(victim,EBGUAttrFloat.Hp,Math.Min(max,before+(float)(-amount*unit)));}
                else if(amount>0&&max>0){
                    BGUCharacterCS source=player;
                    if(victim==player){source=null;targets.TryGetValue((string)message["source"],out source);}
                    var request=source==null?default(FEffectInstReq):new FEffectInstReq(source);
                    bool enemy=victim!=player;
                    request.HitLocation=victim.GetActorLocation();request.HitComp=victim.CapsuleComponent;
                    request.FXWeight_HitChr=enemy?100:0;
                    if(enemy){request.Attacker=player;request.HitDiretionRealDir=victim.GetActorLocation()-player.GetActorLocation();}
                    // Verified native absolute HP-ratio path. No host attack animation or attack-stat approximation.
                    // The host still owns immunity, HP events, native death, rewards and quest progression.
                    // Live54: FallDmg works but never takes an enemy below 1 HP. Live55: Skill without a
                    // real skill effect ID computes 0 damage. So ordinary hits stay FallDmg, and a hit
                    // that would reach the 1-HP floor uses DeadZone, which IsDamageValid always accepts
                    // and which is lethal.
                    double hostDamage=amount*unit;
                    // LIFE-01 (live61): native damage on the player is lethal and starts Wukong's own
                    // death. MC owns player death (death screen -> Respawn -> native rebirth), so the
                    // host player is held at 1 HP minimum.
                    if(!enemy)hostDamage=Math.Min(hostDamage,before-1.0);
                    // Native rules (BtlSvr.Main): our absolute damage is only honoured for FallDmg/
                    // DeadZone; FallDmg leaves enemies at 1 HP; DeadZone/GM kills use dead reason 7
                    // (no death montage, live56). So damage stays FallDmg and the killing blow raises
                    // the native Evt_UnitDead with reason 2 (combat death), as the native settlement does.
                    finishing=enemy&&hostDamage>=before-1.0001;
                    if(hostDamage<=0){Log.Write("world-health","id=player MC="+F(amount)+" host held at "+F(before)+" (MC owns death)");}
                    else{
                    var config=new FSkillDamageConfig{DmgReason=EDamageReason.FallDmg,DamageCalcType=EDamageCalcType.HPMaxRatioAbs,
                        HPMaxINV10000Damage_Abs=(float)(hostDamage/max*10000),DamageImmueLevel=victim==player?2:0,CameraShakeDA=""};
                    deathRequest=request;
                    healthGate.Applying=true;
                    try{NativeEventInvoker.Invoke(NativeEventInvoker.Signal(GameApi.Events(victim),"Evt_TriggerNormalDamageEffect"),source,config,request,new FBattleAttrSnapShot(source,false));}
                    finally{healthGate.Applying=false;}
                    }
                    // Preview58: the absolute-damage path carries no stiff; raise the native normal stiff.
                    if(enemy&&!finishing&&Alive(victim)){
                        try{NativeHitStun.Try(id,victim,player,request,default(FSkillDamageConfig),amount,(string)message["reason"]);}
                        catch(Exception e){Log.Write("world-error","hit-stun: "+e.GetType().Name+": "+e.Message);}
                    }
                }
                float after=BGUFunctionLibraryCS.BGUGetFloatAttr(victim,EBGUAttrFloat.Hp);
                Log.Write("world-health","id="+id+" seq="+seq+" MC="+F(amount)+" reason="+(string)message["reason"]+" host="+F(before)+"->"+F(after));
                if(finishing&&Alive(victim)){
                    try{
                        if(max>=BossHpMax){LastBossDefeatUtc=DateTime.UtcNow;Log.Write("cutscene","Boss-class enemy defeated (hpMax="+F(max)+"); story cutscenes allowed for 180 s");}
                        CombatDeath(victim,deathRequest);
                        Log.Write("world-health","id="+id+" killing blow: native combat death (reason 2); dead="+BGUFunctionLibraryCS.BGUIsUnitDead(victim));
                        if(Alive(victim)){BGUFunctionLibraryCS.BGUGMDead(victim);Log.Write("world-health","id="+id+" combat death did not take; GM death fallback; dead="+BGUFunctionLibraryCS.BGUIsUnitDead(victim));}
                    }catch(Exception e){Log.Write("world-error","native death: "+e.GetType().Name+": "+e.Message);}
                }
            }
            ack=seq;SendEntities();
        }
        // Evt_UnitDead(victim, EDeadReason, effectId, stiffLevel, montage, request, elemDot, abnormal):
        // the same event the native death settlement raises. Parameter types come from the
        // live delegate so the generated EDeadReason/EAbnormalStateType need no SDK reference.
        private static void CombatDeath(BGUCharacterCS victim,FEffectInstReq request)
        {
            var signal=NativeEventInvoker.Signal(GameApi.Events(victim),"Evt_UnitDead");
            if(signal==null)throw new InvalidOperationException("Native death event is not ready");
            System.Reflection.MethodInfo invoke=null;
            foreach(var method in signal.GetType().GetMethods(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic))
                if(method.Name=="Invoke"&&method.GetParameters().Length==8)invoke=method;
            if(invoke==null)throw new MissingMethodException(signal.GetType().FullName,"Invoke(8)");
            var parameters=invoke.GetParameters();
            object reason=Enum.ToObject(parameters[1].ParameterType,2);
            object abnormal=Activator.CreateInstance(parameters[7].ParameterType);
            try{invoke.Invoke(signal,new object[]{victim,reason,-1,1,null,request,false,abnormal});}
            catch(System.Reflection.TargetInvocationException e){throw e.InnerException??e;}
        }
        private void ApplyCell(JsonData cell)
        {
            var p=cell["p"];int x=(int)N(p[0]),y=(int)N(p[1]),z=(int)N(p[2]);string key=x+","+y+","+z;
            List<AActor> old;if(blocks.TryGetValue(key,out old)){foreach(var actor in old)if(NativeObjectLifetime.Live(actor))actor.DestroyActor();blocks.Remove(key);}
            if(nativeDrawn.Remove(key))SendNative("del",x,y,z);
            outlineBounds.Remove(key);if(key==outlineKey)HideOutline();
            var boxes=cell["boxes"];
            string propId=(string)cell["id"];
            if(boxes.Count==0){if(NativeBlockMesh.Prop(propId))NativeProp(cell,key,x,y,z,propId);return;}
            if(blocks.Count>=4096||boxes.Count>128)throw new InvalidOperationException("MC block collision capacity exceeded");
            var actors=new List<AActor>();blocks.Add(key,actors);var shapes=new List<double[]>();
            foreach(JsonData b in boxes){
                double lx=N(b[0]),ly=N(b[1]),lz=N(b[2]),hx=N(b[3]),hy=N(b[4]),hz=N(b[5]);
                if(!Finite(lx+ly+lz+hx+hy+hz)||hx<=lx||hy<=ly||hz<=lz||Math.Max(hx-lx,Math.Max(hy-ly,hz-lz))>8)continue;
                FVector center=convert(x+(lx+hx)/2,y+(ly+hy)/2,z+(lz+hz)/2);
                var actor=BGUFunctionLibraryCS.BGUSpawnActor(NativeObjectLifetime.ActorWorld(player),UClass.GetClass<AActor>(),center,default(FRotator));
                if(actor==null)continue;actors.Add(actor);actor.SetOwner(player);
                var box=UObject.NewObject<UBoxComponent>(actor);actor.RootComponent=box;
                box.SetBoxExtent(new FVector((float)((hz-lz)*50),(float)((hx-lx)*50),(float)((hy-ly)*50)),false);
                // Preview88 (user: blocks and monsters need real collision): explicit responses instead of the
                // named "BlockAll" profile, which this project may not define (then the box stays overlap-only).
                // World-dynamic, blocking every channel, like wukong-steve's block boxes. The hidden Wukong
                // pawn ignores them: MC already resolves the player's own collision with MC blocks.
                box.SetHiddenInGame(true,false);
                box.SetCollisionObjectType(ECollisionChannel.ECC_WorldDynamic);box.SetCollisionResponseToAllChannels(ECollisionResponseType.ECR_Block);
                box.SetCollisionEnabled(ECollisionEnabled.QueryAndPhysics);UGSE_EngineFuncLib.RegisterComponent(box);
                try{player.CapsuleComponent.IgnoreActorWhenMoving(actor,true);box.IgnoreActorWhenMoving(player,true);}catch(Exception e){Log.Write("world-error","block ignore pawn: "+e.Message);}
                actor.SetActorLocation(center,false,out _,true);
                shapes.Add(new[]{lx,ly,lz,hx,hy,hz});
            }
            string id=(string)cell["id"];bool native=false;
            // Preview84: cube-like blocks are drawn by Wukong on the first shape actor; MC stops drawing them.
            if(actors.Count>0&&!(bool)cell["conservative"]&&NativeBlockMesh.Known(id)){
                try{
                    string axis=cell.Keys.Contains("axis")?(string)cell["axis"]:"y";
                    double ax=cell.Keys.Contains("anchorDx")?N(cell["anchorDx"]):0,az=cell.Keys.Contains("anchorDz")?N(cell["anchorDz"]):0;
                    var mesh=NativeBlockMesh.Build(actors[0],actors[0].GetActorLocation(),id,axis,ax,N(cell["anchorDy"]),az,x,y,z,shapes,convert);
                    if(mesh!=null){
                        native=true;nativeDrawn.Add(key);SendNative("add",x,y,z);
                        double lx=1e9,ly=1e9,lz=1e9,hx=-1e9,hy=-1e9,hz=-1e9;
                        foreach(var s in shapes){lx=Math.Min(lx,s[0]);ly=Math.Min(ly,s[1]);lz=Math.Min(lz,s[2]);hx=Math.Max(hx,s[3]);hy=Math.Max(hy,s[4]);hz=Math.Max(hz,s[5]);}
                        outlineBounds[key]=new[]{x+lx,y+ly,z+lz,x+hx,y+hy,z+hz};
                    }
                }catch(Exception e){Log.Write("native-block-error","cell="+key+" "+e.GetType().Name+": "+e.Message);}
            }
            Log.Write("world-block","cell="+key+" id="+id+" shapes="+actors.Count+" conservative="+(bool)cell["conservative"]+" anchorDy="+N(cell["anchorDy"]).ToString("F4",CultureInfo.InvariantCulture)+" native="+native);
        }
        // Preview85: a collisionless prop (flower, grass, sapling, torch) still gets a Wukong-drawn mesh on its own actor.
        private void NativeProp(JsonData cell,string key,int x,int y,int z,string id)
        {
            if(blocks.Count>=4096)return;
            double ax=cell.Keys.Contains("anchorDx")?N(cell["anchorDx"]):0,ay=N(cell["anchorDy"]),az=cell.Keys.Contains("anchorDz")?N(cell["anchorDz"]):0;
            var center=convert(x+.5+ax,y+.5+ay,z+.5+az);
            var actor=BGUFunctionLibraryCS.BGUSpawnActor(NativeObjectLifetime.ActorWorld(player),UClass.GetClass<AActor>(),center,default(FRotator));
            if(actor==null)return;actor.SetOwner(player);
            var actors=new List<AActor>{actor};blocks.Add(key,actors);
            try{
                var root=UObject.NewObject<USceneComponent>(actor);actor.RootComponent=root;UGSE_EngineFuncLib.RegisterComponent(root);
                actor.SetActorLocation(center,false,out _,true);
                var mesh=NativeBlockMesh.Build(actor,actor.GetActorLocation(),id,"y",ax,ay,az,x,y,z,new List<double[]>(),convert);
                if(mesh!=null){nativeDrawn.Add(key);SendNative("add",x,y,z);outlineBounds[key]=new[]{x+ax+.2,y+ay,z+az+.2,x+ax+.8,y+ay+.8,z+az+.8};}
                Log.Write("world-block","cell="+key+" id="+id+" prop native="+(mesh!=null));
            }catch(Exception e){Log.Write("native-block-error","prop cell="+key+" "+e.GetType().Name+": "+e.Message);}
        }
        private void Terrain(McBridgeWire.Frame frame,AActor camera)
        {
            var ignore=new List<AActor>{player,camera};foreach(var actor in targets.Values)ignore.Add(actor);
            foreach(var owned in blocks.Values)ignore.AddRange(owned);
            var json=new StringBuilder("{\"t\":\"voxels\",\"c\":[");bool comma=false;
            int mx=(int)Math.Floor(frame.X),my=(int)Math.Floor(frame.Y),mz=(int)Math.Floor(frame.Z);
            // Preview79 (stutter): 48 box traces every 50 ms while moving. Once the player has stood in
            // one cell for a full 1089-voxel pass, only 8 per call refresh the cube (changes still land in ~7 s).
            if(mx!=terrainCellX||my!=terrainCellY||mz!=terrainCellZ){terrainCellX=mx;terrainCellY=my;terrainCellZ=mz;voxelsSinceMove=0;}
            int budget=voxelsSinceMove>=1089?8:48;voxelsSinceMove+=budget;
            // Bounded native collision raster, including walls. Air removes only host barrier proxies.
            // Exact sub-voxel landscape carving and distant terrain are separate work.
            for(int i=0;i<budget;i++){
                int n=voxelIndex++%1089;int x=mx+n%11-5,z=mz+(n/11)%11-5,y=my+n/121-3;
                // Preview95 (user: stairs bumpy, often stuck): the probe covers only the upper part of the cell
                // (35-95% of its height) and 0.4 m across. The lower part is the exact-height surface proxy's job; the old
                // 0.6 x 0.88 m probe caught the next stair's edge and turned whole 1 m cells above the real steps solid.
                var center=convert(x+.5,y+.65,z+.5);FHitResult hit;
                bool solid=PawnSolid(center,ignore);
                string key=x+","+y+","+z;int value=solid?1:0,previous;
                if(terrain.TryGetValue(key,out previous)&&previous==value)continue;terrain[key]=value;
                if(comma)json.Append(',');comma=true;json.Append(x).Append(',').Append(y).Append(',').Append(z).Append(',').Append(value);
            }
            if(comma)wire.Send(json.Append("]}").ToString());
            if(terrain.Count>16384)terrain.Clear();
        }
        // Preview69 (user: many spots blocked): the Visibility channel also hits foliage, decor and
        // visibility-only meshes the native character walks through. A voxel is a wall only when a
        // static/dynamic component there blocks the Pawn channel, as native movement does. The probe is
        // 0.6 x 0.6 blocks (Steve's width) so 1-1.5 m gaps between rocks stay open.
        private static readonly List<EObjectTypeQuery> WallObjects=new List<EObjectTypeQuery>{EObjectTypeQuery.ObjectTypeQuery1,EObjectTypeQuery.ObjectTypeQuery2};
        private int ignoredVoxels;private double nextVoxelLog;
        private static bool? explorer;private readonly HashSet<string> explorerSkipped=new HashSet<string>();
        internal static bool Explorer(){
            if(explorer==null){explorer=System.IO.File.Exists(System.IO.Path.Combine(Log.Dir,"dqj-explorer.on"));Log.Write("explorer","Explorer mode "+(explorer.Value?"ON: invisible blockers are passable":"off"));}
            return explorer.Value;
        }
        private bool PawnSolid(FVector center,List<AActor> ignore)
        {
            List<FHitResult> hits;
            if(!USystemLibrary.BoxTraceMultiForObjects(player,center-new FVector(0,0,.1f),center+new FVector(0,0,.1f),new FVector(20,20,30),default(FRotator),WallObjects,false,ignore,EDrawDebugTrace.None,out hits,true,default(FLinearColor),default(FLinearColor),0)||hits==null)return false;
            bool any=false;
            foreach(var hit in hits){
                var component=hit.Component.Get();if(component==null||component.IsDestroyed)continue;any=true;
                // Preview92 Explorer mode (switch file dqj-explorer.on): invisible native blockers (hidden components,
                // blocking volumes: story gates, level bounds) are not rastered, so MC can walk through them.
                if(Explorer()){
                    var owner=component.GetOwner();string cls=owner==null?"":owner.GetClass().GetName();
                    if(!component.IsVisible()||cls.IndexOf("Volume",StringComparison.OrdinalIgnoreCase)>=0){
                        if(explorerSkipped.Add(cls+" "+(owner==null?"":owner.GetName())))Log.Write("explorer","invisible blocker not rastered: "+cls+" "+(owner==null?"":owner.GetName()));
                        continue;
                    }
                }
                if(component.GetCollisionResponseToChannel(ECollisionChannel.ECC_Pawn)==ECollisionResponseType.ECR_Block)return true;
            }
            if(any){ignoredVoxels++;double now=UGameplayStatics.GetRealTimeSeconds(player);if(now>=nextVoxelLog){nextVoxelLog=now+10;Log.Write("world-terrain","voxels skipped (no Pawn block) total="+ignoredVoxels);}}
            return false;
        }
        // Item 6a (user 2026-10-16): Wukong enemies break MC blocks in their way. An enemy in
        // battle standing next to an MC block and facing it lands one hit per 1.2 s; the third
        // hit on a block asks MC to break it. Native attack sweeps do not reach our block boxes.
        private readonly Dictionary<string,int> blockHits=new Dictionary<string,int>();
        private readonly Dictionary<string,double> enemyNextHit=new Dictionary<string,double>();
        private double nextBlockScan;
        private static readonly int HitsToBreak=3;
        private void EnemyBlockHits(double now)
        {
            if(now<nextBlockScan||blocks.Count==0||targets.Count==0)return;nextBlockScan=now+.2;
            foreach(var pair in targets){
                var enemy=pair.Value;if(!Alive(enemy))continue;
                double ready;if(enemyNextHit.TryGetValue(pair.Key,out ready)&&now<ready)continue;
                bool battling=false;try{battling=BGUFunctionLibraryCS.IsInBattling(enemy);}catch{}
                if(!battling)continue;
                var capsule=enemy.CapsuleComponent;if(capsule==null||capsule.IsDestroyed)continue;
                float radius=capsule.GetScaledCapsuleRadius(),half=capsule.GetScaledCapsuleHalfHeight();
                var at=enemy.GetActorLocation();var forward=enemy.GetActorForwardVector();
                string best=null;double bestDistance=double.MaxValue;
                foreach(var key in blocks.Keys){
                    var parts=key.Split(',');var center=convert(int.Parse(parts[0])+.5,int.Parse(parts[1])+.5,int.Parse(parts[2])+.5);
                    double dx=center.X-at.X,dy=center.Y-at.Y,dz=center.Z-at.Z,flat=Math.Sqrt(dx*dx+dy*dy);
                    if(flat>radius+120||Math.Abs(dz)>half+60||flat<1)continue;
                    if((dx*forward.X+dy*forward.Y)/flat<.3)continue;
                    if(flat<bestDistance){bestDistance=flat;best=key;}
                }
                if(best==null)continue;
                enemyNextHit[pair.Key]=now+1.2;
                int hits;blockHits.TryGetValue(best,out hits);hits++;
                if(hits<HitsToBreak){blockHits[best]=hits;Log.Write("enemy-break","enemy="+pair.Key+" hit block "+best+" ("+hits+"/"+HitsToBreak+")");continue;}
                blockHits.Remove(best);
                wire.Send("{\"t\":\"enemy_break\",\"p\":["+best+"]}");
                Log.Write("enemy-break","enemy="+pair.Key+" broke block "+best);
            }
        }
        // Preview86: selection outline of a Wukong-drawn block, drawn in the host frame (MC skips its own).
        private readonly Dictionary<string,double[]> outlineBounds=new Dictionary<string,double[]>();
        // Preview86 (BUG-11: user sees Wukong-drawn red blocks drift against the terrain): every 5 s, up to three
        // native blocks log their actor location next to the location recomputed now from their MC cell.
        // A changing actor location = something moves it; a changing recomputed value = coordinate origin moved.
        private double nextNativeProbe;
        private void NativeProbe()
        {
            int n=0;
            foreach(var pair in outlineBounds){
                if(n++>=3)break;List<AActor> owned;
                if(!blocks.TryGetValue(pair.Key,out owned)||owned.Count==0||owned[0]==null||owned[0].IsDestroyed)continue;
                var b=pair.Value;var actor=owned[0].GetActorLocation();var expected=convert((b[0]+b[3])/2,(b[1]+b[4])/2,(b[2]+b[5])/2);
                Log.Write("native-probe","cell="+pair.Key+" actor="+F(actor.X)+","+F(actor.Y)+","+F(actor.Z)+" expectedNow="+F(expected.X)+","+F(expected.Y)+","+F(expected.Z));
            }
        }
        private AActor outlineActor;private string outlineKey;
        private void Outline(JsonData ev)
        {
            if(!ev.Keys.Contains("p")){HideOutline();return;}
            var p=ev["p"];string key=(int)N(p[0])+","+(int)N(p[1])+","+(int)N(p[2]);
            double[] b;if(key==outlineKey)return;HideOutline();
            if(!outlineBounds.TryGetValue(key,out b))return;
            try{
                var center=convert((b[0]+b[3])/2,(b[1]+b[4])/2,(b[2]+b[5])/2);
                outlineActor=BGUFunctionLibraryCS.BGUSpawnActor(NativeObjectLifetime.ActorWorld(player),UClass.GetClass<AActor>(),center,default(FRotator));
                if(outlineActor==null)return;outlineActor.SetOwner(player);
                var root=UObject.NewObject<USceneComponent>(outlineActor);outlineActor.RootComponent=root;UGSE_EngineFuncLib.RegisterComponent(root);
                outlineActor.SetActorLocation(center,false,out _,true);
                NativeBlockMesh.OutlineMesh(outlineActor,outlineActor.GetActorLocation(),b,convert);
                outlineKey=key;
            }catch(Exception e){Log.Write("native-block-error","outline "+key+" "+e.GetType().Name+": "+e.Message);HideOutline();}
        }
        private void HideOutline(){if(outlineActor!=null)try{if(NativeObjectLifetime.Live(outlineActor))outlineActor.DestroyActor();}catch{}outlineActor=null;outlineKey=null;}
        private void ClearBlocks(){HideOutline();outlineBounds.Clear();foreach(var list in blocks.Values)foreach(var actor in list)try{if(NativeObjectLifetime.Live(actor))actor.DestroyActor();}catch{}blocks.Clear();
            if(nativeDrawn.Count>0){nativeDrawn.Clear();try{wire.Send("{\"t\":\"native_blocks\",\"clear\":true}");}catch{}}}
        public void Dispose(){if(healthGate!=null)healthGate.Dispose();healthGate=null;ClearBlocks();targets.Clear();targetUnits.Clear();terrain.Clear();started=false;}
    }
}
