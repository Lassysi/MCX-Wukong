using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using b1;

namespace DqjCombat
{
    // Preview105 (user: replace common, useless monster drops with MC items): one-time read-only export of the game's
    // own drop data, so the replacement list is chosen from facts, not guesses. Writes Log.Dir/dqj-drops.json once per
    // game run: all items (type, quality, sell price, how many crafting recipes use them), all drop rules
    // (CommDropRuleDesc: Will, random item drops) and unit -> drop rule links. Nothing in the game is changed.
    internal static class DropCatalog
    {
        private static bool done;
        internal static void ExportOnce()
        {
            if(done)return;done=true;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            try{
                var recipeUse=new Dictionary<int,int>();
                var recipes=GameDBRuntime.GetTBItemRecipeDesc();
                if(recipes!=null)foreach(var r in recipes.List){
                    if(r==null||r.MaterialList==null)continue;
                    foreach(var m in r.MaterialList){int id=IntProp(m,"ItemId");if(id<=0)id=IntProp(m,"Id");if(id>0){int n;recipeUse.TryGetValue(id,out n);recipeUse[id]=n+1;}}
                }
                // Live105: equipment/gear crafting at shrines is a shop whose prices are material lists (ShopItemDesc.CostItem).
                var shopUse=new Dictionary<int,int>();
                var shop=GameDBRuntime.GetTBShopItemDesc();
                if(shop!=null)foreach(var si in shop.List){
                    if(si==null||si.CostItem==null)continue;
                    foreach(var c in si.CostItem){int id=IntProp(c,"ItemId");if(id<=0)id=IntProp(c,"Id");if(id>0){int n;shopUse.TryGetValue(id,out n);shopUse[id]=n+1;}}
                }
                var sb=new StringBuilder(1<<20);
                sb.Append("{\"exportedAt\":\"").Append(DateTime.Now.ToString("s",CultureInfo.InvariantCulture)).Append("\",\"items\":[");
                bool first=true;
                var items=GameDBRuntime.GetTBItemDesc();
                if(items!=null)foreach(var it in items.List){
                    if(it==null)continue;if(!first)sb.Append(',');first=false;
                    int uses,craft;recipeUse.TryGetValue(it.Id,out uses);shopUse.TryGetValue(it.Id,out craft);
                    sb.Append("{\"id\":").Append(it.Id).Append(",\"name\":").Append(Q(it.Name)).Append(",\"text\":").Append(Q(Text(it.Name))).Append(",\"type\":").Append(Q(it.ItemType.ToString()))
                      .Append(",\"quality\":").Append(Q(it.Quality.ToString())).Append(",\"sell\":").Append(it.SellPrice)
                      .Append(",\"carryMax\":").Append(it.CarryMax).Append(",\"recipeUses\":").Append(uses).Append(",\"craftUses\":").Append(craft).Append(",\"source\":").Append(Q(it.Source)).Append('}');
                }
                sb.Append("],\"commDropRules\":");Dump(sb,GameDBRuntime.GetTBCommDropRuleDesc()==null?null:GameDBRuntime.GetTBCommDropRuleDesc().List,0);
                sb.Append(",\"unitDropRules\":");Dump(sb,GameDBRuntime.GetTBUnitDropRuleDesc()==null?null:GameDBRuntime.GetTBUnitDropRuleDesc().List,0);
                sb.Append('}');
                string path=Path.Combine(Log.Dir,"dqj-drops.json");
                File.WriteAllText(path,sb.ToString(),new UTF8Encoding(false));
                Log.Write("drops","exported drop catalogue items="+(items==null?0:items.List.Count)+" recipes="+(recipes==null?0:recipes.List.Count)+" in "+watch.ElapsedMilliseconds+" ms -> "+path);
            }catch(Exception e){Log.Write("drops-error","export failed: "+e.GetType().Name+": "+e.Message);}
        }
        // Localised display text for a key like "ItemDesc.3306.Name" (falls back to the key).
        private static string Text(string key)
        {
            if(string.IsNullOrEmpty(key))return key;
            try{var t=b1.Localization.GSLocalization.GSLocalizational(key);return string.IsNullOrEmpty(t)?key:t;}catch{return key;}
        }
        private static int IntProp(object o,string name)
        {
            if(o==null)return 0;var p=o.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
            if(p==null)return 0;try{return Convert.ToInt32(p.GetValue(o,null),CultureInfo.InvariantCulture);}catch{return 0;}
        }
        // Generic protobuf message dump: public scalar/string/enum properties and repeated/nested messages (depth 4).
        private static void Dump(StringBuilder sb,object value,int depth)
        {
            if(value==null){sb.Append("null");return;}
            var type=value.GetType();
            if(value is string){sb.Append(Q((string)value));return;}
            if(type.IsEnum){sb.Append(Q(value.ToString()));return;}
            if(type.IsPrimitive||value is decimal){sb.Append(Convert.ToString(value,CultureInfo.InvariantCulture).ToLowerInvariant()=="true"?"true":Convert.ToString(value,CultureInfo.InvariantCulture).ToLowerInvariant()=="false"?"false":Convert.ToString(value,CultureInfo.InvariantCulture));return;}
            if(depth>4){sb.Append("null");return;}
            if(value is IEnumerable){
                sb.Append('[');bool f=true;
                foreach(var item in (IEnumerable)value){if(!f)sb.Append(',');f=false;Dump(sb,item,depth+1);}
                sb.Append(']');return;
            }
            sb.Append('{');bool firstProp=true;
            foreach(var p in type.GetProperties(BindingFlags.Public|BindingFlags.Instance)){
                if(p.GetIndexParameters().Length>0||p.Name=="Parser"||p.Name=="Descriptor"||!p.CanRead)continue;
                object v;try{v=p.GetValue(value,null);}catch{continue;}
                if(!firstProp)sb.Append(',');firstProp=false;
                sb.Append(Q(p.Name)).Append(':');Dump(sb,v,depth+1);
            }
            sb.Append('}');
        }
        private static string Q(string s)
        {
            if(s==null)return "null";
            var b=new StringBuilder(s.Length+2);b.Append('"');
            foreach(char c in s){
                if(c=='"'||c=='\\')b.Append('\\').Append(c);
                else if(c<' ')b.Append("\\u").Append(((int)c).ToString("x4"));
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }
    }
}
