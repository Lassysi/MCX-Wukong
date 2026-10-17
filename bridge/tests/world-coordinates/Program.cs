using System;
using System.IO;
using DqjCombat;

internal static class Program
{
    private static void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
    private static bool Same(BridgePoint a,BridgePoint b){return Math.Abs(a.X-b.X)<1e-7&&Math.Abs(a.Y-b.Y)<1e-7&&Math.Abs(a.Z-b.Z)<1e-7;}
    private static void Reject(Action action,string label){bool failed=false;try{action();}catch{failed=true;}Check(failed,label);}
    private static int Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
        string run=Path.Combine(root,"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(run);
        string world="11111111111111111111111111111111",other="22222222222222222222222222222222";
        var origin=new BridgePoint(-1000,2000,-300);var zero=new BridgePoint(0,0,0);
        var catalog=new McWorldCatalog(Path.Combine(run,"anchors.json"),Path.Combine(run,"no-seed.json"));
        var first=catalog.Resolve(world,"第一回地图",origin,zero);
        Check(Same(first.ToMc(new BridgePoint(-500,1700,150)),new BridgePoint(3.5,68.5,5.5)),"independent axis/unit oracle including negative native coordinates");
        var block=new BridgePoint(7,64,5);var placed=first.ToHost(block);
        var returned=catalog.Resolve(world,"第一回地图",new BridgePoint(-550,1550,150),zero);
        Check(returned.Region==0&&Same(returned.ToHost(block),placed),"moving/menu return does not re-anchor a placed block");
        var second=catalog.Resolve(world,"第二回地图",new BridgePoint(80000,-6000,17000),zero);
        Check(second.Region==1&&Math.Abs(second.McX-first.McX)>8000,"different native maps receive separate MC regions");
        var reloaded=new McWorldCatalog(Path.Combine(run,"anchors.json"),Path.Combine(run,"no-seed.json"));
        var restored=reloaded.Resolve(world,"第一回地图",origin,zero);
        Check(Same(restored.ToHost(block),placed),"fresh catalog instance reloads the same block coordinates");
        var absolute=restored.ToHost(block);var rebased=new BridgePoint(500000,-70000,12000);
        var local=new BridgePoint(absolute.X-rebased.X,absolute.Y-rebased.Y,absolute.Z-rebased.Z);
        Check(Same(restored.ToMc(new BridgePoint(local.X+rebased.X,local.Y+rebased.Y,local.Z+rebased.Z)),block),"Unreal world-origin rebasing preserves MC block identity");
        Check(reloaded.Resolve(other,"第一回地图",new BridgePoint(100,200,300),zero).Region==0,"different MC world identities have separate region numbering");
        Reject(()=>restored.RequireSpawn(new BridgePoint(.5,301,.5)),"reject unsupported vertical destination");
        Reject(()=>restored.RequireSpawn(new BridgePoint(5000,64,.5)),"reject crossing into another map region");
        string seed=Path.Combine(run,"seed.json");File.WriteAllText(seed,"{\"world\":\""+world+"\",\"host\":[-1000,2000,-300],\"unrealOrigin\":[0,0,0]}");
        var migration=new McWorldCatalog(Path.Combine(run,"migration.json"),seed);
        Check(migration.Resolve(world,"foreign-map",new BridgePoint(80000,2000,-300),zero).Region==1,"unmatched legacy area remains reserved rather than appearing on a foreign map");
        Check(migration.Resolve(world,"original-map",origin,zero).Region==0,"bounded matching legacy area binds to its original MC grid");
        var rejectOrigin=new McWorldCatalog(Path.Combine(run,"wrong-origin.json"),seed);
        Check(rejectOrigin.Resolve(world,"map",origin,new BridgePoint(1,0,0)).Region==1,"legacy binding refuses a different Unreal world origin");
        string corrupt=Path.Combine(run,"corrupt.json");string bad="{\"version\":1,\"frames\":[{\"world\":\""+world+"\",\"level\":\"bad\",\"region\":0.5,\"host\":[0,0,0],\"unrealOrigin\":[0,0,0]}]}";File.WriteAllText(corrupt,bad);
        Reject(()=>new McWorldCatalog(corrupt,seed).Resolve(world,"bad",origin,zero),"malformed catalog is rejected");
        Check(File.ReadAllText(corrupt)==bad,"rejected catalog is preserved byte for byte");
        string failing=Path.Combine(run,"write-failure.json");Directory.CreateDirectory(failing+".new");
        var fault=new McWorldCatalog(failing,Path.Combine(run,"no-seed.json"));
        Reject(()=>fault.Resolve(world,"map",origin,zero),"write failure rejects a new coordinate frame");
        Directory.Delete(failing+".new");
        Check(fault.Resolve(world,"map",origin,zero).Region==0,"write retry has no phantom region from the failed transaction");
        var entries=new System.Text.StringBuilder("{\"version\":1,\"frames\":[");
        for(int i=0;i<1024;i++){if(i>0)entries.Append(',');entries.Append("{\"world\":\"").Append(world).Append("\",\"level\":\"map").Append(i).Append("\",\"region\":").Append(i).Append(",\"host\":[0,0,0],\"unrealOrigin\":[0,0,0]}");}
        entries.Append("]}");string full=Path.Combine(run,"full.json");File.WriteAllText(full,entries.ToString());
        Reject(()=>new McWorldCatalog(full,seed).Resolve(other,"new-map",zero,zero),"catalog capacity refuses a new map instead of writing an unloadable catalog");
        Check(File.ReadAllText(full)==entries.ToString(),"capacity rejection preserves all existing map records");
        Console.WriteLine("WORLD COORDINATES PASS "+run);return 0;
    }
}
