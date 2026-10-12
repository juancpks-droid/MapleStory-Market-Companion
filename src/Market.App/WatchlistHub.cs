using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly TextBox watchSearch=new(){Width=220,PlaceholderText="Search watched items",AccessibleName="Search watchlist"};
 readonly ComboBox watchFilter=new GuideComboBox(){Width=175,DropDownStyle=ComboBoxStyle.DropDownList};
 readonly Label watchStatus=Info(58);
 void BuildWatchlistHub(FlowLayoutPanel bar){
  watchFilter.Items.AddRange(["All watched items","Alerts enabled","Muted items","Has a recent ask","Missing a recent ask"]);watchFilter.SelectedIndex=0;
  bar.Controls.Add(watchSearch);bar.Controls.Add(watchFilter);
  bar.Controls.Add(Button("Check prices",OpenWatchedPrices));bar.Controls.Add(Button("Item details",OpenWatchedFinder));
  bar.Controls.Add(Button("Refresh prices",async()=>await RefreshSources()));
  bar.Controls.Add(Button("Clear filters",()=>{watchSearch.Clear();watchFilter.SelectedIndex=0;}));
  watchSearch.TextChanged+=(_,_)=>RenderItems();watchFilter.SelectedIndexChanged+=(_,_)=>RenderItems();
  watchGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)OpenWatchedPrices();};
  watchGrid.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;OpenWatchedPrices();}};
 }
 bool ShowWatchedItem(SavedItem item,Observation? ask)=>TextKey.Search(item.Name+" "+item.Variant).Contains(TextKey.Search(watchSearch.Text))&&(watchFilter.SelectedIndex switch{1=>!item.Muted,2=>item.Muted,3=>RecentWatchAsk(ask),4=>!RecentWatchAsk(ask),_=>true});
 bool RecentWatchAsk(Observation? ask)=>ask is not null&&ask.Fresh(DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours))&&ask.RetrievedAt>=DateTimeOffset.UtcNow.AddMinutes(-30);
 void OpenWatchedPrices(){if(SelectedItem(watchGrid) is {} item)OpenSavedPrice(item.Name);else footer.Text="Select a watched item to check prices.";}
 void OpenWatchedFinder(){if(SelectedItem(watchGrid) is not {} item){footer.Text="Select a watched item to view details.";return;}finderCategory.SelectedIndex=0;finderSource.SelectedIndex=0;finderListed.Checked=false;finderSearch.Text=item.Name;ShowPage("Item Finder");}
 void RestoreWatchSelection(SavedItem? selectedItem){
  if(selectedItem is not null){watchGrid.ClearSelection();foreach(DataGridViewRow row in watchGrid.Rows)if(row.Tag is SavedItem item&&item.Name==selectedItem.Name&&item.Variant==selectedItem.Variant)row.Selected=true;}
  var watched=store.Items().Where(i=>i.Watch).ToList();watchStatus.Text=$"{watchGrid.Rows.Count} shown / {watched.Count} watched • {watched.Count(i=>!i.Muted)} alerts enabled • {watched.Count(i=>i.Muted)} muted\nDouble-click or Enter to check prices. Missing prices are unavailable; asking prices are not completed sales.";
 }
 internal void CheckWatchlistHub(List<string> checks){
  var saved=store.Items();try{
   store.ReplaceItems([new SavedItem("Red Potion",73,true,false),new SavedItem("Blue Potion",0,true,true),new SavedItem("Unlisted fixture",0,true,false)]);
   var fresh=new Observation(){ObservedAt=DateTimeOffset.UtcNow,RetrievedAt=DateTimeOffset.UtcNow};if(!RecentWatchAsk(fresh)||RecentWatchAsk(fresh with{RetrievedAt=DateTimeOffset.UtcNow.AddHours(-1)})||RecentWatchAsk(null))throw new Exception("Watch freshness filter failed");
   ShowPage("Watchlist");RenderItems();watchSearch.Text="RED pot";if(watchGrid.Rows.Count!=1)throw new Exception("Watch search failed");
   watchSearch.Clear();watchFilter.SelectedIndex=2;if(watchGrid.Rows.Count!=1||SelectedItem(watchGrid)?.Name!="Blue Potion")throw new Exception("Mute filter failed");
   watchFilter.SelectedIndex=1;if(watchGrid.Rows.Count!=2)throw new Exception("Enabled alerts filter failed");
   watchFilter.SelectedIndex=0;watchSearch.Text="Red Potion";watchGrid.Rows[0].Selected=true;RenderItems();if(SelectedItem(watchGrid)?.Name!="Red Potion")throw new Exception("Watch selection lost on refresh");
   OpenWatchedPrices();if(selected!="Price Checker"||search.Text!="Red Potion")throw new Exception("Watch price shortcut failed");
   ShowPage("Watchlist");watchGrid.Rows[0].Selected=true;OpenWatchedFinder();if(selected!="Item Finder"||finderSearch.Text!="Red Potion"||finderListed.Checked)throw new Exception("Watch Finder shortcut failed");
   checks.Add("Watchlist search, alert filters, refresh selection and exact-name Price Checker / Item Finder shortcuts passed");
  }finally{watchSearch.Clear();watchFilter.SelectedIndex=0;store.ReplaceItems(saved);RenderItems();}
 }
}
