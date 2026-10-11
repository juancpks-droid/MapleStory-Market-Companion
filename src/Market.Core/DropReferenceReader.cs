using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
namespace Market.Core;
public sealed record DropLocation(string Name,string Url);
public sealed record DropMonster(string Name,string Url,string Evidence,IReadOnlyList<DropLocation> Locations);
public sealed record ItemDrops(string Item,string SourceUrl,string Message,IReadOnlyList<DropMonster> Monsters,DateTimeOffset CheckedAt);
public sealed class DropReferenceReader : IDisposable
{
 const string Host="https://meowdb.com";
 readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(25)};
 readonly SemaphoreSlim gate=new(1,1);
 readonly Dictionary<string,ItemDrops> cache=[];
 string? robots;string? index;DateTimeOffset indexAt,nextRequest;readonly Queue<DateTimeOffset> requests=[];
 public DropReferenceReader(){client.DefaultRequestHeaders.UserAgent.ParseAdd("MapleMarketCompanion/1.20.8 (on-demand item reference)");}
 static string Key(string value)=>Regex.Replace(value.Normalize().Replace('’','\''),@"\s+"," ").Trim().ToLowerInvariant();
 public static string? ResolveItem(string html,string name){
  var links=PublicPages.Document(html).DocumentNode.SelectNodes("//a[@href]")??new HtmlNodeCollection(null);
  var matches=links.Where(a=>Regex.IsMatch(a.GetAttributeValue("href",""),@"^/msclassic/item-db/\d+$")&&Key(PublicPages.Text(a))==Key(name)).Select(a=>a.GetAttributeValue("href","")).Distinct().ToArray();
  return matches.Length==1?Host+matches[0]:null;
 }
 public static IReadOnlyList<(int Id,string Evidence)> ReferenceMonsters(string html,string name){
  var root=PublicPages.Document(html).DocumentNode;
  if(Key(PublicPages.Text(root.SelectSingleNode("//h1")))!=Key(name))throw new FormatException("Source item name does not match the selected item.");
  var heading=root.SelectNodes("//h2")?.FirstOrDefault(h=>PublicPages.Text(h)=="MSEA Reference Drops");
  return (heading?.ParentNode?.ParentNode?.SelectNodes(".//a[@href]")?.ToArray()??[])
   .Select(a=>Regex.Match(a.GetAttributeValue("href",""),@"^/msclassic/monsters/(\d+)$"))
   .Where(m=>m.Success).Select(m=>(int.Parse(m.Groups[1].Value),"Historical MapleSEA reference")).Distinct().ToArray();
 }
 public static DropMonster ReadMonster(string html,string url,string evidence){
  var root=PublicPages.Document(html).DocumentNode;var name=PublicPages.Text(root.SelectSingleNode("//h1"));
  if(string.IsNullOrWhiteSpace(name)||name.Length>150)throw new FormatException("Monster heading unavailable.");
  var maps=(root.SelectSingleNode("//*[@id='locations']")?.SelectNodes(".//a[@href]")?.ToArray()??[])
   .Where(a=>Regex.IsMatch(a.GetAttributeValue("href",""),@"^/msclassic/maps/[0-9]+$"))
   .Select(a=>new DropLocation(PublicPages.Text(a),Host+a.GetAttributeValue("href",""))).Distinct().Take(3).ToArray();
  return new(name,url,evidence,maps);
 }
 async Task<string> Get(string url,CancellationToken ct){
  if(robots is not null&&url!=Host+"/robots.txt"&&!KerningPages.RobotsAllows(robots,new Uri(url).AbsolutePath))throw new HttpRequestException("Source access policy does not allow this reference.");
  var now=DateTimeOffset.UtcNow;while(requests.TryPeek(out var first)&&first<now.AddHours(-1))requests.Dequeue();
  if(requests.Count>=60)throw new HttpRequestException("Reference request limit reached. Try again later.");
  if(nextRequest>now.AddMinutes(1))throw new HttpRequestException("Source is busy. Try again later.");
  var wait=nextRequest-now;if(wait>TimeSpan.Zero)await Task.Delay(wait,ct);
  requests.Enqueue(DateTimeOffset.UtcNow);nextRequest=DateTimeOffset.UtcNow.AddSeconds(3);
  using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct);
  if(response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable){nextRequest=DateTimeOffset.UtcNow.AddMinutes(15);throw new HttpRequestException("Source is busy. Try again in 15 minutes.");}
  response.EnsureSuccessStatusCode();if((int)response.StatusCode>=300)throw new HttpRequestException("Source address changed.");
  using var stream=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[8192];int count;
  while((count=await stream.ReadAsync(buffer,ct))>0){if(output.Length+count>4*1024*1024)throw new IOException("Reference page too large.");await output.WriteAsync(buffer.AsMemory(0,count),ct);}
  return System.Text.Encoding.UTF8.GetString(output.ToArray());
 }
 public async Task<ItemDrops> Lookup(string name,CancellationToken ct){
  await gate.WaitAsync(ct);try{
   if(cache.TryGetValue(Key(name),out var cached)&&cached.CheckedAt>DateTimeOffset.UtcNow.AddHours(-1))return cached;
   if(nextRequest>DateTimeOffset.UtcNow.AddMinutes(1))throw new HttpRequestException("Source is busy. Try again later.");
   if(index is null||indexAt<DateTimeOffset.UtcNow.AddHours(-1)){robots=await Get(Host+"/robots.txt",ct);index=await Get(Host+"/msclassic/item-db/all",ct);indexAt=DateTimeOffset.UtcNow;}
   var url=ResolveItem(index,name);
   if(url is null)return new(name,Host+"/msclassic/item-db/all","No unique exact-name match in NiaMeowDB. No drop information can be confirmed for this item.",[],DateTimeOffset.UtcNow);
   var references=ReferenceMonsters(await Get(url,ct),name).ToList();
   string note="";
   try{using var json=JsonDocument.Parse(await Get(Host+"/msclassic/api/drops?itemId="+url.Split('/').Last(),ct));
    var reports=json.RootElement.GetProperty("drops").EnumerateArray().Select(d=>(Id:d.GetProperty("monsterId").GetInt32(),Evidence:"Community report (not independently verified)")).Where(d=>d.Id>0).Distinct().ToArray();
    references=reports.Concat(references).DistinctBy(r=>r.Id).ToList();
   }catch(Exception e) when(e is HttpRequestException or JsonException or KeyNotFoundException){note="Community reports unavailable; historical references only. ";}
   var monsters=new List<DropMonster>();foreach(var reference in references.Take(5)){
    try{var monsterUrl=Host+"/msclassic/monsters/"+reference.Id;monsters.Add(ReadMonster(await Get(monsterUrl,ct),monsterUrl,reference.Evidence));}
    catch(Exception e) when(e is HttpRequestException or FormatException){note="Some monster details could not load. ";}
   }
   var result=new ItemDrops(name,url,note+(references.Count==0?"No drop sources recorded on this reference. This does not prove that nothing drops it.":$"Showing {monsters.Count} of {references.Count} reference monsters, up to 3 recorded maps each. Classic World / historical MapleSEA coverage can differ from your server."),monsters,DateTimeOffset.UtcNow);
   if(cache.Count>=100)cache.Clear();cache[Key(name)]=result;return result;
  }finally{gate.Release();}
 }
 public void Dispose()=>client.Dispose();
}



