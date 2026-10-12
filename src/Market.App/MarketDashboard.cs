using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly DataGridView hotMarket=Grid(),wantedMarket=Grid();
 readonly Label marketDashboardNote=Info(85);
 readonly MarketMetric saleMetric=new(){Caption="FOR SALE",Detail="Recent available listings"},buyMetric=new(){Caption="WANTED",Detail="Recent buy requests"},watchMetric=new(){Caption="WATCHLIST",Detail="Items you are following"},sourceMetric=new(){Caption="SOURCES",Detail="Enabled connections"};
 void BuildMarketDashboard(){
  var page=pages["Dashboard"];page.Controls.Clear();
  var bar=Toolbar();bar.Height=45;bar.Controls.Add(Button("Find an item",()=>ShowPage("Price Checker")));bar.Controls.Add(Button("Refresh market",async()=>await RefreshSources()));bar.Controls.Add(Button("Watchlist",()=>ShowPage("Watchlist")));bar.Controls.Add(Button("Alert Inbox",()=>ShowPage("Alert Inbox")));
  var columns=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2};columns.ColumnStyles.Add(new(SizeType.Percent,50));columns.ColumnStyles.Add(new(SizeType.Percent,50));
  Panel Section(string title,DataGridView grid){var panel=new Panel(){Dock=DockStyle.Fill,Padding=new Padding(4)};var label=Info(36);label.Text=title;panel.Controls.Add(grid);panel.Controls.Add(label);grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&grid.Rows[e.RowIndex].Tag is string name){search.Text=name;ShowPage("Price Checker");}};return panel;}
  columns.Controls.Add(Section("HOT LISTINGS • Most listed for sale",hotMarket));columns.Controls.Add(Section("WANTED • Most buy requests",wantedMarket));
  var metrics=new TableLayoutPanel(){Dock=DockStyle.Top,Height=116,ColumnCount=4,RowCount=1};
  foreach(var metric in new[]{saleMetric,buyMetric,watchMetric,sourceMetric}){metrics.ColumnStyles.Add(new(SizeType.Percent,25));metrics.Controls.Add(metric);}
  marketDashboardNote.Height=64;
  page.Controls.Add(columns);page.Controls.Add(marketDashboardNote);page.Controls.Add(metrics);page.Controls.Add(bar);RenderMarketDashboard();
 }
 void RenderMarketDashboard(){
  var now=DateTimeOffset.UtcNow;
  var rows=observations.Where(o=>o.Available&&o.Fresh(now,TimeSpan.FromHours(settings.FreshHours))&&settings.Sources.Any(s=>s.Id==o.Provider&&s.Authorized)).DistinctBy(o=>o.Key).ToList();
  void Fill(DataGridView grid,EvidenceKind kind){
   SetColumns(grid,["Item","Listings","People","Latest post"]);
   foreach(var group in rows.Where(o=>o.Kind==kind).GroupBy(o=>o.ItemKey).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).Take(20)){
    int index=grid.Rows.Add(group.First().Name,group.Count(),group.Where(o=>!string.IsNullOrWhiteSpace(o.Seller)).Select(o=>o.Provider+"|"+o.Seller).Distinct().Count(),group.Max(o=>o.ObservedAt)?.ToLocalTime().ToString("g"));grid.Rows[index].Tag=group.First().Name;
   }
  }
  Fill(hotMarket,EvidenceKind.Asking);Fill(wantedMarket,EvidenceKind.Buying);
  saleMetric.Value=rows.Count(o=>o.Kind==EvidenceKind.Asking).ToString("N0");buyMetric.Value=rows.Count(o=>o.Kind==EvidenceKind.Buying).ToString("N0");watchMetric.Value=store.Items().Count(i=>i.Watch).ToString("N0");sourceMetric.Value=settings.Sources.Count(s=>s.Authorized).ToString();foreach(var metric in new[]{saleMetric,buyMetric,watchMetric,sourceMetric})metric.Invalidate();
  marketDashboardNote.Text=$"{rows.Count(o=>o.Kind==EvidenceKind.Asking):N0} recent sell listings • {rows.Count(o=>o.Kind==EvidenceKind.Buying):N0} recent buy requests from enabled sources.\nRanked by cached listing counts, not completed sales or guaranteed availability. Double-click an item to open Price Checker."+(rows.Count==0?"\nNo qualifying listings yet. Refresh sources or check source settings.":"");
 }
}
