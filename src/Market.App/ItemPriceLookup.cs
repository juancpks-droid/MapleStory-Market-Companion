using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly System.Windows.Forms.Timer itemLookupTimer=new(){Interval=800};
 string queuedLookup="",pendingLookup="",lastLookup="";DateTimeOffset lastLookupAt;
 string ResolvePriceQuery(string query){
  query=features.Polish.Resolve(query).Trim();
  return iconCatalog.Entries.FirstOrDefault(e=>e.Id==query)?.Name??query;
 }
 void InitializeItemLookup(){
  itemLookupTimer.Tick+=async(_,_)=>{itemLookupTimer.Stop();var query=queuedLookup;queuedLookup="";if(query.Length<3||closing)return;lastLookup=query;lastLookupAt=DateTimeOffset.UtcNow;await RefreshSources(true,query);};
  FormClosed+=(_,_)=>{itemLookupTimer.Stop();itemLookupTimer.Dispose();};
 }
 void QueueItemLookup(string query){
  itemLookupTimer.Stop();queuedLookup="";query=ResolvePriceQuery(query);
  if(Offline||closing||query.Length<3){pendingSearch=false;pendingLookup="";return;}
  if(query==lastLookup&&DateTimeOffset.UtcNow-lastLookupAt<TimeSpan.FromMinutes(2))return;
  queuedLookup=query;itemLookupTimer.Start();
 }
 async Task LookupPricesNow(){
  itemLookupTimer.Stop();queuedLookup="";var query=ResolvePriceQuery(SelectedPriceItem()?.Name??search.Text);
  if(query.Length==0){footer.Text="Enter an item name or ID to look up prices.";return;}
  lastLookup=query;lastLookupAt=DateTimeOffset.UtcNow;await RefreshSources(true,query);
 }
}
