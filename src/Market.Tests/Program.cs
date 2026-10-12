using Market.Core;

using System.Net;

using System.Text.Json;

using System.Diagnostics;



var results=new List<object>();int failures=0;

void Check(bool success,string message="Assertion failed"){if(!success)throw new Exception(message);}

void Throws(Action a){bool threw=false;try{a();}catch{threw=true;}Check(threw,"Expected rejection");}

async Task Test(string name,Func<Task> action){var timer=Stopwatch.StartNew();try{await action();results.Add(new{name,status="passed",ms=timer.ElapsedMilliseconds});Console.WriteLine("PASS " +name);}catch(Exception e){failures++;results.Add(new{name,status="failed",error=e.Message});Console.WriteLine("FAIL "+name+": "+e.Message);}}

Task Sync(Action action){action();return Task.CompletedTask;}

var now=DateTimeOffset.UtcNow;

Observation O(string id,decimal price=1000,EvidenceKind kind=EvidenceKind.ConfirmedSale)=>new(){Provider="Test only",ListingId=id,ItemId="test-item",Name="Test ETC",UnitPrice=price,Quantity=1,Kind=kind,ObservedAt=now.AddHours(-1),RetrievedAt=now,Url="https://example.org/listing/"+id,Seller="seller"+id,Available=true,Verification="SYNTHETIC UNIT TEST ONLY"};

var sales=Enumerable.Range(1,8).Select(i=>O(i.ToString())).ToList();var s=new Settings(){MinimumProfit=100,Confidence=65};

await Test("Item normalization: punctuation, whitespace and case",()=>Sync(()=>Check(TextKey.Normalize(" Pig\u2019s   Head ")=="pig's head")));

await Test("Different item names remain different",()=>Sync(()=>Check(TextKey.Normalize("Garnet")!=TextKey.Normalize("Garnet Ore"))));

await Test("Price suffixes and thousands",()=>Sync(()=>Check(TextKey.ParsePrice("1.3k")==1300&&TextKey.ParsePrice("1,299")==1299&&TextKey.ParsePrice("2M")==2000000)));

await Test("Malformed and negative prices rejected",()=>Sync(()=>{Throws(()=>TextKey.ParsePrice("-5"));Throws(()=>TextKey.ParsePrice("free"));Throws(()=>TextKey.ParsePrice("0"));}));

await Test("Stack divided by quantity",()=>Sync(()=>{Check(TextKey.PerUnit(39000,30)==1300);Throws(()=>TextKey.PerUnit(100,0));}));

await Test("Median even and odd",()=>Sync(()=>Check(ValuationEngine.Median([10,20])==15&&ValuationEngine.Median([10,20,999])==20)));

await Test("Valuation supported by independent sales",()=>Sync(()=>{var v=ValuationEngine.Estimate(sales,sales[0],now,TimeSpan.FromDays(1),25);Check(v is {Value:1000,Quick:900,Normal:1000,High:1100,MaxBuy:750});}));

await Test("Single price cannot establish value",()=>Sync(()=>Check(ValuationEngine.Estimate(sales.Take(1),sales[0],now,TimeSpan.FromDays(1),25)==null)));

await Test("Extreme outlier excluded",()=>Sync(()=>{var v=ValuationEngine.Estimate(sales.Append(O("outlier",100000)),sales[0],now,TimeSpan.FromDays(1),25);Check(v?.Value==1000&&v.Count==8);}));

await Test("Three sellers required",()=>Sync(()=>Check(ValuationEngine.Estimate(sales.Select(o=>o with{Seller="same"}),sales[0],now,TimeSpan.FromDays(1),25)==null)));

await Test("Asks have explicit low confidence",()=>Sync(()=>{var asks=sales.Select(o=>o with{Kind=EvidenceKind.Asking}).ToList();var v=ValuationEngine.Estimate(asks,asks[0],now,TimeSpan.FromDays(1),25);Check(v!=null&&v.Confidence<=60&&v.Basis.Contains("Asking"));}));

await Test("Community estimates excluded from transaction baseline",()=>Sync(()=>Check(ValuationEngine.Estimate(sales.Select(o=>o with{Kind=EvidenceKind.CommunityEstimate}),sales[0],now,TimeSpan.FromDays(1),25)==null)));

await Test("Equipment variant signatures must match",()=>Sync(()=>{var gear=sales.Select(o=>o with{Equipment=true,VariantComplete=true,Variant="attack:43;slots:7;scrolls:0"}).ToList();var target=gear[0] with{Variant="attack:45;slots:6;scrolls:1"};Check(ValuationEngine.Estimate(gear,target,now,TimeSpan.FromDays(1),25)==null);}));

await Test("Incomplete equipment cannot be valued",()=>Sync(()=>Check(ValuationEngine.Estimate(sales,sales[0] with{Equipment=true},now,TimeSpan.FromDays(1),25)==null)));

await Test("Same listing deduplicated",()=>Sync(()=>Check(ValuationEngine.Deduplicate([sales[0],sales[0] with{RetrievedAt=now.AddMinutes(1)}]).Count==1)));

await Test("Cross-provider original reference deduplicated",()=>Sync(()=>Check(ValuationEngine.Deduplicate([sales[0] with{OriginalReference="origin-1"},sales[1] with{Provider="Other",OriginalReference="origin-1"}]).Count==1)));

await Test("Cross-provider seller item price deduplicated",()=>Sync(()=>Check(ValuationEngine.Deduplicate([sales[0],sales[0] with{Provider="Other"}]).Count==1)));

await Test("Old observations stale despite fresh retrieval",()=>Sync(()=>Check(!(sales[0] with{ObservedAt=now.AddDays(-2)}).Fresh(now,TimeSpan.FromDays(1)))));

await Test("Unknown source time is not retrieval time",()=>Sync(()=>Check(!(sales[0] with{ObservedAt=null}).Fresh(now,TimeSpan.FromDays(1)))));

await Test("Future observation rejected for valuation",()=>Sync(()=>Check(!(sales[0] with{ObservedAt=now.AddHours(1)}).Fresh(now,TimeSpan.FromDays(1)))));

await Test("Simulated bargain: profit, discount and ROI",()=>Sync(()=>{var deals=ValuationEngine.Find(sales.Append(O("bargain",500,EvidenceKind.Asking)),s,now);Check(deals.Count==1&&deals[0].Profit==500&&deals[0].Discount==50&&deals[0].Roi==100);}));

await Test("Thresholds reject small discount",()=>Sync(()=>Check(ValuationEngine.Find(sales.Append(O("not-bargain",900,EvidenceKind.Asking)),s,now).Count==0)));

await Test("Unavailable listing cannot alert",()=>Sync(()=>Check(ValuationEngine.Find(sales.Append(O("closed",500,EvidenceKind.Asking) with{Available=false}),s,now).Count==0)));

await Test("Candidate cannot establish its own baseline",()=>Sync(()=>Check(ValuationEngine.Find(Enumerable.Range(1,4).Select(i=>O(i.ToString(),1000,EvidenceKind.Asking)).Append(O("candidate",500,EvidenceKind.Asking)),new Settings(){Confidence=1,MinimumProfit=0},now).Count==0)));

await Test("Old availability check prevents alert",()=>Sync(()=>Check(ValuationEngine.Find(sales.Append(O("old",500,EvidenceKind.Asking) with{RetrievedAt=now.AddHours(-2)}),s,now).Count==0)));

