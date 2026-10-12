using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly Label lookupStatus=new(){AutoSize=true,MaximumSize=new Size(480,0),Margin=new Padding(5,8,5,5),Text="Search an item to check public prices."};
 readonly System.Windows.Forms.Timer itemLookupTimer=new(){Interval=800};
 string queuedLookup="",pendingLookup="",lastLookup="";DateTimeOffset lastLookupAt;
 string ResolvePriceQuery(string query){
  query=features.Polish.Resolve(query).Trim();
  return iconCatalog.Entries.FirstOrDefault(e=>e.Id==query)?.Name??query;
 }
 void InitializeItemLookup(){
  itemLookupTimer.Tick+=async(_,_)=>{itemLookupTimer.Stop();var query=queuedLookup;queuedLookup="";if(query.Length<3||closing)return;npcKey="";QueueNpcPrices();RememberPriceSearch(query);lastLookup=query;lastLookupAt=DateTimeOffset.UtcNow;await RefreshSources(true,query);};
  FormClosed+=(_,_)=>{itemLookupTimer.Stop();itemLookupTimer.Dispose();};
 }
 void QueueItemLookup(string query){
  itemLookupTimer.Stop();queuedLookup="";query=ResolvePriceQuery(query);
  if(Offline||closing||query.Length<3){lookupStatus.Text=Offline?"Offline • showing saved prices":"Enter at least 3 characters or an item ID.";pendingSearch=false;pendingLookup="";return;}
  if(query==lastLookup&&DateTimeOffset.UtcNow-lastLookupAt<TimeSpan.FromMinutes(2)){lookupStatus.Text="Recently checked • showing cached prices for "+query;return;}
  queuedLookup=query;lookupStatus.Text="Ready to look up: "+query;itemLookupTimer.Start();
 }
 async Task LookupPricesNow(){
  itemLookupTimer.Stop();queuedLookup="";var query=ResolvePriceQuery(SelectedPriceItem()?.Name??search.Text);
  if(query.Length==0){footer.Text="Enter an item name or ID to look up prices.";return;}
  npcKey="";QueueNpcPrices();RememberPriceSearch(query);lastLookup=query;lastLookupAt=DateTimeOffset.UtcNow;await RefreshSources(true,query);
 }
}
