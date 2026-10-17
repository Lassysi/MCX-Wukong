using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DqjCombat;
using UnrealEngine.Engine;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
    private static PrologueSkip Setup(bool enabled=true)
    {
        if(Directory.Exists(Log.Dir))Directory.Delete(Log.Dir,true);
        Directory.CreateDirectory(Log.Dir);if(enabled)File.WriteAllText(Path.Combine(Log.Dir,"skip-prologue.on"),"");
        Test.Calls.Clear();Test.World=new UWorld();Test.State=new APlayerState{World=Test.World};Test.Controller=new APlayerController();
        Test.Role=new b1.BPC_PlayerRoleData();Test.Prologue=9;Test.Target=10;Test.Current=9;Test.Movie=false;Test.Teleporting=false;
        Test.PlayGo=new b1.BGW_PlayGoMgr();Test.Trans=new b1.BPC_TransData();Test.Pending=null;Test.ReplyCode=ResB1.MsgErrCode.Ok;
        Test.Advance=true;Test.AutoReply=true;Test.Events=new b1.BPS_GSEventCollection();
        Test.BattleEvents=new b1.BTF_EventCollectionCS();Test.WorldEvents=new b1.BGW_EventCollection();
        return new PrologueSkip();
    }
    private static bool Tick(PrologueSkip skip,double now){return skip.Update(now,Test.World,()=>Test.Calls.Add("release"));}
    private static void Submit(PrologueSkip skip){Tick(skip,1);Tick(skip,3.1);}
    private static void Main()
    {
        var disabled=Setup(false);Test.Movie=true;Check(!Tick(disabled,1)&&Test.Calls.Count==0,"disabled feature has no native side effects");
        var later=Setup();Test.Current=10;Test.Role.RoleData.RoleCs.Chapter.CurChapter=1;Test.Movie=true;
        Check(!Tick(later,1)&&Test.Calls.Count==0&&Test.Movie,"first-chapter CG is left playing");
        var old=Setup();Test.Role.RoleData.RoleCs.Chapter.CurChapter=1;
        Check(!Tick(old,1)&&Test.Calls.Count==0,"completed save in prologue map is not submitted again");
        var missing=Setup();Test.Role=null;Check(!Tick(missing,1)&&Test.Calls.Count==0,"unready role data is not mutated");
        var download=Setup();Test.PlayGo.Incomplete=true;Check(!Tick(download,1)&&Test.Calls.Count==0,"incomplete game download blocks chapter travel");
        var busy=Setup();Test.Teleporting=true;Check(!Tick(busy,1)&&Test.Calls.Count==0,"existing native travel retains authority");
        var normal=Setup();Test.Movie=true;
        Check(Tick(normal,1)&&string.Join(",",Test.Calls)=="release,force-skip,skip","bridge releases before native opening CG skip");
        Tick(normal,1.3);Check(!Test.Calls.Contains("request"),"completion waits after the CG ends");
        Tick(normal,3.4);Check(Test.Calls.Count(x=>x=="request")==1&&!Test.Calls.Contains("travel"),"native request precedes travel on a later tick");
        Tick(normal,3.8);
        Check(string.Join(",",Test.Calls.TakeLast(4))=="post,rebirth:1001,actor:0,travel","native completion side effects preserve exact original order and arguments");
        Tick(normal,4.2);Check(Test.Calls.Count(x=>x=="travel")==1&&Test.Calls.Count(x=>x=="request")==1,"native request and travel are each dispatched once");
        Test.World=new UWorld();Test.Current=10;Test.State=new APlayerState{World=Test.World};Test.Movie=true;
        Check(!Tick(normal,5)&&Test.Movie,"destination world resumes normal story CG policy");
        var rejected=Setup();Test.ReplyCode=ResB1.MsgErrCode.Rejected;Submit(rejected);Tick(rejected,4);Tick(rejected,40);
        Check(!Test.Calls.Contains("post")&&!Test.Calls.Contains("travel")&&Test.Calls.Count(x=>x=="request")==1,"rejected native transaction does not travel or retry");
        var preflight=Setup();Test.Events.Evt_BPS_TeleportTo=null;Submit(preflight);
        Check(!Test.Calls.Contains("request")&&!Test.Calls.Contains("rebirth:1001"),"missing native finish event is detected before save transaction");
        var timeout=Setup();Test.AutoReply=false;Submit(timeout);var lateReply=Test.Pending;Tick(timeout,34);lateReply(ResB1.MsgErrCode.Ok,new CsB1.CSMsgChapterPrologueCompletedReq(),new CsB1.CSMsgChapterPrologueCompletedRes());Tick(timeout,35);
        Check(!Test.Calls.Contains("travel")&&Test.Calls.Count(x=>x=="request")==1,"timeout and late success do not cause travel or duplicate mutation");
        var sync=Setup();Test.Advance=false;Submit(sync);Tick(sync,4);
        Check(!Test.Calls.Contains("travel"),"successful reply waits for chapter data synchronization");
        Test.Role.RoleData.RoleCs.Chapter.CurChapter=1;Tick(sync,4.5);
        Check(Test.Calls.Contains("travel"),"synchronized chapter then permits native travel");
        var noAdvance=Setup();Test.Advance=false;Submit(noAdvance);Tick(noAdvance,4);Tick(noAdvance,10);
        Check(!Test.Calls.Contains("travel"),"success without chapter advancement never teleports");
        var stale=Setup();Test.AutoReply=false;Submit(stale);var staleReply=Test.Pending;
        Test.World=new UWorld();Test.State=new APlayerState{World=Test.World};Tick(stale,4);Tick(stale,6.1);
        staleReply(ResB1.MsgErrCode.Ok,new CsB1.CSMsgChapterPrologueCompletedReq(),new CsB1.CSMsgChapterPrologueCompletedRes());Tick(stale,6.5);
        Check(!Test.Calls.Contains("travel"),"callback from the previous world cannot complete the new world transaction");
        var stateChanged=Setup();Test.AutoReply=false;Submit(stateChanged);Test.Role.RoleData.RoleCs.Chapter.CurChapter=1;
        Test.Pending(ResB1.MsgErrCode.Ok,new CsB1.CSMsgChapterPrologueCompletedReq(),new CsB1.CSMsgChapterPrologueCompletedRes());Test.State=new APlayerState{World=Test.World};Tick(stateChanged,4);
        Check(!Test.Calls.Contains("travel"),"replaced player state rejects stale native completion");
        Directory.Delete(Log.Dir,true);Console.WriteLine("PROLOGUE NATIVE FLOW PASS checks="+checks);
    }
}

