using System;
using System.Collections;
using System.Reflection;

namespace DqjCombat
{
    // Adapter to the captured native MovieInstance state machine. No Stop, fake OnFinished,
    // timeline-end seek, global skip CVar or save/progression write is used.
    internal sealed class NativeManualMovieSkip
    {
        private object leased;
        private PropertyInfo allowedProperty,typeProperty;
        private bool originalAllowed,changedType,entered;
        private object originalType,requestedType;
        private double requestedAt;
        internal bool Pending {get{return leased!=null;}}
        internal string Update(object current,double now)
        {
            if(leased==null)return null;
            if(!ReferenceEquals(current,leased)){Release();return "native camera movie changed/finished; manual skip lease restored";}
            bool skipping=StateName(leased)=="MovieState_Skipping";
            if(skipping&&!entered){entered=true;return "native Skipping state acknowledged";}
            if(!skipping&&(entered||now-requestedAt>=1)){
                bool completed=entered;Release();
                return completed?"native Skipping state exited; manual skip lease restored":"native skip entry not acknowledged; manual skip lease restored";
            }
            return null;
        }
        internal bool Request(object movie,double now,out string detail)
        {
            detail="";
            if(movie==null||Pending){detail="no camera movie or native skip already pending";return false;}
            var state=Read(movie,"CurrentState");
            var transitions=Read(state,"TransitionMap") as IDictionary;
            bool edge=false;
            if(transitions!=null)foreach(DictionaryEntry entry in transitions)
                if(Convert.ToInt32(entry.Key)==10&&string.Equals(Convert.ToString(entry.Value),"Skipping",StringComparison.Ordinal))edge=true;
            if(!edge){detail="current native state has no Skip -> Skipping transition";return false;}
            var ready=Method(movie,"IsLoadFinished");
            if(ready==null||!Equals(Invoke(ready,movie),true)){detail="native movie resources not ready";return false;}
            if(!Equals(Read(movie,"bDisableCameraCut"),false)||Read(movie,"SequencePlayer")==null){detail="camera movie is not in a supported sequence phase";return false;}
            var allowed=Property(movie,"bCanSkip");var type=Property(movie,"SkipType");var skip=Method(movie,"SkipMovie");
            if(allowed==null||allowed.GetSetMethod(true)==null||type==null||type.GetSetMethod(true)==null||skip==null){detail="native skip contract unavailable";return false;}
            int mode=Convert.ToInt32(type.GetValue(movie,null));
            if(mode<0||mode>2){detail="unknown native skip mode="+mode;return false;}
            originalAllowed=(bool)allowed.GetValue(movie,null);originalType=type.GetValue(movie,null);
            // Manual fallback always plays native event tracks through FastForward, including a
            // transformation's ordinary completion. Native UI skips keep their own configured mode.
            requestedType=Enum.ToObject(type.PropertyType,1);
            leased=movie;allowedProperty=allowed;typeProperty=type;changedType=mode!=1;requestedAt=now;entered=false;
            try{
                // Native Skipping.Enter requires this local movie flag even when the user requests a skip.
                allowed.SetValue(movie,true,null);
                // A descriptor with None has no skipping implementation. Use the engine's event-playing
                // FastForward branch rather than jumping past quest/transform tracks.
                if(changedType)type.SetValue(movie,requestedType,null);
                Invoke(skip,movie);
                detail="native SkipMovie event; originalMode="+mode+" effectiveMode="+Convert.ToInt32(requestedType)+" state="+StateName(movie);
                return true;
            }catch{Release();throw;}
        }
        internal void Release()
        {
            var movie=leased;leased=null;
            if(movie==null)return;
            try{
                if(Equals(allowedProperty.GetValue(movie,null),true))allowedProperty.SetValue(movie,originalAllowed,null);
            }finally{
                try{if(changedType&&Equals(typeProperty.GetValue(movie,null),requestedType))typeProperty.SetValue(movie,originalType,null);}
                finally{allowedProperty=null;typeProperty=null;originalType=null;requestedType=null;changedType=false;entered=false;}
            }
        }
        internal static string StateName(object movie)
        {var state=Read(movie,"CurrentState");return state==null?"none":state.GetType().Name;}
        private static object Read(object owner,string name)
        {var p=Property(owner,name);return p==null?null:p.GetValue(owner,null);}
        private static PropertyInfo Property(object owner,string name)
        {
            for(var type=owner==null?null:owner.GetType();type!=null;type=type.BaseType){
                var p=type.GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
                if(p!=null)return p;
            }
            return null;
        }
        private static MethodInfo Method(object owner,string name)
        {
            for(var type=owner==null?null:owner.GetType();type!=null;type=type.BaseType){
                var m=type.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly,null,Type.EmptyTypes,null);
                if(m!=null)return m;
            }
            return null;
        }
        private static object Invoke(MethodInfo method,object owner)
        {try{return method.Invoke(owner,new object[0]);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    }
}
