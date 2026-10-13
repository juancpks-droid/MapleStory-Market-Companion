using System.Net;
using System.Text.Json;
namespace Market.Core;

// Small, per-item snapshots only. A shared miss immediately falls back to the existing readers.
public static class SharedGameReference
{
 static readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromMilliseconds(1200),MaxResponseContentBufferSize=262144};
 const string Endpoint="https://maple-market-lite.bronze-grape-1829.chatgpt.site/api/game-data";
 public static string ItemId(string id,string name)=>ScrollNames.Resolve(name) is {} scroll?"classic:"+scroll.ClassicId:id;
 public static bool Valid(JsonElement root,string id,string kind)=>root.TryGetProperty("schemaVersion",out var version)&&version.GetInt32()==1&&root.GetProperty("kind").GetString()==kind&&root.GetProperty("item").GetProperty("key").GetString()==(id.StartsWith("classic:")?id:"gms83:"+id)&&root.TryGetProperty("data",out _);
 static async Task<JsonDocument?> Read(string directory,string id,string name,string kind,bool offline,CancellationToken ct){
  id=ItemId(id,name);if(!System.Text.RegularExpressions.Regex.IsMatch(id,@"^(classic:)?\d{1,9}$"))return null;
  string path=Path.Combine(directory,"shared-game-data",kind+"-"+id.Replace(':','-')+".json");JsonDocument? saved=null;
  try{if(File.Exists(path)&&new FileInfo(path).Length<=262144){saved=JsonDocument.Parse(await File.ReadAllTextAsync(path,ct));if(!Valid(saved.RootElement,id,kind)){saved.Dispose();saved=null;}}}catch(Exception e) when(e is IOException or JsonException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException){saved?.Dispose();saved=null;}
  if(saved is not null&&(offline||DateTimeOffset.Parse(saved.RootElement.GetProperty("checkedAt").GetString()!)>DateTimeOffset.UtcNow.AddDays(-7)))return saved;
  if(offline)return saved;
  try{using var response=await client.GetAsync(Endpoint+"?cached=1&kind="+kind+"&id="+Uri.EscapeDataString(id),ct);if(response.StatusCode==HttpStatusCode.NoContent||!response.IsSuccessStatusCode)return saved;
   string json=await response.Content.ReadAsStringAsync(ct);using var parsed=JsonDocument.Parse(json);if(!Valid(parsed.RootElement,id,kind))return saved;
   try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";await File.WriteAllTextAsync(temporary,json,ct);File.Move(temporary,path,true);}catch(Exception e) when(e is IOException or UnauthorizedAccessException){}
   saved?.Dispose();return JsonDocument.Parse(json);
  }catch(Exception e) when(!ct.IsCancellationRequested&&e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException){return saved;}
 }
 static async Task<DropLookupResult?> ReadDrops(string directory,string id,string name,bool offline,CancellationToken ct){using var doc=await Read(directory,id,name,"drops",offline,ct);if(doc is null)return null;var root=doc.RootElement;var data=root.GetProperty("data");var monsters=data.GetProperty("monsters").EnumerateArray().Select(m=>new DropMonster(m.GetProperty("name").GetString()!,m.GetProperty("url").GetString()!,m.GetProperty("evidence").GetString()!,m.GetProperty("maps").EnumerateArray().Select(x=>new DropLocation(x.GetProperty("name").GetString()!,x.GetProperty("url").GetString()!)).ToArray())).ToArray();if(monsters.Length==0)return null;var at=DateTimeOffset.Parse(root.GetProperty("checkedAt").GetString()!);return new(new(name,data.GetProperty("sourceUrl").GetString()!,data.GetProperty("note").GetString()!,monsters,at),data.GetProperty("source").GetString()!,"Shared game database • "+(at<DateTimeOffset.UtcNow.AddDays(-7)?"Older saved reference • ":""));}
 static async Task<NpcPrices?> ReadNpc(string directory,string id,string name,bool offline,CancellationToken ct){using var doc=await Read(directory,id,name,"npc",offline,ct);if(doc is null)return null;var root=doc.RootElement;var data=root.GetProperty("data");decimal? Number(string key)=>data.TryGetProperty(key,out var n)&&n.ValueKind==JsonValueKind.Number?n.GetDecimal():null;var sell=Number("sell");var buy=Number("buy");if(sell is null&&buy is null)return null;return new(name,data.TryGetProperty("url",out var url)?url.GetString()!:"",sell,buy,buy,DateTimeOffset.Parse(root.GetProperty("checkedAt").GetString()!)){Source=data.GetProperty("source").GetString()+" • shared database"};}
 public static async Task<DropLookupResult?> Drops(string directory,string id,string name,bool offline,CancellationToken ct){try{return await ReadDrops(directory,id,name,offline,ct);}catch(Exception e) when(e is IOException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException or OperationCanceledException){return null;}}
 public static async Task<NpcPrices?> Npc(string directory,string id,string name,bool offline,CancellationToken ct){try{return await ReadNpc(directory,id,name,offline,ct);}catch(Exception e) when(e is IOException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException or OperationCanceledException){return null;}}
}