await Test("SQLite reopen: settings items prices alerts",()=>Sync(()=>{var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");try{using(var db=new Store(path)){db.Save(sales);db.Save(sales);db.SaveItem(new("Test ETC",39,true,true));db.SaveSettings(s);db.MarkAlert("x");db.MarkAlert("x");}using(var db=new Store(path)){Check(db.Observations().Count==8&&db.History().Count==8&&db.Items().Single().Quantity==39&&db.WasAlerted("x")&&db.LoadSettings().MinimumProfit==100);db.ClearMarket();Check(db.Items().Count==1&&db.Observations().Count==0);}}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}}));

await Test("Feed validates Classic game identity",()=>Sync(()=>Throws(()=>ApprovedFeed.Parse("{\"schemaVersion\":1,\"game\":\"Modern MapleStory\",\"observations\":[]}",now))));

await Test("Feed preserves observed time and overrides retrieval",()=>Sync(()=>{var data=JsonSerializer.Serialize(new FeedEnvelope(){SchemaVersion=1,Game="MapleStory Classic World",Observations=sales},Json.Options);var parsed=ApprovedFeed.Parse(data,now);Check(parsed.Count==8&&parsed[0].ObservedAt==sales[0].ObservedAt&&parsed[0].Provider=="Approved JSON feed");}));

await Test("Feed rejects absent source timestamps",()=>Sync(()=>Throws(()=>ApprovedFeed.Parse(JsonSerializer.Serialize(new FeedEnvelope(){SchemaVersion=1,Game="MapleStory Classic World",Observations=[sales[0] with{ObservedAt=null}]},Json.Options),now))));

await Test("Feed rejects invalid quantity",()=>Sync(()=>Throws(()=>(sales[0] with{Quantity=0}).Validate())));

await Test("Unapproved sources make zero requests",async()=>{var h=new Handler(_=>new(HttpStatusCode.OK));using var f=new Fetcher(h);var c=new SourceCollector(f);await c.Refresh(new Settings(){Sources=[new(){Id="Kerning Exchange"}]},default);Check(h.Calls==0);});

await Test("Refresh throttling survives settings state",async()=>{var h=new Handler(_=>new(HttpStatusCode.OK){Content=new StringContent("{\"schemaVersion\":1,\"game\":\"MapleStory Classic World\",\"observations\":[]}")});using var f=new Fetcher(h);var c=new SourceCollector(f);var st=new Settings(){Sources=[new(){Id="Approved JSON feed",Enabled=true,PermissionReference="TEST ONLY",FeedUrl="https://example.org/feed"}]};await c.Refresh(st,default);await c.Refresh(st,default);Check(h.Calls==1&&st.Sources[0].NextAllowed>now);});

await Test("429 Retry-After honored",async()=>{var h=new Handler(_=>{var r=new HttpResponseMessage(HttpStatusCode.TooManyRequests);r.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(2));return r;});using var f=new Fetcher(h);var c=new SourceCollector(f);var st=new Settings(){Sources=[new(){Id="Approved JSON feed",Enabled=true,PermissionReference="TEST ONLY",FeedUrl="https://example.org/feed"}]};var r=await c.Refresh(st,default);Check(st.Sources[0].NextAllowed>now.AddMinutes(119)&&r[0].Status.Contains("Rate limited"));});

await Test("Provider failure isolated",async()=>{var h=new Handler(_=>throw new HttpRequestException("offline"));using var f=new Fetcher(h);var c=new SourceCollector(f);var st=new Settings(){Sources=[new(){Id="BasilMarket",Enabled=true,PermissionReference="TEST ONLY"},new(){Id="Approved JSON feed"}]};var r=await c.Refresh(st,default);Check(r.Count==2&&r[0].Status.StartsWith("Unavailable")&&r[1].Status.Contains("disabled"));});

await Test("Network reconnection after backoff",async()=>{int calls=0;var h=new Handler(_=>++calls==1?throw new HttpRequestException("offline"):new(HttpStatusCode.OK){Content=new StringContent("{\"schemaVersion\":1,\"game\":\"MapleStory Classic World\",\"observations\":[]}")});using var f=new Fetcher(h);var c=new SourceCollector(f);var st=new Settings(){Sources=[new(){Id="Approved JSON feed",Enabled=true,PermissionReference="TEST ONLY",FeedUrl="https://example.org/feed"}]};await c.Refresh(st,default);st.Sources[0].NextAllowed=DateTimeOffset.MinValue;st.Sources[0].BlockedUntil=DateTimeOffset.MinValue;var r=await c.Refresh(st,default);Check(r[0].Status=="No observations returned");});

await Test("Cancellation propagates without retry storm",async()=>{using var f=new Fetcher(new Handler(_=>new(HttpStatusCode.OK)));using var cts=new CancellationTokenSource();cts.Cancel();bool canceled=false;try{await new SourceCollector(f).Refresh(new Settings(),cts.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled);});

await Test("Redirect not silently followed",async()=>{using var f=new Fetcher(new Handler(_=>new(HttpStatusCode.Redirect)));bool rejected=false;try{await f.Get(new Uri("https://example.org/"),default);}catch(HttpRequestException){rejected=true;}Check(rejected);});

await Test("Oversized response rejected",async()=>{using var f=new Fetcher(new Handler(_=>new(HttpStatusCode.OK){Content=new StringContent(new string('x',2097153))}));bool rejected=false;try{await f.Get(new Uri("https://example.org/"),default);}catch(FormatException){rejected=true;}Check(rejected);});

await Test("Layout failure fails closed",()=>Sync(()=>Throws(()=>PublicPages.Parse("<h1>Test</h1>","BasilMarket","https://basilmarket.net/market/listing/test",now))));

await Test("Missing listing expires on full snapshot",()=>Sync(()=>{var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");try{using(var db=new Store(path)){var ask=O("removed",500,EvidenceKind.Asking);db.Save([ask]);db.ExpireMissingFromSnapshot("Test only",[]);Check(!db.Observations().Single().Available);Check(db.History().Single().Available);}}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}}));

await Test("Disabled source does not claim a network check",async()=>{using var f=new Fetcher(new Handler(_=>throw new Exception("Must not fetch")));var r=await new SourceCollector(f).Refresh(new Settings(){Sources=[new(){Id="Kerning Exchange"}]},default);Check(r.All(x=>x.CheckedAt==DateTimeOffset.MinValue));});

await Test("10,000 record analysis bounded",()=>Sync(()=>{var records=Enumerable.Range(1,10000).Select(i=>O(i.ToString(),1000+(i%7)*10,EvidenceKind.Asking)).ToList();var timer=Stopwatch.StartNew();ValuationEngine.Find(records,s,now);Check(timer.Elapsed<TimeSpan.FromSeconds(10),"Large feed analysis exceeded ten seconds");Console.WriteLine($"BENCHMARK synthetic 10,000-record analysis: {timer.ElapsedMilliseconds} ms");}));



var fixtureIndex=Array.IndexOf(args,"--fixtures");

if(fixtureIndex>=0)

{

    var folder=args[fixtureIndex+1];

    foreach(var test in new[]{("basil-garnet.html","BasilMarket","https://basilmarket.net/market/listing/303","Garnet",1300m,30L,false),("basil-dagger.html","BasilMarket","https://basilmarket.net/market/listing/291","Iron Dagger",60000m,0L,true),("kerning-cap.html","Kerning Exchange","https://kerning.gg/listing/8003de6d-f266-455c-ba7d-b73d2e0f27f1","Green Mushroom Cap",20m,100L,false),("kerning-cutlus.html","Kerning Exchange","https://kerning.gg/listing/474a8b71-d5aa-44e1-8e58-0ab6ec7090c1","Cutlus",30000m,0L,true)})

        await Test("Genuine captured page adapter: "+test.Item4,()=>Sync(()=>{var row=PublicPages.Parse(File.ReadAllText(Path.Combine(folder,test.Item1)),test.Item2,test.Item3,now);Check(row.Name==test.Item4&&row.UnitPrice==test.Item5&&row.Equipment==test.Item7,$"Parsed {row.Name}: {row.UnitPrice}; equipment={row.Equipment}; quantity={row.Quantity}");if(test.Item6>0)Check(row.Quantity==test.Item6);Check(row.ObservedAt==null&&row.Kind==EvidenceKind.Asking);Console.WriteLine(JsonSerializer.Serialize(row,Json.Options));}));

}



await Test("Robots allows public routes and excludes private paths",()=>Sync(()=>{string robots="User-Agent: *\nAllow: /\nDisallow: /sell\nDisallow: /auth/";Check(KerningPages.RobotsAllows(robots,"/item/4000013-green-mushroom-cap"));Check(!KerningPages.RobotsAllows(robots,"/sell"));Check(!KerningPages.RobotsAllows("User-agent: *\nDisallow: /","/item/1-x"));}));

await Test("Buying and historical advertised prices cannot establish fair value",()=>Sync(()=>{foreach(var kind in new[]{EvidenceKind.Buying,EvidenceKind.HistoricalAsk})Check(ValuationEngine.Estimate(sales.Select(o=>o with{Kind=kind}),sales[0],now,TimeSpan.FromDays(1),25)==null);}));

await Test("Item-scoped expiry preserves other items",()=>Sync(()=>{var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");try{using var db=new Store(path);db.Save([O("a",100,EvidenceKind.Asking) with{ItemId="A"},O("b",100,EvidenceKind.Asking) with{ItemId="B"}]);db.ExpireMissingFromSnapshot("Test only",[],["A"]);Check(!db.Observations().Single(o=>o.ItemId=="A").Available&&db.Observations().Single(o=>o.ItemId=="B").Available);}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}}));

if(fixtureIndex>=0){

var folder=args[fixtureIndex+1];

await Test("Genuine public item snapshot: exact price quantity and timestamp",()=>Sync(()=>{var snapshot=KerningPages.Parse(File.ReadAllText(Path.Combine(folder,"kerning-item.html")),"https://kerning.gg/item/4000013-green-mushroom-cap",now);var row=snapshot.Rows.Single(o=>o.ListingId=="8003de6d-f266-455c-ba7d-b73d2e0f27f1");Check(row.UnitPrice==20&&row.Quantity==100&&row.ObservedAt==DateTimeOffset.Parse("2026-10-10T06:41:39.144343+00:00")&&snapshot.Rows.Count==4);}));

await Test("Genuine equipment snapshot keeps attacks separate and sold ask unverified",()=>Sync(()=>{var snapshot=KerningPages.Parse(File.ReadAllText(Path.Combine(folder,"1302010-cutlus.html")),"https://kerning.gg/item/1302010-cutlus",now);Check(snapshot.Rows.All(o=>o.VariantComplete));Check(snapshot.Rows.Select(o=>o.MatchKey).Distinct().Count()==2);Check(snapshot.Rows.Any(o=>o.Kind==EvidenceKind.HistoricalAsk&&!o.Available));}));

await Test("Wrong catalog identity rejected",()=>Sync(()=>Throws(()=>KerningPages.Parse(File.ReadAllText(Path.Combine(folder,"kerning-item.html")),"https://kerning.gg/item/1-wrong",now))));

await Test("Catalog derives only public canonical item URLs",()=>Sync(()=>{var items=KerningPages.Catalog(File.ReadAllText(Path.Combine(folder,"recheck-kerning.gg-sitemap.xml")));Check(items.Count>100&&items.Any(i=>i.Id=="4000013"));}));

}


await Test("Search aliases handle punctuation without changing market identity",()=>Sync(()=>{Check(TextKey.Search("Pig's Head")==TextKey.Search("pigs head"));Check(TextKey.Search("Claw Attack Scroll: Intermediate")==TextKey.Search("claw-attack-scroll-intermediate"));Check(TextKey.Normalize("Garnet")!=TextKey.Normalize("Garnet Ore"));}));
await Test("Unexpected robots response fails closed",()=>Sync(()=>Check(!KerningPages.RobotsAllows("<html>Challenge</html>","/item/1-a"))));
await Test("Three independent asks give only a low-confidence estimate",()=>Sync(()=>{var asks=Enumerable.Range(1,3).Select(i=>O(i.ToString(),1000,EvidenceKind.Asking)).ToList();var value=ValuationEngine.Estimate(asks,asks[0],now,TimeSpan.FromDays(1),25);Check(value is not null&&value.Confidence<=40&&value.Count==3);Check(ValuationEngine.Find(asks.Append(O("low",500,EvidenceKind.Asking)),new Settings(){Confidence=1,MinimumProfit=0},now).Count==0);}));
await Test("Online search cannot bypass a rate-limit backoff",async()=>{var handler=new Handler(_=>throw new Exception("Must not request"));using var f=new Fetcher(handler);var st=new Settings();var config=st.Sources.Single(x=>x.Id=="Kerning Exchange");config.BlockedUntil=now.AddMinutes(15);st.Sources=[config];await new SourceCollector(f).Refresh(st,default,["Green Mushroom Cap"],true);Check(handler.Calls==0);});
await Test("Changed crawl policy stops item collection",async()=>{var handler=new Handler(_=>new(HttpStatusCode.OK){Content=new StringContent("User-agent: *\nDisallow: /")});using var f=new Fetcher(handler);var st=new Settings();st.Sources=st.Sources.Where(s=>s.Id=="Kerning Exchange").ToList();var result=await new SourceCollector(f).Refresh(st,default,["Green Mushroom Cap"],true);Check(handler.Calls==1&&result.Single().Status.StartsWith("Unavailable"));});
if(File.Exists("work/live-results/observations.json"))await Test("Actual retrieved listings produce an explicitly uncertain recommendation",()=>Sync(()=>{var response=JsonSerializer.Deserialize<SourceResult>(File.ReadAllText("work/live-results/observations.json"),Json.Options)!;var target=response.Rows.First(o=>o.Name=="Green Mushroom Cap");var estimate=ValuationEngine.Estimate(response.Rows,target,response.CheckedAt,TimeSpan.FromHours(72),25);Check(estimate is not null&&estimate.Confidence<=40&&estimate.Basis.Contains("uncertain"));File.WriteAllText("work/live-results/verified-recommendation.json",JsonSerializer.Serialize(new{item=target.Name,ageLimitHours=72,estimate,checkedAt=response.CheckedAt,source=target.Url},Json.Options));}));
if(args.Contains("--live")){

await Test("Live public item search end to end with persisted provenance",async()=>{

using var f=new Fetcher();var reader=new KerningReader(f);var config=new Settings().Sources.Single(x=>x.Id=="Kerning Exchange");

var response=await reader.Refresh(config,["Green Mushroom Cap","Cutlus","Garnet Ore","Blue Mushroom Cap","Tree Branch","Bronze Ore","Opal Ore","Steel Ore","Leather","Orange Potion"],true,default);

Directory.CreateDirectory("work/live-results");File.WriteAllText("work/live-results/observations.json",JsonSerializer.Serialize(response,Json.Options));

Check(response.SnapshotItems.Count>=3,"Fewer than three real item lookups succeeded");Check(response.Rows.Any(o=>o.Equipment)&&response.Rows.Any(o=>!o.Equipment));Check(response.Rows.All(o=>o.ObservedAt is not null));

var values=response.Rows.GroupBy(o=>o.MatchKey).Select(g=>new{item=g.First().Name,variant=g.First().Variant,estimate=ValuationEngine.Estimate(response.Rows,g.First(),DateTimeOffset.UtcNow,TimeSpan.FromHours(72),25)}).ToList();

File.WriteAllText("work/live-results/valuations.json",JsonSerializer.Serialize(values,Json.Options));

Console.WriteLine($"LIVE {response.SnapshotItems.Count} items, {response.Rows.Count} observations, {values.Count(x=>x.estimate!=null)} supported valuations");

using var db=new Store("work/live-results/market.db");db.Save(response.Rows);Check(db.Observations().Count>=response.Rows.Count);

var second=await reader.Refresh(config,["Green Mushroom Cap"],true,default);Check(!second.Updated&&second.Rows.Count==0,"Cached item fetched too soon");

});}


if(fixtureIndex>=0){
await Test("Genuine FM tracker aggregates preserve prices, counts and source time",()=>Sync(()=>{
var rows=FmTracker.Parse(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"fm-tracker-current.html")),now);Check(rows.Count>10);var pig=rows.Single(r=>r.ItemId=="4000008");Check(pig.Name=="Pig's Head"&&pig.Median7==333&&pig.LowestAsk==180&&pig.Sightings==24&&pig.LastSeen==DateTimeOffset.Parse("2026-10-09T22:15:41.762+00:00"));
Directory.CreateDirectory("work/fm-results");File.WriteAllText("work/fm-results/real-summaries.json",JsonSerializer.Serialize(rows,Json.Options));Console.WriteLine($"FM: {rows.Count} genuine item summaries");
}));
await Test("FM layout failures do not invent prices",()=>Sync(()=>Throws(()=>FmTracker.Parse("<html>Loading...</html>",now))));
await Test("FM future source times rejected",()=>Sync(()=>Throws(()=>FmTracker.Parse(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"fm-tracker-current.html")),DateTimeOffset.Parse("2020-01-01T00:00:00Z")))));
await Test("FM cache is separate from observations and survives restart",()=>Sync(()=>{
var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");var rows=FmTracker.Parse(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"fm-tracker-current.html")),now);
try{using(var db=new Store(path)){db.ReplaceSummaries(rows);Check(db.Observations().Count==0&&db.History().Count==0);Check(ValuationEngine.Find(db.Observations(),new Settings(),now).Count==0);}
using(var db=new Store(path)){Check(db.Summaries().Count==rows.Count);db.ClearMarket();Check(db.Summaries().Count==0);}}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}
}));
await Test("FM stale label uses original sighting, not retrieval",()=>Sync(()=>{var row=FmTracker.Parse(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"fm-tracker-current.html")),now).First();Check((row with{LastSeen=now.AddDays(-10),RetrievedAt=now}).Quality(now,72)=="Stale data");}));
}
await Test("Website directory distinguishes browser shortcuts from automatic imports",()=>Sync(()=>{Check(WebsiteDirectory.All.Length==12);Check(WebsiteDirectory.All.Count(w=>w.Connection.StartsWith("Automatic"))==3);Check(WebsiteDirectory.All.All(w=>new Uri(w.Url).Scheme=="https"));Check(WebsiteDirectory.All.Single(w=>w.Name=="MesoWatch").Connection=="Open website");}));
await Test("Target thresholds exclude stale, unavailable, sale and unknown-time observations",()=>Sync(()=>{
var rule=new PriceRule(Guid.NewGuid().ToString(),"Test ETC","",1000);var ask=O("target",1000,EvidenceKind.Asking);Check(CompanionFeatures.Matches(rule,ask,now,72));Check(!CompanionFeatures.Matches(rule,ask with{UnitPrice=1001},now,72));Check(!CompanionFeatures.Matches(rule,ask with{Available=false},now,72));Check(!CompanionFeatures.Matches(rule,ask with{Kind=EvidenceKind.ConfirmedSale},now,72));Check(!CompanionFeatures.Matches(rule,ask with{ObservedAt=null},now,72));Check(!CompanionFeatures.Matches(rule,ask with{RetrievedAt=now.AddMinutes(-31)},now,72));Check(!CompanionFeatures.Matches(rule,ask with{ObservedAt=now.AddDays(-8)},now,72));Check(!CompanionFeatures.Matches(rule with{Enabled=false},ask,now,72));
}));
await Test("Equipment targets require complete exact variant and dedup keys include rule and price",()=>Sync(()=>{
var rule=new PriceRule(Guid.NewGuid().ToString(),"Test ETC","atk=10; remaining=3",1000);var ask=O("target",1000,EvidenceKind.Asking) with{Equipment=true,VariantComplete=true,Variant=rule.Variant};Check(CompanionFeatures.Matches(rule,ask,now,72));Check(!CompanionFeatures.Matches(rule,ask with{Variant="atk=11; remaining=3"},now,72));Check(!CompanionFeatures.Matches(rule,ask with{VariantComplete=false},now,72));Check(!CompanionFeatures.Matches(rule with{Variant=""},ask,now,72));Check(CompanionFeatures.TargetKey(rule,ask)!=CompanionFeatures.TargetKey(rule,ask with{UnitPrice=900}));
}));
TradeEntry T(bool buy,long q,decimal price,decimal fee,int hour,string variant="")=>new(Guid.NewGuid().ToString(),"Test ETC",variant,buy,q,price,fee,now.AddHours(hour));
await Test("FIFO journal handles partial lots and allocates purchase and sale fees",()=>Sync(()=>{
var trades=new[]{T(true,10,100,10,-5),T(true,10,200,20,-4),T(false,15,300,15,-3),T(false,3,400,3,-2)};var result=CompanionFeatures.Outcomes(trades);Check(result[0].MatchedProfit==2465&&result[0].UnmatchedUnits==0);Check(result[1].MatchedProfit==591&&result[1].UnmatchedUnits==0);
}));
await Test("Journal excludes unmatched sold units and keeps variants separate",()=>Sync(()=>{
var result=CompanionFeatures.Outcomes(new[]{T(true,3,10,0,-4,"a"),T(false,4,20,0,-3,"a"),T(false,2,50,0,-2,"b")});Check(result[0].MatchedProfit==30&&result[0].UnmatchedUnits==1);Check(result[1].MatchedProfit==0&&result[1].UnmatchedUnits==2);
}));
await Test("Later purchases cannot supply cost for earlier sales",()=>Sync(()=>{var result=CompanionFeatures.Outcomes(new[]{T(false,1,30,0,-4),T(true,1,10,0,-2)});Check(result[0].UnmatchedUnits==1&&result[0].MatchedProfit==0);}));
await Test("Backup rejects versions, negative inventory and invalid trades before restore",()=>Sync(()=>{
Throws(()=>new PersonalBackup(2,[],new()).Validate());Throws(()=>new PersonalBackup(1,[new("Bad",-1,false,false)],new()).Validate());Throws(()=>new CompanionFeatures(){Trades=[T(true,0,10,0,-1)]}.Validate());Throws(()=>new CompanionFeatures(){Rules=[new("bad","Item","",10)]}.Validate());Throws(()=>new CompanionFeatures(){Equipment=new(){{"Invalid slot","Hat"}}}.Validate());
}));
await Test("Personal backup round trip and feature file preserve rules, journal, layout and sound",()=>Sync(()=>{
var feature=new CompanionFeatures(){Rules=[new(Guid.NewGuid().ToString(),"Item","",12)],Trades=[T(true,2,10,1,-1)],Equipment=new(){{"Hat","Test Hat"}},MenuSounds=true};var backup=new PersonalBackup(1,[new("Item",10,true,true){Variant="atk=5"}],feature);var restored=JsonSerializer.Deserialize<PersonalBackup>(JsonSerializer.Serialize(backup,Json.Options),Json.Options)!;restored.Validate();Check(restored.Items[0].Muted&&restored.Features.MenuSounds&&restored.Features.Trades.Count==1);
var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");try{feature.Save(path);Check(CompanionFeatures.Load(path).Equipment["Hat"]=="Test Hat");}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}
}));
await Test("Activity requires three distinct sellers on two days and excludes incomplete equipment",()=>Sync(()=>{
var rows=Enumerable.Range(0,3).Select(i=>O("a"+i,100,EvidenceKind.Asking) with{ObservedAt=now.AddDays(-3)}).Concat(Enumerable.Range(0,3).Select(i=>O("b"+i,200,EvidenceKind.Asking) with{ObservedAt=now.AddDays(-1)})).ToList();var activity=CompanionFeatures.Activity(rows,now);Check(activity.Count==1&&activity[0].EarlierMedian==100&&activity[0].LatestMedian==200);Check(CompanionFeatures.Activity(rows.Select(o=>o with{Seller="same"}),now).Count==0);Check(CompanionFeatures.Activity(rows.Select(o=>o with{Equipment=true,VariantComplete=false}),now).Count==0);Check(CompanionFeatures.Activity(rows.Take(3),now).Count==0);
}));
await Test("Equipment stat parsing preserves attack and upgrade slots without inventing values",()=>Sync(()=>{
var stats=CompanionFeatures.Stats("watk=42; str=3; remaining=4; used=3; passed=2; unknown=x");Check(stats["watk"]==42&&stats["remaining"]==4&&!stats.ContainsKey("dex")&&!stats.ContainsKey("unknown"));
}));
await Test("Replacing personal items is transactional and retains market observations",()=>Sync(()=>{
var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");try{using var db=new Store(path);db.Save([O("keep")]);db.SaveItem(new("Original",2,true,false));Throws(()=>db.ReplaceItems([new("Duplicate",1,false,false),new("Duplicate",2,false,false)]));Check(db.Items().Single().Name=="Original");db.ReplaceItems([new("Restored",4,true,true)]);Check(db.Items().Single().Quantity==4&&db.Observations().Count==1);}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}
}));

await Test("Legacy inventory migrates once and new profiles stay isolated after restart",()=>Sync(()=>{
var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");var id=Guid.NewGuid().ToString();try{
using(var db=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+path)){db.Open();using var c=db.CreateCommand();c.CommandText="CREATE TABLE items(name TEXT PRIMARY KEY,payload TEXT NOT NULL);INSERT INTO items VALUES('legacy',$p)";c.Parameters.AddWithValue("$p",JsonSerializer.Serialize(new SavedItem("Legacy",7,true,true),Json.Options));c.ExecuteNonQuery();}
using(var db=new Store(path)){Check(db.Items().Single().Quantity==7);db.SaveCharacter(new(id,"Storage",123));db.SelectCharacter(id);Check(db.Items().Count==0);db.SaveItem(new("Other",3,false,false));}
using(var db=new Store(path)){Check(db.Profile==id&&db.Items().Single().Name=="Other");db.SelectCharacter("main");Check(db.Items().Single().Quantity==7);db.RemoveItem("Legacy");}
using(var db=new Store(path)){db.SelectCharacter("main");Check(db.Items().Count==0);Check(db.Characters().Single(c=>c.Id==id).Mesos==123);}
}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}
}));
await Test("Character backup version two preserves balances and inventories",()=>Sync(()=>{
var id=Guid.NewGuid().ToString();var backup=new PersonalBackup(2,[],new()){ActiveCharacter=id,Characters=[new(new("main","Main",3),[new("A",1,true,false)]),new(new(id,"Storage",44),[new("B",2,false,false)])]};backup.Validate();var copy=JsonSerializer.Deserialize<PersonalBackup>(JsonSerializer.Serialize(backup,Json.Options),Json.Options)!;copy.Validate();Check(copy.Characters![1].Character.Mesos==44&&copy.Characters[0].Items[0].Name=="A");Throws(()=>(copy with{ActiveCharacter="missing"}).Validate());
}));
await Test("Profile replacement rollback retains original bags on duplicate names",()=>Sync(()=>{
var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");try{using var db=new Store(path);db.SaveItem(new("Keep",4,true,false));Throws(()=>db.ReplaceCharacters([new(new("main","Main",1),[new("Same",1,false,false),new("Same",2,false,false)])],"main"));Check(db.Items().Single().Name=="Keep");}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}
}));
await Test("CSV and pasted spreadsheet imports handle quoted names and exact variants",()=>Sync(()=>{
var csv=InventoryImport.Parse("Name,Quantity,Variant\n\"Item, Name\",12,\"watk=5; remaining=2\"\nLeather,3,");Check(csv.Count==2&&csv[0].Name=="Item, Name"&&csv[0].Variant=="watk=5; remaining=2");var tabs=InventoryImport.Parse("Blue Potion\t10\nLeather\t2");Check(tabs[0].Quantity==10);
}));
await Test("Bulk imports reject duplicate, malformed and negative rows atomically",()=>Sync(()=>{
Throws(()=>InventoryImport.Parse("Item,-1"));Throws(()=>InventoryImport.Parse("Item,0"));Throws(()=>InventoryImport.Parse("Item,1\nitem,2"));Throws(()=>InventoryImport.Parse("\"Item,1"));Throws(()=>InventoryImport.Parse("Item,price"));Throws(()=>InventoryImport.Parse(""));
}));
await Test("Bulk merge sets quantities, keeps unrelated stock and watch preferences",()=>Sync(()=>{
var merged=InventoryImport.Merge([new("Item",2,true,true),new("Other",4,false,false)],[new("Item",9,false,false)]);Check(merged.Single(i=>i.Name=="Item") is {Quantity:9,Watch:true,Muted:true}&&merged.Single(i=>i.Name=="Other").Quantity==4);Check(InventoryImport.Merge([new("Equip",1,false,false){Variant="watk=1"}],[new("Equip",2,false,false){Variant="watk=2"}]).Count==2);
}));
await Test("Linked trade stock supports purchase, sale, restart recovery and undo",()=>Sync(()=>{
var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");var buy=T(true,5,10,0,-1);try{using(var db=new Store(path)){db.ApplyStockTrade(buy);Check(db.Items().Single().Quantity==5&&db.HasStockChange(buy.Id));Throws(()=>db.ApplyStockTrade(buy));}using(var db=new Store(path)){Check(db.LinkedTrades().Single()==buy);var sale=T(false,2,20,0,0);db.ApplyStockTrade(sale);Check(db.Items().Single().Quantity==3);db.UndoStockTrade(sale.Id);Check(db.Items().Single().Quantity==5);db.UndoStockTrade(buy.Id);Check(db.Items().Count==0);}}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}
}));
await Test("Trade stock rejects overselling and profile mismatches while separating variants",()=>Sync(()=>{
var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");try{using var db=new Store(path);db.SaveItem(new("Test ETC",2,true,false){Variant="a"});Throws(()=>db.ApplyStockTrade(T(false,3,10,0,-1,"a")));db.ApplyStockTrade(T(true,1,10,0,-1,"b"));Throws(()=>db.ApplyStockTrade(T(true,1,10,0,-1,"a") with{Profile=Guid.NewGuid().ToString()}));Check(db.Items().Single(i=>i.Variant=="a").Quantity==2&&db.Items().Single(i=>i.Variant=="b").Quantity==1);}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}
}));
await Test("Trade undo never overwrites later inventory edits",()=>Sync(()=>{
var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");try{using var db=new Store(path);var buy=T(true,5,10,0,-1);db.ApplyStockTrade(buy);db.SaveItem(new("Test ETC",9,false,false));Throws(()=>db.UndoStockTrade(buy.Id));Check(db.Items().Single().Quantity==9&&db.HasStockChange(buy.Id));}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}
}));
await Test("FIFO profit cannot consume another character's purchase lot",()=>Sync(()=>{
var trades=new[]{T(true,1,10,0,-2),T(false,1,20,0,-1) with{Profile=Guid.NewGuid().ToString()}};Check(CompanionFeatures.Outcomes(trades).Single().UnmatchedUnits==1);
}));
await Test("Listing filters combine price, freshness, attack and upgrade slots",()=>Sync(()=>{
var row=O("filtered",500,EvidenceKind.Asking) with{Equipment=true,VariantComplete=true,Variant="watk=42; remaining=3"};var filter=new ListingFilter(true,true,600,"watk",40,3);Check(filter.Matches(row,now,72));Check(!filter.Matches(row with{UnitPrice=601},now,72));Check(!filter.Matches(row with{Variant="watk=42; remaining=2"},now,72));Check(!filter.Matches(row with{VariantComplete=false},now,72));Check(!filter.Matches(row with{ObservedAt=now.AddDays(-5)},now,72));Check(!filter.Matches(row with{Equipment=false},now,72));
}));
await Test("Inbox and display preferences survive feature-file serialization",()=>Sync(()=>{
var f=new CompanionFeatures(){TextSize=12,AutoBackups=true,Inbox=[new("key","main","Target reached","Sample notification","https://example.org",now,true)]};var copy=CompanionFeatures.Read(JsonSerializer.Serialize(f,Json.Options));Check(copy.Inbox.Single().Read&&copy.TextSize==12&&copy.AutoBackups);Throws(()=>new CompanionFeatures(){TextSize=30}.Validate());
}));

