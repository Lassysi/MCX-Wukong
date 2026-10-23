using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DqjCombat;
internal static class Program
{
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static int Main()
    {
        var R=StaggerPolicy.Reaction.None;
        // Unit rules.
        var p=new StaggerPolicy();
        Check(p.Hit("a",0,false,false)==R&&p.Hit("a",.5,false,false)==R,"two light attacks do not stagger");
        Check(p.Hit("a",1,false,false)==StaggerPolicy.Reaction.Light,"the third light attack fills the meter: light stagger");
        Check(p.Hit("a",1.5,false,false)==R&&p.Hit("a",2,false,false)==R&&p.Hit("a",2.5,false,false)==R,"2 s immunity after a stagger");
        Check(p.Hit("a",3.1,false,false)==R&&p.Hit("a",3.5,false,false)==R&&p.Hit("a",4,false,false)==R,"second stagger needs 4.5 hits (escalation)");
        Check(p.Hit("a",4.5,false,false)==R&&p.Hit("a",5,false,false)==StaggerPolicy.Reaction.Light,"...and comes on the fifth");
        var m=new StaggerPolicy();m.Hit("b",0,false,false);
        Check(m.Hit("b",.1,true,false)==R&&m.Hit("b",.2,true,false)==R&&m.Merged==2,"damage messages within 0.25 s are one attack (sweep, enchant bonus, fire)");
        var h=new StaggerPolicy();h.Hit("c",0,true,false);
        Check(h.Hit("c",.5,true,false)==StaggerPolicy.Reaction.Heavy,"two heavy attacks (crit, axe, explosion) fill the meter: heavy reaction");
        Check(h.Hit("c",2.6,true,false)==R&&h.Hit("c",3.1,true,false)==R&&h.Hit("c",3.6,true,false)==StaggerPolicy.Reaction.Light,"a heavy reaction 3.1 s later is still in its 4 s cooldown: light instead");
        var boss=new StaggerPolicy();int bossStaggers=0;for(int i=0;i<12;i++)if(boss.Hit("boss",i*.5,false,true)!=R)bossStaggers++;
        Check(bossStaggers==1,"a Boss takes 6 hits for one stagger (12 hits in 6 s -> 1)");
        var reset=new StaggerPolicy();for(int i=0;i<3;i++)reset.Hit("d",i*.5,false,false);
        Check(reset.Hit("d",10,false,false)==R&&reset.Hit("d",10.5,false,false)==R&&reset.Hit("d",11,false,false)!=R,"after 6 s without a stagger the threshold is back to 3");
        Check(StaggerPolicy.Needed(0,false)==3&&StaggerPolicy.Needed(2,false)==6.75&&StaggerPolicy.Needed(0,true)==6,"thresholds");
        // Replay of Live125 (every native reaction dispatch of the session, all treated as light, non-Boss attacks: upper bound).
        var rx=new Regex(@"^(\d+):(\d+):([\d.]+) \| world-stiff \| id=(\d+) .*event=(\w+)");
        var rows=File.ReadLines(Path.Combine(AppContext.BaseDirectory,"stagger-replay.txt")).Select(l=>rx.Match(l)).Where(x=>x.Success)
            .Select(x=>new{t=int.Parse(x.Groups[1].Value)*3600+int.Parse(x.Groups[2].Value)*60+double.Parse(x.Groups[3].Value,CultureInfo.InvariantCulture),id=x.Groups[4].Value,ev=x.Groups[5].Value}).ToList();
        int before=rows.Count(r=>r.ev=="NormalStiff");
        var replay=new StaggerPolicy();var times=new Dictionary<string,List<double>>();
        foreach(var r in rows)if(replay.Hit(r.id,r.t,false,false)!=R){List<double> l;if(!times.TryGetValue(r.id,out l))times[r.id]=l=new List<double>();l.Add(r.t);}
        int after=replay.Staggers;
        Console.WriteLine("Live125 replay: dispatches="+rows.Count+" NormalStiff before="+before+" staggers now="+after+" merged="+replay.Merged);
        Check(rows.Count==2928&&before==1735,"fixture is the whole Live125 session");
        Check(after*4<before,"at least 4x fewer staggers than Live125");
        double minGap=times.Values.SelectMany(l=>l.Zip(l.Skip(1),(a,b)=>b-a)).DefaultIfEmpty(99).Min();
        Check(minGap>=StaggerPolicy.Immunity,"no enemy staggered twice within 2 s (Live125 median gap was 0.14 s); min gap "+minGap.ToString("0.00",CultureInfo.InvariantCulture));
        int worst=0;foreach(var l in times.Values)for(int i=0;i<l.Count;i++){int n=l.Count(t=>t>=l[i]&&t<l[i]+10);if(n>worst)worst=n;}
        Check(worst<=4,"at most 4 staggers of one enemy in any 10 s (Live125 worst: 45 in 9.6 s); now "+worst);
        Console.WriteLine("STAGGER POLICY PASS");return 0;
    }
}
