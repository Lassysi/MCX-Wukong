using System;
using DqjCombat;
internal static class Program
{
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static int Main()
    {
        // Captured Preview22 menu-return failure: native point 7cm below MC's
        // neighbouring tile. Actual MC support selection is tested in the client.
        var requested=new BridgePoint(.914467,63.849231,-.438908);
        var declared=new BridgePoint(.914467,63.921,-.438908);
        var p=new McGroundPreparation("current",requested,100.5);
        Check(p.Ready("current",true,declared,declared)&&p.Resolved.Y==63.921&&p.Resolved.X==requested.X&&p.Resolved.Z==requested.Z,"accept MC vertical correction from captured failure without changing X/Z");
        Check(!p.Ready("old",true,declared,declared),"reject stale prepare epoch");
        Check(!p.Ready("current",false,declared,declared),"require MC support acknowledgement");
        // Preview99: a blocked spawn column may move the player up to 1.5 m aside (MC resolveNear ring), never further.
        var beside=new BridgePoint(requested.X+1.5,63.921,requested.Z);
        Check(new McGroundPreparation("current",requested,100.5).Ready("current",true,beside,beside),"accept a 1.5 m sideways placement beside a blocked spawn column");
        var far=new BridgePoint(requested.X+1.7,63.921,requested.Z);
        Check(!new McGroundPreparation("current",requested,100.5).Ready("current",true,far,far),"reject horizontal relocation beyond 1.6 m");
        var stepped=new BridgePoint(requested.X,requested.Y+.681802,requested.Z);
        Check(p.Ready("current",true,stepped,stepped),"accept the captured shrine's legacy slab top inside the MC step envelope");
        var tooHigh=new BridgePoint(requested.X,requested.Y+1.27,requested.Z);
        Check(!p.Ready("current",true,tooHigh,tooHigh),"reject ordinary support beyond the 1.26 m native step envelope");
        Check(!p.Ready("current",true,declared,new BridgePoint(10,63.921,0)),"wait for client arrival rather than server registration alone");
        Check(!p.Expired(108.499)&&p.Expired(108.5),"preparation has an eight-second upper bound");
        Check(!p.Ready("current",true,new BridgePoint(double.NaN,64,0),declared),"reject nonfinite acknowledgements");
        var anchored=new BridgePoint(requested.X,requested.Y+1,requested.Z);
        Check(p.Ready("current",true,anchored,anchored),"ordinary MC blocks allow an actual one-metre step with fixed X/Z");
        Check(p.Ready("current",true,anchored,anchored,true),"accept registered MC construction support while retaining X/Z");
        var excessive=new BridgePoint(requested.X,requested.Y+2.01,requested.Z);
        Check(!p.Ready("current",true,excessive,excessive,true),"registered support still has a two-metre bound");
        var below=new BridgePoint(requested.X,requested.Y-.61,requested.Z);
        Check(!p.Ready("current",true,below,below,true),"construction support never widens the downward bound");
        Console.WriteLine("GROUND PREPARATION PASS");return 0;
    }
}
