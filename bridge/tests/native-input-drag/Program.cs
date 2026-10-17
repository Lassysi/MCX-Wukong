using System;
using System.Collections.Generic;
using DqjCombat;
internal static class Program {
 static int failures;
 static void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){++failures;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
 static int Main(){
  Check("free cursor coalesces",()=>{var b=new NativeInputBuffer();b.Add(null,false,10,20);b.Add(null,false,40,20);var r=b.Take();Need(r.Count==1&&r[0].X==40,"last free-cursor position required");});
  Check("left drag visits every crossed slot",()=>{var b=new NativeInputBuffer();b.Add("LeftMouseButton",true,100,100);b.Add(null,false,120,100);b.Add(null,false,140,100);b.Add(null,false,160,100);b.Add("LeftMouseButton",false,160,100);var r=b.Take();Need(r.Count==5,"expected press +3 slot visits +release, got "+r.Count);Need(r[1].X==120&&r[2].X==140&&r[3].X==160,"native quickcraft must receive each crossed slot");});
  Check("right drag survives host tick drain",()=>{var b=new NativeInputBuffer();b.Add("RightMouseButton",true,100,100);b.Take();b.Add(null,false,120,100);b.Add(null,false,140,100);Need(b.Take().Count==2,"held state must survive Take");b.Add("RightMouseButton",false,140,100);b.Take();b.Add(null,false,160,100);b.Add(null,false,180,100);Need(b.Take().Count==1,"release restores free-cursor coalescing");});
  Check("one release does not clear another button",()=>{var b=new NativeInputBuffer();b.Add("LeftMouseButton",true,0,0);b.Add("RightMouseButton",true,0,0);b.Add("LeftMouseButton",false,0,0);b.Take();b.Add(null,false,1,0);b.Add(null,false,2,0);Need(b.Take().Count==2,"right button is still held");b.Clear();b.Add(null,false,3,0);b.Add(null,false,4,0);Need(b.Take().Count==1,"reset clears held state");});
  Check("actual 4K client click hits grass slot at reduced render resolution",()=>{double x,y;Need(NativePointerCoordinates.TryNormalize(2053,1656,3840,2160,out x,out y),"valid observed source rectangle");double gx=x*427-125,gy=y*240-37;Need(gx>=98&&gx<114&&gy>=142&&gy<158,"observed click must land inside original native grass slot, got "+gx+","+gy);double oldY=Math.Min(1,1656.0/1080)*240;Need(oldY>=240,"old render-space normalization reproduces out-of-GUI click");});
  Check("client-size normalization independent of render scaling",()=>{foreach(int width in new[]{1280,1920,2560,3840}){double x,y;Need(NativePointerCoordinates.TryNormalize(width*.25,width*9.0/16*.75,width,width*9/16,out x,out y)&&Math.Abs(x-.25)<1e-9&&Math.Abs(y-.75)<1e-9,"quarter/three-quarter screen identity");}});
  Check("invalid extent and captured cursor boundaries",()=>{double x,y;Need(!NativePointerCoordinates.TryNormalize(1,1,0,1080,out x,out y),"zero client width rejected");Need(NativePointerCoordinates.TryNormalize(-1,2200,3840,2160,out x,out y)&&x==0&&y==1,"captured out-of-window pointer clamps consistently with MC");});
  Console.WriteLine("Failures="+failures+"; input buffer contract only, not actual Wukong inventory acceptance.");return failures==0?0:1;
 }
}
