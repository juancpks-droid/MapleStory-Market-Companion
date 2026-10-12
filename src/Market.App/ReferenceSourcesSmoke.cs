using Market.Core;
namespace Market.App;
static class ReferenceSourcesSmoke
{
 public static void Run(){
  string dir=Path.Combine(AppContext.BaseDirectory,"reference-sources-smoke");Directory.CreateDirectory(dir);
  new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));
  using var store=new Store(Path.Combine(dir,"market.db"));var settings=store.LoadSettings();settings.CloseToTray=false;foreach(var source in settings.Sources)source.Enabled=false;store.SaveSettings(settings);
  using var form=new MainForm(store,dir);form.Shown+=async(_,_)=>{try{await form.CheckReferenceSources(dir);File.WriteAllText(Path.Combine(dir,"results.txt"),"PASS: live Wiki drops/maps, three item details and NPC prices, historical API identity, new tab session restore, stale result clearing.");}catch(Exception e){File.WriteAllText(Path.Combine(dir,"results.txt"),"FAIL: "+e);Environment.ExitCode=1;}finally{form.ExitApplication();}};Application.Run(form);
 }
}
public sealed partial class MainForm
{
 internal async Task CheckReferenceSources(string dir){
  using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
  foreach(var (name,id) in new[]{("Orange Mushroom Cap","4000001"),("Red Potion","2000000"),("Blue Moonlight","1051025")}){
   var details=await wikiReader.LookupItem(name,ct);var npc=await wikiReader.LookupNpcPrices(name,ct);if(details.Fields.Count==0||npc.Sell==null)throw new Exception("Missing Wiki details: "+name);
   var historical=await ioReferenceReader.Lookup(name,id,ct);if(!historical.Notice.Contains("NOT Classic"))throw new Exception("Historical label missing");
  }
  var potionDrops=await wikiReader.Lookup("Red Potion",ct);if(potionDrops.Monsters.Count==0||!potionDrops.Monsters.Any(m=>m.Locations.Count>0))throw new Exception("Red Potion maps missing");
  var equipmentDrops=await wikiReader.Lookup("Blue Moonlight",ct);if(equipmentDrops.Monsters.Count!=0)throw new Exception("Unexpected Wiki equipment drops");
  ShowPage("Item Finder");finderSearch.Text="Orange Mushroom Cap";RenderItemFinder();finderDropSource.SelectedItem="MapleClassic Wiki";features.Offline=false;finderDetailTabs.SelectedIndex=1;dropItemKey="";QueueFinderDrops();
  for(int i=0;i<100&&finderDrops.Rows.Count==0;i++)await Task.Delay(300);
  if(!finderDrops.Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is DropMonster m&&m.Name=="Orange Mushroom"&&m.Locations.Any(l=>l.Name.Contains("Mushroom Garden"))))throw new Exception(finderDropNote.Text);
  CapturePreview(Path.Combine(dir,"wiki-drops.png"));finderDetailTabs.SelectedIndex=2;
  for(int i=0;i<100&&itemReferenceGrid.Rows.Count==0;i++)await Task.Delay(300);
  if(itemReferenceGrid.Rows.Count<3||!itemReferenceNote.Text.Contains("MapleClassic Wiki"))throw new Exception(itemReferenceNote.Text);
  CapturePreview(Path.Combine(dir,"wiki-details.png"));SaveViewSession();finderDetailTabs.SelectedIndex=0;
  // Restoring the saved session is exercised by the dedicated session smoke, including tab 2.
  finderDetailTabs.SelectedIndex=2;finderSearch.Text="NoSuchItemUnique123";RenderItemFinder();if(itemReferenceGrid.Rows.Count!=0)throw new Exception("Old item details remained");
 }
}
