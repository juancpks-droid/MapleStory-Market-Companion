using Market.Core;
namespace Market.App;
public sealed partial class MainForm {
 readonly NumericUpDown accuracyLevel=new(){Minimum=1,Maximum=200,Value=30,Width=150},accuracyAcc=new(){Minimum=0,Maximum=100000,Value=41,Width=150},accuracyMobLevel=new(){Minimum=1,Maximum=200,Value=22,Width=150},accuracyAvoid=new(){Minimum=0,Maximum=100000,Value=13,Width=150};
 readonly ComboBox accuracyPotion=new GuideComboBox(){DropDownStyle=ComboBoxStyle.DropDownList,Width=270},accuracyMonster=new GuideComboBox(){DropDownStyle=ComboBoxStyle.DropDownList,Width=270};
 readonly Label accuracyResult=new(){AutoSize=true,MaximumSize=new Size(760,0),Padding=new Padding(8),Font=new Font("Segoe UI",16,FontStyle.Bold)};
 readonly Label accuracyTargets=new(){AutoSize=true,MaximumSize=new Size(760,0),Padding=new Padding(8),Font=new Font("Segoe UI",11)};
 static readonly (string Name,int Level,int Avoid)[] AccuracyMonsters=[("Custom monster",22,13),("Snail",1,0),("Orange Mushroom",8,0),("Green Mushroom",14,8),("Horny Mushroom",22,13),("Zombie Mushroom",24,14),("Wild Boar",25,15),("Zombie Lupin",41,29)];
 bool settingAccuracy;
 void BuildAccuracy(){
  var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(20)};pages["Accuracy"].Controls.Add(panel);
  void Note(string text){panel.Controls.Add(new Label{Text=text,AutoSize=true,MaximumSize=new Size(760,0),Margin=new Padding(6,8,6,8)});}
  void Field(string label,Control input){var row=Toolbar();row.AutoSize=true;row.Controls.Add(new Label{Text=label,Width=290,Height=30,TextAlign=ContentAlignment.MiddleLeft});input.AccessibleName=label;row.Controls.Add(input);panel.Controls.Add(row);}
  Note("Estimate your hit chance and the accuracy needed for a monster. Calculates instantly, including offline.");
  accuracyPotion.Items.AddRange(["None / already included","Sniper Potion +5","Supreme Sniper Potion +7"]);accuracyPotion.SelectedIndex=0;
  accuracyMonster.Items.AddRange(AccuracyMonsters.Select(m=>m.Name).Cast<object>().ToArray());accuracyMonster.SelectedItem="Horny Mushroom";
  Field("Your level",accuracyLevel);Field("Your ACC (before selected potion)",accuracyAcc);Field("Potion bonus",accuracyPotion);Field("Monster preset",accuracyMonster);Field("Monster level",accuracyMobLevel);Field("Monster avoid (EVA)",accuracyAvoid);
  Note("Choose None if your displayed ACC already includes a potion. The two potion bonuses do not stack.");panel.Controls.Add(accuracyResult);panel.Controls.Add(accuracyTargets);
  Note("Estimate based on the published Classic World model, not a guarantee of live game behavior. Physical attacks and offensive spells use this hit check. Darkness and other separate effects are not included.");
  Note("Formula and selected monster facts: NiaMeowDB • checked October 17, 2026. Use custom stats for other monsters or changed game values.");
  var links=Toolbar();links.Controls.Add(Button("Formula source",()=>Open("https://meowdb.com/msclassic/formulas")));links.Controls.Add(Button("Original simulator",()=>Open("https://meowdb.com/msclassic/accuracy-sim")));links.Controls.Add(Button("Reset calculator",()=>{accuracyLevel.Value=30;accuracyAcc.Value=41;accuracyPotion.SelectedIndex=0;accuracyMonster.SelectedItem="Horny Mushroom";accuracyMobLevel.Value=22;accuracyAvoid.Value=13;accuracyMonster.SelectedItem="Horny Mushroom";}));panel.Controls.Add(links);
  accuracyMonster.SelectedIndexChanged+=(_,_)=>{if(accuracyMonster.SelectedIndex>0){settingAccuracy=true;var m=AccuracyMonsters[accuracyMonster.SelectedIndex];accuracyMobLevel.Value=m.Level;accuracyAvoid.Value=m.Avoid;settingAccuracy=false;}RenderAccuracy();};
  foreach(var n in new[]{accuracyLevel,accuracyAcc,accuracyMobLevel,accuracyAvoid})n.ValueChanged+=(_,_)=>{if(!settingAccuracy&&(n==accuracyMobLevel||n==accuracyAvoid))accuracyMonster.SelectedIndex=0;RenderAccuracy();};accuracyPotion.SelectedIndexChanged+=(_,_)=>RenderAccuracy();RenderAccuracy();
 }
 void RenderAccuracy(){
  double level=(double)accuracyLevel.Value,mob=(double)accuracyMobLevel.Value,avoid=(double)accuracyAvoid.Value;int bonus=accuracyPotion.SelectedIndex==1?5:accuracyPotion.SelectedIndex==2?7:0;double total=(double)accuracyAcc.Value+bonus;
  accuracyResult.Text=$"Estimated hit chance: {AccuracyCalculator.HitChance(level,total,mob,avoid)*100:0.0}%\nTotal ACC: {total:N0} • Level penalty: {Math.Max(0,mob-level):N0}";
  accuracyTargets.Text=string.Join("\n\n",new[]{.90,.95,1.0}.Select(t=>{var needed=AccuracyCalculator.NeededAcc(level,mob,avoid,t);return $"{t*100:0}% hit chance: {needed:N0} total ACC • {Math.Max(0,needed-total):N0} more needed";}));
 }
}
