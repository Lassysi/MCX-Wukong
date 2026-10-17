using System;
using System.Globalization;
using System.Text;
using b1;
using BtlShare;
using UnrealEngine.Engine;

namespace DqjCombat
{
    // Read-only probe (Preview58): when the Wukong player lands a native hit, log the skill,
    // damage effect, its AttackStiff entry and the victim's armor/stiff attributes. These real
    // combo IDs let MC hits count as Wukong combo hits later. Adds a listener; never replaces one.
    internal sealed class NativeHitProbe : IDisposable
    {
        private static readonly int MaxLines=400;
        private EventHandle handle;private Delegate mine;private int lines;
        internal NativeHitProbe(BGUPlayerCharacterCS player)
        {
            try{
                handle=HookLedger.Resolve(GameApi.Events(player),"Evt_NotifyAttackerHitSuccess");
                if(handle==null)throw new InvalidOperationException("event not found");
                var native=HookLedger.ReadHandlers(handle);
                Type type=native!=null&&native.Length>0?native[0].GetType():null;
                if(type==null&&handle.InnerField!=null&&handle.InnerField.FieldType.IsSubclassOf(typeof(MulticastDelegate)))type=handle.InnerField.FieldType;
                if(type==null)throw new InvalidOperationException("listener delegate type unknown");
                mine=DelegateFactory.Create(type,OnHit);
                string error;if(!HookLedger.Install(handle,mine,out error))throw new InvalidOperationException(error);
                Log.Write("hit-probe","installed on Evt_NotifyAttackerHitSuccess handlers="+(native==null?0:native.Length));
            }catch(Exception e){mine=null;Log.Write("hit-probe","not installed: "+e.Message);}
        }
        private void OnHit(object[] args)
        {
            if(lines>=MaxLines||args.Length<3)return;
            try{
                var victim=args[0] as BGUCharacterCS;
                var config=(FSkillDamageConfig)args[1];var request=(FEffectInstReq)args[2];
                if(config.DmgReason==EDamageReason.FallDmg)return; // our own MC-routed damage
                var s=new StringBuilder();
                s.Append("victim=").Append(victim==null?"-":victim.GetUniqueID().ToString(CultureInfo.InvariantCulture));
                s.Append(" skill=").Append(request.TriggerSkillId).Append(" skillName=").Append(BGW_GameDB.GetSkillSToBlame(request.TriggerSkillId));
                s.Append(" reason=").Append(config.DmgReason).Append(" calc=").Append(config.DamageCalcType).Append(" effect=").Append(config.DmgReasonEffectID);
                var desc=BGW_GameDB.GetOriginalSkillEffectDesc(config.DmgReasonEffectID);
                if(desc!=null){
                    s.Append(" effectType=").Append(desc.EffectType).Append(" int=[").Append(string.Join(",",desc.EffectParamsInt)).Append("] float=[").Append(string.Join(",",desc.EffectParamsFloat)).Append("]");
                    if(desc.EffectParamsInt.Count>1){
                        var stiff=BGW_GameDB.GetOriginalSkillEffectDesc(desc.EffectParamsInt[1]);
                        if(stiff!=null)s.Append(" stiffEffect=").Append(stiff.ID).Append(" stiffType=").Append(stiff.EffectType).Append(" stiffFloat=[").Append(string.Join(",",stiff.EffectParamsFloat)).Append("]");
                    }
                }
                if(victim!=null&&!victim.IsDestroyed){
                    foreach(int a in new[]{160,164,171})
                        s.Append(' ').Append(((EBGUAttrFloat)a).ToString()).Append('=').Append(BGUFunctionLibraryCS.BGUGetFloatAttr(victim,(EBGUAttrFloat)a).ToString("0.##",CultureInfo.InvariantCulture));
                    s.Append(" hp=").Append(BGUFunctionLibraryCS.BGUGetFloatAttr(victim,EBGUAttrFloat.Hp).ToString("0.#",CultureInfo.InvariantCulture));
                }
                lines++;Log.Write("hit-probe",s.ToString());
            }catch(Exception e){lines++;Log.Write("hit-probe","error "+e.GetType().Name+": "+e.Message);}
        }
        public void Dispose()
        {
            if(mine!=null){string error;HookLedger.Uninstall(handle,mine,out error);}
            mine=null;handle=null;
        }
    }
}
