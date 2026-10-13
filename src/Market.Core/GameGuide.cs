using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Market.Core;

public sealed record GuideField(string Label,string Value);
public sealed record GuideLink(string Name,string Path,string Detail);
public sealed record GuideSection(string Title,IReadOnlyList<GuideLink> Links,int Total);
public sealed record GuideEntry(string Path,string Name,string Kind);
public sealed record GameGuide(int SchemaVersion,string Path,string Kind,string Name,string Source,string SourceUrl,DateTimeOffset CheckedAt,string Notice,IReadOnlyList<GuideField> Fields,IReadOnlyList<GuideSection> Sections);
public sealed class GameGuideReader : IDisposable
{
 const string Host="https://ccplz.cc",Snapshot="https://raw.githubusercontent.com/juancpks-droid/MapleStory-Market-Companion/main/SharedGameData/guides/";
 readonly PublicReferenceClient source=new(Host);
 readonly HttpClient published=new(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromMilliseconds(1200),MaxResponseContentBufferSize=262144};
 readonly SemaphoreSlim gate=new(1,1);
 static readonly JsonSerializerOptions json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true};
 static readonly Lazy<IReadOnlyList<GameGuide>> starter=new(()=>{using var stream=typeof(GameGuideReader).Assembly.GetManifestResourceStream("game-guide-seed");return stream is null?[]:JsonSerializer.Deserialize<GameGuide[]>(stream,json)??[];});
 public static bool ValidPath(string path)=>path is not null&&path.Length<180&&Regex.IsMatch(path,@"^/(items|monsters|maps|npcs|shops|quests|recipes)/[a-z0-9]+(?:-[a-z0-9]+)*$");
 public static string ItemPath(string name)=>new Uri(CcplzReader.ItemUrl(ScrollNames.Resolve(name)?.Name??name)).AbsolutePath;
 static string Kind(string path)=>path.Split('/')[1] switch{"items"=>"item","monsters"=>"monster","maps"=>"map","npcs"=>"npc","shops"=>"shop","quests"=>"quest","recipes"=>"recipe",_=>""};
 static bool English(string text)=>!string.IsNullOrWhiteSpace(text)&&!Regex.IsMatch(text,@"[\u1100-\u11ff\u2e80-\u9fff\uac00-\ud7af]");
 static string Key(string name)=>TextKey.Normalize(Regex.Replace(name,@"\s*\(Lv\.?\s*\d+\)\s*$","",RegexOptions.IgnoreCase));
 public static bool Valid(GameGuide? guide,string path,string name="")=>guide is not null&&ValidPath(path)&&guide.SchemaVersion==1&&guide.Path==path&&guide.Kind==Kind(path)&&English(guide.Name)&&guide.Name.Length<=160&&(name.Length==0||Key(guide.Name)==Key(name))&&guide.Source=="CCPLZ"&&guide.SourceUrl==Host+path&&guide.Notice is {Length:<=2000}&&guide.CheckedAt<=DateTimeOffset.UtcNow.AddMinutes(2)&&guide.CheckedAt>=new DateTimeOffset(2000,1,1,0,0,0,TimeSpan.Zero)&&guide.Fields is {Count:<=70}&&guide.Fields.All(x=>x is not null&&English(x.Label)&&English(x.Value)&&x.Label.Length<100&&x.Value.Length<300)&&guide.Sections is {Count:<=23}&&guide.Sections.All(s=>s is not null&&English(s.Title)&&s.Title.Length<=160&&s.Links is {Count:<=60}&&s.Total>=s.Links.Count&&s.Links.All(l=>l is not null&&English(l.Name)&&l.Name.Length<=160&&l.Detail is {Length:<=250}&&(l.Detail.Length==0||English(l.Detail))&&ValidPath(l.Path)))&&(guide.Fields.Count>0||guide.Sections.Count>0);
 public static GameGuide Parse(string html,string path,string name=""){
  if(!ValidPath(path))throw new FormatException("Unsupported reference");
  var root=PublicPages.Document(html).DocumentNode.SelectSingleNode("//article[@data-ssr]")??throw new FormatException("Missing reference");
  var title=PublicPages.Text(root.SelectSingleNode(".//h1"));var kind=Kind(path);
  if(root.GetAttributeValue("data-ssr","")!=kind||!English(title)||(name.Length>0&&Key(title)!=Key(name)))throw new FormatException("Reference identity mismatch");
  var fields=new List<GuideField>();var sections=new List<GuideSection>();
  foreach(var dt in root.SelectNodes(".//dl//dt")?.ToArray()??[]){var label=PublicPages.Text(dt);var value=PublicPages.Text(dt.SelectSingleNode("following-sibling::dd[1]"));if(English(label)&&English(value)&&label.Length<100&&value.Length<300&&fields.Count<60)fields.Add(new(label,value));}
  foreach(var section in root.SelectNodes(".//section")?.ToArray()??[]){var heading=PublicPages.Text(section.SelectSingleNode("./h2|./h3"));if(!English(heading)||sections.Count>=20)continue;var links=new List<GuideLink>();foreach(var a in section.SelectNodes(".//a[@href]")?.ToArray()??[]){var target=a.GetAttributeValue("href","");var text=PublicPages.Text(a);if(!ValidPath(target)||!English(text)||text.Length>160)continue;var row=a.Ancestors().FirstOrDefault(n=>n.Name is "li" or "tr");var detail=string.Join(" · ",row?.SelectNodes(".//span")?.Select(PublicPages.Text)??[]);if(detail.Length>250)detail=detail[..250];if(!English(detail))detail="";var link=new GuideLink(text,target,detail);if(!links.Contains(link))links.Add(link);}if(links.Count>0)sections.Add(new(heading,links.Take(60).ToArray(),links.Count));}
  AddStructuredFacts(html,kind,fields,sections);
  var result=new GameGuide(1,path,kind,title,"CCPLZ",Host+path,DateTimeOffset.UtcNow,"CCPLZ reference; game versions may differ. Reported and historical drops are not independently confirmed Classic drops. Missing information is not proof of absence. Lists may be partial.",fields,sections);
  if(!Valid(result,path,name))throw new FormatException("Invalid factual reference");return result;
 }
 static void AddStructuredFacts(string html,string kind,List<GuideField> fields,List<GuideSection> sections){
  try{
   var structured=new HtmlAgilityPack.HtmlDocument();structured.LoadHtml(html);var node=structured.DocumentNode.SelectSingleNode("//*[@id='ccplz-entity']");if(node is null)return;
   using var document=JsonDocument.Parse(node.InnerText);if(!document.RootElement.TryGetProperty(kind,out var entity))return;
   if(entity.TryGetProperty("facts",out var facts)&&facts.ValueKind==JsonValueKind.Object){
    var labels=new Dictionary<string,string>{{"meso","Crafting fee (mesos)"},{"resultCount","Result quantity"},{"skillLevel","Required skill level"},{"success","Success rate (%)"},{"reqLevel","Required level"},{"reqSTR","Required STR"},{"reqDEX","Required DEX"},{"reqINT","Required INT"},{"reqLUK","Required LUK"},{"tradeBlock","Trade restriction flag"},{"slotMax","Stack limit"}};
    foreach(var pair in labels)if(facts.TryGetProperty(pair.Key,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out _)&&!fields.Any(f=>f.Label==pair.Value)&&fields.Count<70)fields.Add(new(pair.Value,value.GetRawText()));
   }
   if(kind=="item"&&entity.TryGetProperty("graph",out var graph)&&graph.TryGetProperty("incoming",out var incoming)&&incoming.ValueKind==JsonValueKind.Array){
    var prices=new List<decimal>();foreach(var edge in incoming.EnumerateArray()){
     if(edge.TryGetProperty("canonical_relation_type",out var relation)&&relation.GetString()=="sells_item"&&edge.TryGetProperty("target",out var target)&&target.TryGetProperty("entity_type",out var type)&&type.GetString()=="shop"&&edge.TryGetProperty("qualifiers",out var q)&&q.TryGetProperty("currency",out var currency)&&currency.GetString()=="Mesos"&&q.TryGetProperty("price",out var p)&&p.ValueKind==JsonValueKind.Number&&p.TryGetDecimal(out var price)&&price>=0)prices.Add(price);
    }if(prices.Count>0&&fields.Count<70&&!fields.Any(f=>f.Label=="NPC buy price (lowest recorded)"))fields.Add(new("NPC buy price (lowest recorded)",prices.Min().ToString(System.Globalization.CultureInfo.InvariantCulture)+" mesos"));
   }
   if(kind=="recipe")foreach(var pair in new[]{("ingredients","Ingredients"),("result","Result"),("catalyst","Catalyst")}){
    if(!entity.TryGetProperty(pair.Item1,out var value)||sections.Any(s=>s.Title==pair.Item2))continue;
    var values=value.ValueKind==JsonValueKind.Array?value.EnumerateArray().ToArray():new[]{value};var links=new List<GuideLink>();
    foreach(var ingredient in values){if(ingredient.ValueKind!=JsonValueKind.Object||!ingredient.TryGetProperty("name",out var n)||n.ValueKind!=JsonValueKind.String||!ingredient.TryGetProperty("canonical_slug",out var slug)||slug.ValueKind!=JsonValueKind.String)continue;string name=n.GetString()!,path="/items/"+slug.GetString();if(!English(name)||name.Length>160||!ValidPath(path))continue;string detail=ingredient.TryGetProperty("quantity",out var quantity)&&quantity.ValueKind==JsonValueKind.Number?"Quantity: "+quantity.GetRawText():"";links.Add(new(name,path,detail));}
    if(links.Count>0&&sections.Count<23)sections.Add(new(pair.Item2,links.Take(60).ToArray(),links.Count));
   }
  }catch(Exception e) when(e is JsonException or InvalidOperationException or FormatException){/* The visible factual sections remain usable. */}
 }
 public static IReadOnlyList<GuideEntry> SearchDirectory(IEnumerable<GuideEntry> entries,string query,string kind="all"){
  var tokens=query.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
  return entries.Where(e=>ValidPath(e.Path)&&English(e.Name)&&(kind=="all"||e.Kind==kind)&&tokens.All(t=>e.Name.Contains(t,StringComparison.OrdinalIgnoreCase))).OrderBy(e=>e.Name).Take(40).ToArray();
 }
 public async Task<IReadOnlyList<GuideEntry>> DirectoryEntries(bool offline,CancellationToken ct){
  var fallback=starter.Value.Select(g=>new GuideEntry(g.Path,g.Name,g.Kind)).ToArray();if(offline)return fallback;
  try{using var response=await published.GetAsync(Snapshot.Replace("guides/","guide-index.json"),ct);response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));if(doc.RootElement.GetProperty("schemaVersion").GetInt32()!=1)return fallback;var entries=doc.RootElement.GetProperty("entries").Deserialize<GuideEntry[]>(json);if(entries is {Length:<=2000}&&entries.All(e=>e is not null&&ValidPath(e.Path)&&English(e.Name)&&e.Name.Length<=160&&e.Kind==Kind(e.Path)))return entries;}catch(Exception e) when(!ct.IsCancellationRequested&&e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException){}return fallback;
 }
 static string FileName(string path)=>path[1..].Replace("/","--")+".json";
 async Task<GameGuide?> Saved(string directory,string path,string name,CancellationToken ct){
  try{string file=System.IO.Path.Combine(directory,"game-guide",FileName(path));if(File.Exists(file)&&new FileInfo(file).Length<=262144){var record=JsonSerializer.Deserialize<GameGuide>(await File.ReadAllTextAsync(file,ct),json);if(Valid(record,path,name))return record;}}
  catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException){}
  return starter.Value.FirstOrDefault(r=>Valid(r,path,name));
 }
 async Task Store(string directory,GameGuide guide,CancellationToken ct){string folder=System.IO.Path.Combine(directory,"game-guide");string temporary="";try{Directory.CreateDirectory(folder);string file=System.IO.Path.Combine(folder,FileName(guide.Path));temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(guide,json),ct);File.Move(temporary,file,true);
   // Only this reader's validated per-page JSON cache, never player settings.
   foreach(var old in new DirectoryInfo(folder).GetFiles("*.json").OrderByDescending(x=>x.LastWriteTimeUtc).Skip(100))old.Delete();
  }catch(Exception e) when(e is IOException or UnauthorizedAccessException){}finally{if(temporary.Length>0&&File.Exists(temporary))try{File.Delete(temporary);}catch(IOException){}}
 }
 public async Task<GameGuide?> Lookup(string directory,string path,string name,bool offline,CancellationToken ct,bool refresh=false){
  if(!ValidPath(path))throw new FormatException("Invalid reference path");
  var saved=await Saved(directory,path,name,ct);if(offline||(saved is not null&&!refresh&&saved.CheckedAt>DateTimeOffset.UtcNow.AddDays(-7)))return saved;
  await gate.WaitAsync(ct);try{
   try{using var response=await published.GetAsync(Snapshot+FileName(path),ct);if(response.IsSuccessStatusCode){var record=JsonSerializer.Deserialize<GameGuide>(await response.Content.ReadAsStringAsync(ct),json);if(Valid(record,path,name)&&(saved is null||record!.CheckedAt>saved.CheckedAt)){await Store(directory,record!,ct);return record;}}}catch(Exception e) when(!ct.IsCancellationRequested&&e is HttpRequestException or TaskCanceledException or JsonException){}
   try{var record=Parse(await source.Get(Host+path,ct),path,name);await Store(directory,record,ct);return record;}catch(Exception e) when(!ct.IsCancellationRequested&&e is HttpRequestException or IOException or FormatException){if(saved is not null)return saved;throw;}
  }finally{gate.Release();}
 }
 public void Dispose(){source.Dispose();published.Dispose();gate.Dispose();}
}
