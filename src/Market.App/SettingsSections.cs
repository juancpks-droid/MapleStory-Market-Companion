namespace Market.App;
public sealed partial class MainForm
{
 readonly TabControl settingsSections=new(){Dock=DockStyle.Fill};
 void BuildSettingsSections(){
  var preferences=new TabPage("Preferences");var backups=new TabPage("Backups");
  foreach(Control control in pages["Settings"].Controls.Cast<Control>().ToArray())preferences.Controls.Add(control);
  foreach(Control control in pages["Backups"].Controls.Cast<Control>().ToArray())backups.Controls.Add(control);
  settingsSections.TabPages.Add(preferences);settingsSections.TabPages.Add(backups);
  pages["Settings"].Controls.Add(settingsSections);
 }
}
