using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 internal void CheckMarketDetails(List<string> checks){
  void Check(bool ok,string note){if(!ok)throw new Exception(note);checks.Add(note);}
  Observation Row(string id,string name,decimal price,EvidenceKind kind,bool available=true)=>new(){Provider="Kerning Exchange",ListingId=id,ItemId=name=="Red Potion"?"2000000":"2000001",Name=name,UnitPrice=price,Kind=kind,Available=available,Quantity=25,Seller="Test merchant",Note="CH 2, FM room 3 — player note",ObservedAt=DateTimeOffset.UtcNow,RetrievedAt=DateTimeOffset.UtcNow,Url="https://example.org/listing/"+id};
  observations=[Row("a","Red Potion",600,EvidenceKind.Asking),Row("b","Red Potion",700,EvidenceKind.Asking),Row("c","Red Potion",400,EvidenceKind.Buying),Row("d","Red Potion",500,EvidenceKind.Buying),Row("e","Red Potion",100,EvidenceKind.Asking,false),Row("f","Blue Potion",5,EvidenceKind.Asking)];
  int Count(FlowLayoutPanel panel)=>panel.Controls.Cast<Control>().Count(c=>c.Tag is Observation);
  ShowPage("Price Checker");priceDetails.SelectedIndex=0;search.Text="Red Potion";RenderPrices(false);
  Check(Count(sellCards)==2&&Count(buyCards)==2,"Separate sale/buy cards exclude closed listings");CheckListingWorkbench(checks);
  listingSource.SelectedItem="BasilMarket";Check(Count(sellCards)==0&&Count(buyCards)==0,"Source filter excludes other providers");
  ResetMarketFilters();listingRecent.Checked=true;Check(Count(sellCards)==0,"Recent-only filter excludes disabled sources");
  ResetMarketFilters();listingCompact.Checked=true;Check(Count(sellCards)==2&&!sellCards.Controls[0].Controls.OfType<Label>().Any(l=>l.Text.Contains("player note")),"Compact cards keep prices while collapsing long notes");
  Check(ListingDetails(observations[0]).Contains("CH 2, FM room 3")&&ListingDetails(observations[0]).Contains("https://example.org/listing/a"),"Copy details contains player location and original listing link");listingCompact.Checked=false;
  ShowPage("Item Finder");ShowPage("Price Checker");NavigateHistory(true);Check(selected=="Item Finder","Back returns to previous page");NavigateHistory(false);Check(selected=="Price Checker","Forward returns to next page");
  for(int i=0;i<15;i++)RememberPriceSearch("Test item "+i);RememberPriceSearch("test ITEM 14");Check(features.RecentItems.Count==12&&features.RecentItems[0]=="test ITEM 14","Recent searches are capped and deduplicated without case sensitivity");
  Check(CompanionFeatures.Load(Path.Combine(dataDirectory,"companion-features.json")).RecentItems.SequenceEqual(features.RecentItems),"Recent searches persist to disk");
  listingMin.Value=450;listingMax.Value=650;Check(Count(sellCards)==1&&Count(buyCards)==1,"Listing price range filters both sides");
  listingMin.Value=0;listingMax.Value=0;listingSort.SelectedIndex=2;
  Check(((Observation)sellCards.Controls[0].Tag!).UnitPrice==700,"Listing prices sort high to low");
  foreach(var query in new[]{"red potion"," RED POTION ","Red Po","2000000"}){search.Text=query;RenderPrices(false);Check(priceGrid.Rows.Count==5&&SelectedPriceItem()?.Name=="Red Potion","Price search matches "+query);}
  search.Text="Potion";RenderPrices(false);priceItemChoice.SelectedItem="Blue Potion";
  Check(marketTarget?.Name=="Blue Potion"&&Count(sellCards)==1&&Count(buyCards)==0,"Matching-item selector synchronizes summary and cards");
  OpenPriceInFm();Check(fmSearch.Text=="Blue Potion","FM shortcut follows matching-item selection");
  ShowPage("Price Checker");search.Text="NoSuchMarketItem";RenderPrices(false);Check(Count(sellCards)==0&&Count(buyCards)==0&&marketTarget is null,"Unmatched search clears stale item details");
  search.Clear();RenderPrices(false);Check(priceGrid.Rows.Count==6,"Clearing price search restores all results");
  search.Text="Red Potion";RenderPrices(false);Size=new Size(1024,720);PerformLayout();CapturePreview(Path.Combine(dataDirectory,"market-listings.png"));
  Check(sellCards.Width>=250&&buyCards.Width>=250&&sellCards.Height>=100,"Two listing columns fit minimum window size");
  foreach(var page in new[]{"Dashboard","Watchlist","Alert Inbox"})Check(organizedNav!.Controls.Contains(navigation[page]),page+" has standalone navigation");
  Check(!organizedNav!.Controls.OfType<Button>().Any(b=>b.Text.Contains("Trading")||b.Text=="Inventory"||b.Text=="Characters"),"Retired navigation is hidden");
  ShowPage("Inventory");Check(selected=="Dashboard","Old inventory route safely opens dashboard");
 }
}
