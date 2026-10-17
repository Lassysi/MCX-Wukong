using System;
using System.Globalization;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // Preview41 measurement. Live40 showed PCM at host frame F equals the pose
    // requested by the core ticker at F-1: the world tick updates the camera
    // before the core ticker runs. BeginFrame now applies the newest MC pose
    // before the world tick; this counts which pose the PCM actually adopted.
    internal sealed class CameraFrameOrder
    {
        private static readonly float PositionTolerance=.5f;   // centimetres
        private static readonly float AngleTolerance=.01f;     // degrees
        private bool hasBegin,hasTick;
        private FVector beginEye,tickEye;
        private FRotator beginRot,tickRot;
        private long beginMcFrame;
        private int applied,samples,matchBegin,matchPreviousTick,matchNeither,skippedBegin;
        private long newerAtTick;
        private double nextLog;

        internal void Began(FVector eye,FRotator rotation,long mcFrame)
        {beginEye=eye;beginRot=rotation;beginMcFrame=mcFrame;hasBegin=true;applied++;}

        internal void SkippedBegin(){skippedBegin++;}

        // Call in the core tick before the tick overwrites the camera actor.
        internal void Sample(APlayerCameraManager manager,long tickMcFrame,double now)
        {
            if(manager!=null&&!manager.IsDestroyed&&!manager.IsPendingKill&&(hasBegin||hasTick)){
                var at=manager.GetCameraLocation();var rot=manager.GetCameraRotation();
                bool begin=hasBegin&&Same(at,rot,beginEye,beginRot);
                bool previous=hasTick&&Same(at,rot,tickEye,tickRot);
                samples++;
                if(begin)matchBegin++;else if(previous)matchPreviousTick++;else matchNeither++;
                if(hasBegin&&tickMcFrame>beginMcFrame)newerAtTick+=tickMcFrame-beginMcFrame;
            }
            hasBegin=false;
            if(now>=nextLog){
                if(samples>0||applied>0)
                    Log.Write("camera-order","beginApplied="+applied+" beginSkipped="+skippedBegin+" samples="+samples+" pcmMatchesBegin="+matchBegin
                        +" pcmMatchesPreviousTick="+matchPreviousTick+" neither="+matchNeither
                        +" meanMcFramesNewerAtTick="+(applied==0?"n/a":((double)newerAtTick/applied).ToString("0.###",CultureInfo.InvariantCulture)));
                nextLog=now+10;applied=samples=matchBegin=matchPreviousTick=matchNeither=skippedBegin=0;newerAtTick=0;
            }
        }

        internal void Ticked(FVector eye,FRotator rotation){tickEye=eye;tickRot=rotation;hasTick=true;}

        internal void Reset(){hasBegin=hasTick=false;nextLog=0;applied=samples=matchBegin=matchPreviousTick=matchNeither=skippedBegin=0;newerAtTick=0;}

        private static bool Same(FVector at,FRotator rot,FVector eye,FRotator expected)
        {
            return (at-eye).Size()<=PositionTolerance&&Angle(rot.Yaw,expected.Yaw)<=AngleTolerance&&Angle(rot.Pitch,expected.Pitch)<=AngleTolerance;
        }
        private static float Angle(float a,float b){return Math.Abs(((a-b)%360+540)%360-180);}
    }
}
