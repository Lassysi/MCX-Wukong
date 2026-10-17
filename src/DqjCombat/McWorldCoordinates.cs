using System;

namespace DqjCombat
{
    internal struct BridgePoint
    {
        internal double X,Y,Z;
        internal BridgePoint(double x,double y,double z){X=x;Y=y;Z=z;}
        internal bool Finite {get{return !double.IsNaN(X+Y+Z)&&!double.IsInfinity(X+Y+Z);}}
    }
    // Absolute host centimetres <-> persistent MC metres. Unreal origin rebasing
    // belongs at the caller boundary; it must never move a placed MC block.
    internal sealed class McWorldCoordinates
    {
        internal static readonly double RegionStride=8192;
        internal string World,Level;
        internal int Region;
        internal BridgePoint HostOrigin,UnrealOrigin;
        internal double McX {get{return .5+(Region%32)*RegionStride;}}
        internal double McZ {get{return .5+(Region/32)*RegionStride;}}
        internal BridgePoint ToMc(BridgePoint absolute)
        {return new BridgePoint(McX-(absolute.Y-HostOrigin.Y)/100,64+(absolute.Z-HostOrigin.Z)/100,McZ+(absolute.X-HostOrigin.X)/100);}
        internal BridgePoint ToHost(BridgePoint mc)
        {return new BridgePoint(HostOrigin.X+(mc.Z-McZ)*100,HostOrigin.Y-(mc.X-McX)*100,HostOrigin.Z+(mc.Y-64)*100);}
        internal void RequireSpawn(BridgePoint spawn)
        {
            if(!spawn.Finite||spawn.Y< -50||spawn.Y>300||Math.Abs(spawn.X-McX)>RegionStride*.5-64||Math.Abs(spawn.Z-McZ)>RegionStride*.5-64)
                throw new InvalidOperationException("Native destination is outside this MC region's verified bounds");
        }
    }
}
