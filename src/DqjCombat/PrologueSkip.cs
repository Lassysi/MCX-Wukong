using System;
using System.IO;
using b1;
using UnrealEngine.Engine;

namespace DqjCombat
{
    // Preview97. Replay the native TaskNodeInstance_PrologueCompleted boundary, not a coordinate teleport.
    // Evidence: .work/preview97-build/il-prologue-task.txt and il-prologue-server.txt.
    internal sealed class PrologueSkip
    {
        private bool? enabled;
        private UWorld observedWorld;
        private APlayerState state;
        private PrologueSkipGate gate=new PrologueSkipGate();
        private double nextPoll,readySince,nextMovieSkip,travelAt,completionAt;
        private int targetMap,expectedChapter;
        private bool holding,loggedMovie;

        internal bool Update(double now,UWorld world,Action releaseBridge)
        {
            if(enabled==null){enabled=File.Exists(Path.Combine(Log.Dir,"skip-prologue.on"));Log.Write("prologue-skip","policy="+(enabled.Value?"native prologue completion -> first chapter only":"disabled"));}
            if(!enabled.Value)return false;
            if(observedWorld!=world){
                if(holding)Log.Write("prologue-skip","world changed after native travel; bridge may attach to the destination");
                observedWorld=world;state=null;gate=new PrologueSkipGate();holding=false;readySince=0;completionAt=0;loggedMovie=false;nextPoll=0;
            }
            if(now<nextPoll)return holding;
            nextPoll=now+.25;
            try{
                if(gate.Failed){holding=false;return false;}
                int current=BGUFuncLibMap.GetCurLevelId(world);
                if(gate.TravelStarted){
                    if(current==targetMap){holding=false;return false;}
                    if(now-travelAt>60){gate.Fail();holding=false;Log.Write("prologue-skip-error","native travel did not leave the prologue within 60 s; no retry");}
                    return holding;
                }
                if(gate.Requested){
                    if(gate.Timeout(now)){holding=false;Log.Write("prologue-skip-error","completion reply timed out; no teleport or retry");return false;}
                    if(gate.CompletionReady){
                        if(!NativeObjectLifetime.Live(state)||NativeObjectLifetime.ActorWorld(state)!=world
                            ||UGameplayStatics.GetPlayerState(world,0)!=state)throw new InvalidOperationException("player state changed while completion was pending");
                        var controller=GameApi.Controller(world);
                        var role=controller==null?null:BGU_DataUtil.GetReadOnlyData<BPC_PlayerRoleData>(controller);
                        if(completionAt==0)completionAt=now;
                        if(role==null||role.RoleData==null||role.RoleData.RoleCs==null||role.RoleData.RoleCs.Chapter==null
                            ||role.RoleData.RoleCs.Chapter.CurChapter!=expectedChapter){
                            if(now-completionAt<5)return true;
                            throw new InvalidOperationException("native completion reply did not advance the chapter; travel withheld");
                        }
                        ValidateFinish(world,state);
                        if(!gate.TakeCompletion())return true;
                        Finish(world,state);travelAt=now;
                        Log.Write("prologue-skip","native completion committed; first-chapter loading requested; targetMap="+targetMap);
                    }
                    return holding;
                }
                int prologue=BGUFuncLibMap.GetPrologueLevelId(world);
                targetMap=BGUFuncLibMap.GetAfterPrologueLevelId(world);
                if(current!=prologue||prologue<=0){readySince=0;return false;}
                var pc=GameApi.Controller(world);
                var ps=UGameplayStatics.GetPlayerState(world,0);
                var data=pc==null?null:BGU_DataUtil.GetReadOnlyData<BPC_PlayerRoleData>(pc);
                if(ps==null||ps.IsDestroyed||ps.IsPendingKill||data==null||data.RoleData==null||data.RoleData.RoleCs==null
                    ||data.RoleData.RoleCs.Chapter==null){readySince=0;return false;}
                int initialChapter=GameDBRuntime.GetCommLogicCfgValue(ResB1.CommCfgType.DefaultChapterId);
                if(!PrologueSkipGate.Eligible(current,prologue,targetMap,data.RoleData.RoleCs.Chapter.CurChapter,initialChapter))return false;
                var chapter=GameDBRuntime.GetChapterDesc(initialChapter);
                if(chapter==null||GameDBRuntime.GetChapterDesc(chapter.NextChapterId)==null)return false;
                expectedChapter=chapter.NextChapterId;
                var playGo=BGW_PlayGoMgr.Get(world);
                if(playGo==null||playGo.IsDownloadIncomplete())return false;
                if(GameApi.NativeTeleporting(world))return false;
                if(!holding){releaseBridge();holding=true;}
                bool canSkip;
                if(GameApi.MoviePlaying(world,out canSkip)){
                    readySince=0;
                    if(now>=nextMovieSkip){nextMovieSkip=now+1;if(!canSkip)GameApi.ForceSkippable(world);BGUFunctionLibraryCS.SkipCurrentSequence(world);
                        if(!loggedMovie){loggedMovie=true;Log.Write("prologue-skip","opening CG native skip requested; later chapters are excluded");}}
                    return true;
                }
                if(readySince==0){readySince=now;return true;}
                if(now-readySince<2)return true;
                // Preflight every event before the transaction can change the save.
                var request=NativeEventInvoker.Signal(BTF_EventCollectionCS.Get(ps),"Evt_ChapterPrologueCompletedReq");
                var requestGate=gate;
                Action<ResB1.MsgErrCode,CsB1.CSMsgChapterPrologueCompletedReq,CsB1.CSMsgChapterPrologueCompletedRes> reply=
                    delegate(ResB1.MsgErrCode code,CsB1.CSMsgChapterPrologueCompletedReq req,CsB1.CSMsgChapterPrologueCompletedRes res){
                        requestGate.Reply(Convert.ToInt32(code)==0);Log.Write("prologue-skip","completion reply="+code);};
                NativeEventInvoker.Validate(request,new CsB1.CSMsgChapterPrologueCompletedReq(),reply,null,true);
                ValidateFinish(world,ps);
                state=ps;
                if(gate.Begin(now)){
                    Log.Write("prologue-skip","request native prologue completion; sourceMap="+current+" targetMap="+targetMap+" chapter="+initialChapter);
                    NativeEventInvoker.Invoke(request,new CsB1.CSMsgChapterPrologueCompletedReq(),reply,null,true);
                }
                return true;
            }catch(Exception e){gate.Fail();holding=false;Log.Write("prologue-skip-error",e.GetType().Name+": "+e.Message+"; automatic retry disabled for this world");return false;}
        }
        private static void ValidateFinish(UWorld world,APlayerState ps)
        {
            var events=BPS_EventCollectionCS.Get(ps);
            NativeEventInvoker.Validate(NativeEventInvoker.Signal(BGW_EventCollection.Get(world),"Evt_PostPrologueCompleted"));
            NativeEventInvoker.Validate(NativeEventInvoker.Signal(events,"Evt_ForceSetRebirthPoint"),1001);
            NativeEventInvoker.Validate(NativeEventInvoker.Signal(events,"Evt_BPS_TeleportTo"),ETeleportTypeV2.PrologueCompleted,null,EPlayerTeleportReason.PrologueCompleted);
        }
        private static void Finish(UWorld world,APlayerState ps)
        {
            // Same order and arguments as the native task. The native template selects the default PlayerStart.
            NativeEventInvoker.Invoke(NativeEventInvoker.Signal(BGW_EventCollection.Get(world),"Evt_PostPrologueCompleted"));
            var events=BPS_EventCollectionCS.Get(ps);
            NativeEventInvoker.Invoke(NativeEventInvoker.Signal(events,"Evt_ForceSetRebirthPoint"),1001);
            var trans=BGU_DataUtil.GetReadOnlyData<BPC_TransData>(ps);if(trans!=null)trans.SetCurActorResId(0);
            NativeEventInvoker.Invoke(NativeEventInvoker.Signal(events,"Evt_BPS_TeleportTo"),ETeleportTypeV2.PrologueCompleted,null,EPlayerTeleportReason.PrologueCompleted);
        }
    }
}
