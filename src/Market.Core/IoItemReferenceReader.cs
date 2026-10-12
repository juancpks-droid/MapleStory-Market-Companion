using System.Text.Json;
namespace Market.Core;
public sealed class IoItemReferenceReader : IDisposable
{
 readonly PublicReferenceClient client=new("https://maplestory.io",false);
 public static ItemReference Read(string json,string name,string id){
  using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
  if(root.GetProperty("id").ToString()!=id||TextKey.Normalize(root.GetProperty("description").GetProperty("name").GetString()??"")!=TextKey.Normalize(name))throw new FormatException("Historical reference identity mismatch");
  var fields=new List<KeyValuePair<string,string>>();var meta=root.GetProperty("metaInfo");
  foreach(var (key,label) in new[]{("reqLevel","Required level"),("reqSTR","Required STR"),("reqDEX","Required DEX"),("reqINT","Required INT"),("reqLUK","Required LUK"),("incPAD","Weapon attack"),("incMAD","Magic attack"),("incPDD","Weapon defense"),("incMDD","Magic defense"),("incSTR","STR"),("incDEX","DEX"),("incINT","INT"),("incLUK","LUK"),("tuc","Upgrade slots")})if(meta.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number)fields.Add(new(label,v.ToString()));
  return new(name,"MapleStory.io • Historical GMS 83","https://maplestory.io/api/gms/83/item/"+id,root.GetProperty("description").GetProperty("description").GetString()?.Replace("\\n","\n")??"",fields,"Historical GMS version 83 — NOT Classic World confirmation. Stats, effects and availability may differ. No drop tables or market/NPC prices inferred. Game data © Nexon; served by MapleStory.io.");
 }
 public async Task<ItemReference> Lookup(string name,string id,CancellationToken ct){if(id.Length<1||id.Length>9||!id.All(char.IsAsciiDigit))throw new FormatException("A catalog item ID is required");return Read(await client.Get("https://maplestory.io/api/gms/83/item/"+id,ct),name,id);}
 public void Dispose()=>client.Dispose();
}
