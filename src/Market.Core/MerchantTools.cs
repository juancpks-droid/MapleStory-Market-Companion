namespace Market.Core;

public sealed record MerchantGoal(string Id,string Profile,string Name,decimal Target);
public sealed record RestockRule(string Id,string Profile,string Name,long Minimum);
public sealed record ItemMemo(string Id,string Profile,string Name,string Tag,string Notes);
public sealed record GearWish(string Id,string Profile,string Name,string Variant,decimal Maximum);
public sealed record MerchantSession(string Id,string Profile,string Name,DateTimeOffset Start,DateTimeOffset? End){public decimal? StartMesos {get;init;} public decimal? EndMesos {get;init;}};
public sealed record SessionTotals(decimal Purchases,decimal Sales,decimal Fees,decimal Profit,long Unmatched,int Trades);
public static class MerchantTools
{
    public static readonly string[] Themes=["Henesys","Ellinia","Kerning City","Perion"];
    public static readonly string[] Tags=["Keep","Sell","Upgrade","For another character"];
    public static decimal Progress(decimal balance,decimal target)=>target<=0?0:Math.Clamp(balance/target*100,0,100);
    public static long Needed(RestockRule rule,IEnumerable<SavedItem> items)=>Math.Max(0,rule.Minimum-items.Where(i=>TextKey.Search(i.Name)==TextKey.Search(rule.Name)).Sum(i=>i.Quantity));
    public static bool Matches(GearWish wish,Observation o,DateTimeOffset now,int hours)=>!o.PriceApproximate&&o.Equipment&&o.VariantComplete&&wish.Variant.Length>0&&wish.Variant==o.Variant&&TextKey.Search(wish.Name)==TextKey.Search(o.Name)&&o.Kind==EvidenceKind.Asking&&o.Available&&o.UnitPrice>0&&(wish.Maximum==0||o.UnitPrice<=wish.Maximum)&&o.Fresh(now,TimeSpan.FromHours(hours))&&o.RetrievedAt>=now.AddMinutes(-30);
    public static List<Observation> History(IEnumerable<Observation> rows)=>rows.Where(o=>o.UnitPrice>0&&o.ObservedAt is not null).GroupBy(o=>new{o.Key,o.ObservedAt,o.UnitPrice,o.Variant}).Select(g=>g.MaxBy(o=>o.RetrievedAt)!).OrderBy(o=>o.ObservedAt).ToList();
    public static SessionTotals Totals(MerchantSession session,IEnumerable<TradeEntry> all,DateTimeOffset now)
    {
        var trades=all.Where(t=>t.Profile==session.Profile).ToList();var window=trades.Where(t=>t.At>=session.Start&&t.At<(session.End??now.AddTicks(1))).ToList();var ids=window.Select(t=>t.Id).ToHashSet();var outcomes=CompanionFeatures.Outcomes(trades).Where(o=>ids.Contains(o.Id)).ToList();
        return new(window.Where(t=>t.Buy).Sum(t=>t.Quantity*t.UnitPrice),window.Where(t=>!t.Buy).Sum(t=>t.Quantity*t.UnitPrice),window.Sum(t=>t.Fees),outcomes.Sum(t=>t.MatchedProfit),outcomes.Sum(t=>t.UnmatchedUnits),window.Count);
    }
}
