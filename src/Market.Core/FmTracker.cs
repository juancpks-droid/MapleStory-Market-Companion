using System.Text.Json;
using System.Text.RegularExpressions;

namespace Market.Core;

// Aggregate provider statistics are deliberately separate from individual observations.
// They cannot be counted as independent sales, used as deal baselines, or mark stock available.
public sealed record FmSummary(string ItemId,string Name,bool Equipment,decimal? Median7,decimal? LowestAsk,
    decimal? ReportedSaleMedian,int Sightings,int ReportedSales,int Sellers,DateTimeOffset LastSeen,
    DateTimeOffset RetrievedAt,string Url)
{
    public string Quality(DateTimeOffset now,int hours)=>LastSeen>now.AddMinutes(2)?"Invalid source time":now-LastSeen>TimeSpan.FromHours(hours)?"Stale data":"Provider summary — unverified";
}
public static class FmTracker
{
    public static IEnumerable<JsonElement> Objects(JsonElement node)
    {
        if(node.ValueKind==JsonValueKind.Object){yield return node;foreach(var p in node.EnumerateObject())foreach(var c in Objects(p.Value))yield return c;}
        if(node.ValueKind==JsonValueKind.Array)foreach(var n in node.EnumerateArray())foreach(var c in Objects(n))yield return c;
    }
    public static List<FmSummary> Parse(string html,DateTimeOffset retrieved)
    {
        var flight=string.Concat(Regex.Matches(html,"self\\.__next_f\\.push\\(\\[1,(\"(?:\\\\.|[^\"\\\\])*\")\\]\\)").Select(m=>JsonSerializer.Deserialize<string>(m.Groups[1].Value)));
        foreach(var line in flight.Split('\n'))
        {
            int colon=line.IndexOf(':');if(colon<0)continue;var body=line[(colon+1)..];if(!body.StartsWith('[')&&!body.StartsWith('{'))continue;
            JsonDocument doc;try{doc=JsonDocument.Parse(body);}catch(JsonException){continue;}
            using(doc)foreach(var o in Objects(doc.RootElement))
            {
                if(!o.TryGetProperty("prices",out var prices)||prices.ValueKind!=JsonValueKind.Array)continue;
                var rows=new List<FmSummary>();
                foreach(var p in prices.EnumerateArray())
                {
                    var item=p.GetProperty("item");var id=p.GetProperty("itemId").ToString();
                    if(id!=item.GetProperty("id").ToString())throw new FormatException("FM item identity mismatch");
                    var name=item.GetProperty("name").GetString()??throw new FormatException("Missing FM name");
                    int seen=p.GetProperty("seen").GetInt32();if(seen==0)continue;
                    int sold=p.GetProperty("sales").GetInt32(),sellers=p.GetProperty("players").GetInt32();
                    if(seen<0||sold<0||sold>seen||sellers<0)throw new FormatException("Invalid FM counts");
                    if(!DateTimeOffset.TryParse(p.GetProperty("lastSeen").GetString(),out var at)||at>retrieved.AddMinutes(2))throw new FormatException("Invalid FM timestamp");
                    decimal? Price(string key){var value=p.GetProperty(key);if(value.ValueKind==JsonValueKind.Null)return null;var n=value.GetDecimal();if(n<=0)throw new FormatException("Invalid FM price");return n;}
                    var summary=new FmSummary(id,name,item.GetProperty("type").GetString()=="Equip",Price("median7"),Price("lowestAsk"),Price("typicalSale"),seen,sold,sellers,at,retrieved,"https://kerning.gg/fm-tracker");
                    rows.Add(summary);
                }
                if(rows.Select(r=>r.ItemId).Distinct().Count()!=rows.Count)throw new FormatException("Duplicate FM item");
                return rows;
            }
        }
        throw new FormatException("FM summary data missing or layout changed");
    }
}

public sealed record Website(string Name,string Url,string Connection,string Status);
public static class WebsiteDirectory
{
    public static readonly Website[] All=[
        new("Kerning Exchange","https://kerning.gg/","Automatic prices","Public item listings; enable in Settings"),
        new("Kerning FM Tracker","https://kerning.gg/fm-tracker","Automatic summaries","Provider aggregates; separate from deal evidence"),
        new("BasilMarket","https://basilmarket.net/","Automatic prices","Categories + search + exact listing details where available; rounded prices labeled"),
        new("NiaMeowDB","https://meowdb.com/msclassic/free-market","Open website","Automated collection requires provider permission"),
        new("MESOLANE","https://mesolane.gg/","Open website","No verified permitted automatic price feed"),
        new("Meso Scout","https://mesoscout.com/","Open website","Live listings announced for official release; no verified live feed"),
        new("Sleepywood","https://www.sleepywood.io/","Open website","No verified working market feed or live listings"),
        new("MapleBuddy","https://maplebuddy.co/","Open website","Item library and planner; live price feed unverified"),
        new("MapleClassicBase","https://mapleclassicbase.com/","Open website","Price edits stored in browser; no shared feed verified"),
        new("Metaroad","https://metaroad.gg/maplestory-classic/market-prices","Open website","Automated collection requires permission; no public feed verified"),
        new("GameMargins","https://gamemargins.com/maplestory-classic","Open website","Terms prohibit scraping and bulk price collection"),
        new("MesoWatch","https://meso.watch/","Open website","Automatic scraping and bulk copying prohibited")
    ];
}
