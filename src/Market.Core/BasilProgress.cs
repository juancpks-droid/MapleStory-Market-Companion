namespace Market.Core;

public sealed record BasilCheck(string Fingerprint,string Outcome,DateTimeOffset At);
public static class BasilProgress
{
    public static string Fingerprint(Observation row)=>$"{row.Name}|{row.Quantity}|{row.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    public static bool NeedsCheck(Observation row,SourceConfig config,DateTimeOffset now)
    {lock(config)return !config.BasilChecks.TryGetValue(row.ListingId,out var check)||check.Fingerprint!=Fingerprint(row)||check.Outcome=="Retry"||now-check.At>=TimeSpan.FromDays(1);}
    public static void Complete(Observation original,SourceConfig config,string outcome,DateTimeOffset now)
    {lock(config)config.BasilChecks[original.ListingId]=new(Fingerprint(original),outcome,now);}
    public static string Summary(IEnumerable<Observation> observations,SourceConfig config,DateTimeOffset now)
    {
        lock(config)
        {
        var rows=observations.Where(r=>r.Provider=="BasilMarket"&&r.Kind==EvidenceKind.Asking&&r.Available).DistinctBy(r=>r.ListingId).ToList();
        int left=rows.Count(r=>r.PriceApproximate&&NeedsCheck(r,config,now));
        int rounded=rows.Count(r=>r.PriceApproximate&&!NeedsCheck(r,config,now));
        int exact=rows.Count-left-rounded;
        int pages=config.BasilPages.Count,visited=config.BasilPages.Count(p=>config.BasilVisited.TryGetValue(p,out var at)&&now-at<TimeSpan.FromDays(1));
        var discovery=pages==0?"Category discovery not started; website total unknown.":$"{visited}/{pages} category pages checked today.";
        return $"Basil queue: {left} left to check • {exact} exact • {rounded} checked, still rounded • {rows.Count} discovered sale listings\nDiscovery: {discovery} New listings may increase the queue; failed checks stay pending.";
        }
    }
}
