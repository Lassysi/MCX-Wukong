using System;
using b1;
using UnrealEngine.UMG;
using UnrealEngine.SlateCore;

namespace DqjCombat
{
    // 精确指向 GSUI 的战斗主页面；不扫描/隐藏菜单、字幕或加载页面。
    internal sealed class NativeBattleHud : IDisposable
    {
        private UUserWidget root;
        private ESlateVisibility originalVisibility;
        private double nextLookup, nextLog;

        internal void Update(double now, bool gameplay)
        {
            if (!gameplay) { Dispose(); return; }
            try
            {
                if (root != null && (root.IsDestroyed || root.IsPendingKill)) root = null;
                if (now >= nextLookup)
                {
                    nextLookup = now + 1;
                    var pages = B1UI.GSG.GSPageOP;
                    var page = pages == null ? null : pages.FindUIPage((int)EUIPageID.BattleMainCon);
                    UUserWidget current = page == null ? null : page.GetRootUserWidget();
                    if (current != null && (current.IsDestroyed || current.IsPendingKill)) current = null;
                    if (current != root)
                    {
                        Restore();
                        if (current != null)
                        {
                            originalVisibility = current.GetVisibility();
                            root = current;
                            Log.Write("native-ui", "绑定 BattleMainCon root=" + root.GetClass().GetPathName()
                                + " original=" + originalVisibility);
                        }
                    }
                }
                if (root != null) root.SetVisibility(ESlateVisibility.Collapsed);
                if (now >= nextLog)
                {
                    nextLog = now + 5;
                    Log.Write("native-ui", root == null ? "BattleMainCon 尚未创建，继续等待"
                        : "BattleMainCon visibility=" + root.GetVisibility());
                }
            }
            catch (Exception e)
            {
                if (now >= nextLog)
                {
                    nextLog = now + 5;
                    Log.Write("native-ui-error", e.GetType().Name + ": " + e.Message);
                }
            }
        }

        private void Restore()
        {
            try
            {
                if (root != null && !root.IsDestroyed && !root.IsPendingKill)
                {
                    root.SetVisibility(originalVisibility);
                    Log.Write("native-ui", "已还原 BattleMainCon visibility=" + originalVisibility);
                }
            }
            catch (Exception e) { Log.Write("native-ui-error", "还原: " + e.Message); }
            root = null;
        }

        public void Dispose() { Restore(); nextLookup = 0; }
    }
}
