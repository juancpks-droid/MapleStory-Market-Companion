using Market.Core;
namespace Market.App;
static class InlineDropsSmoke
{
 public static void Run(){
  string dir=Path.Combine(AppContext.BaseDirectory,"inline-drops-smoke-data");Directory.CreateDirectory(dir);
  new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));
  using var store=new Store(Path.Combine(dir,"market.db"));var settings=store.LoadSettings();settings.AccessVersion=5;settings.CloseToTray=false;foreach(var source in settings.Sources)source.Enabled=false;store.SaveSettings(settings);
  using var form=new MainForm(store,dir);form.Shown+=async(_,_)=>{try{await form.CheckInlineDrops(dir);await form.CheckInlineDrops(dir,"CCPLZ");File.WriteAllText(Path.Combine(dir,"results.txt"),"PASS: offline state, live NiaMeowDB and CCPLZ inline exact-item monsters/maps, refresh during lookup, rapid selection change, empty selection clearing, source attribution, minimum-size layout.");}catch(Exception e){Environment.ExitCode=1;File.WriteAllText(Path.Combine(dir,"results.txt"),"FAIL: "+e);}finally{form.ExitApplication();}};Application.Run(form);
 }
}
public sealed partial class MainForm
{
 internal async Task CheckInlineDrops(string dir,string source="NiaMeowDB"){
  features.Offline=true;finderDropSource.SelectedItem=source;dropItemKey="";
  Size=new Size(1024,720);ShowPage("Item Finder");finderSearch.Text="Snail Shell";RenderItemFinder();
  void SelectShell(){var row=finderItems.Rows.Cast<DataGridViewRow>().Single(r=>r.Tag is FinderResult f&&f.Name=="Snail Shell");finderItems.ClearSelection();row.Selected=true;}
  SelectShell();finderDetailTabs.SelectedIndex=1;QueueFinderDrops();if(!finderDropNote.Text.StartsWith("Offline:"))throw new Exception("Offline state missing");
  features.Offline=false;finderSearch.Text="Red Potion";RenderItemFinder();finderSearch.Text="Snail Shell";RenderItemFinder();SelectShell();
  for(int i=0;i<120&&finderDrops.Rows.Count==0;i++){await Task.Delay(500);if(i is 5 or 10 or 15)RenderItemFinder();if(finderDropNote.Text.StartsWith("Drop reference unavailable"))throw new Exception(finderDropNote.Text);}
  if(!finderDrops.Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is DropMonster m&&m.Name=="Snail"&&m.Locations.Any(l=>l.Name=="Snail Hunting Ground I")))throw new Exception("Inline Snail and map missing: "+finderDropNote.Text);
  CapturePreview(Path.Combine(dir,"layout-diagnostic.png"));
  if(!finderDropNote.Text.Contains(source)||finderDrops.Height<65)throw new Exception("Attribution or layout missing: height="+finderDrops.Height+"; note="+finderDropNote.Text);
  CapturePreview(Path.Combine(dir,source+"-inline-drops.png"));
  if(source=="CCPLZ"){
   var url=await ccplzReader.Resolve("Snail Shell",stop.Token);if(url!="https://ccplz.cc/items/snail-shell")throw new Exception("Exact CCPLZ link failed");
   if(await ccplzReader.Resolve("NoSuchItemUnique123",stop.Token) is not null)throw new Exception("Unknown item opened");
   if(!finderDrops.Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is DropMonster m&&m.Name=="Mano"&&m.Evidence.Contains("Older-version")))throw new Exception("Historical label missing");
   using var viewer=new WebsiteViewer(new("CCPLZ",url,"Snail Shell",false,"Reference"),dir);
   viewer.Show(this);if(!await viewer.Ready.Task.WaitAsync(TimeSpan.FromSeconds(40)))throw new Exception("CCPLZ viewer failed");
   if(viewer.CurrentUrl!=url)throw new Exception("Viewer changed exact item URL");
   await viewer.CapturePage(Path.Combine(dir,"ccplz-website.png"));viewer.Close();
  }
  if(source=="NiaMeowDB"){
   finderSearch.Text="Blue Moonlight";RenderItemFinder();var blue=finderItems.Rows.Cast<DataGridViewRow>().Single(r=>r.Tag is FinderResult f&&f.Name=="Blue Moonlight");finderItems.ClearSelection();blue.Selected=true;RenderFinderListings();QueueFinderDrops();
   for(int i=0;i<120&&finderDrops.Rows.Count==0;i++)await Task.Delay(500);
   var monsters=finderDrops.Rows.Cast<DataGridViewRow>().Select(r=>r.Tag).OfType<DropMonster>().ToArray();
   if(!new[]{"Toy Trojan","Zombie Lupin","Dark Nependeath"}.All(n=>monsters.Any(m=>m.Name==n)))throw new Exception("Blue Moonlight drop references missing: "+finderDropNote.Text);
   var zombie=monsters.Single(m=>m.Name=="Zombie Lupin");if(!zombie.Evidence.Contains("Dropped By")||!zombie.Evidence.Contains("MSEA Reference Drops")||zombie.Locations.Count==0)throw new Exception("Combined evidence or maps missing");
   CapturePreview(Path.Combine(dir,"blue-moonlight-inline.png"));File.WriteAllText(Path.Combine(dir,"blue-moonlight.txt"),string.Join(Environment.NewLine,monsters.Select(m=>m.Name+": "+m.Evidence+" | Maps: "+string.Join(", ",m.Locations.Select(l=>l.Name)))));
  }
  finderSearch.Text="NoSuchItemUnique123";RenderItemFinder();if(finderDrops.Rows.Count!=0||!finderDropNote.Text.StartsWith("Select an item"))throw new Exception("Stale monster results after selection cleared");
 }
}


