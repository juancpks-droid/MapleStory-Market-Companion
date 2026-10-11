using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Market.Core;

public static class BasilPages
{
    public static List<Observation> DetailQueue(IEnumerable<Observation> rows,SourceConfig config,DateTimeOffset now,int batchRequests)
    {
        int remaining=Math.Max(0,Math.Min(12,Math.Min(15-batchRequests,60-config.RequestTimes.Count(t=>t>=now.AddHours(-1)))));
        return rows.Where(r=>r.Provider=="BasilMarket"&&r.PriceApproximate&&r.Available&&r.Kind==EvidenceKind.Asking&&Uri.TryCreate(r.Url,UriKind.Absolute,out var u)&&u.Host=="basilmarket.net"&&u.Scheme=="https"&&u.AbsolutePath=="/market/listing/"+r.ListingId&&Regex.IsMatch(r.ListingId,@"^\d+$"))
            .DistinctBy(r=>r.ListingId).Where(r=>BasilProgress.NeedsCheck(r,config,now)).Where(r=>!config.PageChecked.TryGetValue(new Uri(r.Url).AbsolutePath,out var at)||now-at>=TimeSpan.FromMinutes(Math.Max(15,config.IntervalMinutes)))
            .OrderBy(r=>config.PageChecked.GetValueOrDefault(new Uri(r.Url).AbsolutePath)).ThenByDescending(r=>r.Quantity).Take(remaining).ToList();
    }
    public static Observation Detail(string html,Observation listing,DateTimeOffset retrieved)
    {
        var root=PublicPages.Document(html).DocumentNode;var heading=root.SelectSingleNode("//h1")??throw new FormatException("Basil listing heading missing");
        if(TextKey.Normalize(PublicPages.Text(heading))!=TextKey.Normalize(listing.Name))throw new FormatException("Basil listing identity changed");
        var header=PublicPages.Text(heading.ParentNode);if(!Regex.IsMatch(header,@"\bsell\b")||Regex.IsMatch(header,@"\bsold\b",RegexOptions.IgnoreCase))throw new FormatException("Basil listing is not an active sale");
        var quantity=Regex.Match(header,@"×\s*([\d,]+)");long qty=quantity.Success?long.Parse(quantity.Groups[1].Value.Replace(",","")):1;
        var label=root.SelectNodes("//div")?.SingleOrDefault(n=>n.ChildNodes.All(c=>c.NodeType==HtmlAgilityPack.HtmlNodeType.Text)&&PublicPages.Text(n)=="Price")??throw new FormatException("Basil listing price block missing");
        var values=label.ParentNode.ChildNodes.Where(n=>n.NodeType==HtmlAgilityPack.HtmlNodeType.Element).Skip(1).Select(PublicPages.Text).ToList();
        var each=values.FirstOrDefault(v=>Regex.IsMatch(v,@"^\d+(?:,\d{3})* each$"));
        var raw=each is not null?each[..^5]:qty==1?values.FirstOrDefault():null;
        if(raw is null||!Regex.IsMatch(raw,@"^\d+(?:,\d{3})*$"))return listing; // Never expand k/m displays into a claimed exact value.
        var price=decimal.Parse(raw,NumberStyles.AllowThousands,CultureInfo.InvariantCulture);if(price<=0)throw new FormatException("Invalid exact price");
        var result=listing with{UnitPrice=price,Quantity=qty,PriceApproximate=false,RetrievedAt=retrieved,Verification="Exact numeric asking price from public listing detail; original posting timestamp still unknown; not a verified sale"};
        result.Validate();return result;
    }
    public static bool CategoryPath(string path)=>Regex.IsMatch(path,@"^/market/sell\?type=[a-z0-9-]{1,50}$");
    public static List<string> Categories(string html)=>PublicPages.Document(html).DocumentNode.SelectNodes("//a[@href]")?.Select(n=>n.GetAttributeValue("href","")).Where(CategoryPath).Distinct().ToList()??[];
    public static List<Observation> Browse(string html,DateTimeOffset retrieved)
    {
        var root=PublicPages.Document(html).DocumentNode;var rows=new List<Observation>();
        foreach(var a in root.SelectNodes("//a[@href]")?.Where(a=>Regex.IsMatch(a.GetAttributeValue("href",""),@"^/market/listing/\d+$"))??[])
        {
            var nameNode=a.SelectSingleNode(".//span[contains(@class,'font-semibold')]");
            var priceNode=a.SelectSingleNode(".//span[contains(@class,'text-right')]/span[contains(@class,'font-bold')]");
            var typeNode=a.SelectSingleNode(".//span[contains(@class,'uppercase')]");
            if(nameNode is null||priceNode is null||PublicPages.Text(typeNode)!="sell")throw new FormatException("Basil category row layout changed");
            var name=string.Concat(nameNode.ChildNodes.Where(n=>n.NodeType==HtmlAgilityPack.HtmlNodeType.Text).Select(n=>HtmlAgilityPack.HtmlEntity.DeEntitize(n.InnerText))).Trim();
            var qtyMatch=Regex.Match(PublicPages.Text(nameNode),@"×\s*([\d,]+)");long qty=qtyMatch.Success?long.Parse(qtyMatch.Groups[1].Value.Replace(",","")):1;
            var icon=(a.SelectSingleNode(".//img")??throw new FormatException("Basil item icon missing")).GetAttributeValue("src","");var item=Regex.Match(icon,@"^/images/items/(\d{7,8})\.png$");if(!item.Success||qty<1)throw new FormatException("Basil category identity changed");
            var price=PublicPages.Text(priceNode);var id=a.GetAttributeValue("href","").Split('/').Last();var itemId=long.Parse(item.Groups[1].Value).ToString(CultureInfo.InvariantCulture);bool equipment=itemId.StartsWith('1');
            var row=new Observation{Provider="BasilMarket",ListingId=id,ItemId=itemId,Name=name,Quantity=qty,UnitPrice=TextKey.PerUnit(TextKey.ParsePrice(price),qty),Kind=EvidenceKind.Asking,
                Equipment=equipment,VariantComplete=!equipment,Variant=equipment?"Equipment attributes incomplete (BasilMarket category)":"",Available=true,ObservedAt=null,RetrievedAt=retrieved,
                SourceAge=Regex.Match(PublicPages.Text(a),@"\d+\s*[mhd]\s+ago").Value,PriceApproximate=true,Url="https://basilmarket.net/market/listing/"+id,
                Verification="Public category/search listing; displayed total divided by quantity; rounded price and relative age only; excluded from estimates"};
            row.Validate();rows.Add(row);
        }
        if(rows.Count==0&&!Regex.IsMatch(PublicPages.Text(root),@"\b0 listings\b|No listings",RegexOptions.IgnoreCase))throw new FormatException("Basil browse layout unrecognized");
        return rows.DistinctBy(r=>r.ListingId).ToList();
    }
    public const string AccessReference="https://basilmarket.net/terms — public listings reviewed 2026-10-10; robots checked each batch";
    public static List<Observation> Parse(string html,DateTimeOffset retrieved)
    {
        // Decode data literals only. Never execute downloaded scripts.
        var flight=string.Concat(Regex.Matches(html,"self\\.__next_f\\.push\\(\\[1,(\"(?:\\\\.|[^\"\\\\])*\")\\]\\)").Select(m=>JsonSerializer.Deserialize<string>(m.Groups[1].Value)));
        var rows=new Dictionary<string,Observation>();bool recognized=false;
        foreach(var line in flight.Split('\n'))
        {
            int colon=line.IndexOf(':');if(colon<0)continue;var body=line[(colon+1)..];if(!body.StartsWith('[')&&!body.StartsWith('{'))continue;
            JsonDocument doc;try{doc=JsonDocument.Parse(body);}catch(JsonException){continue;}
            using(doc)foreach(var o in FmTracker.Objects(doc.RootElement))
            {
                if(!o.TryGetProperty("createdAt",out var time)||!o.TryGetProperty("kind",out var kind)||!o.TryGetProperty("item",out var item)||!o.TryGetProperty("status",out var status))continue;
                recognized=true;
                if(kind.GetString() is not ("sell" or "buy"))continue; // Auction bids and bundles aren't ordinary asks.
                var price=o.GetProperty("price");if(price.ValueKind!=JsonValueKind.Number||!price.TryGetDecimal(out var unit)||unit<=0)continue;
                var stamp=Regex.Replace(time.GetString()??"",@"\s*\([^)]*\)$","");
                stamp=Regex.Replace(stamp,@"GMT([+-]\d{2})(\d{2})","$1:$2");
                if(!DateTimeOffset.TryParseExact(stamp,["ddd MMM dd yyyy HH:mm:ss zzz","ddd MMM d yyyy HH:mm:ss zzz"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)||at>retrieved.AddMinutes(2))throw new FormatException("Invalid Basil source timestamp");
                var id=o.GetProperty("id").GetInt64();if(id<=0)throw new FormatException("Invalid Basil listing identity");
                var icon=item.GetProperty("icon").GetString()??"";var match=Regex.Match(icon,@"^/images/items/(\d{7,8})\.png$");
                if(!match.Success)throw new FormatException("Unknown Basil item identity");
                string itemId=long.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                bool equipment=itemId.StartsWith('1'); // Including cash equips: do not pool unknown variants.
                var stats=o.GetProperty("stats");var slots=o.GetProperty("slotsLeft");
                string variant=equipment?string.Join("; ",stats.ValueKind==JsonValueKind.Object?stats.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>p.Name+"="+p.Value.ToString()):[]):"";
                if(equipment)variant+=(variant.Length>0?"; ":"")+"remaining="+(slots.ValueKind==JsonValueKind.Number?slots.ToString():"unknown")+"; upgrade history unknown (BasilMarket)";
                var row=new Observation{Provider="BasilMarket",ListingId=id.ToString(CultureInfo.InvariantCulture),ItemId=itemId,Name=item.GetProperty("name").GetString()??"",Equipment=equipment,Variant=variant,VariantComplete=!equipment,
                    UnitPrice=unit,Quantity=o.GetProperty("quantity").GetInt64(),Kind=kind.GetString()=="buy"?EvidenceKind.Buying:EvidenceKind.Asking,
                    ObservedAt=at,RetrievedAt=retrieved,SourceAge="Original posting time",Seller=o.GetProperty("sellerName").GetString()??"",Url="https://basilmarket.net/market/listing/"+id,
                    Available=status.GetString()=="active",Verification="Public Classic World listing; price per item; recent-page coverage only; availability not verified in game"};
                row.Validate();rows[row.ListingId]=row;
            }
        }
        if(!recognized)throw new FormatException("Basil listing data missing or layout changed");
        return rows.Values.ToList();
    }
}

