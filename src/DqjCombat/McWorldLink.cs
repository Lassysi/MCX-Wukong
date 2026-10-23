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
        private readonly Dictionary<string,float[]> bodyBounds=new Dictionary<string,float[]>();
        private readonly Dictionary<string,int> bodyBoundsAt=new Dictionary<string,int>();
        private void BodyProxy(string id,BGUCharacterCS actor,float radius,float half,bool boss,out float proxyRadius,out float proxyHalf,out FVector feet)
        {
            var location=actor.GetActorLocation();
            proxyRadius=radius;proxyHalf=half;feet=location-new FVector(0,0,half);
            if(boss)BossProxySize(id,actor,radius,half,out proxyRadius,out proxyHalf);
            int now=Environment.TickCount,at;float[] b;
            if(!bodyBounds.TryGetValue(id,out b)||!bodyBoundsAt.TryGetValue(id,out at)||unchecked(now-at)>500){
                try{FVector o,e;actor.GetActorBounds(false,out o,out e,false);b=new[]{o.X,o.Y,o.Z,e.X,e.Y,e.Z};}catch{b=null;}
                bodyBounds[id]=b;bodyBoundsAt[id]=now;
            }
            if(b==null||!Finite(b[0]+b[1]+b[2]+b[3]+b[4]+b[5]))return;
            float ex=b[3],ey=b[4],ez=b[5];
            if(ex>2000||ey>2000||ez>2000)return;   // bounds swollen by attached effects: keep the capsule
            if(!boss){
                proxyRadius=Math.Min(250f,Math.Max(radius,.6f*Math.Min(ex,ey)));
                proxyHalf=Math.Min(600f,Math.Max(half,.9f*ez));
            }
            float dx=b[0]-location.X,dy=b[1]-location.Y;
            if(dx*dx+dy*dy>400f*400f)return;          // body centre implausibly far from the actor: keep the capsule
            feet=new FVector(b[0],b[1],Math.Min(location.Z-half,b[2]-ez));
        }
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
        private NativeAppleProtection appleProtection;
        internal McWorldLink(BGUPlayerCharacterCS player,McBridgeWire wire,Func<double,double,double,FVector> convert,Func<FVector,BridgePoint> toMc)
        {this.player=player;this.wire=wire;this.convert=convert;this.toMc=toMc;sceneBreak=new SceneBreak(player,convert);}
        private readonly SceneBreak sceneBreak;
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
            if(appleProtection!=null)appleProtection.Tick();
            JsonData ev;int handled=0;
            while(handled++<96&&wire.TryEvent(out ev)){
                try{
                    string type=(string)ev["t"];
                    if(type=="health_delta")ApplyHealth(ev);
                    else if(type=="block_reset")ClearBlocks();
                    else if(type=="block_cells")foreach(JsonData cell in ev["cells"])ApplyCell(cell);
                    else if(type=="target_block")Outline(ev);
                    // Preview107: MC pickaxe swings and explosions break the game's own destructible scenery.
                    else if(type=="mine"){var ignore=new List<AActor>{player,camera};foreach(var a in targets.Values)ignore.Add(a);foreach(var owned in blocks.Values)ignore.AddRange(owned);
                        sceneBreak.Mine((string)ev["tool"],ev.Keys.Contains("eff")?(int)N(ev["eff"]):0,camera,ignore);}
                    else if(type=="swing"){var ignore=new List<AActor>{player,camera};foreach(var a in targets.Values)ignore.Add(a);foreach(var owned in blocks.Values)ignore.AddRange(owned);
                        sceneBreak.Hit((string)ev["tool"],camera,ignore);}
                    else if(type=="pearl")Pearl(ev,camera);
                    else if(type=="proxy_fx")ProxyEffects(ev);
                    else if(type=="player_fx"){
                        McPlayerHealthGate.FireResistant=(bool)ev["fireRes"];
                        if(appleProtection==null)appleProtection=new NativeAppleProtection(GameApi.Events(player),McPlayerHealthGate.Clock);
                        appleProtection.Receive(ev.Keys.Contains("appleGuardTicks")?(int)N(ev["appleGuardTicks"]):0);
                    }
                    else if(type=="player_cure"){string kind=(string)ev["kind"];McPlayerHealthGate.CureUntil=McPlayerHealthGate.Clock()+(kind=="milk"?30:15);Log.Write("items",kind+" drunk: native status damage blocked for "+(kind=="milk"?30:15)+" s");}
                    else if(type=="explosion"){var p=ev["pos"];sceneBreak.Blast(N(p[0]),N(p[1]),N(p[2]),N(ev["r"]),(string)ev["src"]);}
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
                if(actor==player||!Alive(actor)||NativeObjectLifetime.ActorWorld(actor)!=NativeObjectLifetime.ActorWorld(player))continue;
                if(!BGUFunctionLibraryCS.BGUIsEnemyTeam(player,actor)){
                    // Preview107 diagnostic: a nearby live unit that is not (yet) hostile gets no MC hitbox; name it once.
                    if(skipLogged.Count<20&&(actor.GetActorLocation()-at).Size()<3000&&skipLogged.Add(actor.GetUniqueID()))
                        Log.Write("world-damage","no MC hitbox (not enemy team): class="+actor.GetClass().GetName()+" id="+actor.GetUniqueID());
                    continue;
                }
                // The dragon's root may be far away while its head/tail is right beside the player.
                if((actor.GetActorLocation()-at).Size()>(BodySegments.KnownDragon(actor.GetClass().GetName())||BGUFunctionLibraryCS.BGUGetFloatAttr(actor,EBGUAttrFloat.HpMax)>=EffectBossHp?14000:6400))continue;
                targets[actor.GetUniqueID().ToString(CultureInfo.InvariantCulture)]=actor;
                // Apply the wire budget after prioritising the dragon, not during unordered object discovery.
            }
        }
        private void SendEntities()
        {
            var json=new StringBuilder("{\"t\":\"host_entities\",\"session\":\"");json.Append(session).Append("\",\"ack\":").Append(ack)
                .Append(",\"hp\":").Append(F(BGUFunctionLibraryCS.BGUGetFloatAttr(player,EBGUAttrFloat.Hp)/healthUnit)).Append(",\"entities\":[");
            bool comma=false;int sent=0;
            var ordered=new List<KeyValuePair<string,BGUCharacterCS>>(targets);
            ordered.Sort((a,b)=>BodySegments.KnownDragon(b.Value.GetClass().GetName()).CompareTo(BodySegments.KnownDragon(a.Value.GetClass().GetName())));
            foreach(var pair in ordered){
                if(sent>=120)break; // Hard cap also applies to primary proxies, not only extra parts.
                var actor=pair.Value;if(!Alive(actor))continue;
                float hp=BGUFunctionLibraryCS.BGUGetFloatAttr(actor,EBGUAttrFloat.Hp),max=BGUFunctionLibraryCS.BGUGetFloatAttr(actor,EBGUAttrFloat.HpMax);
                if(!Finite(hp)||!Finite(max)||max<=0)continue;
                var capsule=actor.CapsuleComponent;if(capsule==null||capsule.IsDestroyed)continue;
                float half=capsule.GetScaledCapsuleHalfHeight(),radius=capsule.GetScaledCapsuleRadius();
                float proxyRadius=radius,proxyHalf=half;
                // Live110 (user: a crouching bone demon on a ledge could not be hit, not even by arrows; 0 MC hits logged): every
                // enemy's hitbox now follows its visible body (actor bounds), not only Bosses'. Capsules of crouching or leaning
                // creatures sit far from the body.
                FVector feetAt;BodyProxy(pair.Key,actor,radius,half,max>=EffectBossHp,out proxyRadius,out proxyHalf,out feetAt);
                if(sizeLogged.Add(pair.Key)){
                    // Preview67 diagnostic for Boss hitboxes: capsule vs whole-actor bounds.
                    try{FVector origin,extent;actor.GetActorBounds(false,out origin,out extent,false);
                        Log.Write("target-size","id="+pair.Key+" class="+actor.GetClass().GetName()+" hpMax="+F(max)+" capsuleR="+F(radius)+" capsuleHalf="+F(half)+" boundsExtent="+F(extent.X)+","+F(extent.Y)+","+F(extent.Z));
                    }catch(Exception e){Log.Write("target-size","id="+pair.Key+" bounds error "+e.GetType().Name);}
                }
                var pos=ToMc(feetAt);
                if(comma)json.Append(',');comma=true;sent++;
                float unit=Math.Max(healthUnit,max/1024f);targetUnits[pair.Key]=unit;
                json.Append("{\"id\":\"").Append(pair.Key).Append("\",\"hp\":").Append(F(hp/unit))
                    .Append(",\"max\":").Append(F(max/unit)).Append(",\"w\":").Append(F(proxyRadius*.02))
                    .Append(",\"h\":").Append(F(proxyHalf*.02)).Append(KindField(actor)).Append(BottomBar(actor,max)?",\"big\":true":"").Append(",\"p\":[").Append(F(pos.X)).Append(',').Append(F(pos.Y)).Append(',').Append(F(pos.Z)).Append("]}");
                // Preview120: long bodies (Kang-Jin Loong) get extra hitboxes along their skeleton; hits map back via BodySegments.BaseId.
                float[] bb;if(bodyBounds.TryGetValue(pair.Key,out bb)&&bb!=null){
                    var parts=BodySegments.Points(pair.Key,actor,bb[3],bb[4],bb[5],max>=EffectBossHp,radius,half);
                    for(int k=0;k<parts.Count&&sent<120;k++){sent++;   // MC accepts at most 128 proxies
                        var pp=parts[k];if(!Finite(pp.X+pp.Y+pp.Z))continue;
                        var feetPart=ToMc(pp-new FVector(0,0,BodySegments.Half));
                        json.Append(",{\"id\":\"").Append(pair.Key).Append('#').Append(k).Append("\",\"hp\":").Append(F(hp/unit))
                            .Append(",\"max\":").Append(F(max/unit)).Append(",\"w\":").Append(F(BodySegments.Radius*.02))
                            .Append(",\"h\":").Append(F(BodySegments.Half*.02)).Append(KindField(actor)).Append(BottomBar(actor,max)?",\"big\":true":"")
                            .Append(",\"p\":[").Append(F(feetPart.X)).Append(',').Append(F(feetPart.Y)).Append(',').Append(F(feetPart.Z)).Append("]}");
                    }
                }
            }
            wire.Send(json.Append("]}").ToString());
        }
        private readonly HashSet<string> rejectLogged=new HashSet<string>();
        private readonly HashSet<string> kindLogged=new HashSet<string>();
        private int staleLogged;
        private string KindField(AActor actor)
        {
            string cls=actor.GetClass().GetName();string kind=EnemyEffects.Kind(cls);
            if(kindLogged.Add(cls))Log.Write("enchant-kind",cls+" -> "+(kind.Length==0?"none (Smite/Bane no bonus)":kind));
            return kind.Length==0?"":",\"k\":\""+kind+"\"";
        }
        // Potions & items D1: MC effects on a proxy -> native time dilation / weakness scaling / launch (EnemyEffects).
        private static readonly float EffectBossHp=3000f;
        // Live112 polish (user): enemies with a bottom health bar (chiefs, Bosses) can't be captured in boats/minecarts or reeled in.
        // Preview125 (user: a big monk without a bottom health bar could not be caught; it has 1188 HP and the old rule was
        // "1000 HP or more"): the game's own unit data decides. BossBar and EliteBar are the named bars at the bottom of the
        // screen (Bosses, chiefs) -> no capture, no reeling in; EnemyBar (over the head) and None -> capturable. HP is never
        // used (user: later ordinary mobs have more HP than the first Boss); an unreadable entry counts as capturable and is
        // logged. Each class is logged once ("capture-kind").
        private readonly Dictionary<int,bool> bottomBar=new Dictionary<int,bool>();
        private readonly HashSet<string> barLogged=new HashSet<string>();
        private bool BottomBar(BGUCharacterCS actor,float max)
        {
            int id=0;try{id=actor.GetFinalBattleInfoExtendID();}catch{}
            bool big;if(id>0&&bottomBar.TryGetValue(id,out big))return big;
            string cls=actor.GetClass().GetName();
            try{
                var desc=id>0?BGW_GameDB.GetUnitBattleInfoExtendDesc(id):null;
                if(desc!=null){
                    string bar=desc.BloodBarType.ToString();big=bar=="BossBar"||bar=="EliteBar";bottomBar[id]=big;
                    if(barLogged.Add(cls))Log.Write("capture-kind",cls+" battleInfo="+id+" bar="+bar+" -> "+(big?"bottom health bar: no capture":"capturable"));
                    return big;
                }
            }catch(Exception e){if(barLogged.Add(cls+"!"))Log.Write("capture-kind",cls+" bar lookup failed ("+e.GetType().Name+"); treated as capturable");}
            if(barLogged.Add(cls+"?"))Log.Write("capture-kind",cls+" no battle info (id="+id+"); treated as capturable");
            return false;
        }
        private int fxLogged;
        private readonly Dictionary<string,FVector> captureOffsets=new Dictionary<string,FVector>();
        private void FollowCapture(string id,BGUCharacterCS actor,JsonData e)
        {
            if(!e.Keys.Contains("seat_p")||!NativeObjectLifetime.Live(actor))return;
            var p=e["seat_p"];if(p.Count!=3)return;
            double x=N(p[0]),y=N(p[1]),z=N(p[2]);if(!Finite(x+y+z))return;
            var seat=convert(x,y,z);FVector offset;
            if(!captureOffsets.TryGetValue(id,out offset)){
                var capsule=actor.CapsuleComponent;if(capsule==null||capsule.IsDestroyed)return;
                float r,h;FVector feet;
                BodyProxy(id,actor,capsule.GetScaledCapsuleRadius(),capsule.GetScaledCapsuleHalfHeight(),false,out r,out h,out feet);
                offset=actor.GetActorLocation()-feet;
            }
            var target=seat+offset;
            // Preview124: capture reaches 4 blocks past the hitbox (up to ~7 m from the actor), so the seat may be up to 12 m away.
            if(!Finite(target.X+target.Y+target.Z)||(target-actor.GetActorLocation()).Size()>1200f)return;
            captureOffsets[id]=offset;
            actor.SetActorLocation(target,false,out _,true);
            // Bounds contain world coordinates. Invalidate after moving so the next hitbox snapshot follows the actor.
            bodyBounds.Remove(id);bodyBoundsAt.Remove(id);
        }
        private void ProxyEffects(JsonData ev)
        {
            foreach(JsonData e in ev["e"]){
                string id=BodySegments.BaseId((string)e["id"]);BGUCharacterCS actor;
                if(!targets.TryGetValue(id,out actor)||!Alive(actor)){McPlayerHealthGate.WeakScale.Remove(id);continue;}
                bool boss=BottomBar(actor,0); // Preview125: Boss caps follow the native health-bar type, not HP
                int slow=(int)N(e["slow"]),weak=(int)N(e["weak"]),lev=(int)N(e["lev"]);
                bool freeze=(bool)e["freeze"],web=(bool)e["web"],soul=(bool)e["soul"];
                bool seat=e.Keys.Contains("seat")&&(bool)e["seat"]&&!BottomBar(actor,BGUFunctionLibraryCS.BGUGetFloatAttr(actor,EBGUAttrFloat.HpMax));
                float dilation=EnemyEffects.Dilation(slow,freeze,web,soul,boss);
                if(seat){dilation=Math.Min(dilation,.0001f);FollowCapture(id,actor,e);}
                else captureOffsets.Remove(id);
                EnemyEffects.SetDilation(actor,dilation);
                float scale=EnemyEffects.WeaknessScale(weak,boss);
                if(scale<.999f)McPlayerHealthGate.WeakScale[id]=scale;else McPlayerHealthGate.WeakScale.Remove(id);
                bool hasKb=false;foreach(string key in e.Keys)if(key=="kb")hasKb=true;
                if(!boss&&!seat&&(hasKb||lev>=0)){
                    float x=0,y=0,z=0;
                    if(hasKb){var k=e["kb"];EnemyEffects.LaunchVelocity(N(k[0]),N(k[1]),N(k[2]),out x,out y,out z);}
                    if(lev>=0)z=Math.Max(z,250f+100f*lev);
                    try{actor.LaunchCharacter(new FVector(x,y,z),hasKb,lev>=0);}catch(Exception ex){if(fxLogged<40){fxLogged++;Log.Write("items","launch failed: "+ex.Message);}}
                }
                if(fxLogged<40&&(dilation<.999f||scale<.999f||hasKb||lev>=0||seat)){fxLogged++;
                    Log.Write("items","enemy "+actor.GetClass().GetName()+" id="+id+(boss?" (boss caps)":"")+": time x"+F(dilation)+" weakness x"+F(scale)+(hasKb?" knockback":"")+(lev>=0?" levitation":"")+(seat?" captured":""));}
            }
        }
        // Preview107 (user: ender pearls don't teleport): MC only has Wukong geometry rastered near Steve, so a pearl flew
        // through the real terrain. MC sends each tick's pearl segment; the host traces it against the native world and
        // replies with the hit point (backed off 0.6 m), where MC teleports the thrower.
        // Live108: the host's pose safety check rejected the pearl jump (speed limit) and pulled Steve back; the confirmed
        // landing point is accepted once within 3 s (McBridgeHost.SafePose).
        internal static BridgePoint PearlTarget;internal static double PearlAt=-100;
        private void Pearl(JsonData ev,AActor camera)
        {
            var a=ev["a"];var b=ev["b"];
            var from=convert(N(a[0]),N(a[1]),N(a[2]));var to=convert(N(b[0]),N(b[1]),N(b[2]));
            var ignore=new List<AActor>{player};if(NativeObjectLifetime.Live(camera))ignore.Add(camera);foreach(var t in targets.Values)ignore.Add(t);
            FHitResult hit;
            if(!USystemLibrary.LineTraceSingle(player,from,to,ETraceTypeQuery.TraceTypeQuery1,true,ignore,EDrawDebugTrace.None,out hit,true,default(FLinearColor),default(FLinearColor),0)||hit.StartPenetrating)return;
            var point=new FVector(hit.ImpactPoint.X,hit.ImpactPoint.Y,hit.ImpactPoint.Z);
            var back=from-to;float len=back.Size();if(len>1)point=point+back*(60f/len);
            var mc=toMc(point);if(!mc.Finite)return;
            PearlTarget=mc;PearlAt=McPlayerHealthGate.Clock();
            wire.Send("{\"t\":\"pearl_hit\",\"id\":"+(int)N(ev["id"])+",\"p\":["+F(mc.X)+","+F(mc.Y)+","+F(mc.Z)+"]}");
            Log.Write("pearl","ender pearl hit native "+(hit.Component.Get()==null?"world":hit.Component.Get().GetOwner()==null?"world":hit.Component.Get().GetOwner().GetClass().GetName())+" -> teleport to "+F(mc.X)+","+F(mc.Y)+","+F(mc.Z));
        }
        private readonly HashSet<uint> skipLogged=new HashSet<uint>();
        private void ApplyHealth(JsonData message)
        {
            if((string)message["session"]!=session)return;
            long seq=(long)N(message["seq"]);if(seq<=ack){if(staleLogged++<20)Log.Write("world-health","stale MC hit dropped: seq="+seq+" ack="+ack+" id="+(string)message["id"]);return;}
            // FIFO on one WebSocket; a sequence gap forces resync rather than silent damage loss.
            if(seq!=ack+1)throw new InvalidOperationException("MC damage sequence gap "+ack+"->"+seq);
            string id=BodySegments.BaseId((string)message["id"]);double amount=N(message["amount"]);
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
                        HPMaxINV10000Damage_Abs=(float)(hostDamage/max*10000),DamageImmueLevel=2,CameraShakeDA=""};
                    deathRequest=request;
                    healthGate.Applying=true;
                    try{NativeEventInvoker.Invoke(NativeEventInvoker.Signal(GameApi.Events(victim),"Evt_TriggerNormalDamageEffect"),source,config,request,new FBattleAttrSnapShot(source,false));}
                    finally{healthGate.Applying=false;}
                    // Preview107 (user: some enemies, especially after skipped areas, take no damage): native
                    // IsDamageValid rejects level-0 damage on CommonDamageImmue (116) units; MC hits now carry level 1 like
                    // a normal Wukong attack. Live107: rooftop crossbowmen (ShuNuShou Stand) still refused level 1, so level 2
                    // (passes StrongDamageImmue 117 too). Full ImmueDamage (1: scripted invulnerability) is still respected. Any enemy
                    // that still refuses a hit is named once, so a remaining case can be identified.
                    if(enemy&&!finishing&&Alive(victim)&&BGUFunctionLibraryCS.BGUGetFloatAttr(victim,EBGUAttrFloat.Hp)>=before-.0001f&&rejectLogged.Add(id))
                        Log.Write("world-damage","enemy refused MC hit: id="+id+" class="+victim.GetClass().GetName()+" hp="+F(before)+"/"+F(max)+" damage="+F(hostDamage)+" (native immune state)");
                    }
                    // Preview58: the absolute-damage path carries no stiff; raise the native normal stiff.
                    // Preview126: the poise meter (StaggerPolicy) decides whether this attack staggers at all, by hit count and time.
                    string hitReason=(string)message["reason"];
                    if(enemy&&!finishing&&Alive(victim)&&!NativeHitStun.NoReaction(hitReason)){
                        try{
                            bool heavy=message.Keys.Contains("hk")&&(string)message["hk"]=="heavy";
                            double now=UGameplayStatics.GetRealTimeSeconds(player);
                            var reaction=stagger.Hit(id,now,heavy,BottomBar(victim,max));
                            if(reaction!=StaggerPolicy.Reaction.None)NativeHitStun.Try(id,victim,player,request,default(FSkillDamageConfig),amount,hitReason,reaction==StaggerPolicy.Reaction.Heavy);
                            else heldReactions++;
                            if(now>=nextPoiseLog){nextPoiseLog=now+10;Log.Write("world-poise","staggers="+stagger.Staggers+" heavy="+stagger.HeavyReactions+" held="+heldReactions+" merged="+stagger.Merged+" (poise by hit count and time, not damage)");}
                        }
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
                bool solid=PawnSolid(center,ignore,30)&&!GroundOnly(x,y,z,ignore);
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
        // Preview102 (user: slopes very sticky, often stuck): on a slope the walkable ground itself rises into the
        // probed upper part of a cell, so the whole 1 m cell became a wall (and its full barrier replaced the exact-height
        // ground surface). A cell whose blocking geometry is only walkable ground (normal within ~45 deg of up) is left
        // to the surface proxy; only geometry clearly above that ground still counts as a wall.
        private int slopeCellsKept;private double nextSlopeLog;
        private bool GroundOnly(int x,int y,int z,List<AActor> ignore)
        {
            // The ground may be in this cell or up to 0.6 m below it (the cell above a steep slope's surface cell).
            var start=convert(x+.5,y+1.0,z+.5);var end=convert(x+.5,y-.6,z+.5);var bottom=convert(x+.5,y,z+.5);FHitResult hit;
            if(!USystemLibrary.LineTraceSingle(player,start,end,ETraceTypeQuery.TraceTypeQuery1,true,ignore,EDrawDebugTrace.None,out hit,true,default(FLinearColor),default(FLinearColor),0))return false;
            if(hit.StartPenetrating||hit.ImpactNormal.Z<.7f)return false;   // inside rock, or a steep face: a real wall
            double floor=(hit.ImpactPoint.Z-bottom.Z)/100;                    // ground height relative to the cell, -0.6..1
            // A <=45 deg slope rises at most 0.2 m across the probe's 0.2 m half-width; anything higher is not this ground.
            double low=Math.Max(0,floor+.25),high=.98;
            bool ground=high-low<.15||!PawnSolid(convert(x+.5,y+(low+high)/2,z+.5),ignore,(float)((high-low)*50));
            if(ground){slopeCellsKept++;double now=UGameplayStatics.GetRealTimeSeconds(player);
                if(now>=nextSlopeLog){nextSlopeLog=now+30;Log.Write("world-terrain","slope cells left to the ground surface (not walls) total="+slopeCellsKept);}}
            return ground;
        }
        private bool PawnSolid(FVector center,List<AActor> ignore,float halfHeight)
        {
            List<FHitResult> hits;
            if(!USystemLibrary.BoxTraceMultiForObjects(player,center-new FVector(0,0,.1f),center+new FVector(0,0,.1f),new FVector(20,20,halfHeight),default(FRotator),WallObjects,false,ignore,EDrawDebugTrace.None,out hits,true,default(FLinearColor),default(FLinearColor),0)||hits==null)return false;
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
        // Preview126 (user: blocks too hard to break; tier them, a Boss breaks them at once): every hit goes to MC, which knows
        // the block (BlockToughness: soft 1 hit, stone 2, obsidian-class 3; a bottom-health-bar Boss hit counts double).
        private readonly Dictionary<string,double> enemyNextHit=new Dictionary<string,double>();
        private readonly StaggerPolicy stagger=new StaggerPolicy();private int heldReactions;private double nextPoiseLog;
        private double nextBlockScan;
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
                bool boss=BottomBar(enemy,0);
                wire.Send("{\"t\":\"enemy_block_hit\",\"p\":["+best+"],\"boss\":"+(boss?"true":"false")+"}");
                Log.Write("enemy-break","enemy="+pair.Key+" hit block "+best+(boss?" (Boss)":""));
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
        public void Dispose(){if(appleProtection!=null)appleProtection.Dispose();appleProtection=null;McPlayerHealthGate.FireResistant=false;McPlayerHealthGate.CureUntil=-100;EnemyEffects.RestoreAll();BodySegments.Forget();captureOffsets.Clear();if(healthGate!=null)healthGate.Dispose();healthGate=null;ClearBlocks();targets.Clear();targetUnits.Clear();terrain.Clear();started=false;}
    }
}
