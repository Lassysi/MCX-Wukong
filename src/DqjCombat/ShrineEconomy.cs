using System;
using b1;
using UnrealEngine.Engine;

namespace DqjCombat
{
    // Preview105 (user, chapter 2 shrine): Will (spirit essence) feeds MC emeralds; the shrine trader's offers grow
    // with the chapter. Read-only on the native side: the native Will is never changed, and quest/story items are
    // not touched. Will lives in BPC_PlayerRoleData RoleData.RoleCs.Bag.MoneyList (MoneyType.Spirit).
    internal sealed class ShrineEconomy
    {
        private readonly Action<string> send;
        private long lastWill=-1;
        private int lastChapter=-1;
        private double nextPoll;
        private bool loggedError;
        internal ShrineEconomy(Action<string> send){this.send=send;}

        /// Pure rule (tested offline): only increases pay; a decrease (spent, or a reload) moves the baseline.
        internal static long Gain(long previous,long current){return previous<0||current<=previous?0:current-previous;}

        /// Live105: the game's chapter ids are 10, 20, 30... (chapter 2 reported as 20); MC tiers use 1..6.
        internal static int ChapterNumber(int id){return id>=10?id/10:Math.Max(1,id);}

        internal void Update(double now,APlayerController controller)
        {
            if(now<nextPoll||!NativeObjectLifetime.Live(controller))return;nextPoll=now+.5;
            try{
                var role=BGU_DataUtil.GetReadOnlyData<BPC_PlayerRoleData>(controller);
                var data=role==null?null:role.RoleData;var cs=data==null?null:data.RoleCs;
                if(cs==null)return;
                if(cs.Chapter!=null&&cs.Chapter.CurChapter!=lastChapter){
                    lastChapter=cs.Chapter.CurChapter;send("{\"t\":\"chapter\",\"n\":"+ChapterNumber(lastChapter)+"}");
                    Log.Write("shop","chapter id "+lastChapter+" -> chapter "+ChapterNumber(lastChapter)+" sent to MC trader");
                }
                long will=-1;
                if(cs.Bag!=null&&cs.Bag.MoneyList!=null)
                    foreach(var money in cs.Bag.MoneyList)if(money!=null&&money.MoneyType==ResB1.MoneyType.Spirit){will=money.MoneyValue;break;}
                if(will<0)return;
                long gain=Gain(lastWill,will);
                if(lastWill<0)Log.Write("shop","Will baseline "+will+" (no payout for the existing amount)");
                lastWill=will;
                if(gain>0)send("{\"t\":\"will\",\"gain\":"+gain+",\"total\":"+will+"}");
            }catch(Exception e){if(!loggedError){loggedError=true;Log.Write("shop-error","Will/chapter read: "+e.GetType().Name+": "+e.Message);}}
        }
    }
}
