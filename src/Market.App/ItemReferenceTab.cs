using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly DataGridView itemReferenceGrid=Grid();readonly Label itemReferenceNote=Info(100);
 CancellationTokenSource? itemReferenceStop;int itemReferenceGeneration;string itemReferenceKey="";
 void BuildItemReferenceTab(){
  var page=new TabPage("Item details");SetColumns(itemReferenceGrid,["Detail","Value"]);itemReferenceGrid.Columns[0].FillWeight=30;itemReferenceGrid.Columns[1].FillWeight=70;
  itemReferenceGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;itemReferenceGrid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
  var actions=Toolbar();actions.Controls.Add(Button("Reload details",()=>{itemReferenceKey="";QueueItemReference();}));
  actions.Controls.Add(Button("Reference credits",()=>MessageBox.Show(this,WikiReferenceReader.Credit+"\n\nSource: https://mapleclassic.wiki\nSelected information is reformatted for this free companion. Wiki-derived content remains CC BY-NC-SA 4.0; it is not covered by the app's code license.\n\nHistorical API: https://maplestory.io — GMS version 83, not Classic World. Game data belongs to Nexon.","Item reference credits")));
  page.Controls.Add(itemReferenceGrid);FitPriceText(itemReferenceNote);page.Controls.Add(itemReferenceNote);page.Controls.Add(actions);finderDetailTabs.TabPages.Add(page);
  itemReferenceNote.Text="Select an item to load its details inside the app.";
 }
 async void QueueItemReference(){
  if(selected!="Item Finder"||finderDetailTabs.SelectedIndex!=2){itemReferenceStop?.Cancel();itemReferenceGeneration++;itemReferenceKey="";return;}
  var item=FinderSelection;string key=(item?.Id??"")+"|"+Offline;if(key==itemReferenceKey)return;itemReferenceKey=key;int generation=++itemReferenceGeneration;
  itemReferenceStop?.Cancel();itemReferenceStop?.Dispose();itemReferenceStop=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);itemReferenceStop.CancelAfter(TimeSpan.FromSeconds(55));var ct=itemReferenceStop.Token;itemReferenceGrid.Rows.Clear();
  if(item==null){itemReferenceNote.Text="Select an item to see its details.";return;}
  if(Offline){itemReferenceNote.Text="Offline: connect to load reference details.";return;}
  itemReferenceNote.Text=item.Name+" • Looking up item details…";
  try{
   await Task.Delay(500,ct);ItemReference details;
   try{details=await wikiReader.LookupItem(item.Name,ct);}
   catch(Exception e) when(!ct.IsCancellationRequested&&e is HttpRequestException or IOException or FormatException){details=await ioReferenceReader.Lookup(item.Name,item.Id,ct);}
   if(closing||IsDisposed||generation!=itemReferenceGeneration||Offline)return;
   itemReferenceNote.Text=details.Name+" • "+details.Source+"\n"+details.Notice;
   if(details.Description.Length>0)itemReferenceGrid.Rows.Add("Description",details.Description);
   foreach(var field in details.Fields)itemReferenceGrid.Rows.Add(field.Key,field.Value);
   itemReferenceGrid.Rows.Add("Source",details.Url);
  }catch(OperationCanceledException){if(!closing&&!IsDisposed&&generation==itemReferenceGeneration){itemReferenceKey="";itemReferenceNote.Text="Item lookup cancelled or timed out. Use Reload details.";}}
  catch(Exception){if(!closing&&!IsDisposed&&generation==itemReferenceGeneration){itemReferenceKey="";itemReferenceNote.Text="Item details unavailable. No verified matching reference was received. Use Reload details to retry.";}}
 }
}
