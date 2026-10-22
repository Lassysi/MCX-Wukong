using System;
using DqjCombat;
internal static class Program
{
    private static int checks;
    private static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    private static int Main()
    {
        Check(BoundaryDamage.Drop(true,-180,360,true),"Live121 kill-volume hit (-180 of 360, no attacker) dropped");
        Check(BoundaryDamage.Drop(true,-360,360,true),"Live121 full kill (-360 of 360) dropped");
        Check(!BoundaryDamage.Drop(false,-360,360,true),"a hit with an attacker always reaches MC");
        Check(!BoundaryDamage.Drop(true,-14,360,true),"small attacker-less tick (hazard ground) is not boundary damage");
        Check(!BoundaryDamage.Drop(true,-180,360,false),"native control (cutscene, menu, transformation) keeps native rules");
        Check(!BoundaryDamage.Drop(true,90,360,true),"healing is never dropped");
        Check(!BoundaryDamage.Drop(true,-180,0,true)&&!BoundaryDamage.Drop(true,float.NaN,360,true),"bad numbers are not treated as boundary hits");
        Console.WriteLine("BOUNDARY DAMAGE PASS checks="+checks);return 0;
    }
}
