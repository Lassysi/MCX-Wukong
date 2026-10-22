using System;
using DqjCombat;
internal static class Program
{
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
    private static void RetainedCombat()
    {
        var authority=new NativeCombatAuthority();var move=new NativeMovementLease();int nativeMode=1,restores=0;
        move.Hold(nativeMode,0,()=>nativeMode=0);
        Check(authority.Gameplay(true,false,true,true),"ordinary MC gameplay is retained");
        for(int i=0;i<600;i++){
            CheckSilent(authority.Gameplay(false,true,true,true),"native grab input lock released MC ownership");
            move.Hold(nativeMode,0,()=>nativeMode=0);
        }
        Check(authority.Gameplay(false,false,true,true),"sync completion keeps MC despite a late native input-unlock event");
        Check(authority.Gameplay(true,false,true,true),"native input unlocked after combat");
        Check(!authority.Gameplay(false,false,true,true),"combat input exception ends when normal input resumes");
        move.Release((mode,custom)=>{nativeMode=mode;restores++;});
        Check(nativeMode==1&&restores==1,"native movement lease preserves walking across repeated sync frames");
        // Captured failure: old GrabView released the session and tried to resolve this out-of-range landing.
        var frame=new McWorldCoordinates{HostOrigin=new BridgePoint(-76023.59375,54829.5625,1189.531494)};
        var safe=new BridgePoint(259.6,-49,-44.3);var bad=frame.ToHost(new BridgePoint(259.6,-50.763,-44.3));
        bool rejected=false;try{frame.RequireSpawn(frame.ToMc(bad));}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&authority.Gameplay(true,false,true,true),"captured out-of-range native grab landing does not require releasing the existing MC frame");
        Check(Math.Abs(frame.ToMc(frame.ToHost(safe)).Y-safe.Y)<.000001,"retaining the MC frame preserves existing block coordinates");
    }
    private static void CheckSilent(bool ok,string label){if(!ok)throw new Exception(label);}
    private static int Main()
    {
        RetainedCombat();
        foreach(string boundary in new[]{"CG","native menu","loading","death","lost pawn","native interaction"}){
            var p=new NativeCombatAuthority();p.Gameplay(false,true,true,true);
            Check(!p.Gameplay(false,true,true,false),boundary+" revokes combat input exception");
            Check(!p.Gameplay(false,false,true,true),boundary+" cannot leave a stale combat exception");
        }
        var fresh=new NativeCombatAuthority();
        Check(!fresh.Gameplay(false,true,false,true),"native input lock cannot create initial MC ownership");
        fresh.Gameplay(false,true,true,true);fresh.Clear();
        Check(!fresh.Gameplay(false,false,true,true),"explicit bridge release clears combat state");
        Check(fresh.Gameplay(true,false,false,true),"normal initial handshake remains allowed");
        Console.WriteLine("COMBAT AUTHORITY PASS (offline policy/lifecycle; native gameplay still requires live acceptance)");return 0;
    }
}
