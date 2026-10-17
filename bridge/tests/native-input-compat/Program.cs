using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using DqjCombat;

internal static class Program
{
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
    private static int Main(string[] args)
    {
        if(args.Length==3){
            using(var windowProbe=new NativeTravelKey(()=>{})){
                typeof(NativeTravelKey).GetField("inputWindow",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(windowProbe,new IntPtr(Convert.ToInt64(args[0],16)));
                var before=GetThreadDpiAwarenessContext();int width,height;
                if(!windowProbe.TryGetPointerClientSize(out width,out height)||width!=int.Parse(args[1])||height!=int.Parse(args[2]))throw new Exception("Source window physical client size mismatch: "+width+"x"+height);
                if(GetThreadDpiAwarenessContext()!=before)throw new Exception("Caller thread DPI context was not restored");
                Console.WriteLine("PASS: formal NativeTravelKey reads actual source client "+width+"x"+height+" and restores caller DPI context. Read-only window geometry; no game input.");
            }return 0;
        }
        var wheel=new List<int>();var sprint=new List<bool>();int travel=0;
        var controls=new NativeInputBuffer();
        using(var hook=new NativeTravelKey(()=>travel++,wheel.Add,sprint.Add,controls.Add,(x,y)=>controls.Add(null,false,x,y))){
            var callback=typeof(NativeTravelKey).GetMethod("OnMessage",BindingFlags.NonPublic|BindingFlags.Instance);
            var message=Marshal.AllocHGlobal(48);
            try{
                Action<int,long,long> send=(id,key,flags)=>{
                    for(int i=0;i<48;i++)Marshal.WriteByte(message,i,0);
                    Marshal.WriteInt32(message,IntPtr.Size,id);
                    if(IntPtr.Size==8){Marshal.WriteInt64(message,16,key);Marshal.WriteInt64(message,24,flags);}
                    else{Marshal.WriteInt32(message,8,(int)key);Marshal.WriteInt32(message,12,(int)flags);}
                    callback.Invoke(hook,new object[]{0,new IntPtr(1),message});
                };
                send(0x20a,120L<<16,0);send(0x20a,unchecked((ushort)-120)*65536L,0);send(0x20a,15L<<16,0);
                send(0x100,0x11,0);send(0x100,0x11,1L<<30);send(0x101,0x11,0);
                send(0x100,0x11,1L<<24);send(0x101,0x11,1L<<24);
                send(0x100,0x75,0);send(0x100,0x75,1L<<30);
                if(string.Join(",",wheel)!="120,-120,15")throw new Exception("Lost signed or high-resolution wheel pulse");
                if(string.Join(",",sprint)!="True,False")throw new Exception("Lost short Ctrl press/release or accepted repeat/right Ctrl");
                if(travel!=1)throw new Exception("F6 regression");
                controls.Clear();
                send(0x100,0x45,0);send(0x101,0x45,0);
                send(0x200,0,100|(200L<<16));send(0x200,0,120|(210L<<16));
                send(0x201,0,125|(215L<<16));
                send(0x200,0,220|(210L<<16));send(0x202,0,225|(215L<<16));
                var inputs=controls.Take();
                if(inputs.Count!=6||inputs[0].Name!="E"||!inputs[0].Down||inputs[1].Down||inputs[2].Name!=null||inputs[2].X!=120||
                    inputs[3].Name!="LeftMouseButton"||!inputs[3].Down||inputs[3].X!=125||inputs[4].X!=220||inputs[5].Down||inputs[5].X!=225)
                    throw new Exception("Short E or pointer/press/drag/release ordering lost");
            }finally{Marshal.FreeHGlobal(message);}
        }
        Console.WriteLine("PASS: formal native callback decodes positive/negative/fractional wheel messages; short left Ctrl press/release survives; autorepeat/right Ctrl ignored; F6 preserved. Synthetic MSG data in a child process, not actual Wukong keyboard acceptance.");
        return 0;
    }
}
namespace DqjCombat {internal static class Log {internal static void Write(string tag,string text){Console.WriteLine(tag+" | "+text);}}}
