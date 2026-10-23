using Market.Core;
namespace Market.App;
public sealed partial class MainForm{
 readonly NiaMarketClient niaMarketClient=new();
 readonly Label niaNote=new(){Dock=DockStyle.Top,Height=110,Padding=new Padding(8)};
 readonly DataGridView niaGrid=Grid();
 string niaKey="";NiaMarketResult? niaResult;CancellationTokenSource? niaRequest;
 void BuildNiaPrices(){var tab=new TabPage("NiaMeowDB prices & offers");niaGrid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;niaGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;foreach(var title in new[]{"Side","Listed price","Quantity","Seller","Condition / stats","Location","Updated","Note"})niaGrid.Columns.Add(title,title);var bar=Toolbar();bar.Controls.Add(Button("Retry NiaMeowDB",()=>{niaKey="";QueueNiaPrices();}));bar.Controls.Add(Button("Open NiaMeowDB source",()=>{if(niaResult is {} r)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(r.Url){UseShellExecute=true});}));tab.Controls.Add(niaGrid);tab.Controls.Add(niaNote);tab.Controls.Add(bar);priceDetails.TabPages.Add(tab);FormClosed+=(_,_)=>{niaRequest?.Cancel();niaMarketClient.Dispose();};}
 async void QueueNiaPrices(){if(HoldPriceResults||closing)return;string name=SelectedPriceItem()?.Name??DisplayedPriceQuery,key=name+"|"+Offline;if(key==niaKey)return;niaKey=key;niaRequest?.Cancel();niaRequest=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);niaResult=null;niaGrid.Rows.Clear();if(name.Length<3){niaNote.Text="Choose an item to check NiaMeowDB.";return;}niaNote.Text="Checking NiaMeowDB prices and offers…";
  try{var result=await niaMarketClient.Lookup(dataDirectory,name,Offline,niaRequest.Token);if(closing||IsDisposed||key!=niaKey)return;niaResult=result;if(result is null){niaNote.Text="NiaMeowDB could not return a verified match or is unavailable. Existing sources and NPC prices remain available.";return;}
   string Price(decimal? n)=>n is {} value?value.ToString("N0")+" mesos":"Not available";
   niaNote.Text=$"NiaMeowDB · {result.Name} · Checked {result.CheckedAt.ToLocalTime():g}"+(result.CheckedAt<DateTimeOffset.UtcNow.AddMinutes(-15)?" · Older saved result":"")+$"\nUsual clean price: {Price(result.Usual)} · Lowest clean offer: {Price(result.Lowest)} · Best clean buy: {Price(result.BestBuy)}\nSource summaries; equipment stats vary. Listings include clean and scrolled copies. Availability is unverified.\nNewest price report: {result.Newest?.ToLocalTime().ToString("g")??"Unknown"}. "+(result.Partial?"Partial results. ":"")+"Full price history requires the source website.";
   foreach(var o in result.Offers)niaGrid.Rows.Add(o.Side=="sell"?"SELL":"BUY",o.Price is null?"Make an offer":Price(o.Price),o.Quantity?.ToString("N0")??"Unknown",o.Seller,o.Condition+" · "+o.Stats,o.Location,o.At?.ToLocalTime().ToString("g")??"Unknown",o.Note);
   if(result.Offers.Count==0)niaNote.Text+="\nNo active offers returned.";
  }catch(OperationCanceledException){}catch(Exception){if(!closing&&key==niaKey)niaNote.Text="NiaMeowDB unavailable. Retry later; existing prices remain available.";}
 }
}
