using System;
using System.Globalization;

namespace DqjCombat
{
    // Geometry and packet contract only. Does not replace MC picking or place blocks.
    internal static class McNativeRay
    {
        internal struct Hit
        {
            internal bool Blocking,Penetrating;
            internal BridgePoint Point,Normal;
            // Preview85: MC Y of the lowest native ground under a 1x1 footprint centred on Point.
            internal bool HasSupport;internal double Support;
        }
        internal static BridgePoint Direction(double yaw,double pitch)
        {
            double y=yaw*Math.PI/180,p=pitch*Math.PI/180,c=Math.Cos(p);
            return new BridgePoint(-Math.Sin(y)*c,-Math.Sin(p),Math.Cos(y)*c);
        }
        internal static BridgePoint NormalToMc(double x,double y,double z)
        {return new BridgePoint(-y,z,x);}
        private static double Dot(BridgePoint a,BridgePoint b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        private static string N(double n){return n.ToString("R",CultureInfo.InvariantCulture);}
        private static string P(BridgePoint p){return "["+N(p.X)+","+N(p.Y)+","+N(p.Z)+"]";}
        private static bool Token(string value)
        {
            if(value==null||value.Length!=32)return false;
            foreach(char c in value)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F')))return false;
            return true;
        }
        private static string Sample(Hit hit,BridgePoint eye,BridgePoint direction,double reach)
        {
            if(!hit.Blocking){hit.Penetrating=false;hit.Point=new BridgePoint(eye.X+direction.X*reach,eye.Y+direction.Y*reach,eye.Z+direction.Z*reach);hit.Normal=new BridgePoint();}
            if(!hit.Point.Finite||!hit.Normal.Finite)return null;
            var delta=new BridgePoint(hit.Point.X-eye.X,hit.Point.Y-eye.Y,hit.Point.Z-eye.Z);
            double distance=Math.Sqrt(Dot(delta,delta)),along=Dot(delta,direction);
            if(along<-.02||along>reach+.02||Dot(delta,delta)-along*along>.0009)return null;
            double length=Math.Sqrt(Dot(hit.Normal,hit.Normal));
            bool valid=!hit.Penetrating&&(!hit.Blocking||(length>.9&&length<1.1));
            if(valid&&hit.Blocking)hit.Normal=new BridgePoint(hit.Normal.X/length,hit.Normal.Y/length,hit.Normal.Z/length);
            return "{\"hit\":"+(hit.Blocking?"true":"false")+",\"penetrating\":"+(hit.Penetrating?"true":"false")
                +",\"valid\":"+(valid?"true":"false")+",\"point\":"+P(hit.Point)+",\"normal\":"+P(hit.Normal)+",\"distance\":"+N(distance)
                +(hit.HasSupport&&valid&&hit.Blocking&&!double.IsNaN(hit.Support)&&!double.IsInfinity(hit.Support)?",\"support\":"+N(hit.Support):"")+"}";
        }
        internal static string Packet(string world,string epoch,long frame,long tick,BridgePoint eye,BridgePoint direction,double reach,Hit simple,Hit complex)
        {
            if(!Token(world)||!Token(epoch)||frame<1||tick<0||!eye.Finite||!direction.Finite||Math.Abs(Dot(direction,direction)-1)>.00001||double.IsNaN(reach)||double.IsInfinity(reach)||reach<=0||reach>12)return null;
            string a=Sample(simple,eye,direction,reach),b=Sample(complex,eye,direction,reach);
            if(a==null||b==null)return null;
            return "{\"t\":\"host_ray\",\"world\":\""+world+"\",\"epoch\":\""+epoch+"\",\"f\":"+frame+",\"tick\":"+tick
                +",\"eye\":"+P(eye)+",\"direction\":"+P(direction)+",\"reach\":"+N(reach)+",\"simple\":"+a+",\"complex\":"+b+"}";
        }
    }
}
