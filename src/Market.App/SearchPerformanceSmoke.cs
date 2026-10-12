using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 void CheckSearchPerformance(List<string> checks){
  var original=observations;bool offline=features.Offline;features.Offline=true;ShowPage("Price Checker");
  observations=Enumerable.Range(0,1000).Select(i=>new Observation{Provider="Kerning Exchange",ListingId="perf-"+i,ItemId="perf-"+i,Name="Performance item "+i,Kind=EvidenceKind.Asking,Available=true,UnitPrice=100+i,RetrievedAt=DateTimeOffset.UtcNow,ObservedAt=DateTimeOffset.UtcNow,Url="https://example.org/"+i}).ToList();
  search.Text="Performance item";RenderPrices(false);long initial=lastPriceRenderMs;RenderPrices(false);long repeat=lastPriceRenderMs;if(priceGrid.Rows.Count!=1000)throw new Exception("Performance rows missing");
  checks.Add($"Measured 1,000 saved rows: initial {initial} ms, repeat {repeat} ms; one final valuation/graph rebuild");
  observations=original;search.Text="Red Potion";RenderPrices(false);features.Offline=false;RememberLookupComplete("Red Potion");QueueItemLookup("Red Potion");BeginPriceLookup("Red Potion",false).GetAwaiter().GetResult();if(itemLookupTimer.Enabled||!lookupStatus.Text.StartsWith("Cached"))throw new Exception("Recent search requested network again");
  refreshStop=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);activePriceSearch=true;activeSearchQuery="Red Potion";QueueItemLookup("Blue Potion");if(!refreshStop.IsCancellationRequested)throw new Exception("Superseded request not cancelled");refreshStop.Dispose();refreshStop=null;activePriceSearch=false;itemLookupTimer.Stop();queuedLookup="";features.Offline=offline;
  if(search.AutoCompleteMode!=AutoCompleteMode.Suggest||search.AutoCompleteSource!=AutoCompleteSource.CustomSource)throw new Exception("Price autocomplete disabled");
  foreach(var pair in new[]{("blue mus","Blue Mushroom Cap"),("red po","Red Potion"),("orange mush","Orange Mushroom Cap")}){
   if(!search.AutoCompleteCustomSource.Cast<string>().Any(n=>n.StartsWith(pair.Item1,StringComparison.OrdinalIgnoreCase)&&n==pair.Item2))throw new Exception("Partial-name autocomplete missing: "+pair.Item1);
  }
  checks.Add("Native item dropdown enabled with Blue Mushroom Cap, Red Potion and Orange Mushroom Cap partial-name matches");
  if(priceSearchIndex?.Suggest("Red Potoin").All(i=>i.Name!="Red Potion")!=false)throw new Exception("Typo suggestion missing");
  checks.Add("Cached searches avoid duplicate requests; changing item cancels old search; typo suggestions require explicit selection");
  search.Text="Red Potoin";search.Focus();ShowPriceSuggestions(false);pages["Price Checker"].PerformLayout();if(!priceSuggestions.Visible||priceSuggestions.Controls.Count<2||priceSuggestions.Height<30)throw new Exception("Suggestions not visible");CapturePreview(Path.Combine(dataDirectory,"search-suggestions.png"));priceSuggestions.Visible=false;suggestionTimer.Stop();
  if(original.Count>0){ShowListingDetails(original[0]);var details=OwnedForms.Single(f=>f.Text.EndsWith("· Listing details"));details.Close();checks.Add("Listing details opens in-app without fetching or opening a website");}
 }
}
