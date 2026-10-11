namespace Market.Core;
public sealed record TradeTemplate(string Id,string Profile,string Label,string Name,string Variant,bool Buy,long Quantity,decimal Price,decimal Fees);
public sealed record WatchPriority(string Profile,string Name,string Variant,string Group,decimal Threshold);
public sealed class PlanningOptions
{
    public List<TradeTemplate> Templates {get;set;}=[];
    public List<WatchPriority> Priorities {get;set;}=[];
    public Dictionary<string,decimal> Budgets {get;set;}=[];
    public List<string> DashboardPanels {get;set;}=["Watchlist","Deals","FM","Merchant"];
    public void Validate()
    {
        bool Profile(string p)=>p=="main"||Guid.TryParse(p,out _);
        if(Templates is null||Priorities is null||Budgets is null||DashboardPanels is null||Templates.Count>10000||Priorities.Count>10000||Budgets.Count>1000)throw new FormatException("Invalid planning data");
        foreach(var t in Templates){if(t is null||!Guid.TryParse(t.Id,out _)||!Profile(t.Profile)||string.IsNullOrWhiteSpace(t.Label)||t.Label.Length>250)throw new FormatException("Invalid template");new CompanionFeatures{Trades=[new(t.Id,t.Name,t.Variant,t.Buy,t.Quantity,t.Price,t.Fees,DateTimeOffset.UtcNow){Profile=t.Profile}]}.Validate();}
        if(Templates.Select(t=>t.Id).Distinct().Count()!=Templates.Count||Priorities.Any(p=>p is null||!Profile(p.Profile)||string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>250||p.Variant is null||p.Variant.Length>4000||!new[]{"Buy soon","Sell soon","Just tracking"}.Contains(p.Group)||p.Threshold is <0 or >1_000_000_000_000m)||Priorities.Select(p=>p.Profile+ItemIdentity.Key(p.Name,p.Variant)).Distinct().Count()!=Priorities.Count||Budgets.Any(p=>!Profile(p.Key)||p.Value is <0 or >1_000_000_000_000m)||DashboardPanels.Distinct().Count()!=DashboardPanels.Count||DashboardPanels.Any(p=>!new[]{"Watchlist","Deals","FM","Merchant"}.Contains(p)))throw new FormatException("Invalid planner preferences");
    }
}
public sealed record StockBasis(long JournalUnits,decimal? Cost,DateTimeOffset? Oldest);
public static class PlanningTools
{
    public static StockBasis Basis(SavedItem item,IEnumerable<TradeEntry> journal)
    {
        var lots=new Queue<(long qty,decimal cost,DateTimeOffset at)>();
        foreach(var t in journal.Where(t=>ItemIdentity.Key(t.Name,t.Variant)==ItemIdentity.Key(item)).OrderBy(t=>t.At).ThenBy(t=>t.Buy?0:1).ThenBy(t=>t.Id,StringComparer.Ordinal))
        {if(t.Buy)lots.Enqueue((t.Quantity,t.UnitPrice+t.Fees/t.Quantity,t.At));else{long n=t.Quantity;while(n>0&&lots.Count>0){var lot=lots.Dequeue();long used=Math.Min(n,lot.qty);n-=used;if(lot.qty>used){var remaining=lots.ToArray();lots.Clear();lots.Enqueue((lot.qty-used,lot.cost,lot.at));foreach(var r in remaining)lots.Enqueue(r);}}}}
        long units=lots.Sum(l=>l.qty);return new(units,units==item.Quantity&&units>0?lots.Sum(l=>l.qty*l.cost):null,units==item.Quantity&&units>0?lots.Peek().at:null);
    }
    public static bool PriorityMatches(WatchPriority p,Observation o,DateTimeOffset now,int hours)=>p.Group!="Just tracking"&&p.Threshold>0&&TextKey.Search(p.Name)==TextKey.Search(o.Name)&&(!o.Equipment||o.VariantComplete&&p.Variant.Length>0&&o.Variant==p.Variant)&&o.Kind==EvidenceKind.Asking&&o.Available&&!o.PriceApproximate&&o.Fresh(now,TimeSpan.FromHours(hours))&&o.RetrievedAt>=now.AddMinutes(-30)&&(p.Group=="Buy soon"?o.UnitPrice<=p.Threshold:o.UnitPrice>=p.Threshold);
    public static TradeEntry FromTemplate(TradeTemplate t,DateTimeOffset now)=>new(Guid.NewGuid().ToString(),t.Name,t.Variant,t.Buy,t.Quantity,t.Price,t.Fees,now){Profile=t.Profile};
    public static List<Observation> VariantHistory(IEnumerable<Observation> rows,string name,string variant)=>MerchantTools.History(rows.Where(o=>TextKey.Search(o.Name)==TextKey.Search(name)&&(o.Equipment?o.VariantComplete&&o.Variant==variant:variant.Length==0)&&o.Kind==EvidenceKind.Asking));
}
