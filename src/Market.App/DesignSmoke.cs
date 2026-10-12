namespace Market.App;
public sealed partial class MainForm
{
 internal void CheckDesign(List<string> checks){
  string original=features.Theme;int text=features.TextSize;
  foreach(var theme in Market.Core.MerchantTools.Themes){
   themeChoice.SelectedItem=theme;
   foreach(var size in new[]{new Size(1024,720),new Size(1380,920)}){
    Size=size;features.TextSize=size.Width==1024?13:10;ApplyTextSize();
    foreach(var page in new[]{"Dashboard","Price Checker","Item Finder","FM Tracker","Watchlist","Alert Inbox","Settings"}){
     ShowPage(page);PerformLayout();
     if(!pages[page].Visible||pages[page].Width<600||pages[page].Height<300)throw new Exception("Page layout failed: "+page);
     if(page=="Settings"){
      IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
      var scroller=All(settingsSections.TabPages[0]).OfType<Panel>().First(p=>p.AutoScroll&&p.Controls.OfType<FlowLayoutPanel>().Any(f=>f.FlowDirection==FlowDirection.TopDown));
      if(scroller.HorizontalScroll.Visible)throw new Exception("Settings requires horizontal scrolling: "+scroller.Width+" / "+scroller.Controls[0].Width+" bounds "+scroller.Controls[0].Bounds+" display "+scroller.DisplayRectangle+" max "+scroller.HorizontalScroll.Maximum+" children "+string.Join(";",scroller.Controls.Cast<Control>().Select(c=>c.GetType().Name+":"+c.Bounds)));
     }
    }
   }
   ShowPage("Dashboard");CapturePreview(Path.Combine(dataDirectory,"redesign-"+theme.Replace(" ","-")+".png"));
  }
  features.TextSize=text;themeChoice.SelectedItem=original;ApplyTextSize();Size=new Size(1024,720);ShowPage("Settings");CapturePreview(Path.Combine(dataDirectory,"responsive-settings.png"));
  checks.Add("Seven primary screens opened in all four themes at compact/large sizes and larger text; Settings needs no horizontal scroll");
 }
}
