using Market.Core;
namespace Market.App;
static class InlineDropsSmoke
{
 public static void Run(){
  string dir=Path.Combine(AppContext.BaseDirectory,"inline-drops-smoke-data");Directory.CreateDirectory(dir);
  new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));
  using var store=new Store(Path.Combine(dir,"market.db"));var settings=store.LoadSettings();settings.AccessVersion=5;settings.CloseToTray=false;foreach(var source in settings.Sources)source.Enabled=false;store.SaveSettings(settings);
  using var form=new MainForm(store,dir);form.Shown+=async(_,_)=>{try{await form.CheckInlineDrops(dir);File.WriteAllText(Path.Combine(dir,"results.txt"),"PASS: offline state, live inline exact-item monsters/maps, refresh during lookup, rapid selection change, empty selection clearing, source attribution, minimum-size layout.");}catch(Exception e){Environment.ExitCode=1;File.WriteAllText(Path.Combine(dir,"results.txt"),"FAIL: "+e);}finally{form.ExitApplication();}};Application.Run(form);
 }
}
public sealed partial class MainForm
{
 internal async Task CheckInlineDrops(string dir){
  Width=1024;ShowPage("Item Finder");finderSearch.Text="Snail Shell";RenderItemFinder();
  void SelectShell(){var row=finderItems.Rows.Cast<DataGridViewRow>().Single(r=>r.Tag is FinderResult f&&f.Name=="Snail Shell");finderItems.ClearSelection();row.Selected=true;}
  SelectShell();finderDetailTabs.SelectedIndex=1;if(!finderDropNote.Text.StartsWith("Offline:"))throw new Exception("Offline state missing");
  features.Offline=false;finderSearch.Text="Red Potion";RenderItemFinder();finderSearch.Text="Snail Shell";RenderItemFinder();SelectShell();
  for(int i=0;i<120&&finderDrops.Rows.Count==0;i++){await Task.Delay(500);if(i is 5 or 10 or 15)RenderItemFinder();if(finderDropNote.Text.StartsWith("Drop reference unavailable"))throw new Exception(finderDropNote.Text);}
  if(!finderDrops.Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is DropMonster m&&m.Name=="Snail"&&m.Locations.Any(l=>l.Name=="Snail Hunting Ground I")))throw new Exception("Inline Snail and map missing: "+finderDropNote.Text);
  if(!finderDropNote.Text.Contains("NiaMeowDB")||finderDrops.Height<65)throw new Exception("Attribution or layout missing");
  CapturePreview(Path.Combine(dir,"inline-drops.png"));
  finderSearch.Text="NoSuchItemUnique123";RenderItemFinder();if(finderDrops.Rows.Count!=0||!finderDropNote.Text.StartsWith("Select an item"))throw new Exception("Stale monster results after selection cleared");
 }
}
