using System;

namespace DqjCombat
{
    // Native input may show its cursor before the ticker adopts the page. Only
    // a remembered gameplay value is a valid return state, not that menu value.
    internal sealed class NativeUiCursorLease
    {
        private bool remembered,gameplayCursor,leased,returnCursor;
        internal void RememberGameplay(bool cursor,bool mcOwnsCursor)
        {if(!leased&&!mcOwnsCursor){gameplayCursor=cursor;remembered=true;}}
        internal void Begin(bool mcOwnsCursor,bool mcOriginalCursor)
        {if(leased)return;returnCursor=remembered?gameplayCursor:mcOwnsCursor&&mcOriginalCursor;leased=true;}
        internal void End(Action<bool> restore)
        {if(!leased)return;restore(returnCursor);leased=false;}
    }
}
