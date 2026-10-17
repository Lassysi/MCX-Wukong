using System;
using System.Collections.Generic;

namespace DqjCombat
{
    internal sealed class HumanoidSkinRig
    {
        internal string Pelvis,Neck,Head,ArmL,ArmR,HandL,HandR,LegL,LegR,FootL,FootR;
        internal static bool TryResolve(IList<string> bones,out HumanoidSkinRig rig)
        {
            rig=new HumanoidSkinRig();
            rig.Pelvis=Bone(bones,"pelvis",null,null)??Bone(bones,"hips",null,null)??Bone(bones,"spine",null,null);
            rig.Neck=Bone(bones,"neck",null,null)??Bone(bones,"spine",null,null);
            rig.Head=Bone(bones,"head",null,"end")??rig.Neck;
            rig.ArmL=Bone(bones,"upperarm","L",null)??Bone(bones,"arm","L","fore");
            rig.ArmR=Bone(bones,"upperarm","R",null)??Bone(bones,"arm","R","fore");
            rig.HandL=Bone(bones,"hand","L","finger");rig.HandR=Bone(bones,"hand","R","finger");
            rig.LegL=Bone(bones,"thigh","L",null)??Bone(bones,"upleg","L",null);
            rig.LegR=Bone(bones,"thigh","R",null)??Bone(bones,"upleg","R",null);
            rig.FootL=Bone(bones,"foot","L",null);rig.FootR=Bone(bones,"foot","R",null);
            return rig.Pelvis!=null&&rig.Neck!=null&&rig.ArmL!=null&&rig.ArmR!=null&&rig.HandL!=null
                &&rig.HandR!=null&&rig.LegL!=null&&rig.LegR!=null&&rig.FootL!=null&&rig.FootR!=null;
        }
        private static string Bone(IList<string> bones,string key,string side,string exclude)
        {
            foreach(var b in bones){
                string value=b.ToLowerInvariant();
                if(value.IndexOf(key,StringComparison.Ordinal)<0||exclude!=null&&value.IndexOf(exclude,StringComparison.Ordinal)>=0)continue;
                if(Side(value)!=side)continue;
                return b;
            }
            return null;
        }
        private static string Side(string value)
        {
            if(value.EndsWith("_l")||value.StartsWith("l_")||value.Contains(" l ")||value.Contains("_l_")||value.Contains("left"))return "L";
            if(value.EndsWith("_r")||value.StartsWith("r_")||value.Contains(" r ")||value.Contains("_r_")||value.Contains("right"))return "R";
            return null;
        }
    }
}
