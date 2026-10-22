using System;
using System.Collections.Generic;
using DqjCombat;

internal static class Program
{
    private static int checks;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    private static int Main()
    {
        var hold=new ManualSkipHold();var scene=new object();
        Check(!hold.Update(scene,1,true,true,0,1.5)&&!hold.Update(scene,1,true,true,2,1.5),"carried E cannot skip a newly entered movie");
        hold.Update(scene,1,true,false,2.1,1.5);hold.Update(scene,1,true,true,3,1.5);
        Check(!hold.Update(scene,1,true,true,4.49,1.5)&&hold.Update(scene,1,true,true,4.5,1.5),"fresh focused hold triggers after the required duration");
        Check(!hold.Update(scene,1,true,true,9,1.5),"continuing to hold does not repeat the request");
        hold.Update(scene,1,true,false,10,1.5);hold.Update(scene,1,true,true,11,1.5);hold.Update(scene,1,true,false,12,1.5);
        Check(!hold.Update(scene,1,true,true,13,1.5)&&!hold.Update(scene,1,true,true,14,1.5),"release cancels an incomplete hold");
        hold.Update(scene,1,false,true,15,1.5);
        Check(!hold.Update(scene,1,true,true,19,1.5),"focus return with E still held requires release");
        hold.Update(scene,1,true,false,20,1.5);hold.Update(scene,1,true,true,21,1.5);
        Check(!hold.Update(new object(),2,true,true,24,1.5),"a scene change cannot carry completed intent into the next CG");
        hold.Clear();Check(!hold.Update(scene,1,true,true,30,1.5),"world/loading clear drops pending intent");

        var transaction=new NativeManualMovieSkip();string detail;
        var movie=new Movie();
        Check(transaction.Request(movie,1,out detail)&&movie.Requests==1&&movie.SawAllowed&&movie.SawMode==Mode.FastForward,"blocked/default native mode enters the actual SkipMovie event with FastForward");
        Check(transaction.Pending&&movie.Allowed&&movie.Mode==Mode.FastForward,"local flags are leased while native skipping runs");
        Check(!transaction.Request(movie,1.1,out detail)&&movie.Requests==1,"pending native request cannot be repeated");
        transaction.Update(movie,2);Check(movie.Allowed&&movie.Mode==Mode.FastForward,"native skipping Tick retains its required mode");
        movie.Finish();transaction.Update(movie,3);
        Check(!transaction.Pending&&!movie.Allowed&&movie.Mode==Mode.None,"native return restores the original per-movie state");
        transaction.Release();Check(movie.Mode==Mode.None,"lease release is idempotent");

        var position=new Movie{Mode=Mode.SetPosition};transaction.Request(position,4,out detail);
        Check(position.SawMode==Mode.FastForward,"manual fallback plays event tracks instead of seeking past them");
        position.Finish();transaction.Update(position,5);Check(position.Mode==Mode.SetPosition,"configured mode restored after manual event playback");
        var late=new Movie{Deferred=true};transaction.Request(late,6,out detail);transaction.Update(late,6.5);
        Check(transaction.Pending&&late.Allowed,"deferred native transition retains its entry contract");
        late.EnterSkipping();transaction.Update(late,6.6);late.Finish();transaction.Update(late,7);
        Check(!late.Allowed&&late.Mode==Mode.None,"deferred native completion restores the lease");
        var refused=new Movie{Deferred=true};transaction.Request(refused,8,out detail);transaction.Update(refused,9.1);
        Check(!transaction.Pending&&!refused.Allowed&&refused.Mode==Mode.None,"unacknowledged native entry releases after a bounded wait");
        var changed=new Movie();transaction.Request(changed,10,out detail);changed.Mode=Mode.SetPosition;changed.Allowed=false;
        transaction.Update(null,11);Check(changed.Mode==Mode.SetPosition&&!changed.Allowed,"native changes made during the lease are preserved");

        var noEdge=new Movie{HasSkipEdge=false};
        Check(!transaction.Request(noEdge,12,out detail)&&noEdge.Requests==0&&!noEdge.Allowed,"missing native Skip transition causes no mutation");
        var unloaded=new Movie{Loaded=false};Check(!transaction.Request(unloaded,12,out detail)&&unloaded.Requests==0,"unloaded sequence cannot be skipped");
        var disabled=new Movie{CameraDisabled=true};Check(!transaction.Request(disabled,12,out detail),"native camera-disabled phase is preserved");
        var noSequence=new Movie{HasSequence=false};Check(!transaction.Request(noSequence,12,out detail),"missing sequence player is not treated as a camera CG");
        var unknown=new Movie{Mode=(Mode)7};Check(!transaction.Request(unknown,12,out detail)&&unknown.Mode==(Mode)7,"unknown native skip modes are refused");
        var throwing=new Movie{Throw=true};bool threw=false;
        try{transaction.Request(throwing,13,out detail);}catch(InvalidOperationException){threw=true;}
        Check(threw&&!transaction.Pending&&!throwing.Allowed&&throwing.Mode==Mode.None,"native exceptions unwrap and release only owned flags");
        var inherited=new DerivedMovie();Check(transaction.Request(inherited,14,out detail),"private native state in an inherited runtime type is resolved");transaction.Release();

        Check(!NativeSuspensionPolicy.CanExpire(90,true,false),"90-second story CG cannot hit the 45-second teleport timeout");
        Check(!NativeSuspensionPolicy.CanExpire(90,false,true),"long native interaction cannot hit teleport timeout");
        Check(NativeSuspensionPolicy.CanExpire(46,false,false)&&!NativeSuspensionPolicy.CanExpire(45,false,false),"ordinary stale teleport keeps its bounded recovery");
        Check(NativeSuspensionPolicy.IsSlideVolume("BP_SplineFlyMoveBase_C")&&NativeSuspensionPolicy.IsSlideVolume("SplineFlyMove_C"),"Preview125: native slide volume (Live124 SplineFlyMove_C_1) hands control to Wukong");
        Check(!NativeSuspensionPolicy.IsSlideVolume("AkAcousticPortal")&&!NativeSuspensionPolicy.IsSlideVolume("DeadZone_C")&&!NativeSuspensionPolicy.IsSlideVolume(null),"ordinary volumes do not");
        Check(!NativeSuspensionPolicy.IgnoreStuckTeleport(true,false,true),"a slide counts as live native work, not a stuck teleport");
        Check(!NativeSuspensionPolicy.IgnoreStuckTeleport(true,true,false)&&!NativeSuspensionPolicy.IgnoreStuckTeleport(true,false,true),"stale teleport flag cannot override a new CG or interaction");
        Check(NativeSuspensionPolicy.IgnoreStuckTeleport(true,false,false),"ordinary stale teleport can still recover");
        Console.WriteLine("MANUAL SKIP AND CG SUSPENSION LINKED-CODE PASS checks="+checks+" (stub native movie; no live progression acceptance)");return 0;
    }
    private enum Event{Skip=10}
    private enum State{Skipping=15}
    private enum Mode:byte{None=0,FastForward=1,SetPosition=2}
    private class MovieState_Playing
    {
        private Dictionary<Event,State> TransitionMap{get;}=new Dictionary<Event,State>{{Event.Skip,State.Skipping}};
        public void RemoveSkip(){TransitionMap.Clear();}
    }
    private sealed class MovieState_Skipping{}
    private class Movie
    {
        private object CurrentState{get;set;}=new MovieState_Playing();
        private bool bCanSkip{get;set;}
        private Mode SkipType{get;set;}
        private bool bDisableCameraCut=>CameraDisabled;
        private object SequencePlayer=>HasSequence?this:null;
        private bool IsLoadFinished()=>Loaded;
        public bool Loaded=true,CameraDisabled,HasSequence=true,Deferred,Throw;
        public int Requests;public bool SawAllowed;public Mode SawMode;
        public bool Allowed{get=>bCanSkip;set=>bCanSkip=value;}
        public Mode Mode{get=>SkipType;set=>SkipType=value;}
        public bool HasSkipEdge{set{if(!value)((MovieState_Playing)CurrentState).RemoveSkip();}}
        private void SkipMovie(){Requests++;SawAllowed=bCanSkip;SawMode=SkipType;if(Throw)throw new InvalidOperationException("native failure");if(!Deferred)EnterSkipping();}
        public void EnterSkipping(){CurrentState=new MovieState_Skipping();}
        public void Finish(){CurrentState=new MovieState_Playing();}
    }
    private sealed class DerivedMovie:Movie{}
}