await Test("Merchant goals cap progress and restock counts normalized inventory",()=>Sync(()=>{
Check(MerchantTools.Progress(150,100)==100&&MerchantTools.Progress(25,100)==25);var r=new RestockRule(Guid.NewGuid().ToString(),"main","Red Potion",20);Check(MerchantTools.Needed(r,[new("red potion",7,false,false)])==13);Check(MerchantTools.Needed(r,[new("Red Potion",30,false,false)])==0);
}));
await Test("Exact gear wishlist excludes incomplete, stale, mismatched and over-budget asks",()=>Sync(()=>{
var w=new GearWish(Guid.NewGuid().ToString(),"main","Test ETC","watk=42; remaining=7",1000);var o=O("wish",900,EvidenceKind.Asking) with{Equipment=true,VariantComplete=true,Variant=w.Variant};Check(MerchantTools.Matches(w,o,now,72));foreach(var bad in new[]{o with{Equipment=false},o with{VariantComplete=false},o with{Variant="watk=43; remaining=7"},o with{Available=false},o with{UnitPrice=1001},o with{PriceApproximate=true},o with{RetrievedAt=now.AddHours(-1)},o with{ObservedAt=now.AddDays(-4)},o with{Kind=EvidenceKind.ConfirmedSale}})Check(!MerchantTools.Matches(w,bad,now,72));Check(MerchantTools.Matches(w with{Maximum=0},o with{UnitPrice=1001},now,72));
}));
await Test("Session profit uses purchases before session and excludes boundary and other character trades",()=>Sync(()=>{
var session=new MerchantSession(Guid.NewGuid().ToString(),"main","Run",now.AddHours(-2),now);var buy=new TradeEntry(Guid.NewGuid().ToString(),"Test","",true,10,100,10,now.AddHours(-4));var sell=new TradeEntry(Guid.NewGuid().ToString(),"Test","",false,3,200,6,now.AddHours(-1));var totals=MerchantTools.Totals(session,[buy,sell,sell with{Id=Guid.NewGuid().ToString(),At=now},sell with{Id=Guid.NewGuid().ToString(),Profile=Guid.NewGuid().ToString()}],now);Check(totals.Purchases==0&&totals.Sales==600&&totals.Fees==6&&totals.Profit==291&&totals.Trades==1&&totals.Unmatched==0);
}));
await Test("Session unmatched sales do not invent acquisition costs",()=>Sync(()=>{
var t=new TradeEntry(Guid.NewGuid().ToString(),"Test","",false,5,100,0,now.AddMinutes(-1));var s2=new MerchantSession(Guid.NewGuid().ToString(),"main","Run",now.AddHours(-1),null);var total=MerchantTools.Totals(s2,[t],now);Check(total.Profit==0&&total.Unmatched==5&&total.Sales==500);
}));
await Test("History preserves changed asks while removing repeated fetches",()=>Sync(()=>{
var row=O("history",100,EvidenceKind.Asking);var rows=MerchantTools.History([row,row with{RetrievedAt=now.AddMinutes(1)},row with{UnitPrice=120,ObservedAt=now},row with{ObservedAt=null}]);Check(rows.Count==2&&rows[0].RetrievedAt==now.AddMinutes(1)&&rows[1].UnitPrice==120);
}));
await Test("Merchant settings migrate old files and roundtrip all new records",()=>Sync(()=>{
var old=CompanionFeatures.Read("{}");Check(old.Goals.Count==0&&old.Theme=="Henesys"&&!old.SetupComplete);var f=new CompanionFeatures(){Theme="Ellinia",SetupComplete=true,Goals=[new(Guid.NewGuid().ToString(),"main","Gear",100)],Restocks=[new(Guid.NewGuid().ToString(),"main","Potion",10)],Wishes=[new(Guid.NewGuid().ToString(),"main","Gear","watk=1",0)],Memos=[new(Guid.NewGuid().ToString(),"main","Gear","Sell","My note")],Sessions=[new(Guid.NewGuid().ToString(),"main","Run",now.AddHours(-1),null)]};var copy=CompanionFeatures.Read(JsonSerializer.Serialize(f,Json.Options));Check(copy.Goals.Count==1&&copy.Restocks.Count==1&&copy.Wishes.Count==1&&copy.Memos.Single().Notes=="My note"&&copy.Sessions.Count==1&&copy.Theme=="Ellinia"&&copy.SetupComplete);
}));
await Test("Merchant validation rejects corrupt records and multiple active sessions",()=>Sync(()=>{
Throws(()=>new CompanionFeatures(){Theme="Unknown"}.Validate());Throws(()=>new CompanionFeatures(){Goals=[new(Guid.NewGuid().ToString(),"main","Goal",0)]}.Validate());Throws(()=>new CompanionFeatures(){Wishes=[new(Guid.NewGuid().ToString(),"main","Gear","",10)]}.Validate());Throws(()=>new CompanionFeatures(){Sessions=[new(Guid.NewGuid().ToString(),"main","A",now,null),new(Guid.NewGuid().ToString(),"main","B",now,null)]}.Validate());Throws(()=>new CompanionFeatures(){Memos=[new(Guid.NewGuid().ToString(),"main","Gear","Unsupported","")]}.Validate());
}));

