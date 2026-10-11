using System.Text.Json;
namespace Market.Core;
public static class ItemIdentity
{
    public static string Key(string name,string variant)=>JsonSerializer.Serialize(new[]{TextKey.Search(name),variant});
    public static string Key(SavedItem item)=>Key(item.Name,item.Variant);
    public static SavedItem? Resolve(IEnumerable<SavedItem> items,string key){var list=items.ToList();return list.FirstOrDefault(i=>Key(i)==key)??(list.Count(i=>TextKey.Search(i.Name)==TextKey.Search(key))==1?list.Single(i=>TextKey.Search(i.Name)==TextKey.Search(key)):null);}
}
public sealed class PolishOptions
{
    public Dictionary<string,string> Aliases {get;set;}=[];
    public bool HighContrast {get;set;}
    public int SlotSize {get;set;}=60;
    public int InventoryDetailWidth {get;set;}=280;
    public void Validate(){if(Aliases is null||Aliases.Count>10000||Aliases.Any(p=>string.IsNullOrWhiteSpace(p.Key)||string.IsNullOrWhiteSpace(p.Value)||p.Key.Length>250||p.Value.Length>250)||SlotSize is <60 or >100||InventoryDetailWidth is <220 or >450)throw new FormatException("Invalid accessibility or alias preferences");}
    public string Resolve(string query)=>Aliases.FirstOrDefault(p=>TextKey.Search(p.Key)==TextKey.Search(query)).Value??query;
}
public static class PolishTools
{
    public static bool Similar(string query,string name){query=TextKey.Search(query);name=TextKey.Search(name);if(query.Length==0||name.Contains(query))return true;var initials=string.Concat(name.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(w=>w[0]));if(query==initials)return true;if(query.Length<4||Math.Abs(query.Length-name.Length)>2)return false;var previous=Enumerable.Range(0,name.Length+1).ToArray();for(int i=1;i<=query.Length;i++){var current=new int[name.Length+1];current[0]=i;for(int j=1;j<=name.Length;j++)current[j]=Math.Min(Math.Min(current[j-1]+1,previous[j]+1),previous[j-1]+(query[i-1]==name[j-1]?0:1));previous=current;}return previous[^1]<= (query.Length>=8?2:1);}
    public static bool Duplicate(TradeEntry a,TradeEntry b)=>a.Id!=b.Id&&a.Profile==b.Profile&&TextKey.Search(a.Name)==TextKey.Search(b.Name)&&a.Variant==b.Variant&&a.Buy==b.Buy&&a.Quantity==b.Quantity&&a.UnitPrice==b.UnitPrice&&a.Fees==b.Fees&&Math.Abs((a.At-b.At).TotalMinutes)<=5;
}
public sealed partial class Store
{
    void MigrateVariantKeys()
    {
        Run("CREATE TABLE IF NOT EXISTS schema_flags(name TEXT PRIMARY KEY)");using var check=db.CreateCommand();check.CommandText="SELECT COUNT(*) FROM schema_flags WHERE name='variants14'";if((long)check.ExecuteScalar()!>0)return;
        using var tx=db.BeginTransaction();var inventories=AllInventories();Run("DELETE FROM profile_items");foreach(var inventory in inventories)foreach(var item in inventory.Items)Run("INSERT INTO profile_items VALUES($p,$n,$v)",("$p",inventory.Character.Id),("$n",ItemIdentity.Key(item)),("$v",JsonSerializer.Serialize(item,Json.Options)));
        using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT id,after_payload FROM stock_changes";var changes=new List<(string,string)>();using(var r=c.ExecuteReader()){while(r.Read()){var item=JsonSerializer.Deserialize<SavedItem>(r.GetString(1),Json.Options)!;changes.Add((r.GetString(0),ItemIdentity.Key(item)));}}foreach(var (id,key) in changes)Run("UPDATE stock_changes SET name=$n WHERE id=$id",("$n",key),("$id",id));Run("INSERT INTO schema_flags VALUES('variants14')");tx.Commit();
    }
}
