using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Globalization;

namespace Market.Core;
public sealed partial class Store
{
    void InitializeProfiles()
    {
        Run("BEGIN IMMEDIATE; CREATE TABLE IF NOT EXISTS inventory_undo(id TEXT PRIMARY KEY,payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS transfers(id TEXT PRIMARY KEY,payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS characters(id TEXT PRIMARY KEY,name TEXT NOT NULL,mesos TEXT NOT NULL); CREATE TABLE IF NOT EXISTS profile_state(id INTEGER PRIMARY KEY,profile TEXT NOT NULL); CREATE TABLE IF NOT EXISTS profile_items(profile TEXT NOT NULL,name TEXT NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(profile,name)); CREATE TABLE IF NOT EXISTS stock_changes(id TEXT PRIMARY KEY,profile TEXT NOT NULL,name TEXT NOT NULL,before_payload TEXT,after_payload TEXT NOT NULL); INSERT OR IGNORE INTO characters VALUES('main','Main character','0'); INSERT INTO profile_items SELECT 'main',name,payload FROM items WHERE NOT EXISTS(SELECT 1 FROM profile_state WHERE id=1); INSERT OR IGNORE INTO profile_state VALUES(1,'main'); COMMIT;");
        using var command=db.CreateCommand();command.CommandText="PRAGMA table_info(stock_changes)";bool column=false;using(var reader=command.ExecuteReader()){while(reader.Read())if(reader.GetString(1)=="trade_payload")column=true;}if(!column)Run("ALTER TABLE stock_changes ADD COLUMN trade_payload TEXT");
    }
    public string Profile {get{using var c=db.CreateCommand();c.CommandText="SELECT profile FROM profile_state WHERE id=1";return (string)c.ExecuteScalar()!;}}
    public List<CharacterProfile> Characters(){using var c=db.CreateCommand();c.CommandText="SELECT id,name,mesos FROM characters ORDER BY name";using var r=c.ExecuteReader();var rows=new List<CharacterProfile>();while(r.Read())rows.Add(new(r.GetString(0),r.GetString(1),decimal.Parse(r.GetString(2),CultureInfo.InvariantCulture)));return rows;}
    public void SaveCharacter(CharacterProfile p)
    {
        if((p.Id!="main"&&!Guid.TryParse(p.Id,out _))||string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>80||p.Mesos is <0 or >1_000_000_000_000m)throw new FormatException("Invalid character name or mesos balance");
        Run("INSERT INTO characters VALUES($id,$name,$mesos) ON CONFLICT(id) DO UPDATE SET name=$name,mesos=$mesos",("$id",p.Id),("$name",p.Name.Trim()),("$mesos",p.Mesos.ToString(CultureInfo.InvariantCulture)));
    }
    public void SelectCharacter(string id){if(!Characters().Any(c=>c.Id==id))throw new FormatException("Character not found");Run("UPDATE profile_state SET profile=$p WHERE id=1",("$p",id));}
    public List<SavedItem> ItemsFor(string profile)=>Read<SavedItem>("SELECT payload FROM profile_items WHERE profile=$profile ORDER BY name",("$profile",profile));
    public List<CharacterInventory> AllInventories()=>Characters().Select(c=>new CharacterInventory(c,ItemsFor(c.Id))).ToList();
    public void ReplaceCharacters(List<CharacterInventory> characters,string active,List<TransferRecord>? transfers=null)
    {
        using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="DELETE FROM profile_items;DELETE FROM characters;DELETE FROM stock_changes;DELETE FROM transfers;DELETE FROM inventory_undo;";c.ExecuteNonQuery();
        foreach(var row in characters){c.Parameters.Clear();c.CommandText="INSERT INTO characters VALUES($id,$name,$mesos)";c.Parameters.AddWithValue("$id",row.Character.Id);c.Parameters.AddWithValue("$name",row.Character.Name);c.Parameters.AddWithValue("$mesos",row.Character.Mesos.ToString(CultureInfo.InvariantCulture));c.ExecuteNonQuery();foreach(var item in row.Items){c.Parameters.Clear();c.CommandText="INSERT INTO profile_items VALUES($p,$n,$v)";c.Parameters.AddWithValue("$p",row.Character.Id);c.Parameters.AddWithValue("$n",ItemIdentity.Key(item));c.Parameters.AddWithValue("$v",JsonSerializer.Serialize(item,Json.Options));c.ExecuteNonQuery();}}
        foreach(var t in transfers??[]){c.Parameters.Clear();c.CommandText="INSERT INTO transfers VALUES($id,$v)";c.Parameters.AddWithValue("$id",t.Id);c.Parameters.AddWithValue("$v",JsonSerializer.Serialize(t,Json.Options));c.ExecuteNonQuery();}
        c.Parameters.Clear();c.CommandText="UPDATE profile_state SET profile=$p WHERE id=1";c.Parameters.AddWithValue("$p",active);c.ExecuteNonQuery();tx.Commit();
    }
    public bool HasStockChange(string id){using var c=db.CreateCommand();c.CommandText="SELECT COUNT(*) FROM stock_changes WHERE id=$id";c.Parameters.AddWithValue("$id",id);return (long)c.ExecuteScalar()!>0;}
    public void ApplyStockTrade(TradeEntry t)
    {
        new CompanionFeatures(){Trades=[t]}.Validate();
        var currentProfile=Profile;if(t.Profile!=currentProfile||HasStockChange(t.Id))throw new InvalidOperationException("Trade profile differs or stock was already applied");
        var before=Items().FirstOrDefault(i=>TextKey.Search(i.Name)==TextKey.Search(t.Name)&&i.Variant==t.Variant);
        if(before is not null&&before.Quantity>0&&before.Variant!=t.Variant)throw new InvalidOperationException("Trade variant does not match current inventory");
        long q=checked((before?.Quantity??0)+(t.Buy?t.Quantity:-t.Quantity));if(q<0||q>1_000_000_000)throw new InvalidOperationException("Not enough stock for sale, or resulting quantity exceeds limit");
        var after=(before??new SavedItem(t.Name,0,false,false)) with{Quantity=q,Variant=t.Variant};string payload=JsonSerializer.Serialize(after,Json.Options);
        using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO profile_items VALUES($profile,$name,$after) ON CONFLICT(profile,name) DO UPDATE SET payload=$after; INSERT INTO stock_changes VALUES($id,$profile,$name,$before,$after,$trade);";c.Parameters.AddWithValue("$profile",currentProfile);c.Parameters.AddWithValue("$name",ItemIdentity.Key(after));c.Parameters.AddWithValue("$after",payload);c.Parameters.AddWithValue("$id",t.Id);c.Parameters.AddWithValue("$before",before is null?DBNull.Value:JsonSerializer.Serialize(before,Json.Options));c.Parameters.AddWithValue("$trade",JsonSerializer.Serialize(t,Json.Options));c.ExecuteNonQuery();tx.Commit();
    }
    public List<TradeEntry> LinkedTrades()=>Read<TradeEntry>("SELECT trade_payload FROM stock_changes WHERE trade_payload IS NOT NULL");
    public void UndoStockTrade(string id)
    {
        using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT profile,name,before_payload,after_payload FROM stock_changes WHERE id=$id";c.Parameters.AddWithValue("$id",id);string profile,name,after;string? before;
        using(var r=c.ExecuteReader()){if(!r.Read())throw new InvalidOperationException("No linked stock update to undo");profile=r.GetString(0);name=r.GetString(1);before=r.IsDBNull(2)?null:r.GetString(2);after=r.GetString(3);}
        c.Parameters.Clear();c.CommandText="SELECT payload FROM profile_items WHERE profile=$p AND name=$n";c.Parameters.AddWithValue("$p",profile);c.Parameters.AddWithValue("$n",name);if((string?)c.ExecuteScalar()!=after)throw new InvalidOperationException("Stock changed since this trade. Undo later changes first; no inventory was overwritten.");
        c.CommandText=before is null?"DELETE FROM profile_items WHERE profile=$p AND name=$n":"UPDATE profile_items SET payload=$before WHERE profile=$p AND name=$n";if(before is not null)c.Parameters.AddWithValue("$before",before);c.ExecuteNonQuery();c.CommandText="DELETE FROM stock_changes WHERE id=$id";c.Parameters.Clear();c.Parameters.AddWithValue("$id",id);c.ExecuteNonQuery();tx.Commit();
    }
}
