using Market.Core;
using System.Text.Json;
namespace Market.App;
public sealed partial class MainForm{
 const string HistoricalMonsterSource="Historical GMS 83 · MapleStory.io";
 GameGuide HistoricalMonster(string name){
  var matches=AccuracyMonsterCatalog.All.Where(m=>m.Name==name).ToArray();var levels=matches.Select(m=>m.Level).Distinct().Order().ToArray();
  var fields=new List<GuideField>{new("Level",levels.Length==0?"Unknown":string.Join(" / ",levels)),new("Avoidability","Unknown"),new("Catalog IDs",string.Join(", ",matches.Select(m=>m.Id)))};
  string notice="Historical fallback stats; may differ from Classic World. Drop and map reference unavailable; this does not mean there are none.";
  if(matches.Length>1)notice+=" Multiple historical variants share this name. Choose an ID in Accuracy for exact variant stats.";
  return new(1,MonsterPath(name),"monster",name,HistoricalMonsterSource,"https://maplestory.io/api/gms/83/mob",new DateTimeOffset(2026,10,17,0,0,0,TimeSpan.Zero),notice,fields,[]);
 }
 async Task<GameGuide> LoadHistoricalMonster(string name,CancellationToken ct){
  var fallback=HistoricalMonster(name);var matches=AccuracyMonsterCatalog.All.Where(m=>m.Name==name).ToArray();if(matches.Length!=1)return fallback;
  var m=matches[0];string path=Path.Combine(dataDirectory,"accuracy-monsters",m.Id+".json"),raw="";DateTimeOffset checkedAt=DateTimeOffset.UtcNow;
  try{raw=File.ReadAllText(path);AccuracyMonsterCatalog.Parse(raw,m.Id);checkedAt=File.GetLastWriteTimeUtc(path);}catch{raw="";}
  try{
   if(raw.Length==0){if(Offline)return fallback;raw=await accuracyClient.Get("https://maplestory.io/api/gms/83/mob/"+m.Id,ct);AccuracyMonsterCatalog.Parse(raw,m.Id);try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,raw);}catch{}}
   var stats=AccuracyMonsterCatalog.Parse(raw,m.Id);var fields=new List<GuideField>{new("Level",stats.Level.ToString()),new("Avoidability",stats.Avoid.ToString()),new("Catalog ID",m.Id.ToString())};
   using var doc=JsonDocument.Parse(raw);var meta=doc.RootElement.GetProperty("meta");
   foreach(var (key,label) in new[]{("maxHP","HP"),("maxMP","MP"),("exp","EXP"),("physicalDamage","Attack"),("physicalDefense","Defense"),("accuracy","Accuracy")})if(meta.TryGetProperty(key,out var v)&&v.TryGetInt64(out var n)&&n>=0)fields.Add(new(label,n.ToString("N0")));
   return fallback with{Fields=fields,CheckedAt=checkedAt,SourceUrl="https://maplestory.io/api/gms/83/mob/"+m.Id};
  }catch{return fallback;}
 }
}
