namespace Market.Core;
public static class ReliabilityTools
{
    public static string EvidenceStatus(Observation row,bool enabled,DateTimeOffset now,int hours)
    {
        if(!enabled)return "Source disabled • cached only";
        if(!row.Available&&row.Kind==EvidenceKind.Asking)return "Unavailable at last check";
        if(row.ObservedAt is null)return "Source time unknown";
        if(!row.Fresh(now,TimeSpan.FromHours(hours)))return "Old source post • stale evidence";
        if(row.RetrievedAt<now.AddMinutes(-30)||row.RetrievedAt>now.AddMinutes(2))return "Cached • availability needs recheck";
        return row.Kind switch{EvidenceKind.Asking=>"Recently checked ask • not a sale",EvidenceKind.ReportedSale=>"Reported sale • unverified",EvidenceKind.ConfirmedSale=>"Confirmed sale evidence",_=>"Reference evidence only"};
    }
    public static bool CanDeliver(InboxEntry entry,IEnumerable<SavedItem> items,IEnumerable<Observation> rows,DateTimeOffset now,int hours)
    {
        if(entry.ItemName.Length>0&&items.Any(i=>TextKey.Search(i.Name)==TextKey.Search(entry.ItemName)&&i.Muted&&(entry.Variant.Length==0||i.Variant==entry.Variant)))return false;
        if(entry.ListingKey.Length==0)return !entry.Key.StartsWith("inbox-deal:")&&!entry.Key.StartsWith("target:")&&!entry.Key.StartsWith("wish:");
        return rows.Any(o=>o.Key==entry.ListingKey&&o.Variant==entry.Variant&&o.UnitPrice==entry.Price&&o.Available&&o.Kind==EvidenceKind.Asking&&o.Fresh(now,TimeSpan.FromHours(hours))&&o.RetrievedAt>=now.AddMinutes(-30)&&o.RetrievedAt<=now.AddMinutes(2));
    }
}
