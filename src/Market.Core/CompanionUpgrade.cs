using System.Text.Json;
using System.Text.RegularExpressions;

namespace Market.Core;

public sealed record TradeDraft(TradeEntry Entry, TradeEntry? Original, bool UpdateStock, DateTimeOffset SavedAt)
{
    public void Validate()
    {
        if(Entry is null || Entry.Name is null || Entry.Variant is null || Entry.Name.Length>250 || Entry.Variant.Length>4000)throw new FormatException("Invalid draft fields");
        new CompanionFeatures{Trades=[Entry with{Name=string.IsNullOrWhiteSpace(Entry.Name)?"Unfinished item":Entry.Name}]}.Validate();
        if(Original is not null){new CompanionFeatures{Trades=[Original]}.Validate();if(Original.Id!=Entry.Id||Original.Profile!=Entry.Profile)throw new FormatException("Draft identity changed");}
    }
    public void Save(string path){Validate();File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this,Json.Options));File.Move(path+".tmp",path,true);}
    public static TradeDraft? Load(string path){if(!File.Exists(path))return null;if(new FileInfo(path).Length>100000)throw new FormatException("Draft is too large");var d=JsonSerializer.Deserialize<TradeDraft>(File.ReadAllText(path),Json.Options)??throw new FormatException("Draft is empty");d.Validate();return d;}
    public bool AlreadyRecorded(IEnumerable<TradeEntry> trades)=>trades.Any(t=>t==Entry);
    public bool CanResume(IEnumerable<TradeEntry> trades)=>Original is null?!trades.Any(t=>t.Id==Entry.Id):trades.Any(t=>t==Original);
}
public sealed record ScreenshotSuggestion(string Name,long? Quantity,string Variant);
public sealed record Recap(decimal Purchases,decimal Sales,decimal Fees,decimal NetCashFlow,decimal MatchedProfit,long Unmatched,int TradeCount);
public static class CompanionUpgrade
{
    public static decimal NetProfit(decimal buy,decimal sell,long quantity,decimal buyFees,decimal saleFees)
    {if(buy<0||sell<0||quantity<=0||buyFees<0||saleFees<0)throw new ArgumentException("Enter nonnegative prices and fees, and a positive quantity");return (sell-buy)*quantity-buyFees-saleFees;}
    public static Recap SessionRecap(MerchantSession s,IEnumerable<TradeEntry> trades,DateTimeOffset now)
    {var t=MerchantTools.Totals(s,trades,now);return new(t.Purchases,t.Sales,t.Fees,t.Sales-t.Purchases-t.Fees,t.Profit,t.Unmatched,t.Trades);}
    // OCR is only a suggestion: names must match the catalog, quantities stay unknown unless labeled.
    public static ScreenshotSuggestion Suggest(string text,IEnumerable<string> catalog)
    {
        var lines=text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).ToList();
        var names=catalog.Distinct().Where(n=>lines.Any(l=>TextKey.Search(l)==TextKey.Search(n))).ToList();
        var q=Regex.Matches(text,@"(?im)^\s*(?:quantity|qty|count)\s*[:x]?\s*([\d,]+)\s*$");long? quantity=null;
        if(q.Count==1&&long.TryParse(q[0].Groups[1].Value.Replace(",",""),out var v)&&v is >0 and <=1_000_000_000)quantity=v;
        var stats=new Dictionary<string,string>();var aliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"weapon attack","watk"},{"magic attack","matk"},{"str","str"},{"dex","dex"},{"int","int"},{"luk","luk"},{"weapon defense","wdef"},{"magic defense","mdef"},{"accuracy","acc"},{"avoidability","avoid"},{"speed","speed"},{"jump","jump"},{"remaining upgrades","remaining"}};
        foreach(var l in lines){var m=Regex.Match(l,@"^([a-z ]+)\s*:\s*\+?(\d+)\s*$",RegexOptions.IgnoreCase);if(m.Success&&aliases.TryGetValue(m.Groups[1].Value.Trim(),out var key))stats[key]=m.Groups[2].Value;}
        return new(names.Count==1?names[0]:"",quantity,string.Join("; ",stats.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value)));
    }
}
