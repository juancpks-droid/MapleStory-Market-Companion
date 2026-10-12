using System.Globalization;
using System.Text.Json;
namespace Market.Core;
public sealed record NpcPrices(string Item,string Url,decimal? Sell,decimal? BuyMin,decimal? BuyMax,DateTimeOffset CheckedAt);
public sealed partial class CcplzReader
{
 public static NpcPrices ReadNpcPrices(string html,string name,string id,DateTimeOffset at){
  var dom=new HtmlAgilityPack.HtmlDocument();dom.LoadHtml(html);var root=dom.DocumentNode;
  var script=root.SelectSingleNode("//script[@id='ccplz-entity']")??throw new FormatException("NPC reference missing");
  using var document=JsonDocument.Parse(script.InnerText);var item=document.RootElement.GetProperty("item");
  if(id.Length>0){if(!item.TryGetProperty("client_id",out var clientId)||clientId.ToString()!=id)throw new FormatException("NPC item ID mismatch");}
  else if(!MatchesItem(html,name))throw new FormatException("NPC item name mismatch");
  decimal? sell=null;
  var label=root.SelectNodes("//dl[contains(@class,'server-facts')]//dt")?.FirstOrDefault(n=>PublicPages.Text(n)=="NPC sell price");
  if(label is not null){var text=PublicPages.Text(label.SelectSingleNode("following-sibling::dd[1]"));var match=System.Text.RegularExpressions.Regex.Match(text,@"^([\d,]+) mesos?$");if(match.Success&&decimal.TryParse(match.Groups[1].Value,NumberStyles.AllowThousands,CultureInfo.InvariantCulture,out var value)&&value>=0)sell=value;}
  var buys=new List<decimal>();
  if(item.TryGetProperty("graph",out var graph)&&graph.TryGetProperty("incoming",out var incoming))foreach(var edge in incoming.EnumerateArray()){
   if(!edge.TryGetProperty("canonical_relation_type",out var type)||type.GetString()!="sells_item"||!edge.TryGetProperty("target",out var target)||!target.TryGetProperty("entity_type",out var kind)||kind.GetString()!="shop"||!edge.TryGetProperty("qualifiers",out var q))continue;
   if(q.TryGetProperty("currency",out var currency)&&currency.GetString()=="Mesos"&&q.TryGetProperty("price",out var price)&&price.ValueKind==JsonValueKind.Number&&price.TryGetDecimal(out var value)&&value>=0)buys.Add(value);
  }
  return new(name,ItemUrl(name),sell,buys.Count==0?null:buys.Min(),buys.Count==0?null:buys.Max(),at);
 }
 public async Task<NpcPrices> LookupNpcPrices(string name,string id,CancellationToken ct){
  await gate.WaitAsync(ct);try{
   if(robots is null||robotsAt<DateTimeOffset.UtcNow.AddHours(-1)){robots=await Get(Host+"/robots.txt",ct);robotsAt=DateTimeOffset.UtcNow;}
   return ReadNpcPrices(await Get(ItemUrl(name),ct),name,id,DateTimeOffset.UtcNow);
  }finally{gate.Release();}
 }
}
