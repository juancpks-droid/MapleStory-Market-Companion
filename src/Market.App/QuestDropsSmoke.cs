using Market.Core;
namespace Market.App;
static class QuestDropsSmoke
{
 public static void Run(bool npc=false){
  string dir=Path.Combine(AppContext.BaseDirectory,npc?"npc-fallback-smoke-data":"quest-smoke-data");Directory.CreateDirectory(dir);
  new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));
  using var store=new Store(Path.Combine(dir,"market.db"));var settings=store.LoadSettings();settings.CloseToTray=false;foreach(var source in settings.Sources)source.Enabled=false;store.SaveSettings(settings);
  using var form=new MainForm(store,dir);form.Shown+=async(_,_)=>{try{if(npc)await form.CheckLiveNpcFallback(dir);else await form.CheckQuestDrops(dir);File.WriteAllText(Path.Combine(dir,"results.txt"),npc?"PASS: live NPC fallback price 8 displayed with Quest attribution; market graph remains visible and empty.":"PASS: live Quest inline monster/maps, source attribution, source preference saved, empty selection clears results.");}catch(Exception e){File.WriteAllText(Path.Combine(dir,"results.txt"),"FAIL: "+e);Environment.ExitCode=1;}finally{form.ExitApplication();}};Application.Run(form);
 }
}
public sealed partial class MainForm
{
 internal async Task CheckLiveNpcFallback(string dir){
  ShowPage("Price Checker");search.Text="Orange Mushroom Cap";RenderPrices(false);features.Offline=false;npcKey="";QueueNpcPrices();
  for(int i=0;i<180&&npcSellMetric.Tag is not true;i++)await Task.Delay(500);
  if(npcSellMetric.Value!="8 mesos"||!npcPriceNote.Text.Contains("MapleStory Quest"))throw new Exception(npcPriceNote.Text);
  priceDetails.SelectedIndex=1;RenderHistory();if(history.Points.Count!=0||!history.Visible)throw new Exception("NPC price polluted market graph");CapturePreview(Path.Combine(dir,"npc-fallback.png"));
 }
 internal async Task CheckQuestDrops(string dir){
  ShowPage("Item Finder");finderDropSource.SelectedItem="MapleStory Quest";finderSearch.Text="Wooden Club";RenderItemFinder();finderDetailTabs.SelectedIndex=1;features.Offline=false;dropItemKey="";QueueFinderDrops();
  for(int i=0;i<130&&finderDrops.Rows.Count==0;i++)await Task.Delay(500);
  if(!finderDrops.Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is DropMonster m&&m.Name=="Stump"&&m.Locations.Count>0)||!finderDropNote.Text.Contains("MapleStory Quest"))throw new Exception(finderDropNote.Text);
  SaveViewSession();if(!File.ReadAllText(SessionPath).Contains("MapleStory Quest"))throw new Exception("Source preference was not saved");CapturePreview(Path.Combine(dir,"quest-inline.png"));
  finderSearch.Text="NoSuchItemUnique123";RenderItemFinder();if(finderDrops.Rows.Count!=0)throw new Exception("Old drops remained after clearing selection");
 }
}
