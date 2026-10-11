namespace Market.Core;

public sealed record BasilEtaResult(DateTimeOffset? QueueFinish,DateTimeOffset? DiscoveryFinish,string Status);
public static class BasilEta
{
    // Conservative planning: eight detail checks and fifteen total requests per batch.
    // It estimates the known backlog, never an unknown website-wide listing count.
    public static BasilEtaResult Estimate(IEnumerable<Observation> rows,SourceConfig config,DateTimeOffset now,bool offline=false,bool busy=false)
    {
        lock(config)
        {
            if(offline||!config.Authorized)return new(null,null,offline?"Paused while offline":"Paused: BasilMarket disabled");
            if(busy)return new(null,null,"Updating estimate after the current refresh");
            var pending=rows.Where(r=>r.Provider=="BasilMarket"&&r.Available&&r.Kind==EvidenceKind.Asking&&r.PriceApproximate&&BasilProgress.NeedsCheck(r,config,now)).DistinctBy(r=>r.ListingId).ToList();
            bool retry=pending.Any(r=>config.BasilChecks.TryGetValue(r.ListingId,out var check)&&check.Outcome=="Retry");
            int queueBatches=(pending.Count+7)/8;
            int discoveryBatches=(config.BasilPages.Count(p=>!config.BasilVisited.TryGetValue(p,out var checkedAt)||now-checkedAt>=TimeSpan.FromDays(1))+1)/2;
            var interval=TimeSpan.FromMinutes(Math.Max(15,config.IntervalMinutes));
            var start=new[]{now,config.NextAllowed,config.BlockedUntil}.Max();
            foreach(var row in pending)if(config.PageChecked.TryGetValue("/market/listing/"+row.ListingId,out var at)&&at+interval>start)start=at+interval;
            var requests=config.RequestTimes.Where(t=>t>now.AddHours(-1)).Order().ToList();
            DateTimeOffset? queue=queueBatches==0?now:null,discovery=config.BasilPages.Count==0?null:discoveryBatches==0?now:null;
            for(int batch=1;batch<=Math.Max(queueBatches,discoveryBatches);batch++)
            {
                requests.RemoveAll(t=>t<=start.AddHours(-1));
                if(requests.Count>45){start=requests[requests.Count-46].AddHours(1).AddSeconds(1);requests.RemoveAll(t=>t<=start.AddHours(-1));}
                var finish=start.AddSeconds(45);requests.AddRange(Enumerable.Repeat(finish,15));requests.Sort();
                if(batch==queueBatches)queue=finish;
                if(batch==discoveryBatches&&config.BasilPages.Count>0)discovery=finish;
                start+=interval;
            }
            return new(retry?null:queue,discovery,retry?"Retryable failures: queue finish time uncertain":"Approximate; assumes successful checks while the app stays open");
        }
    }
    public static string Display(BasilEtaResult result,DateTimeOffset now)
    {
        string Time(DateTimeOffset? end)=>end is null?"unknown":end<=now?"done":$"about {Math.Max(1,(int)Math.Ceiling((end.Value-now).TotalMinutes))} min (around {end.Value.ToLocalTime():t})";
        if(result.Status.StartsWith("Paused")||result.Status.StartsWith("Updating"))return "Basil ETA: "+result.Status;
        return $"Basil ETA - known queue: {Time(result.QueueFinish)}; category pass: {Time(result.DiscoveryFinish)}.\n{result.Status}. Newly discovered listings can extend the queue.";
    }
}