await Test("Transfer moves items and mesos atomically and persists history",()=>Sync(()=>{
var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");var dest=Guid.NewGuid().ToString();using(var db=new Store(path)){db.SaveCharacter(new("main","Main",1000));db.SaveCharacter(new(dest,"Storage",20));db.SaveItem(new SavedItem("Red Potion",20,true,false));db.Transfer(dest,"Red Potion",5,100);Check(db.Items().Single().Quantity==15&&db.Items().Single().Watch);Check(db.ItemsFor(dest).Single().Quantity==5&&!db.ItemsFor(dest).Single().Watch);Check(db.Characters().Single(c=>c.Id=="main").Mesos==900&&db.Characters().Single(c=>c.Id==dest).Mesos==120);}using(var db=new Store(path)){Check(db.Transfers().Count==1&&db.Transfers().Single().Quantity==5);}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
}));
await Test("Rejected transfers never partly move items or mesos",()=>Sync(()=>{
var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");using(var db=new Store(path)){var dest=Guid.NewGuid().ToString();db.SaveCharacter(new("main","Main",100));db.SaveCharacter(new(dest,"Storage",0));db.SaveItem(new SavedItem("Gear",10,false,false){Variant="watk=1"});Throws(()=>db.Transfer(dest,"Gear",11,1));Throws(()=>db.Transfer(dest,"Gear",1,101));db.SelectCharacter(dest);db.SaveItem(new SavedItem("Gear",2,false,false){Variant="watk=2"});db.SelectCharacter("main");Throws(()=>db.Transfer(dest,"Gear",1,10,"missing variant"));Throws(()=>db.Transfer("main","Gear",1,10));Check(db.Items().Single().Quantity==10&&db.Characters().Single(c=>c.Id=="main").Mesos==100&&db.Transfers().Count==0);}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
}));
await Test("Mesos-only transfers and history survive backup restore",()=>Sync(()=>{
var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");using(var db=new Store(path)){var dest=Guid.NewGuid().ToString();db.SaveCharacter(new("main","Main",500));db.SaveCharacter(new(dest,"Storage",0));db.Transfer(dest,"",0,200);var b=new PersonalBackup(2,db.Items(),new()){Characters=db.AllInventories(),ActiveCharacter="main",Transfers=db.Transfers()};var copy=JsonSerializer.Deserialize<PersonalBackup>(JsonSerializer.Serialize(b,Json.Options),Json.Options)!;copy.Validate();db.ReplaceCharacters(copy.Characters!,copy.ActiveCharacter,copy.Transfers);Check(db.Transfers().Count==1&&db.Characters().Single(c=>c.Id==dest).Mesos==200);}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
}));
await Test("Shopping estimates reject uncertain equipment and account for remaining quantity",()=>Sync(()=>{
var entry=new ShoppingEntry(Guid.NewGuid().ToString(),"main","Test ETC","",10,3,1000);var row=O("shop",100,EvidenceKind.Asking);Check(WorkspaceOptions.ShoppingCost(entry,[row],now,72)==700);Check(WorkspaceOptions.ShoppingCost(entry,[row with{RetrievedAt=now.AddHours(-1)}],now,72)==null);Check(WorkspaceOptions.ShoppingCost(entry,[row with{Equipment=true,VariantComplete=false}],now,72)==null);Check(WorkspaceOptions.ShoppingCost(entry with{Purchased=10},[],now,72)==0);Check(WorkspaceOptions.ShoppingCost(entry,[row with{PriceApproximate=true}],now,72)==null);
}));
await Test("Quiet hours handle overnight, daytime, boundaries and all-day schedules",()=>Sync(()=>{
var o=new WorkspaceOptions(){QuietHours=true,QuietStart=22,QuietEnd=8};DateTime At(int h)=>new(2026,10,10,h,0,0);Check(o.QuietAt(At(22))&&o.QuietAt(At(7))&&!o.QuietAt(At(8))&&!o.QuietAt(At(21)));o.QuietStart=9;o.QuietEnd=17;Check(o.QuietAt(At(9))&&!o.QuietAt(At(17)));o.QuietEnd=9;Check(o.QuietAt(At(0)));o.QuietHours=false;Check(!o.QuietAt(At(0)));o.LastDesktopAlert=now;o.CooldownMinutes=5;Check(!o.CanNotify(now.AddMinutes(4))&&o.CanNotify(now.AddMinutes(5)));
}));
await Test("Workspace preferences roundtrip without losing plans, layout or delivery state",()=>Sync(()=>{
var o=new WorkspaceOptions(){Shopping=[new(Guid.NewGuid().ToString(),"main","Potion","",10,2,100)],Selling=[new(Guid.NewGuid().ToString(),"main","Potion",4)],Favorites=new(){{"main",["potion"]}},SlotOrder=new(){{"main",["potion","leather"]}},DashboardCards=["Inventory","Market"],DeliveredAlerts=["main|key"],QuietHours=true};var f=CompanionFeatures.Read(JsonSerializer.Serialize(new CompanionFeatures(){Workspace=o},Json.Options));Check(f.Workspace.Shopping.Single().Purchased==2&&f.Workspace.Selling.Count==1&&f.Workspace.Favorites["main"].Contains("potion")&&f.Workspace.SlotOrder["main"][1]=="leather"&&f.Workspace.DashboardCards[0]=="Inventory"&&f.Workspace.DeliveredAlerts.Contains("main|key"));
}));
await Test("Workspace validation and trade totals reject invalid plans and account for fees",()=>Sync(()=>{
Throws(()=>new WorkspaceOptions(){Shopping=[new(Guid.NewGuid().ToString(),"main","Potion","",1,2,0)]}.Validate());Throws(()=>new WorkspaceOptions(){QuietStart=24}.Validate());Throws(()=>new WorkspaceOptions(){DashboardCards=["Market","Market"]}.Validate());Check(WorkspaceOptions.TradeTotal(true,5,100,10)==510&&WorkspaceOptions.TradeTotal(false,5,100,10)==490);
}));

await Test("Undo inventory survives restart and restores saved stock and watch state",()=>Sync(()=>{
var path=Path.Combine("work","undo-tests",Guid.NewGuid()+".db");string id;using(var db=new Store(path)){db.SaveItem(new("Potion",4,true,false));db.SaveItem(new("Potion",8,false,false));id=db.InventoryChanges().First().Id;}using(var db=new Store(path)){db.UndoInventory(id);Check(db.Items().Single().Quantity==4&&db.Items().Single().Watch&&db.InventoryChanges().Single(x=>x.Id==id).Undone);Throws(()=>db.UndoInventory(id));}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
}));
await Test("Undo refuses later edits and can unwind newer changes first",()=>Sync(()=>{
var path=Path.Combine("work","undo-tests",Guid.NewGuid()+".db");using(var db=new Store(path)){db.SaveItem(new("Potion",4,false,false));var first=db.InventoryChanges().First();db.SaveItem(new("Potion",8,false,false));var second=db.InventoryChanges().First();Throws(()=>db.UndoInventory(first.Id));Check(db.Items().Single().Quantity==8);db.UndoInventory(second.Id);db.UndoInventory(first.Id);Check(db.Items().Count==0);}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
}));
await Test("Import undo is atomic and wrong-character undo is rejected",()=>Sync(()=>{
var path=Path.Combine("work","undo-tests",Guid.NewGuid()+".db");using(var db=new Store(path)){db.SaveItem(new("Potion",3,false,false));db.ReplaceItems([new("Potion",10,false,false),new("Leather",5,false,false)]);var change=db.InventoryChanges().First();var profile=Guid.NewGuid().ToString();db.SaveCharacter(new(profile,"Other",0));db.SelectCharacter(profile);Throws(()=>db.UndoInventory(change.Id));db.SelectCharacter("main");db.UndoInventory(change.Id);Check(db.Items().Single().Quantity==3);var count=db.InventoryChanges().Count;Throws(()=>db.ReplaceItems([new("Potion",1,false,false),new("Potion",2,false,false)]));Check(db.Items().Single().Quantity==3&&db.InventoryChanges().Count==count);}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
}));
await Test("Price changes use same listing and variant and exclude approximate asks",()=>Sync(()=>{
var old=O("moving",100,EvidenceKind.Asking) with{RetrievedAt=now.AddHours(-1)};var latest=old with{UnitPrice=80,RetrievedAt=now};var changes=UsabilityTools.Changes([old,latest]);Check(changes.Count==1&&changes[0].Before==100&&changes[0].After==80);Check(UsabilityTools.Changes([old,latest with{Variant="different"}]).Count==0);Check(UsabilityTools.Changes([old,latest with{ListingId="different"}]).Count==0);Check(UsabilityTools.Changes([old,latest with{PriceApproximate=true}]).Count==0);
}));
await Test("Profit breakdown includes purchase fees, residual stock and unmatched sales",()=>Sync(()=>{
var buy=new TradeEntry(Guid.NewGuid().ToString(),"Potion","",true,10,100,20,now.AddHours(-2));var sell=new TradeEntry(Guid.NewGuid().ToString(),"Potion","",false,4,200,8,now.AddHours(-1));var row=UsabilityTools.Profits([buy,sell]).Single();Check(row.Profit==384&&row.Fees==28&&row.Unsold==6&&row.Cost==612&&row.Unmatched==0);row=UsabilityTools.Profits([sell]).Single();Check(row.Profit==0&&row.Unmatched==4&&row.Cost==0&&row.Unsold==0);
}));
await Test("Pinned pages roundtrip and invalid duplicate pins are rejected",()=>Sync(()=>{
var f=new CompanionFeatures();f.Workspace.PinnedPages=["Inventory","Price Checker"];var copy=CompanionFeatures.Read(JsonSerializer.Serialize(f,Json.Options));Check(copy.Workspace.PinnedPages.SequenceEqual(f.Workspace.PinnedPages));Throws(()=>new WorkspaceOptions(){PinnedPages=["Inventory","Inventory"]}.Validate());
}));

