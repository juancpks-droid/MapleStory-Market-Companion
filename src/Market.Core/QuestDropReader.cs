using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
namespace Market.Core;
public sealed partial class QuestDropReader : IDisposable
{
 const string Host="https://maplestory.quest";
 readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(25)};
 readonly SemaphoreSlim gate=new(1,1);
 readonly Dictionary<string,(string Html,DateTimeOffset At)> pages=[];
 readonly Dictionary<string,ItemDrops> cache=[];
 readonly Queue<DateTimeOffset> requests=[];
 string? robots;DateTimeOffset robotsAt,nextRequest;
 public QuestDropReader(){client.DefaultRequestHeaders.UserAgent.ParseAdd("MapleMarketCompanion/1.24.4 (on-demand item reference)");}
 static string Key(string text)=>Regex.Replace(text.Normalize().Replace('’','\''),@"\s+"," ").Trim().ToLowerInvariant();
 static bool Route(string path,string kind)=>Regex.IsMatch(path,@"^/"+kind+@"/[0-9]+/[A-Za-z0-9%_.~'-]+$");
 public static string? ResolveItem(string html,string name){
  var links=PublicPages.Document(html).DocumentNode.SelectNodes("//a[@href]")??new HtmlNodeCollection(null);
  var matches=links.Where(a=>Route(a.GetAttributeValue("href",""),"items")&&Key(PublicPages.Text(a))==Key(name)).Select(a=>Host+a.GetAttributeValue("href","")).Distinct().ToArray();
  return matches.Length==1?matches[0]:null;
 }
 public static IReadOnlyList<DropMonster> ReadReferences(string html,string name){
  var root=PublicPages.Document(html).DocumentNode;
  if(Key(PublicPages.Text(root.SelectSingleNode("//h1")))!=Key(name))throw new FormatException("Quest item identity mismatch.");
  var result=new List<DropMonster>();
  foreach(var heading in root.SelectNodes("//h3")??new HtmlNodeCollection(null)){
   if(!PublicPages.Text(heading).StartsWith("Dropped by",StringComparison.OrdinalIgnoreCase))continue;
   var section=heading.Ancestors("section").FirstOrDefault();if(section is null)continue;
   string evidence=PublicPages.Text(heading.ParentNode).Contains("older version",StringComparison.OrdinalIgnoreCase)?"MapleStory Quest • Historical older-version drop; not confirmed for Classic":"MapleStory Quest • Source-reported drop; not independently verified";
   foreach(var a in section.SelectNodes(".//a[@href]")??new HtmlNodeCollection(null)){
    string path=a.GetAttributeValue("href","");if(!Route(path,"monsters"))continue;
    string monster=PublicPages.Text(a.SelectSingleNode(".//span[contains(concat(' ',normalize-space(@class),' '),' truncate ')]"));
    if(ItemFinder.HasEnglishDisplayName(monster))result.Add(new(monster,Host+path,evidence,[]));
   }
  }
  return result.GroupBy(m=>m.Url).Select(g=>g.First() with{Evidence=string.Join("\n",g.Select(m=>m.Evidence).Distinct())}).ToArray();
 }
 public static DropMonster ReadMaps(string html,DropMonster monster){
  var root=PublicPages.Document(html).DocumentNode;
  if(Key(PublicPages.Text(root.SelectSingleNode("//h1")))!=Key(monster.Name))throw new FormatException("Quest monster identity mismatch.");
  var heading=root.SelectNodes("//h2")?.FirstOrDefault(h=>PublicPages.Text(h).StartsWith("Where it spawns",StringComparison.OrdinalIgnoreCase));
  var section=heading?.Ancestors("section").FirstOrDefault();
  var maps=(section?.SelectNodes(".//a[@href]")??new HtmlNodeCollection(null)).Where(a=>Route(a.GetAttributeValue("href",""),"maps")).Select(a=>new DropLocation(PublicPages.Text(a),Host+a.GetAttributeValue("href",""))).Where(m=>ItemFinder.HasEnglishDisplayName(m.Name)).Distinct().Take(3).ToArray();
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

 public async Task<ItemDrops> Lookup(string name,CancellationToken ct){
  await gate.WaitAsync(ct);try{
   if(cache.TryGetValue(Key(name),out var cached)&&cached.CheckedAt>DateTimeOffset.UtcNow.AddHours(-1))return cached;
   if(robots is null||robotsAt<DateTimeOffset.UtcNow.AddHours(-1)){robots=await Get(Host+"/robots.txt",ct);robotsAt=DateTimeOffset.UtcNow;}
   var url=ResolveItem(await Get(Host+"/items/all",ct),name);
   if(url is null)return new(name,"","No unique matching MapleStory Quest item. Names and IDs can differ between game versions; no drops were assigned.",[],DateTimeOffset.UtcNow);
   var refs=ReadReferences(await Get(url,ct),name);var monsters=new List<DropMonster>();bool incomplete=false;
   foreach(var monster in refs.Take(5)){
    try{monsters.Add(ReadMaps(await Get(monster.Url,ct),monster));}
    catch(Exception e) when(e is HttpRequestException or FormatException){monsters.Add(monster);incomplete=true;}
   }
   var result=new ItemDrops(name,url,$"Showing {monsters.Count} of {refs.Count} recorded monsters, up to 3 maps each. Historical and source-reported drops may differ from your server."+(incomplete?" Some maps could not load.":""),monsters,DateTimeOffset.UtcNow);
   if(cache.Count>=100)cache.Clear();cache[Key(name)]=result;return result;
  }finally{gate.Release();}
 }
 public void Dispose()=>client.Dispose();
}
