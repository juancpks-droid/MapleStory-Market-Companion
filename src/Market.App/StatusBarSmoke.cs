using Market.Core;
namespace Market.App;
static class StatusBarSmoke{
 public static void Run(){string dir=Path.Combine(AppContext.BaseDirectory,"status-smoke");Directory.CreateDirectory(dir);new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));using var store=new Store(Path.Combine(dir,"market.db"));var settings=store.LoadSettings();settings.CloseToTray=false;settings.AccessVersion=5;foreach(var source in settings.Sources)source.Enabled=false;store.SaveSettings(settings);using var form=new MainForm(store,dir);form.Shown+=async(_,_)=>{try{await form.CheckStatusBar(dir);File.WriteAllText(Path.Combine(dir,"results.txt"),"PASS: 100 status updates did not invalidate connection controls; duplicate messages did not repaint; source messages survive analysis; four themes and both text sizes fit; offline toggle works.");}catch(Exception e){File.WriteAllText(Path.Combine(dir,"results.txt"),"FAIL: "+e);Environment.ExitCode=1;}finally{form.ExitApplication();}};Application.Run(form);}
}
public sealed partial class MainForm{
 internal async Task CheckStatusBar(string dir){
  timer.Stop();await Task.Delay(300);int invalidations=0;connectionStatus.Invalidated+=(_,_)=>invalidations++;
  footer.Text="Provider backoff until 6:20 PM - cached prices available";await Task.Delay(150);invalidations=0;
  for(int i=0;i<100;i++){footer.Text="Source check "+i;await Task.Delay(20);}
  if(invalidations!=0)throw new Exception("Message updates invalidated connection controls: "+invalidations);
  int messages=0;footer.Message.Invalidated+=(_,_)=>messages++;footer.Text="Stable message";await Task.Delay(100);messages=0;for(int i=0;i<100;i++)footer.Text="Stable message";if(messages!=0)throw new Exception("Duplicate status repainted");
  footer.Text="Provider backoff - cached prices available";Analyze();if(!footer.Text.StartsWith("Provider backoff"))throw new Exception("Analysis overwrote source status");
  foreach(var theme in MerchantTools.Themes)foreach(var size in new[]{9,13}){features.Theme=theme;features.TextSize=size;ApplyTextSize();ApplyGuideStyle();Size=new Size(1024,720);PerformLayout();await Task.Delay(80);if(footer.Message.Bounds.IntersectsWith(connectionStatus.Parent!.Bounds)||footer.Message.Height<footer.Message.Font.Height||connectionStatus.BackColor.A!=255)throw new Exception("Status layout failed: "+theme+size);CapturePreview(Path.Combine(dir,theme.Replace(" ","-")+size+".png"));}
  offlineToggle.Checked=false;offlineToggle.Checked=true;if(!connectionStatus.Text.StartsWith("OFFLINE"))throw new Exception("Offline status failed");
 }
}
