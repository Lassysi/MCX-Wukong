using System;
using System.Reflection;
using b1;
using UnrealEngine.Engine;

namespace DqjCombat
{
    // Item 1 (user 2026-10-16): Steve cannot be grabbed, so enemy grab/execution attacks on the
    // player always take the game's own "grab missed" branch: the native pre-check handler is
    // wrapped and, when the grabber's lock target is the player, BGS_AnimationSyncSystem.
    // OnAnimationSyncFailed(host, JumpToSectionName) runs instead (the enemy plays its miss
    // section). Grabs on any other target keep the original handler.
    internal sealed class NativeGrabSkip : IDisposable
    {
        private static readonly BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private readonly BGUPlayerCharacterCS player;
        private EventHandle handle;private Delegate original,mine;private object system;private MethodInfo fail;
        internal NativeGrabSkip(BGUPlayerCharacterCS player)
        {
            this.player=player;
            try{
                handle=HookLedger.Resolve(BGS_EventCollectionCS.Get(player),"Evt_BGS_BeginAnimationSyncPreCheck");
                var native=HookLedger.ReadHandlers(handle);
                if(native==null||native.Length!=1||native[0].Target==null||native[0].Target.GetType().FullName!="b1.BGS_AnimationSyncSystem")
                    throw new InvalidOperationException("native grab pre-check baseline differs ("+(native==null?0:native.Length)+" handlers)");
                system=native[0].Target;fail=system.GetType().GetMethod("OnAnimationSyncFailed",Members);
                if(fail==null)throw new MissingMethodException("b1.BGS_AnimationSyncSystem","OnAnimationSyncFailed");
                original=native[0];mine=DelegateFactory.Create(original.GetType(),OnPreCheck);
                string error;
                if(!HookLedger.Uninstall(handle,original,out error))throw new InvalidOperationException(error);
                if(!HookLedger.Install(handle,mine,out error)){HookLedger.Install(handle,original,out error);throw new InvalidOperationException(error);}
                Log.Write("grab","grab skip installed on Evt_BGS_BeginAnimationSyncPreCheck");
            }catch(Exception e){mine=null;Log.Write("grab","grab skip not installed: "+e.Message);}
        }
        private void OnPreCheck(object[] args)
        {
            try{
                var host=args.Length>3?args[0] as AActor:null;
                if(host!=null&&!host.IsDestroyed&&player!=null&&!player.IsDestroyed){
                    var info=BGUFunctionLibraryCS.BGUGetSkillBaseTargetInfo(host);
                    if(info!=null&&info.LockTargetActor==player){
                        fail.Invoke(system,new object[]{host,args[3]});
                        Log.Write("grab","enemy grab on the player skipped (native miss branch); host="+host.GetName());
                        return;
                    }
                }
            }catch(Exception e){Log.Write("grab-error",e.GetType().Name+": "+e.Message);}
            original.DynamicInvoke(args);
        }
        public void Dispose()
        {
            if(mine==null)return;string error;
            HookLedger.Uninstall(handle,mine,out error);HookLedger.Install(handle,original,out error);mine=null;
        }
    }

    // Item 2 (user 2026-10-16): interaction points locked behind a missing item/story item open
    // with the interact key. The native flag BUC_InteractData.ItemRequireCondition (true = a
    // required bag item is missing) is cleared on interactables within 15 m every 0.5 s.
    internal sealed class NativeInteractUnlock
    {
        private static readonly System.Collections.Generic.List<EObjectTypeQuery> Types=new System.Collections.Generic.List<EObjectTypeQuery>{
            EObjectTypeQuery.ObjectTypeQuery1,EObjectTypeQuery.ObjectTypeQuery2,EObjectTypeQuery.ObjectTypeQuery3,
            EObjectTypeQuery.ObjectTypeQuery4,EObjectTypeQuery.ObjectTypeQuery5,EObjectTypeQuery.ObjectTypeQuery6};
        private readonly System.Collections.Generic.HashSet<string> logged=new System.Collections.Generic.HashSet<string>();
        private readonly System.Collections.Generic.HashSet<string> blocked=new System.Collections.Generic.HashSet<string>();
        private double next;
        internal void Update(double now,BGUPlayerCharacterCS player)
        {
            if(now<next||player==null||player.IsDestroyed)return;next=now+.5;
            try{
                System.Collections.Generic.List<AActor> actors;
                if(!USystemLibrary.SphereOverlapActors(player,player.GetActorLocation(),1500,Types,null,new System.Collections.Generic.List<AActor>{player},out actors)||actors==null)return;
                foreach(var actor in actors){
                    if(actor==null||actor.IsDestroyed)continue;
                    BUC_InteractData data=null;try{data=BGU_DataUtil.GetReadOnlyData<BUC_InteractData>(actor);}catch{}
                    if(data==null)continue;
                    // Preview91 (user: nothing may soft-lock progress): every interactable the native rules refuse is
                    // logged once with its reason; item locks (flag and constraint) are lifted, other reasons (enemies
                    // around, cooldown, story restriction) stay native and only show up in the log.
                    var constraint=data.InteractConstraint;
                    if(data.ItemRequireCondition||constraint==EInteractConstraint.ItemRequireCondition){
                        data.ItemRequireCondition=false;
                        if(constraint==EInteractConstraint.ItemRequireCondition)data.InteractConstraint=EInteractConstraint.None;
                        if(logged.Add(actor.GetPathName()))Log.Write("interaction","item lock lifted on "+actor.GetName());
                    }else if(data.InteractUIState==EInteractUIState.CantInteract&&constraint!=EInteractConstraint.None&&constraint!=EInteractConstraint.Busy
                        &&constraint!=EInteractConstraint.Interacting&&constraint!=EInteractConstraint.CD&&constraint!=EInteractConstraint.OtherIsUsing
                        &&blocked.Add(actor.GetPathName()+":"+constraint))
                        Log.Write("interaction","blocked: "+actor.GetName()+" reason="+constraint+" unit="+data.InteractiveUnitID);
                }
            }catch(Exception e){next=now+5;Log.Write("interaction-error",e.GetType().Name+": "+e.Message);}
        }
    }
}
