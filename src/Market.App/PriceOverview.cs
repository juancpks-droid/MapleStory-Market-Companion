namespace Market.App;
public sealed partial class MainForm
{
 readonly MarketMetric lowestPriceMetric=new(){Caption="LOWEST PLAYER ASK"},wantedPriceMetric=new(){Caption="BEST BUY OFFER"},estimatePriceMetric=new(){Caption="ESTIMATED VALUE"};
 void BuildPriceOverview(Panel page){
  var strip=new TableLayoutPanel(){Dock=DockStyle.Top,Height=114,ColumnCount=3,RowCount=1};
  foreach(var metric in new[]{lowestPriceMetric,wantedPriceMetric,estimatePriceMetric}){strip.ColumnStyles.Add(new(SizeType.Percent,100f/3));strip.Controls.Add(metric);}
  int index=page.Controls.GetChildIndex(priceSummary);page.Controls.Add(strip);page.Controls.SetChildIndex(strip,index);
  priceSummary.Visible=false;marketHeadline.Visible=false;
 }
 void UpdatePriceOverview(decimal? ask,decimal? buy,Market.Core.Valuation? estimate){
  void Set(MarketMetric card,decimal? value,string detail){card.Value=value.HasValue?Market.Core.TextKey.Mesos(value):"—";card.Detail=detail;card.AccessibleName=card.Caption+": "+card.Value+". "+detail;card.Invalidate();}
  Set(lowestPriceMetric,ask,"Mesos / unit • recently checked");Set(wantedPriceMetric,buy,"Mesos / unit • player request");
  Set(estimatePriceMetric,estimate?.Value,estimate is null?"More comparable data needed":$"Confidence {estimate.Confidence}/100 • {estimate.Count} records");
 }
}
