using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 bool renderingPriceRows,activePriceSearch;string activeSearchQuery="";int priceRefreshGeneration;long lastPriceRenderMs;
 readonly Dictionary<string,DateTimeOffset> completedLookups=new(StringComparer.OrdinalIgnoreCase);
 void RememberLookupComplete(string query){if(completedLookups.Count>=100)completedLookups.Remove(completedLookups.MinBy(p=>p.Value).Key);completedLookups[ResolvePriceQuery(query)]=DateTimeOffset.UtcNow;}
 void ApplySourceResult(SourceResult result){
  if(!result.Updated)return;
  if(result.Provider=="Approved JSON feed")store.ExpireMissingFromSnapshot(result.Provider,result.Rows);
  if(result.SnapshotItems.Count>0)store.ExpireMissingFromSnapshot(result.Provider,result.Rows,result.SnapshotItems);
  if(result.Rows.Count>0)store.Save(result.Rows.Where(r=>!(r.Provider=="BasilMarket"&&r.PriceApproximate&&observations.Any(old=>old.Key==r.Key&&!old.PriceApproximate))).ToList());
  if(result.FmSummaries is not null)store.ReplaceSummaries(result.FmSummaries);
 }
 void ShowListingDetails(Observation listing){
  var dialog=new Form{Text=listing.Name+" · Listing details",Size=new Size(600,490),MinimumSize=new Size(460,360),StartPosition=FormStartPosition.CenterParent,BackColor=GuidePalette.Surface,ForeColor=GuidePalette.Text};
  var content=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=GuidePalette.Surface,ForeColor=GuidePalette.Text,Font=new Font("Segoe UI",11),Text=ListingDetails(listing)+Environment.NewLine+"Posted: "+(listing.ObservedAt?.ToLocalTime().ToString("g")??"Unknown")+Environment.NewLine+listing.Verification};
  var actions=Toolbar();actions.Dock=DockStyle.Bottom;actions.Controls.Add(Button("Copy details",()=>{try{Clipboard.SetText(content.Text);}catch{}}));actions.Controls.Add(Button("Website fallback",()=>Open(listing.Url)));actions.Controls.Add(Button("Close",dialog.Close));dialog.Padding=new Padding(16);dialog.Controls.Add(content);dialog.Controls.Add(actions);dialog.Show(this);
 }
}
