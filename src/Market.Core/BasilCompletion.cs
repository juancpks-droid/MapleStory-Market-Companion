namespace Market.Core;

public static class BasilCompletion
{
    public static bool IsComplete(IEnumerable<Observation> observations,SourceConfig config,DateTimeOffset now)
    {
        lock(config)
        {
            if(config.BasilPages.Count==0||config.BasilPages.Any(p=>!config.BasilVisited.TryGetValue(p,out var at)||now-at>=TimeSpan.FromDays(1)||at>now.AddMinutes(2)))return false;
            var rows=observations.Where(r=>r.Provider=="BasilMarket"&&r.Available&&r.Kind==EvidenceKind.Asking).ToList();
            return rows.Count>0&&rows.All(r=>!r.PriceApproximate||!BasilProgress.NeedsCheck(r,config,now));
        }
    }
    public static void Observe(IEnumerable<Observation> observations,SourceConfig config,DateTimeOffset now)
    {
        var complete=IsComplete(observations,config,now);
        if(complete&&!config.BasilWasComplete)config.BasilCompletionKey="basil-complete:"+Guid.NewGuid().ToString("N");
        config.BasilWasComplete=complete;
    }
    public static bool CanDeliver(string key,IEnumerable<Observation> rows,SourceConfig config,DateTimeOffset now,bool enabled)=>
        enabled&&config.Authorized&&config.BasilWasComplete&&key==config.BasilCompletionKey&&IsComplete(rows,config,now);
}
