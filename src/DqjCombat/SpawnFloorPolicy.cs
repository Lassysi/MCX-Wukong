namespace DqjCombat
{
    // Preview126 (Live125 21:20:45 / 21:22:36, LYS arrival at Z 514.516): after a native teleport Wukong was in MOVE_Flying at a fixed
    // height inside the hillside, so the visibility floor trace (2.5 m up / 4 m down) found nothing and the takeover waited 11 s / 36 s.
    // Where a takeover may stand, in order: visibility trace, the character's own floor, a Pawn-object floor, the lowest walkable surface
    // above a flying or sunk body. Pure rules here; the engine probes live in McBridgeHost.
    internal static class SpawnFloorPolicy
    {
        internal static readonly float[] AboveSteps={250,500,1000,2000,3000};
        internal static readonly double MinWalkableNormalZ=.7;
        internal static readonly double HeadroomMarginCm=20;
        // CMC floor: walking on a blocking, walkable floor no further than 30 cm under the capsule.
        internal static bool AcceptCharacterFloor(bool walking,bool blocking,bool walkable,float floorDist)
        {
            return walking&&blocking&&walkable&&!float.IsNaN(floorDist)&&floorDist>=-5&&floorDist<=30;
        }
        // A surface found above the feet: walkable, above the feet, within the 30 m search and with room for the capsule.
        internal static bool AcceptSurfaceAbove(double footZ,double surfaceZ,double normalZ,bool ceilingHit,double ceilingZ,double capsuleHeight)
        {
            if(double.IsNaN(surfaceZ)||double.IsInfinity(surfaceZ)||double.IsNaN(normalZ))return false;
            double rise=surfaceZ-footZ;
            if(rise<=0||rise>AboveSteps[AboveSteps.Length-1])return false;
            if(normalZ<MinWalkableNormalZ)return false;
            return !ceilingHit||ceilingZ-surfaceZ>=capsuleHeight+HeadroomMarginCm;
        }
        // GroundCells format "x,y,z,mm" (mm = surface height inside cell y). Adds the spawn column when the raster has none for it.
        internal static string WithColumn(string cells,double x,double top,double z)
        {
            if(double.IsNaN(top)||double.IsInfinity(top))return cells;
            int cx=(int)System.Math.Floor(x),cz=(int)System.Math.Floor(z);
            string[] values=string.IsNullOrEmpty(cells)?new string[0]:cells.Split(',');
            for(int i=0;i+3<values.Length;i+=4)if(values[i]==cx.ToString(System.Globalization.CultureInfo.InvariantCulture)&&values[i+2]==cz.ToString(System.Globalization.CultureInfo.InvariantCulture))return cells;
            int millimetres=(int)System.Math.Round(top*1000);int y=(int)System.Math.Ceiling(millimetres/1000.0)-1;
            string cell=cx.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+y.ToString(System.Globalization.CultureInfo.InvariantCulture)+","
                +cz.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+(millimetres-y*1000).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(cells)?cell:cells+","+cell;
        }
        // The upward search only runs when the body has no floor under it and is held in the air or inside geometry.
        internal static bool SearchAbove(bool walking,bool falling)
        {
            return !walking&&!falling;
        }
    }
}
