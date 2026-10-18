using System.Text.Json;
using System.Globalization;
namespace Market.Core;

public sealed record ClassicPriceDay(DateTimeOffset Date,decimal Median,int Sales);
public sealed record ClassicPriceSummary(string Name,string Slug,decimal Median,int Sales,DateTimeOffset UpdatedAt,ClassicPriceDay[] History)
{
 public const string Source="MapleStory-Classic.com";
 public bool Stale=>DateTimeOffset.UtcNow-UpdatedAt>TimeSpan.FromHours(24);
}
// Summary evidence stays separate from individual listings and valuation/alert inputs.
public static class ClassicMarketFeed
{
 public const string Url="https://maplestory-classic.com/market.json";
 public static ClassicPriceSummary[] Parse(string json,DateTimeOffset now)
 {
  using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
  var updated=root.GetProperty("updatedAt").GetDateTimeOffset();
  if(updated>now.AddMinutes(5)||updated.Year<2020)throw new FormatException("Invalid feed date");
  var items=root.GetProperty("items");if(items.GetArrayLength()>10000)throw new FormatException("Oversized catalog");
  var result=new List<ClassicPriceSummary>();
  foreach(var row in items.EnumerateArray())try{
   var name=row.GetProperty("n").GetString()!;var slug=row.GetProperty("s").GetString()!;
   decimal price=row.GetProperty("p").GetDecimal();int sales=row.GetProperty("sold").GetInt32();
   if(string.IsNullOrWhiteSpace(name)||name.Length>200||!System.Text.RegularExpressions.Regex.IsMatch(slug,"^[a-z0-9-]+$")||price<=0||price>1_000_000_000_000m||sales<1)continue;
   var days=new List<ClassicPriceDay>();
   foreach(var h in row.GetProperty("h").EnumerateArray()){
    if(h.GetArrayLength()!=3)continue;
    if(!DateTimeOffset.TryParseExact(h[0].GetString(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var date))continue;
    var median=h[1].GetDecimal();var count=h[2].GetInt32();
    if(date<=updated&&date.Year>=2020&&median>0&&median<=1_000_000_000_000m&&count>0)days.Add(new(date,median,count));
   }
   if(days.GroupBy(d=>d.Date).Any(g=>g.Count()>1))continue;
   result.Add(new(name,slug,price,sales,updated,days.OrderBy(d=>d.Date).ToArray()));
  }catch(Exception e) when(e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException){ }
  if(result.Count==0)throw new FormatException("No valid prices");return result.ToArray();
 }
 public static ClassicPriceSummary? Match(IEnumerable<ClassicPriceSummary> items,string name){
  var key=TextKey.Search(ScrollNames.Query(name));var matches=items.Where(i=>TextKey.Search(i.Name)==key).Take(2).ToArray();return matches.Length==1?matches[0]:null;
 }
}

public sealed class ClassicMarketClient
{
 readonly SemaphoreSlim gate=new(1,1);static readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(15)};
 ClassicPriceSummary[]? cached;DateTimeOffset checkedAt;
 public async Task<ClassicPriceSummary?> Lookup(string directory,string name,bool offline,CancellationToken ct)
 {
  await gate.WaitAsync(ct);try{
   var path=Path.Combine(directory,"classic-market-feed.json");
   if(cached is null&&File.Exists(path))try{cached=ClassicMarketFeed.Parse(await File.ReadAllTextAsync(path,ct),DateTimeOffset.UtcNow);checkedAt=File.GetLastWriteTimeUtc(path);}catch(Exception e) when(e is not OperationCanceledException){ }
   if(!offline&&DateTimeOffset.UtcNow-checkedAt>TimeSpan.FromHours(1)){
    checkedAt=DateTimeOffset.UtcNow;
    try{
     using var req=new HttpRequestMessage(HttpMethod.Get,ClassicMarketFeed.Url);req.Headers.UserAgent.ParseAdd("MapleMarketCompanion/1.33");
     using var res=await http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);res.EnsureSuccessStatusCode();
     await res.Content.LoadIntoBufferAsync(4_000_000,ct);var text=await res.Content.ReadAsStringAsync(ct);var parsed=ClassicMarketFeed.Parse(text,DateTimeOffset.UtcNow);
     Directory.CreateDirectory(directory);await File.WriteAllTextAsync(path+".tmp",text,ct);File.Move(path+".tmp",path,true);cached=parsed;
    }catch(Exception e) when(e is not OperationCanceledException){/* Preserve the last valid snapshot and back off for an hour. */}
   }
   return ClassicMarketFeed.Match(cached??[],name);
  }finally{gate.Release();}
 }
}
