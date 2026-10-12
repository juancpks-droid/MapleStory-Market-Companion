using Market.Core;
namespace Market.App;
static class PriceLookupSmoke
{
 public static void Run(){
  var dir=Path.Combine(AppContext.BaseDirectory,"lookup-smoke-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));
  using var store=new Store(Path.Combine(dir,"market.db"));var settings=store.LoadSettings();settings.AccessVersion=5;settings.CloseToTray=false;foreach(var s in settings.Sources)s.Enabled=false;store.SaveSettings(settings);
  using var form=new MainForm(store,dir);
  form.Shown+=async(_,_)=>{try{await form.CheckPriceLookup();File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"lookup-smoke-result.txt"),"PASS: typing debounce, item-ID resolution, replacement/clear, offline suppression, busy-refresh queue and live targeted Kerning lookup returned dated prices.");}catch(Exception e){Environment.ExitCode=1;File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"lookup-smoke-result.txt"),"FAIL: "+e);}finally{form.ExitApplication();}};
  Application.Run(form);
 }
}
public sealed partial class MainForm
{
 internal async Task CheckPriceLookup(){
  void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  timer.Stop();ShowPage("Price Checker");search.Text="Red Potion";Check(!itemLookupTimer.Enabled,"Offline search started network timer");
  features.Offline=false;search.Text="Blue Potion";Check(itemLookupTimer.Enabled&&queuedLookup=="Blue Potion","Typing did not queue automatic price lookup");
  search.Text="2000000";Check(queuedLookup=="Red Potion","Item ID not resolved before source lookup");search.Clear();Check(!itemLookupTimer.Enabled,"Clearing query failed to cancel lookup");
  busy=true;await RefreshSources(true,"Snail Shell");Check(pendingSearch&&pendingLookup=="Snail Shell","Busy refresh lost requested item");busy=false;pendingSearch=false;pendingLookup="";
  var source=settings.Sources.Single(s=>s.Id=="Kerning Exchange");source.Enabled=true;store.SaveSettings(settings);
  var before=DateTimeOffset.UtcNow;search.Text="Green Mushroom Cap";
  var until=DateTimeOffset.UtcNow.AddSeconds(100);
  while(DateTimeOffset.UtcNow<until){await Task.Delay(250);if(lastLookup=="Green Mushroom Cap"&&!busy&&!itemLookupTimer.Enabled)break;}
  Check(lastLookup=="Green Mushroom Cap"&&!busy,"Automatic lookup did not finish");
  Check(observations.Any(o=>o.Provider=="Kerning Exchange"&&o.Name=="Green Mushroom Cap"&&o.RetrievedAt>=before&&o.ObservedAt is not null),"Automatic lookup returned no fresh dated target listings");
  CapturePreview(Path.Combine(dataDirectory,"automatic-prices.png"));
 }
}
