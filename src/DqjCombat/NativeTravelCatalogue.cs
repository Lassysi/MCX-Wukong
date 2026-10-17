using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using b1;

namespace DqjCombat
{
    // SDK method signatures differ from the installed game's UI assembly. Keep
    // all calls on this boundary reflective so a missing SDK MemberRef cannot
    // prevent the native controller-data fallback from executing during JIT.
    internal static class NativeTravelCatalogue
    {
        private static readonly BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static bool described;

        internal static List<int> Read(BGUPlayerCharacterCS player)
        {
            var serviceType=typeof(B1UI.GSSvc.B1BattleLogicSvc);
            if(!described){
                described=true;
                Log.Write("travel-api","runtime UI assembly="+serviceType.Assembly.FullName+" mvid="+serviceType.Module.ModuleVersionId);
                Describe(serviceType,"Teleport");
                Describe(typeof(b1.UI.DS_IBPC_RebirthPointData),"RebirthPoints");
            }
            var service=Member(typeof(B1UI.GSG),null,"BattleLogicSvc");
            MethodInfo catalogue=null;
            if(service!=null)foreach(var method in service.GetType().GetMethods(Any)){
                if(method.Name!="GetCanTeleportRebirthPointList"||method.IsStatic)continue;
                bool optional=true;foreach(var argument in method.GetParameters())if(!argument.IsOptional){optional=false;break;}
                if(!optional)continue;
                if(catalogue!=null)throw new InvalidOperationException("Ambiguous native travel catalogue signature");
                catalogue=method;
            }
            if(catalogue!=null){
                var parameters=catalogue.GetParameters();var args=new object[parameters.Length];
                for(int i=0;i<args.Length;i++)args[i]=Type.Missing;
                Log.Write("travel-api","calling runtime catalogue "+catalogue);
                return Ids(Invoke(catalogue,service,args));
            }

            var data=ControllerData(player,typeof(b1.UI.DS_IBPC_RebirthPointData));
            if(data==null)throw new InvalidOperationException("Native rebirth point data is not ready");
            var active=ReadIds(data,"GetActivedRebirthPoints");
            var blocked=new HashSet<int>(ReadIds(data,"GetBlockedRebirthPoints"));
            var seen=new HashSet<int>();var result=new List<int>();
            // AhudDump confirms the original predicate exactly: descriptor !=
            // null && CanTeleport == EGSYesNo.Yes (1). Preserve that filter.
            foreach(int id in active){
                if(blocked.Contains(id)||!seen.Add(id))continue;
                var desc=BGW_GameDB.GetRebirthPointDesc(id);
                if(desc!=null&&Convert.ToInt32(Member(desc.GetType(),desc,"CanTeleport"))==1)result.Add(id);
            }
            Log.Write("travel","catalogue from native controller data; active="+active.Count+" blocked="+blocked.Count+" unlocked="+result.Count);
            return result;
        }
        internal static object ControllerData(BGUPlayerCharacterCS player,Type dataType)
        {return ActorData(player,dataType,"PlayerController");}

        internal static object ActorData(BGUPlayerCharacterCS player,Type dataType,string actorTag)
        {
            var managerType=typeof(b1.UI.B1GSUIActorMgr);
            var get=managerType.GetMethod("Get",Any,null,new[]{typeof(UnrealEngine.Runtime.UObject)},null);
            if(get==null||!get.IsStatic)throw new MissingMethodException(managerType.FullName,"Get(UObject)");
            var manager=Invoke(get,null,new object[]{player});
            if(manager==null)throw new InvalidOperationException("Native UI actor data manager is not ready");
            MethodInfo read=null;
            foreach(var method in manager.GetType().GetMethods(Any)){
                var args=method.GetParameters();
                if(method.Name!="GetBattleData"||!method.IsGenericMethodDefinition||method.GetGenericArguments().Length!=1||args.Length!=2
                    ||args[0].ParameterType.FullName!="b1.UI.B1ActorTag"||args[1].ParameterType!=typeof(bool))continue;
                if(read!=null)throw new InvalidOperationException("Ambiguous native controller data signature");
                read=method;
            }
            if(read==null)throw new MissingMethodException(manager.GetType().FullName,"GetBattleData<T>(B1ActorTag,bool)");
            var tag=Enum.Parse(read.GetParameters()[0].ParameterType,actorTag);
            return Invoke(read.MakeGenericMethod(dataType),manager,new[]{tag,(object)false});
        }

        private static List<int> ReadIds(object data,string name)
        {
            MethodInfo selected=null;
            foreach(var method in data.GetType().GetMethods(Any)){
                if(method.Name!=name||method.IsStatic)continue;
                var args=method.GetParameters();
                if(args.Length!=0&&(args.Length!=1||!args[0].ParameterType.IsByRef
                    ||!typeof(IEnumerable).IsAssignableFrom(args[0].ParameterType.GetElementType())))continue;
                if(selected!=null)throw new InvalidOperationException("Ambiguous native rebirth list signature: "+name);
                selected=method;
            }
            if(selected==null)throw new MissingMethodException(data.GetType().FullName,name);
            var values=new object[selected.GetParameters().Length];var returned=Invoke(selected,data,values);
            return Ids(values.Length==0?returned:values[0]);
        }
        private static List<int> Ids(object values)
        {
            var source=values as IEnumerable;if(source==null)throw new InvalidOperationException("Native travel list is not enumerable");
            var result=new List<int>();foreach(var value in source){
                if(!(value is int))throw new InvalidOperationException("Native travel list contains a non-integer ID");
                if(result.Count>=512)throw new InvalidOperationException("Native travel list exceeds limit");
                result.Add((int)value);
            }
            return result;
        }
        private static object Member(Type type,object target,string name)
        {
            var field=type.GetField(name,Any);if(field!=null)return field.GetValue(target);
            var property=type.GetProperty(name,Any);if(property!=null&&property.GetIndexParameters().Length==0)return property.GetValue(target,null);
            throw new MissingMemberException(type.FullName,name);
        }
        private static object Invoke(MethodInfo method,object target,object[] args)
        {try{return method.Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
        private static void Describe(Type type,string keyword)
        {foreach(var method in type.GetMethods(Any))if(method.Name.IndexOf(keyword,StringComparison.OrdinalIgnoreCase)>=0)Log.Write("travel-api",type.FullName+" :: "+method);}
    }
}
