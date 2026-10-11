using System.Text.Json;
namespace Market.Core;
public sealed record InventoryUndo(string Id,string Profile,string Description,string Before,string After,DateTimeOffset At,bool Undone=false);
public sealed record PriceMovement(string Name,string Seller,decimal Before,decimal After,DateTimeOffset PreviousCheck,DateTimeOffset LatestCheck,Observation Listing);
public sealed record ProfitRow(string Name,string Variant,decimal Profit,decimal Fees,long Unsold,decimal Cost,long Unmatched);
public static class UsabilityTools
{
    public static List<PriceMovement> Changes(IEnumerable<Observation> history)=>history.Where(o=>o.Kind==EvidenceKind.Asking&&!o.PriceApproximate&&o.UnitPrice>0).GroupBy(o=>new{o.Key,o.Variant}).Select(g=>g.OrderByDescending(o=>o.RetrievedAt).ThenByDescending(o=>o.ObservedAt).ToList()).Where(g=>g.Count>=2&&g[0].RetrievedAt>g[1].RetrievedAt&&g[0].UnitPrice!=g[1].UnitPrice).Select(g=>new PriceMovement(g[0].Name,g[0].Seller,g[1].UnitPrice,g[0].UnitPrice,g[1].RetrievedAt,g[0].RetrievedAt,g[0])).OrderByDescending(x=>x.LatestCheck).ToList();
    public static List<ProfitRow> Profits(IEnumerable<TradeEntry> trades)
    {
        var result=new List<ProfitRow>();foreach(var group in trades.GroupBy(t=>new{t.Profile,Name=TextKey.Search(t.Name),t.Variant})){var all=group.ToList();var outcomes=CompanionFeatures.Outcomes(all);decimal matchedCost=0;long sold=0;foreach(var o in outcomes){var t=all.Single(t=>t.Id==o.Id);long matched=t.Quantity-o.UnmatchedUnits;sold+=matched;matchedCost+=matched*(t.UnitPrice-t.Fees/t.Quantity)-o.MatchedProfit;}result.Add(new(all[0].Name,group.Key.Variant,outcomes.Sum(o=>o.MatchedProfit),all.Sum(t=>t.Fees),all.Where(t=>t.Buy).Sum(t=>t.Quantity)-sold,all.Where(t=>t.Buy).Sum(t=>t.Quantity*t.UnitPrice+t.Fees)-matchedCost,outcomes.Sum(o=>o.UnmatchedUnits)));}return result.OrderByDescending(r=>r.Profit).ToList();
    }
}
public sealed partial class Store
{
    string InventoryJson()=>JsonSerializer.Serialize(Items().OrderBy(i=>ItemIdentity.Key(i)),Json.Options);
    public List<InventoryUndo> InventoryChanges()=>Read<InventoryUndo>("SELECT payload FROM inventory_undo ORDER BY rowid DESC");
    void WriteUndo(string before,string description)
    {
        string after=InventoryJson();if(before==after)return;var record=new InventoryUndo(Guid.NewGuid().ToString(),Profile,description,before,after,DateTimeOffset.UtcNow);Run("INSERT INTO inventory_undo VALUES($id,$v)",("$id",record.Id),("$v",JsonSerializer.Serialize(record,Json.Options)));Run("DELETE FROM inventory_undo WHERE rowid NOT IN (SELECT rowid FROM inventory_undo ORDER BY rowid DESC LIMIT 200)");
    }
    void ChangeInventory(string description,Action action){using var tx=db.BeginTransaction();var before=InventoryJson();action();WriteUndo(before,description);tx.Commit();}
    void ApplyBag(List<SavedItem> items){Run("DELETE FROM profile_items WHERE profile=$p",("$p",Profile));foreach(var i in items)Run("INSERT INTO profile_items VALUES($p,$n,$v)",("$p",Profile),("$n",ItemIdentity.Key(i)),("$v",JsonSerializer.Serialize(i,Json.Options)));}
    public void UndoInventory(string id)
    {
        using var tx=db.BeginTransaction();var entry=InventoryChanges().SingleOrDefault(x=>x.Id==id)??throw new InvalidOperationException("Undo entry not found");if(entry.Profile!=Profile||entry.Undone)throw new InvalidOperationException("Switch to the matching character or choose a change that has not been undone");if(InventoryJson()!=entry.After)throw new InvalidOperationException("Inventory has changed since this action. Undo newer changes first; nothing was overwritten.");var items=JsonSerializer.Deserialize<List<SavedItem>>(entry.Before,Json.Options)!;new PersonalBackup(1,items,new()).Validate();ApplyBag(items);Run("UPDATE inventory_undo SET payload=$v WHERE id=$id",("$id",id),("$v",JsonSerializer.Serialize(entry with{Undone=true},Json.Options)));tx.Commit();
    }
}
