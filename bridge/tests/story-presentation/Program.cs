using System;
using System.Collections.Generic;
using DqjCombat;
using UnrealEngine.Engine;

internal static class Test
{
    internal static bool Movie=true;
    internal static int Created,Disposed;
    internal static readonly List<USkeletalMeshComponent> Components=new List<USkeletalMeshComponent>();
    internal static readonly List<SteveSkin> Skins=new List<SteveSkin>();
    private static int checks;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    private static AActor Actor(UWorld world,string type,bool biped=true,bool player=false)
    {
        var a=player?new b1.BGUPlayerCharacterCS():new AActor();a.World=world;a.Type=type;a.Name=type+"_"+Components.Count;
        Components.Add(new USkeletalMeshComponent{Owner=a,Supported=biped});return a;
    }
    private static int Main()
    {
        var world=new UWorld();var controller=new APlayerController();var session=new StoryPresentation();
        Actor(world,"Unit_Player_Chan_C",false,true);
        var hero=Actor(world,"LSP_player_wukong_C");
        Actor(world,"LSP_tudi_C");Actor(new UWorld(),"LSP_player_wukong_C");
        session.Update(1,world,controller,null,false);
        Check(Created==1&&Skins[0].Owner==hero,"null/changed primary pawn must not stop independent CG hero replacement");
        session.Update(2,world,controller,null,false);
        Check(Created==1,"repeated world ticks cannot attach the same actor twice");
        session.Update(3,world,controller,null,true);
        Check(!Skins[0].Cinematic&&Skins[0].NativeView,"small interaction Movie remains visible at normal scale");
        session.ReleaseOwner(hero);
        Check(Disposed==1,"primary-skin handoff releases CG lease before another owner attaches");
        session.Update(4,world,controller,hero,false);
        Check(Created==1,"ordinary primary owner is excluded from duplicate CG construction");
        session.Update(5,world,controller,null,false);
        Check(Created==2,"independent skin can reattach after primary possession ends");
        hero.IsDestroyed=true;session.Update(6,world,controller,null,false);
        Check(Disposed==2,"destroyed CG actor releases its parts");
        var generic=Actor(world,"GenericSequenceActor_C");b1.BGU_DataUtil.Data.CameraMovieInstance.Bind("Player",generic);
        session.Update(7,world,controller,null,false);
        Check(Created==3&&Skins[2].Owner==generic,"explicit native Player binding covers generic sequence actors");
        Movie=false;session.Update(8,world,controller,null,false);
        Check(Disposed==3,"CG completion restores all independent skin leases");
        Movie=true;session.Update(9,world,controller,null,false);session.Update(10,new UWorld(),controller,null,false);
        Check(Created==4&&Disposed==4,"world changes discard old appearance ownership");
        session.Clear();session.Clear();Check(Disposed==4,"loading/unload cleanup is idempotent");
        // Preview102: an unnamed sequence copy carrying Wukong's mesh is the hero; a same-world NPC with another mesh is not.
        session.Clear();int before=Created;var w2=new UWorld();
        var copy=Actor(w2,"SkeletalMeshActor");Components[Components.Count-1].SkeletalMesh.Path="/Game/00Main/Art/Character/Player/Wukong/SK_Wukong";
        var npc=Actor(w2,"SkeletalMeshActor");
        session.Update(11,w2,controller,null,false);
        Check(Created==before+1&&Skins[Skins.Count-1].Owner==copy,"Preview102: Wukong-mesh sequence copy replaced, other skeletal actors kept native");
        var heroChar=new ACharacter{World=w2,Type="Unit_Player_Wukong_C",Name="hero"};var heroMesh=new USkeletalMesh{Skeleton=new USkeleton()};
        heroChar.Mesh=new USkeletalMeshComponent{Owner=heroChar,Supported=true,SkeletalMesh=heroMesh};
        var sameSkeleton=Actor(w2,"SkeletalMeshActor");Components[Components.Count-1].SkeletalMesh=new USkeletalMesh{Skeleton=heroMesh.Skeleton};
        session.Update(12,w2,controller,heroChar,false);
        Check(Skins[Skins.Count-1].Owner==sameSkeleton,"Preview102: copy on the controlled hero's skeleton is replaced");
        Console.WriteLine("STORY WORLD SESSION LINKED-CODE PASS checks="+checks+" (stub native actors; no visual acceptance)");return 0;
    }
}

