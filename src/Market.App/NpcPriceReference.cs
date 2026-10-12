using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly Label npcPriceNote=Info(58);
 string npcKey="",npcUrl="";int npcGeneration;
 CancellationTokenSource? npcStop;
 void BuildNpcReference(Panel page){
  var panel=new Panel(){Dock=DockStyle.Top,Height=58};npcPriceNote.Dock=DockStyle.Fill;panel.Controls.Add(npcPriceNote);
  var button=Button("Retry NPC prices",()=>{npcKey="";QueueNpcPrices();});button.Dock=DockStyle.Right;button.Width=130;panel.Controls.Add(button);npcPriceNote.BringToFront();FitPriceText(npcPriceNote,panel);
  page.Controls.Add(panel);panel.BringToFront();priceDetails.BringToFront();
  FormClosed+=(_,_)=>{npcStop?.Cancel();npcStop?.Dispose();};
 }
 void ShowNpcPrices(NpcPrices prices){
   UpdateNpcPriceMetrics(prices);
   npcPriceNote.Text=prices.Sell is null&&prices.BuyMin is null?"No NPC prices recorded by "+prices.Source+". Try again later.":"NPC reference from "+prices.Source+" • Separate from player prices and market history.";
 }
 async void QueueNpcPrices(){
  var selectedItem=SelectedPriceItem();var query=features.Polish.Resolve(search.Text).Trim();
  var catalog=iconCatalog.Entries.FirstOrDefault(e=>e.Id==query||TextKey.Search(e.Name)==TextKey.Search(query));
  string name=selectedItem?.Name??catalog?.Name??"",id=selectedItem?.ItemId??catalog?.Id??"";
  string key=name+"|"+id+"|"+Offline;if(key==npcKey)return;npcKey=key;npcUrl="";UpdateNpcPriceMetrics(null);int generation=++npcGeneration;
  npcStop?.Cancel();npcStop?.Dispose();npcStop=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);npcStop.CancelAfter(TimeSpan.FromSeconds(90));var ct=npcStop.Token;
  if(name.Length==0){npcPriceNote.Text="NPC reference: select an item or enter its exact name / ID. Player listings may be unavailable.";return;}
  if(Offline){npcPriceNote.Text="NPC reference: unavailable offline. Turn off Work offline to look up NPC prices.";return;}
  npcPriceNote.Text=name+" • Looking up NPC reference prices…";
  try{
   await Task.Delay(650,ct);var prices=await NpcPriceFallback.Lookup(c=>ccplzReader.LookupNpcPrices(name,id,c),c=>questDropReader.LookupNpcPrices(name,c),ct,()=>{if(!closing&&!IsDisposed&&generation==npcGeneration)npcPriceNote.Text=name+" • Trying MapleStory Quest NPC prices…";});
   if(closing||IsDisposed||generation!=npcGeneration||Offline)return;npcUrl=prices.Url;
   ShowNpcPrices(prices);
  }catch(OperationCanceledException){if(!closing&&!IsDisposed&&generation==npcGeneration&&!Offline){npcKey="";npcPriceNote.Text="NPC lookup timed out. Use Retry NPC prices.";}}catch(Exception){if(!closing&&!IsDisposed&&generation==npcGeneration){npcKey="";npcPriceNote.Text="NPC prices: unavailable from CCPLZ and MapleStory Quest. No verified matching price was received. Use Retry NPC prices.";}}
 }
}
