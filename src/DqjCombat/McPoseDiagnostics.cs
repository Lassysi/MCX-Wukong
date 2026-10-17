using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using UnrealEngine.Engine;

namespace DqjCombat
{
    // Opt-in camera observation only. These are game-thread PCM getters, not
    // an assertion about the final render-thread view/projection matrix.
    internal sealed class McPoseDiagnostics : IDisposable
    {
        private MemoryMappedFile mapping;
        private MemoryMappedViewAccessor memory;
        private string requestPath;
        private long sequence,nextPoll,until;
        private bool failed;
        internal void Sample(McBridgeWire.Frame frame,bool active,APlayerController controller,
            McWorldCoordinates coordinates,BridgePoint origin)
        {
            if(failed)return;
            try{
                long now=WindowsClock.Nanoseconds();
                if(now>=nextPoll){
                    nextPoll=now+1000000000L;
                    if(requestPath==null)requestPath=Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),"DqjPoseHostTrace.request");
                    if(File.Exists(requestPath)){
                        int seconds;bool parsed=int.TryParse(File.ReadAllText(requestPath).Trim(),out seconds);
                        File.Delete(requestPath);until=now+Math.Max(1,Math.Min(30,parsed?seconds:10))*1000000000L;
                        if(mapping==null){mapping=MemoryMappedFile.CreateOrOpen("Local\\DqjPoseDiagnostic",160);memory=mapping.CreateViewAccessor(0,160);}
                        Log.Write("pose-trace","requested bounded PCM observation; final GPU scene camera not claimed");
                    }
                }
                if(memory==null||now>=until)return;
                sequence+=2;memory.Write(8,sequence-1);System.Threading.Thread.MemoryBarrier();
                memory.Write(0,0x504a5144u);memory.Write(4,1u);memory.Write(16,GameApi.Frame());memory.Write(24,now);
                memory.Write(32,frame==null?0L:frame.Number);memory.Write(40,frame==null?0L:frame.Received);
                uint flags=active?1u:0u;
                if(frame!=null){
                    memory.Write(48,frame.EyeX);memory.Write(56,frame.EyeY);memory.Write(64,frame.EyeZ);
                    memory.Write(72,(float)frame.Yaw);memory.Write(76,(float)frame.Pitch);memory.Write(80,(float)frame.Roll);memory.Write(84,(float)frame.Fov);
                }
                var manager=controller.PlayerCameraManager;
                if(active&&coordinates!=null&&manager!=null&&!manager.IsDestroyed&&!manager.IsPendingKill){
                    var at=manager.GetCameraLocation();var rot=manager.GetCameraRotation();float fov=manager.GetFOVAngle();
                    var mc=coordinates.ToMc(new BridgePoint(at.X+origin.X,at.Y+origin.Y,at.Z+origin.Z));
                    if(mc.Finite&&!double.IsNaN(rot.Pitch+rot.Yaw+rot.Roll)&&!double.IsInfinity(rot.Pitch+rot.Yaw+rot.Roll)){
                        memory.Write(88,mc.X);memory.Write(96,mc.Y);memory.Write(104,mc.Z);
                        memory.Write(112,(float)rot.Yaw);memory.Write(116,(float)-rot.Pitch);memory.Write(120,(float)-rot.Roll);
                        memory.Write(124,fov);flags|=2;
                    }
                }
                memory.Write(128,flags);memory.Write(136,WindowsClock.Nanoseconds());
                System.Threading.Thread.MemoryBarrier();memory.Write(8,sequence);
            }catch(Exception e){failed=true;Log.Write("pose-trace-error",e.GetType().Name+": "+e.Message);Dispose();}
        }
        public void Dispose(){if(memory!=null)memory.Dispose();memory=null;if(mapping!=null)mapping.Dispose();mapping=null;}
    }
}
