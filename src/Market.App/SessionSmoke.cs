using Market.Core;
namespace Market.App;
static class SessionSmoke
{
 internal static void Run(){
  string dir=Path.Combine(AppContext.BaseDirectory,"session-smoke-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try{
   foreach(bool setupComplete in new[]{false,true}){
   new CompanionFeatures{SetupComplete=setupComplete,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));
   using(var store=new Store(Path.Combine(dir,"market.db"))){var settings=store.LoadSettings();settings.AccessVersion=5;settings.CloseToTray=false;store.SaveSettings(settings);store.SaveItem(new("Red Potion",73,true,false));using var form=new MainForm(store,dir);form.Shown+=(_,_)=>{form.SetSessionFixture();form.ExitApplication();};Application.Run(form);}
   using(var store=new Store(Path.Combine(dir,"market.db"))){using var form=new MainForm(store,dir);form.CheckSessionFixture();if(store.Items().Single().Quantity!=73)throw new Exception("Inventory not preserved");form.Shown+=(_,_)=>form.ExitApplication();Application.Run(form);}
   }
   File.WriteAllText(Path.Combine(dir,"view-session.json"),"{broken json");
   using(var store=new Store(Path.Combine(dir,"market.db"))){using var form=new MainForm(store,dir);form.Shown+=(_,_)=>form.ExitApplication();Application.Run(form);}
   File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"session-smoke-result.txt"),"PASS: with setup incomplete AND complete, close/reopen preserved active tab, dashboard/price/finder searches, finder filters, chart range, bag category, window size and inventory. Invalid view JSON falls back without losing player data.");
  }catch(Exception e){Environment.ExitCode=1;File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"session-smoke-result.txt"),"FAIL: "+e);}
 }
}
public sealed partial class MainForm
{
 internal void SetSessionFixture(){features.Theme="Ellinia";ApplyTownTheme();quickSearch.Text="Red Potion";search.Text="Snail Shell";finderSearch.Text="Potion";finderCategory.SelectedItem="Use";finderListed.Checked=true;listingSort.SelectedIndex=2;listingMin.Value=12;listingMax.Value=900;priceDetails.SelectedIndex=1;historyDays=14;inventoryCategory="USE";Size=new Size(1100,760);ShowPage("Item Finder");finderDropSource.SelectedItem="CCPLZ";finderDetailTabs.SelectedIndex=1;}
 internal void CheckSessionFixture(){if(listingSort.SelectedIndex!=2||listingMin.Value!=12||listingMax.Value!=900||priceDetails.SelectedIndex!=1||features.Theme!="Ellinia"||finderDropSource.Text!="CCPLZ"||finderDetailTabs.SelectedIndex!=1||selected!="Item Finder"||quickSearch.Text!="Red Potion"||search.Text!="Snail Shell"||finderSearch.Text!="Potion"||finderCategory.Text!="Use"||!finderListed.Checked||historyDays!=14||inventoryCategory!="USE"||Width!=1100||Height!=760)throw new Exception("View session did not round-trip");}
}
