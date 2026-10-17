namespace DqjCombat
{
    // Null means no write: native CGs retain control of props after we release their gameplay suppression.
    internal sealed class AppearanceVisibilityLease
    {
        private bool held,original;
        internal bool? Update(bool suppress,bool current)
        {
            if(!suppress)return Release();
            if(!held){original=current;held=true;}
            return current?(bool?)null:true;
        }
        internal bool? Release()
        {if(!held)return null;held=false;return original;}
    }
}
