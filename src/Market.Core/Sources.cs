using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Market.Core;

public sealed record SourceResult(string Provider, List<Observation> Rows, string Status, DateTimeOffset CheckedAt, bool Updated=false) { public List<string> SnapshotItems {get;init;}=[]; public List<FmSummary>? FmSummaries {get;init;} }
public sealed class RateLimitException(TimeSpan retry) : Exception("Provider rate limit; waiting before retry") { public TimeSpan Retry { get; } = retry; }
public sealed class Fetcher : IDisposable
{
    readonly HttpClient client;
    public Fetcher(HttpMessageHandler? handler = null)
    {
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All });
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MapleClassicMarketCompanion/0.3 (personal public-market reader)");
    }
    public async Task<string> Get(Uri uri, CancellationToken ct)
    {
        if (uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidOperationException("Only HTTPS URLs without embedded credentials are supported.");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(30));ct=deadline.Token;
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var retry = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMinutes(30);
            throw new RateLimitException(retry < TimeSpan.FromMinutes(15) ? TimeSpan.FromMinutes(15) : retry);
        }
        if ((int)response.StatusCode is >= 300 and < 400) throw new HttpRequestException("Source redirected; review its approved URL before continuing.");
        response.EnsureSuccessStatusCode();
        const int maximum = 2 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > maximum) throw new FormatException("Response exceeds 2 MB limit.");
        using var stream = await response.Content.ReadAsStreamAsync(ct); using var output = new MemoryStream(); var buffer = new byte[8192];
        int n; while ((n = await stream.ReadAsync(buffer, ct)) > 0) { if (output.Length + n > maximum) throw new FormatException("Response exceeds 2 MB limit."); await output.WriteAsync(buffer.AsMemory(0,n), ct); }
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }
    public void Dispose() => client.Dispose();
}

