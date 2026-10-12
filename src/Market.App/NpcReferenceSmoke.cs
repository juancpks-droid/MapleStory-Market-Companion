using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 internal void CheckNpcReference(List<string> checks){
  ShowPage("Price Checker");search.Text="Snail Shell";RenderPrices();
  if(priceGrid.Rows.Count!=0)throw new Exception("Expected no market listings for NPC fixture");
  ShowNpcPrices(new("Snail Shell","https://ccplz.cc/items/snail-shell",1,null,null,DateTimeOffset.UtcNow));
  if(!npcPriceNote.Text.Contains("Sell to NPC: 1 mesos")||!npcPriceNote.Text.Contains("Buy from NPC: Not available"))throw new Exception("NPC reference labels mixed");
  checks.Add("NPC reference remains separate when no player listings exist; missing shop price stays unavailable");
  priceDetails.SelectedIndex=1;RenderHistory();
  ShowNpcPrices(new("Snail Shell","https://ccplz.cc/items/snail-shell",1,null,null,DateTimeOffset.UtcNow));
  if(history.Points.Count!=0||!history.Visible)throw new Exception("NPC reference polluted market chart or chart hidden");
  CapturePreview(Path.Combine(dataDirectory,"npc-empty-history.png"));checks.Add("Empty market graph stays visible and NPC price never becomes a history point");
  priceDetails.SelectedIndex=0;
  fmContext.Text="Kerning FM • 7-day median 30 • Lowest observed ask 20 • 9 sightings\nLast seen 10/9/2026 10:14 PM • Cached data • Summary only; excluded from bargain alerts.";
  foreach(var size in new[]{9f,13f}){fmContext.Font=new Font("Segoe UI",size);PerformLayout();if(fmContext.Height<fmContext.GetPreferredSize(new Size(fmContext.Width,0)).Height)throw new Exception("FM summary text clipped");CapturePreview(Path.Combine(dataDirectory,"price-text-"+size+".png"));}
  checks.Add("Multiline FM summary fits at minimum window width and normal/large text sizes");
 }
}
