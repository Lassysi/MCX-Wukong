using System;
using System.Collections.Generic;
using DqjCombat;
using LitJson;

internal static class Program
{
    private static void Equal(List<int> actual,params int[] expected)
    {if(string.Join(",",actual)!=string.Join(",",expected))throw new Exception("Unexpected native IDs: "+string.Join(",",actual));}
    private static int Main()
    {
        var player=new b1.BGUPlayerCharacterCS();
        B1UI.GSG.BattleLogicSvc=new CurrentService();
        Equal(NativeTravelCatalogue.Read(player),17,18);
        B1UI.GSG.BattleLogicSvc=new OptionalService();
        Equal(NativeTravelCatalogue.Read(player),19);
        B1UI.GSG.BattleLogicSvc=new MissingService();
        Equal(NativeTravelCatalogue.Read(player),1);
        B1UI.GSG.BattleLogicSvc=null;
        Equal(NativeTravelCatalogue.Read(player),1);
        B1UI.GSG.BattleLogicSvc=new InvalidService();
        bool rejected=false;try{NativeTravelCatalogue.Read(player);}catch(InvalidOperationException){rejected=true;}
        if(!rejected)throw new Exception("Non-ID native result was accepted");
        if(NativeTravelText.Read("temple-1")!="前山土地庙")throw new Exception("Native name failed without SDK ToFText");
        if(NativeTravelText.Read(typeof(StringLocalization),"temple-1")!="Native string name")throw new Exception("String localization fallback failed");
        B1UI.GSG.BattleLogicSvc=new MissingService();
        int suspended=0,aborted=0;var wire=new McBridgeWire();
        var menu=new McNativeTravel(player,wire,()=>suspended++,()=>aborted++);
        menu.Open();
        if(wire.Messages.Count!=2)throw new Exception("Whole native menu path failed");
        var open=JsonMapper.ToObject(wire.Messages[0]);var entries=JsonMapper.ToObject(wire.Messages[1]);
        if((int)open["count"]!=1||(int)entries["entries"][0]["id"]!=1||(string)entries["entries"][0]["name"]!="前山土地庙")throw new Exception("Native menu row mismatch");
        if(b1.MockSignal.Invocations!=0||suspended!=0)throw new Exception("Menu preflight executed teleport");
        var request=new JsonData();request["t"]="travel_select";request["token"]=(string)open["token"];request["id"]=2;
        menu.Handle(request,true);
        if(b1.MockSignal.Invocations!=0||suspended!=0)throw new Exception("Locked destination was executed");
        request["id"]=1;menu.Handle(request,true);
        if(b1.MockSignal.Invocations!=1||b1.MockSignal.LastId!=1||suspended!=1||aborted!=0)throw new Exception("Validated native selection failed");
        menu.Open();open=JsonMapper.ToObject(wire.Messages[wire.Messages.Count-2]);
        request["token"]=(string)open["token"];b1.MockSignal.Fail=true;menu.Handle(request,true);
        if(suspended!=2||aborted!=1)throw new Exception("Native event failure did not undo pending travel");
        var pages=new MockPages();int handoffs=0;var nativeMap=new NativeMapUi(()=>pages,2,12);
        nativeMap.Open(100,()=>handoffs++);
        if(!nativeMap.Active||handoffs!=1||pages.Button.Calls!=1||!nativeMap.Update(100.1))throw new Exception("Native map open failed");
        nativeMap.Open(100.2,()=>handoffs++);
        if(handoffs!=1||pages.Button.Calls!=1)throw new Exception("Repeated F6 duplicated native map entry");
        pages.Shown=false;
        if(nativeMap.Update(101)||nativeMap.Active)throw new Exception("Native map close retained ownership");
        pages.Shown=true;
        if(!nativeMap.Adopt(102,()=>handoffs++)||handoffs!=2||pages.Button.Calls!=1)throw new Exception("M input adoption replayed native entry");
        nativeMap.Loading();pages.Shown=false;
        if(!nativeMap.LoadingObserved||nativeMap.Update(103))throw new Exception("Native loading did not end UI hold");
        pages.Button.OpenEnabled=false;nativeMap.Open(104,()=>handoffs++);
        if(!nativeMap.Update(108)||nativeMap.Update(110))throw new Exception("Denied native entry did not recover after bounded wait");
        var incompatible=new MockPages{Button=null};var unavailable=new NativeMapUi(()=>incompatible,2,12);
        bool incompatibleRejected=false;try{unavailable.Open(111,()=>handoffs++);}catch(InvalidOperationException){incompatibleRejected=true;}
        if(!incompatibleRejected||handoffs!=3||unavailable.Active)throw new Exception("Unavailable native page changed input owner");
        var cursor=new NativeUiCursorLease();bool restored=true;
        cursor.RememberGameplay(false,false);
        // Native M/ESC has already made the current cursor true before adoption.
        cursor.Begin(false,true);cursor.End(value=>restored=value);
        if(restored)throw new Exception("Menu cursor leaked into the return state");
        cursor.RememberGameplay(true,true);cursor.Begin(true,true);
        cursor.RememberGameplay(true,false);cursor.End(value=>restored=value);
        if(restored)throw new Exception("Inventory or active native menu overwrote gameplay baseline");
        int restores=0;cursor.End(value=>restores++);
        if(restores!=0)throw new Exception("Cursor return was not idempotent");
        var originalTrue=new NativeUiCursorLease();originalTrue.RememberGameplay(true,false);
        originalTrue.Begin(false,false);originalTrue.End(value=>restored=value);
        if(!restored)throw new Exception("Explicit pre-menu true cursor baseline was not preserved");
        var firstMenu=new NativeUiCursorLease();firstMenu.Begin(false,true);firstMenu.End(value=>restored=value);
        if(restored)throw new Exception("An unobserved gameplay cursor inherited the current native menu");
        var childPages=new MockPages();var layered=new NativeMapUi(()=>childPages,2,12,33,27);
        childPages.Shown=true;layered.Adopt(120,()=>{});childPages.Shown=false;childPages.Child=33;
        if(!layered.Update(121,false))throw new Exception("Settings child released native ownership");
        childPages.Child=27;if(!layered.Update(122,false))throw new Exception("Confirm child released native ownership");
        childPages.Child=0;if(!layered.Update(123,false))throw new Exception("Closing fade resumed before native gameplay readiness");
        if(layered.Update(124,true)||layered.Active)throw new Exception("Ready gameplay did not finish native menu ownership");
        var mapPages=new MockPages{Child=113};B1UI.GSG.GSPageOP=mapPages;
        var currentMap=new NativeMapUi();
        if(!currentMap.Adopt(125,()=>{})||!currentMap.Update(130))throw new Exception("Runtime MapMain page was missed by old RoleMain watcher");
        mapPages.Child=0;if(currentMap.Update(131))throw new Exception("Runtime MapMain close was not observed");
        mapPages.Child=114;
        if(!currentMap.Adopt(132,()=>{})||!currentMap.ChapterTransition)throw new Exception("ChapterMovie must retain native chapter ownership");
        mapPages.Child=0;
        if(!currentMap.Update(140,true)||!currentMap.Update(180,true))throw new Exception("Chapter movie/award gap must not reattach the old pawn, even after 45 seconds");
        mapPages.Child=115;
        if(!currentMap.Update(181,true))throw new Exception("ChapterAward must retain native control");
        mapPages.Child=0;currentMap.Loading();
        if(!currentMap.Update(182,false))throw new Exception("Loading must not release chapter hold before native gameplay readiness");
        if(currentMap.Update(183,true)||currentMap.ChapterTransition)throw new Exception("Ready destination after loading must release chapter hold");
        mapPages.Child=115;currentMap.Adopt(184,()=>{});currentMap.Clear();
        if(currentMap.Active||currentMap.ChapterTransition)throw new Exception("New world/session must discard old chapter UI hold");
        var nativeState=new NativeShrineState();int nativeInputs=0,shrineSuspends=0,shrineMaps=0,menuReturns=0;
        var shrineSession=new NativeShrineSession(_=>nativeState,()=>{menuReturns++;return true;},()=>shrineMaps++);
        if(shrineSession.Start(200,()=>shrineSuspends++,()=>nativeInputs++))throw new Exception("No-target native interaction was accepted");
        nativeState=new NativeShrineState{Id=55,SelectedShrine=true,Busy=true};
        if(shrineSession.Start(200,()=>shrineSuspends++,()=>nativeInputs++))throw new Exception("Busy native interaction was restarted");
        nativeState.Busy=false;nativeState.Activated=true;
        if(!shrineSession.Start(201,()=>shrineSuspends++,()=>nativeInputs++)||shrineMaps!=0||nativeInputs!=1||shrineSuspends!=1)throw new Exception("Activated shrine did not dispatch its native rest action");
        nativeState.Busy=true;
        if(!shrineSession.Update(201.1,true))throw new Exception("Native rest animation was interrupted");
        nativeState.Busy=false;
        if(shrineSession.Update(201.2,true)||!shrineSession.Rested)throw new Exception("Completed rest did not notify host");
        shrineSession.Rested=false;nativeInputs=0;shrineSuspends=0;
        nativeState.Activated=false;
        shrineSession.Start(202,()=>shrineSuspends++,()=>{nativeInputs++;nativeState.Busy=true;});
        shrineSession.Start(202.1,()=>shrineSuspends++,()=>nativeInputs++);
        if(nativeInputs!=1||shrineSuspends!=1||!shrineSession.Update(202.2,false))throw new Exception("Native activation start/repeat handling failed");
        nativeState.Activated=true;
        if(!shrineSession.Update(203,false)||menuReturns!=0||!shrineSession.Update(203.1,false)||menuReturns!=0)throw new Exception("Native shrine menu was closed automatically or activation cut short");
        nativeState.Busy=false;if(!shrineSession.Update(204,false))throw new Exception("MC resumed before native interaction readiness");
        if(shrineSession.Update(205,true)||shrineSession.Active||shrineMaps!=0||!shrineSession.Rested)throw new Exception("Confirmed activation did not complete while leaving map choice to the player");
        shrineSession.Rested=false;
        nativeState.Activated=false;
        shrineSession.Start(206,()=>shrineSuspends++,()=>{nativeInputs++;nativeState.Busy=true;});
        shrineSession.Update(206.1,false);nativeState.Busy=false;
        if(shrineSession.Update(207,true)||shrineMaps!=0)throw new Exception("Canceled interaction opened map or remained active");
        shrineSession.Start(208,()=>shrineSuspends++,()=>nativeInputs++);
        if(!shrineSession.Update(208.4,true)||shrineSession.Update(209,true))throw new Exception("Refused native interaction did not release after grace period");
        shrineSession.Rested=false;
        foreach(string target in new[]{"material","chest","story mechanism"}){
            nativeState=new NativeShrineState{SelectedInteractable=true,Diagnostic=target};
            int before=nativeInputs;
            if(!shrineSession.Start(210,()=>shrineSuspends++,()=>{nativeInputs++;nativeState.Busy=true;}))throw new Exception(target+" was rejected by the shrine-only gate");
            shrineSession.Start(210.1,()=>shrineSuspends++,()=>nativeInputs++);
            if(nativeInputs!=before+1||!shrineSession.Update(210.2,true))throw new Exception(target+" duplicated input or lost native ownership");
            nativeState.Busy=false;
            if(!shrineSession.Update(211,false)||shrineSession.Update(212,true)||shrineSession.Active||shrineSession.Rested||shrineMaps!=0)throw new Exception(target+" changed rest/map state or resumed before native UI was ready");
        }
        nativeState=new NativeShrineState{SelectedInteractable=true};
        shrineSession.Start(213,()=>{},()=>{});
        if(!shrineSession.Update(213.4,true)||shrineSession.Update(214,true)||shrineSession.Rested)throw new Exception("Refused general interaction did not recover cleanly");
        bool inputFailed=false;
        try{shrineSession.Start(215,()=>{},()=>{throw new InvalidOperationException("native input failure");});}catch(InvalidOperationException){inputFailed=true;}
        if(!inputFailed||shrineSession.Active)throw new Exception("Failed native input retained the ownership lease");
        // Exercise the formal adapter with an explicit IEntityRef implementation.
        var selectedShrine=new b1.BGURebirthPointBase{World=player.World};
        b1.UI.DS_IBPC_PlayerInteractData.Current.BestInteractEntityRef=new b1.EntitySharedRef{Ref=new b1.MockEntityRef(selectedShrine)};
        var emptyControllerData=NativeTravelCatalogue.ControllerData(player,typeof(b1.UI.DS_IBPC_PlayerInteractData)) as b1.UI.DS_IBPC_PlayerInteractData;
        if(emptyControllerData.BestInteractEntityRef!=null)throw new Exception("Fixture no longer reproduces the empty controller interaction store");
        var shrineAdapter=new NativeShrineAdapter(player);var selectedState=shrineAdapter.Read(0);
        if(!selectedState.SelectedShrine||!selectedState.SelectedInteractable||selectedState.Id!=55||selectedState.Activated)throw new Exception("Native selected shrine data read failed");
        if(b1.UI.B1GSUIActorMgr.LastInteractTag!=b1.UI.B1ActorTag.LocalPlayerState||selectedState.Diagnostic.IndexOf("dataOwner=LocalPlayerState",StringComparison.Ordinal)<0)throw new Exception("Interaction target came from the wrong native owner");
        b1.UI.B1GSUIActorMgr.PlayerStateReady=false;bool missingStateRejected=false;
        try{shrineAdapter.Read(0);}catch(InvalidOperationException){missingStateRejected=true;}
        finally{b1.UI.B1GSUIActorMgr.PlayerStateReady=true;}
        if(!missingStateRejected)throw new Exception("Missing native PlayerState interaction data was accepted");
        b1.UI.DS_IBPC_RebirthPointData.Activated.Add(55);
        b1.UI.DS_IBPC_PlayerInteractData.Current.BestInteractEntityRef=null;
        if(!shrineAdapter.Read(55).Activated)throw new Exception("Tracked native activation was lost when native selection cleared");
        b1.UI.DS_IBPC_PlayerInteractData.Current.BestInteractEntityRef=new b1.EntitySharedRef{Ref=new b1.MockEntityRef(new UnrealEngine.Engine.AActor{World=player.World})};
        var generalState=shrineAdapter.Read(0);
        if(generalState.SelectedShrine||!generalState.SelectedInteractable||generalState.Id!=0)throw new Exception("Native-selected non-shrine target was not accepted as a general interaction");
        selectedShrine.World=new UnrealEngine.Engine.UWorld();b1.UI.DS_IBPC_PlayerInteractData.Current.BestInteractEntityRef=new b1.EntitySharedRef{Ref=new b1.MockEntityRef(selectedShrine)};
        if(shrineAdapter.Read(0).SelectedShrine||shrineAdapter.Read(0).SelectedInteractable)throw new Exception("Other-world target was accepted");
        selectedShrine.World=player.World;selectedShrine.IsDestroyed=true;
        if(shrineAdapter.Read(0).SelectedInteractable)throw new Exception("Destroyed target was accepted");
        mapPages.Child=7;
        if(!shrineAdapter.LeaveActivatedShrineMenu()||mapPages.Child!=0)throw new Exception("Original native shrine Return action failed");
        Console.WriteLine("PASS: empty Controller interaction store reproduces Preview16; native interaction target requires LocalPlayerState; missing PlayerState rejects handoff; rebirth activation still uses PlayerController.");
        Console.WriteLine("PASS: native-selected gathering, chest and mechanism handoff; rest-only recovery; no-start/input-failure cleanup; live target/world validation; native shrine rest/menu retained; no automatic map; tracked activation survives target deselection.");
        Console.WriteLine("PASS: pre-menu cursor baseline survives native ESC/M adoption and MC inventory; idempotent cursor return; explicit true baseline preserved; settings/confirm/fade retain native ownership until gameplay readiness.");
        Console.WriteLine("PASS: original private map callback; F6 repeat suppression; manual M adoption without reentry; close/loading recovery; bounded denied-open recovery; missing page rejected before handoff.");
        Console.WriteLine("PASS: chapter movie/award gap retains native ownership beyond 45 seconds; loading waits for gameplay readiness; new world/session clears the old chapter latch.");
        Console.WriteLine("PASS: changed return type, optional argument, absent method/service, native active-minus-blocked descriptor filter, invalid result rejection.");
        Console.WriteLine("PASS: full Open/Handle path with SDK ToFText absent; native names and menu packets; preflight has no teleport side effect; unavailable destination rejected; native event success and failure recovery.");
        return 0;
    }
    private sealed class CurrentService
    {public HashSet<int> GetCanTeleportRebirthPointList(){return new HashSet<int>{17,18};}}
    private sealed class OptionalService
    {public List<int> GetCanTeleportRebirthPointList(bool includeBlocked=false){if(includeBlocked)throw new Exception("Native default was changed");return new List<int>{19};}}
    private sealed class MissingService {}
    private sealed class InvalidService
    {public List<string> GetCanTeleportRebirthPointList(){return new List<string>{"not a shrine ID"};}}
    private static class StringLocalization
    {public static string GSLocalizational(string value){return "Native string name";}}
    private sealed class MockPages
    {
        public bool Shown;public int Child;public MockMapButton Button;
        public MockPages(){Button=new MockMapButton(this);}
        public object FindUIPage(int id){return id==2?Button:id==7?new MockShrinePage(this):null;}
        public bool PageGraphRawIsUiPageShowIng(int id){return id==12&&Shown||id==Child&&Child!=0;}
    }
    private sealed class MockMapButton
    {
        private readonly MockPages owner;public int Calls;public bool OpenEnabled=true;
        public MockMapButton(MockPages owner){this.owner=owner;}
        private void OnClickOpenMapUI(){Calls++;if(OpenEnabled)owner.Shown=true;}
    }
    private sealed class MockShrinePage
    {
        private readonly MockPages owner;public MockShrinePage(MockPages owner){this.owner=owner;}
        public bool IsCanInputUnderToping(){return true;}
        private void OnUIActionBack(){owner.Child=0;}
    }
}

