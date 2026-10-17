using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly TextBox sellerSearch=new(){Width=310,PlaceholderText="Which item do you want to buy?",AccessibleName="Find sellers by item"};
 readonly CheckBox sellerRecent=new(){Text="Recent listings only",Checked=true,AutoSize=true};
 readonly ComboBox sellerSort=new GuideComboBox(){Width=145,DropDownStyle=ComboBoxStyle.DropDownList};
 readonly Label sellerStatus=Info(48);
 readonly System.Windows.Forms.Timer sellerRenderTimer=new(){Interval=300};
 bool sellerLoading;
 void BuildSellerFinder(){
  var p=pages["Seller Finder"];var bar=Toolbar();sellerSearch.Margin=new Padding(3,9,8,3);
  sellerSearch.AutoCompleteMode=AutoCompleteMode.Suggest;sellerSearch.AutoCompleteSource=AutoCompleteSource.CustomSource;
  sellerSearch.AutoCompleteCustomSource.AddRange(iconCatalog.Entries.Where(e=>!ScrollNames.IsScroll(e.Name)).Select(e=>e.Name).Concat(ScrollNames.References.Select(r=>$"{r.Name} ({r.Rate}%)")).Distinct().ToArray());
  sellerSort.Items.AddRange(["Lowest price first","Newest listings first"]);sellerSort.SelectedIndex=0;
  bar.Controls.Add(sellerSearch);bar.Controls.Add(Button("Find sellers",async()=>await FindSellers()));bar.Controls.Add(sellerRecent);bar.Controls.Add(sellerSort);
  bar.Controls.Add(Button("Listing details",()=>{if(SelectedSeller() is {} o)ShowListingDetails(o);}));
  bar.Controls.Add(Button("Copy player name",()=>{if(SelectedSeller() is {} o&&!string.IsNullOrWhiteSpace(o.Seller)){try{Clipboard.SetText(o.Seller);sellerStatus.Text="Player name copied. Availability is not verified in game.";}catch{sellerStatus.Text="Could not copy. Try again.";}}else sellerStatus.Text="Select a listing that provides a player name.";}));
  bar.Controls.Add(Button("Check item price",()=>{if(SelectedSeller() is {} o)OpenSavedPrice(o.Name);}));
  bar.Controls.Add(Button("Open source (fallback)",()=>{if(SelectedSeller() is {} o)Open(o.Url);}));
  dealsGrid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;
  SetColumns(dealsGrid,["Item","Player / seller","Mesos per unit","Quantity","Store / location notes","Equipment / variant","Source","Posted","Last checked","Status"]);
  int[] widths=[190,145,115,80,240,160,135,140,140,240];for(int i=0;i<widths.Length;i++)dealsGrid.Columns[i].Width=widths[i];
  dealsGrid.Columns[2].ValueType=typeof(decimal);dealsGrid.Columns[2].DefaultCellStyle.Format="#,##0.################";
  dealsGrid.Columns[3].ValueType=typeof(long);dealsGrid.Columns[3].DefaultCellStyle.NullValue="Unknown";
  dealSummary.Height=70;sellerStatus.Text="Enter an item and choose Find sellers. Kerning Exchange only.";
  p.Controls.Add(dealsGrid);p.Controls.Add(dealSummary);p.Controls.Add(sellerStatus);p.Controls.Add(bar);
  sellerSearch.TextChanged+=(_,_)=>{sellerRenderTimer.Stop();sellerRenderTimer.Start();};
  sellerRenderTimer.Tick+=(_,_)=>{sellerRenderTimer.Stop();RenderSellerFinder();};
  sellerSearch.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await FindSellers();}};
  sellerRecent.CheckedChanged+=(_,_)=>RenderSellerFinder();sellerSort.SelectedIndexChanged+=(_,_)=>RenderSellerFinder();
  dealsGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&SelectedSeller() is {} o)ShowListingDetails(o);};
  FormClosed+=(_,_)=>{sellerRenderTimer.Stop();sellerRenderTimer.Dispose();};
 }
 Observation? SelectedSeller()=>dealsGrid.SelectedRows.Count>0?dealsGrid.SelectedRows[0].Tag as Observation:null;
 async Task FindSellers(){
  if(sellerLoading)return;var query=ResolvePriceQuery(sellerSearch.Text);
  if(query.Length<3){sellerStatus.Text="Enter at least 3 characters or an item ID.";return;}
  sellerLoading=true;sellerRenderTimer.Stop();RenderSellerFinder();
  try{
   if(Offline){sellerStatus.Text="Offline — showing saved listings; availability may have changed.";return;}
   sellerStatus.Text="Waiting for the current source check; saved listings stay visible…";
   var until=DateTimeOffset.UtcNow.AddSeconds(45);while(busy&&DateTimeOffset.UtcNow<until)await Task.Delay(200,stop.Token);
   if(busy){sellerStatus.Text="Another source check is still running. Try Find sellers again shortly.";return;}
   sellerStatus.Text="Checking Kerning Exchange selling listings for "+query+"…";
   await RefreshSources(true,query,false);
   if(!closing){RenderSellerFinder();sellerStatus.Text=sourceResults.Any(r=>r.Updated)?"Source check finished. See posted time and availability labels.":"No fresh source results. Saved listings remain; source limits or availability may apply.";}
  }catch(OperationCanceledException){}catch(Exception e){Log(e);sellerStatus.Text="Could not finish the lookup. Saved listings are kept.";}finally{sellerLoading=false;}
 }
 void RenderSellerFinder(){
  if(dealsGrid.Columns.Count==0)return;var key=SelectedSeller()?.Key;
  var rows=SellerSearch.Find(PlayerMarketSource.Only(EligibleObservations()),ResolvePriceQuery(sellerSearch.Text),sellerRecent.Checked,sellerSort.SelectedIndex==0,DateTimeOffset.UtcNow,settings.FreshHours);
  dealsGrid.Rows.Clear();foreach(var o in rows.Take(300)){int i=dealsGrid.Rows.Add(o.Name,string.IsNullOrWhiteSpace(o.Seller)?"Not provided":o.Seller,o.UnitPrice,o.Quantity,string.IsNullOrWhiteSpace(o.Note)?"Not provided":o.Note,o.Variant,o.Provider,o.ObservedAt?.ToLocalTime().ToString("g")??"Unknown",o.RetrievedAt.ToLocalTime().ToString("g"),(o.PriceApproximate?"Approximate price · ":"")+o.Freshness(DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours))+" · Availability unverified");dealsGrid.Rows[i].Tag=o;if(o.Key==key)dealsGrid.Rows[i].Selected=true;}
  dealSummary.Text=$"{rows.Count} matching Kerning Exchange selling listings"+(rows.Count>300?" • Showing first 300; narrow the item name":"")+"\n"+(rows.Count==0?"Try Find sellers, a more specific item name, or turn off Recent listings only. No results does not mean nobody is selling. ":"")+"Player names and locations appear only when supplied by the source. This does not scan players or locate them live in game.";
 }
}
