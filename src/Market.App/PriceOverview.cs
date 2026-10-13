using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly MarketMetric lowestPriceMetric=new(){Caption="LOWEST PLAYER ASK"},wantedPriceMetric=new(){Caption="BEST BUY OFFER"},estimatePriceMetric=new(){Caption="ESTIMATED VALUE"};
 readonly MarketMetric npcSellMetric=new(){Caption="SELL TO NPC"},npcBuyMetric=new(){Caption="BUY FROM NPC"},fmMedianMetric=new(){Caption="KERNING FM · 7-DAY MEDIAN"},fmAskMetric=new(){Caption="KERNING FM · LOWEST ASK"};
 readonly TableLayoutPanel priceMetricStrip=new(){Dock=DockStyle.Top,Visible=false,AutoScroll=true};
 string metricLayout="";
 MarketMetric[] PriceMetrics()=>[lowestPriceMetric,wantedPriceMetric,estimatePriceMetric,fmMedianMetric,fmAskMetric,npcSellMetric,npcBuyMetric];
 void BuildPriceOverview(Panel page){
  foreach(var card in PriceMetrics()){card.Detailed=true;card.Visible=false;}
  int index=page.Controls.GetChildIndex(priceSummary);page.Controls.Add(priceMetricStrip);page.Controls.SetChildIndex(priceMetricStrip,index);
  priceMetricStrip.SizeChanged+=(_,_)=>LayoutPriceMetrics();priceSummary.Visible=false;marketHeadline.Visible=false;
 }
 void SetPriceMetric(MarketMetric card,decimal? value,string detail){
  card.Tag=value.HasValue;card.Value=value.HasValue?TextKey.Mesos(value)+" mesos":"—";card.Detail=detail;card.AccessibleName=card.Caption+": "+card.Value+". "+detail;card.Invalidate();
 }
 void LayoutPriceMetrics(){
  var cards=PriceMetrics().Where(c=>c.Tag is true).ToArray();int columns=Math.Min(cards.Length,Math.Max(1,priceMetricStrip.ClientSize.Width/230));
  string key=columns+":"+string.Join(",",cards.Select(c=>c.Caption));if(key==metricLayout)return;metricLayout=key;
  priceMetricStrip.SuspendLayout();priceMetricStrip.Controls.Clear();priceMetricStrip.ColumnStyles.Clear();priceMetricStrip.RowStyles.Clear();
  foreach(var card in PriceMetrics())card.Visible=false;
  priceMetricStrip.Visible=cards.Length>0;
  if(cards.Length>0){int rows=(cards.Length+columns-1)/columns;priceMetricStrip.ColumnCount=columns;priceMetricStrip.RowCount=rows;priceMetricStrip.Height=Math.Min(rows*180,360);
   for(int i=0;i<columns;i++)priceMetricStrip.ColumnStyles.Add(new(SizeType.Percent,100f/columns));
   for(int i=0;i<rows;i++)priceMetricStrip.RowStyles.Add(new(SizeType.Absolute,180));
   for(int i=0;i<cards.Length;i++){cards[i].Visible=true;priceMetricStrip.Controls.Add(cards[i],i%columns,i/columns);}
  }else priceMetricStrip.Height=0;
  priceMetricStrip.ResumeLayout();
 }
 void UpdatePriceOverview(decimal? ask,decimal? buy,Valuation? estimate){
  SetPriceMetric(lowestPriceMetric,ask,"Per unit · recorded asking price, not a sale");SetPriceMetric(wantedPriceMetric,buy,"Per unit · player request; availability unverified");
  SetPriceMetric(estimatePriceMetric,estimate?.Value,estimate is null?"":$"Confidence {estimate.Confidence}/100 · {estimate.Count} records");LayoutPriceMetrics();
 }
 void UpdateFmPriceMetrics(FmSummary? fm,bool enabled){
  string detail=fm is null?"":$"Last seen {fm.LastSeen.ToLocalTime():g}\n{(enabled?fm.Quality(DateTimeOffset.UtcNow,settings.FreshHours):"Source disabled · cached only")}\n{fm.Sightings} sightings · summary, not a live offer";
  SetPriceMetric(fmMedianMetric,fm?.Median7,detail);SetPriceMetric(fmAskMetric,fm?.LowestAsk,detail);LayoutPriceMetrics();
 }
 void UpdateNpcPriceMetrics(NpcPrices? prices){
  string detail=prices is null?"":$"{prices.Source} · NPC reference\nChecked {prices.CheckedAt.ToLocalTime():g}\nMay differ by server/version";
  SetPriceMetric(npcSellMetric,prices?.Sell,detail);SetPriceMetric(npcBuyMetric,prices?.BuyMin,detail+(prices?.Source=="MapleStory Quest"?" · lowest recorded":prices?.BuyMax>prices?.BuyMin?" · up to "+TextKey.Mesos(prices.BuyMax)+" mesos":""));LayoutPriceMetrics();
 }
}
