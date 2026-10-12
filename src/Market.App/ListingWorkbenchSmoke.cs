using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 void CheckListingWorkbench(List<string> checks){
  void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
  var original=observations.ToList();var first=observations.First(o=>o.Name=="Red Potion"&&o.Available&&o.Kind==EvidenceKind.Asking);
  observations.Add(first with{ListingId="approx",UnitPrice=900,PriceApproximate=true,Quantity=null,Seller="=unsafe",Note="Quoted \"note\", CH 9\nFM 2"});
  ShowPage("Price Checker");search.Text="Red Potion";priceDetails.SelectedIndex=3;RenderPrices(false);
  Check(listingTable.Rows.Count==5,"Listing tools separate available offers from closed listings");
  listingText.Text="ch 9";Check(listingTable.Rows.Count==1,"Player and location search is case insensitive");
  listingText.Clear();listingQuantity.Value=26;Check(listingTable.Rows.Count==0,"Quantity filter excludes insufficient and unknown quantities");listingQuantity.Value=25;Check(listingTable.Rows.Count==4,"Quantity filter includes matching known quantities");listingQuantity.Value=0;
  listingExact.Checked=true;Check(listingTable.Rows.Count==4,"Exact-price filter excludes rounded prices");listingSide.SelectedIndex=1;Check(listingTable.Rows.Count==2,"For-sale filter excludes buy offers");
  Check(StackCost(first)==15000&&StackCost(first with{Quantity=null}) is null&&StackCost(first with{Quantity=long.MaxValue,UnitPrice=decimal.MaxValue}) is null,"Full-stack cost handles known, missing and overflowing quantities");
  listingTable.ClearSelection();listingTable.Rows[0].Selected=true;listingTable.Rows[1].Selected=true;UpdateListingComparison();Check(listingComparison.Text.Contains("100 mesos / unit difference"),"Two sale listings compare their per-unit price difference");
  listingSide.SelectedIndex=0;listingTable.ClearSelection();listingTable.Rows[0].Selected=true;listingTable.Rows[2].Selected=true;UpdateListingComparison();Check(listingComparison.Text.Contains("same item, variant and side"),"Comparison refuses mixed buy and sell offers");
  var csv=ListingCsv(observations.Where(o=>o.ListingId=="approx"));Check(csv.Contains("\"'=unsafe\"")&&csv.Contains("\"\"note\"\"")&&csv.Contains("Approximate"),"CSV preserves quoted notes and approximation labels and neutralizes formula text");
  listingTable.Sort(listingTable.Columns[1],System.ComponentModel.ListSortDirection.Descending);Check((decimal)listingTable.Rows[0].Cells[1].Value==700,"Listing table sorts prices numerically");
  foreach(var theme in MerchantTools.Themes){themeChoice.SelectedItem=theme;Size=new Size(1024,720);PerformLayout();Check(listingTable.Height>80&&listingTable.Width>500,"Listing tools fit compact window in "+theme);CapturePreview(Path.Combine(dataDirectory,"listing-tools-"+theme.Replace(" ","-")+".png"));}
  listingText.Clear();listingQuantity.Value=0;listingExact.Checked=false;listingSide.SelectedIndex=0;observations=original;priceDetails.SelectedIndex=0;RenderPrices(false);
 }
}
