using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LitJson;

namespace DqjCombat
{
    // User world data, intentionally excluded from immutable installer payloads.
    // Loading and writes are lazy, never performed in a static initializer.
    internal sealed class McWorldCatalog
    {
        private readonly string path,seedPath;
        private readonly List<McWorldCoordinates> frames=new List<McWorldCoordinates>();
        private bool loaded;
        internal McWorldCatalog(string path,string seedPath){this.path=path;this.seedPath=seedPath;}
        private static double N(JsonData d){return double.Parse(d.ToString(),CultureInfo.InvariantCulture);}
        private static int Region(JsonData d){double value=N(d);if(value<0||value>=1024||value!=Math.Floor(value))throw new InvalidDataException("Invalid world region index");return (int)value;}
        private static BridgePoint Point(JsonData d){if(d.Count!=3)throw new InvalidDataException("Expected three coordinates");return new BridgePoint(N(d[0]),N(d[1]),N(d[2]));}
        private static bool ValidWorld(string value){Guid id;return value!=null&&Guid.TryParseExact(value,"N",out id);}
        private static void Validate(McWorldCoordinates f)
        {
            if(!ValidWorld(f.World)||f.Level==null||f.Level.Length>512||f.Region<0||f.Region>=1024||!f.HostOrigin.Finite||!f.UnrealOrigin.Finite)
                throw new InvalidDataException("Invalid native/MC world coordinate record");
        }
        private void Load()
        {
            if(loaded)return;
            var parsed=new List<McWorldCoordinates>();
            if(File.Exists(path)){
                var root=JsonMapper.ToObject(File.ReadAllText(path,Encoding.UTF8));
                if(N(root["version"])!=1||root["frames"].Count>1024)throw new InvalidDataException("Unsupported world coordinate catalog");
                var regions=new HashSet<string>();var levels=new HashSet<string>();
                foreach(JsonData item in root["frames"]){
                    var f=new McWorldCoordinates{World=(string)item["world"],Level=(string)item["level"],Region=Region(item["region"]),HostOrigin=Point(item["host"]),UnrealOrigin=Point(item["unrealOrigin"])};
                    Validate(f);
                    // Preview121: a large map may own several regions (one per far-apart area), so only regions must be unique.
                    if(!regions.Add(f.World+"/"+f.Region))throw new InvalidDataException("Duplicate world coordinate record");
                    parsed.Add(f);
                }
            }
            frames.AddRange(parsed);loaded=true;
        }
        private McWorldCoordinates Seed(string world)
        {
            if(!File.Exists(seedPath))return null;
            var seed=JsonMapper.ToObject(File.ReadAllText(seedPath,Encoding.UTF8));
            if((string)seed["world"]!=world)return null;
            var frame=new McWorldCoordinates{World=world,Level="",Region=0,HostOrigin=Point(seed["host"]),UnrealOrigin=Point(seed["unrealOrigin"])};
            Validate(frame);return frame;
        }
        internal McWorldCoordinates Resolve(string world,string level,BridgePoint absoluteFoot,BridgePoint unrealOrigin)
        {
            if(!ValidWorld(world)||string.IsNullOrEmpty(level)||level.Length>512||!absoluteFoot.Finite||!unrealOrigin.Finite)
                throw new InvalidOperationException("Native and MC world identities must be ready before takeover");
            Load();
            // Preview121 (user: after a story cutscene that teleports to a far part of the map, MC never took over again): a
            // map's region covers +-4 km and -114..+236 m around where it was first entered. A destination outside every
            // region of this map (Chapter 3's LYS: refused every 3 s) now gets its own extra region centred on it. Existing
            // regions and their blocks never move. Among regions containing the spot, the nearest origin wins.
            McWorldCoordinates best=null;bool sameLevel=false;double bestDistance=double.MaxValue;
            foreach(var f in frames)if(f.World==world&&f.Level==level){
                sameLevel=true;var at=f.ToMc(absoluteFoot);if(!f.Comfortable(at))continue;
                double d=Math.Abs(at.X-f.McX)+Math.Abs(at.Z-f.McZ)+Math.Abs(at.Y-64);
                if(d<bestDistance){bestDistance=d;best=f;}
            }
            if(best!=null)return best;
            int count=0;McWorldCoordinates legacy=null;
            foreach(var f in frames)if(f.World==world){count=Math.Max(count,f.Region+1);if(f.Level.Length==0)legacy=f;}
            if(sameLevel){
                if(frames.Count>=1024||count>=1024)throw new InvalidOperationException("World coordinate catalog capacity reached; existing maps preserved");
                var extra=new McWorldCoordinates{World=world,Level=level,Region=count,HostOrigin=absoluteFoot,UnrealOrigin=unrealOrigin};Validate(extra);frames.Add(extra);
                try{extra.RequireSpawn(extra.ToMc(absoluteFoot));Save();return extra;}
                catch{frames.Remove(extra);throw;}
            }
            bool addedSeed=false;
            if(count==0){legacy=Seed(world);if(legacy!=null){frames.Add(legacy);count=1;addedSeed=true;}}
            McWorldCoordinates result=null;bool boundLegacy=false;
            if(legacy!=null){
                var h=legacy.HostOrigin;
                double dx=h.X-absoluteFoot.X,dy=h.Y-absoluteFoot.Y,dz=h.Z-absoluteFoot.Z;
                bool originMatches=legacy.UnrealOrigin.X==unrealOrigin.X&&legacy.UnrealOrigin.Y==unrealOrigin.Y&&legacy.UnrealOrigin.Z==unrealOrigin.Z;
                if(originMatches&&dx*dx+dy*dy+dz*dz<=2000*2000){legacy.Level=level;result=legacy;boundLegacy=true;}
            }
            if(result==null){
                if(frames.Count>=1024){if(addedSeed)frames.Remove(legacy);throw new InvalidOperationException("World coordinate catalog capacity reached; existing maps preserved");}
                result=new McWorldCoordinates{World=world,Level=level,Region=count,HostOrigin=absoluteFoot,UnrealOrigin=unrealOrigin};Validate(result);frames.Add(result);
            }
            try{result.RequireSpawn(result.ToMc(absoluteFoot));Save();return result;}
            catch{
                if(boundLegacy)legacy.Level="";else frames.Remove(result);
                if(addedSeed)frames.Remove(legacy);
                throw;
            }
        }
        private static string F(double value){return value.ToString("R",CultureInfo.InvariantCulture);}
        private static string Quote(string value){var writer=new JsonWriter();writer.Validate=false;writer.Write(value);return writer.ToString();}
        private static void PointJson(StringBuilder b,BridgePoint p){b.Append('[').Append(F(p.X)).Append(',').Append(F(p.Y)).Append(',').Append(F(p.Z)).Append(']');}
        private void Save()
        {
            var b=new StringBuilder("{\"version\":1,\"frames\":[");bool comma=false;
            foreach(var f in frames){
                if(comma)b.Append(',');comma=true;
                b.Append("{\"world\":").Append(Quote(f.World)).Append(",\"level\":").Append(Quote(f.Level)).Append(",\"region\":").Append(f.Region).Append(",\"host\":");
                PointJson(b,f.HostOrigin);b.Append(",\"unrealOrigin\":");PointJson(b,f.UnrealOrigin);b.Append('}');
            }
            b.Append("]}");Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string pending=path+".new";File.WriteAllText(pending,b.ToString(),new UTF8Encoding(false));
            if(File.Exists(path))File.Replace(pending,path,path+".previous");else File.Move(pending,path);
        }
    }
}
