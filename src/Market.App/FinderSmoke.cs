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
  PresentationChecks(checks);return checks;
 }
}