public static class PublicPages
{
    public static HtmlDocument Document(string html) { var d=new HtmlDocument(); d.LoadHtml(html); foreach(var n in d.DocumentNode.SelectNodes("//script|//style|//noscript")?.ToArray() ?? []) n.Remove(); return d; }
    public static string Text(HtmlNode? n) => Regex.Replace(HtmlEntity.DeEntitize(n is null ? "" : string.Join(" ",n.DescendantsAndSelf().Where(x=>x.NodeType==HtmlNodeType.Text).Select(x=>x.InnerText))), @"\s+", " ").Trim();
    static HtmlNode? Class(HtmlNode n, string cls) => n.SelectSingleNode($".//*[contains(concat(' ',normalize-space(@class),' '),' {cls} ')]");
    public static List<string> ListingLinks(string html, string provider)
    {
        var path = provider == "BasilMarket" ? "/market/listing/" : "/listing/";
        var doc=Document(html);
        return (doc.DocumentNode.SelectNodes("//a[@href]")?.ToArray() ?? []).Select(n => n.GetAttributeValue("href", ""))
            .Where(h => Regex.IsMatch(h, "^" + Regex.Escape(path) + @"[a-zA-Z0-9-]+$")) .Distinct().Take(8).ToList();
    }
    public static Observation Parse(string html, string provider, string url, DateTimeOffset retrieved)
    {
        var doc=Document(html); var root=doc.DocumentNode; var heading=root.SelectSingleNode("//h1") ?? throw new FormatException("Listing heading missing");
        string name=Text(heading), seller="", age="Unknown", variant="", priceText=""; long? qty=null; bool equip=true, available=false;
        if (provider == "Kerning Exchange")
        {
            var hero = Class(root,"hero-info") ?? throw new FormatException("Listing layout changed");
            var priceNode=Class(hero,"hero-price"); priceText=Text(priceNode).Replace(" each","");
            var subtitle=Text(Class(hero,"hero-sub")); var q=Regex.Match(subtitle,@"×\s*(\d[\d,]*)"); if(q.Success) qty=long.Parse(q.Groups[1].Value.Replace(",",""));
            seller=Text(Class(hero,"seller-link")); age=Regex.Match(subtitle,@"listed\s+(.+?ago)").Groups[1].Value;
            var tooltip=Class(hero,"hero-tip-top"); variant=Text(tooltip); equip=Regex.IsMatch(variant,@"(?:WEAPON ATT|MAGIC ATT|WEAPON DEF|MAGIC DEF|REMAINING ENHANCEMENTS|REQ LEV)");
            available=Text(Class(hero,"status")) == "Selling";
            // Category details are preserved, but completeness of upgrade attributes isn't asserted.
            if (!equip) variant="";
        }
        else if(provider == "BasilMarket")
        {
            var label=root.SelectNodes("//div")?.FirstOrDefault(n => n.ChildNodes.All(c => c.NodeType == HtmlNodeType.Text) && Text(n) == "Price") ?? throw new FormatException("Price block missing");
            var next=label.NextSibling; while(next is not null && next.NodeType != HtmlNodeType.Element) next=next.NextSibling;
            priceText=Text(next);
            var header=heading.ParentNode?.ParentNode ?? throw new FormatException("Header layout changed"); var headerText=Text(header); var q=Regex.Match(headerText,@"×\s*(\d[\d,]*)"); if(q.Success) qty=long.Parse(q.Groups[1].Value.Replace(",",""));
            var posted=root.SelectNodes("//span")?.FirstOrDefault(n => Text(n).StartsWith("Posted by "));
            seller=Text(posted?.SelectSingleNode(".//a")); age=Regex.Match(Text(posted),@"(\d+\s*[mhd]\s+ago)").Value;
            var all=Text(root); equip=!Regex.IsMatch(headerText,@"\b(?:etc|use)\s*·",RegexOptions.IgnoreCase);
            available=Regex.IsMatch(headerText,@"\bsell\b") && !Regex.IsMatch(headerText,@"\bsold\b",RegexOptions.IgnoreCase);
            if(equip) { var start=all.IndexOf("Slots",StringComparison.Ordinal); variant=start >= 0 ? all.Substring(Math.Max(0,start-100),Math.Min(120,all.Length-Math.Max(0,start-100))) : "Equipment attributes incomplete"; }
        }
        else throw new FormatException("Unknown page provider");
        var raw=TextKey.ParsePrice(priceText);
        var each=provider == "Kerning Exchange" || qty is null ? raw : TextKey.PerUnit(raw,qty.Value);
        var o=new Observation { Provider=provider,ListingId=new Uri(url).Segments.Last(),ItemId="name:"+TextKey.Normalize(name),Name=name,Variant=variant,Equipment=equip,VariantComplete=false,
            UnitPrice=each,Quantity=qty,Kind=EvidenceKind.Asking,ObservedAt=null,RetrievedAt=retrieved,SourceAge=string.IsNullOrWhiteSpace(age)?"Unknown":age,Seller=seller,Url=url,
            Available=available,PriceApproximate=Regex.IsMatch(priceText,"[kKmM]"),Verification="Public-page ask; exact source time missing; availability unverified in game" };
        o.Validate(); return o;
    }
}

public sealed class FeedEnvelope
{
    public int SchemaVersion {get;set;}
    public string Game {get;set;}="";
    public List<Observation> Observations {get;set;}=[];
}
public static class ApprovedFeed
{
    public static List<Observation> Parse(string data, DateTimeOffset retrieved)
    {
        var envelope=JsonSerializer.Deserialize<FeedEnvelope>(data,Json.Options) ?? throw new FormatException("Empty feed");
        if(envelope.SchemaVersion!=1 || envelope.Game != "MapleStory Classic World") throw new FormatException("Feed must identify schema 1 and MapleStory Classic World");
        if(envelope.Observations is null || envelope.Observations.Count>10000) throw new FormatException("Invalid feed size");
        var rows=new List<Observation>();
        foreach(var incoming in envelope.Observations)
        {
            var o=incoming with {Provider="Approved JSON feed",RetrievedAt=retrieved}; o.Validate();
            if(o.ObservedAt is null || o.ObservedAt>retrieved.AddMinutes(2)) throw new FormatException("Feed needs valid original observation timestamps");
            rows.Add(o);
        }
        return rows;
    }
}

