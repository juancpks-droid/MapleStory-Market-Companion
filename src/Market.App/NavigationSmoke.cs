using Market.Core;
namespace Market.App;
static class NavigationSmoke{
 public static void Run(){string dir=Path.Combine(AppContext.BaseDirectory,"navigation-smoke");Directory.CreateDirectory(dir);new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));using var store=new Store(Path.Combine(dir,"market.db"));var settings=store.LoadSettings();settings.CloseToTray=false;settings.AccessVersion=5;foreach(var source in settings.Sources)source.Enabled=false;store.SaveSettings(settings);using var form=new MainForm(store,dir);form.Shown+=async(_,_)=>{try{await form.CheckNavigation(dir);File.WriteAllText(Path.Combine(dir,"results.txt"),"PASS: 4 themes at 9/13pt; 160 page switches; 80 nested tab switches; page content prepared before visibility; one visible page; repeated navigation keeps row identity, selection, scroll and filters; fresh Finder rows; session data retained.");}catch(Exception e){File.WriteAllText(Path.Combine(dir,"results.txt"),"FAIL: "+e);Environment.ExitCode=1;}finally{form.ExitApplication();}};Application.Run(form);}
}
public sealed partial class MainForm{
 internal async Task CheckNavigation(string dir){
  timer.Stop();await Task.Delay(300);finderSearch.Text="Potion";
  bool unprepared=false;pages["Item Finder"].VisibleChanged+=(_,_)=>{if(pages["Item Finder"].Visible&&finderItems.Rows.Count==0)unprepared=true;};
  foreach(var theme in MerchantTools.Themes)foreach(var size in new[]{9,13}){
   features.Theme=theme;features.TextSize=size;ApplyTextSize();ApplyGuideStyle();Size=new Size(1100,760);
   foreach(var name in new[]{"FM Tracker","Item Finder","Price Checker","Deal Finder","Dashboard","Item Finder","Watchlist","Alert Inbox","Settings","Price Checker","Item Finder","FM Tracker","Price Checker","Dashboard","Settings","Watchlist","Item Finder","Price Checker","Deal Finder","Item Finder"}){
    ShowPage(name);await Task.Delay(25);
    if(selected!=name||pages.Count(p=>p.Value.Visible)!=1||!pages[name].Visible)throw new Exception("Page visibility failed: "+name);
   }
   if(unprepared)throw new Exception("Finder became visible before its rows were ready");
   finderItems.ClearSelection();finderItems.Rows[3].Selected=true;finderItems.FirstDisplayedScrollingRowIndex=3;
   var row=finderItems.Rows[3];int top=finderItems.FirstDisplayedScrollingRowIndex;string query=finderSearch.Text;
   for(int i=0;i<10;i++)navigation["Item Finder"].PerformClick();
   if(!ReferenceEquals(row,finderItems.Rows[3])||!row.Selected||finderItems.FirstDisplayedScrollingRowIndex!=top||finderSearch.Text!=query)throw new Exception("Repeated click rebuilt Finder or lost state");
   foreach(int index in new[]{1,2,0,2,1}){finderDetailTabs.SelectedIndex=index;await Task.Delay(25);if(!finderDetailTabs.SelectedTab!.Visible)throw new Exception("Finder nested tab blank");}
   ShowPage("Price Checker");foreach(int index in new[]{1,2,3,0,1}){priceDetails.SelectedIndex=index;await Task.Delay(25);if(!priceDetails.SelectedTab!.Visible)throw new Exception("Price nested tab blank");}
   CapturePreview(Path.Combine(dir,theme.Replace(" ","-")+size+".png"));
  }
  SetSessionFixture();SaveViewSession();CheckSessionFixture();
 }
}
