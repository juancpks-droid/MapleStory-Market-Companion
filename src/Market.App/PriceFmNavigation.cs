namespace Market.App;
public sealed partial class MainForm
{
 void OpenPriceInFm(){
  var item=priceGrid.SelectedRows.Count>0?priceGrid.SelectedRows[0].Tag as Market.Core.Observation:null;
  var name=item?.Name??search.Text.Trim();
  if(string.IsNullOrWhiteSpace(name)){footer.Text="Select an item or enter an item name first.";return;}
  fmFilter.SelectedIndex=0;
  fmSearch.Text=name;
  ShowPage("FM Tracker");
  RenderFm();
 }
}
