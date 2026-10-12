using System.Globalization;
namespace Market.Core;
public sealed partial class QuestDropReader
{
 public static NpcPrices ReadNpcPrices(string html,string name,string url,DateTimeOffset at){
  var root=PublicPages.Document(html).DocumentNode;
  if(Key(PublicPages.Text(root.SelectSingleNode("//h1")))!=Key(name))throw new FormatException("NPC item name mismatch");
  decimal? Price(string label){
   var tag=root.SelectNodes("//span[@class='stat-tag']")?.SingleOrDefault(n=>PublicPages.Text(n)==label);
   var card=tag?.ParentNode?.ParentNode?.ParentNode;
   var value=PublicPages.Text(card?.SelectSingleNode("./div[contains(concat(' ',normalize-space(@class),' '),' num ')]"));
   return System.Text.RegularExpressions.Regex.IsMatch(value,@"^\d+(,\d{3})*$")&&decimal.TryParse(value,NumberStyles.AllowThousands,CultureInfo.InvariantCulture,out var price)?price:null;
  }
  var sell=Price("NPC pays");var buy=Price("NPC charges");
  return new(name,url,sell,buy,buy,at){Source="MapleStory Quest",Note="Shop purchase price is the lowest recorded on the source; missing prices remain unknown."};
 }
 public async Task<NpcPrices> LookupNpcPrices(string name,CancellationToken ct){
  await gate.WaitAsync(ct);try{
   if(robots is null||robotsAt<DateTimeOffset.UtcNow.AddHours(-1)){robots=await Get(Host+"/robots.txt",ct);robotsAt=DateTimeOffset.UtcNow;}
   var url=ResolveItem(await Get(Host+"/items/all",ct),name);
   if(url is null)throw new FormatException("No unique NPC item reference");
   return ReadNpcPrices(await Get(url,ct),name,url,DateTimeOffset.UtcNow);
  }finally{gate.Release();}
 }
}
public static class NpcPriceFallback
{
 public static async Task<NpcPrices> Lookup(Func<CancellationToken,Task<NpcPrices>> primary,Func<CancellationToken,Task<NpcPrices>> fallback,CancellationToken ct,Action? onFallback=null){
  NpcPrices? first=null;
  using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct)){
   deadline.CancelAfter(TimeSpan.FromSeconds(25));
   try{first=await primary(deadline.Token);ct.ThrowIfCancellationRequested();if(first.Sell is not null||first.BuyMin is not null)return first;}
   catch(Exception e) when(!ct.IsCancellationRequested && e is HttpRequestException or IOException or FormatException or System.Text.Json.JsonException or OperationCanceledException){}
  }
  ct.ThrowIfCancellationRequested();onFallback?.Invoke();
  try{return await fallback(ct);}
  catch(Exception e) when(first is not null&&!ct.IsCancellationRequested && e is HttpRequestException or IOException or FormatException or OperationCanceledException){return first;}
 }
}
