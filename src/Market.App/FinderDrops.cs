using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly TabControl finderDetailTabs=new(){Dock=DockStyle.Fill};
 readonly DataGridView finderDrops=Grid();
 readonly Label finderDropNote=Info(75);
 readonly DropReferenceReader dropReader=new();
 CancellationTokenSource? dropLookupStop;
 string dropItemKey="",dropSourceUrl="";int dropGeneration;
 Control BuildFinderDetails(Control listings){
  var asks=new TabPage("Market listings");asks.Controls.Add(listings);finderDetailTabs.TabPages.Add(asks);
  var drops=new TabPage("Monster drops & locations");
  var actions=Toolbar();actions.Height=40;
  actions.Controls.Add(Button("Load / retry",()=>{dropItemKey="";QueueFinderDrops();}));
  actions.Controls.Add(Button("Source: NiaMeowDB",()=>{if(dropSourceUrl.Length>0&&!Offline)OpenDropReference(dropSourceUrl);}));
  drops.Controls.Add(finderDrops);drops.Controls.Add(finderDropNote);drops.Controls.Add(actions);finderDetailTabs.TabPages.Add(drops);
  finderDrops.DefaultCellStyle.WrapMode=DataGridViewTriState.True;finderDrops.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
  SetColumns(finderDrops,["Monster","Where to find it (click for maps)","Evidence"]);
  finderDrops.Columns[0].FillWeight=22;finderDrops.Columns[1].FillWeight=48;finderDrops.Columns[2].FillWeight=30;foreach(DataGridViewColumn column in finderDrops.Columns)column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
  finderDrops.CellClick+=(_,e)=>{if(Offline||e.RowIndex<0||finderDrops.Rows[e.RowIndex].Tag is not DropMonster monster)return;
   if(e.ColumnIndex==1&&monster.Locations.Count>0){var menu=new ContextMenuStrip();foreach(var map in monster.Locations)menu.Items.Add(map.Name,null,(_,_)=>OpenDropReference(map.Url));menu.Closed+=(_,_)=>menu.Dispose();menu.Show(Cursor.Position);}
   else if(e.ColumnIndex==0)OpenDropReference(monster.Url);
  };
  finderDetailTabs.SelectedIndexChanged+=(_,_)=>QueueFinderDrops();
  FormClosed+=(_,_)=>{dropLookupStop?.Cancel();dropLookupStop?.Dispose();dropReader.Dispose();};
  finderDropNote.Text="Select an item and open this tab to load drop references. No drop rates are inferred.";
  return finderDetailTabs;
 }
 void OpenDropReference(string url){if(!Offline)new WebsiteViewer(new("NiaMeowDB • Drop reference",url,FinderSelection?.Name??"",false,"Community reports and historical references are labelled separately. Check source coverage."),dataDirectory).Show(this);}
 async void QueueFinderDrops(){
  if(finderDetailTabs.SelectedIndex!=1||selected!="Item Finder")return;
  var item=FinderSelection;string key=item is null?"":item.Id+"|"+item.Name;
  if(key==dropItemKey&&key.Length>0)return;
  dropItemKey=key;int generation=++dropGeneration;dropLookupStop?.Cancel();dropLookupStop?.Dispose();dropLookupStop=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);var ct=dropLookupStop.Token;
  finderDrops.Rows.Clear();dropSourceUrl="";
  if(item is null){finderDropNote.Text="Select an item to see monster drops and locations.";return;}
  if(Offline){dropItemKey="";finderDropNote.Text="Offline: turn off Work offline, then select Load / retry to look up drop references.";return;}
  finderDropNote.Text="Looking up "+item.Name+"… Exact-name matching; source requests are spaced to avoid overloading the website.";
  try{
   await Task.Delay(500,ct);var result=await dropReader.Lookup(item.Name,ct);
   if(closing||IsDisposed||generation!=dropGeneration||Offline)return;
   dropSourceUrl=result.SourceUrl;finderDropNote.Text=result.Item+" • NiaMeowDB • Checked "+result.CheckedAt.ToLocalTime().ToString("g")+"\n"+result.Message;
   foreach(var monster in result.Monsters){int row=finderDrops.Rows.Add(monster.Name,monster.Locations.Count==0?"No recorded map information":string.Join("\n",monster.Locations.Select(m=>m.Name)),monster.Evidence);finderDrops.Rows[row].Tag=monster;}
   finderDrops.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
  }catch(OperationCanceledException){}
  catch(Exception){if(!closing&&!IsDisposed&&generation==dropGeneration){dropItemKey="";finderDropNote.Text="Drop reference unavailable. Select Load / retry later, or use the website reference button. This does not mean the item has no drops.";}}
 }
}


