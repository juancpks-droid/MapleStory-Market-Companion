using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 internal void CheckNpcReference(List<string> checks){
  ShowPage("Price Checker");search.Text="Snail Shell";RenderPrices();
  if(priceGrid.Rows.Count!=0)throw new Exception("Expected no market listings for NPC fixture");
  ShowNpcPrices(new("Snail Shell","https://ccplz.cc/items/snail-shell",1,null,null,DateTimeOffset.UtcNow));
  if(npcSellMetric.Value!="1 mesos"||npcBuyMetric.Tag is true)throw new Exception("NPC reference labels mixed");
  checks.Add("NPC reference remains separate when no player listings exist; missing shop price stays unavailable");
  UpdatePriceOverview(null,null,null);UpdateFmPriceMetrics(null,true);
  if(lowestPriceMetric.Visible||wantedPriceMetric.Visible||estimatePriceMetric.Visible||!npcSellMetric.Visible)throw new Exception("Empty market cards were not hidden");
  var fm=new FmSummary("1","Snail Shell",false,53,20,null,16,0,1,DateTimeOffset.UtcNow.AddDays(-10),DateTimeOffset.UtcNow,"https://kerning.gg/fm-tracker");
  UpdateFmPriceMetrics(fm,true);
  if(!fmMedianMetric.Visible||!fmAskMetric.Visible||!fmMedianMetric.Detail.Contains("Stale data")||fmMedianMetric.Value!="53 mesos"||fmAskMetric.Value!="20 mesos")throw new Exception("FM reference cards or stale label missing");
  CapturePreview(Path.Combine(dataDirectory,"reference-price-cards.png"));
  UpdatePriceOverview(100,null,null);if(!lowestPriceMetric.Visible||wantedPriceMetric.Visible||estimatePriceMetric.Visible)throw new Exception("Partially available market cards incorrect");
  UpdatePriceOverview(null,null,null);UpdateFmPriceMetrics(null,true);UpdateNpcPriceMetrics(null);if(priceMetricStrip.Visible)throw new Exception("Old reference cards remained after clearing");
  checks.Add("Empty and partial market cards, NPC-only layout, FM median/ask stale labels and clearing references passed");
  priceDetails.SelectedIndex=1;RenderHistory();
  ShowNpcPrices(new("Snail Shell","https://ccplz.cc/items/snail-shell",1,null,null,DateTimeOffset.UtcNow));
  if(history.Points.Count!=0||!history.Visible)throw new Exception("NPC reference polluted market chart or chart hidden");
  CapturePreview(Path.Combine(dataDirectory,"npc-empty-history.png"));checks.Add("Empty market graph stays visible and NPC price never becomes a history point");
  priceDetails.SelectedIndex=0;
  fmContext.Visible=true;fmContext.Text="Kerning FM • 7-day median 30 • Lowest observed ask 20 • 9 sightings\nLast seen 10/9/2026 10:14 PM • Cached data • Summary only; excluded from bargain alerts.";
  foreach(var size in new[]{9f,13f}){fmContext.Font=new Font("Segoe UI",size);PerformLayout();if(fmContext.Height<fmContext.GetPreferredSize(new Size(fmContext.Width,0)).Height)throw new Exception("FM summary text clipped");CapturePreview(Path.Combine(dataDirectory,"price-text-"+size+".png"));}
  checks.Add("Multiline FM summary fits at minimum window width and normal/large text sizes");
 }
}
