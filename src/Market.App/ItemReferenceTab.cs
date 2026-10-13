using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly DataGridView itemReferenceGrid=Grid();readonly Label itemReferenceNote=Info(100);
 readonly GameGuideReader gameGuideReader=new();readonly Stack<string> guideHistory=new();
 string guidePath="",guideSelection="";bool guideRefresh;System.Windows.Forms.Button? guideBack;
 CancellationTokenSource? itemReferenceStop;int itemReferenceGeneration;string itemReferenceKey="";
 void BuildItemReferenceTab(){
  var page=new TabPage("Item details & guide");SetColumns(itemReferenceGrid,["Detail / related reference","Value (double-click a related reference)"]);itemReferenceGrid.Columns[0].FillWeight=40;itemReferenceGrid.Columns[1].FillWeight=60;
  itemReferenceGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;itemReferenceGrid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCells;
  var actions=Toolbar();guideBack=Button("← Back",()=>{if(guideHistory.TryPop(out var path)){guidePath=path;itemReferenceKey="";QueueItemReference();}});guideBack.Enabled=false;actions.Controls.Add(guideBack);
  actions.Controls.Add(Button("Selected item",()=>{guidePath="";guideHistory.Clear();itemReferenceKey="";QueueItemReference();}));
  actions.Controls.Add(Button("Reload details",()=>{guideRefresh=true;itemReferenceKey="";QueueItemReference();}));
  actions.Controls.Add(Button("Browse references",BrowseGameReferences));
  actions.Controls.Add(Button("Reference credits",()=>MessageBox.Show(this,"Shared field guide: CCPLZ factual references with source and checked date. Historical and reported information may differ from your server.\n\n"+WikiReferenceReader.Credit+"\nWiki fallback: https://mapleclassic.wiki — CC BY-NC-SA 4.0. Historical fallback: https://maplestory.io, GMS 83. Game data © Nexon. Reference data updates separately from the application; saved pages work offline.","Reference credits")));
  itemReferenceGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex<0||itemReferenceGrid.Rows[e.RowIndex].Tag is not GuideLink link)return;guideHistory.Push(guidePath);guidePath=link.Path;itemReferenceKey="";QueueItemReference();};
  page.Controls.Add(itemReferenceGrid);FitPriceText(itemReferenceNote);page.Controls.Add(itemReferenceNote);page.Controls.Add(actions);finderDetailTabs.TabPages.Add(page);
  itemReferenceNote.Text="Item stats, monsters, maps, shops, quests and crafting. Open a related row to explore inside the app.";
  FormClosed+=(_,_)=>gameGuideReader.Dispose();
 }
 async void BrowseGameReferences(){
  using var dialog=new Form{Text="Browse game references",Size=new Size(650,560),MinimumSize=new Size(420,380),StartPosition=FormStartPosition.CenterParent,BackColor=BackColor,ForeColor=ForeColor};
  using var cancel=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
  var query=new TextBox{Dock=DockStyle.Top,PlaceholderText="Search a monster, map, NPC, shop, quest or recipe",BackColor=BackColor,ForeColor=ForeColor};
  var kind=new ComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=BackColor,ForeColor=ForeColor};kind.Items.AddRange(new object[]{"all","item","monster","map","npc","shop","quest","recipe"});kind.SelectedIndex=0;
  var list=new ListBox{Dock=DockStyle.Fill,BackColor=BackColor,ForeColor=ForeColor,DisplayMember="Name"};
  var note=new Label{Dock=DockStyle.Bottom,Height=48,Text="Loading directory… Saved references remain available offline.",Padding=new Padding(8)};
  var open=new System.Windows.Forms.Button{Text="Open selected reference",Dock=DockStyle.Bottom,Height=36};
  IReadOnlyList<GuideEntry> entries=[];
  void Filter(){list.BeginUpdate();list.Items.Clear();foreach(var e in GameGuideReader.SearchDirectory(entries,query.Text,kind.Text))list.Items.Add(e);list.EndUpdate();if(list.Items.Count>0)list.SelectedIndex=0;note.Text=$"{list.Items.Count} shown • {entries.Count} available pages • partial coverage, grows daily";}
  void Choose(){if(list.SelectedItem is not GuideEntry entry)return;guideHistory.Push(guidePath);guidePath=entry.Path;itemReferenceKey="";dialog.DialogResult=DialogResult.OK;dialog.Close();QueueItemReference();}
  query.TextChanged+=(_,_)=>Filter();kind.SelectedIndexChanged+=(_,_)=>Filter();list.DoubleClick+=(_,_)=>Choose();open.Click+=(_,_)=>Choose();
  dialog.Controls.Add(list);dialog.Controls.Add(query);dialog.Controls.Add(kind);dialog.Controls.Add(note);dialog.Controls.Add(open);
  dialog.Shown+=async(_,_)=>{try{entries=await gameGuideReader.DirectoryEntries(Offline,cancel.Token);if(!dialog.IsDisposed)Filter();}catch(OperationCanceledException){}};
  dialog.FormClosed+=(_,_)=>cancel.Cancel();dialog.ShowDialog(this);await Task.CompletedTask;
 }
 void ShowGameGuide(GameGuide guide){
  itemReferenceGrid.Rows.Clear();itemReferenceNote.Text=guide.Name+" • "+guide.Source+" • "+guide.Kind+" • Checked "+guide.CheckedAt.ToLocalTime().ToString("g")+"\n"+(guide.CheckedAt<DateTimeOffset.UtcNow.AddDays(-7)?"Older saved reference. ":"")+guide.Notice;
  foreach(var field in guide.Fields)itemReferenceGrid.Rows.Add(field.Label,field.Value);
  foreach(var section in guide.Sections){itemReferenceGrid.Rows.Add(section.Title,section.Total+" references"+(section.Total>section.Links.Count?" • showing "+section.Links.Count:""));foreach(var link in section.Links){int row=itemReferenceGrid.Rows.Add("↳ "+link.Name,link.Detail.Length>0?link.Detail:"Open in guide");itemReferenceGrid.Rows[row].Tag=link;}}
  itemReferenceGrid.Rows.Add("Source",guide.SourceUrl);
 }
 async void QueueItemReference(){
  if(selected!="Item Finder"||finderDetailTabs.SelectedIndex!=2){itemReferenceStop?.Cancel();itemReferenceGeneration++;itemReferenceKey="";return;}
  var item=FinderSelection;if(item is not null&&item.Id!=guideSelection){guideSelection=item?.Id??"";guidePath="";guideHistory.Clear();}
  string key=(item?.Id??"")+"|"+Offline+"|"+guidePath;if(key==itemReferenceKey)return;itemReferenceKey=key;int generation=++itemReferenceGeneration;
  itemReferenceStop?.Cancel();itemReferenceStop?.Dispose();itemReferenceStop=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);itemReferenceStop.CancelAfter(TimeSpan.FromSeconds(55));var ct=itemReferenceStop.Token;
  if(guideBack is not null)guideBack.Enabled=guideHistory.Count>0;
  if(item==null){itemReferenceNote.Text="Select an item to see its details.";return;}
  string path=guidePath.Length>0?guidePath:GameGuideReader.ItemPath(item.Name),name=guidePath.Length>0?"":ScrollNames.Resolve(item.Name)?.Name??item.Name;bool refresh=guideRefresh;guideRefresh=false;
  itemReferenceNote.Text="Loading reference… Previous details stay visible until the new page is ready.";
  try{
   await Task.Delay(250,ct);GameGuide? guide=await gameGuideReader.Lookup(dataDirectory,path,name,true,ct);
   if(closing||IsDisposed||generation!=itemReferenceGeneration)return;
   if(guide is not null){ShowGameGuide(guide);if(Offline||(!refresh&&guide.CheckedAt>DateTimeOffset.UtcNow.AddDays(-7)))return;}
   if(Offline){itemReferenceNote.Text="Offline: no saved reference for this page. Previous details are kept.";return;}
   try{guide=await gameGuideReader.Lookup(dataDirectory,path,name,Offline,ct,refresh);}catch(Exception e) when(!ct.IsCancellationRequested&&e is HttpRequestException or IOException or FormatException){}
   if(closing||IsDisposed||generation!=itemReferenceGeneration)return;
   if(guide is not null){ShowGameGuide(guide);return;}
   if(Offline||guidePath.Length>0){itemReferenceNote.Text="No saved reference for this page. Previous details are kept. Connect and reload to try its source.";return;}
   ItemReference details;try{details=await wikiReader.LookupItem(item.Name,ct);}
   catch(Exception e) when(!ct.IsCancellationRequested&&e is HttpRequestException or IOException or FormatException){details=await ioReferenceReader.Lookup(item.Name,item.Id,ct);}
   if(closing||IsDisposed||generation!=itemReferenceGeneration||Offline)return;
   itemReferenceGrid.Rows.Clear();itemReferenceNote.Text=details.Name+" • "+details.Source+"\n"+details.Notice;
   if(details.Description.Length>0)itemReferenceGrid.Rows.Add("Description",details.Description);
   foreach(var field in details.Fields)itemReferenceGrid.Rows.Add(field.Key,field.Value);itemReferenceGrid.Rows.Add("Source",details.Url);
  }catch(OperationCanceledException){if(!closing&&!IsDisposed&&generation==itemReferenceGeneration){itemReferenceKey="";itemReferenceNote.Text="Reference cancelled or timed out. Previous details are kept. Use Reload details.";}}
  catch(Exception){if(!closing&&!IsDisposed&&generation==itemReferenceGeneration){itemReferenceKey="";itemReferenceNote.Text="Reference unavailable. Previous details are kept. Use Reload details to retry.";}}
 }
}

