using System;
using DqjCombat;
class Program
{
    static int checks;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
    static void Main(){
        var mode=new ManualControlMode();
        Check(mode.Step(true,true,true)==ManualControlChange.None&&!mode.NativeActive,"starts in MC mode");
        mode.RequestToggle();Check(mode.Step(true,true,true)==ManualControlChange.EnterNative&&mode.NativeActive,"F8 enters native once");
        Check(mode.Step(true,true,true)==ManualControlChange.None&&mode.NativeActive,"native latch stays native without timer");
        for(int i=0;i<100;i++)mode.Step(false,true,true);
        Check(mode.NativeActive,"CG/menu/loading and new pawn lifecycle do not erase native user intent");
        mode.RequestToggle();Check(mode.Step(false,true,true)==ManualControlChange.Waiting&&mode.NativeActive,"return request waits through native CG/menu/teleport");
        Check(mode.Step(true,true,false)==ManualControlChange.Waiting&&mode.NativeActive,"no return while MC disconnected");
        Check(mode.Step(true,false,true)==ManualControlChange.Waiting&&mode.NativeActive,"no return to dead MC avatar");
        Check(mode.Step(true,true,true)==ManualControlChange.ResumeMinecraft&&!mode.NativeActive,"ready destination returns exactly once");
        Check(mode.Step(true,true,true)==ManualControlChange.None,"no repeated resume on later frames");
        mode.RequestToggle();mode.RequestToggle();Check(mode.Step(true,true,true)==ManualControlChange.None,"two actual presses cancel, no repeated transitions");
        mode.RequestToggle();Check(mode.Step(true,false,true)==ManualControlChange.RejectedDead&&!mode.NativeRequested,"MC death screen cannot be bypassed");
        mode.RequestToggle();Check(mode.Step(false,true,true)==ManualControlChange.Waiting&&!mode.NativeActive,"native entry deferred during busy state");
        Check(mode.Step(true,true,true)==ManualControlChange.EnterNative,"deferred native entry completes after busy state");
        mode.RequestToggle();mode.Step(false,true,true);mode.RequestToggle();
        Check(mode.Step(true,true,true)==ManualControlChange.None&&mode.NativeActive,"second F8 cancels a pending return");
        Console.WriteLine("MANUAL CONTROL PASS checks="+checks);
    }
}
