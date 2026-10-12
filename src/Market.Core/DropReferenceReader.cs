using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
namespace Market.Core;
public sealed record DropLocation(string Name,string Url);
public sealed record DropMonster(string Name,string Url,string Evidence,IReadOnlyList<DropLocation> Locations);
public sealed record DropItemSuggestion(string Name,string Url){public override string ToString()=>Name;}
public sealed record ItemDrops(string Item,string SourceUrl,string Message,IReadOnlyList<DropMonster> Monsters,DateTimeOffset CheckedAt){public IReadOnlyList<DropItemSuggestion> Suggestions {get;init;}=[];}
public sealed class DropReferenceReader : IDisposable
{
 const string Host="https://meowdb.com";
 readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(25)};
 readonly SemaphoreSlim gate=new(1,1);
 readonly Dictionary<string,ItemDrops> cache=[];
 string? robots;string? index;DateTimeOffset indexAt,nextRequest;readonly Queue<DateTimeOffset> requests=[];
 public DropReferenceReader(){client.DefaultRequestHeaders.UserAgent.ParseAdd("MapleMarketCompanion/1.20.8 (on-demand item reference)");}
 static string Key(string value)=>Regex.Replace(value.Normalize().Replace('’','\''),@"\s+"," ").Trim().ToLowerInvariant();
 static string ItemLabelKey(string value)=>Key(Regex.Replace(value,@"\s*\(\s*Lv\.?\s*\d+\s*\)\s*$","",RegexOptions.IgnoreCase));
 public static string? ResolveItem(string html,string name){
  var links=PublicPages.Document(html).DocumentNode.SelectNodes("//a[@href]")??new HtmlNodeCollection(null);
  var matches=links.Where(a=>Regex.IsMatch(a.GetAttributeValue("href",""),@"^/msclassic/item-db/\d+$")&&ItemLabelKey(PublicPages.Text(a))==ItemLabelKey(name)).Select(a=>a.GetAttributeValue("href","")).Distinct().ToArray();
  return matches.Length==1?Host+matches[0]:null;
 }
 public static IReadOnlyList<DropItemSuggestion> RelatedItems(string html,string name){
  string Normalize(string text)=>Regex.Replace(Key(text),"[^a-z0-9]+"," ").Trim();
  var query=Normalize(name);if(query.Length<3||query.Length>150)return [];
  var words=query.Split(' ',StringSplitOptions.RemoveEmptyEntries);
  int Distance(string a,string b){var previous=Enumerable.Range(0,b.Length+1).ToArray();for(int i=1;i<=a.Length;i++){var current=new int[b.Length+1];current[0]=i;for(int j=1;j<=b.Length;j++)current[j]=Math.Min(Math.Min(current[j-1]+1,previous[j]+1),previous[j-1]+(a[i-1]==b[j-1]?0:1));previous=current;}return previous[b.Length];}
  int Score(string text){var value=Normalize(text);if(value==query)return 1000;if(value.Contains(query)||query.Contains(value)&&value.Length>=3)return 800-Math.Abs(value.Length-query.Length);var tokens=value.Split(' ',StringSplitOptions.RemoveEmptyEntries);int common=words.Count(w=>tokens.Contains(w));int near=words.Count(w=>w.Length>=4&&tokens.Any(t=>t.Length>=4&&Math.Abs(t.Length-w.Length)<=1&&Distance(w,t)<=1));return common*100+near*25;}
  var links=PublicPages.Document(html).DocumentNode.SelectNodes("//a[@href]")??new HtmlNodeCollection(null);
  return links.Where(a=>Regex.IsMatch(a.GetAttributeValue("href",""),@"^/msclassic/item-db/\d+$"))
   .Select(a=>new DropItemSuggestion(PublicPages.Text(a),Host+a.GetAttributeValue("href","")))
   .Where(a=>a.Name.Length is >0 and <=150&&ItemFinder.HasEnglishDisplayName(a.Name)).DistinctBy(a=>a.Url)
   .Select(a=>(Item:a,Rank:Score(a.Name))).Where(a=>a.Rank>0).OrderByDescending(a=>a.Rank).ThenBy(a=>a.Item.Name).Take(5).Select(a=>a.Item).ToArray();
 }
 public static IReadOnlyList<(int Id,string Evidence)> ReferenceMonsters(string html,string name){
  return SectionMonsters(html,name,"MSEA Reference Drops","MSEA Reference Drops • Historical MapleSEA reference");
 }
 public static IReadOnlyList<(int Id,string Evidence)> CommunityMonsters(string html,string name)=>SectionMonsters(html,name,"Dropped By","Dropped By • Community report (not independently verified)");
 static IReadOnlyList<(int Id,string Evidence)> SectionMonsters(string html,string name,string title,string evidence){
  var root=PublicPages.Document(html).DocumentNode;
  if(ItemLabelKey(PublicPages.Text(root.SelectSingleNode("//h1")))!=ItemLabelKey(name))throw new FormatException("Source item name does not match the selected item.");
  var heading=root.SelectNodes("//h2")?.FirstOrDefault(h=>PublicPages.Text(h)==title);
  return (heading?.ParentNode?.ParentNode?.SelectNodes(".//a[@href]")?.ToArray()??[])
   .Select(a=>Regex.Match(a.GetAttributeValue("href",""),@"^/msclassic/monsters/(\d+)$"))
   .Where(m=>m.Success).Select(m=>(int.Parse(m.Groups[1].Value),evidence)).Distinct().ToArray();
 }
 public static IReadOnlyList<(int Id,string Evidence)> MergeDropEvidence(IEnumerable<(int Id,string Evidence)> rows)=>rows.GroupBy(r=>r.Id).Select(g=>(g.Key,string.Join("\n",g.Select(r=>r.Evidence).Distinct()))).ToArray();
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
   if(url is null){var suggestions=RelatedItems(index,name);return new(name,Host+"/msclassic/item-db/all",suggestions.Count>0?"No confirmed match. Related items are available below; choose one to inspect its reference. Their drops are not assigned to your selected item.":"No confirmed match or relevant suggestions found. Try another drop source or search the website.",[],DateTimeOffset.UtcNow){Suggestions=suggestions};}
   var html=await Get(url,ct);var references=CommunityMonsters(html,name).Concat(ReferenceMonsters(html,name)).ToList();
   string note="";
   try{using var json=JsonDocument.Parse(await Get(Host+"/msclassic/api/drops?itemId="+url.Split('/').Last(),ct));
    var reports=json.RootElement.GetProperty("drops").EnumerateArray().Select(d=>(Id:d.GetProperty("monsterId").GetInt32(),Evidence:"Dropped By • Community report (not independently verified)")).Where(d=>d.Id>0).Distinct().ToArray();
    references=reports.Concat(references).ToList();
   }catch(Exception e) when(e is HttpRequestException or JsonException or KeyNotFoundException){note="Live community reports unavailable; showing the page's recorded references. ";}
   references=MergeDropEvidence(references).ToList();
   var monsters=new List<DropMonster>();foreach(var reference in references.Take(5)){
    try{var monsterUrl=Host+"/msclassic/monsters/"+reference.Id;monsters.Add(ReadMonster(await Get(monsterUrl,ct),monsterUrl,reference.Evidence));}
    catch(Exception e) when(e is HttpRequestException or FormatException){note="Some monster details could not load. ";}
   }
   var result=new ItemDrops(name,url,note+(references.Count==0?"No drop sources recorded on this reference. This does not prove that nothing drops it.":$"Showing {monsters.Count} of {references.Count} reference monsters, up to 3 recorded maps each. MSEA references are likely sources, not confirmed Classic drops."),monsters,DateTimeOffset.UtcNow);
   if(cache.Count>=100)cache.Clear();cache[Key(name)]=result;return result;
  }finally{gate.Release();}
 }
 public void Dispose()=>client.Dispose();
}



