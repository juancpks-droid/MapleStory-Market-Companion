using System.Text.Json;
using System.Globalization;
namespace Market.Core;
public sealed record TransferRecord(string Id,string From,string To,string FromName,string ToName,string Item,string Variant,long Quantity,decimal Mesos,DateTimeOffset At);
public sealed record ShoppingEntry(string Id,string Profile,string Name,string Variant,long Quantity,long Purchased,decimal Budget);
public sealed record SellEntry(string Id,string Profile,string Name,long Quantity){public string Variant {get;init;}="";}
public sealed class WorkspaceOptions
{
    public List<string> PinnedPages {get;set;}=[];
    public List<ShoppingEntry> Shopping {get;set;}=[];
    public List<SellEntry> Selling {get;set;}=[];
    public Dictionary<string,List<string>> SlotOrder {get;set;}=[];
    public Dictionary<string,HashSet<string>> Favorites {get;set;}=[];
    public List<string> DashboardCards {get;set;}=["Market","Deals","Watchlist","Inventory"];
    public bool QuietHours {get;set;}
    public int QuietStart {get;set;}=22;
    public int QuietEnd {get;set;}=8;
    public int CooldownMinutes {get;set;}=5;
    public bool GroupAlerts {get;set;}=true;
    public DateTimeOffset LastDesktopAlert {get;set;}
    public HashSet<string> DeliveredAlerts {get;set;}=[];
    public bool QuietAt(DateTime local)=>QuietHours&&(QuietStart==QuietEnd|| (QuietStart<QuietEnd?local.Hour>=QuietStart&&local.Hour<QuietEnd:local.Hour>=QuietStart||local.Hour<QuietEnd));
    public bool CanNotify(DateTimeOffset now)=>!QuietAt(now.LocalDateTime)&&now>=LastDesktopAlert.AddMinutes(CooldownMinutes);
    public void Validate()
    {
        bool Profile(string p)=>p=="main"||Guid.TryParse(p,out _);
        if(PinnedPages is null||PinnedPages.Count>40||PinnedPages.Any(p=>string.IsNullOrWhiteSpace(p)||p.Length>100)||PinnedPages.Distinct().Count()!=PinnedPages.Count)throw new FormatException("Invalid pinned pages");
        if(Shopping is null||Selling is null||SlotOrder is null||Favorites is null||DashboardCards is null||DeliveredAlerts is null||Shopping.Count+Selling.Count>50000||DeliveredAlerts.Count>2000||QuietStart is <0 or >23||QuietEnd is <0 or >23||CooldownMinutes is <0 or >1440)throw new FormatException("Invalid workspace options");
        if(Shopping.Any(x=>x is null||!Guid.TryParse(x.Id,out _)||!Profile(x.Profile)||string.IsNullOrWhiteSpace(x.Name)||x.Name.Length>250||x.Variant is null||x.Variant.Length>4000||x.Quantity is <=0 or >1_000_000_000||x.Purchased<0||x.Purchased>x.Quantity||x.Budget is <0 or >1_000_000_000_000m)||Selling.Any(x=>x is null||!Guid.TryParse(x.Id,out _)||!Profile(x.Profile)||x.Variant is null||x.Variant.Length>4000||string.IsNullOrWhiteSpace(x.Name)||x.Name.Length>250||x.Quantity is <=0 or >1_000_000_000))throw new FormatException("Invalid shopping or selling entry");
        if(Shopping.Select(x=>x.Id).Distinct().Count()!=Shopping.Count||Selling.Select(x=>x.Id).Distinct().Count()!=Selling.Count)throw new FormatException("Duplicate plan entry");
        if(SlotOrder.Any(x=>!Profile(x.Key)||x.Value is null||x.Value.Count>100000||x.Value.Any(n=>string.IsNullOrWhiteSpace(n)||n.Length>5000)||x.Value.Distinct().Count()!=x.Value.Count)||Favorites.Any(x=>!Profile(x.Key)||x.Value is null||x.Value.Count>100000||x.Value.Any(n=>string.IsNullOrWhiteSpace(n)||n.Length>5000)))throw new FormatException("Invalid inventory layout");
        if(DashboardCards.Count>4||DashboardCards.Distinct().Count()!=DashboardCards.Count||DashboardCards.Any(x=>!new[]{"Market","Deals","Watchlist","Inventory"}.Contains(x)))throw new FormatException("Invalid dashboard cards");
    }
    public static decimal? ShoppingCost(ShoppingEntry entry,IEnumerable<Observation> rows,DateTimeOffset now,int hours)
    {
        var prices=rows.Where(o=>TextKey.Search(o.Name)==TextKey.Search(entry.Name)&&o.Kind==EvidenceKind.Asking&&o.Available&&!o.PriceApproximate&&o.UnitPrice>0&&o.Fresh(now,TimeSpan.FromHours(hours))&&o.RetrievedAt>=now.AddMinutes(-30)&&(!o.Equipment||o.VariantComplete&&entry.Variant.Length>0&&o.Variant==entry.Variant)).Select(o=>(decimal?)o.UnitPrice).ToList();return entry.Quantity==entry.Purchased?0:prices.Count==0?null:prices.Min()*(entry.Quantity-entry.Purchased);
    }
    public static decimal TradeTotal(bool buy,long quantity,decimal price,decimal fees)=>quantity*price+(buy?fees:-fees);
}
public sealed partial class Store
{
    public List<TransferRecord> Transfers()=>Read<TransferRecord>("SELECT payload FROM transfers ORDER BY rowid DESC");
    public TransferRecord Transfer(string destination,string itemName,long quantity,decimal mesos,string? variant=null)
    {
        if(destination==Profile||quantity<0||mesos<0||mesos>1_000_000_000_000m||quantity>1_000_000_000||(quantity==0&&mesos==0)||quantity>0&&string.IsNullOrWhiteSpace(itemName))throw new InvalidOperationException("Choose another profile and a positive item quantity or mesos amount.");
        using var tx=db.BeginTransaction();var from=Characters().Single(c=>c.Id==Profile);var to=Characters().SingleOrDefault(c=>c.Id==destination)??throw new InvalidOperationException("Destination profile missing");
        if(from.Mesos<mesos||to.Mesos+mesos>1_000_000_000_000m)throw new InvalidOperationException("Insufficient mesos or destination balance exceeds limit");
        SavedItem? source=null,target=null;if(quantity>0){var candidates=ItemsFor(from.Id).Where(i=>TextKey.Search(i.Name)==TextKey.Search(itemName)&&(variant is null||i.Variant==variant)).ToList();if(candidates.Count!=1)throw new InvalidOperationException("Select an exact inventory variant");source=candidates[0];target=ItemsFor(to.Id).FirstOrDefault(i=>TextKey.Search(i.Name)==TextKey.Search(itemName)&&i.Variant==source.Variant);if(source.Quantity<quantity||(target?.Quantity??0)+quantity>1_000_000_000)throw new InvalidOperationException("Insufficient stock or destination quantity exceeds limit");if(target is not null&&target.Quantity>0&&target.Variant!=source.Variant)throw new InvalidOperationException("Destination holds a different equipment variant");}
        using var c=db.CreateCommand();c.Transaction=tx;
        void Write(string sql,params (string,object)[] args){c.Parameters.Clear();c.CommandText=sql;foreach(var (k,v) in args)c.Parameters.AddWithValue(k,v);c.ExecuteNonQuery();}
        if(source is not null){var moved=(target??new SavedItem(source.Name,0,false,false)) with{Quantity=(target?.Quantity??0)+quantity,Variant=source.Variant};foreach(var pair in new[]{(from.Id,source with{Quantity=source.Quantity-quantity}),(to.Id,moved)})Write("INSERT INTO profile_items VALUES($p,$n,$v) ON CONFLICT(profile,name) DO UPDATE SET payload=$v",("$p",pair.Id),("$n",ItemIdentity.Key(pair.Item2)),("$v",JsonSerializer.Serialize(pair.Item2,Json.Options)));}
        foreach(var pair in new[]{(from.Id,from.Mesos-mesos),(to.Id,to.Mesos+mesos)})Write("UPDATE characters SET mesos=$m WHERE id=$p",("$p",pair.Id),("$m",pair.Item2.ToString(CultureInfo.InvariantCulture)));
        var record=new TransferRecord(Guid.NewGuid().ToString(),from.Id,to.Id,from.Name,to.Name,source?.Name??"",source?.Variant??"",quantity,mesos,DateTimeOffset.UtcNow);Write("INSERT INTO transfers VALUES($id,$v)",("$id",record.Id),("$v",JsonSerializer.Serialize(record,Json.Options)));tx.Commit();return record;
    }
}
