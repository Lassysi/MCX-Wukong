using System;
using System.Collections.Generic;
using b1;
using LitJson;

namespace DqjCombat
{
    // Only the native unlocked travel catalogue and loading pipeline are retained.
    // The menu belongs to MC; no shrine rest, equipment or alchemy action is invoked.
    internal sealed class McNativeTravel
    {
        private readonly BGUPlayerCharacterCS player;
        private readonly McBridgeWire wire;
        private readonly Action suspend;
        private readonly Action abort;
        private readonly HashSet<int> offered=new HashSet<int>();
        private string token;
        internal McNativeTravel(BGUPlayerCharacterCS player,McBridgeWire wire,Action suspend,Action abort)
        {this.player=player;this.wire=wire;this.suspend=suspend;this.abort=abort;}
        private static string Name(string value){return NativeTravelText.Read(value);}
        private object Signal(){return NativeEventInvoker.Signal(BPS_GSEventCollection.GetLocal(player),"Evt_BPS_TeleportTo");}
        private static object[] Arguments(int id)
        {
            return new object[]{Enum.Parse(typeof(ETeleportTypeV2),"RebirthPointTeleportOnly"),
                new TeleportParam_RebirthPoint{RebirthPointId=id},Enum.Parse(typeof(EPlayerTeleportReason),"RebirthPoint")};
        }
        private List<int> Available()
        {
            return NativeTravelCatalogue.Read(player);
        }
        internal void Open()
        {
            try{
                Log.Write("travel","open request; connected="+wire.Connected+" existingMenu="+(!string.IsNullOrEmpty(token)));
                if(!wire.Connected||!string.IsNullOrEmpty(token))return;
                var available=Available();
                if(available==null||available.Count>512)throw new InvalidOperationException("Native travel catalogue unavailable or too large");
                if(available.Count>0)Log.Write("travel-api","native travel event ready: "+NativeEventInvoker.Validate(Signal(),Arguments(available[0])));
                var entries=new List<JsonData>();offered.Clear();
                var destinations=new List<BtlB1.FUStRebirthPointDesc>();
                foreach(int id in available){
                    var desc=BGW_GameDB.GetRebirthPointDesc(id);if(desc==null||!offered.Add(id))continue;
                    if(desc.RelatedRebirthPointIdToHiddenThis!=0&&available.Contains(desc.RelatedRebirthPointIdToHiddenThis)){offered.Remove(id);continue;}
                    destinations.Add(desc);
                }
                destinations.Sort((a,b)=>{
                    int order=a.GroupMapID.CompareTo(b.GroupMapID);if(order!=0)return order;
                    order=a.GroupAreaID.CompareTo(b.GroupAreaID);if(order!=0)return order;
                    order=a.SortID.CompareTo(b.SortID);return order!=0?order:a.ID.CompareTo(b.ID);
                });
                foreach(var desc in destinations){
                    var area=BGW_GameDB.GetRebirthAreaDesc(desc.GroupAreaID);
                    var row=new JsonData();row["id"]=desc.ID;row["name"]=Name(desc.Name);row["area"]=area==null?"":Name(area.Name);entries.Add(row);
                }
                token=Guid.NewGuid().ToString("N");
                var open=new JsonData();open["t"]="travel_open";open["token"]=token;open["origin"]="黑神话 · 已解锁土地庙";open["count"]=entries.Count;
                wire.Send(open.ToJson());
                for(int at=0;at<entries.Count||at==0;at+=16){
                    var batch=new JsonData();batch.SetJsonType(JsonType.Array);
                    for(int i=at;i<Math.Min(at+16,entries.Count);i++)batch.Add(entries[i]);
                    var message=new JsonData();message["t"]="travel_entries";message["token"]=token;message["entries"]=batch;message["done"]=at+16>=entries.Count;wire.Send(message.ToJson());
                }
                Log.Write("travel","F6: MC destination menu opened; unlocked="+entries.Count);
            }catch(Exception e){Notice("传送列表暂时不可用，请稍后重试");Log.Write("travel-error",e.ToString());Clear();}
        }
        internal void Handle(JsonData message,bool gameplay)
        {
            string requestToken=(string)message["token"];
            if(string.IsNullOrEmpty(token)||requestToken!=token)return;
            if((string)message["t"]=="travel_cancel"){Log.Write("travel","MC destination menu canceled");Clear();return;}
            bool suspended=false,nativeReturned=false;
            try{
                int id=int.Parse(message["id"].ToString(),System.Globalization.CultureInfo.InvariantCulture);
                if(!gameplay||player.IsDestroyed||!offered.Contains(id)
                    ||!Available().Contains(id)){
                    Result(requestToken,false,"当前无法自由行动，或目的地已不可传送");return;
                }
                var signal=Signal();
                if(signal==null){Result(requestToken,false,"宿主传送接口尚未就绪");return;}
                var args=Arguments(id);NativeEventInvoker.Validate(signal,args);
                // Exact native shrine/map path verified with AhudDump. The TeleportOnly
                // template has no pre-teleport montage and uses the original loading FSM.
                Clear();suspended=true;suspend();
                NativeEventInvoker.Invoke(signal,args);nativeReturned=true;
                Result(requestToken,true,"");Log.Write("travel","native TeleportOnly requested; destination="+id);
            }catch(Exception e){if(suspended&&!nativeReturned)abort();Result(requestToken,false,"传送启动失败，请关闭列表后重试");Log.Write("travel-error",e.ToString());}
        }
        private void Result(string requestToken,bool accepted,string error)
        {
            var reply=new JsonData();reply["t"]="travel_result";reply["token"]=requestToken;reply["accepted"]=accepted;reply["error"]=error;wire.Send(reply.ToJson());
        }
        private void Notice(string text){var message=new JsonData();message["t"]="travel_notice";message["text"]=text;wire.Send(message.ToJson());}
        internal void Clear(){token=null;offered.Clear();}
    }
}
