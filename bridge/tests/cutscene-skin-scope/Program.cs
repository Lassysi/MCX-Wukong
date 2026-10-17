using System;
using DqjCombat;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition,string description)
    {if(!condition)throw new Exception(description);checks++;Console.WriteLine("PASS "+description);}
    private static int Main()
    {
        Check(PlayerFormPolicy.CanOwnMovement("Unit_Player_Wukong_C"),"ordinary captured Wukong player keeps MC locomotion");
        Check(!PlayerFormPolicy.CanOwnMovement("Unit_Player_Chan_C"),"captured cicada pawn keeps native controls and transformation state");
        Check(!PlayerFormPolicy.CanOwnMovement("Unit_Player_Wolf_C"),"unknown transformation is never implicitly given MC authority");
        Check(!PlayerFormPolicy.Enlarge(true,true)&&PlayerFormPolicy.Enlarge(true,false),"interaction sessions stay normal-sized even when native Movie is set");
        var biped=new[]{"pelvis","spine_01","neck_01","head","upperarm_l","hand_l","upperarm_r","hand_r","thigh_l","foot_l","thigh_r","foot_r"};
        HumanoidSkinRig rig;
        Check(HumanoidSkinRig.TryResolve(biped,out rig)&&rig.ArmL=="upperarm_l"&&rig.Neck=="neck_01","actual joint contract can accept a different HQ skeleton asset");
        Check(!HumanoidSkinRig.TryResolve(new[]{"Root","pelvis","Spine1_M","head_M","FrontLeg1_L","FrontLeg1_R","BackLeg1_L","BackLeg1_R"},out rig),"captured cicada bones cannot receive a humanoid replacement");
        Check(CutsceneSkinScope.CanReplaceStandIn(false,"LSP_player_wukong_C_1","LSP_player_wukong_C",true),"story player stand-in no longer requires a BGUPlayerCharacter base");
        Check(!CutsceneSkinScope.CanReplaceStandIn(false,"LSP_tudi_C_1","LSP_tudi_C",true),"story NPC retains its native body");
        Check(!CutsceneSkinScope.CanReplaceStandIn(false,"LSP_player_wukong_C_1","LSP_player_wukong_C",false),"story hero in another world remains excluded");
        Check(CutsceneSkinScope.IsHeroBinding("Player")&&!CutsceneSkinScope.IsHeroBinding("PlayerTarget")&&!CutsceneSkinScope.IsHeroBinding("Tudi"),"explicit native hero binding excludes NPC and target roles");
        var input=new NativeCutsceneInput();var ui=new NativeSkipPage();
        Check(!input.Update(ui,false,true,true)&&ui.Presses==0,"E cannot bypass native unskippable permission");
        Check(input.Update(ui,true,true,true)&&ui.IsPress&&ui.Presses==1,"E starts the original native hold callback");
        Check(!input.Update(ui,true,true,true)&&ui.Presses==1,"held E does not restart the native progress timer");
        input.Update(ui,true,true,false);
        Check(!ui.IsPress&&ui.Releases==1,"releasing E cancels the forwarded native hold");
        ui.NativePress();input.Update(ui,true,true,true);
        Check(ui.Presses==2,"already active native binding is not invoked twice");
        input.Update(ui,false,true,false);
        Check(ui.Releases==1,"adapter does not release a hold it did not acquire");
        ui.NativeRelease();input.Update(ui,true,true,true);input.Update(ui,true,false,true);
        Check(!ui.IsPress&&ui.Releases==3,"focus loss releases adapter-owned input");
        input.Update(null,true,true,true);Check(!input.Update(null,true,true,true),"missing UI does not synthesize a skip");
        Check(CutsceneSkinScope.CanReplaceStandIn(false,"LSP_dasheng_C_4","LSP_dasheng_C",true),"captured CG hero is explicitly eligible during story CG");
        Check(CutsceneSkinScope.CanReplaceStandIn(false,"LSP_dasheng_seamless_C_1","LSP_dasheng_seamless_C",true),"captured seamless hero is explicitly eligible during story CG");
        Check(CutsceneSkinScope.CanReplaceStandIn(true,"LSP_dasheng_C_4","LSP_dasheng_C",true),"known hero is eligible when player-derived too");
        Check(CutsceneSkinScope.CanReplaceStandIn(true,"Unit_player_dasheng_C_1","Unit_player_dasheng_C",true),"independent CG player-derived hero is eligible");
        Check(!CutsceneSkinScope.CanReplaceStandIn(true,"Default__Unit_Player_Wukong_C","Unit_Player_Wukong_C",true),"captured player class default never receives procedural parts");
        Check(!CutsceneSkinScope.IsLiveActor("Default__LSP_dasheng_seamless_C",true),"constructor guard rejects captured cinematic class default");
        Check(!CutsceneSkinScope.CanReplaceStandIn(true,"Unit_EquipPreview_Wukong_C_2","Unit_EquipPreview_Wukong_C",true),"captured equipment preview is preserved");
        Check(!CutsceneSkinScope.CanReplaceStandIn(true,"Unit_Player_Wukong_C_12","Unit_Player_Wukong_C",false),"other-world player component cannot be re-skinned");
        Check(!CutsceneSkinScope.CanReplaceStandIn(false,"Npc_C_1","Npc_C",true),"other same-skeleton NPC retains native appearance");
        Check(!CutsceneSkinScope.IsLiveActor("Unit_Player_Wukong_C_12",false),"constructor guard rejects world-less actor");
        Check(!CutsceneSkinScope.CanReplaceStandIn(true,null,"Unit_Player_Wukong_C",true),"missing identity does not establish ownership");
        Check(CutsceneSkinScope.CanReplaceStandIn(true,"Unit_Player_Wukong_C_12","Unit_Player_Wukong_C",true),"live same-world player stand-in remains eligible for the shared skin");
        Check(CutsceneSkinScope.IsLiveActor("Unit_player_dasheng_C_1",true),"controlled player constructor remains eligible independently of stand-in policy");
        var prop=new AppearanceVisibilityLease();
        Check(prop.Update(true,false)==true,"gameplay suppresses an originally visible prop");
        Check(prop.Update(true,true)==null,"repeated gameplay refresh does not replace the saved visibility");
        Check(prop.Update(false,true)==false,"CG releases prop to its original visible state");
        Check(prop.Update(false,true)==null,"native CG can hide a prop after lease release");
        Check(prop.Update(true,true)==null,"gameplay resumes and captures the new native hidden state");
        Check(prop.Release()==true,"dispose restores the latest native hidden state");
        Check(prop.Release()==null,"repeated dispose cannot change native visibility");
        var body=new AppearanceVisibilityLease();
        body.Update(true,false);
        Check(body.Update(true,false)==true,"native body reappearance remains suppressed during CG");
        Check(body.Release()==false,"body disposal restores pre-skin visibility");
        var profile=CinematicSkinProfile.Parse("Width=NaN\nHeight=Infinity\nWidth=9\nHeight=-2");
        Check(profile.Width==1.8f&&profile.Height==1f,"nonfinite configuration is ignored and finite dimensions are bounded");
        Check(CinematicSkinProfile.IsProp("SkeletalMesh","weapon_r","BP_Staff_C"),"native staff socket remains a prop");
        Check(CinematicSkinProfile.IsProp("Mesh","","BP_Hulu_C"),"native gourd remains a prop");
        Check(!CinematicSkinProfile.IsProp("ArmorBody","spine","Armor_C"),"armor does not acquire prop visibility");
        Check(CinematicSkinProfile.IsArmor("HairHelmetMesh"),"independent hair and helmet can be suppressed as body art");
        Check(PrologueSkipGate.Eligible(9,9,10,0,0),"prologue world and native initial chapter qualify together");
        Check(!PrologueSkipGate.Eligible(10,9,10,1,0),"first chapter and all later CGs do not qualify");
        Check(!PrologueSkipGate.Eligible(9,9,10,1,0),"already completed save is excluded even in old prologue world");
        Check(!PrologueSkipGate.Eligible(0,0,0,0,0),"uninitialized level data cannot qualify");
        Check(!PrologueSkipGate.Eligible(9,9,9,0,0),"invalid destination cannot qualify");
        var gate=new PrologueSkipGate();
        gate.Reply(true);Check(!gate.TakeCompletion(),"unsolicited callback cannot trigger travel");
        Check(gate.Begin(10)&&!gate.Begin(11),"transaction is requested once per world");
        gate.Reply(true);gate.Reply(false);
        Check(gate.TakeCompletion()&&!gate.TakeCompletion()&&!gate.Failed,"first reply wins and native travel starts once");
        var rejected=new PrologueSkipGate();rejected.Begin(1);rejected.Reply(false);rejected.Reply(true);
        Check(rejected.Failed&&!rejected.TakeCompletion()&&!rejected.Begin(40),"rejected completion neither teleports nor retries");
        var timeout=new PrologueSkipGate();timeout.Begin(1);
        Check(!timeout.Timeout(30)&&timeout.Timeout(31),"native request has a bounded wait");
        timeout.Reply(true);Check(!timeout.TakeCompletion()&&!timeout.Begin(32),"late reply after timeout cannot mutate travel state");
        Console.WriteLine("PREVIEW100 POLICY AND LIFECYCLE PASS checks="+checks);return 0;
    }
    private sealed class NativeSkipPage
    {
        public bool IsPress;
        public int Presses,Releases;
        private void OnPressSkip(){Presses++;IsPress=true;}
        private void OnReleasedSkip(){Releases++;IsPress=false;}
        public void NativePress(){OnPressSkip();}
        public void NativeRelease(){OnReleasedSkip();}
    }
}
