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
 string? displayedPriceQuery;
 bool priceInputPending,priceLookupLoading;
 int priceInputGeneration;
 string DisplayedPriceQuery=>displayedPriceQuery??ResolvePriceQuery(search.Text);
 bool HoldPriceResults=>priceInputPending||priceLookupLoading;
 void CommitPriceView(string query){
  displayedPriceQuery=query;priceInputPending=false;priceLookupLoading=false;
  RenderPrices();
 }
 void InitializeItemLookup(){
  priceRenderTimer.Tick+=(_,_)=>{priceRenderTimer.Stop();if(!closing&&!HoldPriceResults)RenderPrices();};
  itemLookupTimer.Interval=700;
  itemLookupTimer.Tick+=async(_,_)=>{itemLookupTimer.Stop();var query=queuedLookup;queuedLookup="";if(!closing)await BeginPriceLookup(query,false);};
  FormClosed+=(_,_)=>{itemLookupTimer.Stop();itemLookupTimer.Dispose();priceRenderTimer.Stop();priceRenderTimer.Dispose();};
 }
 void QueueItemLookup(string query){
  // Text edits invalidate in-flight work immediately, but leave the visible results alone.
  displayedPriceQuery??="";priceInputGeneration++;priceInputPending=true;priceLookupLoading=false;
  itemLookupTimer.Stop();priceRenderTimer.Stop();queuedLookup="";pendingSearch=false;pendingLookup="";
  npcGeneration++;npcStop?.Cancel();npcKey="";
  if(activePriceSearch){priceRefreshGeneration++;refreshStop?.Cancel();}
  query=ResolvePriceQuery(query);
  if(closing)return;
  queuedLookup=query;
  lookupStatus.Text="Finish typing or press Enter • Previous results stay visible";
  itemLookupTimer.Start();
 }
 async Task BeginPriceLookup(string query,bool force){
  itemLookupTimer.Stop();priceRenderTimer.Stop();queuedLookup="";
  priceSuggestions.Visible=false;suggestionTimer.Stop();query=ResolvePriceQuery(query);
  if(query.Length==0){CommitPriceView("");lookupStatus.Text="Enter an item to check prices.";return;}
  if(query.Length<3){priceInputPending=false;lookupStatus.Text="Enter at least 3 characters or an item ID • Previous results retained";return;}
  if(Offline){CommitPriceView(query);lookupStatus.Text="Offline • Saved results for "+query;return;}
  if(!force&&completedLookups.TryGetValue(query,out var checkedAt)&&DateTimeOffset.UtcNow-checkedAt<TimeSpan.FromMinutes(2)){
   CommitPriceView(query);lookupStatus.Text="Cached • checked "+checkedAt.ToLocalTime().ToString("t")+" • "+query;return;
  }
  priceInputPending=false;priceLookupLoading=true;
  RememberPriceSearch(query);lastLookup=query;lastLookupAt=DateTimeOffset.UtcNow;
  lookupStatus.Text="Searching for "+query+" • Previous results stay visible";
  await RefreshSources(true,query);
 }
 async Task LookupPricesNow(){
  // Never use the selected row here: it may belong to the previous visible search.
  await BeginPriceLookup(search.Text,true);
 }
}
