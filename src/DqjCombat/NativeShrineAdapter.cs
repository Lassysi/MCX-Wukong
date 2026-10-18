using System;
using System.Reflection;
using b1;
using UnrealEngine.Engine;

namespace DqjCombat
{
    internal sealed class NativeShrineAdapter
    {
        private static readonly BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private readonly BGUPlayerCharacterCS player;
        internal NativeShrineAdapter(BGUPlayerCharacterCS player){this.player=player;}
        internal NativeShrineState Read(int tracked)
        {
            var playerWorld=NativeObjectLifetime.ActorWorld(player);
            if(playerWorld==null)throw new InvalidOperationException("Native player world is not ready");
            // BPS_PlayerInteractComp.OnAttach uses RequireWritablePlayerStateData.
            // The same DStore exists for Controller but its ReadData is empty.
            var data=NativeTravelCatalogue.ActorData(player,typeof(b1.UI.DS_IBPC_PlayerInteractData),"LocalPlayerState");
            if(data==null)throw new InvalidOperationException("Native player interaction data is not ready");
            var state=new NativeShrineState{Busy=(bool)Call(data,"IsInInteraction")};
            var reference=Member(data,"BestInteractEntityRef");
            var entity=reference==null?null:Member(reference,"Ref");
            var actor=entity==null?null:Member(entity,"WorldObj") as AActor;
            state.Diagnostic="dataOwner=LocalPlayerState busy="+state.Busy+" target="+(actor==null?"none":actor.GetType().FullName)
                +" sameWorld="+(NativeObjectLifetime.Live(actor)&&NativeObjectLifetime.ActorWorld(actor)==playerWorld);
            state.SelectedInteractable=NativeObjectLifetime.Live(actor)&&actor!=player&&NativeObjectLifetime.ActorWorld(actor)==playerWorld;
            if(state.SelectedInteractable)state.TargetClass=actor.GetType().Name+" "+actor.GetClass().GetName();
            if(actor is BGURebirthPointBase&&NativeObjectLifetime.Live(actor)&&NativeObjectLifetime.ActorWorld(actor)==playerWorld){
                state.SelectedShrine=true;
                MethodInfo selected=null;
                foreach(var method in typeof(BGU_DataUtil).GetMethods(Any)){
                    var args=method.GetParameters();
                    if(method.Name!="GetUnPersistentReadOnlyData"||!method.IsGenericMethodDefinition||method.GetGenericArguments().Length!=1
                        ||args.Length!=1||args[0].ParameterType!=typeof(AActor))continue;
                    if(selected!=null)throw new InvalidOperationException("Ambiguous native shrine data reader");selected=method;
                }
                if(selected==null)throw new MissingMethodException("GetUnPersistentReadOnlyData<BUC_RebirthPointData>(AActor)");
                var shrine=Invoke(selected.MakeGenericMethod(typeof(BUC_RebirthPointData)),null,new object[]{actor});
                if(shrine==null)throw new InvalidOperationException("Native selected shrine has no rebirth data");
                state.Id=Convert.ToInt32(Member(shrine,"RebirthPointConfigID"));
                state.Diagnostic+=" shrine="+state.Id;
            }
            if(tracked>0)state.Id=tracked;
            if(state.Id>0){
                var rebirth=NativeTravelCatalogue.ControllerData(player,typeof(b1.UI.DS_IBPC_RebirthPointData));
                if(rebirth==null)throw new InvalidOperationException("Native activated shrine data is not ready");
                state.Activated=(bool)Call(rebirth,"IsRebirthPointActived",state.Id);
            }
            return state;
        }
        internal bool LeaveActivatedShrineMenu()
        {
            var pages=(object)B1UI.GSG.GSPageOP;if(pages==null)return false;
            int id=Convert.ToInt32(Enum.Parse(typeof(EUIPageID),"ShrineMain"));
            if(!(bool)Call(pages,"PageGraphRawIsUiPageShowIng",id))return false;
            var page=Call(pages,"FindUIPage",id);if(page==null)return false;
            if(!(bool)Call(page,"IsCanInputUnderToping"))return false;
            Call(page,"OnUIActionBack");Log.Write("shrine","leaving native shrine menu through original Return action");return true;
        }
        private static object Member(object target,string name)
        {
            var type=target.GetType();var property=type.GetProperty(name,Any);
            if(property!=null&&property.GetIndexParameters().Length==0)return property.GetValue(target,null);
            var field=type.GetField(name,Any);if(field!=null)return field.GetValue(target);
            foreach(var contract in type.GetInterfaces()){
                var member=contract.GetProperty(name);if(member!=null)return member.GetValue(target,null);
            }
            throw new MissingMemberException(type.FullName,name);
        }
        private static object Call(object target,string name,params object[] args)
        {
            var types=new Type[args.Length];for(int i=0;i<types.Length;i++)types[i]=args[i].GetType();
            var method=target.GetType().GetMethod(name,Any,null,types,null);
            if(method==null)throw new MissingMethodException(target.GetType().FullName,name);
            return Invoke(method,target,args);
        }
        private static object Invoke(MethodInfo method,object target,object[] args)
        {try{return method.Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    }
}
