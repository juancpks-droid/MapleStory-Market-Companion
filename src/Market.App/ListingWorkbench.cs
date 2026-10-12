using Market.Core;
using System.Globalization;
namespace Market.App;
public sealed partial class MainForm
{
 readonly DataGridView listingTable=Grid();
 readonly TextBox listingText=new(){Width=220,PlaceholderText="Search player or location",AccessibleName="Find player or listing notes"};
 readonly NumericUpDown listingQuantity=Amount(0);
 readonly CheckBox listingFocus=new(){Text="More room",AutoSize=true,Checked=true};
 readonly CheckBox listingExact=new(){Text="Exact prices only",AutoSize=true};
 readonly ComboBox listingSide=new GuideComboBox(){Width=125,DropDownStyle=ComboBoxStyle.DropDownList};
 readonly Label listingComparison=Info(65);
 List<Observation> workbenchRows=[];bool fillingWorkbench;
 void BuildListingWorkbench(){
  var tab=new TabPage("Listing tools");var bar=Toolbar();listingQuantity.Width=70;
  listingSide.Items.AddRange(["Both sides","For sale","Wanted"]);listingSide.SelectedIndex=0;
  bar.Controls.AddRange([listingText,listingSide,new Label(){Text="Min qty",AutoSize=true,Padding=new Padding(0,8,0,0)},listingQuantity,listingExact]);
  bar.Controls.Add(Button("Reset",()=>{listingText.Clear();listingSide.SelectedIndex=0;listingQuantity.Value=0;listingExact.Checked=false;ResetMarketFilters();}));
  bar.Controls.Add(listingFocus);listingFocus.CheckedChanged+=(_,_)=>ApplyWorkbenchFocus();bar.Controls.Add(Button("Open selected",OpenWorkbenchListing));bar.Controls.Add(Button("Export CSV",ExportWorkbench));
  listingComparison.Dock=DockStyle.Bottom;listingComparison.Text="Ctrl+click two rows to compare. Prices are player offers; availability is unverified.";FitPriceText(listingComparison);
  listingTable.MultiSelect=true;listingTable.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;
  SetColumns(listingTable,["Side","Unit mesos","Quantity","Stack mesos","Player","Notes / location","Source","Price type","Checked","Evidence"]);
  int[] widths=[75,100,75,110,130,200,120,95,140,180];for(int i=0;i<widths.Length;i++){listingTable.Columns[i].Width=widths[i];listingTable.Columns[i].SortMode=DataGridViewColumnSortMode.Automatic;}
  foreach(int i in new[]{1,2,3}){listingTable.Columns[i].ValueType=i==2?typeof(long):typeof(decimal);listingTable.Columns[i].DefaultCellStyle.Format="#,##0.############################";listingTable.Columns[i].DefaultCellStyle.NullValue="Unknown";}
  listingTable.SelectionChanged+=(_,_)=>{if(!fillingWorkbench)UpdateListingComparison();};
  listingTable.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)OpenWorkbenchListing();};
  listingTable.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;OpenWorkbenchListing();}};
  listingText.TextChanged+=(_,_)=>RenderMarketCards(marketTarget);listingSide.SelectedIndexChanged+=(_,_)=>RenderMarketCards(marketTarget);listingQuantity.ValueChanged+=(_,_)=>RenderMarketCards(marketTarget);listingExact.CheckedChanged+=(_,_)=>RenderMarketCards(marketTarget);
  tab.Controls.Add(listingTable);tab.Controls.Add(listingComparison);tab.Controls.Add(bar);priceDetails.TabPages.Add(tab);
 }
 static decimal? StackCost(Observation o){if(o.Quantity is not >0)return null;try{return checked(o.UnitPrice*o.Quantity.Value);}catch(OverflowException){return null;}}
 IEnumerable<Observation> WorkbenchMatches(IEnumerable<Observation> rows)=>rows.Where(o=>(listingSide.SelectedIndex!=1||o.Kind==EvidenceKind.Asking)&&(listingSide.SelectedIndex!=2||o.Kind==EvidenceKind.Buying)&&(!listingExact.Checked||!o.PriceApproximate)&&(listingQuantity.Value==0||o.Quantity>=listingQuantity.Value)&&TextKey.Search(o.Seller+" "+o.Note).Contains(TextKey.Search(listingText.Text)));
 void RenderListingWorkbench(IEnumerable<Observation> rows){
  if(listingTable.Columns.Count==0)return;
  var selectedKeys=listingTable.SelectedRows.Cast<DataGridViewRow>().Select(r=>(r.Tag as Observation)?.Key).ToHashSet();
  var sort=listingTable.SortedColumn?.Index;var direction=listingTable.SortOrder;
  fillingWorkbench=true;listingTable.Rows.Clear();workbenchRows=WorkbenchMatches(rows).ToList();
  foreach(var o in workbenchRows){int n=listingTable.Rows.Add(o.Kind==EvidenceKind.Buying?"Wanted":"For sale",o.UnitPrice,o.Quantity,StackCost(o),o.Seller,o.Note,o.Provider,o.PriceApproximate?"Approximate":"Exact",o.RetrievedAt.LocalDateTime,ReliabilityTools.EvidenceStatus(o,settings.Sources.Any(s=>s.Id==o.Provider&&s.Authorized),DateTimeOffset.UtcNow,settings.FreshHours));listingTable.Rows[n].Tag=o;}
  if(sort.HasValue&&direction!=SortOrder.None)listingTable.Sort(listingTable.Columns[sort.Value],direction==SortOrder.Descending?System.ComponentModel.ListSortDirection.Descending:System.ComponentModel.ListSortDirection.Ascending);
  listingTable.ClearSelection();foreach(DataGridViewRow row in listingTable.Rows)row.Selected=row.Tag is Observation o&&selectedKeys.Contains(o.Key);
  fillingWorkbench=false;UpdateListingComparison();
 }
 void ApplyWorkbenchFocus(){bool show=priceDetails.SelectedIndex!=3||!listingFocus.Checked;if(lowestPriceMetric.Parent is {} strip)strip.Visible=show;if(npcPriceNote.Parent is {} npcPanel)npcPanel.Visible=show;}
 void UpdateListingComparison(){
  var selectedRows=listingTable.SelectedRows.Cast<DataGridViewRow>().Select(r=>r.Tag).OfType<Observation>().ToArray();
  string prefix=$"{workbenchRows.Count} matching listings • Uses the source, price range and recent-only filters from the listings tab.\n";
  if(selectedRows.Length==2){var a=selectedRows[0];var b=selectedRows[1];
   if(a.Kind!=b.Kind||a.MatchKey!=b.MatchKey)listingComparison.Text=prefix+"Choose two listings for the same item, variant and side to compare.";
   else{decimal difference=Math.Abs(a.UnitPrice-b.UnitPrice);listingComparison.Text=prefix+(a.PriceApproximate||b.PriceApproximate?"Approximate comparison • ":"")+TextKey.Mesos(difference)+" mesos / unit difference. "+(a.Kind==EvidenceKind.Buying?"Higher buy offer":"Lower asking price")+": "+TextKey.Mesos(a.Kind==EvidenceKind.Buying?Math.Max(a.UnitPrice,b.UnitPrice):Math.Min(a.UnitPrice,b.UnitPrice))+". Check the source before trading.";}
  }else listingComparison.Text=prefix+(selectedRows.Length==1?ListingDetails(selectedRows[0]).Split(Environment.NewLine)[1]+" • Double-click or Enter to open.":"Ctrl+click two rows to compare. Stack cost = unit price × known quantity; it is not a completed sale.");
 }
 void OpenWorkbenchListing(){if(listingTable.SelectedRows.Count==1&&listingTable.SelectedRows[0].Tag is Observation o)Open(o.Url);else footer.Text="Select one listing to open its original source.";}
 static string ListingCsv(IEnumerable<Observation> rows){
  string Cell(string value){if(value.TrimStart().StartsWith('=')||value.TrimStart().StartsWith('+')||value.TrimStart().StartsWith('-')||value.TrimStart().StartsWith('@'))value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
  var lines=new List<string>{"Item,Side,Unit mesos,Quantity,Stack mesos,Player,Notes,Source,Price type,Checked UTC,Link"};
  foreach(var o in rows)lines.Add(string.Join(",",new[]{o.Name,o.Kind==EvidenceKind.Buying?"Wanted":"For sale",o.UnitPrice.ToString(CultureInfo.InvariantCulture),o.Quantity?.ToString(CultureInfo.InvariantCulture)??"",StackCost(o)?.ToString(CultureInfo.InvariantCulture)??"",o.Seller,o.Note,o.Provider,o.PriceApproximate?"Approximate":"Exact",o.RetrievedAt.ToString("O"),o.Url}.Select(Cell)));
  return string.Join("\r\n",lines);
 }
 void ExportWorkbench(){
  if(workbenchRows.Count==0){footer.Text="No matching listings to export. Adjust the listing filters first.";return;}
  using var dialog=new SaveFileDialog(){Filter="CSV files|*.csv",FileName="maple-listings-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".csv"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
  try{File.WriteAllText(dialog.FileName,ListingCsv(listingTable.Rows.Cast<DataGridViewRow>().Select(r=>(Observation)r.Tag!)),new System.Text.UTF8Encoding(true));footer.Text=$"Exported {workbenchRows.Count} listings.";}catch(Exception e)when(e is IOException or UnauthorizedAccessException){footer.Text="Could not save export. Choose a writable folder and try again.";}
 }
}
