using System.Text.Json;
namespace Market.Core;
public sealed record AccuracyMonster(int Id,string Name,int Level,bool Boss){public string Label=>$"{Name} · Lv {Level}{(Boss?" · Boss":"")} · #{Id}";}
public sealed record AccuracyMonsterStats(int Id,int Level,int Avoid);
public static class AccuracyMonsterCatalog {
 public static bool IsDisplayName(string name)=>System.Text.RegularExpressions.Regex.IsMatch(name,@"[A-Za-z]")&&!System.Text.RegularExpressions.Regex.IsMatch(name,@"transparent|\btest\b|placeholder",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
 public static readonly AccuracyMonster[] All=JsonSerializer.Deserialize<AccuracyMonster[]>(typeof(AccuracyMonsterCatalog).Assembly.GetManifestResourceStream("accuracy-monsters")!,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
 public static AccuracyMonsterStats Parse(string json,int id){
  using var doc=JsonDocument.Parse(json);var root=doc.RootElement;var expected=All.FirstOrDefault(m=>m.Id==id)??throw new FormatException("Unknown monster");
  if(root.GetProperty("id").GetInt32()!=id||root.GetProperty("name").GetString()!=expected.Name)throw new FormatException("Monster identity mismatch");
  var meta=root.GetProperty("meta");int level=meta.GetProperty("level").GetInt32(),avoid=meta.GetProperty("evasion").GetInt32();
  if(level<1||level>200||avoid<0||avoid>100000)throw new FormatException("Monster stats unavailable");return new(id,level,avoid);
 }
}
