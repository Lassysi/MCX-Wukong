using System;
using System.Collections.Generic;
using System.Reflection;

namespace DqjCombat
{
    internal static class NativeTravelText
    {
        private static readonly string[] Names={"GetLocaliztionalFText","GSLocalizational","ToFText"};
        private static readonly Dictionary<Type,MethodInfo> Resolved=new Dictionary<Type,MethodInfo>();
        private static readonly BindingFlags Static=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
        internal static string Read(string value){return Read(typeof(b1.Localization.GSLocalization),value);}
        internal static string Read(Type type,string value)
        {
            value=value??"";if(value.Length==0)return "";
            MethodInfo selected;
            if(!Resolved.TryGetValue(type,out selected)){
                foreach(var method in type.GetMethods(Static))
                    if(Array.IndexOf(Names,method.Name)>=0)Log.Write("travel-api",type.FullName+" :: "+method);
                foreach(string name in Names){
                    var candidate=type.GetMethod(name,Static,null,new[]{typeof(string)},null);
                    if(candidate!=null&&!candidate.ContainsGenericParameters&&candidate.ReturnType!=typeof(void)){selected=candidate;break;}
                }
                if(selected==null)throw new MissingMethodException(type.FullName,"Native localization(string)");
                Resolved.Add(type,selected);Log.Write("travel-api","using native localization "+selected);
            }
            object result;try{result=selected.Invoke(null,new object[]{value});}catch(TargetInvocationException e){throw e.InnerException??e;}
            if(result==null)throw new InvalidOperationException("Native localization returned no text");
            return result.ToString();
        }
    }
}
