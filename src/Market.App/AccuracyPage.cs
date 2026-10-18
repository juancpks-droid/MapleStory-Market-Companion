using Market.Core;
namespace Market.App;
public sealed partial class MainForm {
 readonly NumericUpDown accuracyLevel=new(){Minimum=1,Maximum=200,Value=30,Width=150},accuracyAcc=new(){Minimum=0,Maximum=100000,Value=41,Width=150},accuracyMobLevel=new(){Minimum=1,Maximum=200,Value=22,Width=150},accuracyAvoid=new(){Minimum=0,Maximum=100000,Value=13,Width=150};
 readonly ComboBox accuracyPotion=new GuideComboBox(){DropDownStyle=ComboBoxStyle.DropDownList,Width=270},accuracyMonster=new GuideComboBox(){DropDownStyle=ComboBoxStyle.DropDownList,Width=270};
 readonly Label accuracyResult=new(){AutoSize=true,MaximumSize=new Size(760,0),Padding=new Padding(8),Font=new Font("Segoe UI",16,FontStyle.Bold)};
 readonly Label accuracyTargets=new(){AutoSize=true,MaximumSize=new Size(760,0),Padding=new Padding(8),Font=new Font("Segoe UI",11)};
 static readonly (string Name,int Level,int Avoid)[] AccuracyMonsters=[("Custom monster",22,13),("Snail",1,0),("Orange Mushroom",8,0),("Green Mushroom",14,8),("Horny Mushroom",22,13),("Zombie Mushroom",24,14),("Wild Boar",25,15),("Zombie Lupin",41,29)];
 readonly TextBox accuracySearch=new(){Width=370,PlaceholderText="Monster name or ID"};
 readonly Label accuracySource=new(){AutoSize=true,MaximumSize=new Size(760,0)};
 readonly PublicReferenceClient accuracyClient=new("https://maplestory.io",false);
 CancellationTokenSource? accuracyLookup;
 bool settingAccuracy,accuracyUnavailable;
 void ConfigureAccuracySearch(){
  var names=new AutoCompleteStringCollection();
  names.AddRange(AccuracyMonsters.Select(m=>m.Name).Concat(AccuracyMonsterCatalog.All.Select(m=>m.Label)).Distinct().Order().ToArray());
  accuracySearch.AutoCompleteCustomSource=names;
  accuracySearch.AutoCompleteSource=AutoCompleteSource.CustomSource;
  accuracySearch.AutoCompleteMode=AutoCompleteMode.Suggest;
  accuracySearch.TextChanged+=(_,_)=>{FilterAccuracyMonsters();if(accuracySearch.Focused)SelectAccuracySearch(false);};
  accuracySearch.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){SelectAccuracySearch(true);e.SuppressKeyPress=true;}};
 }
 void SelectAccuracySearch(bool allowUnique){
  var query=accuracySearch.Text.Trim();if(query.Length==0)return;
  var exact=accuracyMonster.Items.Cast<string>().FirstOrDefault(n=>string.Equals(n,query,StringComparison.OrdinalIgnoreCase));
  if(exact is not null){accuracyMonster.SelectedItem=exact;return;}
  if(!allowUnique)return;
  var byId=AccuracyMonsterCatalog.All.FirstOrDefault(m=>m.Id.ToString()==query);
  if(byId is not null){accuracyMonster.SelectedItem=byId.Label;return;}
  var matches=AccuracyMonsterCatalog.All.Where(m=>m.Id.ToString()==query||m.Label.Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();
  if(matches.Length==1)accuracyMonster.SelectedItem=matches[0].Label;
 }
 void FilterAccuracyMonsters(){
  var keep=accuracyMonster.Text;settingAccuracy=true;accuracyMonster.BeginUpdate();accuracyMonster.Items.Clear();accuracyMonster.Items.AddRange(AccuracyMonsters.Select(m=>(object)m.Name).ToArray());
  accuracyMonster.Items.AddRange(AccuracyMonsterCatalog.All.Where(m=>m.Label==keep||m.Label.Contains(accuracySearch.Text.Trim(),StringComparison.OrdinalIgnoreCase)).Select(m=>(object)m.Label).ToArray());
  accuracyMonster.SelectedItem=keep;accuracyMonster.EndUpdate();settingAccuracy=false;
 }
 async Task LoadAccuracyMonster(){
  accuracyLookup?.Cancel();accuracyLookup=new();var ct=accuracyLookup.Token;
  var classic=Array.FindIndex(AccuracyMonsters,m=>m.Name==accuracyMonster.Text);
  accuracyUnavailable=false;
  if(classic>=0){if(classic>0){settingAccuracy=true;accuracyMobLevel.Value=AccuracyMonsters[classic].Level;accuracyAvoid.Value=AccuracyMonsters[classic].Avoid;settingAccuracy=false;}accuracySource.Text=classic==0?"Custom monster stats":"Selected classic preset • NiaMeowDB reference";RenderAccuracy();return;}
  var m=AccuracyMonsterCatalog.All.FirstOrDefault(m=>m.Label==accuracyMonster.Text);if(m is null)return;
  accuracyUnavailable=true;accuracySource.Text="Loading historical monster stats…";RenderAccuracy();
  try{
   string path=Path.Combine(dataDirectory,"accuracy-monsters",m.Id+".json"),json="";AccuracyMonsterStats? stats=null;
   try{json=File.ReadAllText(path);stats=AccuracyMonsterCatalog.Parse(json,m.Id);}catch{}
   if(stats is null){if(Offline){accuracySource.Text="Offline: no saved stats for this monster. Go online and retry, or enter custom stats.";RenderAccuracy();return;}json=await accuracyClient.Get("https://maplestory.io/api/gms/83/mob/"+m.Id,ct);stats=AccuracyMonsterCatalog.Parse(json,m.Id);try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,json);}catch{}}
   if(ct.IsCancellationRequested||IsDisposed)return;settingAccuracy=true;accuracyMobLevel.Value=stats.Level;accuracyAvoid.Value=stats.Avoid;settingAccuracy=false;accuracyUnavailable=false;
   accuracySource.Text="Historical GMS 83 • MapleStory.io • Game data © Nexon. Stats and availability may differ in Classic World.";
  }catch(OperationCanceledException){return;}catch{if(ct.IsCancellationRequested||IsDisposed)return;accuracySource.Text="Monster stats unavailable. Retry or enter custom stats. Missing avoid is never treated as zero.";}
  RenderAccuracy();
 }

 void BuildAccuracy(){
  var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(20)};pages["Accuracy"].Controls.Add(panel);
  void Note(string text){panel.Controls.Add(new Label{Text=text,AutoSize=true,MaximumSize=new Size(760,0),Margin=new Padding(6,8,6,8)});}
  void Field(string label,Control input){var row=Toolbar();row.AutoSize=true;row.Controls.Add(new Label{Text=label,Width=290,Height=30,TextAlign=ContentAlignment.MiddleLeft});input.AccessibleName=label;row.Controls.Add(input);panel.Controls.Add(row);}
  Note("Estimate your hit chance and the accuracy needed for a monster. Calculates instantly, including offline.");
  accuracyPotion.Items.AddRange(["None / already included","Sniper Potion +5","Supreme Sniper Potion +7"]);accuracyPotion.SelectedIndex=0;
  accuracyMonster.Items.AddRange(AccuracyMonsters.Select(m=>m.Name).Cast<object>().ToArray());accuracyMonster.SelectedItem="Horny Mushroom";FilterAccuracyMonsters();accuracyMonster.Width=470;ConfigureAccuracySearch();
  Field("Your level",accuracyLevel);Field("Your ACC (before selected potion)",accuracyAcc);Field("Potion bonus",accuracyPotion);Field("Search monsters",accuracySearch);Field("Monster preset",accuracyMonster);Field("Monster level",accuracyMobLevel);Field("Monster avoid (EVA)",accuracyAvoid);
  Note($"{AccuracyMonsterCatalog.All.Length:N0} named historical monsters, including bosses and variants. Search by name or ID.");panel.Controls.Add(accuracySource);
  Note("Choose None if your displayed ACC already includes a potion. The two potion bonuses do not stack.");panel.Controls.Add(accuracyResult);panel.Controls.Add(accuracyTargets);
  Note("Estimate based on the published Classic World model, not a guarantee of live game behavior. Physical attacks and offensive spells use this hit check. Darkness and other separate effects are not included.");
  Note("Formula and selected monster facts: NiaMeowDB • checked October 17, 2026. Expanded catalog: MapleStory.io historical GMS 83. Use custom stats for changed game values.");
  var links=Toolbar();links.Controls.Add(Button("Retry monster stats",async()=>await LoadAccuracyMonster()));links.Controls.Add(Button("Monster source",()=>{var m=AccuracyMonsterCatalog.All.FirstOrDefault(m=>m.Label==accuracyMonster.Text);if(m is not null)Open("https://maplestory.io/api/gms/83/mob/"+m.Id);}));links.Controls.Add(Button("Formula source",()=>Open("https://meowdb.com/msclassic/formulas")));links.Controls.Add(Button("Original simulator",()=>Open("https://meowdb.com/msclassic/accuracy-sim")));links.Controls.Add(Button("Reset calculator",()=>{accuracySearch.Text="";accuracyLevel.Value=30;accuracyAcc.Value=41;accuracyPotion.SelectedIndex=0;accuracyMonster.SelectedItem="Horny Mushroom";accuracyMobLevel.Value=22;accuracyAvoid.Value=13;accuracyMonster.SelectedItem="Horny Mushroom";}));panel.Controls.Add(links);
  accuracyMonster.SelectedIndexChanged+=async(_,_)=>{if(!settingAccuracy)await LoadAccuracyMonster();};
  FormClosed+=(_,_)=>accuracyLookup?.Cancel();
  foreach(var n in new[]{accuracyLevel,accuracyAcc,accuracyMobLevel,accuracyAvoid})n.ValueChanged+=(_,_)=>{if(!settingAccuracy&&(n==accuracyMobLevel||n==accuracyAvoid))accuracyMonster.SelectedIndex=0;RenderAccuracy();};accuracyPotion.SelectedIndexChanged+=(_,_)=>RenderAccuracy();RenderAccuracy();
 }
 void RenderAccuracy(){
  if(accuracyUnavailable){accuracyResult.Text="Waiting for monster stats";accuracyTargets.Text="Select Retry monster stats, or enter custom level and avoid values.";return;}
  double level=(double)accuracyLevel.Value,mob=(double)accuracyMobLevel.Value,avoid=(double)accuracyAvoid.Value;int bonus=accuracyPotion.SelectedIndex==1?5:accuracyPotion.SelectedIndex==2?7:0;double total=(double)accuracyAcc.Value+bonus;
  accuracyResult.Text=$"Estimated hit chance: {AccuracyCalculator.HitChance(level,total,mob,avoid)*100:0.0}%\nTotal ACC: {total:N0} • Level penalty: {Math.Max(0,mob-level):N0}";
  accuracyTargets.Text=string.Join("\n\n",new[]{.90,.95,1.0}.Select(t=>{var needed=AccuracyCalculator.NeededAcc(level,mob,avoid,t);return $"{t*100:0}% hit chance: {needed:N0} total ACC • {Math.Max(0,needed-total):N0} more needed";}));
 }
}
