using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly Stack<string> backPages=new(),forwardPages=new();bool replayNavigation;
 Button? backPageButton,forwardPageButton;
 void TrackPage(string page){
  if(!replayNavigation&&page!=selected){backPages.Push(selected);forwardPages.Clear();}
  if(backPageButton is not null)backPageButton.Enabled=backPages.Count>0;
  if(forwardPageButton is not null)forwardPageButton.Enabled=forwardPages.Count>0;
 }
 void NavigateHistory(bool back){
  var from=back?backPages:forwardPages;var to=back?forwardPages:backPages;if(from.Count==0)return;
  to.Push(selected);replayNavigation=true;try{ShowPage(from.Pop());}finally{replayNavigation=false;}
 }
 void BuildMarketShortcuts(){
  ((ClassicTitle)title).PriceStatus=()=>lookupStatus.Text;lookupStatus.TextChanged+=(_,_)=>title.Invalidate();
  var actions=new FlowLayoutPanel(){Dock=DockStyle.Right,Width=190,Padding=new Padding(0,13,0,0),WrapContents=false};
  backPageButton=Button("←",()=>NavigateHistory(true));forwardPageButton=Button("→",()=>NavigateHistory(false));var jump=Button("Go to",ShowQuickJump);
  foreach(var button in new[]{backPageButton,forwardPageButton,jump}){button.AutoSize=false;button.Width=button==jump?70:42;button.Height=34;actions.Controls.Add(button);}
  backPageButton.AccessibleName="Previous page (Alt+Left)";forwardPageButton.AccessibleName="Next page (Alt+Right)";jump.AccessibleName="Go to page or saved item (Ctrl+Shift+P)";title.Controls.Add(actions);TrackPage(selected);
 }
 protected override bool ProcessCmdKey(ref Message message,Keys key){
  if(key==(Keys.Alt|Keys.Left)){NavigateHistory(true);return true;}
  if(key==(Keys.Alt|Keys.Right)){NavigateHistory(false);return true;}
  if(key==(Keys.Control|Keys.Shift|Keys.P)){ShowQuickJump();return true;}
  if(key==Keys.Escape&&search.Focused){search.Clear();return true;}
  return base.ProcessCmdKey(ref message,key);
 }
 void RememberPriceSearch(string query){
  query=query.Trim();if(query.Length<3||query.Length>200)return;features.RecentItems??=[];
  if(features.RecentItems.FirstOrDefault()==query)return;
  features.RecentItems.RemoveAll(x=>TextKey.Search(x)==TextKey.Search(query));features.RecentItems.Insert(0,query);features.RecentItems=features.RecentItems.Take(12).ToList();SaveFeatures();
 }
 void OpenSavedPrice(string name){search.Text=name;ShowPage("Price Checker");QueueItemLookup(name);}
 void ShowSavedMenu(Control anchor,bool watched){
  var names=watched?store.Items().Where(i=>i.Watch).Select(i=>i.Name).Distinct().Order().ToArray():(features.RecentItems??[]).ToArray();
  var menu=new ContextMenuStrip(){BackColor=GuidePalette.Surface,ForeColor=GuidePalette.Text};
  if(names.Length==0)menu.Items.Add(watched?"Watch an item to add a shortcut":"Your recent searches will appear here").Enabled=false;
  foreach(var name in names)menu.Items.Add(name,null,(_,_)=>OpenSavedPrice(name));
  if(!watched&&names.Length>0){menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Clear recent searches",null,(_,_)=>{features.RecentItems=[];SaveFeatures();});}
  menu.Closed+=(_,_)=>menu.Dispose();menu.Show(anchor,new Point(0,anchor.Height));
 }
 void AddSavedShortcuts(FlowLayoutPanel toolbar){
  Button? recent=null,watched=null;recent=Button("Recent items",()=>ShowSavedMenu(recent!,false));watched=Button("Watched items",()=>ShowSavedMenu(watched!,true));toolbar.Controls.Add(recent);toolbar.Controls.Add(watched);
 }
 sealed record JumpChoice(string Label,string Page,string Item){public override string ToString()=>Label;}
 void ShowQuickJump(){
  using var dialog=new Form(){Text="Go to • Ctrl+Shift+P",Size=new Size(560,450),MinimumSize=new Size(440,330),StartPosition=FormStartPosition.CenterParent,BackColor=GuidePalette.Back,ForeColor=GuidePalette.Text,Font=Font,KeyPreview=true,Padding=new Padding(16)};
  var input=new TextBox(){Dock=DockStyle.Top,PlaceholderText="Find a page or watched / recent item",Font=new Font("Segoe UI",12),BackColor=GuidePalette.Raised,ForeColor=GuidePalette.Text};
  var list=new ListBox(){Dock=DockStyle.Fill,BackColor=GuidePalette.Surface,ForeColor=GuidePalette.Text,BorderStyle=BorderStyle.None,IntegralHeight=false};
  var hint=new Label(){Dock=DockStyle.Bottom,Height=30,Text="Enter to open • Esc to close",ForeColor=GuidePalette.Muted};
  var choices=pages.Keys.Where(p=>MarketPage(p)&&p!="Backups").Select(p=>new JumpChoice("Page  ·  "+p,p,""))
   .Concat(store.Items().Where(i=>i.Watch).Select(i=>i.Name).Concat(features.RecentItems??[]).Distinct().Select(n=>new JumpChoice("Item  ·  "+n,"Price Checker",n))).ToArray();
  void Fill(){list.Items.Clear();list.Items.AddRange(choices.Where(c=>TextKey.Search(c.Label).Contains(TextKey.Search(input.Text))).ToArray());if(list.Items.Count>0)list.SelectedIndex=0;}
  void Open(){if(list.SelectedItem is not JumpChoice choice)return;dialog.Close();if(choice.Item.Length>0)OpenSavedPrice(choice.Item);else ShowPage(choice.Page);}
  input.TextChanged+=(_,_)=>Fill();list.DoubleClick+=(_,_)=>Open();dialog.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){dialog.Close();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Enter){Open();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Down&&input.Focused){list.Focus();e.SuppressKeyPress=true;}};
  dialog.Controls.Add(list);dialog.Controls.Add(input);dialog.Controls.Add(hint);Fill();dialog.Shown+=(_,_)=>input.Focus();dialog.ShowDialog(this);
 }
}
