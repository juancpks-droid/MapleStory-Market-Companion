using Market.Core;
namespace Market.App;
static class MonsterFinderSmoke{
 public static void Run(){
  var dir=Path.Combine(AppContext.BaseDirectory,"monster-finder-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var report=Path.Combine(AppContext.BaseDirectory,"monster-finder-result.txt");
  new CompanionFeatures{SetupComplete=true,Offline=true}.Save(Path.Combine(dir,"companion-features.json"));
  using(var store=new Store(Path.Combine(dir,"market.db"))){var settings=store.LoadSettings();settings.AccessVersion=5;settings.CloseToTray=false;foreach(var s in settings.Sources)s.Enabled=false;store.SaveSettings(settings);using var form=new MainForm(store,dir);form.Shown+=async(_,_)=>{try{await form.CheckMonsterFinder(false);File.WriteAllText(report,"PASS: three monster references, stats, drops, maps, exact Accuracy handoff, price handoff, local filters, all themes, offline cache.\n");}catch(Exception e){File.WriteAllText(report,"FAIL: "+e);Environment.ExitCode=1;}finally{form.ExitApplication();}};Application.Run(form);}
  if(Environment.ExitCode!=0)return;
  using(var store=new Store(Path.Combine(dir,"market.db"))){using var form=new MainForm(store,dir);form.Shown+=async(_,_)=>{try{await form.CheckMonsterFinder(true);File.AppendAllText(report,"PASS: reopened selection, search, source label and exact transferred stats.\n");}catch(Exception e){File.AppendAllText(report,"FAIL: "+e);Environment.ExitCode=1;}finally{form.ExitApplication();}};Application.Run(form);}
 }
}
public sealed partial class MainForm{
 internal async Task CheckMonsterFinder(bool restored){
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  timer.Stop();updateTimer.Stop();
  if(restored){Check(selected=="Accuracy"&&accuracyMobLevel.Value==80&&accuracyAvoid.Value==49&&accuracyReference.Text.Contains("Jr. Balrog"),"Accuracy source values lost on reopen");Check(monsterChosen.Text=="Jr. Balrog"&&monsterQuery.Text=="Jr. Balrog","Monster selection/search lost");ShowPage("Monster Finder");await LoadMonsterFinder();Check(monsterGuide?.Name=="Jr. Balrog","Offline details failed to restore");CapturePreview(Path.Combine(dataDirectory,"monster-restored.png"));return;}
  ShowPage("Monster Finder");Check(navigation["Monster Finder"].Visible,"Monster Finder missing from menu");
  foreach(var (name,level,avoid) in new[]{("Slime",6,0),("Blue Snail",2,0),("Jr. Balrog",80,49)}){
   monsterQuery.Text=name;Check(monsterMatches.Items.Contains(name),"Search missing "+name);monsterChosen.Text=name;await LoadMonsterFinder();
   Check(monsterGuide?.Name==name&&MonsterStat(monsterGuide,"Level")==level&&MonsterStat(monsterGuide,"Avoidability")==avoid,"Wrong reference stats "+name);
   Check(monsterDrops.Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is GuideLink)&&monsterMaps.Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is GuideLink),"Missing drops/maps "+name);
   UseMonsterAccuracy();Check(selected=="Accuracy"&&accuracyMobLevel.Value==level&&accuracyAvoid.Value==avoid&&accuracyReference.Text.Contains(name),"Accuracy handoff mismatched "+name);ShowPage("Monster Finder");
  }
  var drop=monsterDrops.Rows.Cast<DataGridViewRow>().Select(r=>r.Tag).OfType<GuideLink>().First();search.Text=drop.Name;ShowPage("Price Checker");await LookupPricesNow();Check(DisplayedPriceQuery==ResolvePriceQuery(drop.Name),"Drop price handoff mismatch");
  ShowPage("Monster Finder");foreach(var theme in MerchantTools.Themes){features.Theme=theme;ApplyGuideStyle();Check(monsterFacts.Rows.Count>0,"Theme removed facts");}CapturePreview(Path.Combine(dataDirectory,"monster-finder.png"));
  UseMonsterAccuracy();SaveViewSession();
 }
}
