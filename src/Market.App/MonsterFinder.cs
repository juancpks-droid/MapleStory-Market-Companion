using Market.Core;
namespace Market.App;
public sealed partial class MainForm{
 readonly TextBox monsterQuery=new(){Width=360,PlaceholderText="Monster name"},monsterChosen=new(){Visible=false};
 readonly CheckBox monsterBoss=new(){Text="Boss names only",AutoSize=true};
 readonly ListBox monsterMatches=new(){Dock=DockStyle.Top,Height=145};
 readonly Label monsterNote=new(){Dock=DockStyle.Top,Height=76,Padding=new Padding(8)};
 readonly DataGridView monsterFacts=Grid(),monsterDrops=Grid(),monsterMaps=Grid();
 readonly GameGuideReader monsterReader=new();
 GameGuide? monsterGuide;CancellationTokenSource? monsterStop;int monsterGeneration;Task? monsterJob;
 Button? monsterAccuracy;
 static string MonsterPath(string name)=>GameGuideReader.ItemPath(name).Replace("/items/","/monsters/");
 void BuildMonsterFinder(){
  var page=pages["Monster Finder"];var bar=Toolbar();bar.AutoSize=true;bar.Controls.Add(monsterQuery);bar.Controls.Add(monsterBoss);
  bar.Controls.Add(Button("Open monster",()=>{if(monsterMatches.SelectedItem is string name){monsterChosen.Text=name;_=LoadMonsterFinder();}}));
  bar.Controls.Add(Button("Refresh reference",()=>_=LoadMonsterFinder(true)));
  monsterAccuracy=Button("Use these stats in Accuracy",UseMonsterAccuracy);monsterAccuracy.Enabled=false;bar.Controls.Add(monsterAccuracy);
  var tabs=new MarketTabs{Dock=DockStyle.Fill};foreach(var (title,grid) in new[]{("Stats",monsterFacts),("Drops",monsterDrops),("Maps",monsterMaps)}){var tab=new TabPage(title);tab.Controls.Add(grid);tabs.TabPages.Add(tab);grid.Columns.Add("Name","Name");grid.Columns.Add("Detail","Details");grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;}
  monsterFacts.Columns[0].HeaderText="Stat";monsterFacts.Columns[1].HeaderText="Value";
  var dropsBar=Toolbar();dropsBar.Controls.Add(Button("Check selected drop price",()=>{if(monsterDrops.CurrentRow?.Tag is GuideLink link){search.Text=link.Name;ShowPage("Price Checker");_=LookupPricesNow();}}));tabs.TabPages[1].Controls.Add(dropsBar);
  page.Controls.Add(tabs);page.Controls.Add(monsterNote);page.Controls.Add(monsterMatches);page.Controls.Add(bar);
  var names=new AutoCompleteStringCollection();names.AddRange(AccuracyMonsterCatalog.All.Where(m=>AccuracyMonsterCatalog.IsDisplayName(m.Name)).Select(m=>m.Name).Distinct().Order().ToArray());monsterQuery.AutoCompleteCustomSource=names;monsterQuery.AutoCompleteSource=AutoCompleteSource.CustomSource;monsterQuery.AutoCompleteMode=AutoCompleteMode.Suggest;
  monsterQuery.TextChanged+=(_,_)=>FilterMonsterFinder();monsterBoss.CheckedChanged+=(_,_)=>FilterMonsterFinder();
  monsterQuery.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){var exact=monsterMatches.Items.Cast<string>().FirstOrDefault(n=>n.Equals(monsterQuery.Text.Trim(),StringComparison.OrdinalIgnoreCase));if(exact is not null)monsterMatches.SelectedItem=exact;if(monsterMatches.SelectedItem is string n){monsterChosen.Text=n;_=LoadMonsterFinder();}e.SuppressKeyPress=true;}};
  monsterMatches.DoubleClick+=(_,_)=>{if(monsterMatches.SelectedItem is string n){monsterChosen.Text=n;_=LoadMonsterFinder();}};
  FitPriceText(monsterNote);FilterMonsterFinder();monsterNote.Text="Select a monster, then Open monster. Stats, drops and maps use labeled CCPLZ references. Coverage may be partial.";
  FormClosed+=async(_,_)=>{monsterStop?.Cancel();try{if(monsterJob is not null)await monsterJob;}finally{monsterReader.Dispose();}};
 }
 void FilterMonsterFinder(){var keep=monsterMatches.SelectedItem as string;monsterMatches.BeginUpdate();monsterMatches.Items.Clear();foreach(var n in AccuracyMonsterCatalog.All.Where(m=>AccuracyMonsterCatalog.IsDisplayName(m.Name)&&(!monsterBoss.Checked||m.Boss)&&m.Name.Contains(monsterQuery.Text.Trim(),StringComparison.OrdinalIgnoreCase)).Select(m=>m.Name).Distinct().Order().Take(100))monsterMatches.Items.Add(n);if(keep is not null&&monsterMatches.Items.Contains(keep))monsterMatches.SelectedItem=keep;else if(monsterMatches.Items.Count>0)monsterMatches.SelectedIndex=0;monsterMatches.EndUpdate();}
 Task LoadMonsterFinder(bool refresh=false)=>monsterJob=ReadMonsterFinder(refresh);
 async Task ReadMonsterFinder(bool refresh=false){
  string name=monsterChosen.Text;if(!AccuracyMonsterCatalog.IsDisplayName(name)){monsterChosen.Text="";monsterNote.Text="Choose a named monster; placeholder entries are excluded.";return;}
  int generation=++monsterGeneration;monsterStop?.Cancel();monsterStop?.Dispose();monsterStop=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);monsterStop.CancelAfter(TimeSpan.FromSeconds(55));var ct=monsterStop.Token;
  if(monsterGuide?.Name!=name){monsterGuide=null;monsterFacts.Rows.Clear();monsterDrops.Rows.Clear();monsterMaps.Rows.Clear();monsterAccuracy!.Enabled=false;}
  if(monsterGuide is null){monsterGuide=HistoricalMonster(name);RenderMonsterFinder();}
  monsterNote.Text=name+" • Checking drop/map reference. Historical catalog stats are shown until available…";
  try{using var guideDeadline=CancellationTokenSource.CreateLinkedTokenSource(ct);guideDeadline.CancelAfter(TimeSpan.FromSeconds(12));var guide=await monsterReader.Lookup(dataDirectory,MonsterPath(name),name,Offline,guideDeadline.Token,refresh);if(generation!=monsterGeneration||IsDisposed)return;if(guide is null)throw new IOException("No saved reference");monsterGuide=guide;RenderMonsterFinder();}
  catch(OperationCanceledException){if(generation!=monsterGeneration||IsDisposed)return;}
  catch(Exception e) when(e is HttpRequestException or IOException or FormatException){if(generation!=monsterGeneration||IsDisposed)return;}
  if(generation!=monsterGeneration||IsDisposed)return;
  if(monsterGuide?.Source!="CCPLZ"){
   var historical=await LoadHistoricalMonster(name,ct);
   if(generation!=monsterGeneration||IsDisposed)return;
   monsterGuide=historical;
   RenderMonsterFinder();
  }

 }
 static int? MonsterStat(GameGuide? guide,string field){var value=guide?.Fields.FirstOrDefault(f=>f.Label.Equals(field,StringComparison.OrdinalIgnoreCase))?.Value.Replace(",","");return int.TryParse(value,out int n)&&n>=0?n:null;}
 void RenderMonsterFinder(){
  if(monsterGuide is not {} guide)return;monsterFacts.Rows.Clear();monsterDrops.Rows.Clear();monsterMaps.Rows.Clear();
  monsterNote.Text=guide.Name+" • "+guide.Source+" • Checked "+guide.CheckedAt.ToLocalTime().ToString("g")+(guide.CheckedAt<DateTimeOffset.UtcNow.AddDays(-7)?" • Older reference":"")+"\n"+guide.Notice;
  foreach(var f in guide.Fields)monsterFacts.Rows.Add(f.Label,f.Value);
  foreach(var s in guide.Sections)foreach(var link in s.Links){var grid=link.Path.StartsWith("/items/")?monsterDrops:link.Path.StartsWith("/maps/")?monsterMaps:null;if(grid is null)continue;int row=grid.Rows.Add(link.Name,s.Title+" • "+link.Detail+(s.Total>s.Links.Count?$" • partial list ({s.Links.Count}/{s.Total})":""));grid.Rows[row].Tag=link;}
  if(monsterDrops.Rows.Count==0)monsterDrops.Rows.Add("Unknown","No drop reference available.");if(monsterMaps.Rows.Count==0)monsterMaps.Rows.Add("Unknown","No map reference available.");
  monsterAccuracy!.Enabled=MonsterStat(guide,"Level") is >=1 and <=200&&MonsterStat(guide,"Avoidability") is >=0 and <=100000;
 }
 void UseMonsterAccuracy(){
  if(monsterGuide is not {} guide||MonsterStat(guide,"Level") is not int level||MonsterStat(guide,"Avoidability") is not int avoid||level is <1 or >200||avoid>100000)return;
  accuracyLookup?.Cancel();settingAccuracy=true;accuracyMonster.SelectedIndex=0;accuracyMobLevel.Value=level;accuracyAvoid.Value=avoid;accuracySearch.Text="";settingAccuracy=false;accuracyUnavailable=false;
  accuracyReference.Text=guide.Source+" reference: "+guide.Name+". Using Monster Finder stats; values remain editable.";accuracySource.Text="Custom reference values";RenderAccuracy();ShowPage("Accuracy");
 }
}