public sealed class BasilReader(Fetcher fetcher)
{
    async Task<string> Get(string path,SourceConfig config,CancellationToken ct)
    {
        var now=DateTimeOffset.UtcNow;config.RequestTimes.RemoveAll(t=>t<now.AddHours(-1));
        if(config.RequestTimes.Count>=60)throw new RateLimitException(TimeSpan.FromHours(1));
        if(config.RequestTimes.Count>0){var wait=TimeSpan.FromSeconds(3)-(now-config.RequestTimes.Max());if(wait>TimeSpan.Zero)await Task.Delay(wait,ct);}
        config.RequestTimes.Add(DateTimeOffset.UtcNow);return await fetcher.Get(new Uri("https://basilmarket.net"+path),ct);
    }
    public async Task<SourceResult> Refresh(SourceConfig config,CancellationToken ct,IEnumerable<string>? names=null,IEnumerable<Observation>? cached=null)
    {
        var batchStart=DateTimeOffset.UtcNow;
        string? robots=null;
        try{robots=await Get("/robots.txt",config,ct);}
        catch(HttpRequestException e) when(e.StatusCode==HttpStatusCode.NotFound){ } // Absent file, not a grant of access to private endpoints.
        if(robots is not null&&!KerningPages.RobotsAllows(robots,"/"))throw new InvalidOperationException("Provider crawl policy blocks public listings");
        var rows=BasilPages.Parse(await Get("/",config,ct),DateTimeOffset.UtcNow).ToDictionary(r=>r.ListingId);
        bool Allowed(string path)=>robots is null||KerningPages.RobotsAllows(robots,path);
        int pages=0,failed=0,exact=0,details=0;string extra="";
        try
        {
            if(Allowed("/market/sell"))
            {
                var categories=BasilPages.Categories(await Get("/market/sell",config,ct));
                if(categories.Count>0){lock(config)config.BasilPages=categories;}
                var paths=(names??[]).Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct().Take(2).Select(n=>"/market/sell?q="+Uri.EscapeDataString(n.Trim())).Concat(categories.OrderBy(p=>config.BasilVisited.GetValueOrDefault(p)).ThenBy(p=>config.PageChecked.GetValueOrDefault(p)).ThenBy(p=>p.EndsWith("=etc")||p.EndsWith("=scroll")?0:1).Take(2));
                foreach(var path in paths.Distinct())
                {
                    if(!Allowed(path)){failed++;continue;}
                    if(config.PageChecked.TryGetValue(path,out var checkedAt)&&DateTimeOffset.UtcNow-checkedAt<TimeSpan.FromMinutes(Math.Max(15,config.IntervalMinutes)))continue;
                    try{foreach(var row in BasilPages.Browse(await Get(path,config,ct),DateTimeOffset.UtcNow))rows.TryAdd(row.ListingId,row);pages++;lock(config)config.BasilVisited[path]=DateTimeOffset.UtcNow;}
                    catch(Exception e) when(e is FormatException or HttpRequestException){failed++;}
                    config.PageChecked[path]=DateTimeOffset.UtcNow;
                }
                extra=$"; {pages} browse pages checked across {categories.Count} categories (rotating)";
            }
            var candidates=rows.Values.Concat((cached??[]).Where(r=>!rows.ContainsKey(r.ListingId)));
            foreach(var row in BasilPages.DetailQueue(candidates,config,DateTimeOffset.UtcNow,config.RequestTimes.Count(t=>t>=batchStart)))
            {
                var path=new Uri(row.Url).AbsolutePath;if(!Allowed(path)){failed++;continue;}
                if(config.PageChecked.TryGetValue(path,out var at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromMinutes(Math.Max(15,config.IntervalMinutes)))continue;
                try{details++;var detail=BasilPages.Detail(await Get(path,config,ct),row,DateTimeOffset.UtcNow);BasilProgress.Complete(row,config,detail.PriceApproximate?"Rounded":"Exact",DateTimeOffset.UtcNow);if(!detail.PriceApproximate){rows[row.ListingId]=detail;exact++;}}
                catch(Exception e) when(e is FormatException or HttpRequestException){failed++;BasilProgress.Complete(row,config,"Retry",DateTimeOffset.UtcNow);}
                config.PageChecked[path]=DateTimeOffset.UtcNow;
            }
        }
        catch(RateLimitException e){config.BlockedUntil=DateTimeOffset.UtcNow+e.Retry;config.NextAllowed=config.BlockedUntil;extra="; additional pages rate limited; waiting before retry";}
        catch(Exception e) when(e is HttpRequestException or FormatException){failed++;extra="; category index unavailable";}
        // Partial coverage: missing rows never imply sold or removed listings.
        return new("BasilMarket",rows.Values.ToList(),$"{rows.Count} listing records returned{extra}; {details} detail pages checked, {exact} exact prices; {failed} pages unavailable; unknown posting times excluded from estimates",DateTimeOffset.UtcNow,true);
    }
}
