using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DqjCombat;

internal static class Program
{
    private static readonly List<string> checks=new List<string>();
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
    private static bool Near(BridgePoint p,double x,double y,double z){return Math.Abs(p.X-x)+Math.Abs(p.Y-y)+Math.Abs(p.Z-z)<.00001;}
    private static BridgePoint Point(JsonElement a){return new BridgePoint(a[0].GetDouble(),a[1].GetDouble(),a[2].GetDouble());}
    private static int Main(string[] args)
    {
        if(args.Length==1&&args[0]=="--packet"){
            // Actual MC tests feed a recorded frame. This uses the same packet
            // producer as the mod, with synthetic native impact distances.
            var m=JsonDocument.Parse(Console.In.ReadToEnd()).RootElement;
            var v=m.GetProperty("view");var origin=Point(v.GetProperty("eye"));
            var direction=Point(v.GetProperty("direction"));double d=m.GetProperty("distance").GetDouble();
            var normal=m.TryGetProperty("normal",out var n)?Point(n):new BridgePoint(0,1,0);
            var impact=new McNativeRay.Hit{Blocking=true,Point=new BridgePoint(origin.X+direction.X*d,origin.Y+direction.Y*d,origin.Z+direction.Z*d),Normal=normal};
            if(m.TryGetProperty("hit",out var enabled)&&!enabled.GetBoolean())impact=new McNativeRay.Hit();
            string encoded=McNativeRay.Packet(v.GetProperty("world").GetString(),v.GetProperty("epoch").GetString(),v.GetProperty("frame").GetInt64(),v.GetProperty("tick").GetInt64(),origin,direction,v.GetProperty("reach").GetDouble(),impact,impact);
            if(encoded==null)return 2;Console.WriteLine(encoded);return 0;
        }
        Check(Near(McNativeRay.Direction(0,0),0,0,1)&&Near(McNativeRay.Direction(90,0),-1,0,0)&&Near(McNativeRay.Direction(180,0),0,0,-1),"MC yaw axes retain native host orientation");
        Check(Near(McNativeRay.Direction(37,90),0,-1,0)&&Near(McNativeRay.Direction(37,-90),0,1,0),"MC positive pitch points downward");
        Check(Near(McNativeRay.NormalToMc(1,0,0),0,0,1)&&Near(McNativeRay.NormalToMc(0,1,0),-1,0,0)&&Near(McNativeRay.NormalToMc(0,0,1),0,1,0),"normal axes rotate without centimetre scaling");
        var coords=new McWorldCoordinates{Region=0,HostOrigin=new BridgePoint(-35348.16,18174.31,-920.917)};
        var local=coords.ToMc(new BridgePoint(-35048.16,18074.31,-870.917));
        Check(Near(local,1.5,64.5,3.5),"captured negative host origin transforms measured point in metres");
        string world=new string('a',32),epoch=new string('b',32);var eye=new BridgePoint(.5,65.62,.5);
        var hit=new McNativeRay.Hit{Blocking=true,Point=new BridgePoint(.5,63.35,.5),Normal=new BridgePoint(0,1,0)};
        var down=new BridgePoint(0,-1,0);
        string packet=McNativeRay.Packet(world,epoch,42,20,eye,down,4.5,hit,new McNativeRay.Hit());
        var root=JsonDocument.Parse(packet).RootElement;
        Check(root.GetProperty("simple").GetProperty("valid").GetBoolean()&&Math.Abs(root.GetProperty("simple").GetProperty("distance").GetDouble()-2.27)<1e-8,"fractional native hit distance and normal serialize coherently");
        Check(!root.GetProperty("complex").GetProperty("hit").GetBoolean()&&Near(Point(root.GetProperty("complex").GetProperty("point")),.5,61.12,.5),"miss encodes finite segment endpoint rather than default hit origin");
        Check(McNativeRay.Packet("bad",epoch,42,20,eye,down,4.5,hit,hit)==null&&McNativeRay.Packet(world,epoch,42,20,eye,down,double.NaN,hit,hit)==null&&McNativeRay.Packet(world,epoch,42,20,eye,down,12.01,hit,hit)==null,"reject malformed identity and invalid or over-budget reach");
        Check(McNativeRay.Packet(world,epoch,42,20,eye,new BridgePoint(0,-2,0),4.5,hit,hit)==null,"reject non-unit direction rather than extending MC reach");
        var off=hit;off.Point.X+=.1;
        Check(McNativeRay.Packet(world,epoch,42,20,eye,down,4.5,off,hit)==null,"reject native point outside the aimed segment");
        var penetrating=hit;penetrating.Penetrating=true;
        root=JsonDocument.Parse(McNativeRay.Packet(world,epoch,42,20,eye,down,4.5,penetrating,hit)).RootElement;
        Check(!root.GetProperty("simple").GetProperty("valid").GetBoolean(),"start penetration cannot become an actionable surface");
        if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new {pass=true,scope="actual C# packet/coordinate producer; no native runtime acceptance",checks},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("NATIVE RAY CONTRACT PASS "+checks.Count);return 0;
    }
}
