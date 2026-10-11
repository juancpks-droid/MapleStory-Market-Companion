using Market.Core;

namespace Market.App;
public sealed partial class MainForm
{
    readonly DataGridView dashboardWatch=Grid(),dashboardDeals=Grid(),dashboardMarket=Grid();
    readonly Label marketCard=Info(80),dealCard=Info(80),watchCard=Info(80),inventoryCard=Info(80);
    readonly Label dashboardStatus=Info(130),watchEmpty=Info(80),dealsEmpty=Info(80),marketEmpty=Info(80);
    readonly TextBox quickSearch=new(){Width=290,PlaceholderText="Check an item price",BackColor=Surface,ForeColor=TextColor};

    void BuildDashboard()
    {
        var page=pages["Dashboard"];
        var layout=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=Padding.Empty,Padding=Padding.Empty};
        layout.RowStyles.Add(new(SizeType.Absolute,50));layout.RowStyles.Add(new(SizeType.Absolute,94));layout.RowStyles.Add(new(SizeType.Absolute,130));layout.RowStyles.Add(new(SizeType.Percent,50));layout.RowStyles.Add(new(SizeType.Percent,50));
        var bar=Toolbar();quickSearch.Margin=new Padding(3,9,8,3);bar.Controls.Add(quickSearch);
        async void Search(){search.Text=quickSearch.Text;ShowPage("Price Checker");await RefreshSources(true);}
        bar.Controls.Add(Button("Check price",Search));bar.Controls.Add(Button("Refresh",async()=>await RefreshSources()));bar.Controls.Add(Button("Customize cards",EditDashboardCards));bar.Controls.Add(Button("Search all",()=>ShowPage("Search Everything")));
        quickSearch.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Search();}};layout.Controls.Add(bar,0,0);
        var cards=dashboardCards;
        foreach(var (label,target) in new[]{(marketCard,"FM Tracker"),(dealCard,"Deal Finder"),(watchCard,"Watchlist"),(inventoryCard,"Inventory")})
        {
            cards.ColumnStyles.Add(new(SizeType.Percent,25));label.Dock=DockStyle.Fill;label.BackColor=Surface;label.ForeColor=TextColor;label.BorderStyle=BorderStyle.FixedSingle;label.Padding=new Padding(12,10,5,5);label.Margin=new Padding(3,4,8,4);label.Cursor=Cursors.Hand;label.Click+=(_,_)=>ShowPage(target);cards.Controls.Add(label);
        }
        layout.Controls.Add(cards,0,1);dashboardStatus.Dock=DockStyle.Fill;layout.Controls.Add(dashboardStatus,0,2);
        Panel Section(string heading,string action,Action open,DataGridView grid,Label empty)
        {
            var section=new Panel(){Dock=DockStyle.Fill,Margin=new Padding(3,4,8,8)};
            var top=new ClassicTitle(){Dock=DockStyle.Top,Height=36};var titleLabel=new Label(){Text=heading,Dock=DockStyle.Fill,Font=new Font("Tahoma",9,FontStyle.Bold),ForeColor=Color.White,BackColor=Color.Transparent,Padding=new Padding(10,10,0,0)};var button=Button(action,open);button.Dock=DockStyle.Right;top.Controls.Add(titleLabel);top.Controls.Add(button);
            var body=new Panel(){Dock=DockStyle.Fill};empty.Dock=DockStyle.Fill;empty.BackColor=Surface;empty.Padding=new Padding(14,18,10,10);body.Controls.Add(grid);body.Controls.Add(empty);section.BorderStyle=BorderStyle.FixedSingle;section.Controls.Add(body);section.Controls.Add(top);return section;
        }
        var upper=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};upper.ColumnStyles.Add(new(SizeType.Percent,50));upper.ColumnStyles.Add(new(SizeType.Percent,50));
        upper.Controls.Add(Section("Your watchlist","Manage",()=>ShowPage("Watchlist"),dashboardWatch,watchEmpty),0,0);
        upper.Controls.Add(Section("Potential deals","View all",()=>ShowPage("Deal Finder"),dashboardDeals,dealsEmpty),1,0);layout.Controls.Add(upper,0,3);
        layout.Controls.Add(Section("Latest FM sightings","Browse FM",()=>ShowPage("FM Tracker"),dashboardMarket,marketEmpty),0,4);
        dashboardWatch.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&dashboardWatch.Rows[e.RowIndex].Tag is SavedItem item){ShowItemDetails(item);}};
        dashboardDeals.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&dashboardDeals.Rows[e.RowIndex].Tag is Deal deal)Open(deal.Listing.Url);};
        dashboardMarket.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&dashboardMarket.Rows[e.RowIndex].Tag is FmSummary item){ShowItemDetails(item);}};
        page.Controls.Add(layout);
    }
    void RenderDashboard(bool tables=true)
    {
        var now=DateTimeOffset.UtcNow;var enabled=settings.Sources.Where(s=>s.Authorized).ToList();var kerning=enabled.FirstOrDefault(s=>s.Id=="Kerning Exchange");
        var last=enabled.Select(s=>s.LastChecked).DefaultIfEmpty().Max();var next=enabled.Select(s=>s.BlockedUntil>s.NextAllowed?s.BlockedUntil:s.NextAllowed).DefaultIfEmpty().Min();
        var failedSources=sourceResults.Where(r=>enabled.Any(s=>s.Id==r.Provider)&&r.Status.StartsWith("Unavailable")).Select(r=>r.Provider).ToList();
        dashboardStatus.Text=busy?"Updating market data… You can keep using the app.":enabled.Count==0?"No automatic source enabled. Open Sources & settings to enable Kerning Exchange.":$"Last check: {(last==default?"not yet":last.ToLocalTime().ToString("t"))}   •   Next fetch: {(next<=now?"due now":next.ToLocalTime().ToString("t"))}   •   Local checks: {settings.LocalSeconds}s";
        if(!busy&&failedSources.Count>0)dashboardStatus.Text+="\nUnavailable: "+string.Join(", ",failedSources)+"; cached data retained.";
        var basil=settings.Sources.FirstOrDefault(s=>s.Id=="BasilMarket");if(basil is not null)dashboardStatus.Text+="\n"+(Offline?"Offline — ":!basil.Authorized?"Disabled — ":"")+BasilProgress.Summary(observations,basil,now)+"\n"+BasilEta.Display(BasilEta.Estimate(observations,basil,now,Offline,busy),now);
        if(!tables)return;
        var active=EligibleObservations().ToList();var items=store.Items();var age=TimeSpan.FromHours(settings.FreshHours);
        var recent=active.Where(o=>o.Kind==EvidenceKind.Asking&&o.Available&&o.Fresh(now,age)).ToList();
        marketCard.Text=$"MARKET COVERAGE\n{recent.Count:N0} recent asking listings\n{(kerning is null?0:fmSummaries.Count):N0} FM reference items";
        dealCard.Text=$"POTENTIAL DEALS\n{deals.Count:N0} pass your filters\nClick to investigate";
        watchCard.Text=$"WATCHLIST\n{items.Count(i=>i.Watch):N0} saved items\n{items.Count(i=>i.Watch&&i.Muted):N0} muted";
        SetColumns(dashboardWatch,["Item","Lowest ask","Evidence"]);
        decimal subtotal=0;int unpriced=0;var inventory=items.Where(i=>i.Quantity>0).ToList();
        foreach(var item in items)
        {
            var comparable=recent.Where(o=>TextKey.Search(o.Name)==TextKey.Search(item.Name)&&(!o.Equipment||o.Variant==item.Variant)).ToList();var target=comparable.FirstOrDefault();
            if(item.Watch){int n=dashboardWatch.Rows.Add(item.Name,TextKey.Mesos(comparable.Count==0?null:comparable.Min(o=>o.UnitPrice)),target is null?"Search listings for coverage":$"{comparable.Count} asks"+(item.Muted?" • muted":""));dashboardWatch.Rows[n].Tag=item;}
            if(item.Quantity>0){var value=target is null?null:ValuationEngine.Estimate(active,target,now,age,settings.Discount);if(value is null)unpriced++;else subtotal+=value.Value*item.Quantity;}
        }
        inventoryCard.Text=inventory.Count==0?"INVENTORY VALUE\nNo inventory items\nClick to add quantities":$"INVENTORY SUBTOTAL\n{(unpriced==inventory.Count?"Value unavailable":TextKey.Mesos(subtotal)+" mesos")}\n{unpriced} of {inventory.Count} items unpriced";
        watchEmpty.Text="Watch the items you trade most.\nChoose Manage to add an item, then search it online.";watchEmpty.Visible=dashboardWatch.Rows.Count==0;
        SetColumns(dashboardDeals,["Item","Ask","Gross / unit"]);foreach(var d in deals.Take(5)){int n=dashboardDeals.Rows.Add(d.Listing.Name,TextKey.Mesos(d.Listing.UnitPrice),TextKey.Mesos(d.Profit));dashboardDeals.Rows[n].Tag=d;dashboardDeals.Rows[n].Cells[0].ToolTipText=$"{d.Discount:N0}% below estimate • confidence {d.Estimate.Confidence}/100 • {d.Rating}";}
        dealsEmpty.Text="No deals pass your filters right now.\nA bargain needs enough independent, recent listings.\nFM summaries alone never trigger alerts.";dealsEmpty.Visible=deals.Count==0;
        SetColumns(dashboardMarket,["Item","7-day median","Lowest ask","Last seen","Freshness"]);
        foreach(var row in (kerning is null?Enumerable.Empty<FmSummary>():fmSummaries).OrderByDescending(r=>r.LastSeen).Take(8))
        {int n=dashboardMarket.Rows.Add(row.Name,TextKey.Mesos(row.Median7),TextKey.Mesos(row.LowestAsk),row.LastSeen.ToLocalTime().ToString("g"),row.Quality(now,settings.FreshHours));dashboardMarket.Rows[n].Tag=row;}
        marketEmpty.Text="No FM summaries loaded yet.\nEnable Kerning Exchange and allow the next update to finish.";marketEmpty.Visible=dashboardMarket.Rows.Count==0;
        dashboardMarket.Columns[0].FillWeight=145;dashboardMarket.Columns[3].FillWeight=140;dashboardMarket.Columns[4].FillWeight=155;
    }
}