await Test("Different equipment stacks edit, remove and undo independently",()=>Sync(()=>{
var path=Path.Combine("work","variants-tests",Guid.NewGuid()+".db");using(var db=new Store(path)){var a=new SavedItem("Sword",2,true,false){Variant="watk=42"};var b=a with{Quantity=3,Variant="watk=48"};db.SaveItem(a);db.SaveItem(b);db.SaveItem(a with{Quantity=1});Check(db.Items().Count==2&&db.Items().Single(i=>i.Variant==b.Variant).Quantity==3);db.RemoveItem(a.Name,a.Variant);Check(db.Items().Single().Variant==b.Variant);db.UndoInventory(db.InventoryChanges().First().Id);Check(db.Items().Count==2);}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
}));
await Test("Exact variant transfers preserve a different destination stack",()=>Sync(()=>{
var path=Path.Combine("work","variants-tests",Guid.NewGuid()+".db");using(var db=new Store(path)){var dest=Guid.NewGuid().ToString();db.SaveCharacter(new(dest,"Storage",0));db.SaveItem(new("Sword",2,false,false){Variant="a"});db.SaveItem(new("Sword",3,false,false){Variant="b"});Throws(()=>db.Transfer(dest,"Sword",1,0));db.SelectCharacter(dest);db.SaveItem(new("Sword",5,false,false){Variant="b"});db.SelectCharacter("main");db.Transfer(dest,"Sword",1,0,"a");Check(db.ItemsFor(dest).Single(i=>i.Variant=="b").Quantity==5&&db.ItemsFor(dest).Single(i=>i.Variant=="a").Quantity==1&&db.Items().Single(i=>i.Variant=="b").Quantity==3);}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
}));
await Test("Legacy variant-key migration preserves linked trade undo",()=>Sync(()=>{
var path=Path.Combine("work","variants-tests",Guid.NewGuid()+".db");var t=T(true,2,10,0,-1,"watk=3");using(var db=new Store(path)){db.ApplyStockTrade(t);}using(var connection=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+path)){connection.Open();using var c=connection.CreateCommand();c.CommandText="DELETE FROM schema_flags;UPDATE profile_items SET name='test etc';UPDATE stock_changes SET name='test etc';";c.ExecuteNonQuery();}using(var db=new Store(path)){Check(db.Items().Single().Variant==t.Variant);db.UndoStockTrade(t.Id);Check(db.Items().Count==0);}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
}));
await Test("Variant import and version-three backups preserve distinct stacks",()=>Sync(()=>{
var items=InventoryImport.Parse("Name,Quantity,Variant\nSword,1,watk=42\nSword,2,watk=48");Check(items.Count==2);Throws(()=>InventoryImport.Parse("Sword,1,watk=42\nSword,2,watk=42"));var b=new PersonalBackup(3,items,new()){Characters=[new(new("main","Main",0),items)]};b.Validate();var copy=JsonSerializer.Deserialize<PersonalBackup>(JsonSerializer.Serialize(b,Json.Options),Json.Options)!;copy.Validate();Check(copy.Characters!.Single().Items.Count==2);Throws(()=>new PersonalBackup(3,[items[0],items[0]],new()){Characters=b.Characters}.Validate());
}));
await Test("Aliases initials and typo search do not alter exact inventory identity",()=>Sync(()=>{
var options=new PolishOptions(){Aliases=new(){{"my cap","Blue Mushroom Cap"}}};Check(options.Resolve("MY CAP")=="Blue Mushroom Cap"&&PolishTools.Similar("bmc","Blue Mushroom Cap")&&PolishTools.Similar("Blue Mushrom Cap","Blue Mushroom Cap")&&!PolishTools.Similar("Sword","Blue Mushroom Cap"));Check(ItemIdentity.Key("Sword","watk=1")!=ItemIdentity.Key("Sword","watk=2"));
}));
await Test("Duplicate trades stay isolated by time profile and equipment variant",()=>Sync(()=>{
var a=T(true,1,100,2,-1,"a");var b=a with{Id=Guid.NewGuid().ToString(),At=a.At.AddMinutes(3)};Check(PolishTools.Duplicate(a,b));Check(!PolishTools.Duplicate(a,b with{Variant="b"})&&!PolishTools.Duplicate(a,b with{Profile=Guid.NewGuid().ToString()})&&!PolishTools.Duplicate(a,b with{At=a.At.AddMinutes(6)}));
}));
await Test("History cache invalidates on new prices and clear",()=>Sync(()=>{
var path=Path.Combine("work","variants-tests",Guid.NewGuid()+".db");using(var db=new Store(path)){db.Save([O("cache1")]);var history=db.History();history.Clear();Check(db.History().Count==1);db.Save([O("cache2")]);Check(db.History().Count==2);db.ClearMarket();Check(db.History().Count==0);}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
}));
await Test("Accessibility preferences validate and survive backup serialization",()=>Sync(()=>{
var f=new CompanionFeatures(){Polish=new(){SlotSize=80,HighContrast=true,InventoryDetailWidth=350,Aliases=new(){{"pots","Red Potion"}}}};var copy=CompanionFeatures.Read(JsonSerializer.Serialize(f,Json.Options));Check(copy.Polish.SlotSize==80&&copy.Polish.HighContrast&&copy.Polish.Resolve("pots")=="Red Potion");Throws(()=>new PolishOptions(){SlotSize=300}.Validate());
}));

await Test("Evidence labels separate posting age availability check and disabled source",()=>Sync(()=>{
var row=O("quality",100,EvidenceKind.Asking);Check(ReliabilityTools.EvidenceStatus(row,true,now,72).StartsWith("Recently checked"));Check(ReliabilityTools.EvidenceStatus(row with{RetrievedAt=now.AddHours(-1)},true,now,72).StartsWith("Cached"));Check(ReliabilityTools.EvidenceStatus(row with{ObservedAt=now.AddDays(-4)},true,now,72).StartsWith("Old source"));Check(ReliabilityTools.EvidenceStatus(row,false,now,72).StartsWith("Source disabled"));Check(ReliabilityTools.EvidenceStatus(row with{ObservedAt=null},true,now,72)=="Source time unknown");Check(ReliabilityTools.EvidenceStatus(row with{Available=false},true,now,72).StartsWith("Unavailable"));
}));
await Test("Delayed alerts suppress changed sold stale disabled and muted listings",()=>Sync(()=>{
var row=O("pending",100,EvidenceKind.Asking) with{Variant="watk=42"};var entry=new InboxEntry("target:key","main","Target","Test","https://example.org",now){ItemName=row.Name,Variant=row.Variant,ListingKey=row.Key,Price=row.UnitPrice};Check(ReliabilityTools.CanDeliver(entry,[],[row],now,72));foreach(var bad in new[]{row with{Available=false},row with{UnitPrice=200},row with{RetrievedAt=now.AddHours(-1)},row with{ObservedAt=now.AddDays(-5)},row with{Variant="watk=48"}})Check(!ReliabilityTools.CanDeliver(entry,[],[bad],now,72));Check(!ReliabilityTools.CanDeliver(entry,[],[],now,72));Check(!ReliabilityTools.CanDeliver(entry,[new(row.Name,1,true,true){Variant=row.Variant}],[row],now,72));Check(ReliabilityTools.CanDeliver(entry,[new(row.Name,1,true,true){Variant="watk=48"}],[row],now,72));
}));
await Test("Legacy market notifications remain in inbox without unvalidated redelivery",()=>Sync(()=>{
var entry=new InboxEntry("inbox-deal:old","main","Old deal","Test","https://example.org",now);Check(!ReliabilityTools.CanDeliver(entry,[],[],now,72));var features2=new CompanionFeatures(){Inbox=[entry]};var restored=CompanionFeatures.Read(JsonSerializer.Serialize(features2,Json.Options));Check(restored.Inbox.Count==1&&restored.Inbox[0].ListingKey=="");
}));
await Test("Notification evidence identity survives saved data roundtrip",()=>Sync(()=>{
var row=O("saved-alert",100,EvidenceKind.Asking);var entry=new InboxEntry("target:key","main","Target","Test",row.Url,now){ItemName=row.Name,Variant=row.Variant,ListingKey=row.Key,Price=row.UnitPrice};var f=CompanionFeatures.Read(JsonSerializer.Serialize(new CompanionFeatures(){Inbox=[entry]},Json.Options));Check(f.Inbox.Single()==entry&&ReliabilityTools.CanDeliver(f.Inbox.Single(),[],[row],now,72));
}));


