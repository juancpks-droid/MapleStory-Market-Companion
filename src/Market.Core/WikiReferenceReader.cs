using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
namespace Market.Core;
public sealed record ItemReference(string Name,string Source,string Url,string Description,IReadOnlyList<KeyValuePair<string,string>> Fields,string Notice);
public sealed class WikiReferenceReader : IDisposable
{
 public const string Host="https://mapleclassic.wiki";
 public const string Credit="MapleClassic Wiki contributors • CC BY-NC-SA 4.0 • Selected facts reformatted; source-reported, not independently verified. https://creativecommons.org/licenses/by-nc-sa/4.0/";
 readonly PublicReferenceClient client=new(Host);
 public static string ItemUrl(string name)=>Host+"/wiki/"+Uri.EscapeDataString(name.Trim().Replace(' ','_'));
 static string Key(string s)=>TextKey.Normalize(s);
 static HtmlNode Root(string html,string name){var root=PublicPages.Document(html).DocumentNode;if(Key(PublicPages.Text(root.SelectSingleNode("//h1")))!=Key(name))throw new FormatException("Wiki item identity mismatch");return root;}
 static HtmlNode? Table(HtmlNode root,string id){var heading=root.SelectSingleNode("//*[@id='"+id+"']")?.ParentNode;if(heading?.Name!="h2")return null;for(var n=heading.NextSibling;n!=null;n=n.NextSibling){if(n.Name=="h2")break;if(n.Name=="table")return n;}return null;}
 static IEnumerable<HtmlNode> Rows(HtmlNode? table)=>table?.SelectNodes(".//tr[td]")??Enumerable.Empty<HtmlNode>();
 static string? Link(HtmlNode a){string href=a.GetAttributeValue("href","");if(!href.StartsWith("/wiki/",StringComparison.Ordinal)||href.Contains(':')||href.Contains('?')||href.Contains('#'))return null;return Host+href;}
 static IEnumerable<(string Name,string Url)> FirstColumn(HtmlNode? table){foreach(var row in Rows(table)){var cell=row.SelectSingleNode("./td[1]");foreach(var a in cell?.SelectNodes(".//a[@href]")??Enumerable.Empty<HtmlNode>()){string name=PublicPages.Text(a);var url=Link(a);if(url!=null&&name.Length>0&&ItemFinder.HasEnglishDisplayName(name))yield return(name,url);}}}
 public static ItemReference ReadItem(string html,string name){
  var root=Root(html,name);if(root.SelectSingleNode("//a[@href='/wiki/Category:Items']")==null)throw new FormatException("Not a wiki item page");
  var fields=new List<KeyValuePair<string,string>>();
  foreach(var row in root.SelectNodes("//div[@class='infobox-row']")??Enumerable.Empty<HtmlNode>()){
   string label=PublicPages.Text(row.SelectSingleNode("./div[@class='infobox-label']")),value=PublicPages.Text(row.SelectSingleNode("./div[@class='infobox-value']"));if(label.Length>0&&value.Length>0)fields.Add(new(label,value));
  }
  foreach(var row in root.SelectNodes("//*[contains(concat(' ',@class,' '),' infobox-stat-cell ')]")??Enumerable.Empty<HtmlNode>()){
   var label=PublicPages.Text(row.SelectSingleNode(".//*[contains(@class,'stat-label')]"));var value=PublicPages.Text(row.SelectSingleNode(".//*[contains(@class,'stat-value')]"));if(label.Length>0&&value.Length>0)fields.Add(new(label,value));
  }
  string footer=PublicPages.Text(root.SelectSingleNode("//div[@class='infobox-footer']"));
  return new(name,"MapleClassic Wiki",ItemUrl(name),PublicPages.Text(root.SelectSingleNode("//div[@class='infobox-description']")),fields,footer+". "+Credit);
 }
 public static IReadOnlyList<DropMonster> ReadDrops(string html,string name){var root=Root(html,name);ReadItem(html,name);return FirstColumn(Table(root,"Dropped_by")).Distinct().Select(m=>new DropMonster(m.Name,m.Url,"MapleClassic Wiki • Source-reported drop; Classic coverage may change",[])).ToArray();}
 public static DropMonster ReadMaps(string html,DropMonster monster){var root=Root(html,monster.Name);return monster with{Locations=FirstColumn(Table(root,"Spawns")).Distinct().Select(m=>new DropLocation(m.Name,m.Url)).ToArray()};}
 static decimal? Price(string text){var match=Regex.Match(text.Trim(),@"^([0-9][0-9,]*)(?: mesos)?$");return match.Success&&decimal.TryParse(match.Groups[1].Value,NumberStyles.AllowThousands,CultureInfo.InvariantCulture,out var n)?n:null;}
 public static NpcPrices ReadNpcPrices(string html,string name){var details=ReadItem(html,name);var root=Root(html,name);var sell=Price(details.Fields.FirstOrDefault(x=>x.Key=="Sell price").Value??"");var buys=new List<decimal>();var table=Table(root,"Sold_by");
  var headings=table?.SelectNodes(".//tr[1]/th")?.Select(PublicPages.Text).ToList();int index=headings?.FindIndex(x=>x=="Price")??-1;
  if(index>=0)foreach(var row in Rows(table)){var cells=row.SelectNodes("./td");if(cells!=null&&index<cells.Count&&Price(PublicPages.Text(cells[index])) is decimal n)buys.Add(n);}
  return new(name,details.Url,sell,buys.Count>0?buys.Min():null,buys.Count>0?buys.Max():null,DateTimeOffset.UtcNow){Source=details.Source,Note=details.Notice};
 }
 public async Task<ItemReference> LookupItem(string name,CancellationToken ct)=>ReadItem(await client.Get(ItemUrl(name),ct),name);
 public async Task<NpcPrices> LookupNpcPrices(string name,CancellationToken ct)=>ReadNpcPrices(await client.Get(ItemUrl(name),ct),name);
 public async Task<ItemDrops> Lookup(string name,CancellationToken ct){
  string html=await client.Get(ItemUrl(name),ct);var all=ReadDrops(html,name);var monsters=new List<DropMonster>();bool partial=false;
  foreach(var m in all.Take(5)){try{monsters.Add(ReadMaps(await client.Get(m.Url,ct),m));}catch(Exception e) when(e is HttpRequestException or IOException or FormatException){monsters.Add(m);partial=true;}}
  return new(name,ItemUrl(name),$"Showing {monsters.Count} of {all.Count} recorded monsters. "+(all.Count==0?"No drops recorded here; this is not proof the item has no drops. ":"")+(partial?"Some maps could not load. ":"")+Credit,monsters,DateTimeOffset.UtcNow);
 }
 public void Dispose()=>client.Dispose();
}
