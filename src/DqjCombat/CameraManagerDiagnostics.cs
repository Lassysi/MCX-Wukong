using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using b1;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // Preview39: read-only evidence for why PCM FOV ignores the view target.
    // Wukong's manager is ABGPPlayerCameraManager, whose UpdateCamera runs a
    // game-script UpdateCameraCS. This class only reads reflection metadata,
    // managed field values and native getters; it never writes camera state.
    internal sealed class CameraManagerDiagnostics
    {
        private static readonly BindingFlags Declared=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
        private static readonly int MaxMembers=80;
        private static readonly int MaxObjects=12;
        private double enabledAt=-1;
        private int snapshots;

        internal void Reset(){enabledAt=-1;snapshots=0;}

        // Snapshot at about 2s and 15s after MC authority starts, so the manager
        // has updated at least once with the native CameraActor as view target.
        internal void Update(double now,APlayerController controller,AActor cameraActor,UCameraComponent camera,float desired)
        {
            if(snapshots>=2)return;
            if(enabledAt<0){enabledAt=now;return;}
            if(now<enabledAt+(snapshots==0?2:15))return;
            snapshots++;
            try{Snapshot(controller,cameraActor,camera,desired,snapshots==1);}
            catch(Exception e){Log.Write("camera-manager-error",e.GetType().Name+": "+e.Message);}
        }

        private static void Snapshot(APlayerController controller,AActor cameraActor,UCameraComponent camera,float desired,bool full)
        {
            var manager=controller.PlayerCameraManager;
            if(manager==null||manager.IsDestroyed||manager.IsPendingKill){Log.Write("camera-manager","no live manager");return;}
            AActor pcmTarget=null;try{pcmTarget=UGSE_CameraUtilFuncLib.GetCameraViewTarget(manager);}catch(Exception e){Log.Write("camera-manager-error","GetCameraViewTarget: "+e.Message);}
            AActor pcTarget=controller.GetViewTarget();
            var bgp=manager as ABGPPlayerCameraManager;
            AActor csTarget=null,csPending=null;
            if(bgp!=null){try{csTarget=bgp.GetViewTargetCS();csPending=bgp.GetPendingViewTargetCS();}catch(Exception e){Log.Write("camera-manager-error","ViewTargetCS: "+e.Message);}}
            Log.Write("camera-manager","snapshot="+(full?"full":"repeat")+" desiredH="+F(desired)+" pcmH="+F(manager.GetFOVAngle())+" defaultH="+F(manager.DefaultFOV)
                +" componentH="+F(camera==null?float.NaN:camera.FieldOfView)+" componentActive="+(camera!=null&&camera.IsActive())
                +" pcTargetIsBridge="+(pcTarget==cameraActor)+" pcmTargetIsBridge="+(pcmTarget==cameraActor)+" csTargetIsBridge="+(csTarget==cameraActor)
                +" pcmTarget="+Name(pcmTarget)+" csTarget="+Name(csTarget)+" csPending="+Name(csPending));
            ThirdPersonCameras();
            if(!full)return;
            Log.Write("camera-manager","native class="+manager.GetClass().GetPathName()+" object="+manager.GetPathName());
            var chain=new StringBuilder();
            for(Type t=manager.GetType();t!=null;t=t.BaseType){
                if(chain.Length>0)chain.Append(" <- ");
                chain.Append(t.FullName).Append(" [").Append(t.Assembly.GetName().Name).Append(']');
            }
            Log.Write("camera-manager","managed chain="+chain);
            for(Type t=manager.GetType();t!=null&&t!=typeof(APlayerCameraManager);t=t.BaseType)DescribeType(t,manager);
        }

        private static void ThirdPersonCameras()
        {
            int count=0;
            foreach(ABGU3rdCameraBase rig in UObject.GetObjects<ABGU3rdCameraBase>()){
                if(rig==null||rig.IsDestroyed)continue;
                if(++count>MaxObjects)break;
                var follow=rig.FollowCamera;
                Log.Write("camera-rig","rig="+rig.GetPathName()+" owner="+Name(rig.GetOwner())
                    +" followH="+F(follow==null?float.NaN:follow.FieldOfView)+" followActive="+(follow!=null&&follow.IsActive())
                    +" followAspect="+F(follow==null?float.NaN:follow.AspectRatio)+" constrain="+(follow!=null&&follow.ConstrainAspectRatio));
            }
            if(count==0)Log.Write("camera-rig","no ABGU3rdCameraBase instances");
        }

        private static void DescribeType(Type t,object instance)
        {
            int members=0;
            foreach(var method in t.GetMethods(Declared)){
                if(++members>MaxMembers){Log.Write("camera-manager-type",t.FullName+" methods truncated");break;}
                Log.Write("camera-manager-type",t.FullName+" M "+(method.IsStatic?"static ":"")+(method.IsVirtual?"virtual ":"")+method.Name+"("+method.GetParameters().Length+")");
            }
            members=0;
            foreach(var field in t.GetFields(Declared)){
                if(++members>MaxMembers){Log.Write("camera-manager-type",t.FullName+" fields truncated");break;}
                Log.Write("camera-manager-type",t.FullName+" F "+field.FieldType.Name+" "+field.Name+"="+Value(field,instance));
            }
            members=0;
            foreach(var property in t.GetProperties(Declared)){
                if(++members>MaxMembers){Log.Write("camera-manager-type",t.FullName+" properties truncated");break;}
                // Names/types only: property getters may call native or script code.
                Log.Write("camera-manager-type",t.FullName+" P "+property.PropertyType.Name+" "+property.Name);
            }
        }

        private static string Value(FieldInfo field,object instance)
        {
            try{
                object value=field.GetValue(field.IsStatic?null:instance);
                if(value==null)return "null";
                var type=value.GetType();
                if(value is float)return F((float)value);
                if(value is double)return F((double)value);
                if(type.IsPrimitive||type.IsEnum)return Convert.ToString(value,CultureInfo.InvariantCulture);
                var text=value as string;if(text!=null)return text.Length>80?text.Substring(0,80)+"...":text;
                var obj=value as UObject;if(obj!=null)return obj.IsDestroyed?"<destroyed>":obj.GetPathName();
                return "<"+type.Name+">";
            }catch(Exception e){return "<"+e.GetType().Name+">";}
        }

        private static string Name(AActor actor)
        {
            try{return actor==null?"null":actor.IsDestroyed?"<destroyed>":actor.GetClass().GetName()+":"+actor.GetName();}
            catch(Exception e){return "<"+e.GetType().Name+">";}
        }
        private static string F(double value){return value.ToString("0.######",CultureInfo.InvariantCulture);}
    }
}
