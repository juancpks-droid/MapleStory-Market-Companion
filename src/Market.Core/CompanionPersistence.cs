using System.Text.Json;
using System.Globalization;
namespace Market.Core;

// Mobile uses the same SQLite transaction for journal, stock and companion state.
// Windows' existing file storage remains compatible through PersonalBackup.
public sealed partial class Store
{
    void EnsureCompanion()=>Run("CREATE TABLE IF NOT EXISTS companion_state(id INTEGER PRIMARY KEY,payload TEXT NOT NULL)");
    public CompanionFeatures LoadCompanion()
    {
        EnsureCompanion();var result=Read<CompanionFeatures>("SELECT payload FROM companion_state WHERE id=1").FirstOrDefault()??new();result.Validate();return result;
    }
    public void SaveCompanion(CompanionFeatures features)
    {
        features.Validate();EnsureCompanion();Run("INSERT INTO companion_state VALUES(1,$v) ON CONFLICT(id) DO UPDATE SET payload=$v",("$v",JsonSerializer.Serialize(features,Json.Options)));
    }
    public PersonalBackup CompanionSnapshot()=>new(3,Items(),LoadCompanion()){Characters=AllInventories(),ActiveCharacter=Profile,Transfers=Transfers()};
    public void RestoreCompanion(PersonalBackup backup)
    {
        backup.Validate();var inventories=backup.Version==1?new List<CharacterInventory>{new(new("main","Main character",0),backup.Items)}:backup.Characters!;
        var ids=inventories.Select(c=>c.Character.Id).ToHashSet();
        if(backup.Features.Trades.Any(t=>!ids.Contains(t.Profile))||backup.Features.Rules.Any(t=>!ids.Contains(t.Profile)))throw new FormatException("Backup references a missing character.");
        EnsureCompanion();using var tx=db.BeginTransaction();
        Run("DELETE FROM profile_items;DELETE FROM characters;DELETE FROM stock_changes;DELETE FROM transfers;DELETE FROM inventory_undo;");
        foreach(var row in inventories)
        {
            SaveCharacter(row.Character);
            foreach(var item in row.Items)Run("INSERT INTO profile_items VALUES($p,$n,$v)",("$p",row.Character.Id),("$n",ItemIdentity.Key(item)),("$v",JsonSerializer.Serialize(item,Json.Options)));
        }
        foreach(var t in backup.Transfers)Run("INSERT INTO transfers VALUES($id,$v)",("$id",t.Id),("$v",JsonSerializer.Serialize(t,Json.Options)));
        Run("UPDATE profile_state SET profile=$p WHERE id=1",("$p",backup.Version==1?"main":backup.ActiveCharacter));
        SaveCompanion(backup.Features);tx.Commit();
    }
    public void RecordCompanionTrade(TradeEntry trade,bool updateStock)
    {
        var features=LoadCompanion();if(trade.Profile!=Profile)throw new InvalidOperationException("Switch to the draft's character first.");
        if(features.Trades.Any(t=>t.Id==trade.Id))throw new InvalidOperationException("This trade has already been recorded.");
        features.Trades.Add(trade);features.Validate();
        using var tx=db.BeginTransaction();
        if(updateStock)
        {
            if(HasStockChange(trade.Id))throw new InvalidOperationException("Stock was already updated for this trade.");
            var before=Items().FirstOrDefault(i=>ItemIdentity.Key(i)==ItemIdentity.Key(trade.Name,trade.Variant));
            var quantity=checked((before?.Quantity??0)+(trade.Buy?trade.Quantity:-trade.Quantity));
            if(quantity<0||quantity>1_000_000_000)throw new InvalidOperationException("Not enough stock, or resulting quantity exceeds the limit.");
            var after=(before??new SavedItem(trade.Name,0,false,false)) with{Quantity=quantity};var value=JsonSerializer.Serialize(after with{Variant=trade.Variant},Json.Options);
            var key=ItemIdentity.Key(trade.Name,trade.Variant);
            Run("INSERT INTO profile_items VALUES($p,$n,$v) ON CONFLICT(profile,name) DO UPDATE SET payload=$v",("$p",Profile),("$n",key),("$v",value));
            Run("INSERT INTO stock_changes VALUES($id,$p,$n,$before,$after,$trade)",("$id",trade.Id),("$p",Profile),("$n",key),("$before",before is null?null:JsonSerializer.Serialize(before,Json.Options)),("$after",value),("$trade",JsonSerializer.Serialize(trade,Json.Options)));
        }
        SaveCompanion(features);tx.Commit();
    }
    public void RemoveCompanionTrade(string id)
    {
        var features=LoadCompanion();var trade=features.Trades.Single(t=>t.Id==id);
        if(trade.Profile!=Profile)throw new InvalidOperationException("Switch to this trade's character first.");
        using var tx=db.BeginTransaction();
        if(HasStockChange(id))
        {
            using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT name,before_payload,after_payload FROM stock_changes WHERE id=$id";c.Parameters.AddWithValue("$id",id);
            string name,after;string? before;using(var r=c.ExecuteReader()){r.Read();name=r.GetString(0);before=r.IsDBNull(1)?null:r.GetString(1);after=r.GetString(2);}
            var current=Items().FirstOrDefault(i=>ItemIdentity.Key(i)==name);
            if(current is null||JsonSerializer.Serialize(current,Json.Options)!=after)throw new InvalidOperationException("Stock changed after this trade. Undo newer stock edits before removing it.");
            if(before is null)Run("DELETE FROM profile_items WHERE profile=$p AND name=$n",("$p",Profile),("$n",name));
            else Run("UPDATE profile_items SET payload=$v WHERE profile=$p AND name=$n",("$v",before),("$p",Profile),("$n",name));
            Run("DELETE FROM stock_changes WHERE id=$id",("$id",id));
        }
        features.Trades.Remove(trade);SaveCompanion(features);tx.Commit();
    }
}
