using System;
using System.Collections;
using System.Reflection;

namespace DqjCombat
{
    // Generated engine SDKs differ from the installed game's bindings. No MemberRef to the changed signatures.
    internal static class NativeAppearanceApi
    {
        private static readonly BindingFlags Any=BindingFlags.Instance|BindingFlags.Public;
        internal static IEnumerable AttachedActors(object owner)
        {
            MethodInfo selected=null;
            foreach(var method in owner.GetType().GetMethods(Any)){
                var p=method.GetParameters();
                if(method.Name!="GetAttachedActors"||p.Length<2||p.Length>3||!p[0].ParameterType.IsByRef
                    ||!typeof(IEnumerable).IsAssignableFrom(p[0].ParameterType.GetElementType())||p[1].ParameterType!=typeof(bool)
                    ||p.Length==3&&p[2].ParameterType!=typeof(bool))continue;
                if(selected==null||p.Length>selected.GetParameters().Length)selected=method;
            }
            if(selected==null)throw new MissingMethodException(owner.GetType().FullName,"GetAttachedActors(out actors,bool[,bool])");
            object[] args=selected.GetParameters().Length==3?new object[]{null,true,false}:new object[]{null,true};
            Invoke(selected,owner,args);return args[0] as IEnumerable;
        }
        internal static string Socket(object owner,string methodName)
        {
            if(owner==null)return "";
            var method=owner.GetType().GetMethod(methodName,Any,null,Type.EmptyTypes,null);
            return method==null?"":(Invoke(method,owner,new object[0])??"").ToString();
        }
        private static object Invoke(MethodInfo method,object owner,object[] args)
        {try{return method.Invoke(owner,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    }
}
