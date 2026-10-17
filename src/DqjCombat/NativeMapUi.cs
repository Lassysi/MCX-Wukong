using System;
using System.Reflection;

namespace DqjCombat
{
    // Use the same native callback as the game's map button. Native pages own
    // destinations, cursor, confirmation and teleport; no copied shrine list.
    internal sealed class NativeMapUi
    {
        private static readonly BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private readonly Func<object> getPages;
        private readonly int battleId,roleId;
        private readonly int[] watchedPages;
        private object pages;
        private MethodInfo visible;
        private bool sawPage,chapterTransition;
        private double started;
        internal bool Active {get;private set;}
        internal bool LoadingObserved {get;private set;}
        internal bool ChapterTransition {get{return chapterTransition;}}
        internal NativeMapUi():this(()=>B1UI.GSG.GSPageOP,
            Convert.ToInt32(Enum.Parse(typeof(b1.EUIPageID),"BattleMainCon")),
            Convert.ToInt32(Enum.Parse(typeof(b1.EUIPageID),"RoleMain")),MenuPages()){}
        internal NativeMapUi(Func<object> getPages,int battleId,int roleId,params int[] watchedPages)
        {this.getPages=getPages;this.battleId=battleId;this.roleId=roleId;this.watchedPages=watchedPages;}
        private static readonly string[] ChapterPageNames={"ChapterMovie","ChapterAward","SelectChapter","EndCredits"};
        private static readonly System.Collections.Generic.HashSet<int> ChapterPages=new System.Collections.Generic.HashSet<int>();
        private static int[] MenuPages()
        {
            var result=new System.Collections.Generic.List<int>();var type=typeof(b1.EUIPageID);
            // Preview44: ESC's native button shows EquipMain. Live43 proved at least one
            // other role/tab page reports "showing" during plain gameplay, which held MC
            // authority off forever; only pages the menu button actually shows are watched.
            foreach(string name in new[]{"Setting","SettingInputKeyBoard","SettingScreenDetail","BrightnessSetting","HDRSetting","Confirm","ConfirmThree","ShrineMain","Archives","EquipMain"})
                if(Enum.IsDefined(type,name))result.Add(Convert.ToInt32(Enum.Parse(type,name)));
            // Preview72 (user item 3): chapter end. After a chapter Boss the game shows the chapter
            // movie, the chapter award page (next chapter button) and chapter select; native UI owns
            // them, then the next chapter loads and MC reattaches there like after map travel.
            foreach(string name in ChapterPageNames)
                if(Enum.IsDefined(type,name)){int id=Convert.ToInt32(Enum.Parse(type,name));result.Add(id);ChapterPages.Add(id);}
            foreach(string name in Enum.GetNames(type))
                if(name.IndexOf("Map",StringComparison.OrdinalIgnoreCase)>=0&&name!="MapTips"){
                    int id=Convert.ToInt32(Enum.Parse(type,name));if(!result.Contains(id))result.Add(id);
                    Log.Write("native-map-api","watch runtime map page="+name+" id="+id);
                }
            return result.ToArray();
        }
        private object Find(int id)
        {
            var method=pages.GetType().GetMethod("FindUIPage",Any,null,new[]{typeof(int)},null);
            if(method==null)throw new MissingMethodException(pages.GetType().FullName,"FindUIPage(int)");
            return Invoke(method,pages,new object[]{id});
        }
        private void Resolve()
        {
            pages=getPages();if(pages==null)throw new InvalidOperationException("Native UI pages are not ready");
            visible=pages.GetType().GetMethod("PageGraphRawIsUiPageShowIng",Any,null,new[]{typeof(int)},null);
            if(visible==null||visible.ReturnType!=typeof(bool))throw new MissingMethodException(pages.GetType().FullName,"PageGraphRawIsUiPageShowIng(int)");
        }
        private bool Shown(){return ShownPage()!=0;}
        // Id of the first watched page reported as showing, 0 if none.
        private int ShownPage()
        {
            if((bool)Invoke(visible,pages,new object[]{roleId}))return roleId;
            foreach(int id in watchedPages)if((bool)Invoke(visible,pages,new object[]{id}))return id;
            return 0;
        }
        // EUIPageID is byte-backed: IsDefined(type,int) throws (live44 released the
        // native-UI hold while the map/settings page was still open).
        private static string PageName(int id)
        {
            try{var value=Enum.ToObject(typeof(b1.EUIPageID),id);return Enum.IsDefined(typeof(b1.EUIPageID),value)?value.ToString():id.ToString();}
            catch(Exception){return id.ToString();}
        }
        private void ObserveChapter(int id)
        {
            string name=PageName(id);
            if(name!="ChapterMovie"&&name!="ChapterAward")return;
            if(!chapterTransition)Log.Write("travel","chapter transition owns native control until loading or a new world/player session");
            chapterTransition=true;
        }
        internal void Open(double now,Action suspend){openTimeout=5;Open(now,suspend,"OnClickOpenMapUI","F6");}
        // Preview92 (live: Esc in a Boss fight showed the Wukong view for 5 s): the equip menu is refused in
        // combat; a menu that is allowed shows within a few frames, so ESC waits 0.6 s (F6 map: 5 s, it may load).
        private double openTimeout=5;
        // ESC: OnClickOpenEquipUI is the game's own menu button (RoleMain/EquipMain,
        // or Setting when the game shows settings only).
        internal void OpenMenu(double now,Action suspend){openTimeout=.6;Open(now,suspend,"OnClickOpenEquipUI","ESC");}
        private void Open(double now,Action suspend,string callbackName,string key)
        {
            if(Active)return;
            Resolve();
            var page=Find(battleId);if(page==null)throw new InvalidOperationException("Native battle menu buttons are not ready");
            var callback=page.GetType().GetMethod(callbackName,Any,null,Type.EmptyTypes,null);
            if(callback==null||callback.ReturnType!=typeof(void))throw new MissingMethodException(page.GetType().FullName,callbackName+"()");
            // Resolve the original entry before handing input back to native UI.
            Activate(now,suspend);Invoke(callback,page,new object[0]);
            Log.Write("travel",key+" invoked native button: "+page.GetType().FullName+"."+callback.Name);
        }
        internal bool Adopt(double now,Action suspend)
        {
            if(Active)return true;
            Resolve();int shown=ShownPage();if(shown==0)return false;
            Activate(now,suspend);sawPage=true;
            ObserveChapter(shown);
            if(ChapterPages.Contains(shown)){McWorldLink.LastBossDefeatUtc=DateTime.UtcNow;Log.Write("cutscene","chapter page "+PageName(shown)+" shown; chapter story cutscenes allowed for 180 s");}
            Log.Write("travel","native map/menu opened by game input; page="+PageName(shown)+"; MC overlay and input suspended");return true;
        }
        private void Activate(double now,Action suspend)
        {suspend();Active=true;sawPage=false;LoadingObserved=false;started=now;}
        internal bool Update(double now,bool nativeGameplayReady=true)
        {
            if(!Active)return false;
            int page=ShownPage();bool shown=page!=0;
            if(shown)ObserveChapter(page);
            if(shown&&!sawPage){sawPage=true;Log.Write("travel","native map/menu visible; page="+PageName(page)+"; native pointer and buttons own input");}
            if(shown)return true;
            // ChapterMovie -> ChapterAward has a real gap with an apparently playable old pawn.
            // Do not prepare MC terrain or reposition that pawn while native chapter flow continues.
            if(chapterTransition&&!LoadingObserved)return true;
            // A closing page or native loading FSM may still own pause/movement.
            // The cursor forced by us is deliberately excluded from this gate.
            if(sawPage&&!nativeGameplayReady)return true;
            if(!sawPage&&!LoadingObserved&&now-started<openTimeout)return true;
            Log.Write("travel",sawPage?"native map/menu closed; MC may reattach when gameplay is ready"
                :LoadingObserved?"native map loading observed; MC may reattach at destination":"native menu/map refused (not shown within "+openTimeout+" s, e.g. in combat); MC resumes at once");
            Clear();return false;
        }
        internal void Loading(){if(Active)LoadingObserved=true;}
        internal void Clear(){Active=false;sawPage=false;chapterTransition=false;LoadingObserved=false;pages=null;visible=null;}
        private static object Invoke(MethodInfo method,object target,object[] args)
        {try{return method.Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    }
}