public sealed class SourceCollector(Fetcher fetcher)
{
    readonly SemaphoreSlim mutex=new(1,1);
    readonly KerningReader kerning=new(fetcher);
    readonly BasilReader basil=new(fetcher);
    public IReadOnlyList<CatalogItem> Catalog=>kerning.Catalog;
    public async Task<List<SourceResult>> Refresh(Settings settings,CancellationToken ct,IEnumerable<string>? names=null,bool search=false,IEnumerable<Observation>? cached=null,Func<SourceResult,Task>? publish=null,Action<string>? checking=null,string? onlyProvider=null)
    {
        if(!await mutex.WaitAsync(0,ct)) return [];
        try
        {
            var results=new List<SourceResult>(); async Task Add(SourceResult result){results.Add(result);if(publish is not null)await publish(result);}
            foreach(var config in settings.Sources.OrderBy(s=>s.Id=="Kerning Exchange"?0:1))
            {
                if(onlyProvider is not null&&config.Id!=onlyProvider)continue;
                if(search && config.Id is not ("Kerning Exchange" or "BasilMarket"))continue;
                ct.ThrowIfCancellationRequested();checking?.Invoke(config.Id);var now=DateTimeOffset.UtcNow;
                if(!config.Authorized) { await Add(new(config.Id,[],"Permission required — disabled",config.LastChecked)); continue; }
                if(now<config.BlockedUntil){await Add(new(config.Id,[],"Provider backoff until "+config.BlockedUntil.ToLocalTime().ToString("g"),config.LastChecked));continue;}
                if(now<config.NextAllowed && (!search || config.Id=="BasilMarket" || config.NextAllowed>config.LastChecked.AddMinutes(Math.Max(15,config.IntervalMinutes)))) { await Add(new(config.Id,[],"Cached; next fetch " + config.NextAllowed.ToLocalTime().ToString("g"),config.LastChecked)); continue; }
                config.LastChecked=now;
                config.NextAllowed=now.AddMinutes(Math.Max(15,config.IntervalMinutes));
                try
                {
                    if(config.Id=="Kerning Exchange") { await Add(await kerning.Refresh(config,names??[],search,ct,publish));continue; }
                    if(config.Id=="BasilMarket") { await Add(await basil.Refresh(config,ct,names,cached));continue; }
                    List<Observation> rows=[];int rejected=0;
                    if(config.Id=="Approved JSON feed") rows=ApprovedFeed.Parse(await fetcher.Get(new Uri(config.FeedUrl),ct),now);
                    else throw new FormatException("Unsupported provider");
                    await Add(new(config.Id,rows,(rows.Count==0?"No observations returned":$"{rows.Count} observations; " + (config.Id=="Approved JSON feed"?"provider feed":"latest-page coverage only; source time unknown"))+(rejected>0?$"; {rejected} unsupported listing pages excluded":""),now,true));
                }
                catch(OperationCanceledException) when(ct.IsCancellationRequested) {config.NextAllowed=default;throw; }
                catch(RateLimitException e) { config.NextAllowed=DateTimeOffset.UtcNow+e.Retry;config.BlockedUntil=config.NextAllowed; await Add(new(config.Id,[],"Rate limited; next fetch " + config.NextAllowed.ToLocalTime().ToString("g"),now)); }
                catch(Exception e)
                { config.NextAllowed=DateTimeOffset.UtcNow.AddMinutes(Math.Max(30,config.IntervalMinutes));config.BlockedUntil=config.NextAllowed; await Add(new(config.Id,[],"Unavailable: " + e.GetType().Name + "; retry after " + config.NextAllowed.ToLocalTime().ToString("g"),now)); }
            }
            return results;
        }
        finally {mutex.Release();}
    }
}
