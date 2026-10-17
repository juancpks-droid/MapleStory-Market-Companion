using Market.Core;
using System.Text;
namespace Market.App;
public sealed partial class MainForm
{
 string LookupReport(string name,string id,string status)=>$"Maple Market {Application.ProductVersion}\nItem: {name} (#{id})\nTool: {selected}\nMode: {(Offline?"Offline":"Online")}\nStatus: {status}\n"+string.Join("\n",sourceResults.Select(r=>$"Source: {r.Provider} · checked {r.CheckedAt:O} · updated {r.Updated}"))+"\nDescribe what looks wrong: ";
 void CopyLookupReport(string name,string id,string status){try{Clipboard.SetText(LookupReport(name,id,status));footer.Text="Problem report copied. Add what looks wrong before sharing; no player data or listing notes included.";}catch{footer.Text="Could not copy the report. Try again.";}}
 void AddLookupTools(FlowLayoutPanel toolbar,Func<(string Name,string Id)> item,Func<string> status){
  Button? saved=null;saved=Button("Saved items",()=>{var menu=new ContextMenuStrip{BackColor=GuidePalette.Surface,ForeColor=GuidePalette.Text};menu.Items.Add("Recent items",null,(_,_)=>ShowSavedMenu(saved!,false));menu.Items.Add("Watched favorites",null,(_,_)=>ShowSavedMenu(saved!,true));menu.Closed+=(_,_)=>menu.Dispose();menu.Show(saved!,new Point(0,saved!.Height));});toolbar.Controls.Add(saved);
  toolbar.Controls.Add(Button("Item summary",()=>{var value=item();if(value.Name.Length>0)ShowLookupSummary(value.Name,value.Id);}));
  toolbar.Controls.Add(Button("Copy problem report",()=>{var value=item();CopyLookupReport(value.Name,value.Id,status());}));
 }
 void ShowLookupSummary(string name,string id){
  if(id.Length==0)id=iconCatalog.Entries.FirstOrDefault(e=>TextKey.Search(e.Name)==TextKey.Search(name))?.Id??"";
  if(id.Length==0&&ScrollNames.Resolve(name) is {} scroll)id=scroll.ClassicId;
  if(id.Length==0){MessageBox.Show(this,"Choose an exact item or scroll percentage first.","Item summary");return;}RememberPriceSearch(name);
  var dialog=new Form{Text=name+" · Item summary",Size=new Size(700,660),MinimumSize=new Size(450,380),StartPosition=FormStartPosition.CenterParent,BackColor=GuidePalette.Surface,ForeColor=GuidePalette.Text};
  var text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=GuidePalette.Surface,ForeColor=GuidePalette.Text,Font=new Font("Segoe UI",11),BorderStyle=BorderStyle.None};
  var status=new Label{Dock=DockStyle.Top,Height=44,Text="Loading saved item references…",Padding=new Padding(8)};var actions=Toolbar();actions.Dock=DockStyle.Bottom;
  var cancellation=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);cancellation.CancelAfter(TimeSpan.FromSeconds(45));var reader=new GameGuideReader();
  actions.Controls.Add(Button("Check prices",()=>{dialog.Close();OpenSavedPrice(name);}));actions.Controls.Add(Button("Watch / unwatch",()=>ToggleItemWatch(name)));actions.Controls.Add(Button("Copy problem report",()=>CopyLookupReport(name,id,status.Text)));
  dialog.Controls.Add(text);dialog.Controls.Add(status);dialog.Controls.Add(actions);
  var summary=new StringBuilder(name+"\r\n\r\nPLAYER MARKET — Kerning Exchange saved observations\r\n");
  var rows=PlayerMarketSource.Only(EligibleObservations()).Where(o=>ScrollNames.Matches(o.Name,name)&&o.Available).ToList();var recent=rows.Where(o=>o.RetrievedAt>DateTimeOffset.UtcNow.AddHours(settings.FreshHours)&&o.ObservedAt is {} at&&at>DateTimeOffset.UtcNow.AddHours(settings.FreshHours)).ToList();
  summary.AppendLine($"{recent.Count} recent observations; {rows.Count-recent.Count} older or undated observations. Asking prices are not completed sales. Equipment stats may differ.");
  foreach(var row in recent.OrderBy(o=>o.UnitPrice).Take(5))summary.AppendLine($"{row.Kind}: {TextKey.Mesos(row.UnitPrice)} mesos / unit · {row.Provider} · posted {row.ObservedAt:g}");
  if(recent.Count==0)summary.AppendLine("Not enough recent evidence for a current market value. Use Check prices to refresh.");text.Text=summary.ToString();
  dialog.Shown+=async(_,_)=>{try{
   var npc=await SharedGameReference.Npc(dataDirectory,id,name,true,cancellation.Token);if(npc is not null){summary.AppendLine($"\r\nNPC REFERENCE — {npc.Source}\r\nSell: {TextKey.Mesos(npc.Sell)} · Buy: {TextKey.Mesos(npc.BuyMin)} mesos\r\nChecked {npc.CheckedAt:g}; separate from player market value.");if(!dialog.IsDisposed)text.Text=summary.ToString();}
   if(npc is null)summary.AppendLine("\r\nNPC REFERENCE\r\nNo saved NPC price record. The item guide below may contain a source-labelled NPC sell price; Check prices can look for more references.");
   var drops=await SharedGameReference.Drops(dataDirectory,id,name,true,cancellation.Token);if(drops is not null){summary.AppendLine("\r\nSAVED DROPS & MAPS — "+drops.Source);foreach(var m in drops.Drops.Monsters.Take(12))summary.AppendLine(m.Name+" · "+m.Evidence+"\r\n"+string.Join(" · ",m.Locations.Select(l=>l.Name)));}else summary.AppendLine("\r\nSAVED DROPS & MAPS\r\nNo saved drop record. The item guide below may contain historical references.");
   if(!dialog.IsDisposed){text.Text=summary.ToString();status.Text=Offline?"Reading offline guide…":"Checking shared item guide; saved sections stay visible…";}
   var guide=await reader.Lookup(dataDirectory,GameGuideReader.ItemPath(name),ScrollNames.Resolve(name)?.Name??name,Offline,cancellation.Token);
   if(guide is not null){summary.AppendLine("\r\nITEM GUIDE — "+guide.Source+" · checked "+guide.CheckedAt.ToLocalTime().ToString("g"));foreach(var field in guide.Fields)summary.AppendLine(field.Label+": "+field.Value);foreach(var section in guide.Sections){summary.AppendLine("\r\n"+section.Title+" ("+section.Total+")");foreach(var link in section.Links.Take(12))summary.AppendLine(link.Name+(link.Detail.Length>0?" · "+link.Detail:""));if(section.Total>12)summary.AppendLine("More references are available in Item Finder > Item details & guide.");}summary.AppendLine(guide.Notice);}
   if(!dialog.IsDisposed){text.Text=summary.ToString();status.Text="Available references shown. Missing information is not proof of absence.";}
  }catch(Exception){if(!dialog.IsDisposed)status.Text="Some references could not load. Saved information is kept.";}finally{reader.Dispose();cancellation.Dispose();}};
  dialog.FormClosed+=(_,_)=>{try{cancellation.Cancel();}catch(ObjectDisposedException){}};dialog.Show(this);
 }
}
