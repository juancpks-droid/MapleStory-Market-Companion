using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly Label lookupStatus=new(){AutoSize=true,MaximumSize=new Size(480,0),Margin=new Padding(5,8,5,5),Text="Search an item to check public prices."};
 readonly System.Windows.Forms.Timer priceRenderTimer=new(){Interval=150};
 void QueuePriceRender(){priceRenderTimer.Stop();priceRenderTimer.Start();}
 readonly System.Windows.Forms.Timer itemLookupTimer=new(){Interval=500};
 string queuedLookup="",pendingLookup="",lastLookup="";DateTimeOffset lastLookupAt;
 string ResolvePriceQuery(string query){
  query=features.Polish.Resolve(query).Trim();
  var entry=iconCatalog.Entries.FirstOrDefault(e=>e.Id==query);
  if(entry is not null&&ScrollNames.IsScroll(entry.Name)&&!ScrollNames.HasVerifiedLegacyRate(entry.Id,entry.Name))return entry.Name+" (unverified success rate; choose a percentage)";
  return ScrollNames.Query(entry?.Name??query);
 }
 void InitializeItemLookup(){priceRenderTimer.Tick+=(_,_)=>{priceRenderTimer.Stop();if(!closing)RenderPrices();};
  itemLookupTimer.Tick+=async(_,_)=>{itemLookupTimer.Stop();var query=queuedLookup;queuedLookup="";if(query.Length<3||closing)return;npcKey="";QueueNpcPrices();RememberPriceSearch(query);lastLookup=query;lastLookupAt=DateTimeOffset.UtcNow;await RefreshSources(true,query);};
  FormClosed+=(_,_)=>{itemLookupTimer.Stop();itemLookupTimer.Dispose();priceRenderTimer.Stop();priceRenderTimer.Dispose();};
 }
 void QueueItemLookup(string query){
  itemLookupTimer.Stop();queuedLookup="";query=ResolvePriceQuery(query);
  if(activePriceSearch&&!string.Equals(query,activeSearchQuery,StringComparison.OrdinalIgnoreCase)){refreshStop?.Cancel();pendingSearch=false;pendingLookup="";}
  if(Offline||closing||query.Length<3){lookupStatus.Text=Offline?"Offline • showing saved prices":"Enter at least 3 characters or an item ID.";pendingSearch=false;pendingLookup="";return;}
  if(completedLookups.TryGetValue(query,out var checkedAt)&&DateTimeOffset.UtcNow-checkedAt<TimeSpan.FromMinutes(2)){lookupStatus.Text="Cached • checked "+checkedAt.ToLocalTime().ToString("t")+" • "+query;return;}
  var saved=observations.Where(o=>TextKey.Search(o.Name)==TextKey.Search(query)).ToArray();queuedLookup=query;lookupStatus.Text=saved.Length>0?$"Showing {saved.Length} saved records • checked {saved.Max(o=>o.RetrievedAt).ToLocalTime():g} • refreshing…":"Ready to look up: "+query;itemLookupTimer.Start();
 }
 async Task LookupPricesNow(){
  priceRenderTimer.Stop();RenderPrices();priceSuggestions.Visible=false;suggestionTimer.Stop();itemLookupTimer.Stop();queuedLookup="";var query=ResolvePriceQuery(SelectedPriceItem()?.Name??search.Text);
  if(query.Length==0){footer.Text="Enter an item name or ID to look up prices.";return;}
  npcKey="";QueueNpcPrices();RememberPriceSearch(query);lastLookup=query;lastLookupAt=DateTimeOffset.UtcNow;await RefreshSources(true,query);
 }
}
