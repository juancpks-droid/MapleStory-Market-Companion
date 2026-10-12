using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly Label npcPriceNote=Info(58);
 string npcKey="",npcUrl="";int npcGeneration;
 CancellationTokenSource? npcStop;
 void BuildNpcReference(Panel page){
  var panel=new Panel(){Dock=DockStyle.Top,Height=58};npcPriceNote.Dock=DockStyle.Fill;panel.Controls.Add(npcPriceNote);
  var button=Button("NPC source / retry",()=>{if(npcUrl.Length>0&&!Offline)Open(npcUrl);else{npcKey="";QueueNpcPrices();}});button.Dock=DockStyle.Right;button.Width=130;panel.Controls.Add(button);npcPriceNote.BringToFront();
  page.Controls.Add(panel);panel.BringToFront();priceDetails.BringToFront();
  FormClosed+=(_,_)=>{npcStop?.Cancel();npcStop?.Dispose();};
 }
 void ShowNpcPrices(NpcPrices prices){
   string Price(decimal? p)=>p is null?"Not available":TextKey.Mesos(p)+" mesos";
   npcPriceNote.Text=$"Sell to NPC: {Price(prices.Sell)} • Buy from NPC: {Price(prices.BuyMin)}"+(prices.BuyMax>prices.BuyMin?" – "+Price(prices.BuyMax):"")+"\nCCPLZ reference • May differ by server/version. Not player market prices. Checked "+prices.CheckedAt.ToLocalTime().ToString("g");
 }
 async void QueueNpcPrices(){
  var selectedItem=SelectedPriceItem();var query=features.Polish.Resolve(search.Text).Trim();
  var catalog=iconCatalog.Entries.FirstOrDefault(e=>e.Id==query||TextKey.Search(e.Name)==TextKey.Search(query));
  string name=selectedItem?.Name??catalog?.Name??"",id=selectedItem?.ItemId??catalog?.Id??"";
  string key=name+"|"+id+"|"+Offline;if(key==npcKey)return;npcKey=key;npcUrl="";int generation=++npcGeneration;
  npcStop?.Cancel();npcStop?.Dispose();npcStop=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);var ct=npcStop.Token;
  if(name.Length==0){npcPriceNote.Text="NPC reference: select an item or enter its exact name / ID. Player listings may be unavailable.";return;}
  if(Offline){npcPriceNote.Text="NPC reference: unavailable offline. Turn off Work offline to look up NPC prices.";return;}
  npcPriceNote.Text=name+" • Looking up NPC reference prices…";
  try{
   await Task.Delay(650,ct);var prices=await ccplzReader.LookupNpcPrices(name,id,ct);
   if(closing||IsDisposed||generation!=npcGeneration||Offline)return;npcUrl=prices.Url;
   ShowNpcPrices(prices);
  }catch(OperationCanceledException){}catch(Exception){if(!closing&&!IsDisposed&&generation==npcGeneration)npcPriceNote.Text="NPC prices: Not available — reference missing, item mismatch or source unavailable. Use NPC source / retry.";}
 }
}
