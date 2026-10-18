using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly ClassicMarketClient classicMarketClient=new();
 readonly MarketMetric classicMedianMetric=new(){Caption="SOURCE-REPORTED MEDIAN"};
 readonly ComboBox priceHistorySource=new GuideComboBox(){Width=220,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Price history source"};
 ClassicPriceSummary? classicPrice;string classicPriceKey="";
 void BuildHistorySource(FlowLayoutPanel bar){
  priceHistorySource.Items.AddRange(["MapleStory-Classic daily medians","Kerning individual observations"]);priceHistorySource.SelectedIndex=0;
  priceHistorySource.SelectedIndexChanged+=(_,_)=>RenderHistory();bar.Controls.Add(priceHistorySource);
 }
 async void QueueClassicPrices(){
  if(HoldPriceResults||closing)return;
  string name=SelectedPriceItem()?.Name??DisplayedPriceQuery,key=TextKey.Search(name)+"|"+Offline;
  if(key==classicPriceKey)return;classicPriceKey=key;classicPrice=null;SetPriceMetric(classicMedianMetric,null,"");LayoutPriceMetrics();
  try{
   var result=await classicMarketClient.Lookup(dataDirectory,name,Offline,stop.Token);
   if(closing||IsDisposed||key!=classicPriceKey||HoldPriceResults)return;
   classicPrice=result;
   SetPriceMetric(classicMedianMetric,result?.Median,result is null?"":$"{ClassicPriceSummary.Source}\nSnapshot {result.UpdatedAt.ToLocalTime():g} · {(result.Stale?"older data":"recent snapshot")}\n{result.Sales:N0} source-reported sales{(result.Sales<5?" · low sample":"")} · variants may differ");
   LayoutPriceMetrics();RenderHistory();
  }catch(OperationCanceledException){if(!closing)classicPriceKey="";}
 }
 List<Observation> ClassicHistory(int days){
  if(classicPrice is null)return [];
  return classicPrice.History.Where(d=>d.Date>=DateTimeOffset.UtcNow.AddDays(-days)).Select(d=>new Observation{
   Provider=ClassicPriceSummary.Source,Name=classicPrice.Name,ListingId=classicPrice.Slug+"/"+d.Date.ToString("yyyy-MM-dd"),
   UnitPrice=d.Median,Kind=EvidenceKind.CommunityEstimate,ObservedAt=d.Date,RetrievedAt=classicPrice.UpdatedAt,
   Note=$"Daily median · {d.Sales} source-reported sales; not an individual sale",Url="https://maplestory-classic.com/market",Available=false
  }).ToList();
 }
}
