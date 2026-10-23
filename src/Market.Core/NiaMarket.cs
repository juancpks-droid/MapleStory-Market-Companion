using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Market.Core;
public sealed record NiaOffer(long Id,string Side,decimal? Price,decimal? Quantity,string Seller,string Condition,string Stats,string Location,string Note,DateTimeOffset? At);
public sealed record NiaMarketResult(string Name,string Url,decimal? Usual,decimal? Lowest,decimal? BestBuy,DateTimeOffset? Newest,IReadOnlyList<NiaOffer> Offers,bool Partial,DateTimeOffset CheckedAt);
public sealed class NiaMarketClient:IDisposable{
 const string Host="https://meowdb.com";
 readonly HttpClient client=new(new HttpClientHandler{AutomaticDecompression=DecompressionMethods.All,AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(20)};
 readonly SemaphoreSlim gate=new(1,1);DateTimeOffset next,indexAt,hour;int count;string? index,robots;
 public NiaMarketClient(){client.DefaultRequestHeaders.UserAgent.ParseAdd("MapleMarketCompanion/1.35.0 (on-demand item reference)");}
 static string Key(string s)=>Regex.Replace(s,@"\s*\(Lv\.?\s*\d+\)\s*$","",RegexOptions.IgnoreCase).Trim().Replace('’','\'').ToLowerInvariant();
 public static string Resolve(string html,string name){var links=PublicPages.Document(html).DocumentNode.SelectNodes("//a[@href]");var paths=links?.Where(a=>Regex.IsMatch(a.GetAttributeValue("href",""),@"^/msclassic/item-db/\d+$")&&Key(PublicPages.Text(a))==Key(name)).Select(a=>a.GetAttributeValue("href","")).Distinct().ToArray()??[];return paths.Length==1?Host+paths[0]:throw new FormatException("No unique NiaMeowDB item match");}
 public static NiaMarketResult Parse(string summary,string listings,int id,string name,string url,DateTimeOffset now){
  using var a=JsonDocument.Parse(summary);using var b=JsonDocument.Parse(listings);var s=a.RootElement;
  string Text(JsonElement e,string key)=>e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
  decimal? Amount(JsonElement e,string key)=>e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetDecimal(out var n)&&n>0&&n==decimal.Truncate(n)&&n<=9007199254740991?n:null;
  DateTimeOffset? Date(string raw)=>DateTimeOffset.TryParse(raw,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeUniversal,out var d)&&d<=now.AddMinutes(2)?d:null;
  if(s.GetProperty("itemId").GetInt32()!=id||Text(s,"condition")!="clean")throw new FormatException("NiaMeowDB summary item mismatch");
  var rows=b.RootElement.GetProperty("listings");if(rows.ValueKind!=JsonValueKind.Array)throw new FormatException("NiaMeowDB listings unavailable");
  var offers=new List<NiaOffer>();foreach(var r in rows.EnumerateArray()){
   if(Text(r,"status")!="active"||Text(r,"side") is not ("sell" or "buy")||Amount(r,"id") is not {} rid)continue;
   string stats=r.TryGetProperty("stats",out var st)&&st.ValueKind==JsonValueKind.Object?string.Join(" · ",st.EnumerateObject().Where(p=>p.Value.ValueKind==JsonValueKind.Number).Select(p=>p.Name+": "+p.Value)):"";
   string location=string.Join(" · ",new[]{Amount(r,"channel") is {} ch?"CH "+ch:"",r.TryGetProperty("fmRoom",out var fm)&&fm.ValueKind==JsonValueKind.Number?"FM "+fm:"",Text(r,"location")}.Where(x=>x.Length>0));
   string seller=Text(r,"ign");if(seller.Length==0)seller=Text(r,"username");
   offers.Add(new((long)rid,Text(r,"side"),Amount(r,"price"),Amount(r,"quantity"),seller,Text(r,"condition"),stats,location,Text(r,"note"),Date(Text(r,"updatedAt"))));if(offers.Count==60)break;
  }
  return new(name,url,Amount(s,"usual"),Amount(s,"cheapestSell"),Amount(s,"bestBuy"),Date(Text(s,"newestPriceAt")),offers,rows.GetArrayLength()>60||b.RootElement.TryGetProperty("hasMoreActive",out var more)&&more.ValueKind==JsonValueKind.True,now);
 }
 async Task<string> Get(string url,CancellationToken ct){if(robots is not null&&!KerningPages.RobotsAllows(robots,new Uri(url).AbsolutePath))throw new HttpRequestException("Source lookup unavailable");if(DateTimeOffset.UtcNow-hour>TimeSpan.FromHours(1)){count=0;hour=DateTimeOffset.UtcNow;}if(count>=60||next>DateTimeOffset.UtcNow.AddSeconds(20))throw new HttpRequestException("NiaMeowDB busy; retry later");if(next>DateTimeOffset.UtcNow)await Task.Delay(next-DateTimeOffset.UtcNow,ct);next=DateTimeOffset.UtcNow.AddSeconds(3);count++;
  using var r=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct);if((int)r.StatusCode is 429 or 503)next=DateTimeOffset.UtcNow.AddMinutes(15);r.EnsureSuccessStatusCode();if((int)r.StatusCode>=300)throw new HttpRequestException("Source moved");using var stream=await r.Content.ReadAsStreamAsync(ct);using var buffer=new MemoryStream();var bytes=new byte[8192];int n;while((n=await stream.ReadAsync(bytes,ct))>0){if(buffer.Length+n>4000000)throw new IOException("Source response too large");buffer.Write(bytes,0,n);}return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
 }
 public async Task<NiaMarketResult?> Lookup(string directory,string name,bool offline,CancellationToken ct){
  name=ScrollNames.Query(name);if(name.Length<3||name.Length>150)return null;
  var folder=Path.Combine(directory,"nia-market");Directory.CreateDirectory(folder);var path=Path.Combine(folder,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Key(name))))+".json");
  await gate.WaitAsync(ct);try{NiaMarketResult? saved=null;try{saved=JsonSerializer.Deserialize<NiaMarketResult>(File.ReadAllText(path));if(saved?.Name!=name)saved=null;}catch{}
   if(offline||saved?.CheckedAt>DateTimeOffset.UtcNow.AddMinutes(-15))return saved;
   try{if(index is null||indexAt<DateTimeOffset.UtcNow.AddHours(-1)){robots=await Get(Host+"/robots.txt",ct);index=await Get(Host+"/msclassic/item-db/all",ct);indexAt=DateTimeOffset.UtcNow;}
    var url=Resolve(index,name);var html=await Get(url,ct);if(Key(PublicPages.Text(PublicPages.Document(html).DocumentNode.SelectSingleNode("//h1")))!=Key(name))throw new FormatException("Item identity changed");int id=int.Parse(url.Split('/').Last());
    var a=await Get(Host+$"/msclassic/api/item-market-summary?itemId={id}&condition=clean",ct);var b=await Get(Host+$"/msclassic/api/market-listings?itemId={id}&offers=1",ct);var result=Parse(a,b,id,name,url,DateTimeOffset.UtcNow);File.WriteAllText(path,JsonSerializer.Serialize(result));return result;
   }catch(Exception e) when(e is HttpRequestException or IOException or JsonException or FormatException or KeyNotFoundException or OperationCanceledException){ct.ThrowIfCancellationRequested();return saved;}
  }finally{gate.Release();}
 }
 public void Dispose()=>client.Dispose();
}
