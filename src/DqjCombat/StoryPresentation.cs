using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using b1;
using UnrealEngine.Engine;
using UnrealEngine.InputCore;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // World-scoped presentation must keep running when story possession switches to a cicada,
    // DefaultEmptyPawn or no pawn. It never owns locomotion, progression or combat state.
    internal sealed class StoryPresentation
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        private readonly NativeCutsceneInput cutsceneInput=new NativeCutsceneInput();
        private readonly ManualSkipHold manualHold=new ManualSkipHold();
        private readonly NativeManualMovieSkip manualSkip=new NativeManualMovieSkip();
        private double nextSkipInputError;
        private object loggedMovie;
        private bool loggedCanSkip,lastPhysicalE;
        private FKey skipKey;
        private bool keyReady;
        private UWorld previousWorld;
        internal void Update(double now,UWorld world,APlayerController controller,AActor primaryOwner,bool interaction)
        {
            if(!NativeObjectLifetime.Live(world)||!NativeObjectLifetime.Live(controller)){Clear();return;}
            if(previousWorld!=world){Clear();previousWorld=world;}
            bool canSkip,movie=GameApi.MoviePlaying(world,out canSkip);
            UpdateCutsceneSkins(now,world,primaryOwner,movie,PlayerFormPolicy.Enlarge(movie,interaction));
            UpdateCutsceneInput(now,world,controller,movie,canSkip);
        }
        internal void Clear()
        {
            try{cutsceneInput.Release();}catch{}
            try{manualSkip.Release();}catch{}
            manualHold.Clear();loggedMovie=null;lastPhysicalE=false;
            foreach(var skin in cutsceneSkins)skin.Dispose();cutsceneSkins.Clear();
            nativeCutsceneActors.Clear();loggedStoryActors.Clear();nextCutsceneScan=0;
            previousWorld=null;
        }
        internal void ReleaseOwner(AActor owner)
        {
            for(int i=cutsceneSkins.Count-1;i>=0;i--)if(cutsceneSkins[i].Owner==owner){cutsceneSkins[i].Dispose();cutsceneSkins.RemoveAt(i);}
        }
        private readonly System.Collections.Generic.List<SteveSkin> cutsceneSkins=new System.Collections.Generic.List<SteveSkin>();
        private readonly HashSet<string> nativeCutsceneActors=new HashSet<string>();
        private double nextCutsceneScan;
        private readonly HashSet<string> loggedStoryActors=new HashSet<string>();
        private void UpdateCutsceneSkins(double now,UWorld world,AActor primaryOwner,bool movie,bool enlarge)
        {
            if(!movie){
                if(cutsceneSkins.Count>0){foreach(var skin in cutsceneSkins)skin.Dispose();cutsceneSkins.Clear();Log.Write("steve-skin","cutscene skins removed");}
                nativeCutsceneActors.Clear();loggedStoryActors.Clear();nextCutsceneScan=0;rejectedLogged=0;
                return;
            }
            for(int i=cutsceneSkins.Count-1;i>=0;i--){var skin=cutsceneSkins[i];if(!skin.Live){skin.Dispose();cutsceneSkins.RemoveAt(i);}else{skin.SetCinematic(enlarge);skin.SetNativeView(true);skin.Update();}}
            if(now<nextCutsceneScan)return;nextCutsceneScan=now+.5;
            try{
                var boundHeroes=new HashSet<AActor>();
                // Native sequence role bindings cover generic cinematic actors without broad NPC replacement.
                var movieData=BGU_DataUtil.GetGameStateReadonlyData<IBGC_MovieData,BGC_MovieData>(world) as BGC_MovieData;
                var instance=movieData==null?null:movieData.CameraMovieInstance;
                try{if(instance!=null){
                    var property=instance.GetType().GetProperty("Performers",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
                    var performers=property==null?null:property.GetValue(instance,null) as System.Collections.IDictionary;
                    if(performers!=null)foreach(System.Collections.DictionaryEntry entry in performers){
                        var actor=entry.Value as AActor;
                        if(actor!=null&&CutsceneSkinScope.IsHeroBinding(Convert.ToString(entry.Key)))boundHeroes.Add(actor);
                    }
                }}catch(Exception e){if(loggedStoryActors.Add("role-binding-error"))Log.Write("cg-skin-role","native role bindings unavailable; explicit hero class policy retained: "+e.Message);}
                var candidates=new Dictionary<AActor,USkeletalMeshComponent>();
                var heroCharacter=primaryOwner as ACharacter;
                var heroComponent=NativeObjectLifetime.Live(heroCharacter)?heroCharacter.Mesh:null;
                var heroAsset=NativeObjectLifetime.Live(heroComponent)?heroComponent.SkeletalMesh:null;
                var heroSkeleton=heroAsset==null?null:heroAsset.Skeleton;
                foreach(USkeletalMeshComponent component in UObject.GetObjects<USkeletalMeshComponent>()){
                    if(!NativeObjectLifetime.Live(component))continue;
                    var actor=component.GetOwner();if(!NativeObjectLifetime.Live(actor)||actor==primaryOwner)continue;
                    string actorName=actor.GetName(),actorClass=actor.GetClass().GetName();bool sameWorld=NativeObjectLifetime.ActorWorld(actor)==world;
                    if(nativeCutsceneActors.Contains(actorName))continue; // one failed construction attempt per CG
                    // Preview102 (user: CGs still show the Wukong model): sequencer copies of the hero are often plain
                    // skeletal-mesh actors with no hero class name. Wukong's own mesh asset or skeleton marks them too.
                    bool heroMesh=sameWorld&&IsHeroMesh(component,heroAsset,heroSkeleton);
                    if(!CutsceneSkinScope.CanReplaceStandIn(actor is BGUPlayerCharacterCS||boundHeroes.Contains(actor)||heroMesh,actorName,actorClass,sameWorld)){
                        if(sameWorld&&rejectedLogged<12&&component.GetNumBones()>=60&&!component.HiddenInGame&&loggedStoryActors.Add("rejected:"+actorName)){
                            rejectedLogged++;Log.Write("cg-skin-role","not replaced: "+actorName+" class="+actorClass+" mesh="+(component.SkeletalMesh==null?"none":component.SkeletalMesh.GetPathName())+" bones="+component.GetNumBones());}
                        continue;
                    }
                    if(loggedStoryActors.Add(actorName))Log.Write("cg-skin-role","candidate="+actorName+" class="+actorClass+" boundHero="+boundHeroes.Contains(actor));
                    // HQ/story and transformed-player CGs need not use the controlled pawn's skeleton asset.
                    // Hero ownership plus the actual required humanoid joint contract establishes eligibility.
                    if(component.SkeletalMesh==null||!SteveSkin.SupportsMesh(component))continue;
                    bool known=false;foreach(var skin in cutsceneSkins)if(skin.Owner==actor){known=true;break;}if(known)continue;
                    USkeletalMeshComponent previous;
                    if(!candidates.TryGetValue(actor,out previous)||component.GetNumBones()>previous.GetNumBones())candidates[actor]=component;
                }
                foreach(var pair in candidates){
                    if(cutsceneSkins.Count>=6)break;
                    try{var skin=new SteveSkin(pair.Key,pair.Value,System.IO.Path.Combine(Log.Dir,"steve.mesh"),true,enlarge);
                        cutsceneSkins.Add(skin);skin.SetNativeView(true);skin.Reveal(true);
                        Log.Write("cg-skin","CG hero replaced once: "+pair.Key.GetName()+" ("+pair.Key.GetClass().GetName()+") bones="+pair.Value.GetNumBones()+"; multipart native body hidden; props remain native");}
                    catch(Exception e){if(nativeCutsceneActors.Add(pair.Key.GetName()))Log.Write("cg-skin-error","CG hero kept native: "+pair.Key.GetName()+" "+e.Message);}
                }
            }catch(Exception e){nextCutsceneScan=now+5;Log.Write("steve-skin-error","cutscene scan: "+e.GetType().Name+": "+e.Message);}
        }
        private int rejectedLogged;
        private static bool IsHeroMesh(USkeletalMeshComponent component,USkeletalMesh heroAsset,USkeleton heroSkeleton)
        {
            var asset=component.SkeletalMesh;if(asset==null)return false;
            if(heroAsset!=null&&(asset==heroAsset||heroSkeleton!=null&&asset.Skeleton==heroSkeleton))return true;
            string path=asset.GetPathName()??"";
            return path.IndexOf("wukong",StringComparison.OrdinalIgnoreCase)>=0&&path.IndexOf("Preview",StringComparison.OrdinalIgnoreCase)<0;
        }
        private void UpdateCutsceneInput(double now,UWorld world,APlayerController controller,bool movie,bool canSkip)
        {
            try{
                uint process;GetWindowThreadProcessId(GetForegroundWindow(),out process);
                bool focused=process==(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                if(!keyReady){skipKey=UGSE_UMGFuncLib.GetKeyWithName(new FName("E"));keyReady=true;}
                // UI input modes may consume E before PlayerController records its state (Live100).
                // Read the physical key only while our game owns foreground focus.
                bool held=focused&&(GetAsyncKeyState(0x45)&0x8000)!=0;
                var data=movie?BGU_DataUtil.GetGameStateReadonlyData<IBGC_MovieData,BGC_MovieData>(world) as BGC_MovieData:null;
                var instance=data==null?null:data.CameraMovieInstance;
                string skipState=manualSkip.Update(instance,now);
                if(skipState!=null)Log.Write("cutscene-input",skipState);
                if(movie&&(!ReferenceEquals(loggedMovie,instance)||loggedCanSkip!=canSkip)){
                    loggedMovie=instance;loggedCanSkip=canSkip;
                    Log.Write("cutscene-input","camera movie="+(instance==null?0:instance.MovieId)+" nativeCanSkip="+canSkip+" state="+NativeManualMovieSkip.StateName(instance));
                }
                if(!movie)loggedMovie=null;
                var pages=B1UI.GSG.GSPageOP;
                var page=pages==null?null:pages.FindUIPage((int)EUIPageID.CommSkip);
                if(movie&&canSkip&&held&&!SequenceSkipPage(page)){
                    B1UI.GSUI.GSB1UIUtil.ActiveCommSkip(true,B1UI.GSUI.ESkipType.Sequence,false);
                    page=pages==null?null:pages.FindUIPage((int)EUIPageID.CommSkip);
                }
                if(cutsceneInput.Update(page,movie&&canSkip,focused,held))
                    Log.Write("cutscene-input","held E forwarded to native CommSkip; native hold timer and skip permission retained");
                if(movie&&held&&!lastPhysicalE)Log.Write("cutscene-input","physical E press observed; controllerDown="+controller.IsInputKeyDown(skipKey)+" nativeCanSkip="+canSkip);
                lastPhysicalE=held;
                if(manualHold.Update(instance,instance==null?0:instance.MovieId,focused,held,now,NativeHoldSeconds(page))){
                    string detail;
                    bool requested=manualSkip.Request(instance,now,out detail);
                    Log.Write("cutscene-input",(requested?"manual native fast-forward requested; ":"manual hold not executed; ")+detail);
                }
            }catch(Exception e){if(now>=nextSkipInputError){nextSkipInputError=now+5;Log.Write("cutscene-input-error",e.GetType().Name+": "+e.Message);}}
        }
        private static double NativeHoldSeconds(object page)
        {
            if(page!=null){
                var field=page.GetType().GetField("PressTime",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
                if(field!=null){double value=Convert.ToDouble(field.GetValue(page));if(value>=.2&&value<=5)return value;}
            }
            return 1.5;
        }
        private static bool SequenceSkipPage(object page)
        {
            if(page==null)return false;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var field=page.GetType().GetField("CommSkipData",flags);var data=field==null?null:field.GetValue(page);
            if(data==null)return false;
            var modeField=data.GetType().GetField("SkipType",flags);var mode=modeField==null?null:modeField.GetValue(data);
            var value=mode==null?null:mode.GetType().GetProperty("Value",flags);
            return value!=null&&Convert.ToInt32(value.GetValue(mode,null))==1;
        }
    }
}
