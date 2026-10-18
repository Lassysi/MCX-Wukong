using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Text;
using b1;
using BtlShare;
using UnrealEngine.Engine;
using UnrealEngine.InputCore;
using UnrealEngine.Runtime;
using UnrealEngine.UMG;

namespace DqjCombat
{
    // MC owns locomotion, eye pose and input; native world outcomes cross via McWorldLink.
    internal sealed class McBridgeHost : IDisposable
    {
        private readonly BGUPlayerCharacterCS player;
        private readonly APlayerController controller;
        private readonly McBridgeWire wire;
        private readonly McInputRouter router;
        private McWorldLink worldLink;
        private readonly NativeMapUi nativeMap=new NativeMapUi();
        private NativeShrineSession nativeShrine;
        private NativeTravelKey travelKeyMessages;
        private readonly Queue<int> wheelDeltas=new Queue<int>();
        private readonly NativeInputBuffer inputMessages=new NativeInputBuffer();
        private int pointerClientWidth,pointerClientHeight;
        private readonly Dictionary<string,FKey> keys=new Dictionary<string,FKey>();
        private readonly Dictionary<string,bool> down=new Dictionary<string,bool>();
        private readonly List<USkeletalMeshComponent> meshes=new List<USkeletalMeshComponent>();
        private readonly List<bool> paused=new List<bool>();
        private readonly List<bool> hidden=new List<bool>();
        private readonly List<UPrimitiveComponent> nativeVisible=new List<UPrimitiveComponent>();
        private readonly List<bool> nativeWasHidden=new List<bool>();
        private AActor cameraActor;
        private UCameraComponent camera;
        private double nextProjectionLog;
        private readonly CameraManagerDiagnostics cameraManagerDiagnostics=new CameraManagerDiagnostics();
        private readonly PcmFovLock fovLock=new PcmFovLock();
        private readonly CameraFrameOrder frameOrder=new CameraFrameOrder();
        // Preview42: MC frame applied at begin-frame, published at host mapping offset 28.
        private uint matchedMcFrame;
        private bool menuRequested;
        private MemoryMappedFile mapping;
        private MemoryMappedViewAccessor memory;
        private readonly McPoseDiagnostics poseDiagnostics=new McPoseDiagnostics();
        private readonly NativeMovementLease movementLease=new NativeMovementLease();
        private float halfHeight;
        private static McWorldCatalog worldCatalog;
        private McWorldCoordinates coordinates;
        private BridgePoint unrealOrigin,spawnPoint;
        private bool owning, initialized,inputPaused;
        private double nextGround,nextLog,nextComponents,nextNativeRay,nextNativeRayLog;
        private long lastNativeRayFrame;
        private McGroundPreparation groundPreparation;
        private bool prepareBlocked;
        private NativeShrineAdapter shrineAdapter;
        private ShrineEconomy economy;
        private bool lastNearShrine;
        private double nextSkinRetry;
        private int skinRetries;
        private FVector blockedAt;
        private long sequence;
        private bool travelPending,travelSawLoading;
        // LIFE-01 (Preview61): MC player death keeps MC authority so the MC death screen shows;
        // Respawn there raises Wukong's own rebirth at the current shrine (rebirth point).
        private bool mcDead,rebirthPending,teleportStuck,moviePlaying,playerSyncing,nativeInteracting;private double nextMovieSkip;
        private FVector rebirthFrom;private double rebirthMovedAt,rebirthSettleAt;
        private double travelStarted,lastUpdateTime;
        private int slot;
        private bool cursorOwned,originalCursor;
        private string groundEpoch;
        private bool groundCommitSent,groundFault;
        private McBridgeWire.Frame recoveryPose;
        private double nextTerrainRequest;
        private McBridgeWire.Frame lastAccepted;
        private McBridgeWire.Frame motionSample;
        private double nextMotionLog;
        private double nextGroundWait;
        private bool travelRequested;
        private readonly NativeUiCursorLease nativeUiCursor=new NativeUiCursorLease();
        private bool? mcHudHidden;
        private double nextNativeUiCheck;
        internal void RequestTravel(){travelRequested=true;}
        internal bool ScreenOwnsCursor {get{return cursorOwned;}}
        internal bool NativeUiActive {get{return nativeMap.Active||(nativeShrine!=null&&nativeShrine.Active);}}
        private static readonly string[] Numbers={"One","Two","Three","Four","Five","Six","Seven","Eight","Nine"};
        private static readonly string[] Physical={"W","S","A","D","SpaceBar","LeftShift","LeftControl","LeftMouseButton","RightMouseButton","E","Q","F","Escape","F5","MiddleMouseButton","T","Slash"};
        private static readonly string[] Semantic={"forward","back","left","right","jump","sneak","sprint","attack","use","inventory","drop","swap","escape","perspective","pick","chat","command"};
        internal AActor PlayerSkinOwner {get{return ordinaryPlayer?player:null;}}
        internal bool Active {get{return owning&&!beginFramePaused;}}
        private NativeHitProbe hitProbe;
        private NativeGrabSkip grabSkip;
        private SteveSkin steveSkin;
        private readonly bool ordinaryPlayer;
        private readonly UWorld nativeWorld;
        private bool beginFramePaused;
        internal bool SessionLive {get{return NativeObjectLifetime.Live(player)&&NativeObjectLifetime.Live(controller)
            &&NativeObjectLifetime.Live(nativeWorld)&&GameApi.World()==nativeWorld&&NativeObjectLifetime.ActorWorld(player)==nativeWorld
            &&GameApi.Pawn(controller)==player;}}
        private readonly NativeInteractUnlock interactUnlock=new NativeInteractUnlock();
        internal McBridgeHost(BGUPlayerCharacterCS player,APlayerController controller)
        {
            this.player=player;this.controller=controller;
            nativeWorld=NativeObjectLifetime.ActorWorld(player);
            if(nativeWorld==null)throw new InvalidOperationException("native player world is not ready");
            bool humanoid=false;
            try{var mesh=player.Mesh;if(NativeObjectLifetime.Live(mesh)){var bones=new System.Collections.Generic.List<string>();
                for(int i=0;i<mesh.GetNumBones();i++)bones.Add(mesh.GetBoneName(i).ToString());HumanoidSkinRig rig;humanoid=HumanoidSkinRig.TryResolve(bones,out rig);}}catch{}
            ordinaryPlayer=PlayerFormPolicy.CanOwnMovement(player.GetClass().GetName(),humanoid);
            Log.Write("native-form","pawn "+player.GetClass().GetName()+" humanoidRig="+humanoid+" -> "+(ordinaryPlayer?"MC locomotion, combat and skin":"native control"));
            router=new McInputRouter(player,GameApi.Events(player));router.TakeoverBridge=true;
            try{
                mapping=MemoryMappedFile.CreateOrOpen("Local\\DqjWukongBridge",96);
                memory=mapping.CreateViewAccessor(0,96);
                if(!router.Install())throw new InvalidOperationException(router.LastError);
                wire=new McBridgeWire();
                var shrine=new NativeShrineAdapter(player);shrineAdapter=shrine;
                economy=new ShrineEconomy(json=>{if(wire!=null)wire.Send(json);});
                DropCatalog.ExportOnce(); // Preview105: one-time read-only drop/item export for the MC drop replacement list
                nativeShrine=new NativeShrineSession(shrine.Read,shrine.LeaveActivatedShrineMenu,()=>nativeMap.Open(lastUpdateTime,SuspendForNativeUi));
                router.BridgeNativeInteraction=NativeInteraction;
                travelKeyMessages=new NativeTravelKey(RequestTravel,QueueWheel,null,QueueControl,QueuePointer);
                router.BridgeHasScreen=()=>cursorOwned||(wire.Latest!=null&&!string.IsNullOrEmpty(wire.Latest.Screen));
                if(Log.Verbose)hitProbe=new NativeHitProbe(player);   // Preview79: probe only when debugging
                // Preview75 (user): grabs/throws play as-is with Steve (grab view mode); no grab skip.
                // Preview73: Steve on the Wukong skeleton. Created before the first
                // CaptureNativeComponents so the hidden Wukong mesh is remembered as hidden.
                try{if(ordinaryPlayer)steveSkin=new SteveSkin(player,System.IO.Path.Combine(Log.Dir,"steve.mesh"));
                    else Log.Write("native-form","native control retained for "+player.GetClass().GetName()+"; MC locomotion and humanoid skin are suspended");}
                catch(Exception e){steveSkin=null;Log.Write("steve-skin-error","not attached: "+e.GetType().Name+": "+e.Message);}
            }catch{
                router.Dispose();if(memory!=null)memory.Dispose();if(mapping!=null)mapping.Dispose();throw;
            }
            Log.Write("bridge","MC physics/input/world adapter created; F6 uses native map button; native UI owns map, cursor and travel; terrain requires server acknowledgement");
        }
        internal void Update(double now,bool gameplay)
        {
            if(!SessionLive){Invalidate();return;}
            beginFramePaused=false;
            lastUpdateTime=now;
            bool canSkip,movie=GameApi.MoviePlaying(player,out canSkip);
            // Transformation pawns are not ordinary MC players. CG stand-ins still update independently.
            gameplay=gameplay&&ordinaryPlayer;
            if(movie||!ordinaryPlayer)router.GameplayActive=false;
            bool interacting=GameApi.PlayerInteracting(player);
            if(steveSkin!=null)steveSkin.SetCinematic(PlayerFormPolicy.Enlarge(movie,interacting||(nativeShrine!=null&&nativeShrine.Active)));
            // Preview99 (Live97: one failed attach left the whole session, chests and gathering included, on
            // the Wukong model): a missing or dead player skin is rebuilt, at most 5 times, 10 s apart.
            if(ordinaryPlayer&&(steveSkin==null||!steveSkin.Live||steveSkin.Mesh!=player.Mesh)&&now>=nextSkinRetry&&skinRetries<5&&!player.IsDestroyed&&!player.IsPendingKill){
                nextSkinRetry=now+10;skinRetries++;
                if(steveSkin!=null){steveSkin.Dispose();steveSkin=null;}
                try{steveSkin=new SteveSkin(player,System.IO.Path.Combine(Log.Dir,"steve.mesh"));skinRetries=0;Log.Write("steve-skin","player skin rebuilt");}
                catch(Exception e){steveSkin=null;Log.Write("steve-skin-error","rebuild "+skinRetries+"/5 failed: "+e.GetType().Name+": "+e.Message);}
            }
            if(steveSkin!=null){steveSkin.SetNativeView(!owning||NativeUiActive);steveSkin.Update();}
            InteractionHint(now);
            if(economy!=null)economy.Update(now,controller);
            var origin=UGameplayStatics.GetWorldOriginLocation(NativeObjectLifetime.ActorWorld(player));
            unrealOrigin=new BridgePoint(origin.X,origin.Y,origin.Z);
            LitJson.JsonData travelEvent;
            while(wire.TryTravelEvent(out travelEvent))Log.Write("travel","legacy MC travel message discarded; native map is authoritative");
            try{
                if(!nativeMap.Active&&now>=nextNativeUiCheck){nextNativeUiCheck=now+.1;nativeMap.Adopt(now,SuspendForNativeUi);}
                if(NativeUiActive){
                    wire.Heartbeat("{\"t\":\"cam\",\"authority\":\"host\"}");Publish(null,false);
                    ResetInput();router.GameplayActive=false;travelRequested=false;
                    bool nativeReady=GameApi.InGameplayWithBridgeCursor(player,controller,NativeObjectLifetime.ActorWorld(player));
                    bool mapActive=nativeMap.Active&&nativeMap.Update(now,nativeReady);
                    bool shrineActive=nativeShrine!=null&&nativeShrine.Active&&nativeShrine.Update(now,nativeReady&&!mapActive);
                    if(nativeShrine!=null&&nativeShrine.Rested){nativeShrine.Rested=false;AfterShrineRest();}
                    if(mapActive)controller.ShowMouseCursor=true;
                    if(mapActive||shrineActive||NativeUiActive)return;
                    RestoreNativeUiCursor();return;
                }
            }catch(Exception e){Log.Write("native-map-error",e.ToString());nativeMap.Clear();if(nativeShrine!=null)nativeShrine.Clear();RestoreNativeUiCursor();nextNativeUiCheck=now+5;}
            // Live65: map travel closed the map, MC reattached at once and kept placing Wukong at
            // Steve's position, cancelling the native teleport. Any native teleport now suspends
            // MC until the teleport FSM finishes; MC then reattaches at the destination.
            bool teleporting=GameApi.NativeTeleporting(player);
            // Preview67 (user: Steve gains nothing from cutscenes): skippable cutscenes are skipped
            // with the game's own skip; any cutscene suspends MC like a teleport until it ends.
            // Items 1/4 (user 2026-10-16): every cutscene is skipped, unskippable ones too, except
            // for 180 s after a Boss-class kill so chapter-end cutscenes and chapter UI still play.
            // Preview92 (user: skipping cutscenes lost story items and soft-locked a door; keep Wukong's story with the
            // Steve model): cutscenes play natively unless the switch file dqj-skip-cutscenes.on exists.
            bool storyAllowed=!SkipCutscenes()||(DateTime.UtcNow-McWorldLink.LastBossDefeatUtc).TotalSeconds<180;
            if(movie){
                if(!moviePlaying){moviePlaying=true;Log.Write("cutscene","native cutscene started; canSkip="+canSkip+" storyAllowed="+storyAllowed);}
                if(!storyAllowed&&now>=nextMovieSkip){nextMovieSkip=now+1;
                    try{if(!canSkip)GameApi.ForceSkippable(player);BGUFunctionLibraryCS.SkipCurrentSequence(player);Log.Write("cutscene","native skip requested; forced="+!canSkip);}
                    catch(Exception e){Log.Write("cutscene-error",e.GetType().Name+": "+e.Message);}}
                teleporting=true;
            }else if(moviePlaying){moviePlaying=false;Log.Write("cutscene","native cutscene ended");}
            if(gameplay)interactUnlock.Update(now,player);
            if(gameplay&&now>=nextOverlapProbe){nextOverlapProbe=now+.5;OverlapProbe();}
            if(interacting!=nativeInteracting){nativeInteracting=interacting;Log.Write("interaction","native interaction "+(interacting?"started; MC suspended while the native animation moves the character":"ended"));}
            if(interacting)teleporting=true;
            // F6 remains available before a successful ground handshake, including after a refused spawn.
            if(gameplay&&!teleporting&&HandleTravelRequest())return;
            bool syncing=GameApi.PlayerAnimationSyncing(player);
            if(syncing!=playerSyncing){playerSyncing=syncing;Log.Write("grab","player animation sync (enemy grab/execution) "+(syncing?"started":"ended")+"; MC owns="+owning);}
            // A teleport FSM that outlived the 45 s travel timeout is ignored until it ends,
            // so a stuck native state cannot keep MC suspended forever.
            if(!teleporting)teleportStuck=false;else if(NativeSuspensionPolicy.IgnoreStuckTeleport(teleportStuck,movie,interacting))teleporting=false;
            if(teleporting&&!travelPending){Log.Write("travel",movie?"native cutscene; MC suspended until it ends":interacting?"native interaction; MC suspended until it ends":"native teleport in progress; MC suspended until it finishes");SuspendForTravel(movie&&storyAllowed);}
            if(travelPending){
                if(!gameplay||teleporting)travelSawLoading=true;
                // Live63: the rebirth teleport to a nearby shrine has no loading screen, so arrival
                // is the player leaving the death spot (>3 m), then 1 s for the native fade-in.
                if(rebirthPending&&!travelSawLoading&&gameplay){
                    if(rebirthMovedAt==0&&(player.GetActorLocation()-rebirthFrom).Size()>300){rebirthMovedAt=now;Log.Write("life","native rebirth teleport arrived without loading");}
                    if((rebirthMovedAt>0&&now-rebirthMovedAt>1)||now-travelStarted>10){travelSawLoading=true;if(rebirthMovedAt==0)Log.Write("life","no rebirth movement after 10 s (shrine at death spot?); reattaching here");}
                }
                wire.Heartbeat("{\"t\":\"cam\",\"authority\":\"host\"}");Publish(null,false);
                // Live68: native rebirth casts the player's rebirth (get-up) skill after the teleport;
                // MC pauses Wukong animations, so the skill never ended and the Boss ignored the
                // player. Hold MC until the montage has finished (8 s cap).
                if(travelSawLoading&&gameplay&&!teleporting&&rebirthPending){
                    if(rebirthSettleAt==0){rebirthSettleAt=now;Log.Write("life","rebirth teleport finished; waiting for the native get-up animation");}
                    bool montage=false;try{var anim=player.Mesh.GetAnimInstance();montage=anim!=null&&anim.IsAnyMontagePlaying();}catch{}
                    if(now-rebirthSettleAt<.5||(montage&&now-rebirthSettleAt<8))return;
                    Log.Write("life","native get-up finished after "+F(now-rebirthSettleAt)+" s; montage="+montage);rebirthSettleAt=0;
                }
                if(travelSawLoading&&gameplay&&!teleporting){travelPending=false;Log.Write("travel","native loading finished; reattach MC authority at destination");
                    // Rebirth: full HP and no lingering MC effects before MC health follows host on reattach.
                    if(rebirthPending){rebirthPending=false;AfterShrineRest();}}
                else if(NativeSuspensionPolicy.CanExpire(now-travelStarted,movie,interacting)){travelPending=false;rebirthPending=false;teleportStuck=teleporting;Log.Write("travel-error","native loading did not finish within 45 seconds; MC authority may resume when gameplay is ready");}
                else return;
            }
            wire.Heartbeat("{\"t\":\"cam\",\"f\":"+GameApi.Frame()+",\"authority\":\""+(gameplay&&owning&&!groundFault?"minecraft":"host")+"\"}");
            var frame=wire.Latest;
            double age=frame==null?double.PositiveInfinity:(WindowsClock.Nanoseconds()-frame.Received)/1000000000.0;
            bool fresh=frame!=null&&age>=0&&age<.5;
            // The transport checks ping/pong independently of the render thread.
            // A delayed pose must not give the camera/input/body back to Wukong.
            if(!gameplay||!wire.Connected||frame==null||!router.Installed){
                if(owning)Log.Write("bridge-release","gameplay="+gameplay+" connected="+wire.Connected+" frame="+(frame!=null)+" route="+router.Installed+" age="+F(age));
                Release();Publish(null,false);return;
            }
            if(!owning&&!fresh){Publish(null,false);return;}
            if(groundFault){RecoverGround(now,frame,fresh);return;}
            nativeUiCursor.RememberGameplay(controller.ShowMouseCursor,cursorOwned);
            if(!initialized)
            {
                if(prepareBlocked){
                    if((player.GetActorLocation()-blockedAt).Size()<100){Publish(null,false);return;}
                    prepareBlocked=false;Log.Write("bridge-ground","native player moved away from failed prepare; retry allowed");
                }
                halfHeight=player.CapsuleComponent.GetScaledCapsuleHalfHeight();var nativeFoot=player.GetActorLocation()-new FVector(0,0,halfHeight);
                FHitResult initialFloor;
                if(!TraceFloor(nativeFoot,250,400,out initialFloor)){
                    if(now>=nextGroundWait){nextGroundWait=now+3;Log.Write("bridge-ground","no native support at spawn; takeover held at host position="+nativeFoot);}
                    Publish(null,false);return;
                }
                nativeFoot.Z=(float)initialFloor.ImpactPoint.Z;
                try{
                    if(worldCatalog==null)worldCatalog=new McWorldCatalog(System.IO.Path.Combine(Log.Dir,"world-anchors.json"),System.IO.Path.Combine(Log.Dir,"world-anchor-bootstrap.json"));
                    string level=UGameplayStatics.GetCurrentLevelName(NativeObjectLifetime.ActorWorld(player),true)+"|"+NativeObjectLifetime.ActorWorld(player).GetPathName();
                    coordinates=worldCatalog.Resolve(frame.WorldId,level,new BridgePoint(nativeFoot.X+unrealOrigin.X,nativeFoot.Y+unrealOrigin.Y,nativeFoot.Z+unrealOrigin.Z),unrealOrigin);
                    spawnPoint=ToMc(nativeFoot);
                    Log.Write("world-anchor","level="+level+" MCworld="+frame.WorldId+" region="+coordinates.Region+" absoluteHost="+F(coordinates.HostOrigin.X)+","+F(coordinates.HostOrigin.Y)+","+F(coordinates.HostOrigin.Z)+" spawn="+F(spawnPoint.X)+","+F(spawnPoint.Y)+","+F(spawnPoint.Z));
                }catch(Exception e){if(now>=nextGroundWait){nextGroundWait=now+3;Log.Write("world-anchor-error",e.ToString());}Publish(null,false);return;}
                worldLink=new McWorldLink(player,wire,ConvertPosition,ToMc);
                float yaw=controller.GetControlRotation().Yaw;
                groundEpoch=Guid.NewGuid().ToString("N");groundCommitSent=false;lastAccepted=null;
                groundPreparation=new McGroundPreparation(groundEpoch,spawnPoint,now);
                string support=GroundCells(spawnPoint.X,spawnPoint.Y,spawnPoint.Z);
                // Only AIR/BARRIER proxy cells are writable by this MC operation.
                // A stale wall raster must not obstruct the verified host spawn.
                int sx=(int)Math.Floor(spawnPoint.X),sy=(int)Math.Floor(spawnPoint.Y+.001),sz=(int)Math.Floor(spawnPoint.Z);
                wire.Send("{\"t\":\"voxels\",\"c\":["+sx+","+sy+","+sz+",0,"+sx+","+(sy+1)+","+sz+",0]}");
                wire.Send("{\"t\":\"bridge_prepare\",\"epoch\":\""+groundEpoch+"\",\"guard\":true,\"spawn\":["+F(spawnPoint.X)+","+F(spawnPoint.Y)+","+F(spawnPoint.Z)+"],\"yaw\":"+F(yaw)+",\"pitch\":"+F(-controller.GetControlRotation().Pitch)+",\"c\":["+support+"]}");
                initialized=true;router.GameplayActive=false; // native movement remains available until MC commits safe ground
                Log.Write("bridge-ground","prepare epoch="+groundEpoch+" nativeFloor="+nativeFoot+" surface="+support);
                if(!string.IsNullOrEmpty(frame.Screen))wire.Send("{\"t\":\"key\",\"k\":\"escape\",\"down\":true}");
                controller.GetViewportSize(out int w,out int h);
                if(w>0&&h>0){double scale=Math.Min(1,Math.Min(1280.0/w,720.0/h));wire.Send("{\"t\":\"view\",\"w\":"+Math.Max(320,(int)(w*scale))+",\"h\":"+Math.Max(240,(int)(h*scale))+"}");}
                return;
            }
            if(!owning){
                if(groundPreparation.Expired(now)||(frame.GroundEpoch==groundEpoch&&!string.IsNullOrEmpty(frame.GroundFailure))){AbortGroundPrepare(frame.GroundFailure);return;}
                if(!fresh||frame.Health<=0||!frame.HasGroundSpawn||!groundPreparation.Ready(frame.GroundEpoch,frame.GroundReady,frame.GroundSpawn,new BridgePoint(frame.X,frame.Y,frame.Z),frame.GroundAnchorResolved)){
                    if(now>=nextGroundWait){nextGroundWait=now+3;Log.Write("bridge-ground","waiting server support epoch="+frame.GroundEpoch+" ready="+frame.GroundReady+" foot="+F(frame.X)+","+F(frame.Y)+","+F(frame.Z)+" hp="+F(frame.Health));}
                    Publish(null,false);return;
                }
                if(spawnPoint.X!=groundPreparation.Resolved.X||spawnPoint.Z!=groundPreparation.Resolved.Z)
                    Log.Write("bridge-ground","MC spawn column blocked; player placed beside it at "+F(groundPreparation.Resolved.X)+","+F(groundPreparation.Resolved.Y)+","+F(groundPreparation.Resolved.Z)+"; world anchor retained");
                else if(spawnPoint.Y!=groundPreparation.Resolved.Y)Log.Write("bridge-ground","MC resolved standing height="+F(spawnPoint.Y)+"->"+F(groundPreparation.Resolved.Y)+"; fixed X/Z and world anchor retained");
                spawnPoint=groundPreparation.Resolved;
                if(!groundCommitSent){groundCommitSent=true;wire.Send("{\"t\":\"bridge_commit\",\"epoch\":\""+groundEpoch+"\"}");return;}
                if(!frame.GroundCommitted)return;
            }
            if(!owning){
                if(player.CharacterMovement.MovementMode==EMovementMode.MOVE_None){
                    if(now>=nextGroundWait){nextGroundWait=now+3;Log.Write("bridge-ground","native movement not yet ready after story; awaiting CMC state before taking ownership");}
                    Publish(null,false);return;
                }
                Enable();
            }
            if(!owning){Publish(null,false);return;}
            if(frame.Health<=0||mcDead){if(McDeath(now,frame,fresh))return;}
            if(playerSyncing||grabView){if(GrabView(now,frame))return;}
            if(!SafePose(frame)){
                if(frame.Health>0&&lastAccepted!=null){BeginGroundRecovery(frame);return;}
                groundFault=true;Log.Write("bridge-ground-error","unsafe/dead MC pose rejected; foot="+F(frame.X)+","+F(frame.Y)+","+F(frame.Z)+" hp="+F(frame.Health)+"; host retained at last accepted position");
                Release();wire.Heartbeat("{\"t\":\"cam\",\"authority\":\"host\"}");Publish(null,false);return;
            }
            if(now>=nextTerrainRequest){
                var request=wire.TakeTerrainRequest();
                if(request!=null&&request.Epoch==groundEpoch&&Math.Abs(request.X-frame.X)<12&&Math.Abs(request.Z-frame.Z)<12&&Math.Abs(request.Y-frame.Y)<8){
                    nextTerrainRequest=now+.15;
                    // Preview90: flight requests measure native ground up to 40 m below (walking: 6 m).
                    if(request.Deep){string cells=GroundCells(request.X,request.Y,request.Z,4,4000);if(cells.Length>0)wire.Send("{\"t\":\"ground_surface\",\"epoch\":\""+groundEpoch+"\",\"c\":["+cells+"]}");}
                    else GroundAt(request.X,request.Y,request.Z);
                    Log.Write("bridge-ground","terrain requested ahead at="+F(request.X)+","+F(request.Y)+","+F(request.Z));
                }
            }
            bool screen=!string.IsNullOrEmpty(frame.Screen);
            if(screen&&!cursorOwned){originalCursor=controller.ShowMouseCursor;UWidgetLibrary.SetInputMode_GameAndUIEx(controller,null,EMouseLockMode.DoNotLock,false);controller.ShowMouseCursor=true;cursorOwned=true;}
            if(!screen&&cursorOwned){
                // The UI mode released viewport capture. Hide the UI cursor before
                // restoring game input, then focus the viewport without a click.
                controller.ShowMouseCursor=originalCursor;
                UWidgetLibrary.SetInputMode_GameOnly(controller);
                if(!originalCursor)UWidgetLibrary.SetFocusToGameViewport();
                cursorOwned=false;
                Log.Write("bridge-input","MC screen closed; gameplay cursor="+originalCursor+"; viewport focus restored");
            }
            router.GameplayActive=true;router.EnsureAttached();
            HoldNativeMovement();
            if(now>=nextComponents){nextComponents=now+1;CaptureNativeComponents();}
            for(int i=0;i<nativeVisible.Count;i++){var primitive=nativeVisible[i];if(!primitive.IsDestroyed&&!primitive.IsPendingKill&&!primitive.HiddenInGame)primitive.SetHiddenInGame(true,false);}
            // Live77 (enemies ignored the player after a shrine teleport; Boss idle after rebirth): native
            // arrival/rebirth/hit-reaction montages must finish or the player stays in a state enemies
            // do not target. The body is hidden (Steve skin) and movement is MC's, so animations keep running.
            for(int i=0;i<meshes.Count;i++){if(NativeObjectLifetime.Live(meshes[i])){meshes[i].PauseAnims=paused[i];meshes[i].SetOwnerNoSee(true);}}
            if(fresh){
                if(inputPaused){Log.Write("bridge-lag","pose resumed; age="+F(age)+"; MC authority retained");inputPaused=false;}
            }else if(!inputPaused){inputPaused=true;Log.Write("bridge-lag","pose delayed; age="+F(age)+"; input transport and MC camera/body/HUD retained");}
            // Input is independent of rendered pose age. Dropping transitions while
            // OpenGL stalls loses E/right-click and can strand a key until the next frame.
            // Genuine disconnect/gameplay loss still releases every held key below.
            SendInput(screen);
            if(NativeUiActive)return;
            worldLink.Update(now,frame,cameraActor);
            if(now>=nextAllyGround){var allies=wire.TakeAllyGround();if(allies!=null){nextAllyGround=now+.5;
                for(int i=0;i+2<allies.Length;i+=3)try{string cells=GroundCells(allies[i],allies[i+1],allies[i+2],2,4000);if(cells.Length>0)wire.Send("{\"t\":\"ground_surface\",\"epoch\":\""+groundEpoch+"\",\"c\":["+cells+"]}");}catch(Exception e){Log.Write("bridge-ground-error","ally ground: "+e.Message);}}}
            var foot=ConvertPosition(frame.X,frame.Y,frame.Z);
            // Preview88 (user: on a high block tower enemies lose the player and wander): the hidden native body
            // stays on the native ground below when the MC feet are 2.5-30 m above it, so enemy AI can path to the
            // tower foot (and break it). Hits from below are dropped by McPlayerHealthGate while this holds.
            var body=foot;FHitResult below;
            // Preview94 (live: walking downhill the decoy flipped every few frames, moved the body 9 m down and the
            // game killed it): only while the MC feet stand on one of the player's own MC blocks for 0.3 s.
            bool onBlock=OnMcBlock(foot);
            if(!onBlock)decoySince=0;else if(decoySince==0)decoySince=now;
            if(onBlock&&now-decoySince>=.3&&TraceFloor(foot,30,3000,out below)&&foot.Z-below.ImpactPoint.Z>250&&foot.Z-below.ImpactPoint.Z<3000){
                if(!decoyActive)McPlayerHealthGate.DecoyMovedAt=McPlayerHealthGate.Clock();
                body=new FVector(foot.X,foot.Y,below.ImpactPoint.Z);McPlayerHealthGate.ElevatedFeetY=frame.Y;
                if(!decoyActive){decoyActive=true;Log.Write("decoy","player "+F((foot.Z-below.ImpactPoint.Z)/100)+" m above native ground; native body placed on the ground below");}
            }else{McPlayerHealthGate.ElevatedFeetY=double.NaN;if(decoyActive){decoyActive=false;McPlayerHealthGate.DecoyMovedAt=McPlayerHealthGate.Clock();Log.Write("decoy","player back near native ground; native body follows the MC feet");}}
            player.SetActorLocationAndRotation(body+new FVector(0,0,halfHeight),new FRotator(0,(float)frame.Yaw,0),false,out _,true);
            var eye=ConvertPosition(frame.EyeX,frame.EyeY,frame.EyeZ);
            var rotation=new FRotator((float)-frame.Pitch,(float)frame.Yaw,(float)-frame.Roll);
            if(Log.Verbose)frameOrder.Sample(controller.PlayerCameraManager,frame.Number,now);
            cameraActor.SetActorLocationAndRotation(eye,rotation,false,out _,true);
            controller.SetControlRotation(rotation);
            frameOrder.Ticked(eye,rotation);
            controller.GetViewportSize(out int width,out int height);
            ApplyProjection(frame.Fov,width,height,now);
            if(controller.GetViewTarget()!=cameraActor)controller.SetViewTargetWithBlend(cameraActor,0);
            if(fresh&&now>=nextGround){nextGround=now+.25;Ground(frame);}
            if(fresh&&!screen&&now>=nextNativeRay&&frame.Number>lastNativeRayFrame){
                nextNativeRay=now+.1;
                try{TraceNativeRay(now,frame);}catch(Exception e){nextNativeRay=now+5;Log.Write("bridge-ray-error",e.GetType().Name+": "+e.Message);}
            }
            if(fresh)lastAccepted=frame;
            if(fresh&&Log.Verbose)LogMotion(now,frame);
            Publish(frame,true);
            if(now>=nextLog){nextLog=now+30;Log.Write("bridge","MC frame="+frame.Number+" screen="+frame.Screen+" foot="+F(frame.X)+","+F(frame.Y)+","+F(frame.Z)+" yaw="+F(frame.Yaw)+" pitch="+F(frame.Pitch)+" age="+F(age)+" peerPongs="+wire.PongCount);}
        }
        private FVector ConvertPosition(double x,double y,double z)
        {
            var absolute=coordinates.ToHost(new BridgePoint(x,y,z));
            return new FVector((float)(absolute.X-unrealOrigin.X),(float)(absolute.Y-unrealOrigin.Y),(float)(absolute.Z-unrealOrigin.Z));
        }
        private BridgePoint ToMc(FVector local)
        {return coordinates.ToMc(new BridgePoint(local.X+unrealOrigin.X,local.Y+unrealOrigin.Y,local.Z+unrealOrigin.Z));}
        private void LogMotion(double now,McBridgeWire.Frame frame)
        {
            if(now<nextMotionLog)return;nextMotionLog=now+.5;
            var previous=motionSample;motionSample=frame;
            if(previous==null||frame.Tick<=previous.Tick)return;
            if(frame.ForwardImpulse==0&&frame.LeftImpulse==0&&previous.ForwardImpulse==0&&previous.LeftImpulse==0&&!frame.SprintKeyDown&&!previous.SprintKeyDown)return;
            double dx=frame.X-previous.X,dz=frame.Z-previous.Z;
            double speed=Math.Sqrt(dx*dx+dz*dz)*20/(frame.Tick-previous.Tick);
            Log.Write("bridge-motion","tick="+frame.Tick+" horizontalSpeed="+F(speed)+" sprint="+frame.Sprinting+" latch="+frame.SprintKeyDown
                +" forward="+F(frame.ForwardImpulse)+" left="+F(frame.LeftImpulse)+" ground="+frame.OnGround+" collision="+frame.HorizontalCollision
                +" minor="+frame.MinorHorizontalCollision+" usingItem="+frame.UsingItem+" crouch="+frame.Crouching+" blind="+frame.Blindness
                +" foot="+F(frame.X)+","+F(frame.Y)+","+F(frame.Z));
        }
        // Preview41: the world tick updates Wukong's camera before the core ticker
        // runs, so a pose set in Update is rendered one engine frame late (live40:
        // PCM at frame F equalled the request from F-1). Re-apply the newest MC eye
        // pose at FCoreDelegates.OnBeginFrame, before the world tick. Update still
        // owns every other decision; this only repeats its last accepted camera move.
        // Preview72 (user item 2): cutscenes that start before the player pawn is ready (new-game
        // opening) are skipped from the mod tick with the world as context. Pre-rendered videos
        // are not sequencer movies and still play.
        private static double nextEarlySkip;private static bool earlyMovieLogged;
        private static bool? skipCutscenes;
        internal static bool SkipCutscenes(){
            if(skipCutscenes==null){skipCutscenes=System.IO.File.Exists(System.IO.Path.Combine(Log.Dir,"dqj-skip-cutscenes.on"));Log.Write("cutscene","policy: "+(skipCutscenes.Value?"skip (dqj-skip-cutscenes.on present)":"play natively; MC suspended until each cutscene ends"));}
            return skipCutscenes.Value;
        }
        internal static void EarlyCutsceneSkip(double now,UObject context)
        {
            if(context==null||now<nextEarlySkip)return;nextEarlySkip=now+1;
            bool canSkip;bool movie=GameApi.MoviePlaying(context,out canSkip);
            if(!movie){earlyMovieLogged=false;return;}
            if(!SkipCutscenes()||(DateTime.UtcNow-McWorldLink.LastBossDefeatUtc).TotalSeconds<180)return;
            try{if(!canSkip)GameApi.ForceSkippable(context);BGUFunctionLibraryCS.SkipCurrentSequence(context);
                if(!earlyMovieLogged){earlyMovieLogged=true;Log.Write("cutscene","pre-bridge cutscene skip requested; forced="+!canSkip);}}
            catch(Exception e){Log.Write("cutscene-error","pre-bridge: "+e.GetType().Name+": "+e.Message);}
        }
        internal void BeginFrame()
        {
            matchedMcFrame=0;
            if(beginFramePaused||GameApi.Loading()||!SessionLive||!owning||!initialized||groundFault||NativeUiActive||!NativeObjectLifetime.Live(cameraActor)||coordinates==null){frameOrder.SkippedBegin();return;}
            var frame=wire.Latest;
            if(frame==null||lastAccepted==null||frame.Number<lastAccepted.Number){frameOrder.SkippedBegin();return;}
            double age=(WindowsClock.Nanoseconds()-frame.Received)/1000000000.0;
            if(age<0||age>=.5||!SafePose(frame)){frameOrder.SkippedBegin();return;}
            var eye=ConvertPosition(frame.EyeX,frame.EyeY,frame.EyeZ);
            var rotation=new FRotator((float)-frame.Pitch,(float)frame.Yaw,(float)-frame.Roll);
            cameraActor.SetActorLocationAndRotation(eye,rotation,false,out _,true);
            controller.SetControlRotation(rotation);
            frameOrder.Began(eye,rotation,frame.Number);
            if(fovLock.Active){
                controller.GetViewportSize(out int width,out int height);
                float horizontal;
                if(HostProjection.TryHorizontal(frame.Fov,width,height,out horizontal)){camera.FieldOfView=horizontal;fovLock.Follow(player,controller,horizontal,lastUpdateTime);}
            }
            // The compositor uploads this exact MC ring slot for the frame being rendered.
            matchedMcFrame=(uint)frame.Number;
        }
        private void Enable()
        {
            try
            {
                if(cameraActor==null||camera==null){
                    var nativeCamera=BGUFunctionLibraryCS.BGUSpawnActor(NativeObjectLifetime.ActorWorld(player),UClass.GetClass<ACameraActor>(),player.GetActorLocation(),default(FRotator)) as ACameraActor;
                    if(nativeCamera==null)throw new InvalidOperationException("Native CameraActor unavailable");
                    cameraActor=nativeCamera;cameraActor.SetOwner(player);
                    camera=nativeCamera.CameraComponent;
                    if(camera==null)throw new InvalidOperationException("Native CameraActor has no owned camera component");
                    camera.ConstrainAspectRatio=false;camera.UsePawnControlRotation=false;
                    Log.Write("camera-source","native actor="+cameraActor.GetClass().GetPathName()+" component="+camera.GetPathName());
                }
                camera.SetActive(true,false);
                foreach(USkeletalMeshComponent mesh in UObject.GetObjects<USkeletalMeshComponent>())
                {if(mesh==null||mesh.IsDestroyed||mesh.GetOwner()!=player||meshes.Contains(mesh))continue;meshes.Add(mesh);paused.Add(mesh.PauseAnims);hidden.Add(mesh.OwnerNoSee);}
                controller.SetViewTargetWithBlend(cameraActor,0);owning=true;SetMcHud(false);Log.Write("bridge","real MC authority enabled");
                if(steveSkin!=null)steveSkin.SetNativeView(false);
            }
            catch(Exception e){Log.Write("bridge-error",e.Message);Release();try{if(cameraActor!=null)cameraActor.DestroyActor();}catch{}cameraActor=null;camera=null;}
        }
        private bool Key(string name)
        {
            FKey key;if(!keys.TryGetValue(name,out key)){key=UGSE_UMGFuncLib.GetKeyWithName(new FName(name));keys.Add(name,key);}return controller.IsInputKeyDown(key);
        }
        private void CaptureNativeComponents()
        {
            var root=player.GetRootComponent();if(root==null||root.IsDestroyed)return;
            List<USceneComponent> children;root.GetChildrenComponents(true,out children);
            if(children==null)return;
            foreach(var child in children){var primitive=child as UPrimitiveComponent;
                if(primitive==null||primitive is UCapsuleComponent||primitive.IsDestroyed||primitive.IsPendingKill||nativeVisible.Contains(primitive))continue;
                if(steveSkin!=null&&steveSkin.IsReplacementComponent(primitive))continue;
                nativeVisible.Add(primitive);nativeWasHidden.Add(primitive.HiddenInGame);
            }
        }
        private void SendInput(bool screen)
        {
            if(travelKeyMessages.Attached&&!travelKeyMessages.HasFocus){ResetInput();return;}
            if(inputMessages.Overflow){Log.Write("bridge-input-error","native input buffer overflow; release keys and resume from new presses");ResetInput();return;}
            // GUI pointer must arrive before a click in the same host frame.
            if(!travelKeyMessages.Attached&&screen&&controller.GetMousePosition(out float x,out float y))SendPointer(x,y);
            foreach(var input in inputMessages.Take()){
                if(screen&&input.X>=0&&input.Y>=0&&!SendWindowPointer(input.X,input.Y)){
                    Log.Write("bridge-input-error","source window client rectangle unavailable; reset input instead of sending a misplaced click");ResetInput();return;
                }
                if(input.Name==null)continue;
                // Typed text and edit keys only reach an open MC screen (chat/command).
                if(input.Name.StartsWith("Char:",StringComparison.Ordinal)){if(screen)wire.Send("{\"t\":\"char\",\"c\":"+input.Name.Substring(5)+"}");continue;}
                if(input.Name.StartsWith("ScreenKey:",StringComparison.Ordinal)){if(screen)wire.Send("{\"t\":\"screen_key\",\"k\":"+input.Name.Substring(10)+",\"down\":"+(input.Down?"true":"false")+"}");continue;}
                bool old;down.TryGetValue(input.Name,out old);
                if(input.Down==old)continue;
                down[input.Name]=input.Down;
                // Gameplay ESC opens Wukong's own menu; with an MC screen open it still closes that screen.
                if(input.Name=="Escape"&&!screen){if(input.Down){menuRequested=true;Log.Write("bridge-input","window Escape=True screen=False -> native menu request");}continue;}
                for(int i=0;i<Physical.Length;i++)if(input.Name==Physical[i]){
                    wire.Send("{\"t\":\"key\",\"k\":\""+Semantic[i]+"\",\"down\":"+(input.Down?"true":"false")+"}");
                    // Preview48 diagnostics: chat/command/escape/mouse edges are always logged (human-paced).
                    if(screen||input.Name=="LeftControl"||input.Name=="E"||input.Name=="T"||input.Name=="Slash"||input.Name=="Escape"
                        ||input.Name=="LeftMouseButton"||input.Name=="RightMouseButton")
                        Log.Write("bridge-input","window "+input.Name+"="+input.Down+" screen="+screen+" at="+input.X+","+input.Y);
                    break;
                }
                for(int i=0;i<Numbers.Length;i++)if(input.Name==Numbers[i]&&input.Down){slot=i;wire.Send("{\"t\":\"slot\",\"n\":"+slot+"}");break;}
            }
            if(!travelKeyMessages.Attached)for(int i=0;i<Physical.Length;i++){bool value=Key(Physical[i]);bool old;down.TryGetValue(Physical[i],out old);if(value==old)continue;down[Physical[i]]=value;if(Physical[i]=="Escape"&&!screen){if(value)menuRequested=true;continue;}wire.Send("{\"t\":\"key\",\"k\":\""+Semantic[i]+"\",\"down\":"+(value?"true":"false")+"}");}
            lock(wheelDeltas){while(wheelDeltas.Count>0){int delta=wheelDeltas.Dequeue();wire.Send("{\"t\":\"scroll\",\"d\":"+F(delta/120.0)+"}");Log.Write("bridge-input","native wheel delta="+delta+"; MC original scroll handler");}}
            controller.GetInputMouseDelta(out float dx,out float dy);
            if(!screen&&(dx!=0||dy!=0))wire.Send("{\"t\":\"look\",\"dx\":"+F(dx)+",\"dy\":"+F(-dy)+"}");
            if(!travelKeyMessages.Attached)for(int i=0;i<9;i++){string name=Numbers[i];bool value=Key(name);bool old;down.TryGetValue(name,out old);down[name]=value;if(value&&!old){slot=i;wire.Send("{\"t\":\"slot\",\"n\":"+slot+"}");}}
            bool menu=menuRequested;menuRequested=false;
            if(menu&&!screen){
                try{nativeMap.OpenMenu(lastUpdateTime,SuspendForNativeUi);}
                catch(Exception e){nativeMap.Clear();RestoreNativeUiCursor();Log.Write("native-map-error","ESC menu: "+e);}
                return;
            }
        }
        private bool HandleTravelRequest()
        {
            bool travelKey=Key("F6"),wasTravel;down.TryGetValue("F6",out wasTravel);down["F6"]=travelKey;
            bool request=travelRequested;travelRequested=false;
            bool screen=wire.Latest!=null&&!string.IsNullOrEmpty(wire.Latest.Screen);
            if((request||travelKey&&!wasTravel)&&!screen){
                var pose=wire.Latest;
                if(pose!=null&&pose.TravelKeyAvailable){
                    try{prepareBlocked=false;nativeMap.Open(lastUpdateTime,SuspendForNativeUi);return nativeMap.Active;}
                    catch(Exception e){nativeMap.Clear();RestoreNativeUiCursor();wire.Send("{\"t\":\"travel_notice\",\"text\":\"原生地图暂时无法打开，请按 M 重试\"}");Log.Write("native-map-error",e.ToString());}
                }
                else{
                    wire.Send("{\"t\":\"travel_notice\",\"text\":\"F6 已被 MC 或模组占用，传送快捷键暂时停用\"}");
                    Log.Write("travel","F6 suppressed: actual MC key mapping conflicts or state unavailable");
                }
            }
            return false;
        }
        private void SendPointer(double x,double y)
        {
            controller.GetViewportSize(out int w,out int h);
            if(w>0&&h>0)wire.Send("{\"t\":\"pointer\",\"x\":"+F(x/w)+",\"y\":"+F(y/h)+"}");
        }
        private bool SendWindowPointer(int x,int y)
        {
            int width,height;double nx,ny;
            if(!travelKeyMessages.TryGetPointerClientSize(out width,out height)||!NativePointerCoordinates.TryNormalize(x,y,width,height,out nx,out ny))return false;
            if(width!=pointerClientWidth||height!=pointerClientHeight){
                pointerClientWidth=width;pointerClientHeight=height;controller.GetViewportSize(out int renderWidth,out int renderHeight);
                Log.Write("bridge-input","WM pointer client="+width+"x"+height+"; render viewport="+renderWidth+"x"+renderHeight+"; source-window normalization");
            }
            wire.Send("{\"t\":\"pointer\",\"x\":"+F(nx)+",\"y\":"+F(ny)+"}");return true;
        }
        private void QueueWheel(int delta)
        {
            if(!owning||NativeUiActive)return;
            lock(wheelDeltas){if(wheelDeltas.Count<64)wheelDeltas.Enqueue(delta);}
        }
        private void QueueControl(string name,bool value,int x,int y)
        {
            if(!owning||NativeUiActive)return;
            inputMessages.Add(name,value,x,y);
        }
        private void QueuePointer(int x,int y)
        {
            var frame=wire.Latest;
            if(owning&&!NativeUiActive&&frame!=null&&!string.IsNullOrEmpty(frame.Screen))inputMessages.Add(null,false,x,y);
        }
        private void Ground(McBridgeWire.Frame frame)
        {
            GroundAt(frame.X,frame.Y,frame.Z);
        }
        private void GroundAt(double x,double y,double z)
        {
            string values=GroundCells(x,y,z);
            if(values.Length>0)wire.Send("{\"t\":\"ground_surface\",\"epoch\":\""+groundEpoch+"\",\"c\":["+values+"]}");
        }
        private void BeginGroundRecovery(McBridgeWire.Frame rejected)
        {
            recoveryPose=lastAccepted;groundFault=true;groundEpoch=Guid.NewGuid().ToString("N");groundCommitSent=false;
            groundPreparation=new McGroundPreparation(groundEpoch,new BridgePoint(recoveryPose.X,recoveryPose.Y,recoveryPose.Z),lastUpdateTime);
            ResetInput();worldLink.ResetTerrain();nextGroundWait=0;
            string support=GroundCells(recoveryPose.X,recoveryPose.Y,recoveryPose.Z);
            wire.Heartbeat("{\"t\":\"cam\",\"authority\":\"host\"}");
            wire.Send("{\"t\":\"bridge_prepare\",\"epoch\":\""+groundEpoch+"\",\"guard\":true,\"spawn\":["+F(recoveryPose.X)+","+F(recoveryPose.Y)+","+F(recoveryPose.Z)+"],\"yaw\":"+F(recoveryPose.Yaw)+",\"pitch\":"+F(recoveryPose.Pitch)+",\"c\":["+support+"]}");
            Publish(recoveryPose,true);
            Log.Write("bridge-ground-recover","unsafe foot="+F(rejected.X)+","+F(rejected.Y)+","+F(rejected.Z)+"; Steve retained; prepare at last accepted foot="+F(recoveryPose.X)+","+F(recoveryPose.Y)+","+F(recoveryPose.Z));
        }
        private void RecoverGround(double now,McBridgeWire.Frame frame,bool fresh)
        {
            if(recoveryPose==null){Publish(null,false);return;}
            if(groundPreparation.Expired(now)||(frame.GroundEpoch==groundEpoch&&!string.IsNullOrEmpty(frame.GroundFailure))){AbortGroundPrepare(frame.GroundFailure);return;}
            ResetInput();router.GameplayActive=true;
            bool near=frame.HasGroundSpawn&&groundPreparation.Ready(frame.GroundEpoch,frame.GroundReady,frame.GroundSpawn,new BridgePoint(frame.X,frame.Y,frame.Z),frame.GroundAnchorResolved);
            if(fresh&&frame.Health>0&&near){
                if(!groundCommitSent){groundCommitSent=true;wire.Send("{\"t\":\"bridge_commit\",\"epoch\":\""+groundEpoch+"\"}");}
                else if(frame.GroundCommitted){groundFault=false;lastAccepted=frame;recoveryPose=null;motionSample=null;Log.Write("bridge-ground-recover","support revalidated; MC can resume from new input presses");}
            }else if(now>=nextGroundWait){nextGroundWait=now+3;Log.Write("bridge-ground-recover","awaiting support epoch="+frame.GroundEpoch+" ready="+frame.GroundReady+" near="+near+" hp="+F(frame.Health));}
            Publish(lastAccepted,true);
        }
        private void AbortGroundPrepare(string reason)
        {
            Log.Write("bridge-ground-error","prepare aborted: "+(string.IsNullOrEmpty(reason)?"acknowledgement timeout":reason)+"; native movement restored, automatic retry waits for movement or a menu round trip");
            Release();blockedAt=player.GetActorLocation();prepareBlocked=true;Publish(null,false);
        }
        private bool TraceFloor(FVector at,float above,float below,out FHitResult hit)
        {
            var ignore=new List<AActor>{player};if(cameraActor!=null)ignore.Add(cameraActor);
            if(worldLink!=null)worldLink.IgnoreOwned(ignore);
            var start=at+new FVector(0,0,above);var end=at-new FVector(0,0,below);
            if(USystemLibrary.LineTraceSingle(player,start,end,ETraceTypeQuery.TraceTypeQuery1,false,ignore,EDrawDebugTrace.None,out hit,true,default(FLinearColor),default(FLinearColor),0))return true;
            return USystemLibrary.LineTraceSingle(player,start,end,ETraceTypeQuery.TraceTypeQuery1,true,ignore,EDrawDebugTrace.None,out hit,true,default(FLinearColor),default(FLinearColor),0);
        }
        private void TraceNativeRay(double now,McBridgeWire.Frame frame)
        {
            if(frame.GroundEpoch!=groundEpoch||!frame.GroundCommitted||frame.WorldId!=coordinates.World||double.IsNaN(frame.TerrainRayReach)||double.IsInfinity(frame.TerrainRayReach)||frame.TerrainRayReach<=0||frame.TerrainRayReach>12)return;
            var eye=new BridgePoint(frame.EyeX,frame.EyeY,frame.EyeZ);
            // Use MC's own float/LUT direction, so a near-edge ray is identical
            // at both ends instead of recomputing yaw/pitch with different trig.
            var direction=frame.TerrainRayDirection;
            if(!direction.Finite||Math.Abs(direction.X*direction.X+direction.Y*direction.Y+direction.Z*direction.Z-1)>.00001)return;
            var start=ConvertPosition(eye.X,eye.Y,eye.Z);
            var end=ConvertPosition(eye.X+direction.X*frame.TerrainRayReach,eye.Y+direction.Y*frame.TerrainRayReach,eye.Z+direction.Z*frame.TerrainRayReach);
            var ignore=new List<AActor>{player,cameraActor};worldLink.IgnoreOwned(ignore);
            FHitResult simple,complex;
            bool a=USystemLibrary.LineTraceSingle(player,start,end,ETraceTypeQuery.TraceTypeQuery1,false,ignore,EDrawDebugTrace.None,out simple,true,default(FLinearColor),default(FLinearColor),0);
            bool b=USystemLibrary.LineTraceSingle(player,start,end,ETraceTypeQuery.TraceTypeQuery1,true,ignore,EDrawDebugTrace.None,out complex,true,default(FLinearColor),default(FLinearColor),0);
            var complexHit=RayHit(b,complex);
            if(complexHit.Blocking&&!complexHit.Penetrating&&complex.ImpactNormal.Z>=.5f)FootprintSupport(ref complexHit,ignore);
            string packet=McNativeRay.Packet(frame.WorldId,groundEpoch,frame.Number,frame.Tick,eye,direction,frame.TerrainRayReach,RayHit(a,simple),complexHit);
            if(packet==null){if(now>=nextNativeRayLog){nextNativeRayLog=now+5;Log.Write("bridge-ray-error","native trace geometry outside verified segment/normal contract");}return;}
            wire.Send(packet);lastNativeRayFrame=frame.Number;
            if(now>=nextNativeRayLog){nextNativeRayLog=now+5;Log.Write("bridge-ray","frame="+frame.Number+" simple="+a+" complex="+b+" reach="+F(frame.TerrainRayReach)+" epoch="+groundEpoch+"; native surface target sent");}
        }
        // Preview85 (user: blocks misaligned/float on rough ground; wukong-steve fits blocks to native
        // geometry): the lowest native ground under the 1x1 footprint centred on the aim point (centre and
        // four corners, complex traces). MC rests a root block there, so it never floats and the uphill
        // side is buried. Needs 4 of 5 hits within 1.5 blocks of the aim height.
        private void FootprintSupport(ref McNativeRay.Hit hit,List<AActor> ignore)
        {
            var p=hit.Point;double low=double.PositiveInfinity;int found=0;
            for(int i=0;i<5;i++){
                double x=p.X+(i==0?0:(i&1)!=0?-.5:.5),z=p.Z+(i==0?0:i<3?-.5:.5);
                var start=ConvertPosition(x,p.Y+1.5,z);var end=ConvertPosition(x,p.Y-1.5,z);
                FHitResult ground;
                if(!USystemLibrary.LineTraceSingle(player,start,end,ETraceTypeQuery.TraceTypeQuery1,true,ignore,EDrawDebugTrace.None,out ground,true,default(FLinearColor),default(FLinearColor),0)||ground.StartPenetrating)continue;
                double y=ToMc(new FVector(ground.ImpactPoint.X,ground.ImpactPoint.Y,ground.ImpactPoint.Z)).Y;
                if(Math.Abs(y-p.Y)>1.5)continue;
                found++;if(y<low)low=y;
            }
            if(found>=4){hit.HasSupport=true;hit.Support=Math.Min(low,p.Y);}
        }
        private McNativeRay.Hit RayHit(bool blocking,FHitResult hit)
        {
            if(!blocking)return new McNativeRay.Hit();
            return new McNativeRay.Hit{Blocking=hit.BlockingHit,Penetrating=hit.StartPenetrating,
                Point=ToMc(new FVector(hit.ImpactPoint.X,hit.ImpactPoint.Y,hit.ImpactPoint.Z)),
                Normal=McNativeRay.NormalToMc(hit.ImpactNormal.X,hit.ImpactNormal.Y,hit.ImpactNormal.Z)};
        }
        private double nextAllyGround;private bool decoyActive;private double decoySince;
        // True when the first thing under the MC feet (0.6 m) is a collision box of one of the player's MC blocks.
        private bool OnMcBlock(FVector foot)
        {
            try{
                FHitResult hit;var ignore=new List<AActor>{player};if(cameraActor!=null)ignore.Add(cameraActor);
                if(!USystemLibrary.LineTraceSingle(player,foot+new FVector(0,0,20),foot-new FVector(0,0,60),ETraceTypeQuery.TraceTypeQuery1,false,ignore,EDrawDebugTrace.None,out hit,true,default(FLinearColor),default(FLinearColor),0))return false;
                var c=hit.Component.Get();var owner=c==null?null:c.GetOwner();
                return owner!=null&&!owner.IsDestroyed&&owner.GetOwner()==player;
            }catch{return false;}
        }
        // Preview92 (user: a door event did not fire): native event/trigger volumes the hidden body overlaps are
        // logged once each, so a level event that never starts can be told apart from a trigger never entered.
        private double nextOverlapProbe;private readonly HashSet<string> overlapsSeen=new HashSet<string>();
        private void OverlapProbe()
        {
            try{
                List<AActor> overlapping;player.GetOverlappingActors(out overlapping,default(TSubclassOf<AActor>));
                if(overlapping==null)return;
                foreach(var actor in overlapping){
                    if(actor==null||actor.IsDestroyed||actor is BGUCharacterCS||actor.GetOwner()==player)continue;
                    string key=actor.GetPathName();if(overlapsSeen.Count>2000)overlapsSeen.Clear();
                    if(overlapsSeen.Add(key))Log.Write("trigger","body overlaps "+actor.GetClass().GetName()+" "+actor.GetName());
                }
            }catch(Exception e){nextOverlapProbe+=10;Log.Write("trigger-error",e.GetType().Name+": "+e.Message);}
        }
        private string GroundCells(double px,double py,double pz){return GroundCells(px,py,pz,4);}
        // Preview94 (live: stuck walking downhill): walking measures 15 m down (was 6 m), so steep slopes ahead get support.
        private string GroundCells(double px,double py,double pz,int radius){return GroundCells(px,py,pz,radius,1500);}
        private string GroundCells(double px,double py,double pz,int radius,float below)
        {
            var values=new StringBuilder();int mx=(int)Math.Floor(px),mz=(int)Math.Floor(pz);
            for(int x=mx-radius;x<=mx+radius;x++)for(int z=mz-radius;z<=mz+radius;z++)
            {
                FHitResult hit;if(!TraceFloor(ConvertPosition(x+.5,py,z+.5),150,below,out hit))continue;
                double top=coordinates.ToMc(new BridgePoint(hit.ImpactPoint.X+unrealOrigin.X,hit.ImpactPoint.Y+unrealOrigin.Y,hit.ImpactPoint.Z+unrealOrigin.Z)).Y;
                int millimetres=(int)Math.Round(top*1000);int y=(int)Math.Ceiling(millimetres/1000.0)-1;
                int height=millimetres-y*1000;
                if(values.Length>0)values.Append(',');values.Append(x).Append(',').Append(y).Append(',').Append(z).Append(',').Append(height);
            }
            return values.ToString();
        }
        private bool SafePose(McBridgeWire.Frame frame)
        {
            if(frame.Health<=0||frame.Y<-60||frame.Y>316)return false;
            if(lastAccepted==null)return Math.Abs(frame.Y-spawnPoint.Y)<.2;
            double dt=Math.Max(.05,(frame.Received-lastAccepted.Received)/1000000000.0);
            double dx=frame.X-lastAccepted.X,dy=frame.Y-lastAccepted.Y,dz=frame.Z-lastAccepted.Z;
            var pearl=McWorldLink.PearlTarget;
            if(McPlayerHealthGate.Clock()-McWorldLink.PearlAt<3){
                double px=frame.X-pearl.X,py=frame.Y-pearl.Y,pz=frame.Z-pearl.Z;
                if(px*px+py*py+pz*pz<4){McWorldLink.PearlAt=-100;Log.Write("pearl","pearl landing accepted by host at "+F(frame.X)+","+F(frame.Y)+","+F(frame.Z));return true;}
            }
            if(dx*dx+dy*dy+dz*dz>Math.Pow(4+80*dt,2))return false;
            if(dy<-.05){FHitResult floor;var at=ConvertPosition(frame.X,lastAccepted.Y,frame.Z);
                if(TraceFloor(at,50,1200,out floor)&&ConvertPosition(frame.X,frame.Y,frame.Z).Z<floor.ImpactPoint.Z-20)return false;
            }
            return true;
        }
        private void Publish(McBridgeWire.Frame frame,bool active)
        {
            if(!NativeObjectLifetime.Live(controller)){if(memory!=null)memory.Write(16,0u);return;}
            if(memory==null)return;sequence+=2;memory.Write(8,sequence-1);ThreadFence();
            memory.Write(0,0x484a5144u);memory.Write(4,1u);memory.Write(16,active?1u:0u);
            controller.GetViewportSize(out int w,out int h);memory.Write(20,(uint)w);memory.Write(24,(uint)h);memory.Write(28,active?matchedMcFrame:0u);
            memory.Write(32,WindowsClock.Nanoseconds());memory.Write(40,GameApi.Frame());
            if(frame!=null){memory.Write(48,frame.EyeX);memory.Write(56,frame.EyeY);memory.Write(64,frame.EyeZ);
                memory.Write(72,(float)frame.Yaw);memory.Write(76,(float)frame.Pitch);memory.Write(80,(float)frame.Roll);memory.Write(84,(float)frame.Fov);}
            memory.Write(88,.10f);memory.Write(92,10000f);ThreadFence();memory.Write(8,sequence);
            poseDiagnostics.Sample(frame,active,controller,coordinates,unrealOrigin);
        }
        private static void ThreadFence(){System.Threading.Thread.MemoryBarrier();}
        private void ApplyProjection(double vertical,int width,int height,double now)
        {
            try{
                float horizontal;if(!HostProjection.TryHorizontal(vertical,width,height,out horizontal))return;
                camera.AspectRatio=(float)width/height;camera.FieldOfView=horizontal;
                var manager=controller.PlayerCameraManager;
                if(manager==null||manager.IsDestroyed||manager.IsPendingKill)return;
                if(Log.Verbose)cameraManagerDiagnostics.Update(now,controller,cameraActor,camera,horizontal);
                fovLock.Apply(player,controller,manager,horizontal,now);
                if(Log.Verbose&&now>=nextProjectionLog){
                    nextProjectionLog=now+10;
                    Log.Write("camera-projection","MCv="+F(vertical)+" desiredH="+F(horizontal)+" componentH="+F(camera.FieldOfView)
                        +" pcmH="+F(manager.GetFOVAngle())+" defaultH="+F(manager.DefaultFOV)
                        +" aspect="+width+"x"+height+" componentActive="+camera.IsActive()+" lockActive="+fovLock.Active+" "+fovLock.Cache(controller));
                }
            }catch(Exception e){if(now>=nextProjectionLog){nextProjectionLog=now+5;Log.Write("camera-projection-error",e.GetType().Name+": "+e.Message);}}
        }
        private static string F(double value){return value.ToString("0.######",CultureInfo.InvariantCulture);}
        private void Command(string command){wire.Send("{\"t\":\"cmd\",\"c\":\""+command+"\"}");}
        private void ResetInput()
        {
            lock(wheelDeltas)wheelDeltas.Clear();
            inputMessages.Clear();
            if(down.Count==0)return;
            wire.Send("{\"t\":\"input_reset\"}");down.Clear();
        }
        // Item 5 (user 2026-10-16): the monkey flashed during teleports and the death/respawn
        // frames because every suspend restored its visibility. A suspend (travel, rebirth,
        // interaction) keeps the body hidden; a real release (MC gone, menus) restores it.
        private bool bodyHeld;
        private void RestoreBody()
        {
            for(int i=0;i<meshes.Count;i++){try{if(NativeObjectLifetime.Live(meshes[i]))meshes[i].SetOwnerNoSee(hidden[i]);}catch{}}
            for(int i=0;i<nativeVisible.Count;i++){try{if(NativeObjectLifetime.Live(nativeVisible[i])
                &&(steveSkin==null||!steveSkin.ManagesNativeVisibility(nativeVisible[i])))nativeVisible[i].SetHiddenInGame(nativeWasHidden[i],false);}catch{}}
            bodyHeld=false;
        }
        private void Release(bool nativeUiHandoff=false,bool keepBodyHidden=false)
        {
            if(steveSkin!=null)steveSkin.SetNativeView(true);
            if(!keepBodyHidden&&bodyHeld){RestoreBody();Log.Write("bridge","Wukong body restored after a held suspend");}
            nextProjectionLog=0;cameraManagerDiagnostics.Reset();frameOrder.Reset();
            if(NativeObjectLifetime.Live(player)&&NativeObjectLifetime.Live(controller))fovLock.Release(player,controller);
            SetMcHud(true);
            bool wasInitialized=initialized;
            if(wasInitialized&&groundEpoch!=null)wire.Send("{\"t\":\"bridge_release\",\"epoch\":\""+groundEpoch+"\"}");
            router.GameplayActive=false;
            inputPaused=false;
            motionSample=null;nextMotionLog=0;
            groundFault=false;recoveryPose=null;mcDead=false;
            groundPreparation=null;
            lastNativeRayFrame=0;nextNativeRay=0;
            if(worldLink!=null){worldLink.Dispose();worldLink=null;initialized=false;}
            if(cursorOwned){if(NativeObjectLifetime.Live(controller)){if(!nativeUiHandoff)UWidgetLibrary.SetInputMode_GameOnly(controller);controller.ShowMouseCursor=originalCursor;}cursorOwned=false;}
            ResetInput();
            travelRequested=false;
            ReleaseNativeMovement();
            if(!owning)return;owning=false;
            try{if(NativeObjectLifetime.Live(player)&&NativeObjectLifetime.Live(controller))controller.SetViewTargetWithBlend(player,0);}catch{}
            // Animations always resume (native rebirth/interaction montages must run).
            for(int i=0;i<meshes.Count;i++){try{if(NativeObjectLifetime.Live(meshes[i]))meshes[i].PauseAnims=paused[i];}catch{}}
            if(keepBodyHidden)bodyHeld=true;else RestoreBody();
            McPlayerHealthGate.ElevatedFeetY=double.NaN;decoyActive=false;Log.Write("bridge","authority released to host");
        }
        // Returns true while the MC death screen owns the session. Wukong stays put (the MC
        // pose is not applied), the MC frame and death screen stay on top, and the cursor and
        // input go to MC so its Respawn button works. MC Respawn -> native Evt_UnitRebirth
        // (ERebirthType.RebirthPoint): Wukong teleports to the current shrine with its own
        // loading and rebirth reset; the travel reattach then places Steve there.
        private bool McDeath(double now,McBridgeWire.Frame frame,bool fresh)
        {
            if(frame.Health<=0){
                if(!mcDead){mcDead=true;ResetInput();Log.Write("life","MC player died; MC death screen owns the session; Wukong held at last accepted position");}
                bool screen=!string.IsNullOrEmpty(frame.Screen);
                if(screen&&!cursorOwned){originalCursor=controller.ShowMouseCursor;UWidgetLibrary.SetInputMode_GameAndUIEx(controller,null,EMouseLockMode.DoNotLock,false);controller.ShowMouseCursor=true;cursorOwned=true;}
                router.GameplayActive=true;router.EnsureAttached();
                HoldNativeMovement();
                SendInput(screen);
                if(worldLink!=null)worldLink.Update(now,frame,cameraActor);
                Publish(frame,true);
                return true;
            }
            if(!fresh)return true;
            mcDead=false;
            Log.Write("life","MC respawn pressed; native rebirth at current shrine");
            try{
                NativeEventInvoker.Invoke(NativeEventInvoker.Signal(GameApi.Events(player),"Evt_UnitRebirth"),(ERebirthType)0);
                rebirthFrom=player.GetActorLocation();rebirthMovedAt=0;
                SuspendForTravel();rebirthPending=true;
                Log.Write("life","native rebirth requested; MC reattaches after the native loading");
            }catch(Exception e){
                Log.Write("life-error","native rebirth: "+e.GetType().Name+": "+e.Message+"; falling back to ground recovery at last position");
                return false;
            }
            return true;
        }
        // Preview75 (user: grabs/throws play as-is, Steve is grabbed). While the player is in a native
        // animation sync, MC keeps its session (HP gate: damage still goes through MC, no native death)
        // but the native third-person camera shows Steve (skin) being grabbed/thrown; MC pose, input and
        // overlay pause. When the sync ends MC reattaches where the native animation left the player.
        private bool grabView;
        private bool GrabView(double now,McBridgeWire.Frame frame)
        {
            if(playerSyncing){
                if(!grabView){
                    grabView=true;ResetInput();router.GameplayActive=false;
                    if(controller!=null&&!controller.IsDestroyed){fovLock.Release(player,controller);controller.SetViewTargetWithBlend(player,.15f);}
                    for(int i=0;i<meshes.Count;i++){try{if(NativeObjectLifetime.Live(meshes[i]))meshes[i].PauseAnims=paused[i];}catch{}}
                    ReleaseNativeMovement();
                    Log.Write("grab","player grabbed: native third-person view of Steve; MC session kept");
                }
                if(steveSkin!=null)steveSkin.Reveal(true);
                if(worldLink!=null)worldLink.Update(now,frame,cameraActor);
                wire.Heartbeat("{\"t\":\"cam\",\"authority\":\"host\"}");Publish(null,false);
                return true;
            }
            grabView=false;
            Log.Write("grab","grab ended; MC reattaches at the native landing position");
            Release();Publish(null,false);
            return true;
        }
        // Preview97: CG-only visual enlargement, one complete replacement per actor. Interaction montages
        // never enable this mode. Known opening hero roles qualify alongside real player stand-ins.
        private void SuspendForTravel(bool showBody=false)
        {
            travelStarted=lastUpdateTime;travelSawLoading=false;travelPending=true;
            Release(false,!showBody);wire.Heartbeat("{\"t\":\"cam\",\"authority\":\"host\"}");Publish(null,false);
        }
        internal void PrepareForPrologueSkip(){SuspendForTravel(true);}
        private void AbortTravel()
        {
            travelPending=false;travelSawLoading=false;
            Log.Write("travel","native request failed; MC can reattach when gameplay is ready");
        }
        // Preview54: Wukong's own rest restores HP while MC is suspended (the HP gate is
        // uninstalled with the world link), and the next link sync sets MC health from it.
        // Complete the pair: clear MC status effects that would keep hurting after a
        // rest, and top HP up only if the native rest left it below max.
        private void AfterShrineRest()
        {
            try{
                float hp=BGUFunctionLibraryCS.BGUGetFloatAttr(player,EBGUAttrFloat.Hp),max=BGUFunctionLibraryCS.BGUGetFloatAttr(player,EBGUAttrFloat.HpMax);
                if(max>0&&hp<max){BGUFunctionLibraryCS.BGUSetAttrValue(player,EBGUAttrFloat.Hp,max);Log.Write("shrine","rest top-up hp "+F(hp)+"->"+F(max));}
                else Log.Write("shrine","native rest restored hp="+F(hp)+"/"+F(max));
                Command("effect clear @a");
                wire.Send("{\"t\":\"shrine_rest\"}"); // Preview105: MC repairs gear for emeralds
                Log.Write("shrine","MC status effects cleared after rest; MC health follows host on reattach");
            }catch(Exception e){Log.Write("shrine-error","after rest: "+e.GetType().Name+": "+e.Message);}
        }
        private void SuspendForNativeUi()
        {
            prepareBlocked=false;
            nativeUiCursor.Begin(cursorOwned,originalCursor);
            var frame=wire.Latest;
            if(frame!=null&&!string.IsNullOrEmpty(frame.Screen))wire.Send("{\"t\":\"key\",\"k\":\"escape\",\"down\":true}");
            Release(true);wire.Heartbeat("{\"t\":\"cam\",\"authority\":\"host\"}");Publish(null,false);
            controller.ShowMouseCursor=true;
            Log.Write("travel","native UI handoff; MC keys released and overlay disabled");
        }
        private void NativeInteraction(Action nativePress)
        {
            var frame=wire.Latest;
            if(!owning||frame==null||!string.IsNullOrEmpty(frame.Screen)||Key("E")){
                Log.Write("shrine","interaction suppressed: MC screen/inventory input or no MC authority");return;
            }
            try{
                nativeShrine.Start(lastUpdateTime,()=>{
                    nativeUiCursor.Begin(cursorOwned,originalCursor);
                    Release(true);wire.Heartbeat("{\"t\":\"cam\",\"authority\":\"host\"}");Publish(null,false);
                    Log.Write("interaction","native selected-target handoff; original movement and animation restored");
                },nativePress);
            }catch(Exception e){nativeShrine.Clear();if(!nativeMap.Active)RestoreNativeUiCursor();Log.Write("shrine-error",e.ToString());}
        }
        // Preview102 (user: "I run toward the campfire for a while before it triggers"): the native interaction
        // prompt is hidden under MC, and the interact key only reaches the game once Wukong has selected a
        // target. Mirror that selection on the MC action bar so the player knows when the key will work.
        private double nextHint,hintRefresh;private string lastHint="";
        private void InteractionHint(double now)
        {
            if(now<nextHint||shrineAdapter==null)return;nextHint=now+.25;
            string text="";
            if(owning&&!NativeUiActive&&wire.Latest!=null&&string.IsNullOrEmpty(wire.Latest.Screen))
                try{var state=shrineAdapter.Read(0);if(state.SelectedInteractable&&!state.Busy)text=HintText(state);
                    // Preview105: tell MC when Steve stands at an activated shrine (right-click opens the trader there).
                    bool near=state.SelectedShrine&&state.Activated;
                    if(near!=lastNearShrine){lastNearShrine=near;wire.Send("{\"t\":\"shrine\",\"near\":"+(near?"true":"false")+",\"id\":"+state.Id+"}");}
                }catch{}
            if(text==lastHint&&(text.Length==0||now<hintRefresh))return;
            lastHint=text;hintRefresh=now+1.5;
            wire.Send("{\"t\":\"travel_notice\",\"text\":\""+text+"\"}");
        }
        private static string HintText(NativeShrineState state)
        {
            string c=state.TargetClass??"";
            if(state.SelectedShrine)return state.Activated?"Shrine - interact: rest/repair | right-click empty hand: trader | weapon + book in off-hand: enchant":"Shrine - press interact to light it";
            if(c.IndexOf("Collection",StringComparison.OrdinalIgnoreCase)>=0)return "Press interact to gather";
            if(c.IndexOf("Chest",StringComparison.OrdinalIgnoreCase)>=0||c.IndexOf("Box",StringComparison.OrdinalIgnoreCase)>=0)return "Press interact to open";
            if(c.IndexOf("CharacterAI",StringComparison.OrdinalIgnoreCase)>=0||c.IndexOf("NPC",StringComparison.OrdinalIgnoreCase)>=0)return "Press interact to talk";
            return "Press interact";
        }
        private void RestoreNativeUiCursor()
        {
            nativeUiCursor.End(value=>{
                if(NativeObjectLifetime.Live(controller)){controller.ShowMouseCursor=value;Log.Write("native-ui-cursor","restored gameplay cursor="+value);}
            });
        }
        private void SetMcHud(bool hidden)
        {
            if(wire==null||mcHudHidden==hidden)return;
            wire.Send("{\"t\":\"hud\",\"hidden\":"+(hidden?"true":"false")+"}");mcHudHidden=hidden;
            Log.Write("bridge-hud","MC HUD hidden="+hidden);
        }
        public void Dispose()
        {
            if(hitProbe!=null)hitProbe.Dispose();hitProbe=null;
            if(grabSkip!=null)grabSkip.Dispose();grabSkip=null;
            if(travelKeyMessages!=null)travelKeyMessages.Dispose();travelKeyMessages=null;
            nativeMap.Clear();RestoreNativeUiCursor();
            if(nativeShrine!=null)nativeShrine.Clear();
            Release();try{Publish(null,false);}catch{}
            if(steveSkin!=null)steveSkin.Dispose();steveSkin=null;
            try{SetMcHud(false);}catch{}
            router.Dispose();wire.Dispose();poseDiagnostics.Dispose();if(memory!=null)memory.Dispose();if(mapping!=null)mapping.Dispose();
            try{if(NativeObjectLifetime.Live(cameraActor))cameraActor.DestroyActor();}catch{}
        }
        internal void Invalidate(){beginFramePaused=true;router.GameplayActive=false;ResetInput();if(travelPending)travelSawLoading=true;nativeMap.Loading();if(nativeShrine!=null)nativeShrine.Clear();SetMcHud(true);wire.Heartbeat("{\"t\":\"cam\",\"authority\":\"host\"}");if(memory!=null)memory.Write(16,0u);}
        private void HoldNativeMovement()
        {var move=player.CharacterMovement;movementLease.Hold((int)move.MovementMode,move.CustomMovementMode,()=>move.DisableMovement());}
        private void ReleaseNativeMovement()
        {try{movementLease.Release((mode,custom)=>{if(NativeObjectLifetime.Live(player)&&NativeObjectLifetime.Live(player.CharacterMovement))player.CharacterMovement.SetMovementMode((EMovementMode)mode,custom);});}catch{}}
    }
}
