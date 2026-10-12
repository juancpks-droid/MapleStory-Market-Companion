using Market.Core;
using System.Text.Json;
namespace Market.App;
static class FinderSmoke
{
 public static void Run(){
  string directory=Path.Combine(AppContext.BaseDirectory,"finder-smoke-data");Directory.CreateDirectory(directory);
  new CompanionFeatures(){SetupComplete=true,Offline=true}.Save(Path.Combine(directory,"companion-features.json"));
  using var store=new Store(Path.Combine(directory,"market.db"));var settings=store.LoadSettings();settings.AccessVersion=5;settings.CloseToTray=false;foreach(var source in settings.Sources)source.Enabled=false;store.SaveSettings(settings);
  using var form=new MainForm(store,directory);
  form.Shown+=async(_,_)=>{try{await Task.Delay(200);var checks=form.RunFinderChecks();form.CapturePreview(Path.Combine(directory,"item-finder.png"));File.WriteAllText(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(new{status="passed",checks},Json.Options));}catch(Exception e){Environment.ExitCode=1;File.WriteAllText(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(new{status="failed",error=e.ToString()},Json.Options));}finally{form.ExitApplication();}};
  Application.Run(form);
 }
}
public sealed partial class MainForm
{
 internal List<string> RunFinderChecks(){
  var checks=new List<string>();void Check(bool ok,string note){if(!ok)throw new Exception(note);checks.Add(note);}
  observations=[new(){Provider="BasilMarket",ListingId="finder-test",ItemId="2000000",Name="Red Potion",Kind=EvidenceKind.Asking,Available=true,UnitPrice=51,Quantity=73,Seller="Test seller",RetrievedAt=DateTimeOffset.UtcNow,PriceApproximate=true,Url="https://example.org/test"}];
  CheckSearchPerformance(checks);
  ShowPage("Item Finder");finderSearch.Text="Red Potion";RenderItemFinder();
  Check(navigation["Item Finder"].Visible,"Item Finder is available in the simple Market menu");
  Check(finderItems.Rows.Count>0,"Catalog search returns matching items");
  var row=finderItems.Rows.Cast<DataGridViewRow>().First(r=>r.Tag is FinderResult item&&item.Id=="2000000");finderItems.ClearSelection();row.Selected=true;RenderFinderListings();
  Check(finderListings.Rows.Count==1&&finderListings.Rows[0].Cells[0].Value.ToString()!.StartsWith("~"),"Rounded listing price stays labelled");
  Check(finderListings.Rows[0].Cells[1].Value.ToString()=="73","Listing quantity remains exact");
  WatchObject(FinderActionItem!);Check(store.Items().Any(i=>i.Name=="Red Potion"&&i.Watch&&i.Quantity==0),"Finder watch action creates a watch without inventing owned stock");
  finderListed.Checked=true;finderSource.SelectedItem="Kerning Exchange";RenderItemFinder();Check(finderItems.Rows.Count==0,"Provider and listing-only filters work together");
  finderListed.Checked=false;RenderItemFinder();Check(finderItems.Rows.Count>0&&finderListings.Rows.Count==0,"Catalog item remains findable when selected provider has no listings");
  finderSearch.Text="NoSuchItemUniqueFinderTest";RenderItemFinder();Check(finderItems.Rows.Count==0&&finderListings.Rows.Count==0,"Empty search clears previous item and listing selections");
  finderSource.SelectedItem="All";finderSearch.Text="Red Potion";RenderItemFinder();
  Check(finderItems.Width>300&&finderListings.Height>60,"Both results panels are visible at the default window size");
  Check(organizedNav!.Controls.Contains(navigation["Settings"])&&!organizedNav.Controls.Contains(navigation["Backups"]),"Settings is direct; Backups is absent from sidebar");
  Check(!organizedNav.Controls.OfType<Button>().Any(b=>b.Text is "+ Settings" or "− Settings"),"No duplicate expandable Settings menu");
  navigation["Settings"].PerformClick();Check(selected=="Settings","Direct Settings entry opens Settings");
  ShowPage("Backups");Check(selected=="Settings"&&settingsSections.SelectedIndex==1&&settingsSections.TabPages[1].Controls.Count>0,"Backup shortcuts open backup tools inside Settings");CapturePreview(Path.Combine(dataDirectory,"Backups-settings.png"));settingsSections.SelectedIndex=0;Check(themeChoice.Visible&&themeChoice.Parent!.Bottom<=settingsSections.Height,"Theme controls remain visible in Settings");
  ShowPage("Item Finder");
  ShowPage("Price Checker");search.Text="Potion";RenderPrices();priceGrid.ClearSelection();priceGrid.Rows.Cast<DataGridViewRow>().Single(r=>r.Tag is Observation o&&o.Name=="Red Potion").Selected=true;
  var fmButton=pages["Price Checker"].Controls.OfType<FlowLayoutPanel>().SelectMany(p=>p.Controls.OfType<Button>()).Single(b=>b.Text=="View in FM Tracker");
  fmFilter.SelectedIndex=2;fmButton.PerformClick();
  Check(selected=="FM Tracker"&&fmSearch.Text=="Red Potion"&&fmFilter.SelectedIndex==0,"FM button carries selected item instead of broad query and clears restrictive filter");
  Check(PageGroup("FM Tracker")=="Market"&&essentialPages.Contains("FM Tracker")&&navigation["FM Tracker"].Visible,"FM Tracker stays visible under Market in simple menu");
  ShowPage("Price Checker");search.Text="NoSuchItemUnique";RenderPrices();fmButton.PerformClick();
  Check(selected=="FM Tracker"&&fmSearch.Text=="NoSuchItemUnique","FM button falls back to search text when no listing is selected");
  ShowPage("Price Checker");search.Clear();priceGrid.ClearSelection();fmButton.PerformClick();
  Check(selected=="Price Checker","Empty selection stays on Price Checker with guidance");
  Size=new Size(1024,720);PerformLayout();Check(fmButton.Parent!.ClientRectangle.Contains(fmButton.Bounds),"FM shortcut fits at minimum window size");CapturePreview(Path.Combine(dataDirectory,"price-fm-button.png"));
  ShowPage("Item Finder");
  var originalTheme=features.Theme;var seen=new HashSet<int>();
  foreach(var theme in MerchantTools.Themes){themeChoice.SelectedItem=theme;Check(seen.Add(GuidePalette.Gold.ToArgb()),theme+" has a distinct accent");Check(finderDrops.ColumnHeadersDefaultCellStyle.ForeColor==GuidePalette.Gold,theme+" applies to item reference tables");CapturePreview(Path.Combine(dataDirectory,"theme-"+theme.Replace(" ","-")+".png"));}
  themeChoice.SelectedItem=originalTheme;
  PresentationChecks(checks);
  Size=new Size(1024,720);
  foreach(var page in new[]{"Dashboard","Item Finder","Settings"}){
   ShowPage(page);PerformLayout();CapturePreview(Path.Combine(dataDirectory,page+"-guide.png"));
   Check(sidebarUpdateButton.Visible&&sidebarVersion.Visible&&sidebarUpdateButton.Parent!.ClientRectangle.Contains(sidebarUpdateButton.Bounds),"Sidebar update controls visible on "+page+" at minimum window size");
  }
  ShowPage("Item Finder");sidebarUpdateButton.PerformClick();
  Check(selected=="Item Finder"&&sidebarUpdateStatus.Text.Contains("Offline mode"),"Sidebar button checks updates without changing tabs");
  Check(sidebarVersion.Text=="Installed: v"+Application.ProductVersion.Split('+')[0],"Sidebar shows installed version");
  updateStatus.Text="Update v99.0.0 available. Choose Check for updates when ready.";
  Check(sidebarUpdateStatus.Text.Contains("v99.0.0"),"Available update version appears in sidebar");
  sidebarUpdateButton.PerformClick();
  CheckMarketDetails(checks);CheckWatchToggles(checks);CheckNpcReference(checks);CheckWatchlistHub(checks);CheckRelatedDropChoices(checks);CheckDesign(checks);return checks;
 }
}