await Test("Net profit deducts both total fees at quantity",()=>Sync(()=>{Check(CompanionUpgrade.NetProfit(100,160,5,20,30)==250);Check(CompanionUpgrade.NetProfit(100,80,2,5,0)==-45);Throws(()=>CompanionUpgrade.NetProfit(10,20,0,0,0));}));
await Test("Screenshot suggestion does not guess unlabeled quantities",()=>Sync(()=>{var r=CompanionUpgrade.Suggest("Red Potion\n70",["Red Potion"]);Check(r.Name=="Red Potion"&&r.Quantity is null);var a=CompanionUpgrade.Suggest("Red Potion\nBlue Potion\nQuantity: 4",["Red Potion","Blue Potion"]);Check(a.Name==""&&a.Quantity==4);}));
await Test("Screenshot fields use exact catalog identity and labeled stats",()=>Sync(()=>{var r=CompanionUpgrade.Suggest("Test Sword\nQuantity: 1\nWeapon Attack: +42\nSTR: 3\nRemaining upgrades: 7",["Test Sword"]);Check(r.Name=="Test Sword"&&r.Quantity==1&&r.Variant.Contains("watk=42")&&r.Variant.Contains("remaining=7"));Check(CompanionUpgrade.Suggest("Test Svvord",["Test Sword"]).Name=="");}));
await Test("Conflicting OCR quantities require manual review",()=>Sync(()=>{Check(CompanionUpgrade.Suggest("Qty: 5\nQty: 7",[]).Quantity is null);Check(CompanionUpgrade.Suggest("Qty: 1000000001",[]).Quantity is null);}));
await Test("Session cash flow separates realized profit and unmatched sales",()=>Sync(()=>{var start=now.AddMinutes(-30);var buy=new TradeEntry(Guid.NewGuid().ToString(),"Potion","",true,10,10,0,start.AddMinutes(-10));var sale=new TradeEntry(Guid.NewGuid().ToString(),"Potion","",false,4,20,2,start.AddMinutes(1));var r=CompanionUpgrade.SessionRecap(new("s","main","session",start,now),[buy,sale],now);Check(r.Purchases==0&&r.Sales==80&&r.NetCashFlow==78&&r.MatchedProfit==38&&r.TradeCount==1);}));
await Test("Draft atomic roundtrip preserves variant fees and stock intent",()=>Sync(()=>{var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");try{var entry=new TradeEntry(Guid.NewGuid().ToString(),"Sword","watk=42",false,2,50,3,now);var draft=new TradeDraft(entry,null,true,now);draft.Save(path);Check(TradeDraft.Load(path)==draft);Check(!File.Exists(path+".tmp"));}finally{File.Delete(path);}}));
await Test("Draft recovery rejects changed originals and prevents duplicate IDs",()=>Sync(()=>{var entry=new TradeEntry(Guid.NewGuid().ToString(),"Sword","watk=42",false,2,50,3,now);var draft=new TradeDraft(entry,null,true,now);Check(draft.CanResume([]));Check(draft.AlreadyRecorded([entry])&&!draft.CanResume([entry]));var edit=new TradeDraft(entry with{Quantity=3},entry,false,now);Check(edit.CanResume([entry])&&!edit.CanResume([entry with{Fees=4}])&&!edit.CanResume([]));}));
await Test("Old preferences keep offline disabled and old session balance unknown",()=>Sync(()=>{var f=CompanionFeatures.Read("{}");Check(!f.Offline);var session=JsonSerializer.Deserialize<MerchantSession>("{\"Id\":\"s\",\"Profile\":\"main\",\"Name\":\"old\",\"Start\":\"2026-10-01T00:00:00Z\"}");Check(session?.StartMesos is null);}));


await Test("Stock basis uses FIFO remaining lots and purchase fees",()=>Sync(()=>{var a=new TradeEntry(Guid.NewGuid().ToString(),"Sword","watk=42",true,5,100,10,now.AddDays(-3));var b=a with{Id=Guid.NewGuid().ToString(),Quantity=4,UnitPrice=200,Fees=0,At=now.AddDays(-2)};var sale=a with{Id=Guid.NewGuid().ToString(),Buy=false,Quantity=6,At=now.AddDays(-1)};var r=PlanningTools.Basis(new("Sword",3,false,false){Variant="watk=42"},[a,b,sale]);Check(r.JournalUnits==3&&r.Cost==600&&r.Oldest==b.At);}));
await Test("Stock basis remains unknown on stock mismatch or variant mismatch",()=>Sync(()=>{var t=new TradeEntry(Guid.NewGuid().ToString(),"Sword","watk=42",true,5,100,0,now);Check(PlanningTools.Basis(new("Sword",4,false,false){Variant="watk=42"},[t]).Cost is null);Check(PlanningTools.Basis(new("Sword",5,false,false){Variant="watk=48"},[t]).Cost is null);}));
await Test("Priority thresholds keep buy and sell directions separate",()=>Sync(()=>{var row=O("priority",100,EvidenceKind.Asking);var p=new WatchPriority("main",row.Name,"","Buy soon",100);Check(PlanningTools.PriorityMatches(p,row,now,24));Check(!PlanningTools.PriorityMatches(p,row with{UnitPrice=101},now,24));Check(PlanningTools.PriorityMatches(p with{Group="Sell soon"},row with{UnitPrice=101},now,24));Check(!PlanningTools.PriorityMatches(p with{Group="Just tracking"},row,now,24));Check(!PlanningTools.PriorityMatches(p,row with{RetrievedAt=now.AddHours(-1)},now,24));}));
await Test("Variant history never pools equipment or sales into asking chart",()=>Sync(()=>{var a=O("h",100,EvidenceKind.Asking) with{Equipment=true,VariantComplete=true,Variant="watk=42"};var rows=PlanningTools.VariantHistory([a,a with{ListingId="other",Variant="watk=48"},a with{ListingId="sale",Kind=EvidenceKind.ConfirmedSale}],a.Name,"watk=42");Check(rows.Count==1&&rows[0].Variant=="watk=42");}));
await Test("Template use creates new identity and current time without changing fields",()=>Sync(()=>{var t=new TradeTemplate(Guid.NewGuid().ToString(),"main","Potion sale","Potion","",false,10,50,7);var a=PlanningTools.FromTemplate(t,now);var b=PlanningTools.FromTemplate(t,now);Check(a.Id!=b.Id&&a.At==now&&a.Fees==7&&a.Quantity==10&&!a.Buy);}));
await Test("Planner preferences validate and survive roundtrip",()=>Sync(()=>{var f=new CompanionFeatures();f.Planning.Budgets["main"]=500;f.Planning.Templates.Add(new(Guid.NewGuid().ToString(),"main","Sale","Potion","",false,1,10,0));f.Planning.Priorities.Add(new("main","Potion","","Buy soon",10));var copy=CompanionFeatures.Read(JsonSerializer.Serialize(f,Json.Options));Check(copy.Planning.Templates.Count==1&&copy.Planning.Budgets["main"]==500);copy.Planning.Budgets["main"]=-1;Throws(copy.Validate);Check(CompanionFeatures.Read("{}").Planning.DashboardPanels.Count==4);}));
string BasilFixture(string kind="sell",string status="active",int quantity=100,decimal price=1000,string stamp="Sat Oct 10 2026 19:46:31 GMT-0400 (Eastern Daylight Time)",string icon="/images/items/04010001.png")
{
    var payload=new[]{new{id=273,kind,status,quantity,price,createdAt=stamp,sellerName="Fixture seller",item=new{name="Fixture item",icon},stats=new{watk=42},slotsLeft=7}};
    return "<script>self.__next_f.push([1,"+JsonSerializer.Serialize("0:"+JsonSerializer.Serialize(payload)+"\n")+"])</script>";
}
await Test("Basil bulk listings retain per-item price and original offset timestamp",()=>Sync(()=>{var rows=BasilPages.Parse(BasilFixture(),DateTimeOffset.Parse("2026-10-11T01:00:00Z"));var r=rows.Single();Check(r.UnitPrice==1000&&r.Quantity==100&&r.ObservedAt==DateTimeOffset.Parse("2026-10-10T23:46:31Z")&&!r.Equipment&&r.Available&&r.Kind==EvidenceKind.Asking);}));
await Test("Basil buy auction closed and equipment evidence stay separate",()=>Sync(()=>{var at=DateTimeOffset.Parse("2026-10-11T01:00:00Z");Check(BasilPages.Parse(BasilFixture(kind:"buy"),at).Single().Kind==EvidenceKind.Buying);Check(BasilPages.Parse(BasilFixture(kind:"auction"),at).Count==0);Check(!BasilPages.Parse(BasilFixture(status:"sold"),at).Single().Available);var equip=BasilPages.Parse(BasilFixture(icon:"/images/items/01082007.png"),at).Single();Check(equip.Equipment&&!equip.VariantComplete&&equip.Variant.Contains("watk=42")&&equip.Variant.Contains("remaining=7"));}));
await Test("Basil changed layout invalid future time and invalid quantity fail closed",()=>Sync(()=>{Throws(()=>BasilPages.Parse("<h1>Loading</h1>",now));Throws(()=>BasilPages.Parse(BasilFixture(stamp:"bad time"),now));Throws(()=>BasilPages.Parse(BasilFixture(stamp:"Sun Oct 10 2094 19:46:31 GMT-0400 (Eastern Daylight Time)"),now));Throws(()=>BasilPages.Parse(BasilFixture(quantity:0),now));}));
await Test("Basil duplicate rendered rows do not multiply evidence",()=>Sync(()=>{Check(BasilPages.Parse(BasilFixture()+BasilFixture(),DateTimeOffset.Parse("2026-10-11T01:00:00Z")).Count==1);}));
await Test("Basil source policy denial makes no listing request",async()=>{var h=new Handler(_=>new(HttpStatusCode.OK){Content=new StringContent("User-agent: *\nDisallow: /\n")});using var f=new Fetcher(h);var config=new SourceConfig(){Id="BasilMarket",Enabled=true,PermissionReference=BasilPages.AccessReference};var result=await new SourceCollector(f).Refresh(new(){Sources=[config]},default);Check(h.Calls==1&&!result.Single().Updated&&config.BlockedUntil>now);});
await Test("Basil policy server errors are not treated as absent robots",async()=>{var h=new Handler(_=>new(HttpStatusCode.Forbidden));using var f=new Fetcher(h);var config=new SourceConfig(){Id="BasilMarket",Enabled=true,PermissionReference=BasilPages.AccessReference};var result=await new SourceCollector(f).Refresh(new(){Sources=[config]},default);Check(h.Calls==1&&!result.Single().Updated);});
await Test("Basil missing robots permits public page only and refresh search respects cache",async()=>{var h=new Handler(r=>r.RequestUri!.AbsolutePath=="/robots.txt"?new(HttpStatusCode.NotFound):new(HttpStatusCode.OK){Content=new StringContent(BasilFixture())});using var f=new Fetcher(h);var config=new SourceConfig(){Id="BasilMarket",Enabled=true,PermissionReference=BasilPages.AccessReference};var settings2=new Settings(){Sources=[config]};var collector=new SourceCollector(f);var result=await collector.Refresh(settings2,default);Check(h.Calls==3&&result.Single().Updated&&result.Single().Rows.Count==1&&result.Single().SnapshotItems.Count==0);await collector.Refresh(settings2,default,["Fixture"],true);Check(h.Calls==3);});
if(fixtureIndex>=0)await Test("Basil captured public homepage yields timestamped genuine listings",()=>Sync(()=>{var rows=BasilPages.Parse(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"source-audit18/basil-home.txt")),DateTimeOffset.Parse("2026-10-11T01:00:00Z"));Check(rows.Count>20&&rows.All(r=>r.ObservedAt is not null));var glove=rows.Single(r=>r.ListingId=="343");Check(glove.Name=="Black Duo"&&glove.UnitPrice==3000&&glove.Equipment&&!glove.VariantComplete);}));

if(fixtureIndex>=0)
{
await Test("Basil category bulk totals convert once and remain approximate with unknown timestamp",()=>Sync(()=>{var rows=BasilPages.Browse(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"basil-category19.html")),now);Check(rows.Count==10);var skin=rows.Single(r=>r.ListingId=="307");Check(skin.Name=="Jr. Necki Skin"&&skin.Quantity==300&&skin.UnitPrice==39&&skin.PriceApproximate&&skin.ObservedAt is null);Check(rows.All(r=>r.Kind==EvidenceKind.Asking&&r.Available));}));
await Test("Basil item search yields matching listings outside homepage",()=>Sync(()=>{var rows=BasilPages.Browse(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"basil-search19.html")),now);Check(rows.Count==2&&rows.All(r=>r.Name=="Garnet"&&r.UnitPrice==1300));}));
await Test("Basil category discovery accepts only public sale category paths",()=>Sync(()=>{var paths=BasilPages.Categories(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"source-audit18/basil-sell.txt")));Check(paths.Count>15&&paths.Contains("/market/sell?type=etc"));Check(!BasilPages.CategoryPath("https://other.example/market/sell?type=etc")&&!BasilPages.CategoryPath("/api/listings")&&!BasilPages.CategoryPath("/market/sell?type=etc&bad=1"));}));
}
await Test("Basil browse recognizes explicit empty results and rejects loading errors",()=>Sync(()=>{Check(BasilPages.Browse("<div>0 listings</div>",now).Count==0);Throws(()=>BasilPages.Browse("<div>Loading...</div>",now));}));
await Test("Basil category policy denial preserves exact homepage evidence",async()=>{var h=new Handler(r=>r.RequestUri!.AbsolutePath=="/robots.txt"?new(HttpStatusCode.OK){Content=new StringContent("User-agent: *\nAllow: /\nDisallow: /market/")}:new(HttpStatusCode.OK){Content=new StringContent(BasilFixture())});using var f=new Fetcher(h);var c=new SourceConfig(){Id="BasilMarket",Enabled=true,PermissionReference=BasilPages.AccessReference};var result=await new BasilReader(f).Refresh(c,default);Check(h.Calls==2&&result.Updated&&result.Rows.Count==1&&result.SnapshotItems.Count==0);} );


if(fixtureIndex>=0){
await Test("Basil detail verifies exact per-item price without dividing twice or inventing time",()=>Sync(()=>{var r=BasilPages.Browse(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"basil-category19.html")),now).Single(x=>x.ListingId=="307");var d=BasilPages.Detail(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"basil-detail20-307.html")),r,now);Check(d.UnitPrice==39&&d.Quantity==300&&!d.PriceApproximate&&d.ObservedAt is null&&d.Verification.StartsWith("Exact numeric"));}));
await Test("Basil rounded detail each price never becomes falsely exact",()=>Sync(()=>{var r=BasilPages.Browse(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"basil-category19.html")),now).Single(x=>x.ListingId=="292");Check(BasilPages.Detail(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"basil-detail20-292.html")),r,now)==r);}));
await Test("Basil single item rounded detail keeps approximation",()=>Sync(()=>{var r=BasilPages.Browse(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"basil-category19.html")),now).Single(x=>x.ListingId=="297");Check(BasilPages.Detail(File.ReadAllText(Path.Combine(args[fixtureIndex+1],"basil-detail20-297.html")),r,now).PriceApproximate);}));
}
await Test("Basil exact detail validates identity and sale kind",()=>Sync(()=>{var r=O("detail") with{Name="Test",PriceApproximate=true,Quantity=1,ObservedAt=null};var html="<div><h1>Test</h1><span>sell</span></div><div><div>Price</div><div>12,345</div></div>";var d=BasilPages.Detail(html,r,now);Check(d.UnitPrice==12345&&!d.PriceApproximate&&d.ObservedAt is null);Throws(()=>BasilPages.Detail(html.Replace("Test","Other"),r,now));Throws(()=>BasilPages.Detail(html.Replace("sell","buy"),r,now));Throws(()=>BasilPages.Detail(html.Replace("sell","sold"),r,now));}));


List<Observation> BasilQueueRows()=>Enumerable.Range(1,25).Select(i=>O(i.ToString(),100,EvidenceKind.Asking) with{Provider="BasilMarket",PriceApproximate=true,Url="https://basilmarket.net/market/listing/"+i,Quantity=i}).ToList();
await Test("Basil detail budget doubles normal capacity without exceeding batch cap",()=>Sync(()=>{var c=new SourceConfig();Check(BasilPages.DetailQueue(BasilQueueRows(),c,now,7).Count==8);Check(BasilPages.DetailQueue(BasilQueueRows(),c,now,5).Count==10);Check(BasilPages.DetailQueue(BasilQueueRows(),c,now,2).Count==12);Check(BasilPages.DetailQueue(BasilQueueRows(),c,now,15).Count==0);}));
await Test("Basil detail budget respects remaining rolling hourly allowance",()=>Sync(()=>{var c=new SourceConfig(){RequestTimes=Enumerable.Repeat(now.AddMinutes(-10),58).ToList()};Check(BasilPages.DetailQueue(BasilQueueRows(),c,now,7).Count==2);c.RequestTimes.AddRange([now,now]);Check(BasilPages.DetailQueue(BasilQueueRows(),c,now,7).Count==0);c.RequestTimes=Enumerable.Repeat(now.AddHours(-2),60).ToList();Check(BasilPages.DetailQueue(BasilQueueRows(),c,now,7).Count==8);}));
await Test("Basil cached details do not consume queue slots and unseen listings rotate first",()=>Sync(()=>{var c=new SourceConfig();for(int i=18;i<=25;i++)c.PageChecked["/market/listing/"+i]=now.AddMinutes(-1);var q=BasilPages.DetailQueue(BasilQueueRows(),c,now,7);Check(q.Count==8&&q.All(r=>int.Parse(r.ListingId)<18));c.PageChecked["/market/listing/17"]=now.AddHours(-1);Check(BasilPages.DetailQueue(BasilQueueRows(),c,now,7).All(r=>r.ListingId!="17"));}));
await Test("Basil queue excludes foreign sold exact and duplicate records",()=>Sync(()=>{var rows=BasilQueueRows();var one=rows[0];var q=BasilPages.DetailQueue([one,one,one with{Provider="Other"},one with{ListingId="2",Url="https://other.example/market/listing/2"},one with{ListingId="3",Available=false},one with{ListingId="4",PriceApproximate=false}],new(),now,0);Check(q.Count==1&&q[0].ListingId=="1");}));


