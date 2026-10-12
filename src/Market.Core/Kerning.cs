using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace Market.Core;

public sealed record CatalogItem(string Id, string Name, string Url, DateTimeOffset Changed);
public sealed record ItemSnapshot(string ItemId, string Name, List<Observation> Rows);

public static class KerningPages
{
    public static List<CatalogItem> Catalog(string xml)
    {
        var doc=XDocument.Parse(xml); XNamespace ns="http://www.sitemaps.org/schemas/sitemap/0.9";
        return doc.Descendants(ns+"url").Select(n=>new {Url=(string?)n.Element(ns+"loc")??"",Changed=(string?)n.Element(ns+"lastmod")})
            .Where(n=>Regex.IsMatch(n.Url,@"^https://kerning\.gg/item/\d+-[a-z0-9-]+$"))
            .Select(n=>{var slug=n.Url.Split('/').Last();return new CatalogItem(slug.Split('-')[0],slug[(slug.IndexOf('-')+1)..].Replace('-',' '),n.Url,DateTimeOffset.TryParse(n.Changed,out var t)?t:DateTimeOffset.MinValue);}).ToList();
    }
    static IEnumerable<JsonElement> Walk(JsonElement node)
    {
        yield return node;
        if(node.ValueKind==JsonValueKind.Array) foreach(var c in node.EnumerateArray())foreach(var d in Walk(c))yield return d;
        if(node.ValueKind==JsonValueKind.Object) foreach(var c in node.EnumerateObject())foreach(var d in Walk(c.Value))yield return d;
    }
    public static ItemSnapshot Parse(string html,string url,DateTimeOffset retrieved)
    {
        // Decode only JSON string literals from the public server-rendered page. Never execute scripts.
        var chunks=Regex.Matches(html,"self\\.__next_f\\.push\\(\\[1,(\"(?:\\\\.|[^\"\\\\])*\")\\]\\)")
            .Select(m=>JsonSerializer.Deserialize<string>(m.Groups[1].Value)??"");
        var flight=string.Concat(chunks);
        foreach(var line in flight.Split('\n'))
        {
            int colon=line.IndexOf(':');if(colon<0)continue;var json=line[(colon+1)..];if(!json.StartsWith('[')&&!json.StartsWith('{'))continue;
            JsonDocument document;try{document=JsonDocument.Parse(json);}catch(JsonException){continue;}
            using(document)
            foreach(var node in Walk(document.RootElement))
            {
                if(node.ValueKind!=JsonValueKind.Object || !node.TryGetProperty("item",out var item) || !node.TryGetProperty("forSale",out var sales))continue;
                var id=item.GetProperty("id").ToString();var name=item.GetProperty("name").GetString()??throw new FormatException("Missing item name");
                if(!new Uri(url).AbsolutePath.StartsWith("/item/"+id+"-",StringComparison.Ordinal))throw new FormatException("Item identity mismatch");
                bool equipment=item.GetProperty("type").GetString()=="Equip";
                var rows=new List<Observation>();
                foreach(var group in new[]{"forSale","wanted","sold"})
                {
                    if(!node.TryGetProperty(group,out var list)||list.ValueKind!=JsonValueKind.Array)throw new FormatException("Incomplete item snapshot");
                    foreach(var row in list.EnumerateArray())
                    {
                        var listing=row.GetProperty("id").GetString()??"";if(!Guid.TryParse(listing,out _))throw new FormatException("Invalid listing ID");
                        var price=row.GetProperty("price").GetDecimal();if(price<=0)continue; // Offers without a price aren't market evidence.
                        int used=row.GetProperty("used").GetInt32(),passed=row.GetProperty("passed").GetInt32();
                        int slots=item.GetProperty("slots").GetInt32();var stats=row.GetProperty("stats");
                        string signature=equipment?string.Join("; ",stats.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal).Select(x=>x.Name+"="+x.Value.ToString()))+$"; remaining={slots-used}; used={used}; passed={passed}":"";
                        bool complete=!equipment || (stats.ValueKind==JsonValueKind.Object && stats.EnumerateObject().Any() && used>=0 && passed>=0 && passed<=used && used<=slots);
                        if(!DateTimeOffset.TryParse(row.GetProperty("at").GetString(),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var at)||at>retrieved.AddMinutes(2))throw new FormatException("Invalid source timestamp");
                        var o=new Observation{Provider="Kerning Exchange",ListingId=listing,ItemId=id,Name=name,Equipment=equipment,Variant=signature,VariantComplete=complete,UnitPrice=price,Quantity=row.GetProperty("quantity").GetInt64(),
                            Kind=group=="wanted"?EvidenceKind.Buying:group=="sold"?EvidenceKind.HistoricalAsk:EvidenceKind.Asking,ObservedAt=at,RetrievedAt=retrieved,Seller=row.GetProperty("ign").GetString()??"",Url="https://kerning.gg/listing/"+listing,
                            Available=group!="sold"&&!row.GetProperty("sold").GetBoolean(),Note=row.TryGetProperty("note",out var note)&&note.ValueKind==JsonValueKind.String?(note.GetString()??""):"",Verification=group=="sold"?"Seller marked sold; advertised price retained, actual transaction price/time unverified":"Public listing; source posting timestamp; availability observed on page, not verified in game"};
                        o.Validate();rows.Add(o);
                    }
                }
                return new(id,name,rows);
            }
        }
        throw new FormatException("Public item data missing or layout changed");
    }
    public static bool RobotsAllows(string text,string path)
    {
        bool applies=false,recognized=false;int best=-1;bool allow=true;
        foreach(var raw in text.Split('\n'))
        {
            var line=raw.Split('#')[0].Trim();int colon=line.IndexOf(':');if(colon<0)continue;
            var key=line[..colon].Trim().ToLowerInvariant();var value=line[(colon+1)..].Trim();
            if(key=="user-agent"){applies=value=="*"||value.Contains("MapleClassicMarketCompanion",StringComparison.OrdinalIgnoreCase);recognized|=applies;continue;}
            if(!applies||key is not ("allow" or "disallow")||value.Length==0)continue;
            var pattern="^"+Regex.Escape(value).Replace("\\*",".*").Replace("\\$","$");
            if(Regex.IsMatch(path,pattern)&&value.Length>=best){if(value.Length>best||key=="allow"){best=value.Length;allow=key=="allow";}}
        }
        return recognized&&allow;
    }
}

