using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly TabControl finderDetailTabs=new MarketTabs(){Dock=DockStyle.Fill};
 readonly DataGridView finderDrops=Grid();
 readonly FlowLayoutPanel relatedDropPanel=new(){Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Visible=false};
 readonly ComboBox relatedDropChoice=new GuideComboBox(){Width=270,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Related source items (unconfirmed)"};
 readonly Label finderDropNote=Info(75);
 readonly DropReferenceReader dropReader=new();
 readonly CcplzReader ccplzReader=new(); readonly QuestDropReader questDropReader=new();
 readonly ComboBox finderDropSource=new GuideComboBox(){Width=180,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Drop reference source"};
 CancellationTokenSource? dropLookupStop;
 string dropItemKey="",dropSourceUrl="";int dropGeneration;TimeSpan dropLookupTimeout=TimeSpan.FromMinutes(2);
 Control BuildFinderDetails(Control listings){
  var asks=new TabPage("Market listings");asks.Controls.Add(listings);finderDetailTabs.TabPages.Add(asks);
  var drops=new TabPage("Monster drops & locations");
  var actions=Toolbar();actions.Height=40;
  finderDropSource.Items.AddRange(["NiaMeowDB","CCPLZ","MapleStory Quest"]);finderDropSource.SelectedIndex=0;finderDropSource.SelectedIndexChanged+=(_,_)=>{dropItemKey="";QueueFinderDrops();};actions.Controls.Add(finderDropSource);
  actions.Controls.Add(Button("Load / retry",()=>{dropItemKey="";QueueFinderDrops();}));
  actions.Controls.Add(Button("Open source item",()=>{if(dropSourceUrl.Length>0&&!Offline)OpenDropReference(dropSourceUrl);}));
  relatedDropPanel.Controls.Add(new Label(){Text="Related items (unconfirmed)",AutoSize=true,Padding=new Padding(3,8,3,0)});relatedDropPanel.Controls.Add(relatedDropChoice);relatedDropPanel.Controls.Add(Button("View related item",()=>{if(!Offline&&relatedDropChoice.SelectedItem is DropItemSuggestion suggestion)new WebsiteViewer(new("NiaMeowDB • "+suggestion.Name,suggestion.Url,suggestion.Name,false,"Related suggestion, not a confirmed match for the selected item. Check its identity and drop evidence."),dataDirectory).Show(this);}));
  drops.Controls.Add(finderDrops);drops.Controls.Add(relatedDropPanel);FitPriceText(finderDropNote);drops.Controls.Add(finderDropNote);drops.Controls.Add(actions);finderDetailTabs.TabPages.Add(drops);
  finderDrops.DefaultCellStyle.WrapMode=DataGridViewTriState.True;finderDrops.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
  SetColumns(finderDrops,["Monster","Where to find it (click for maps)","Evidence"]);
  finderDrops.Columns[0].FillWeight=22;finderDrops.Columns[1].FillWeight=48;finderDrops.Columns[2].FillWeight=30;foreach(DataGridViewColumn column in finderDrops.Columns)column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
  finderDrops.CellClick+=(_,e)=>{if(Offline||e.RowIndex<0||finderDrops.Rows[e.RowIndex].Tag is not DropMonster monster)return;
   if(e.ColumnIndex==1&&monster.Locations.Count>0){var menu=new ContextMenuStrip();foreach(var map in monster.Locations)menu.Items.Add(map.Name,null,(_,_)=>OpenDropReference(map.Url));menu.Closed+=(_,_)=>menu.Dispose();menu.Show(Cursor.Position);}
   else if(e.ColumnIndex==0)OpenDropReference(monster.Url);
  };
  finderDetailTabs.SelectedIndexChanged+=(_,_)=>{if(finderDetailTabs.Parent is TableLayoutPanel layout){layout.RowStyles[0].Height=finderDetailTabs.SelectedIndex==1?30:45;layout.RowStyles[1].Height=finderDetailTabs.SelectedIndex==1?70:55;}QueueFinderDrops();};
  FormClosed+=(_,_)=>{dropLookupStop?.Cancel();dropLookupStop?.Dispose();dropReader.Dispose();ccplzReader.Dispose();questDropReader.Dispose();};
  finderDropNote.Text="Select an item and open this tab to load drop references. No drop rates are inferred.";
  return finderDetailTabs;
 }
 void ShowDropSuggestions(IReadOnlyList<DropItemSuggestion> suggestions){relatedDropChoice.Items.Clear();relatedDropChoice.Items.AddRange(suggestions.Cast<object>().ToArray());relatedDropPanel.Visible=suggestions.Count>0;if(suggestions.Count>0)relatedDropChoice.SelectedIndex=0;}
 void OpenDropReference(string url){if(!Offline)new WebsiteViewer(new((new Uri(url).Host=="ccplz.cc"?"CCPLZ":new Uri(url).Host=="maplestory.quest"?"MapleStory Quest":"NiaMeowDB")+" • Drop reference",url,FinderSelection?.Name??"",false,"Community reports and historical references are labelled separately. Check source coverage."),dataDirectory).Show(this);}
 async Task OpenCcplzItem(){
  var item=FinderSelection;if(item is null)return;
  if(Offline){MessageBox.Show(this,"Turn off Work offline to open CCPLZ.","Offline");return;}
  try{
   var url=await ccplzReader.Resolve(item.Name,stop.Token);
   if(closing||IsDisposed||Offline)return;
   if(url is null){MessageBox.Show(this,"No exact CCPLZ page was verified for "+item.Name+". It may have a different name or be missing from the reference.","CCPLZ item lookup");return;}
   new WebsiteViewer(new("CCPLZ • "+item.Name,url,item.Name,false,"CCPLZ reference. Reported and historical data can differ from your server."),dataDirectory).Show(this);
  }catch(OperationCanceledException){}catch(Exception){if(!closing&&!IsDisposed)MessageBox.Show(this,"CCPLZ could not be reached. Please try again later.","CCPLZ item lookup");}
 }
 async void QueueFinderDrops(){
  if(finderDetailTabs.Parent is TableLayoutPanel layout){float top=finderDetailTabs.SelectedIndex==1?30:45;if(layout.RowStyles[0].Height!=top){layout.RowStyles[0].Height=top;layout.RowStyles[1].Height=100-top;}}
  if(finderDetailTabs.SelectedIndex!=1||selected!="Item Finder")return;
  var item=FinderSelection;string source=finderDropSource.Text;string key=item is null?"":source+"|"+item.Id+"|"+item.Name;
  if(key==dropItemKey&&key.Length>0)return;
  dropItemKey=key;int generation=++dropGeneration;dropLookupStop?.Cancel();dropLookupStop?.Dispose();dropLookupStop=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);dropLookupStop.CancelAfter(dropLookupTimeout);var ct=dropLookupStop.Token;
  finderDrops.Rows.Clear();dropSourceUrl="";ShowDropSuggestions([]);
  if(item is null){finderDropNote.Text="Select an item to see monster drops and locations.";return;}
  if(Offline){dropItemKey="";finderDropNote.Text="Offline: turn off Work offline, then select Load / retry to look up drop references.";return;}
  finderDropNote.Text="Looking up "+item.Name+"… Checking the item and related references; source requests are spaced.";
  string stage=source;var started=DateTimeOffset.UtcNow;using var progress=new System.Windows.Forms.Timer(){Interval=1000};
  progress.Tick+=(_,_)=>{if(!closing&&!IsDisposed&&generation==dropGeneration)finderDropNote.Text=$"Looking up {item.Name}… {(int)(DateTimeOffset.UtcNow-started).TotalSeconds}s elapsed • {stage} • Waiting for item, monster and map details. Requests are spaced; maximum wait 2 minutes.";};progress.Start();
  try{
   await Task.Delay(500,ct);DropLookupResult lookup=source=="NiaMeowDB"?await DropFallback.Lookup(item.Name,dropReader.Lookup,ccplzReader.Lookup,ct,message=>stage=message):new(source=="CCPLZ"?await ccplzReader.Lookup(item.Name,ct):await questDropReader.Lookup(item.Name,ct),source,"");var result=lookup.Drops;
   if(closing||IsDisposed||generation!=dropGeneration||Offline)return;
   ShowDropSuggestions(result.Suggestions);dropSourceUrl=result.SourceUrl;finderDropNote.Text=result.Item+" • "+lookup.Source+" • Checked "+result.CheckedAt.ToLocalTime().ToString("g")+"\n"+lookup.Notice+result.Message;
   foreach(var monster in result.Monsters){int row=finderDrops.Rows.Add(monster.Name,monster.Locations.Count==0?"No recorded map information":string.Join("\n",monster.Locations.Select(m=>m.Name)),monster.Evidence);finderDrops.Rows[row].Tag=monster;}
   finderDrops.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
  }catch(OperationCanceledException){ShowDropTimeout(generation);}
  catch(Exception){if(!closing&&!IsDisposed&&generation==dropGeneration){dropItemKey="";finderDropNote.Text="Drop reference unavailable. Select Load / retry later, or use the website reference button. This does not mean the item has no drops.";}}
  finally{progress.Stop();}
 }
 void ShowDropTimeout(int generation){if(closing||IsDisposed||generation!=dropGeneration||Offline)return;dropItemKey="";finderDropNote.Text="The drop lookup timed out. No completed result was received. Select Load / retry, or switch to the other reference source.";}
}
