using System;
using DqjCombat;
internal static class Program
{
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static int Main()
    {
        // Live125 21:20:45 / 21:22:36: foot Z 514.516 in MOVE_Flying, visible ground 532-775 above it.
        Check(SpawnFloorPolicy.AcceptCharacterFloor(true,true,true,2.1f),"walking on a walkable CMC floor 2 cm below is support");
        Check(!SpawnFloorPolicy.AcceptCharacterFloor(false,true,true,2.1f),"a flying body's stale CMC floor is not support");
        Check(!SpawnFloorPolicy.AcceptCharacterFloor(true,true,false,2.1f),"an unwalkable CMC floor is not support");
        Check(!SpawnFloorPolicy.AcceptCharacterFloor(true,true,true,45f),"a CMC floor 45 cm below is not standing support");
        Check(!SpawnFloorPolicy.AcceptCharacterFloor(true,true,true,float.NaN),"reject a non-finite floor distance");
        Check(SpawnFloorPolicy.AcceptSurfaceAbove(514.516,532.387,.98,false,double.NaN,180),"captured shrine ground 18 cm above the sunk feet is accepted");
        Check(SpawnFloorPolicy.AcceptSurfaceAbove(514.516,775.686,.9,true,1100,180),"hillside 2.6 m above with 3.2 m headroom is accepted");
        Check(!SpawnFloorPolicy.AcceptSurfaceAbove(514.516,700,.9,true,850,180),"surface under a low roof (1.5 m headroom) is rejected");
        Check(!SpawnFloorPolicy.AcceptSurfaceAbove(514.516,700,.5,false,double.NaN,180),"steep surface (normal 0.5) is rejected");
        Check(!SpawnFloorPolicy.AcceptSurfaceAbove(514.516,514.516-10,.99,false,double.NaN,180),"a surface below the feet is not 'above'");
        Check(!SpawnFloorPolicy.AcceptSurfaceAbove(514.516,514.516+3001,.99,false,double.NaN,180),"nothing beyond the 30 m search");
        Check(SpawnFloorPolicy.SearchAbove(false,false),"flying/held body searches above");
        Check(!SpawnFloorPolicy.SearchAbove(true,false)&&!SpawnFloorPolicy.SearchAbove(false,true),"walking or falling bodies never search above (no snapping back onto a ledge)");
        Check(SpawnFloorPolicy.WithColumn("",40960.5,64.0,0.5)=="40960,63,0,1000","spawn column added to an empty raster (top 64.000 = cell 63 + 1000 mm)");
        Check(SpawnFloorPolicy.WithColumn("40960,63,0,1000",40960.5,64.2,0.5)=="40960,63,0,1000","existing raster column is kept, never duplicated");
        Check(SpawnFloorPolicy.WithColumn("1,2,3,4",-0.5,66.361,-3.6)=="1,2,3,4,-1,66,-4,361","negative coordinates floor correctly");
        Check(SpawnFloorPolicy.WithColumn("1,2,3,4",0,double.NaN,0)=="1,2,3,4","non-finite top leaves the raster unchanged");
        Console.WriteLine("SPAWN FLOOR PASS");return 0;
    }
}