namespace DqjCombat
{
    internal static class GameApi{internal static bool MoviePlaying(object context,out bool canSkip){canSkip=false;return Test.Movie;}}
    internal static class Log{internal static string Dir="stub";internal static void Write(string a,string b){}}
    internal sealed class SteveSkin
    {
        internal AActor Owner;internal bool Cinematic,NativeView;private bool active=true;
        internal bool Live=>active&&!Owner.IsDestroyed&&!Owner.IsPendingKill;
        internal SteveSkin(AActor a,USkeletalMeshComponent b,string c,bool d,bool cinematic){Owner=a;Cinematic=cinematic;Test.Created++;Test.Skins.Add(this);}
        internal static bool SupportsMesh(USkeletalMeshComponent c)=>c.Supported;
        internal void SetCinematic(bool value){Cinematic=value;}internal void SetNativeView(bool value){NativeView=value;}
        internal void Reveal(bool value){}internal void Update(){}
        internal void Dispose(){if(active){active=false;Test.Disposed++;}}
    }
}
namespace UnrealEngine.Runtime
{
    public class UObject{public IntPtr Address=new IntPtr(1);public bool IsDestroyed,IsPendingKill;public static IEnumerable<T> GetObjects<T>(){foreach(var c in Test.Components)if(c is T t)yield return t;}}
    public struct FName{private string value;public FName(string s){value=s;}public override string ToString()=>value;}
}
namespace UnrealEngine.InputCore{public struct FKey{}}
namespace UnrealEngine.Engine
{
    public class UWorld:UnrealEngine.Runtime.UObject{}
    public class AActor:UnrealEngine.Runtime.UObject{public UWorld World;public string Name,Type;public string GetName()=>Name;public ActorClass GetClass()=>new ActorClass(Type);}
    public sealed class ActorClass{private string name;public ActorClass(string n){name=n;}public string GetName()=>name;}
    public class USkeletalMeshComponent:UnrealEngine.Runtime.UObject{public AActor Owner;public bool Supported;public USkeletalMesh SkeletalMesh=new USkeletalMesh();public bool HiddenInGame;public AActor GetOwner()=>Owner;public int GetNumBones()=>Supported?100:47;}
    public class USkeleton:UnrealEngine.Runtime.UObject{}
    public class USkeletalMesh:UnrealEngine.Runtime.UObject{public USkeleton Skeleton;public string Path="/Game/Stub/NPC";public string GetPathName()=>Path;}
    public class ACharacter:AActor{public USkeletalMeshComponent Mesh;}
    public class APlayerController:UnrealEngine.Runtime.UObject{public bool IsInputKeyDown(UnrealEngine.InputCore.FKey key)=>false;}
}
namespace b1
{
    public enum EUIPageID{CommSkip=45}
    public class BGUPlayerCharacterCS:AActor{}
    public interface IBGC_MovieData{}
    public class BGC_MovieData:IBGC_MovieData{public MovieInstance CameraMovieInstance=new MovieInstance();}
    public class MovieInstance{public int MovieId=>1;private Dictionary<string,AActor> Performers{get;}=new Dictionary<string,AActor>();public void Bind(string role,AActor a){Performers[role]=a;}}
    public static class BGU_DataUtil{public static BGC_MovieData Data=new BGC_MovieData();public static T GetGameStateReadonlyData<T,U>(object world)=> (T)(object)Data;}
    public static class UGSE_UMGFuncLib{public static UnrealEngine.InputCore.FKey GetKeyWithName(UnrealEngine.Runtime.FName name)=>new UnrealEngine.InputCore.FKey();}
}
namespace B1UI
{
    public static class GSG{public static Pages GSPageOP=new Pages();}
    public sealed class Pages{public object FindUIPage(int id)=>null;}
}
namespace B1UI.GSUI
{
    public enum ESkipType{Sequence=1}
    public static class GSB1UIUtil{public static void ActiveCommSkip(bool a,ESkipType b,bool c){throw new Exception("Unpermitted skip UI activation");}}
}
