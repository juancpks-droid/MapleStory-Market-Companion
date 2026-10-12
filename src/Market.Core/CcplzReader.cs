using System.Net;
using System.Text.RegularExpressions;
namespace Market.Core;

// On-demand facts only: no search crawling or bundled copy of the source database.
public sealed partial class CcplzReader : IDisposable
{
 const string Host="https://ccplz.cc";
 readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(25)};
 readonly SemaphoreSlim gate=new(1,1);
 readonly Dictionary<string,(string Html,DateTimeOffset At)> pages=[];
 readonly Dictionary<string,ItemDrops> cache=[];
 readonly Queue<DateTimeOffset> requests=[];
 string? robots;DateTimeOffset robotsAt,nextRequest;
 public CcplzReader(){client.DefaultRequestHeaders.UserAgent.ParseAdd("MapleMarketCompanion/1.20.11 (on-demand item reference)");}
 static string Key(string text)=>Regex.Replace(text.Normalize().Replace('’','\''),@"\s+"," ").Trim().ToLowerInvariant();
 public static string ItemUrl(string name)=>Host+"/items/"+Regex.Replace(Key(name),@"[^a-z0-9]+","-").Trim('-');
 public static bool MatchesItem(string html,string name)=>Key(PublicPages.Text(PublicPages.Document(html).DocumentNode.SelectSingleNode("//h1")))==Key(name);
 public static IReadOnlyList<DropMonster> ReadReferences(string html,string name){
  if(!MatchesItem(html,name))throw new FormatException("No exact item match.");
  var root=PublicPages.Document(html).DocumentNode;var result=new List<DropMonster>();
  foreach(var section in new[]{"dropped-by","older-dropped-by"}){
   foreach(var a in root.SelectSingleNode($"//*[@id='{section}']")?.SelectNodes(".//a[@href]")?.ToArray()??[]){
    var path=a.GetAttributeValue("href","");if(!Regex.IsMatch(path,@"^/monsters/[a-z0-9-]+$"))continue;
    result.Add(new(PublicPages.Text(a),Host+path,section=="older-dropped-by"?"CCPLZ • Older-version reference":"CCPLZ • Reported drop; historical rates, not verified for your server",[]));
   }
  }
  return result.DistinctBy(m=>m.Url).ToArray();
 }
 public static DropMonster ReadMaps(string html,DropMonster monster){
  var root=PublicPages.Document(html).DocumentNode;
  if(Key(PublicPages.Text(root.SelectSingleNode("//h1")))!=Key(monster.Name))throw new FormatException("Monster name mismatch.");
  var maps=(root.SelectSingleNode("//*[@id='maps']")?.SelectNodes(".//a[@href]")?.ToArray()??[])
   .Where(a=>Regex.IsMatch(a.GetAttributeValue("href",""),@"^/maps/[a-z0-9-]+$"))
   .Select(a=>new DropLocation(PublicPages.Text(a),Host+a.GetAttributeValue("href",""))).Distinct().Take(3).ToArray();
  return monster with{Locations=maps};
 }
 async Task<string> Get(string url,CancellationToken ct){
  if(robots is not null&&url!=Host+"/robots.txt"&&!KerningPages.RobotsAllows(robots,new Uri(url).AbsolutePath))throw new HttpRequestException("Source does not allow this lookup.");
  if(pages.TryGetValue(url,out var page)&&page.At>DateTimeOffset.UtcNow.AddHours(-1))return page.Html;
  var now=DateTimeOffset.UtcNow;while(requests.TryPeek(out var first)&&first<now.AddHours(-1))requests.Dequeue();
  if(requests.Count>=60||nextRequest>now.AddMinutes(1))throw new HttpRequestException("Reference busy; retry later.");
  if(nextRequest>now)await Task.Delay(nextRequest-now,ct);
  requests.Enqueue(DateTimeOffset.UtcNow);nextRequest=DateTimeOffset.UtcNow.AddSeconds(3);
  using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct);
  if(response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)nextRequest=DateTimeOffset.UtcNow.AddMinutes(15);
  response.EnsureSuccessStatusCode();if((int)response.StatusCode>=300)throw new HttpRequestException("Source address changed.");
  using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[8192];int count;
  while((count=await input.ReadAsync(buffer,ct))>0){if(output.Length+count>2*1024*1024)throw new IOException("Reference too large.");await output.WriteAsync(buffer.AsMemory(0,count),ct);}
  var html=System.Text.Encoding.UTF8.GetString(output.ToArray());if(pages.Count>=100)pages.Clear();pages[url]=(html,DateTimeOffset.UtcNow);return html;
 }
 async Task<string?> ExactPage(string name,CancellationToken ct){
  if(robots is null||robotsAt<DateTimeOffset.UtcNow.AddHours(-1)){robots=await Get(Host+"/robots.txt",ct);robotsAt=DateTimeOffset.UtcNow;}
  try{var html=await Get(ItemUrl(name),ct);return MatchesItem(html,name)?html:null;}
  catch(HttpRequestException e) when(e.StatusCode==HttpStatusCode.NotFound){return null;}
 }
 public async Task<string?> Resolve(string name,CancellationToken ct){
  await gate.WaitAsync(ct);try{return await ExactPage(name,ct) is null?null:ItemUrl(name);}finally{gate.Release();}
 }
 public async Task<ItemDrops> Lookup(string name,CancellationToken ct){
  await gate.WaitAsync(ct);try{
   if(cache.TryGetValue(Key(name),out var cached)&&cached.CheckedAt>DateTimeOffset.UtcNow.AddHours(-1))return cached;
   var html=await ExactPage(name,ct);
   if(html is null)return new(name,"","No verified exact-name CCPLZ page found. This item may be missing or use a different name.",[],DateTimeOffset.UtcNow);
   var references=ReadReferences(html,name);var monsters=new List<DropMonster>();bool incomplete=false;
   foreach(var monster in references.Take(5)){
    try{monsters.Add(ReadMaps(await Get(monster.Url,ct),monster));}
    catch(Exception e) when(e is HttpRequestException or FormatException){monsters.Add(monster);incomplete=true;}
   }
   var result=new ItemDrops(name,ItemUrl(name),(references.Count==0?"No recorded drops; this does not prove there are none.":$"Showing {monsters.Count} of {references.Count} reference monsters, up to 3 maps each. Reported and older-version data may differ from your server.")+(incomplete?" Some maps could not load.":""),monsters,DateTimeOffset.UtcNow);
   if(cache.Count>=100)cache.Clear();cache[Key(name)]=result;return result;
  }finally{gate.Release();}
 }
 public void Dispose()=>client.Dispose();
}
