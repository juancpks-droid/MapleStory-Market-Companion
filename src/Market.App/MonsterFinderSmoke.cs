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
  Check(!AccuracyMonsterCatalog.IsDisplayName("???")&&!AccuracyMonsterCatalog.IsDisplayName("A transparent item for checking elimination")&&!AccuracyMonsterCatalog.IsDisplayName("Stat Change Test"),"Placeholder filter failed");
  Check(AccuracyMonsterCatalog.IsDisplayName("Straw Target Dummy")&&AccuracyMonsterCatalog.IsDisplayName("Latest Hits Compilation"),"Real monster removed");
  monsterQuery.Text="transparent";Check(monsterMatches.Items.Count==0,"Placeholder still searchable");
  var ambiguous=HistoricalMonster("Pianus");Check(MonsterStat(ambiguous,"Avoidability") is null&&ambiguous.Notice.Contains("Multiple"),"Ambiguous variant guessed");
  features.Offline=false;monsterChosen.Text="Advanced Pianus";monsterQuery.Text="Advanced Pianus";await LoadMonsterFinder();
  var historical=await LoadHistoricalMonster("Advanced Pianus",stop.Token);Check(MonsterStat(historical,"Level")==100&&MonsterStat(historical,"Avoidability")==40,"Historical fallback stats failed");
  monsterGuide=historical;RenderMonsterFinder();Check(monsterFacts.Rows.Count>=5&&monsterDrops.Rows[0].Cells[0].Value?.ToString()=="Unknown"&&monsterMaps.Rows[0].Cells[0].Value?.ToString()=="Unknown","Fallback facts or unknown drop/map labels missing");
  UseMonsterAccuracy();Check(accuracyMobLevel.Value==100&&accuracyAvoid.Value==40&&accuracyReference.Text.Contains("Historical GMS"),"Fallback accuracy source lost");ShowPage("Monster Finder");CapturePreview(Path.Combine(dataDirectory,"advanced-pianus-fallback.png"));
  features.Offline=true;var cached=await LoadHistoricalMonster("Advanced Pianus",stop.Token);Check(MonsterStat(cached,"Avoidability")==40,"Offline fallback cache missing");
  monsterQuery.Text="Jr. Balrog";monsterChosen.Text="Jr. Balrog";await LoadMonsterFinder();
  var drop=monsterDrops.Rows.Cast<DataGridViewRow>().Select(r=>r.Tag).OfType<GuideLink>().First();search.Text=drop.Name;ShowPage("Price Checker");await LookupPricesNow();Check(DisplayedPriceQuery==ResolvePriceQuery(drop.Name),"Drop price handoff mismatch");
  ShowPage("Monster Finder");foreach(var theme in MerchantTools.Themes){features.Theme=theme;ApplyGuideStyle();Check(monsterFacts.Rows.Count>0,"Theme removed facts");}CapturePreview(Path.Combine(dataDirectory,"monster-finder.png"));
  UseMonsterAccuracy();SaveViewSession();
 }
}
