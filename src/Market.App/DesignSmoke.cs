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
   CheckQuickJump();ShowPage("Dashboard");CapturePreview(Path.Combine(dataDirectory,"redesign-"+theme.Replace(" ","-")+".png"));
  }
  features.TextSize=text;themeChoice.SelectedItem=original;ApplyTextSize();Size=new Size(1024,720);ShowPage("Settings");CapturePreview(Path.Combine(dataDirectory,"responsive-settings.png"));
  checks.Add("Seven primary screens opened in all four themes at compact/large sizes and larger text; Settings needs no horizontal scroll");
  checks.Add("Go to dialog filters pages and Enter opens the selected destination in all four themes");
 }
 void CheckQuickJump(){
  Exception? failure=null;using var probe=new System.Windows.Forms.Timer(){Interval=100};
  probe.Tick+=(_,_)=>{probe.Stop();var dialog=Application.OpenForms.Cast<Form>().First(f=>f.Text.StartsWith("Go to"));try{
   var input=dialog.Controls.OfType<TextBox>().Single();var list=dialog.Controls.OfType<ListBox>().Single();input.Text="Price Checker";
   if(list.Items.Count!=1||!list.Items[0].ToString()!.Contains("Price Checker"))throw new Exception("Go to page filtering failed");
   using var shot=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(shot,dialog.ClientRectangle);shot.Save(Path.Combine(dataDirectory,"go-to-"+features.Theme.Replace(" ","-")+".png"));
   typeof(Form).GetMethod("OnKeyDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(dialog,[new KeyEventArgs(Keys.Enter)]);
  }catch(Exception e){failure=e;}finally{dialog.Close();}};
  ShowPage("Dashboard");probe.Start();ShowQuickJump();if(failure is not null)throw failure;if(selected!="Price Checker")throw new Exception("Go to Enter navigation failed");
 }
}