internal static class Test
{
    internal static readonly List<string> Calls=new List<string>();
    internal static UWorld World;internal static APlayerState State;internal static APlayerController Controller;
    internal static b1.BPC_PlayerRoleData Role;internal static b1.BPC_TransData Trans;
    internal static b1.BGW_PlayGoMgr PlayGo;internal static b1.BPS_GSEventCollection Events;
    internal static b1.BTF_EventCollectionCS BattleEvents;internal static b1.BGW_EventCollection WorldEvents;
    internal static int Prologue,Target,Current;internal static bool Movie,Teleporting,AutoReply,Advance;
    internal static ResB1.MsgErrCode ReplyCode;
    internal static Action<ResB1.MsgErrCode,CsB1.CSMsgChapterPrologueCompletedReq,CsB1.CSMsgChapterPrologueCompletedRes> Pending;
}
namespace DqjCombat
{
    internal static class Log{internal static string Dir=Path.Combine(Environment.CurrentDirectory,".work/preview97-build/flow-fixture");internal static void Write(string tag,string value){} }
    internal static class GameApi
    {
        internal static APlayerController Controller(UWorld world){return Test.Controller;}
        internal static bool NativeTeleporting(UWorld world){return Test.Teleporting;}
        internal static bool MoviePlaying(UWorld world,out bool canSkip){canSkip=false;return Test.Movie;}
        internal static void ForceSkippable(UWorld world){Test.Calls.Add("force-skip");}
    }
}
namespace UnrealEngine.Engine
{
    public sealed class UWorld{}
    public sealed class APlayerState{public bool IsDestroyed,IsPendingKill;public UWorld World;}
    public sealed class APlayerController{}
    public static class UGameplayStatics{public static APlayerState GetPlayerState(UWorld world,int index){return Test.State;}}
}
namespace ResB1{public enum MsgErrCode{Ok=0,Rejected=1}public enum CommCfgType{DefaultChapterId} }
namespace CsB1{public sealed class CSMsgChapterPrologueCompletedReq{}public sealed class CSMsgChapterPrologueCompletedRes{} }
namespace b1
{
    public static class BGUFuncLibMap
    {
        public static int GetCurLevelId(UWorld w){return Test.Current;}
        public static int GetPrologueLevelId(UWorld w){return Test.Prologue;}
        public static int GetAfterPrologueLevelId(UWorld w){return Test.Target;}
    }
    public sealed class Chapter{public int CurChapter;}
    public sealed class RoleCs{public Chapter Chapter=new Chapter();}
    public sealed class RoleData{public RoleCs RoleCs=new RoleCs();}
    public sealed class BPC_PlayerRoleData{public RoleData RoleData=new RoleData();}
    public sealed class BPC_TransData{public void SetCurActorResId(int id){Test.Calls.Add("actor:"+id);}}
    public static class BGU_DataUtil
    {
        public static T GetReadOnlyData<T>(object actor) where T:class
        {return typeof(T)==typeof(BPC_PlayerRoleData)?Test.Role as T:Test.Trans as T;}
    }
    public sealed class ChapterDesc{public int NextChapterId=1;}
    public static class GameDBRuntime
    {
        public static int GetCommLogicCfgValue(ResB1.CommCfgType kind){return 0;}
        public static ChapterDesc GetChapterDesc(int id){return new ChapterDesc();}
    }
    public sealed class BGW_PlayGoMgr{public bool Incomplete;public static BGW_PlayGoMgr Get(UWorld w){return Test.PlayGo;}public bool IsDownloadIncomplete(){return Incomplete;}}
    public static class BGUFunctionLibraryCS{public static void SkipCurrentSequence(UWorld w){Test.Calls.Add("skip");Test.Movie=false;}}
    public enum ETeleportTypeV2{PrologueCompleted}
    public enum EPlayerTeleportReason{PrologueCompleted}
    public sealed class BTF_EventCollectionCS
    {
        public static BTF_EventCollectionCS Get(APlayerState ps){return Test.BattleEvents;}
        public Action<CsB1.CSMsgChapterPrologueCompletedReq,Action<ResB1.MsgErrCode,CsB1.CSMsgChapterPrologueCompletedReq,CsB1.CSMsgChapterPrologueCompletedRes>,APlayerState,bool> Evt_ChapterPrologueCompletedReq=
            (req,reply,ps,all)=>{Test.Calls.Add("request");Test.Pending=reply;if(Test.AutoReply){if(Test.Advance&&Test.ReplyCode==0)Test.Role.RoleData.RoleCs.Chapter.CurChapter=1;reply(Test.ReplyCode,req,new CsB1.CSMsgChapterPrologueCompletedRes());}};
    }
    public sealed class BPS_GSEventCollection
    {
        public Action<int> Evt_ForceSetRebirthPoint=id=>Test.Calls.Add("rebirth:"+id);
        public Action<ETeleportTypeV2,ValueType,EPlayerTeleportReason> Evt_BPS_TeleportTo=(type,param,reason)=>Test.Calls.Add("travel");
    }
    public static class BPS_EventCollectionCS{public static BPS_GSEventCollection Get(APlayerState ps){return Test.Events;}}
    public sealed class BGW_EventCollection
    {
        public static BGW_EventCollection Get(UWorld w){return Test.WorldEvents;}
        public Action Evt_PostPrologueCompleted=()=>Test.Calls.Add("post");
    }
}