await Test("Basil full sweep finishes rounded listings instead of endlessly retrying them",()=>Sync(()=>{var rows=BasilQueueRows();var c=new SourceConfig();int checkedCount=0;for(int batch=0;batch<4;batch++){var q=BasilPages.DetailQueue(rows,c,now,7);foreach(var row in q){BasilProgress.Complete(row,c,"Rounded",now);checkedCount++;}}Check(checkedCount==25&&BasilPages.DetailQueue(rows,c,now,7).Count==0&&BasilProgress.Summary(rows,c,now).Contains("0 left to check"));}));
await Test("Basil changed prices new listings and next-day refresh reopen queue",()=>Sync(()=>{var row=BasilQueueRows()[0];var c=new SourceConfig();BasilProgress.Complete(row,c,"Rounded",now);Check(!BasilProgress.NeedsCheck(row,c,now));Check(BasilProgress.NeedsCheck(row with{UnitPrice=101},c,now));Check(BasilProgress.NeedsCheck(row with{Quantity=3},c,now));Check(BasilProgress.NeedsCheck(row with{ListingId="new"},c,now));Check(BasilProgress.NeedsCheck(row,c,now.AddDays(1)));}));
await Test("Basil failed detail requests remain pending rather than completed",()=>Sync(()=>{var row=BasilQueueRows()[0];var c=new SourceConfig();BasilProgress.Complete(row,c,"Retry",now);Check(BasilProgress.NeedsCheck(row,c,now)&&BasilProgress.Summary([row],c,now).Contains("1 left to check"));}));
await Test("Basil queue outcomes and discovery progress survive saved settings",()=>Sync(()=>{var c=new SourceConfig(){Id="BasilMarket",BasilPages=["/market/sell?type=etc"]};c.BasilVisited[c.BasilPages[0]]=now;BasilProgress.Complete(BasilQueueRows()[0],c,"Rounded",now);var copy=JsonSerializer.Deserialize<SourceConfig>(JsonSerializer.Serialize(c,Json.Options),Json.Options)!;Check(!BasilProgress.NeedsCheck(BasilQueueRows()[0],copy,now)&&BasilProgress.Summary([BasilQueueRows()[0]],copy,now).Contains("1/1 category pages"));}));
await Test("Basil progress separates exact rounded pending and unknown site total",()=>Sync(()=>{var r=BasilQueueRows();var c=new SourceConfig();BasilProgress.Complete(r[0],c,"Rounded",now);var summary=BasilProgress.Summary([r[0],r[1],r[2] with{PriceApproximate=false},r[0]],c,now);Check(summary.Contains("1 left to check")&&summary.Contains("1 exact")&&summary.Contains("1 checked, still rounded")&&summary.Contains("3 discovered")&&summary.Contains("website total unknown"));}));


SourceConfig EtaConfig()=>new(){Id="BasilMarket",Enabled=true,PermissionReference="test",BasilPages=["/market/sell?type=etc"]};
await Test("Basil ETA accounts for next refresh and multiple batches",()=>Sync(()=>{var c=EtaConfig();c.NextAllowed=now.AddMinutes(10);var e=BasilEta.Estimate(BasilQueueRows(),c,now);Check(e.QueueFinish==now.AddMinutes(55).AddSeconds(45)&&e.DiscoveryFinish==now.AddMinutes(10).AddSeconds(45));}));
await Test("Basil ETA respects backoff page cache and hourly budget",()=>Sync(()=>{var c=EtaConfig();c.BlockedUntil=now.AddMinutes(20);c.PageChecked["/market/listing/1"]=now.AddMinutes(10);c.RequestTimes=Enumerable.Repeat(now,60).ToList();var e=BasilEta.Estimate(BasilQueueRows().Take(1),c,now);Check(e.QueueFinish>=now.AddHours(1)&&e.QueueFinish<now.AddHours(1).AddMinutes(2));}));
await Test("Basil ETA does not promise completion while offline disabled busy or failed",()=>Sync(()=>{var c=EtaConfig();Check(BasilEta.Estimate(BasilQueueRows(),c,now,true).QueueFinish is null);c.Enabled=false;Check(BasilEta.Estimate(BasilQueueRows(),c,now).Status.StartsWith("Paused"));c.Enabled=true;Check(BasilEta.Estimate(BasilQueueRows(),c,now,false,true).QueueFinish is null);BasilProgress.Complete(BasilQueueRows()[0],c,"Retry",now);Check(BasilEta.Estimate(BasilQueueRows(),c,now).QueueFinish is null);}));
await Test("Basil ETA distinguishes complete known queue from undiscovered website",()=>Sync(()=>{var c=EtaConfig();c.BasilPages=[];var e=BasilEta.Estimate([],c,now);Check(e.QueueFinish==now&&e.DiscoveryFinish is null&&BasilEta.Display(e,now).Contains("category pass: unknown"));}));
await Test("Basil ETA honors changed interval and completed rounded checks",()=>Sync(()=>{var c=EtaConfig();c.IntervalMinutes=30;var rows=BasilQueueRows().Take(9).ToList();Check(BasilEta.Estimate(rows,c,now).QueueFinish==now.AddMinutes(30).AddSeconds(45));foreach(var r in rows)BasilProgress.Complete(r,c,"Rounded",now);Check(BasilEta.Estimate(rows,c,now).QueueFinish==now);}));


SourceConfig CompletedBasilConfig(){var c=EtaConfig();c.BasilVisited[c.BasilPages[0]]=now;return c;}
await Test("Basil completion waits for category discovery and pending detail checks",()=>Sync(()=>{var c=EtaConfig();var row=BasilQueueRows()[0];Check(!BasilCompletion.IsComplete([row with{PriceApproximate=false}],c,now));c.BasilVisited[c.BasilPages[0]]=now;Check(!BasilCompletion.IsComplete([row],c,now));BasilProgress.Complete(row,c,"Retry",now);Check(!BasilCompletion.IsComplete([row],c,now));BasilProgress.Complete(row,c,"Rounded",now);Check(BasilCompletion.IsComplete([row],c,now));Check(!BasilCompletion.IsComplete([],c,now));}));
await Test("Basil completion persists one event across restart and repeated checks",()=>Sync(()=>{var c=CompletedBasilConfig();var rows=new[]{BasilQueueRows()[0] with{PriceApproximate=false}};BasilCompletion.Observe(rows,c,now);var key=c.BasilCompletionKey;Check(key.StartsWith("basil-complete:"));c=JsonSerializer.Deserialize<SourceConfig>(JsonSerializer.Serialize(c,Json.Options),Json.Options)!;BasilCompletion.Observe(rows,c,now);Check(c.BasilCompletionKey==key&&c.BasilWasComplete);}));
await Test("Basil new work rearms completion only after it is checked",()=>Sync(()=>{var c=CompletedBasilConfig();var row=BasilQueueRows()[0];BasilCompletion.Observe([row with{PriceApproximate=false}],c,now);var old=c.BasilCompletionKey;BasilCompletion.Observe([row],c,now);Check(!c.BasilWasComplete);BasilProgress.Complete(row,c,"Rounded",now);BasilCompletion.Observe([row],c,now);Check(c.BasilWasComplete&&old!=c.BasilCompletionKey);}));
await Test("Basil deferred completion delivery rechecks setting source and queue",()=>Sync(()=>{var c=CompletedBasilConfig();var row=BasilQueueRows()[0] with{PriceApproximate=false};BasilCompletion.Observe([row],c,now);Check(BasilCompletion.CanDeliver(c.BasilCompletionKey,[row],c,now,true));Check(!BasilCompletion.CanDeliver(c.BasilCompletionKey,[row],c,now,false));Check(!BasilCompletion.CanDeliver(c.BasilCompletionKey,[row with{PriceApproximate=true}],c,now,true));c.Enabled=false;Check(!BasilCompletion.CanDeliver(c.BasilCompletionKey,[row],c,now,true));}));
await Test("Basil stale category scan cannot announce completion",()=>Sync(()=>{var c=CompletedBasilConfig();Check(!BasilCompletion.IsComplete([BasilQueueRows()[0] with{PriceApproximate=false}],c,now.AddDays(1)));Check(new Settings().BasilCompletionNotifications);}));

await Test("Website lookup encodes names without altering URL parameters",()=>Sync(()=>{var site=WebsiteDirectory.All.Single(s=>s.Name=="BasilMarket");var link=WebsiteLookup.Create(site,"Pig's Head & q=#? 雪");Check(new Uri(link.Url).Query=="?q="+Uri.EscapeDataString("Pig's Head & q=#? 雪"));}));
await Test("Nia lookup never reuses another provider item ID",()=>Sync(()=>{var site=WebsiteDirectory.All.Single(s=>s.Name=="NiaMeowDB");var link=WebsiteLookup.Create(site,"Blue Mushroom Cap","https://kerning.gg/item/999-wrong");Check(link.SearchNia&&link.Url=="https://meowdb.com/msclassic/free-market");}));
await Test("Website links reject unsafe schemes and credentials",()=>Sync(()=>{foreach(var url in new[]{"file:///C:/test","javascript:alert(1)","http://meowdb.com","https://user:pass@meowdb.com","https://localhost/"})Check(!WebsiteLookup.SafeUrl(url));Check(WebsiteLookup.SafeUrl("https://meowdb.com/msclassic"));}));
await Test("Website lookup uses matching source host only",()=>Sync(()=>{var site=WebsiteDirectory.All.Single(s=>s.Name=="Kerning Exchange");Check(WebsiteLookup.Create(site,"Item","https://kerning.gg/listing/12").SourceUrl!.EndsWith("/listing/12"));Check(WebsiteLookup.Create(site,"Item","https://kerning.gg.evil.test/listing/12").Url==site.Url);}));
await Test("Unknown item routes stay on verified website destination",()=>Sync(()=>{foreach(var site in WebsiteDirectory.All){var link=WebsiteLookup.Create(site,"");Check(WebsiteLookup.SafeUrl(link.Url)&&!link.SearchNia);}}));

await Test("Eight websites expose exact item lookup with FM routed to Kerning",()=>Sync(()=>{Check(WebsiteDirectory.All.Count(s=>WebsiteLookup.ExactLookup(WebsiteLookup.ModeFor(s.Name)))==9);var fm=WebsiteLookup.Create(WebsiteDirectory.All.Single(s=>s.Name=="Kerning FM Tracker"),"Blue Mushroom Cap");Check(fm.Url=="https://kerning.gg/"&&fm.Mode==WebsiteSearchMode.Kerning);}));
await Test("Item lookup search pages encode names without invented IDs",()=>Sync(()=>{foreach(var name in new[]{"MapleBuddy","GameMargins"}){var p=WebsiteLookup.Create(WebsiteDirectory.All.Single(s=>s.Name==name),"Pig's Head & q=#?");Check(p.Url.Contains(Uri.EscapeDataString("Pig's Head & q=#?")));}}));
await Test("Lookup stops on external hosts and unrelated paths",()=>Sync(()=>{var p=WebsiteLookup.Create(WebsiteDirectory.All.Single(s=>s.Name=="MESOLANE"),"Item");Check(p.AtSearchPage("https://mesolane.gg/?test=1"));Check(!p.AtSearchPage("https://mesolane.gg.evil.test/")&&!p.AtSearchPage("https://mesolane.gg/login"));}));
await Test("Unsupported websites keep honest search capabilities",()=>Sync(()=>{Check(!WebsiteLookup.ExactLookup(WebsiteLookup.ModeFor("Sleepywood")));Check(!WebsiteLookup.ExactLookup(WebsiteLookup.ModeFor("Meso Scout")));Check(WebsiteLookup.ModeFor("MapleClassicBase")==WebsiteSearchMode.None);Check(WebsiteLookup.Create(WebsiteDirectory.All[0],"").Mode==WebsiteSearchMode.None);}));

await Test("Ad blocker matches exact hosts, subdomains, case and trailing dots",()=>Sync(()=>{foreach(var url in new[]{"https://doubleclick.net/ad","https://a.b.doubleclick.net/ad","https://PAGEAD2.GOOGLESYNDICATION.COM./pagead.js","http://ads.pubmatic.com/ad"})Check(WebsiteAdPolicy.IsAdvertisingUrl(url));}));
await Test("Ad blocker preserves item pages, images, CAPTCHA and host lookalikes",()=>Sync(()=>{foreach(var url in new[]{"https://niameowdb.com/msclassic/item-db/363","https://basilmarket.net/market/sell","https://maplestory.io/api/GMS/83/item/4000011/icon","https://challenges.cloudflare.com/test","https://www.google.com/recaptcha/api.js","https://notdoubleclick.net/ad","https://doubleclick.net.example.com/ad","https://example.com/?next=doubleclick.net","https://doubleclick.net@example.com/ad","data:text/plain,doubleclick.net","invalid"})Check(!WebsiteAdPolicy.IsAdvertisingUrl(url));}));

