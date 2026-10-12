using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 internal void CheckWatchToggles(List<string> checks){
  void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
  store.Save(observations);
  store.SaveItem(new SavedItem("Red Potion",73,false,true));
  finderListed.Checked=false;finderSource.SelectedItem="All";finderCategory.SelectedItem="All";finderSearch.Text="Red Potion";
  ShowPage("Item Finder");RenderItemFinder();
  Check(finderWatchButton!.Enabled&&finderWatchButton.Text=="Watch item","Finder watch toggle starts unwatched");
  finderWatchButton.PerformClick();
  Check(finderWatchButton.Text=="★ Watching"&&store.Items().Count(i=>i.Name=="Red Potion"&&i.Watch)==1,"Finder click adds exactly one shared watch");
  search.Text="Red Potion";ShowPage("Price Checker");
  Check(priceWatchButton!.Enabled&&priceWatchButton.Text=="★ Watching","Price Checker sees Finder watch immediately");
  priceWatchButton.PerformClick();
  Check(!IsWatching("Red Potion")&&finderWatchButton.Text=="Watch item"&&priceWatchButton.Text=="Watch item","Price toggle removes watch and updates both buttons");
  priceWatchButton.PerformClick();
  Check(store.Items().Count(i=>i.Name=="Red Potion")==1&&IsWatching("Red Potion"),"Rewatching reuses saved item without duplicates");
  var saved=store.Items().Single(i=>i.Name=="Red Potion");Check(saved.Quantity==73&&saved.Muted,"Watch toggles preserve existing quantity and mute preference");
  search.Text="NoSuchItemWatchToggle";RenderPrices();Check(!priceWatchButton.Enabled,"Unmatched price search disables watch button");
  ShowPage("Item Finder");finderSearch.Text="NoSuchItemWatchToggle";RenderItemFinder();Check(!finderWatchButton.Enabled,"Empty Finder results disable watch button");
  finderSearch.Text="Red Potion";RenderItemFinder();finderWatchButton.PerformClick();Check(!IsWatching("Red Potion"),"Finder can remove a Price Checker watch");
  finderWatchButton.PerformClick();CapturePreview(Path.Combine(dataDirectory,"watch-toggle.png"));
 }
}
