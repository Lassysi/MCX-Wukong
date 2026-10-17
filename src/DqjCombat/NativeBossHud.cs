using System;
using System.Collections.Generic;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;
using UnrealEngine.UMG;

namespace DqjCombat
{
    // Move the original boss widgets, retaining native HP, names and animations.
    // A menu handoff or a removed widget restores its previous layout.
    //
    // Preview83 (live82: the bar still sat under the MC HUD; the three leased BI_BossBar widgets
    // never reported an on-screen size, so nothing moved): every visible strip inside the native
    // BloodBarList tree is measured, and the root panel of that tree is translated so the top
    // strip sits at 6 % of the screen height. Child geometry includes the root's translation, so
    // measuring after a move converges; steps are rate-limited so stale geometry cannot double them.
    // If no strip can be measured while boss bars exist, the visible small widgets in the lower half
    // of the screen are logged once (boss-hud-probe) to identify the real bar widget.
    internal sealed class NativeBossHud : IDisposable
    {
        private sealed class Lease
        {
            internal UUserWidget Widget;
            internal FVector2D Original;
        }
        private readonly List<Lease> bars=new List<Lease>();
        private readonly List<UUserWidget> strips=new List<UUserWidget>();
        private UPanelWidget root;private string rootPath;private FVector2D rootOriginal;
        private int moves,lastMeasured=-1;
        private double nextMove,firstUnmeasured=-1;private bool probed;
        private readonly HashSet<string> observed=new HashSet<string>();
        private double nextLookup,nextLog,nextRejectReset;private int loggedOwned=-1;
        private readonly HashSet<UUserWidget> rejected=new HashSet<UUserWidget>();
        internal void Update(double now,bool active,APlayerController controller)
        {
            if(!active||controller==null){Dispose();return;}
            try{
                var viewGeo=UWidgetLayoutLibrary.GetViewportWidgetGeometry(controller);var view=USlateLibrary.GetLocalSize(viewGeo);
                if(now>=nextLookup){
                    nextLookup=now+1;
                    // Preview79 (stutter): each widget's class is read once; non-bar widgets are skipped
                    // without native calls on later scans. The set is rebuilt every 30 s (addresses recycle).
                    if(now>=nextRejectReset){nextRejectReset=now+30;rejected.Clear();}
                    foreach(UUserWidget widget in UObject.GetObjects<UUserWidget>()){
                        if(widget==null||rejected.Contains(widget)||widget.IsDestroyed||widget.IsPendingKill)continue;
                        string path=widget.GetClass().GetPathName();
                        bool bossClass=path.IndexOf("Boss",StringComparison.OrdinalIgnoreCase)>=0&&
                            (path.IndexOf("Bar",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("Blood",StringComparison.OrdinalIgnoreCase)>=0);
                        string identity=widget.GetPathName();
                        bool inBarTree=identity.IndexOf("BloodBarList",StringComparison.OrdinalIgnoreCase)>=0;
                        if(!bossClass&&!inBarTree){rejected.Add(widget);continue;}
                        if(widget.GetOwningPlayer()!=controller)continue;
                        // List containers and full-screen roots are never strips themselves.
                        if(path.IndexOf("List",StringComparison.OrdinalIgnoreCase)>=0)continue;
                        if(inBarTree&&!strips.Contains(widget)){strips.Add(widget);if(observed.Add("strip:"+identity))Log.Write("boss-hud","bar-tree strip="+identity+" class="+path);}
                        if(!bossClass)continue;
                        bool existing=false;foreach(var b in bars)if(b.Widget==widget){existing=true;break;}if(existing)continue;
                        var size=USlateLibrary.GetLocalSize(widget.GetCachedGeometry());
                        if(observed.Add(identity))Log.Write("boss-hud","candidate="+identity+" size="+size+" viewport="+view);
                        if(size.Y>0&&view.Y>0&&size.Y>view.Y*.4)continue;
                        bars.Add(new Lease{Widget=widget,Original=widget.RenderTransform.Translation});
                        Log.Write("boss-hud","native boss bar acquired="+identity);
                    }
                    if(bars.Count!=loggedOwned){loggedOwned=bars.Count;Log.Write("boss-hud","native boss widgets owned="+bars.Count+" bar-tree strips="+strips.Count);}
                }
                if(now<nextMove||view.Y<=0)return;
                nextMove=now+.1;
                double top=double.MaxValue;UUserWidget first=null;int measured=0;
                foreach(var strip in strips){
                    if(!Shown(strip))continue;
                    var geo=strip.GetCachedGeometry();var size=USlateLibrary.GetLocalSize(geo);
                    if(size.X<=0||size.Y<=0)continue;
                    var a=USlateLibrary.AbsoluteToLocal(viewGeo,USlateLibrary.LocalToAbsolute(geo,default(FVector2D)));
                    var b=USlateLibrary.AbsoluteToLocal(viewGeo,USlateLibrary.LocalToAbsolute(geo,size));
                    if(b.Y-a.Y<=0||b.Y-a.Y>view.Y*.12)continue;
                    measured++;if(a.Y<top){top=a.Y;first=strip;}
                }
                if(measured!=lastMeasured){lastMeasured=measured;Log.Write("boss-hud","visible bar strips="+measured);}
                if(first==null){
                    if(bars.Count>0&&!probed){
                        if(firstUnmeasured<0)firstUnmeasured=now;
                        else if(now-firstUnmeasured>5){probed=true;Probe(viewGeo,view,controller);}
                    }
                    return;
                }
                firstUnmeasured=-1;
                if(root==null||!Alive(root)){
                    UPanelWidget p=first.GetParent(),last=null;
                    while(Alive(p)){last=p;p=p.GetParent();}
                    if(last==null)return;
                    root=last;rootPath=last.GetPathName();rootOriginal=last.RenderTransform.Translation;moves=0;
                    Log.Write("boss-hud","bar-tree root leased="+rootPath+" original translation="+rootOriginal);
                }
                double wantedY=view.Y*.06;
                if(Math.Abs(top-wantedY)<=view.Y*.004)return;
                var rootGeo=root.GetCachedGeometry();
                var from=USlateLibrary.AbsoluteToLocal(rootGeo,USlateLibrary.LocalToAbsolute(viewGeo,new FVector2D(view.X*.5,top)));
                var to=USlateLibrary.AbsoluteToLocal(rootGeo,USlateLibrary.LocalToAbsolute(viewGeo,new FVector2D(view.X*.5,wantedY)));
                root.SetRenderTranslation(root.RenderTransform.Translation+new FVector2D(0,to.Y-from.Y));
                moves++;
                if(moves==1||moves%50==0)Log.Write("boss-hud","moved bar-tree root top="+top+" -> "+wantedY+" of "+view.Y+" moves="+moves+" strip="+first.GetPathName());
            }catch(Exception e){if(now>=nextLog){nextLog=now+5;Log.Write("boss-hud-error",e.ToString());}}
        }
        // One-time read-only scan: small visible widgets in the lower half of the screen.
        private static void Probe(UnrealEngine.SlateCore.FGeometry viewGeo,FVector2D view,APlayerController controller){
            int n=0;
            foreach(UUserWidget w in UObject.GetObjects<UUserWidget>()){
                if(n>=25)break;
                if(!Shown(w)||w.GetOwningPlayer()!=controller)continue;
                var geo=w.GetCachedGeometry();var size=USlateLibrary.GetLocalSize(geo);if(size.X<=0||size.Y<=0)continue;
                var a=USlateLibrary.AbsoluteToLocal(viewGeo,USlateLibrary.LocalToAbsolute(geo,default(FVector2D)));
                var b=USlateLibrary.AbsoluteToLocal(viewGeo,USlateLibrary.LocalToAbsolute(geo,size));
                double h=b.Y-a.Y,wd=b.X-a.X;
                if(h<=0||h>view.Y*.12||a.Y<view.Y*.5||wd<view.X*.1)continue;
                n++;Log.Write("boss-hud-probe","widget="+w.GetPathName()+" class="+w.GetClass().GetPathName()+" rect="+a+" .. "+b);
            }
            Log.Write("boss-hud-probe","done; "+n+" wide strips in the lower half of the screen");
        }
        private static bool Alive(UWidget w){return w!=null&&!w.IsDestroyed&&!w.IsPendingKill;}
        private static bool Shown(UWidget w){
            if(!Alive(w))return false;var v=w.GetVisibility();return v!=ESlateVisibility.Collapsed&&v!=ESlateVisibility.Hidden;
        }
        public void Dispose()
        {
            foreach(var lease in bars)try{if(Alive(lease.Widget))lease.Widget.SetRenderTranslation(lease.Original);}catch(Exception e){Log.Write("boss-hud-error","restore: "+e.Message);}
            try{if(Alive(root))root.SetRenderTranslation(rootOriginal);}catch(Exception e){Log.Write("boss-hud-error","restore root: "+e.Message);}
            bars.Clear();strips.Clear();root=null;rootPath=null;nextLookup=0;nextMove=0;firstUnmeasured=-1;probed=false;lastMeasured=-1;loggedOwned=-1;
        }
    }
}
