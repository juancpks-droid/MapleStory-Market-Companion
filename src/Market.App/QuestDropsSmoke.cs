using Market.Core;
namespace Market.App;
static class QuestDropsSmoke
{
 public static void Run(){
  string dir=Path.Combine(AppContext.BaseDirectory,"quest-smoke-data");Directory.CreateDirectory(dir);
  new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));
  using var store=new Store(Path.Combine(dir,"market.db"));var settings=store.LoadSettings();settings.CloseToTray=false;foreach(var source in settings.Sources)source.Enabled=false;store.SaveSettings(settings);
  using var form=new MainForm(store,dir);form.Shown+=async(_,_)=>{try{await form.CheckQuestDrops(dir);File.WriteAllText(Path.Combine(dir,"results.txt"),"PASS: live Quest inline monster/maps, source attribution, source preference saved, empty selection clears results.");}catch(Exception e){File.WriteAllText(Path.Combine(dir,"results.txt"),"FAIL: "+e);Environment.ExitCode=1;}finally{form.ExitApplication();}};Application.Run(form);
 }
}
public sealed partial class MainForm
{
 internal async Task CheckQuestDrops(string dir){
  ShowPage("Item Finder");finderDropSource.SelectedItem="MapleStory Quest";finderSearch.Text="Wooden Club";RenderItemFinder();finderDetailTabs.SelectedIndex=1;features.Offline=false;dropItemKey="";QueueFinderDrops();
  for(int i=0;i<130&&finderDrops.Rows.Count==0;i++)await Task.Delay(500);
  if(!finderDrops.Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is DropMonster m&&m.Name=="Stump"&&m.Locations.Count>0)||!finderDropNote.Text.Contains("MapleStory Quest"))throw new Exception(finderDropNote.Text);
  SaveViewSession();if(!File.ReadAllText(SessionPath).Contains("MapleStory Quest"))throw new Exception("Source preference was not saved");CapturePreview(Path.Combine(dir,"quest-inline.png"));
  finderSearch.Text="NoSuchItemUnique123";RenderItemFinder();if(finderDrops.Rows.Count!=0)throw new Exception("Old drops remained after clearing selection");
 }
}
