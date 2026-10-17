using System;

namespace DqjCombat
{
    // Preview97: explicit CG hero roles, not shared skeleton alone. CG may include an independent opening hero.
    internal static class CutsceneSkinScope
    {
        internal static bool IsLiveActor(string actorName,bool hasWorld)
        {
            return hasWorld&&!string.IsNullOrEmpty(actorName)
                &&!actorName.StartsWith("Default__",StringComparison.OrdinalIgnoreCase);
        }

        internal static bool CanReplaceStandIn(bool isPlayerCharacter,string actorName,string actorClass,bool sameWorld)
        {
            if(!sameWorld||!IsLiveActor(actorName,true)||string.IsNullOrEmpty(actorClass))return false;
            // Captured Live95: LSP_dasheng_C_4 and LSP_dasheng_seamless_C_1 are independent cinematic actors.
            // Equipment preview classes can derive from the player class and are still excluded.
            if(actorClass.IndexOf("EquipPreview",StringComparison.OrdinalIgnoreCase)>=0)return false;
            return isPlayerCharacter||IsNativeOpeningActor(actorClass)||IsStoryHeroActor(actorClass);
        }

        internal static bool IsStoryHeroActor(string actorClass)
        {
            if(string.IsNullOrEmpty(actorClass)||!actorClass.StartsWith("LSP_",StringComparison.OrdinalIgnoreCase))return false;
            return actorClass.StartsWith("LSP_player_",StringComparison.OrdinalIgnoreCase)
                ||actorClass.Equals("LSP_player_C",StringComparison.OrdinalIgnoreCase)
                ||actorClass.StartsWith("LSP_wukong",StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsHeroBinding(string binding)
        {
            return string.Equals(binding,"Player",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(binding,"Player_Wukong",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(binding,"Wukong",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(binding,"Dasheng",StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsNativeOpeningActor(string actorClass)
        {
            return actorClass!=null&&(actorClass.StartsWith("LSP_dasheng",StringComparison.OrdinalIgnoreCase)
                ||actorClass.StartsWith("Unit_player_dasheng",StringComparison.OrdinalIgnoreCase));
        }
    }
}
