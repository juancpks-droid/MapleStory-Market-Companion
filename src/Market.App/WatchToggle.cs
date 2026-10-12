using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 Button? finderWatchButton,priceWatchButton;
 string FinderWatchName=>FinderSelection?.Name??"";
 string PriceWatchName=>SelectedPriceItem()?.Name??"";
 bool IsWatching(string name)=>name.Length>0&&store.Items().Any(i=>i.Watch&&TextKey.Search(i.Name)==TextKey.Search(name));
 void UpdateWatchButtons(){
  void Update(Button? button,string name){if(button is null)return;button.Enabled=name.Length>0;button.Text=IsWatching(name)?"★ Watching":"Watch item";button.AccessibleName=button.Text+(name.Length>0?": "+name:"");}
  Update(finderWatchButton,FinderWatchName);Update(priceWatchButton,PriceWatchName);
 }
 void ToggleItemWatch(string name){
  if(string.IsNullOrWhiteSpace(name))return;
  var matches=store.Items().Where(i=>TextKey.Search(i.Name)==TextKey.Search(name)).ToList();
  bool remove=matches.Any(i=>i.Watch);
  if(remove){foreach(var item in matches.Where(i=>i.Watch))store.SaveItem(item with{Watch=false});}
  else if(matches.Count>0)store.SaveItem(matches[0] with{Watch=true});
  else store.SaveItem(new SavedItem(name,0,true,false));
  Render();UpdateWatchButtons();footer.Text=remove?"Removed from watchlist: "+name:"Watching "+name;
 }
}