namespace UnrealEngine.Runtime {public class UObject {public IntPtr Address=new IntPtr(1);public bool IsDestroyed {get;set;} public bool IsPendingKill {get;set;}}}
namespace UnrealEngine.Engine
{
    public class AActor:UnrealEngine.Runtime.UObject
    {public UWorld World {get;set;}=new UWorld();}
    public class UWorld:UnrealEngine.Runtime.UObject{}
}
namespace b1
{
    internal enum EUIPageID {BattleMainCon=2,ShrineMain=7,RoleMain=12,MapMain=113,MapTips=32,ChapterMovie=114,ChapterAward=115}
    public sealed class BGUPlayerCharacterCS:UnrealEngine.Engine.AActor {}
    internal sealed class BGURebirthPointBase:UnrealEngine.Engine.AActor {}
    internal sealed class BUC_RebirthPointData {public int RebirthPointConfigID {get;set;}=55;}
    internal interface IEntityRef {UnrealEngine.Engine.AActor WorldObj {get;}}
    internal sealed class EntitySharedRef {public IEntityRef Ref {get;set;}}
    internal sealed class MockEntityRef:IEntityRef
    {private readonly UnrealEngine.Engine.AActor actor;public MockEntityRef(UnrealEngine.Engine.AActor actor){this.actor=actor;}UnrealEngine.Engine.AActor IEntityRef.WorldObj=>actor;}
    internal static class BGU_DataUtil
    {public static T GetUnPersistentReadOnlyData<T>(UnrealEngine.Engine.AActor actor) where T:class {return new BUC_RebirthPointData() as T;}}
    internal static class BGW_GameDB
    {
        internal static BtlB1.FUStRebirthPointDesc GetRebirthPointDesc(int id)
        {return id==4?null:new BtlB1.FUStRebirthPointDesc{ID=id,Name="temple-"+id,GroupAreaID=2,CanTeleport=id==3?0:1};}
        internal static BtlB1.FUStRebirthAreaDesc GetRebirthAreaDesc(int id){return new BtlB1.FUStRebirthAreaDesc{Name="area-"+id};}
    }
    internal enum ETeleportTypeV2 {RebirthPointTeleportOnly=5}
    internal enum EPlayerTeleportReason {RebirthPoint=2}
    internal struct TeleportParam_RebirthPoint {public int RebirthPointId;}
    internal sealed class BPS_GSEventCollection
    {
        public static BPS_GSEventCollection GetLocal(BGUPlayerCharacterCS player){return new BPS_GSEventCollection();}
        public MockSignal Evt_BPS_TeleportTo {get;}=new MockSignal();
    }
    internal sealed class MockSignal
    {
        public static int Invocations,LastId;public static bool Fail;
        public void Invoke(ETeleportTypeV2 type,object value,EPlayerTeleportReason reason)
        {if(Fail)throw new InvalidOperationException("Test native event failure");Invocations++;LastId=((TeleportParam_RebirthPoint)value).RebirthPointId;}
    }
}
namespace BtlB1
{
    internal sealed class FUStRebirthPointDesc
    {public int ID,GroupMapID,GroupAreaID,SortID,RelatedRebirthPointIdToHiddenThis;public string Name;public int CanTeleport {get;set;}}
    internal sealed class FUStRebirthAreaDesc {public string Name;}
}
namespace b1.Localization
{
    internal static class GSLocalization
    {public static object GetLocaliztionalFText(string key){return key=="temple-1"?"前山土地庙":"黑风山";}}
}
namespace B1UI
{
    internal static class GSG {internal static object BattleLogicSvc;internal static object GSPageOP;}
}
namespace B1UI.GSSvc
{
    // Deliberately lacks the SDK method. The adapter must still compile and run.
    internal sealed class B1BattleLogicSvc {}
}
namespace b1.UI
{
    internal enum B1ActorTag {LocalPlayerState=5,PlayerController=6}
    internal sealed class B1GSUIActorMgr
    {
        public static B1ActorTag LastInteractTag;
        public static bool PlayerStateReady=true;
        public static B1GSUIActorMgr Get(UnrealEngine.Runtime.UObject player){return new B1GSUIActorMgr();}
        public T GetBattleData<T>(B1ActorTag tag,bool update) where T:class
        {
            if(update)throw new Exception("Read-only data path changed");
            if(typeof(T)==typeof(DS_IBPC_PlayerInteractData)){
                LastInteractTag=tag;
                if(tag==B1ActorTag.LocalPlayerState)return PlayerStateReady?DS_IBPC_PlayerInteractData.Current as T:null;
                if(tag==B1ActorTag.PlayerController)return new DS_IBPC_PlayerInteractData() as T;
                throw new Exception("Unexpected interaction data owner");
            }
            if(tag!=B1ActorTag.PlayerController)throw new Exception("Rebirth data owner changed");
            return new DS_IBPC_RebirthPointData() as T;
        }
    }
    internal sealed class DS_IBPC_RebirthPointData
    {
        internal static readonly HashSet<int> Activated=new HashSet<int>();
        public bool IsRebirthPointActived(int id){return Activated.Contains(id);}
        public int GetActivedRebirthPoints(out List<int> values){values=new List<int>{1,2,3,4,1};return values.Count;}
        public IEnumerable<int> GetBlockedRebirthPoints(){return new[]{2};}
    }
    internal sealed class DS_IBPC_PlayerInteractData
    {
        internal static readonly DS_IBPC_PlayerInteractData Current=new DS_IBPC_PlayerInteractData();
        public b1.EntitySharedRef BestInteractEntityRef {get;set;}
        public bool IsInInteraction(){return false;}
    }
}
namespace DqjCombat
{
    internal static class McWorldLink {internal static DateTime LastBossDefeatUtc=DateTime.MinValue;}
    internal static class Log {internal static void Write(string tag,string value){Console.WriteLine(tag+" | "+value);}}
    internal sealed class McBridgeWire
    {internal bool Connected=true;internal readonly List<string> Messages=new List<string>();internal void Send(string value){Messages.Add(value);}}
}
