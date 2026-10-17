using System;
using System.Globalization;
using System.IO;

namespace DqjCombat
{
    // Visual component dimensions only. The native skeleton, actor/collision and interaction state are untouched.
    internal sealed class CinematicSkinProfile
    {
        internal float Width=1.45f,Height=1.10f;
        internal static CinematicSkinProfile Parse(string text)
        {
            var profile=new CinematicSkinProfile();
            foreach(var row in (text??"").Split('\n')){
                var fields=row.Trim().Split('=');float value;
                if(fields.Length!=2||!float.TryParse(fields[1],NumberStyles.Float,CultureInfo.InvariantCulture,out value)
                    ||float.IsNaN(value)||float.IsInfinity(value))continue;
                if(fields[0].Trim().Equals("Width",StringComparison.OrdinalIgnoreCase))profile.Width=Math.Max(1f,Math.Min(1.8f,value));
                if(fields[0].Trim().Equals("Height",StringComparison.OrdinalIgnoreCase))profile.Height=Math.Max(1f,Math.Min(1.3f,value));
            }
            return profile;
        }
        internal static CinematicSkinProfile Load(string path)
        {return File.Exists(path)?Parse(File.ReadAllText(path)):new CinematicSkinProfile();}

        internal static bool IsProp(string componentName,string socket,string actorClass)
        {
            string description=(componentName??"")+" "+(socket??"")+" "+(actorClass??"");
            foreach(string token in new[]{"weapon","staff","gourd","hulu","prop","niagara","effect","weapon_l","weapon_r"})
                if(description.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0)return true;
            return false;
        }
        internal static bool IsArmor(string name)
        {
            foreach(string token in new[]{"armor","armour","cloth","costume","outfit","hair","helmet","cape"})
                if((name??"").IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0)return true;
            return false;
        }
    }
}
