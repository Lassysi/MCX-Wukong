using System;
using System.Reflection;

namespace DqjCombat
{
    // Feed missing E edges to the original UICommSkip. Its own timer/icon, native permission check,
    // skip mode and movie completion path remain authoritative. Never simulate completion or force a flag.
    internal sealed class NativeCutsceneInput
    {
        private object forwardedPage;
        private bool latched;
        internal bool Update(object page,bool allowed,bool focused,bool down)
        {
            if(!allowed||!focused||!down||page!=forwardedPage&&forwardedPage!=null){Release();latched=false;}
            if(!allowed||!focused||!down||page==null||latched)return false;
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var press=page.GetType().GetMethod("OnPressSkip",flags,null,Type.EmptyTypes,null);
            var state=page.GetType().GetField("IsPress",flags);
            var release=page.GetType().GetMethod("OnReleasedSkip",flags,null,Type.EmptyTypes,null);
            if(press==null||release==null||state==null||state.FieldType!=typeof(bool))return false;
            latched=true;
            if((bool)state.GetValue(page))return false; // already started by native bindings; do not restart its timer
            Invoke(press,page);
            if((bool)state.GetValue(page))forwardedPage=page;
            return true;
        }
        internal void Release()
        {
            var page=forwardedPage;forwardedPage=null;
            if(page==null)return;
            var release=page.GetType().GetMethod("OnReleasedSkip",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);
            if(release!=null)Invoke(release,page);
        }
        private static void Invoke(MethodInfo method,object page)
        {try{method.Invoke(page,new object[0]);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    }
}