public sealed class KerningReader(Fetcher fetcher)
{
    List<CatalogItem> catalog=[];DateTimeOffset catalogAt;
    public IReadOnlyList<CatalogItem> Catalog=>catalog;
    static string SearchKey(string s)=>TextKey.Search(s);
    async Task<string> Get(string url,SourceConfig config,CancellationToken ct)
    {
        var now=DateTimeOffset.UtcNow;config.RequestTimes.RemoveAll(t=>t<now.AddHours(-1));
        if(config.RequestTimes.Count>=60)throw new RateLimitException(TimeSpan.FromHours(1));
        if(config.RequestTimes.Count>0){var wait=TimeSpan.FromSeconds(3)-(now-config.RequestTimes.Max());if(wait>TimeSpan.Zero)await Task.Delay(wait,ct);}
        config.RequestTimes.Add(DateTimeOffset.UtcNow);return await fetcher.Get(new Uri(url),ct);
    }
    public async Task<SourceResult> Refresh(SourceConfig config,IEnumerable<string> names,bool search,CancellationToken ct,Func<SourceResult,Task>? publish=null)
    {
        var now=DateTimeOffset.UtcNow;
        var robots=await Get("https://kerning.gg/robots.txt",config,ct);
        if(!KerningPages.RobotsAllows(robots,"/sitemap.xml")||!KerningPages.RobotsAllows(robots,"/item/"))throw new InvalidOperationException("Provider crawl policy blocks item access");
        if(catalog.Count==0||now-catalogAt>TimeSpan.FromDays(1)){catalog=KerningPages.Catalog(await Get("https://kerning.gg/sitemap.xml",config,ct));catalogAt=now;}
        var wanted=new List<CatalogItem>();
        foreach(var name in names.Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct())
        {
            var key=SearchKey(name);var exact=catalog.Where(i=>SearchKey(i.Name)==key).ToList();
            wanted.AddRange(exact.Count>0?exact:catalog.Where(i=>SearchKey(i.Name).Contains(key)).Take(5));
        }
        if(!search)wanted.AddRange(catalog.OrderByDescending(i=>i.Changed).Take(20));
        var candidates=wanted.DistinctBy(i=>i.Id).OrderBy(i=>config.PageChecked.GetValueOrDefault(i.Url)).ToList();
        var rows=new List<Observation>();var snapshots=new List<string>();int failed=0;
        foreach(var item in candidates.Where(i=>!config.PageChecked.TryGetValue(i.Url,out var t)||now-t>=TimeSpan.FromMinutes(Math.Max(15,config.IntervalMinutes))).Take(10))
        {
            if(!KerningPages.RobotsAllows(robots,new Uri(item.Url).AbsolutePath)){failed++;continue;}
            try
            {
                var page=await Get(item.Url,config,ct);var snapshot=KerningPages.Parse(page,item.Url,DateTimeOffset.UtcNow);
                rows.AddRange(snapshot.Rows);snapshots.Add(snapshot.ItemId);config.PageChecked[item.Url]=DateTimeOffset.UtcNow; if(publish is not null)await publish(new SourceResult("Kerning Exchange",snapshot.Rows,$"{snapshot.Name}: {snapshot.Rows.Count} listing records loaded",DateTimeOffset.UtcNow,true){SnapshotItems=[snapshot.ItemId]});
            }
            catch(HttpRequestException){failed++;config.PageChecked[item.Url]=DateTimeOffset.UtcNow;}
            catch(FormatException){failed++;config.PageChecked[item.Url]=DateTimeOffset.UtcNow;}
        }
        List<FmSummary>? summaries=null;string fmStatus="";
        const string fmUrl="https://kerning.gg/fm-tracker";
        if(!config.PageChecked.TryGetValue(fmUrl,out var fmChecked)||now-fmChecked>=TimeSpan.FromMinutes(Math.Max(15,config.IntervalMinutes)))
        {
            if(!KerningPages.RobotsAllows(robots,"/fm-tracker"))fmStatus="; FM tracker blocked by provider policy";
            else try{summaries=FmTracker.Parse(await Get(fmUrl,config,ct),DateTimeOffset.UtcNow);config.PageChecked[fmUrl]=DateTimeOffset.UtcNow;fmStatus=$"; {summaries.Count} FM item summaries updated";}
            catch(Exception e) when(e is HttpRequestException or FormatException or JsonException or KeyNotFoundException or InvalidOperationException){config.PageChecked[fmUrl]=DateTimeOffset.UtcNow;fmStatus="; FM tracker unavailable; cached summaries retained";}
        }
        string status=candidates.Count==0?"No matching item in the provider catalog":$"{snapshots.Count} item pages updated; {rows.Count} genuine listings; {catalog.Count} catalog items";
        if(snapshots.Count==0&&candidates.Count>0)status="Using cached item pages; external refresh interval applies";
        if(failed>0)status+=$"; {failed} pages unavailable";
        return new("Kerning Exchange",rows,status+fmStatus,now,snapshots.Count>0||summaries is not null){SnapshotItems=snapshots,FmSummaries=summaries};
    }
}
