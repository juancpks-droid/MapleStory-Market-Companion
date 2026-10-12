namespace Market.App;
public sealed partial class MainForm
{
 void OpenPriceInFm(){
  var item=string.IsNullOrWhiteSpace(search.Text)&&priceGrid.SelectedRows.Count==0?null:SelectedPriceItem();
  var name=item?.Name??search.Text.Trim();
  if(string.IsNullOrWhiteSpace(name)){footer.Text="Select an item or enter an item name first.";return;}
  fmFilter.SelectedIndex=0;
  fmSearch.Text=name;
  ShowPage("FM Tracker");
  RenderFm();
 }
}