await Test("Drop reference exact names, ambiguous identities and historical evidence",()=>Sync(()=>{const string index="<a href='/msclassic/item-db/1'>Snail Shell</a><a href='/msclassic/item-db/2'>Blue Snail Shell</a>";Check(DropReferenceReader.ResolveItem(index,"Snail Shell")=="https://meowdb.com/msclassic/item-db/1");Check(DropReferenceReader.ResolveItem(index,"Shell") is null);Check(DropReferenceReader.ResolveItem(index+"<a href='/msclassic/item-db/3'>Snail Shell</a>","Snail Shell") is null);var rows=DropReferenceReader.ReferenceMonsters("<h1>Snail Shell</h1><div><div><h2>MSEA Reference Drops</h2></div><a href='/msclassic/monsters/2'>Snail</a></div>","Snail Shell");Check(rows.Count==1&&rows[0].Id==2&&rows[0].Evidence.Contains("Historical"));bool rejected=false;try{DropReferenceReader.ReferenceMonsters("<h1>Blue Snail Shell</h1>","Snail Shell");}catch(FormatException){rejected=true;}Check(rejected);}));
await Test("Drop locations are bounded and missing maps stay unknown",()=>Sync(()=>{var monster=DropReferenceReader.ReadMonster("<h1>Snail</h1><div id='locations'><a href='/msclassic/maps/100'>Map A</a><a href='https://elsewhere.test'>Unsafe</a></div>","https://meowdb.com/msclassic/monsters/2","Community report");Check(monster.Name=="Snail"&&monster.Locations.Count==1&&monster.Locations[0].Name=="Map A");Check(DropReferenceReader.ReadMonster("<h1>Snail</h1>",monster.Url,"Historical").Locations.Count==0);}));
await Test("Finder hides untranslated names from catalog and market-only results",()=>Sync(()=>{var catalog=new[]{new FinderIdentity("1012139","달래 스티커"),new FinderIdentity("2000000","Red Potion")};var rows=new[]{O("foreign") with{Name="GMS 콘라드",Kind=EvidenceKind.Asking},O("english") with{Name="Red Potion",ItemId="2000000",Kind=EvidenceKind.Asking}};var result=ItemFinder.Search(catalog,rows,"");Check(result.Count==1&&result[0].Id=="2000000"&&result[0].Listings.Count==1);Check(ItemFinder.Search(catalog,rows,"1012139").Count==0);Check(ItemFinder.HasEnglishDisplayName("Maple's Sword +1")&&ItemFinder.HasEnglishDisplayName("Café Chair")&&!ItemFinder.HasEnglishDisplayName("帽子")&&!ItemFinder.HasEnglishDisplayName(""));}));
await Test("Finder excludes removed, buying and sold evidence",()=>Sync(()=>{var rows=new[]{O("ask") with{Kind=EvidenceKind.Asking},O("removed") with{Kind=EvidenceKind.Asking,Available=false},O("buy") with{Kind=EvidenceKind.Buying},O("sale")};var found=ItemFinder.Search([new("4000000","Test ETC")],rows,"test");Check(found.Single().Listings.Count==1&&found[0].Listings[0].ListingId=="ask");}));
await Test("Finder preserves ambiguous identities and exact variants",()=>Sync(()=>{var rows=new[]{O("one") with{Kind=EvidenceKind.Asking,ItemId="1000000",Equipment=true,Variant="watk=10"},O("two") with{Kind=EvidenceKind.Asking,ItemId="1000001",Equipment=true,Variant="watk=20"}};var found=ItemFinder.Search([new("1000000","Test ETC"),new("1000001","Test ETC")],rows,"test");Check(found.Count==2&&found.All(i=>i.Listings.Count==1)&&found[0].Listings[0].Variant=="watk=10"&&found[1].Listings[0].Variant=="watk=20");}));
await Test("Finder searches IDs, unknown market items and source/category filters",()=>Sync(()=>{var catalog=new[]{new FinderIdentity("2000000","Red Potion"),new FinderIdentity("1000000","Sword")};var rows=new[]{O("unknown") with{Kind=EvidenceKind.Asking}};Check(ItemFinder.Search(catalog,rows,"2000000").Single().Name=="Red Potion");Check(ItemFinder.Search(catalog,rows,"test","Unknown",listedOnly:true).Single().Listings.Count==1);Check(ItemFinder.Search(catalog,rows,"test",provider:"Other",listedOnly:true).Count==0);Check(ItemFinder.Search(catalog,rows,"","Equipment").Single().Name=="Sword");Check(ItemFinder.Search(catalog,rows,"absent").Count==0);}));
await Test("CCPLZ exact identity and historical evidence",()=>Sync(()=>{
 Check(CcplzReader.ItemUrl("Snail Shell")=="https://ccplz.cc/items/snail-shell");
 Check(CcplzReader.MatchesItem("<h1>Snail Shell</h1>","Snail Shell"));
 Check(!CcplzReader.MatchesItem("<h1>Blue Snail Shell</h1>","Snail Shell"));
 var refs=CcplzReader.ReadReferences("<h1>Snail Shell</h1><section id='dropped-by'><a href='/monsters/snail'>Snail</a></section><section id='older-dropped-by'><a href='/monsters/mano'>Mano</a></section>","Snail Shell");
 Check(refs.Count==2&&refs[1].Evidence.Contains("Older-version"));
 var monster=CcplzReader.ReadMaps("<h1>Snail</h1><section id='maps'><a href='/maps/one'>Map A</a><a href='/maps/two'>Map B</a><a href='/maps/three'>Map C</a><a href='/maps/four'>Map D</a><a href='https://example.org/'>External</a></section>",refs[0]);
 Check(monster.Locations.Count==3&&monster.Locations[0].Url=="https://ccplz.cc/maps/one");
 Check(CcplzReader.ReadMaps("<h1>Snail</h1>",refs[0]).Locations.Count==0);
 bool rejected=false;try{CcplzReader.ReadReferences("<h1>Blue Snail Shell</h1>","Snail Shell");}catch(FormatException){rejected=true;}Check(rejected);
}));
await Test("Buy requests preserve notes and expire with item snapshot",()=>Sync(()=>{var path=Path.Combine("work","transfer-tests",Guid.NewGuid()+".db");try{using var db=new Store(path);db.Save([O("buyer",500,EvidenceKind.Buying) with{ItemId="A",Available=true,Note="CH 2 FM 3"},O("otherbuyer",300,EvidenceKind.Buying) with{ItemId="B",Available=true}]);Check(db.Observations().Single(o=>o.ItemId=="A").Note=="CH 2 FM 3");db.ExpireMissingFromSnapshot("Test only",[],["A"]);Check(!db.Observations().Single(o=>o.ItemId=="A").Available&&db.Observations().Single(o=>o.ItemId=="B").Available);}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}}));
await Test("NPC reference distinguishes sell-back and shop purchase prices",()=>Sync(()=>{
 string html="<h1>Quest Potion</h1><dl class='server-facts'><dt>NPC sell price</dt><dd>5 mesos</dd></dl><script id='ccplz-entity'>"+"""
 {"item":{"client_id":"2000000","graph":{"incoming":[{"canonical_relation_type":"sells_item","target":{"entity_type":"shop"},"qualifiers":{"currency":"Mesos","price":50}},{"canonical_relation_type":"sells_item","target":{"entity_type":"shop"},"qualifiers":{"currency":"Mesos","price":60}},{"canonical_relation_type":"sells_item","target":{"entity_type":"shop"},"qualifiers":{"currency":"Cash","price":1}}]}}}
 """+"</script>";
 var npc=CcplzReader.ReadNpcPrices(html,"Red Potion","2000000",now);Check(npc.Sell==5&&npc.BuyMin==50&&npc.BuyMax==60);
 Throws(()=>CcplzReader.ReadNpcPrices(html,"Red Potion","2000001",now));Throws(()=>CcplzReader.ReadNpcPrices(html,"Red Potion","",now));
 var missing=CcplzReader.ReadNpcPrices("<h1>Stone</h1><script id='ccplz-entity'>{\"item\":{\"client_id\":\"1\"}}</script>","Stone","1",now);Check(missing.Sell is null&&missing.BuyMin is null);
}));
await Test("Related drop items rank relevant English references without replacing exact identity",()=>Sync(()=>{
 const string html="<a href='/msclassic/item-db/1'>Snail Shell</a><a href='/msclassic/item-db/2'>Blue Snail Shell</a><a href='/msclassic/item-db/3'>Red Potion</a><a href='https://evil.test'>Snail Shell</a><a href='/msclassic/item-db/4'>달팽이 Shell</a>";
 Check(DropReferenceReader.RelatedItems(html,"snail shel").First().Name=="Snail Shell");
 Check(DropReferenceReader.RelatedItems(html,"Shell").Count==2);Check(DropReferenceReader.RelatedItems(html,"Sword").Count==0);
 Check(DropReferenceReader.RelatedItems(html,"Snail-Shell").First().Name=="Snail Shell");Check(DropReferenceReader.ResolveItem(html,"snail shel") is null);
 Check(DropReferenceReader.RelatedItems(html,"Snail Shell").All(i=>i.Url.StartsWith("https://meowdb.com/msclassic/item-db/")));
}));
await Test("All item names accept trailing level metadata and retain both drop evidence types",()=>Sync(()=>{
 foreach(var name in new[]{"Blue Moonlight","Red Moonlight","Snail Shell","Mystic Scroll"}){
  string index=$"<a href='/msclassic/item-db/1'>{name} (Lv 40)</a>";Check(DropReferenceReader.ResolveItem(index,name)=="https://meowdb.com/msclassic/item-db/1");
  Check(DropReferenceReader.ResolveItem(index+$"<a href='/msclassic/item-db/2'>{name} (Lv 50)</a>",name) is null);
 }
 const string html="<h1>Blue Moonlight</h1><section><header><h2>Dropped By</h2></header><a href='/msclassic/monsters/38'>Zombie Lupin</a></section><section><header><h2>MSEA Reference Drops</h2></header><a href='/msclassic/monsters/38'>Zombie Lupin</a><a href='/msclassic/monsters/10'>Toy Trojan</a></section>";
 var rows=DropReferenceReader.MergeDropEvidence(DropReferenceReader.CommunityMonsters(html,"Blue Moonlight").Concat(DropReferenceReader.ReferenceMonsters(html,"Blue Moonlight")));
 Check(rows.Count==2&&rows.Single(r=>r.Id==38).Evidence.Contains("Dropped By")&&rows.Single(r=>r.Id==38).Evidence.Contains("MSEA Reference Drops"));
 Throws(()=>DropReferenceReader.ReferenceMonsters(html,"Red Moonlight"));
}));
await Test("Drop fallback handles failure, empty results, deadline and user cancellation",async()=>{
 var empty=new ItemDrops("Shell","","",[],now);var found=empty with{Monsters=[new("Snail","https://ccplz.cc/monsters/snail","Historical",[])]};int calls=0;
 Task<ItemDrops> Backup(string n,CancellationToken c){calls++;return Task.FromResult(found);}
 var result=await DropFallback.Lookup("Shell",(_,_)=>throw new HttpRequestException(),Backup,CancellationToken.None);Check(result.Source=="CCPLZ"&&calls==1);
 result=await DropFallback.Lookup("Shell",(_,_)=>Task.FromResult(empty),Backup,CancellationToken.None);Check(result.Source=="CCPLZ"&&calls==2);
 result=await DropFallback.Lookup("Shell",(_,_)=>Task.FromResult(found),Backup,CancellationToken.None);Check(result.Source=="NiaMeowDB"&&calls==2);
 result=await DropFallback.Lookup("Shell",async(_,c)=>{await Task.Delay(10000,c);return empty;},Backup,CancellationToken.None,primaryTimeout:TimeSpan.FromMilliseconds(20));Check(result.Source=="CCPLZ"&&calls==3);
 using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{await DropFallback.Lookup("Shell",(_,c)=>Task.FromCanceled<ItemDrops>(c),Backup,cancel.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&calls==3);
 result=await DropFallback.Lookup("Shell",(_,_)=>throw new HttpRequestException(),(_,_)=>throw new HttpRequestException(),CancellationToken.None);Check(result.Drops.Monsters.Count==0&&result.Notice.Contains("also could not load"));
});
await Test("Quest uses source name lookup, rejects ambiguity and preserves historical evidence",()=>Sync(()=>{
 const string index="<a href='/items/1051019/Blue-Moonlight'>Blue Moonlight</a><a href='/items/1051025/Dark-Calaf'>Dark Calaf</a>";
 Check(QuestDropReader.ResolveItem(index,"Blue Moonlight")=="https://maplestory.quest/items/1051019/Blue-Moonlight");Check(QuestDropReader.ResolveItem(index+"<a href='/items/2/Blue-Moonlight'>Blue Moonlight</a>","Blue Moonlight") is null);
 const string html="<h1>Wooden Club</h1><section><header><h3>Dropped by</h3><span>an older version of the game, rates differ</span></header><a href='/monsters/6/Stump'><span class='truncate'>Stump</span></a></section><a href='/monsters/7/Slime'>Unrelated</a>";
 var monster=QuestDropReader.ReadReferences(html,"Wooden Club").Single();Check(monster.Name=="Stump"&&monster.Evidence.Contains("Historical"));Throws(()=>QuestDropReader.ReadReferences(html,"Sword"));
 var maps=QuestDropReader.ReadMaps("<h1>Stump</h1><section><header><h2>Where it spawns (1)</h2></header><a href='/maps/1/Forest'>Forest</a><a href='https://evil.test/maps/2'>Unsafe</a></section>",monster);Check(maps.Locations.Single().Name=="Forest");Throws(()=>QuestDropReader.ReadMaps("<h1>Slime</h1>",monster));
}));
await Test("Quest NPC prices distinguish sell-back, purchase and missing values",()=>Sync(()=>{
 string Card(string label,string value)=>$"<div><div><div><span class='stat-tag'>{label}</span></div></div><div class='num text-lg'>{value}</div></div>";
 string html="<h1>Cap</h1>"+Card("NPC pays","8")+Card("NPC charges","1,200");var p=QuestDropReader.ReadNpcPrices(html,"Cap","https://maplestory.quest/items/1/Cap",now);Check(p.Sell==8&&p.BuyMin==1200&&p.Source=="MapleStory Quest");
 var empty=QuestDropReader.ReadNpcPrices("<h1>Cap</h1>"+Card("NPC pays","-"),"Cap",p.Url,now);Check(empty.Sell is null&&empty.BuyMin is null);Throws(()=>QuestDropReader.ReadNpcPrices(html,"Sword",p.Url,now));
}));
await Test("NPC fallback handles identity failure and empty source without swallowing cancellation",async()=>{
 var empty=new NpcPrices("Cap","",null,null,null,now);var found=empty with{Sell=8,Source="MapleStory Quest"};int calls=0;
 Task<NpcPrices> Fallback(CancellationToken ct){calls++;return Task.FromResult(found);}
 var p=await NpcPriceFallback.Lookup(_=>throw new FormatException("ID mismatch"),Fallback,CancellationToken.None);Check(p.Sell==8&&calls==1);
 p=await NpcPriceFallback.Lookup(_=>Task.FromResult(empty),Fallback,CancellationToken.None);Check(p.Sell==8&&calls==2);
 p=await NpcPriceFallback.Lookup(_=>Task.FromResult(found),Fallback,CancellationToken.None);Check(p.Sell==8&&calls==2);
 using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{await NpcPriceFallback.Lookup(c=>Task.FromCanceled<NpcPrices>(c),Fallback,cancel.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&calls==2);
});
Directory.CreateDirectory("test-results");File.WriteAllText("test-results/results.json",JsonSerializer.Serialize(new{total=results.Count,failed=failures,results},Json.Options));Console.WriteLine($"{results.Count-failures}/{results.Count} passed");Environment.ExitCode=failures==0?0:1;



sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> fn):HttpMessageHandler

{

    public int Calls{get;private set;}

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;return Task.FromResult(fn(request));}

}
