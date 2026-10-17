using System;
using System.Reflection;

namespace DqjCombat
{
    // The installed game uses a different generated event wrapper from the SDK.
    // Resolve the live wrapper without emitting a reference to its generated type.
    internal static class NativeEventInvoker
    {
        private static readonly BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static object Signal(object collection,string member)
        {
            if(collection==null)return null;
            var type=collection.GetType();var property=type.GetProperty(member,Any);
            if(property!=null)return property.GetValue(collection,null);
            var field=type.GetField(member,Any);return field==null?null:field.GetValue(collection);
        }
        internal static void Invoke(object signal,params object[] args)
        {
            var selected=Resolve(signal,args);
            try{selected.Invoke(signal,args);}catch(TargetInvocationException e){throw e.InnerException??e;}
        }
        internal static string Validate(object signal,params object[] args){return Resolve(signal,args).ToString();}
        private static MethodInfo Resolve(object signal,object[] args)
        {
            if(signal==null)throw new InvalidOperationException("Native event is not ready");
            MethodInfo selected=null;
            foreach(var method in signal.GetType().GetMethods(Any)){
                if(method.Name!="Invoke")continue;var parameters=method.GetParameters();if(parameters.Length!=args.Length)continue;
                bool match=true;for(int i=0;i<parameters.Length;i++){
                    var type=parameters[i].ParameterType;if(type.IsByRef)type=type.GetElementType();
                    if(args[i]==null?type.IsValueType:!type.IsInstanceOfType(args[i])){match=false;break;}
                }
                if(!match)continue;if(selected!=null)throw new InvalidOperationException("Ambiguous native event signature");selected=method;
            }
            if(selected==null)throw new MissingMethodException(signal.GetType().FullName,"Invoke");
            return selected;
        }
    }
}
