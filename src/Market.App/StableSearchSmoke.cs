using Market.Core;
namespace Market.App;
static class StableSearchSmoke
{
 public static void Run(){
  var dir=Path.Combine(AppContext.BaseDirectory,"stable-search-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));
  using var store=new Store(Path.Combine(dir,"market.db"));var settings=store.LoadSettings();settings.AccessVersion=5;settings.CloseToTray=false;foreach(var s in settings.Sources)s.Enabled=false;store.SaveSettings(settings);
  using var form=new MainForm(store,dir);
  form.Shown+=async(_,_)=>{try{await form.CheckStableSearch();File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"stable-search-result.txt"),"PASS: row/card identity preserved while typing and waiting; stable popup bounds in four themes; offline debounce; cached selection; explicit search uses new text; stale work cancelled; busy queue replaced by latest input; raw draft and displayed query saved separately.");}catch(Exception e){Environment.ExitCode=1;File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"stable-search-result.txt"),"FAIL: "+e);}finally{form.ExitApplication();}};
  Application.Run(form);
 }
}
public sealed partial class MainForm
{
 internal async Task CheckStableSearch(){
  void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  timer.Stop();updateTimer.Stop();ShowPage("Price Checker");
  observations=new[]{"Red Potion","Blue Potion"}.Select((name,i)=>new Observation{Provider="Kerning Exchange",Name=name,ItemId="test-"+i,ListingId="stable-"+i,Kind=EvidenceKind.Asking,Available=true,UnitPrice=50+i,Quantity=3,ObservedAt=DateTimeOffset.UtcNow,RetrievedAt=DateTimeOffset.UtcNow,Url="https://example.org/"+i}).ToList();
  search.Text="Red Potion";CommitPriceView("Red Potion");itemLookupTimer.Stop();
  var oldRow=priceGrid.Rows[0];var oldCard=sellCards.Controls[0];var pageBounds=priceDetails.Bounds;
  foreach(var value in new[]{"B","Bl","Blue","Blue Po","Blue Potion"}){
   search.Text=value;await Task.Delay(60);
   Check(ReferenceEquals(priceGrid.Rows[0],oldRow)&&ReferenceEquals(sellCards.Controls[0],oldCard),"Typing rebuilt visible results");
   Check(priceDetails.Bounds==pageBounds&&DisplayedPriceQuery=="Red Potion","Typing shifted the page or changed its displayed item");
  }
  await Task.Delay(850);Check(DisplayedPriceQuery=="Blue Potion"&&SelectedPriceItem()?.Name=="Blue Potion"&&!busy,"Offline pause did not commit saved results");
  foreach(var theme in MerchantTools.Themes){
   features.Theme=theme;ApplyGuideStyle();search.Text="Red Potoin";itemLookupTimer.Stop();pageBounds=priceDetails.Bounds;
   ShowPriceSuggestions(false);pages["Price Checker"].PerformLayout();
   Check(priceSuggestions.Visible&&priceSuggestions.Controls.Count>=2,"Related suggestion popup missing");
   Check(priceDetails.Bounds==pageBounds&&priceSuggestions.Dock==DockStyle.None,"Suggestions pushed results down");
   priceSuggestions.Visible=false;Check(priceDetails.Bounds==pageBounds,"Hiding suggestions shifted results");
  }
  features.Offline=false;RememberLookupComplete("Red Potion");search.Text="Red Potion";
  await Task.Delay(850);Check(DisplayedPriceQuery=="Red Potion"&&!busy&&lookupStatus.Text.StartsWith("Cached"),"Cached debounce did not commit without networking");
  var retained=priceGrid.Rows[0];busy=true;search.Text="Blue Potion";await LookupPricesNow();
  Check(lastLookup=="Blue Potion"&&pendingLookup=="Blue Potion"&&ReferenceEquals(retained,priceGrid.Rows[0]),"Explicit search reused the old selected item or removed its results");
  refreshStop=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);activePriceSearch=true;var generation=priceRefreshGeneration;
  search.Text="Claw Attack Scroll: Intermediate";
  Check(refreshStop.IsCancellationRequested&&priceRefreshGeneration>generation&&!pendingSearch,"New input did not invalidate older work");
  Check(ReferenceEquals(retained,priceGrid.Rows[0]),"Cancelling old search changed visible results");
  itemLookupTimer.Stop();SaveViewSession();var saved=System.Text.Json.JsonSerializer.Deserialize<ViewSession>(File.ReadAllText(SessionPath))!;
  Check(saved.Values["priceSearch"]==search.Text&&saved.DisplayedPriceQuery=="Red Potion","Draft and displayed results not saved separately");
  refreshStop.Dispose();refreshStop=null;activePriceSearch=false;busy=false;pendingSearch=false;
  search.Text="No Such Stable Search Item";await LookupPricesNow();
  Check(lastLookup=="No Such Stable Search Item"&&DisplayedPriceQuery==lastLookup&&priceGrid.Rows.Count==0&&!HoldPriceResults,"Completed explicit search did not replace old results");
  CapturePreview(Path.Combine(dataDirectory,"stable-search.png"));
 }
}
