using Market.Core;
using System.Text.Json;
namespace Market.App;
static class KerningFallbackSmoke{
 internal static void Run(){
  var dir=Path.Combine(AppContext.BaseDirectory,"kerning-fallback-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));
  using var store=new Store(Path.Combine(dir,"market.db"));var settings=store.LoadSettings();settings.AccessVersion=5;settings.CloseToTray=false;foreach(var source in settings.Sources)source.Enabled=false;store.SaveSettings(settings);
  using var form=new MainForm(store,dir);form.Shown+=async(_,_)=>{try{await form.CheckKerningFallback();File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"kerning-fallback-result.txt"),"PASS: three live exact Kerning destinations; actual fallback button; NPC cards retained; empty/ask/buy visibility; pending search identity.");}catch(Exception e){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"kerning-fallback-result.txt"),"FAIL: "+e);Environment.ExitCode=1;}finally{form.ExitApplication();}};Application.Run(form);
 }
}
public sealed partial class MainForm{
 internal async Task CheckKerningFallback(){
  timer.Stop();updateTimer.Stop();ShowPage("Price Checker");
  foreach(var (name,npc) in new[]{("Orange Mushroom Cap",8m),("Blue Snail Shell",2m),("Red Snail Shell",4m)}){
   features.Offline=true;search.Text=name;itemLookupTimer.Stop();CommitPriceView(name);priceItemChoice.Items.Clear();UpdatePriceOverview(null,null,null);ShowNpcPrices(new(name,"https://maplestory.quest/",npc,null,null,DateTimeOffset.UtcNow));
   if(kerningPriceFallback is null||!kerningPriceFallback.Visible||!npcSellMetric.Visible)throw new Exception("Missing fallback or NPC card: "+name+" button="+kerningPriceFallback?.Visible+" parent="+kerningPriceFallback?.Parent?.Visible+" npc="+npcSellMetric.Visible+" name="+KerningFallbackName+" page="+selected);
   UpdatePriceOverview(10,null,null);if(kerningPriceFallback.Visible)throw new Exception("Fallback shown with ask");UpdatePriceOverview(null,10,null);if(kerningPriceFallback.Visible)throw new Exception("Fallback shown with buy");UpdatePriceOverview(null,null,null);
   search.Text="Different pending query";itemLookupTimer.Stop();if(KerningFallbackName!=name)throw new Exception("Pending query replaced displayed item");
   features.Offline=false;UpdateKerningPriceFallback();Exception? failure=null;
   using var probe=new System.Windows.Forms.Timer{Interval=150};probe.Tick+=async(_,_)=>{
    var viewer=Application.OpenForms.OfType<WebsiteViewer>().FirstOrDefault();if(viewer is null)return;probe.Stop();
    try{if(!await viewer.Ready.Task.WaitAsync(TimeSpan.FromSeconds(55)))throw new Exception(viewer.ViewerStatus);
     var until=DateTime.UtcNow.AddSeconds(20);bool matched=false;
     while(DateTime.UtcNow<until){var headings=JsonSerializer.Deserialize<string[]>(await viewer.ReadHeadings())??[];if(viewer.CurrentUrl.StartsWith("https://kerning.gg/item/")&&headings.Contains(name)){matched=true;break;}await Task.Delay(250);}
     if(!matched)throw new Exception("Wrong Kerning destination: "+name+" "+viewer.CurrentUrl+" "+viewer.ViewerStatus);
    }catch(Exception e){failure=e;}finally{viewer.Close();}
   };probe.Start();kerningPriceFallback.PerformClick();if(failure is not null)throw failure;
   if(!npcSellMetric.Visible||npcSellMetric.Value!=TextKey.Mesos(npc)+" mesos")throw new Exception("NPC price lost");
  }
  features.Offline=true;CapturePreview(Path.Combine(dataDirectory,"kerning-fallback.png"));await Task.CompletedTask;
 }
}
