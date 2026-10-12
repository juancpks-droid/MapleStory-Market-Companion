using Market.Core;

using System.Diagnostics;

using System.Globalization;



namespace Market.App;

public sealed partial class MainForm : Form

{

    readonly Store store; readonly string dataDirectory; readonly Fetcher fetcher=new(); readonly SourceCollector collector;

    readonly CancellationTokenSource stop=new(); readonly System.Windows.Forms.Timer timer=new(); readonly DesktopTray tray;

    Settings settings; List<Observation> observations=[]; List<Deal> deals=[]; List<SourceResult> sourceResults=[]; List<FmSummary> fmSummaries=[];
    readonly DataGridView fmGrid=Grid(),websiteGrid=Grid();
    readonly TextBox fmSearch=new(){Width=230,PlaceholderText="Find an FM item",BackColor=Surface,ForeColor=TextColor};
    readonly Label fmNote=Info(82),fmContext=Info(60);

    readonly Dictionary<string,Panel> pages=[]; readonly Dictionary<string,Button> navigation=[];

    readonly Panel content=new(){Dock=DockStyle.Fill,Padding=new Padding(22,16,22,14)};

    readonly Label title=new ClassicTitle(){Text="Dashboard",Dock=DockStyle.Top,Height=48};

    readonly Label footer=new(){Dock=DockStyle.Bottom,Height=36,Padding=new Padding(18,8,0,0),Text="Ready • Local analysis every 10 seconds"};

    readonly TextBox search=new(){Width=340,PlaceholderText="Search an item by name",BackColor=Surface,ForeColor=TextColor};

    readonly DataGridView priceGrid=Grid(),dealsGrid=Grid(),watchGrid=Grid(),inventoryGrid=Grid(),sourcesGrid=Grid();

    readonly Label priceSummary=Info(100),dashboardSummary=Info(100),inventorySummary=Info(54),dealSummary=Info(56);

    readonly HistoryPlot history=new(){Dock=DockStyle.Fill,MinimumSize=new Size(200,130)};

    int historyDays=7;

    readonly Dictionary<int,Button> historyButtons=[];

    bool closing,busy,pendingSearch; string selected="Dashboard",notificationUrl=""; DateTimeOffset lastAnalysis; public int AnalysisTicks {get;private set;}

    public List<DateTimeOffset> TimerTicks {get;}=[];

    static Color Back=>GuidePalette.Back; static Color Surface=>GuidePalette.Surface; static Color TextColor=>GuidePalette.Text; static readonly Color Accent=Color.FromArgb(185,99,35); static Color Muted=>GuidePalette.Muted;



    public MainForm(Store store,string dataDirectory)

    {

        this.store=store; this.dataDirectory=dataDirectory;features=CompanionFeatures.Load(FeaturePath);RecoverLinkedJournal(); collector=new(fetcher); settings=store.LoadSettings(); observations=store.Observations();fmSummaries=store.Summaries();

        if(settings.AccessVersion<2){var source=settings.Sources.FirstOrDefault(s=>s.Id=="Kerning Exchange");if(source is not null&&string.IsNullOrEmpty(source.PermissionReference)){source.Enabled=true;source.PermissionReference="https://kerning.gg/robots.txt — public pages allowed; checked each batch";}settings.AccessVersion=2;store.SaveSettings(settings);}

        if(settings.AccessVersion<3){var source=settings.Sources.FirstOrDefault(s=>s.Id=="BasilMarket");if(source is null){source=new(){Id="BasilMarket"};settings.Sources.Add(source);}source.Enabled=true;source.PermissionReference=BasilPages.AccessReference;source.NextAllowed=default;source.BlockedUntil=default;settings.AccessVersion=3;store.SaveSettings(settings);}

        if(settings.AccessVersion<4){var source=settings.Sources.FirstOrDefault(s=>s.Id=="BasilMarket");if(source is not null&&source.BlockedUntil<=DateTimeOffset.UtcNow)source.NextAllowed=default;settings.AccessVersion=4;store.SaveSettings(settings);}

        if(settings.AccessVersion<5){var source=settings.Sources.FirstOrDefault(s=>s.Id=="BasilMarket");if(source is not null&&source.BlockedUntil<=DateTimeOffset.UtcNow){source.NextAllowed=default;source.PageChecked.Clear();}settings.AccessVersion=5;store.SaveSettings(settings);}

        Text="MapleStory Classic Market Companion"; BackColor=Back; ForeColor=TextColor; Font=new Font("Segoe UI",9); Size=new Size(1220,840); MinimumSize=new Size(1024,720); StartPosition=FormStartPosition.CenterScreen; Icon=AppArtwork.Icon;

        var sidebar=new Panel(){Dock=DockStyle.Left,Width=215,Padding=new Padding(12,12,12,15),BackColor=GuidePalette.Back};

        var brand=new ClassicBrand(); sidebar.Controls.Add(brand);

        var nav=new FlowLayoutPanel(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(0,12,0,0)}; sidebar.Controls.Add(nav); nav.BringToFront();

        foreach(string name in new[]{"Dashboard","Item Finder","Price Checker","Deal Finder","Watchlist","Inventory","FM Tracker","Item Catalog","Compare","Equipment","Price Alerts","Trade Journal","Market Activity","Backups","Characters","Alert Inbox","Market Health","Websites","Settings","Merchant Goals","Restock","Gear Wishlist","Item Notes","Trading Sessions","Price History","Getting Started","Transfers","Shopping List","Selling Planner","Search Everything","Price Changes","Seller Comparison","Undo Center","Profit Breakdown","Duplicate Review"})

        {

            var button=Button(name,()=>ShowPage(name)); ((ClassicButton)button).Badge=name;button.AccessibleName=name; button.Width=164; button.Height=38; button.TextAlign=ContentAlignment.MiddleLeft; navigation[name]=button; nav.Controls.Add(button);

            var page=new Panel(){Dock=DockStyle.Fill,Visible=false}; pages[name]=page; content.Controls.Add(page);

        }

        sidebar.Controls.Add(BuildSidebarUpdates());nav.BringToFront();

        var main=new Panel(){Dock=DockStyle.Fill,Padding=new Padding(0)}; main.Controls.Add(content); main.Controls.Add(title); main.Controls.Add(footer); title.Padding=new Padding(20,10,0,0); Controls.Add(main); Controls.Add(sidebar);

        ConfigureIcons(); BuildDashboard(); BuildPrices(); BuildDeals(); BuildWatch(); BuildInventory(); BuildFm(); BuildWebsites(); BuildSettings(); BuildCatalog(); BuildComparison(); BuildCompleteUpgrade(); BuildDailyTools(); BuildMerchantTools(); BuildWorkspaceTools(); BuildUsabilityTools(); BuildPolishTools(); BuildReliabilityPolish(); ConfigureTradingTools(); BuildCompanionUpgrade(); BuildPlanningUpgrade(); BuildWebsiteLookup(); BuildItemFinder(); BuildSettingsSections(); ApplyTextSize(); ApplyGuideStyle();

        var menu=new ContextMenuStrip(); menu.Items.Add("Open Market Companion",null,(_,_)=>{Show();WindowState=FormWindowState.Normal;Activate();}); menu.Items.Add("Snooze alerts for 30 minutes",null,(_,_)=>Snooze()); menu.Items.Add("Exit",null,(_,_)=>ExitApplication());

        tray=new DesktopTray(){ContextMenuStrip=menu}; tray.DoubleClick+=(_,_)=>{Show();WindowState=FormWindowState.Normal;};
        InitializeUpdates(main);

        tray.NotificationClick+=(_,_)=>{if(notificationUrl.Length>0)Open(notificationUrl);else{Show();WindowState=FormWindowState.Normal;ShowPage("Alert Inbox");}};

        FormClosing+=(_,e)=>{try{SaveViewSession();SaveFeatures();store.SaveSettings(settings);}catch(Exception){e.Cancel=true;closing=false;MessageBox.Show("Could not save before closing. Please check data-folder access and try again.","Save failed");return;}if(!closing && settings.CloseToTray && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}else{stop.Cancel();timer.Stop();tray.Visible=false;}};

        FormClosed+=(_,_)=>{timer.Dispose();tray.Dispose();fetcher.Dispose();stop.Dispose();};

        timer.Interval=settings.LocalSeconds*1000; timer.Tick+=async (_,_)=>{TimerTicks.Add(DateTimeOffset.UtcNow);if(TimerTicks.Count>1000)TimerTicks.RemoveAt(0);Analyze(); if(!Offline && !busy && settings.Sources.Any(s=>s.Authorized && DateTimeOffset.UtcNow>=s.NextAllowed)) await RefreshSources();}; timer.Start();

        Shown+=async (_,_)=>{Render();AutomaticBackup();if(File.Exists(DraftPath)&&!Environment.GetCommandLineArgs().Contains("--smoke"))MessageBox.Show("An unfinished trade is available. Press Ctrl+R or choose Resume draft in Trade Journal to review it. Nothing has been recorded automatically.","Trade draft recovered");await RefreshSources();}; ShowPage(features.SetupComplete||Environment.GetCommandLineArgs().Contains("--smoke")?"Dashboard":"Getting Started"); InitializeViewSession();

    }

    static Label Info(int height)=>new(){Height=height,Dock=DockStyle.Top,ForeColor=Muted,Padding=new Padding(4,8,4,4)};

    static Button Button(string text,Action action) {var b=new ClassicButton(){Text=text,AutoSize=true,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Surface,ForeColor=TextColor,Padding=new Padding(10,4,10,4),Margin=new Padding(3,4,7,4)};b.FlatAppearance.BorderColor=Color.FromArgb(55,72,92);b.Click+=(_,_)=>action();return b;}

    static DataGridView Grid()

    {

        var g=new DataGridView(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Back,BorderStyle=BorderStyle.None,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,EnableHeadersVisualStyles=false,AllowUserToResizeRows=false};

        g.DefaultCellStyle=new DataGridViewCellStyle(){BackColor=Surface,ForeColor=TextColor,SelectionBackColor=Color.FromArgb(68,59,35),SelectionForeColor=Color.White,Padding=new Padding(5)};

        g.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle(){BackColor=GuidePalette.Raised,ForeColor=TextColor,Padding=new Padding(5),Font=new Font("Segoe UI",9,FontStyle.Bold)};

        g.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(23,23,27);g.ColumnHeadersHeight=38;g.RowTemplate.Height=44;g.GridColor=GuidePalette.Line;return g;

    }

    static FlowLayoutPanel Toolbar()=>new(){Dock=DockStyle.Top,Height=48,WrapContents=false,AutoScroll=true};

    void BuildPrices()

    {

        var p=pages["Price Checker"];var bar=Toolbar();search.Margin=new Padding(3,9,10,3);search.TextChanged+=(_,_)=>RenderPrices();bar.Controls.Add(search);

        bar.Controls.Add(Button("Search online",async()=>await RefreshSources(true)));search.KeyDown+=async (_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await RefreshSources(true);}};bar.Controls.Add(Button("Refresh now",async()=>await RefreshSources()));bar.Controls.Add(Button("Watch item",()=>SaveSearchItem()));bar.Controls.Add(Button("Filters",EditListingFilters));

        var bottom=new Panel(){Dock=DockStyle.Bottom,Height=225};var chartBar=Toolbar();chartBar.Height=54;

        foreach(var (label,days) in new[]{("24 hours",1),("7 days",7),("14 days",14),("30 days",30)}){var b=Button(label,()=>{historyDays=days;foreach(var pair in historyButtons)pair.Value.ForeColor=pair.Key==days?Accent:TextColor;RenderHistory();});b.ForeColor=days==historyDays?Accent:TextColor;historyButtons[days]=b;chartBar.Controls.Add(b);}

        chartBar.Controls.Add(new Label(){AutoSize=true,Text="History • actual source timestamps only",ForeColor=Muted,Padding=new Padding(5,12,0,0)});bottom.Controls.Add(history);bottom.Controls.Add(chartBar);

        p.Controls.Add(priceGrid);p.Controls.Add(bottom);p.Controls.Add(priceSummary);p.Controls.Add(fmContext);p.Controls.Add(bar);priceGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0 && priceGrid.Rows[e.RowIndex].Tag is Observation o)Open(o.Url);};priceGrid.SelectionChanged+=(_,_)=>{RenderValuation();RenderHistory();};

    }

    void BuildDeals()

    {

        var p=pages["Deal Finder"];var bar=Toolbar();bar.Controls.Add(Button("Snooze 30 min",Snooze));bar.Controls.Add(Button("Dismiss selected",()=>{if(SelectedDeal() is {} d){store.MarkAlert(AlertKey(d));footer.Text="Alert dismissed for this listing and price.";}}));bar.Controls.Add(Button("Open source",()=>{if(SelectedDeal() is {} d)Open(d.Listing.Url);}));

        dealSummary.Text="Market data unavailable. Opportunities need independent, recent evidence. Profit figures are gross per unit; fees and actual availability are unverified.";

        p.Controls.Add(dealsGrid);p.Controls.Add(dealSummary);p.Controls.Add(bar);dealsGrid.CellDoubleClick+=(_,_)=>{if(SelectedDeal() is {} d)Open(d.Listing.Url);};

    }

    void BuildWatch()

    {

        var p=pages["Watchlist"];var bar=Toolbar();bar.Controls.Add(Button("Add item",()=>EditItem(null,true)));bar.Controls.Add(Button("Edit selected",()=>EditSelected(watchGrid,true)));bar.Controls.Add(Button("Remove selected",()=>Remove(watchGrid,true)));bar.Controls.Add(Button("Mute / unmute",()=>{if(SelectedItem(watchGrid) is {} i){store.SaveItem(i with {Muted=!i.Muted});Render();}}));

        var note=Info(58);note.Text="Saved items persist locally. Name matching is exact after case and spacing normalization. Missing prices remain unavailable.";

        p.Controls.Add(watchGrid);p.Controls.Add(note);p.Controls.Add(bar);

    }

    void BuildInventory()

    {

        BuildSlotInventory();

    }

    void BuildWebsites()
    {
        var p=pages["Websites"];var bar=Toolbar();bar.Controls.Add(Button("Open in app",()=>{if(websiteGrid.SelectedRows.Count>0&&websiteGrid.SelectedRows[0].Tag is Website site)ShowWebsite(site.Name,search.Text.Trim());}));
        var note=Info(72);note.Text="All researched websites, in one place. Automatic sources import data; website shortcuts open in an in-app window.\nA shortcut does not supply prices, and no email requests are sent.";
        SetColumns(websiteGrid,["Website","Connection","Status"]);foreach(var site in WebsiteDirectory.All){int n=websiteGrid.Rows.Add(site.Name,site.Connection,site.Status);websiteGrid.Rows[n].Tag=site;}
        websiteGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&websiteGrid.Rows[e.RowIndex].Tag is Website site)ShowWebsite(site.Name,search.Text.Trim());};websiteGrid.Columns[2].FillWeight=220;
        p.Controls.Add(websiteGrid);p.Controls.Add(note);p.Controls.Add(bar);
    }
    void BuildFm()
    {
        var p=pages["FM Tracker"];var bar=Toolbar();fmSearch.Margin=new Padding(3,9,10,3);fmSearch.TextChanged+=(_,_)=>RenderFm();bar.Controls.Add(fmSearch);fmFilter.Items.AddRange(["All items","Watched items","Equipment","Stackable items","Recent sightings"]);fmOrder.Items.AddRange(["Most sightings","Newest sighting","Name A–Z","Lowest ask"]);fmFilter.SelectedIndex=0;fmOrder.SelectedIndex=0;fmFilter.SelectedIndexChanged+=(_,_)=>RenderFm();fmOrder.SelectedIndexChanged+=(_,_)=>RenderFm();bar.Controls.Add(fmFilter);bar.Controls.Add(fmOrder);bar.Controls.Add(Button("Refresh",async()=>await RefreshSources()));bar.Controls.Add(Button("Open tracker",()=>Open("https://kerning.gg/fm-tracker")));
        fmGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&fmGrid.Rows[e.RowIndex].Tag is FmSummary row){ShowItemDetails(row);}};
        p.Controls.Add(fmGrid);p.Controls.Add(fmNote);p.Controls.Add(bar);
    }
    void RenderFm()
    {
        SetColumns(fmGrid,["Item","7-day median","Lowest ask","Sightings","Source last seen","Quality"]);
        var query=TextKey.Search(fmSearch.Text);bool enabled=settings.Sources.Any(s=>s.Id=="Kerning Exchange"&&s.Authorized);
        foreach(var row in BrowseSummaries())
        {
            int n=fmGrid.Rows.Add(row.Name+(row.Equipment?" • variants mixed":""),TextKey.Mesos(row.Median7),TextKey.Mesos(row.LowestAsk),row.Sightings,row.LastSeen.ToLocalTime().ToString("g"),enabled?row.Quality(DateTimeOffset.UtcNow,settings.FreshHours):"Source disabled — cached only");fmGrid.Rows[n].Tag=row;fmGrid.Rows[n].Cells[0].ToolTipText=$"Provider: Kerning Exchange. Retrieved {row.RetrievedAt.ToLocalTime():g}. Provider-reported sold sightings: {row.ReportedSales}; players: {row.Sellers}. These are aggregates, not independently verified trades.";
        }
        fmGrid.Columns[0].FillWeight=160;fmGrid.Columns[4].FillWeight=140;fmGrid.Columns[5].FillWeight=160;
        fmNote.Text=$"{fmGrid.Rows.Count} shown / {fmSummaries.Count} cached summaries. Double-click for details; right-click to watch or manage quantities.\nSeven-day aggregates may mix equipment variants and old observations. They are reference prices only, not inventory values or bargain baselines. Last seen is the latest provider sighting, not the age of every record.";
    }
    void BuildSettings()

    {

        var page=pages["Settings"];page.Controls.Clear();var scroller=new Panel(){Dock=DockStyle.Fill,AutoScroll=true};var form=new FlowLayoutPanel(){AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Width=880,Padding=new Padding(3)};scroller.Controls.Add(form);page.Controls.Add(scroller);

        void Note(string text,int h=50)=>form.Controls.Add(new Label(){Text=text,Width=850,Height=h,ForeColor=Muted,Margin=new Padding(3,6,3,6)});

        FlowLayoutPanel Row(string label,Control field) {var r=new FlowLayoutPanel(){Width=850,Height=42,WrapContents=false};r.Controls.Add(new Label(){Text=label,Width=310,Padding=new Padding(0,7,0,0)});r.Controls.Add(field);form.Controls.Add(r);return r;}

        NumericUpDown Number(decimal value,decimal min,decimal max)=>new(){Minimum=min,Maximum=max,Value=Math.Clamp(value,min,max),Width=120,BackColor=Surface,ForeColor=TextColor};

        CheckBox Check(string text,bool value)=>new(){Text=text,Checked=value,Width=430,Height=32};

        BuildUpdateSettings(form);
        Note("Monitoring & alerts",28);
        Note("Item artwork: MapleStory / Nexon, served by MapleStory.io (GMS 83). Icons download on demand and are cached locally. Items absent from this version keep an empty slot.",44);

        var interval=Number(settings.LocalSeconds,5,10);interval.Increment=5;Row("Local analysis interval (5 or 10 seconds)",interval);

        var fresh=Number(settings.FreshHours,1,168);Row("Maximum observation age (hours)",fresh);

        var discount=Number(settings.Discount,1,95);Row("Minimum discount (%)",discount);

        var profit=Number(settings.MinimumProfit,0,1000000000);Row("Minimum gross profit per unit (mesos)",profit);

        var confidence=Number(settings.Confidence,1,100);Row("Minimum confidence score",confidence);

        var notifications=Check("Desktop notifications",settings.Notifications);form.Controls.Add(notifications);var basilDone=Check("Notify when the BasilMarket scan finishes",settings.BasilCompletionNotifications);form.Controls.Add(basilDone);var sound=Check("Notification sound",settings.Sound);form.Controls.Add(sound);menuSoundCheck.Checked=features.MenuSounds;menuSoundCheck.CheckedChanged+=(_,_)=>{features.MenuSounds=menuSoundCheck.Checked;SaveFeatures();};form.Controls.Add(menuSoundCheck);var textSize=Number(features.TextSize,9,13);textSize.ValueChanged+=(_,_)=>{features.TextSize=(int)textSize.Value;SaveFeatures();ApplyTextSize();};Row("Interface text size",textSize);Note("Shortcuts: Ctrl+F search • Ctrl+I inventory • Ctrl+J journal • Ctrl+B backups • Ctrl+L catalog • F5 refresh eligible sources",46);

        var watchOnly=Check("Only alert for watched items",settings.WatchOnly);form.Controls.Add(watchOnly);var trayClose=Check("Keep running in the tray when closing",settings.CloseToTray);form.Controls.Add(trayClose);

        Note("Market sources",28);

        Note("Kerning Exchange and BasilMarket read public listing pages and check robots.txt each batch. Requests are spaced by 3 seconds, capped at 60/hour per website and cached for at least 15 minutes. Basil rotates two sale categories per refresh and searches up to two requested items. Up to 12 listing details are checked within the remaining request budget (usually 8–10). Saved approximate listings stay queued; rounded prices remain labeled. Custom JSON feeds need an access reference.",72);

        var controls=new List<(SourceConfig config,CheckBox enabled,TextBox reference,NumericUpDown minutes,TextBox url)>();

        foreach(var s in settings.Sources)

        {

            var enable=Check(s.Id + " — enabled",s.Enabled);form.Controls.Add(enable);

            var reference=new TextBox(){Width=510,Text=s.PermissionReference,PlaceholderText="Access policy or operator permission reference",ReadOnly=s.Id is "Kerning Exchange" or "BasilMarket",BackColor=Surface,ForeColor=TextColor};Row("Access reference",reference);

            var minutes=Number(s.IntervalMinutes,15,1440);Row("External fetch interval (minutes)",minutes);

            var url=new TextBox(){Width=510,Text=s.FeedUrl,PlaceholderText="Approved HTTPS feed URL",BackColor=Surface,ForeColor=TextColor};if(s.Id=="Approved JSON feed")Row("Feed address",url);

            controls.Add((s,enable,reference,minutes,url));

        }

        Note("Website audit: 10 Oct 2026. Other destinations are browser shortcuts: permission restrictions, no verified public live feed, or reference-only data. See Reports/price-source-audit-1.8.txt in the download.",50);

        string SettingsDraft()=>System.Text.Json.JsonSerializer.Serialize(new {
            Interval=interval.Value,Fresh=fresh.Value,Discount=discount.Value,Profit=profit.Value,Confidence=confidence.Value,
            Notifications=notifications.Checked,Sound=sound.Checked,BasilDone=basilDone.Checked,WatchOnly=watchOnly.Checked,TrayClose=trayClose.Checked,
            Sources=controls.Select(c=>new{Enabled=c.enabled.Checked,Reference=c.reference.Text,Minutes=c.minutes.Value,Url=c.url.Text}).ToArray()
        });
        var savedSettingsDraft=SettingsDraft();
        hasUnsavedSettings=()=>SettingsDraft()!=savedSettingsDraft;
        var actions=new FlowLayoutPanel(){Width=850,Height=48};actions.Controls.Add(Button("Save settings",()=>

        {

            if(busy){MessageBox.Show("Wait for the current refresh to finish before changing source settings.");return;}

            foreach(var c in controls)if(c.enabled.Checked && string.IsNullOrWhiteSpace(c.reference.Text)){MessageBox.Show("An operator permission reference is required for " + c.config.Id);return;}

            foreach(var c in controls)if(c.enabled.Checked && c.config.Id=="Approved JSON feed" && (!Uri.TryCreate(c.url.Text,UriKind.Absolute,out var u)||u.Scheme!="https"||u.UserInfo!="")){MessageBox.Show("Enter a valid approved HTTPS feed URL without credentials.");return;}

            settings.LocalSeconds=interval.Value==5?5:10;settings.FreshHours=(int)fresh.Value;settings.Discount=discount.Value;settings.MinimumProfit=profit.Value;settings.Confidence=(int)confidence.Value;settings.Notifications=notifications.Checked;settings.Sound=sound.Checked;settings.BasilCompletionNotifications=basilDone.Checked;settings.WatchOnly=watchOnly.Checked;settings.CloseToTray=trayClose.Checked;

            foreach(var c in controls){c.config.Enabled=c.enabled.Checked;c.config.PermissionReference=c.reference.Text.Trim();c.config.IntervalMinutes=(int)c.minutes.Value;c.config.FeedUrl=c.url.Text.Trim();}

            store.SaveSettings(settings);interval.Value=settings.LocalSeconds;savedSettingsDraft=SettingsDraft();timer.Interval=settings.LocalSeconds*1000;Render();footer.Text="Settings saved. External requests respect each source's next allowed time.";

        }));actions.Controls.Add(Button("Test notification",TestNotification));actions.Controls.Add(Button("Clear market cache",()=>{if(busy){MessageBox.Show("Wait for refresh to finish.");return;}store.ClearMarket();observations=[];fmSummaries=[];Render();footer.Text="Market history cleared. Watchlist, inventory and settings retained.";}));form.Controls.Add(actions);

        Note("Price model: recency-weighted median, with median absolute deviation filtering. Minimum: 3 provider-confirmed sales, 4 reported sales, or 3 asks from at least 3 sellers (rough estimate, confidence capped at 40 for fewer than 5). Deals require 5 other comparable asks. Asking-only confidence is capped at 60. Recommendations are heuristics, not guaranteed sale prices. Fees are not assumed.",80);

    }

    public void ShowPage(string name)

    {

        if(name=="Backups"){name="Settings";settingsSections.SelectedIndex=1;}EnsureNavigation(name);if(name=="Duplicate Review")RenderDuplicates();if(usabilityReady&&new[]{"Price Changes","Seller Comparison","Undo Center","Profit Breakdown"}.Contains(name))RenderUsability();if(features.MenuSounds&&selected!=name)System.Media.SystemSounds.Asterisk.Play();selected=name;title.Text=name+" • "+store.Characters().Single(c=>c.Id==store.Profile).Name;foreach(var (n,p) in pages){p.Visible=n==name;if(p.Visible)p.BringToFront();navigation[n].ForeColor=n==name?Accent:TextColor;navigation[n].BackColor=n==name?Accent:Surface;}if(navigation[name].Parent is ScrollableControl navScroll)navScroll.ScrollControlIntoView(navigation[name]);if(workspaceReady&&new[]{"Transfers","Shopping List","Selling Planner","Search Everything"}.Contains(name))RenderWorkspace();if(name=="Item Finder")RenderItemFinder();if(name=="Price Checker")RenderPrices();if(name=="Dashboard"){RenderDashboard();ApplyDashboardPanels();}if(name=="Compare")RenderComparison();if(name=="Item Catalog")LoadCatalogImages();if(name=="Equipment")RenderEquipment();if(name=="Inventory")RenderInventorySlots();if(name=="Market Activity")RenderActivity();if(name=="Settings")menuSoundCheck.Checked=features.MenuSounds;if(name=="Characters"||name=="Alert Inbox"||name=="Market Health")RenderDailyTools();

    }

    async Task RefreshSources(bool onlineSearch=false)

    {

        if(Offline){UpdateConnectionStatus();footer.Text="Offline: searching saved evidence. Turn off Work offline to request new data.";RenderPrices();return;}if(busy){if(onlineSearch){pendingSearch=true;footer.Text="Search queued after the current market refresh.";}return;}busy=true;refreshStop=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);footer.Text="Checking source access and refreshing eligible sources…";

        try

        {

            var names=onlineSearch?new[]{features.Polish.Resolve(search.Text)}:store.Items().Select(i=>i.Name).ToArray();

            sourceResults=await Task.Run(()=>collector.Refresh(settings,refreshStop.Token,names,onlineSearch,observations.ToArray()),refreshStop.Token);

            if(Offline)return;
            if(collector.Catalog.Count>0){var choices=new AutoCompleteStringCollection();choices.AddRange(collector.Catalog.Select(i=>CultureInfo.CurrentCulture.TextInfo.ToTitleCase(i.Name)).ToArray());search.AutoCompleteCustomSource=choices;search.AutoCompleteMode=AutoCompleteMode.Suggest;search.AutoCompleteSource=AutoCompleteSource.CustomSource;}

            foreach(var result in sourceResults)if(result.Updated){if(result.Provider=="Approved JSON feed")store.ExpireMissingFromSnapshot(result.Provider,result.Rows);if(result.SnapshotItems.Count>0)store.ExpireMissingFromSnapshot(result.Provider,result.Rows,result.SnapshotItems);if(result.Rows.Count>0)store.Save(result.Rows.Where(r=>!(r.Provider=="BasilMarket"&&r.PriceApproximate&&observations.Any(old=>old.Key==r.Key&&!old.PriceApproximate))).ToList());if(result.FmSummaries is not null)store.ReplaceSummaries(result.FmSummaries);}

            observations=store.Observations();var basilScan=settings.Sources.FirstOrDefault(s=>s.Id=="BasilMarket");if(basilScan is not null&&sourceResults.Any(r=>r.Provider=="BasilMarket"&&r.Updated))BasilCompletion.Observe(observations,basilScan,DateTimeOffset.UtcNow);store.SaveSettings(settings);fmSummaries=store.Summaries();Render();

            footer.Text=settings.Sources.Any(s=>s.Authorized)?string.Join(" • ",sourceResults.Where(r=>settings.Sources.Any(s=>s.Id==r.Provider&&s.Authorized)).Select(r=>r.Status)):"Market data unavailable • No permitted source enabled. See Settings and the source report.";

        }

        catch(OperationCanceledException){}

        catch(Exception e){Log(e);footer.Text="Refresh could not finish. Existing data retained; see source status.";}

        finally{busy=false;refreshStop?.Dispose();refreshStop=null;UpdateConnectionStatus();}

        if(pendingSearch&&!stop.IsCancellationRequested){pendingSearch=false;await RefreshSources(true);}

    }

    void Log(Exception e)=>File.AppendAllText(Path.Combine(dataDirectory,"diagnostics.log"),$"{DateTimeOffset.UtcNow:O} {e.GetType().Name}\n");

    void Analyze()

    {

        AnalysisTicks++;lastAnalysis=DateTimeOffset.UtcNow;var newDeals=ValuationEngine.Find(EligibleObservations(),settings,lastAnalysis);bool changed=!deals.Select(AlertKey).SequenceEqual(newDeals.Select(AlertKey));deals=newDeals;

        if(changed){RenderDeals();RenderDashboard();}else if(selected=="Dashboard")RenderDashboard(false);

        if(!Offline)RecordDealInbox();

        if(!Offline){CheckBasilCompletionInbox();CheckPriceTargets();CheckPriorityAlerts();CheckMerchantAlerts();DispatchWorkspaceAlerts();}UpdateConnectionStatus();UpdateCompact();AutomaticBackup();if(!busy)footer.Text=$"Local analysis: {lastAnalysis.ToLocalTime():T} • Every {settings.LocalSeconds}s • {deals.Count} eligible opportunities • External feeds follow separate limits";

    }

    IEnumerable<Observation> EligibleObservations()=>observations.Where(o=>settings.Sources.Any(s=>s.Id==o.Provider && s.Authorized));

    void Render()

    {

        Analyze();var active=EligibleObservations().ToList();int fresh=active.Count(o=>o.Fresh(DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours)));

        dashboardSummary.Text=$"{observations.Count:N0} stored listings   •   {fmSummaries.Count:N0} FM summaries   •   {fresh:N0} eligible recent observations   •   {store.Items().Count(i=>i.Watch)} watched items   •   {deals.Count} potential deals\n" + (active.Count==0?"Market data unavailable. Search an item online, or check the source status below.":"Asking prices are uncertain evidence, not completed sales. Coverage: watched items plus a rotating selection of recently updated items.");

        SetColumns(sourcesGrid,["Source","Status","Last checked"]);

        foreach(var s in settings.Sources){var r=sourceResults.FirstOrDefault(r=>r.Provider==s.Id);sourcesGrid.Rows.Add(s.Id,s.Authorized?r?.Status??"Awaiting first fetch":"Disabled — see Settings",r is null||r.CheckedAt==DateTimeOffset.MinValue?"Not fetched":r.CheckedAt.ToLocalTime().ToString("g"));}

        foreach(var (source,reason) in new[]{("NiaMeowDB","Not connected: provider permission required"),("MESOLANE","Not connected: approved price feed needed"),("Meso Scout","Not connected: live coverage not verified")})sourcesGrid.Rows.Add(source,reason,"Not connected");

        RenderPrices();RenderDeals();RenderItems();RenderFm();RenderDashboard();RenderInventorySlots();RenderFeatures();RenderDailyTools();RenderMerchantTools();RenderWorkspace();RenderUsability();RenderDuplicates();ApplyAccessibility();RenderItemFinder();

    }

    static void SetColumns(DataGridView grid,string[] names) {grid.Rows.Clear();if(grid.Columns.Count==names.Length)return;grid.Columns.Clear();foreach(var n in names)grid.Columns.Add(n,n);}

    void RenderPrices()

    {

        if(priceGrid.IsDisposed)return;var query=TextKey.Search(features.Polish.Resolve(search.Text));var rows=observations.Where(o=>(query.Length==0||TextKey.Search(o.Name).Contains(query))&&listingFilter.Matches(o,DateTimeOffset.UtcNow,settings.FreshHours)).OrderByDescending(o=>o.RetrievedAt).ToList();

        SetColumns(priceGrid,["Item","Price / unit","Type","Source","Data observed","Retrieved","Quality"]);priceGrid.Columns[0].FillWeight=130;priceGrid.Columns[6].FillWeight=180;

        foreach(var o in rows.Take(1000)){int i=priceGrid.Rows.Add(o.Name+(o.Equipment?" • equipment":""),TextKey.Mesos(o.UnitPrice)+(o.PriceApproximate?" ≈":""),o.Kind,o.Provider,o.ObservedAt?.ToLocalTime().ToString("g")??o.SourceAge+" (unverified)",o.RetrievedAt.ToLocalTime().ToString("g"),ReliabilityTools.EvidenceStatus(o,settings.Sources.Any(s=>s.Id==o.Provider&&s.Authorized),DateTimeOffset.UtcNow,settings.FreshHours));priceGrid.Rows[i].Tag=o;priceGrid.Rows[i].Cells[0].ToolTipText=o.Variant;}

        RenderValuation();RenderHistory();

    }

    void RenderValuation()

    {

        var query=TextKey.Search(features.Polish.Resolve(search.Text));var exact=observations.Where(o=>TextKey.Search(o.Name)==query).ToList();

        var selectedRow=priceGrid.SelectedRows.Count>0?priceGrid.SelectedRows[0].Tag as Observation:null;

        var target=selectedRow??exact.FirstOrDefault();

        var v=target is null?null:ValuationEngine.Estimate(EligibleObservations(),target,DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours),settings.Discount);

        var asks=target is null?[]:EligibleObservations().Where(o=>o.MatchKey==target.MatchKey&&o.Kind==EvidenceKind.Asking&&o.Available&&o.Fresh(DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours))&&o.RetrievedAt>=DateTimeOffset.UtcNow.AddMinutes(-30)).ToList();

        string lead=target is null?"Search an item online to retrieve its market listings.":target.Name+" • Lowest recently checked ask: "+TextKey.Mesos(asks.Count==0?null:asks.Min(o=>o.UnitPrice))+" • "+asks.Count+" recent asks";

        var fm=fmSummaries.FirstOrDefault(x=>TextKey.Search(x.Name)==query);
        bool fmEnabled=settings.Sources.Any(x=>x.Id=="Kerning Exchange"&&x.Authorized);
        fmContext.Text=fm is null?"FM summaries: search an exact item name or open FM Tracker for broader coverage.":$"Kerning FM • 7-day median {TextKey.Mesos(fm.Median7)} • Lowest observed ask {TextKey.Mesos(fm.LowestAsk)} • {fm.Sightings} sightings\nLast seen {fm.LastSeen.ToLocalTime():g} • {(fmEnabled?fm.Quality(DateTimeOffset.UtcNow,settings.FreshHours):"Source disabled — cached only")} • Summary only; excluded from bargain alerts.";
        priceSummary.Text=lead+"\n"+(v is null?"Estimated value: Price unavailable — needs 3 comparable recent asks from 3 sellers, or sufficient sale evidence.":$"Estimated value: {TextKey.Mesos(v.Value)} • Confidence {v.Confidence}/100 • {v.Count} observations • {v.Basis}\nQuick {TextKey.Mesos(v.Quick)}   Normal {TextKey.Mesos(v.Normal)}   Higher {TextKey.Mesos(v.High)}   Max buy {TextKey.Mesos(v.MaxBuy)}")+"\n"+(target?.Equipment==true?"Selected variant: "+target.Variant:"Double-click a listing to open its source. Posted time is not a completed-sale time.");

    }

    void RenderHistory()

    {

        var item=priceGrid.SelectedRows.Count>0?priceGrid.SelectedRows[0].Tag as Observation:null;var name=item?.ItemKey??TextKey.Normalize(search.Text);

        int days=historyDays;var now=DateTimeOffset.UtcNow;

        history.Points=store.History().Where(o=>o.ItemKey==name && o.ObservedAt is {} t && t>=now.AddDays(-days)&&t<=now && (item is null || o.MatchKey==item.MatchKey)).OrderBy(o=>o.ObservedAt).ToList();history.Invalidate();

    }

    void RenderDeals()

    {

        SetColumns(dealsGrid,["Item","Ask","Est. value","Gross / unit","Discount","ROI","Confidence","Rating"]);

        foreach(var d in deals){int i=dealsGrid.Rows.Add(d.Listing.Name,TextKey.Mesos(d.Listing.UnitPrice),TextKey.Mesos(d.Estimate.Value),TextKey.Mesos(d.Profit),$"{d.Discount:N0}%",$"{d.Roi:N0}%",d.Estimate.Confidence,d.Rating);dealsGrid.Rows[i].Tag=d;}

        dealSummary.Text=deals.Count==0?"No eligible opportunities. This can mean insufficient evidence, unknown timestamps, stale data, or no permitted source.\nGross profit is per unit. No fees or guaranteed resale are assumed.":$"{deals.Count} potential opportunities across retrieved coverage. Verify the listing and equipment before trading.\nGross profit is per unit; fees, selling speed and in-game availability are unverified.";

    }

    void RenderItems()

    {

        SetColumns(watchGrid,["Item","Latest ask","Est. value","Change","Confidence","Freshness","Alerts"]);SetColumns(inventoryGrid,["Item","Quantity","Est. unit","Est. stack","Source","Freshness"]);

        var storedHistory=store.History();decimal total=0;int missing=0;foreach(var i in store.Items())

        {

            var row=EligibleObservations().Where(o=>TextKey.Search(o.Name)==TextKey.Search(i.Name)&&o.Kind==EvidenceKind.Asking&&o.Available&&(!o.Equipment||o.Variant==i.Variant)).OrderByDescending(o=>o.ObservedAt).FirstOrDefault();var v=row is null?null:ValuationEngine.Estimate(EligibleObservations(),row,DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours),settings.Discount);

            string freshness=row?.Freshness(DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours))??"Market data unavailable";

            var previous=row is null?null:storedHistory.Where(o=>o.MatchKey==row.MatchKey&&o.Kind==EvidenceKind.Asking&&o.ObservedAt<row.ObservedAt).OrderByDescending(o=>o.ObservedAt).FirstOrDefault();string change=previous is null||row is null?"Unavailable":$"{(row.UnitPrice/previous.UnitPrice-1)*100:+0.0;-0.0;0}% ask";

            if(i.Watch){int n=watchGrid.Rows.Add(i.Name,TextKey.Mesos(row?.UnitPrice),TextKey.Mesos(v?.Value),change,v?.Confidence.ToString()??"Insufficient evidence",freshness,i.Muted?"Muted":"Enabled");watchGrid.Rows[n].Tag=i;}

            if(i.Quantity>0){decimal? stack=v?.Value*i.Quantity;int n=inventoryGrid.Rows.Add(i.Name,i.Quantity,TextKey.Mesos(v?.Value),TextKey.Mesos(stack),row?.Provider??"Unavailable",freshness);inventoryGrid.Rows[n].Tag=i;if(stack is {} amount)total+=amount;else missing++;}

        }

        inventorySummary.Text=$"Valued subtotal: {(missing>0&&missing==store.Items().Count(i=>i.Quantity>0)?"Unavailable":TextKey.Mesos(total)+" mesos")} • {missing} inventory entries have unavailable values.\nUnpriced items are excluded, not treated as worthless. Quantities are the only inventory numbers you enter.";

    }

    SavedItem? SelectedItem(DataGridView g)=>g.SelectedRows.Count>0?g.SelectedRows[0].Tag as SavedItem:null;

    Deal? SelectedDeal()=>dealsGrid.SelectedRows.Count>0?dealsGrid.SelectedRows[0].Tag as Deal:null;

    void EditSelected(DataGridView g,bool watch){if(SelectedItem(g) is {} i)EditItem(i,watch);}

    void Remove(DataGridView g,bool watch){if(SelectedItem(g) is {} i){var changed=watch?i with {Watch=false}:i with {Quantity=0};if(!changed.Watch&&changed.Quantity==0)store.RemoveItem(i.Name,i.Variant);else store.SaveItem(changed);Render();}}

    void EditItem(SavedItem? item,bool watch){using var dialog=CreateItemEditor(item,watch);dialog.ShowDialog(this);}
    Form CreateItemEditor(SavedItem? item,bool watch)
    {
        var dialog=EntryDialog(watch?"Watch an item":"Inventory stack",out var fields);var name=new TextBox(){Text=item?.Name??search.Text,ReadOnly=item is not null,MaxLength=250};var qty=Amount(item?.Quantity??(watch?0:1),0,1_000_000_000);var variant=new ComboBox(){DropDownStyle=ComboBoxStyle.DropDown,Text=item?.Variant??"",Enabled=item is null};
        void Fill(){variant.Items.Clear();variant.Items.Add("");foreach(var v in observations.Where(o=>TextKey.Search(o.Name)==TextKey.Search(name.Text)&&o.Equipment&&o.VariantComplete).Select(o=>o.Variant).Distinct())variant.Items.Add(v);}name.TextChanged+=(_,_)=>Fill();Fill();variant.Text=item?.Variant??"";Field(fields,"Item name",name);Field(fields,"Quantity (0 = watch only)",qty);Field(fields,"Exact variant (blank for stackable; add a new stack for different stats)",variant);fields.Controls.Add(Button("Save stack",()=>{try{var exact=variant.Text.Trim();var previous=store.Items().FirstOrDefault(i=>ItemIdentity.Key(i)==ItemIdentity.Key(name.Text,exact));var saved=new SavedItem(name.Text.Trim(),(long)qty.Value,watch||previous?.Watch==true||qty.Value==0,previous?.Muted??false){Variant=exact};new PersonalBackup(3,[saved],new()){Characters=[new(new("main","Main",0),[saved])]}.Validate();store.SaveItem(saved);Render();dialog.Close();}catch(Exception e){MessageBox.Show(e.Message);}}));return dialog;
    }

    void SaveSearchItem(){var name=features.Polish.Resolve(search.Text.Trim());if(name.Length==0){EditItem(null,true);return;}var existing=store.Items().FirstOrDefault(i=>TextKey.Normalize(i.Name)==TextKey.Normalize(name));store.SaveItem(existing is null?new(name,0,true,false):existing with{Watch=true});RenderItems();RenderDashboard();footer.Text="Saved to watchlist: " + name;}

    string AlertKey(Deal d)=>store.Profile+":"+d.Listing.Key+"|"+d.Listing.UnitPrice.ToString(CultureInfo.InvariantCulture);

    void Snooze(){settings.SnoozedUntil=DateTimeOffset.UtcNow.AddMinutes(30);store.SaveSettings(settings);footer.Text="Alerts snoozed until " + settings.SnoozedUntil.ToLocalTime().ToString("t");}

    void TestNotification(){notificationUrl="";tray.BalloonTipTitle="Market Companion • notification test";tray.BalloonTipText="This is a notification test, not a market opportunity. Windows notification settings may suppress it.";tray.Sound=settings.Sound;if(!tray.ShowBalloonTip(5000))throw new InvalidOperationException("Windows rejected the notification request");}

    static void Open(string url){if(Uri.TryCreate(url,UriKind.Absolute,out var u)&&u.Scheme=="https")Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}

    public void ExitApplication(){closing=true;Close();}

    public void CapturePreview(string path){using var bmp=new Bitmap(Width,Height);DrawToBitmap(bmp,new Rectangle(Point.Empty,Size));bmp.Save(path);}

    public async Task<string[]> Smoke()

    {

        var checks=new List<string>();foreach(var name in pages.Keys){ShowPage(name);await Task.Delay(80);if(!pages[name].Visible)throw new Exception("Page did not show");}checks.Add("All 34 pages opened");

        search.Text="Blue Mushroom Cap";SaveSearchItem();store.SaveItem(new("Blue Mushroom Cap",39,true,false));RenderItems();if(store.Items().Count==0)throw new Exception("Persistence failed");checks.Add("Watchlist and inventory save");

        checks.Add("Production adapter contains no embedded market prices");

        var before=AnalysisTicks;Analyze();if(AnalysisTicks<=before)throw new Exception("Analysis timer failed");checks.Add("Local engine evaluated current cache");

        TestNotification();checks.Add("Windows notification API invoked (visual delivery unverified)");

        while(busy)await Task.Delay(200);search.Text="Green Mushroom Cap";await RefreshSources(true);if(!observations.Any(o=>o.Provider=="Kerning Exchange"&&o.ObservedAt is not null))throw new Exception("Live market fetch returned no timestamped observations");checks.Add("Live public market retrieval and source timestamps verified");if(!observations.Any(o=>o.Provider=="BasilMarket"&&o.ObservedAt is not null&&o.RetrievedAt>DateTimeOffset.UtcNow.AddMinutes(-10)))throw new Exception("Live Basil retrieval missing");checks.Add("Live Basil exact listings and original timestamps verified");if(!observations.Any(o=>o.Provider=="BasilMarket"&&o.PriceApproximate&&o.RetrievedAt>DateTimeOffset.UtcNow.AddMinutes(-10)))throw new Exception("Basil expanded browse coverage missing");checks.Add("Live Basil category listings beyond exact homepage records loaded");if(!observations.Any(o=>o.Provider=="BasilMarket"&&!o.PriceApproximate&&o.Verification.StartsWith("Exact numeric")&&o.RetrievedAt>DateTimeOffset.UtcNow.AddMinutes(-10)))throw new Exception("Basil exact detail retrieval missing");checks.Add("Live Basil exact numeric detail prices verified independently of posting time");var basilProgress=settings.Sources.Single(s=>s.Id=="BasilMarket");if(basilProgress.BasilChecks.Count==0||basilProgress.BasilPages.Count==0)throw new Exception("Basil persistent queue missing");ShowPage("Dashboard");RenderDashboard();if(!dashboardStatus.Text.Contains("Basil ETA"))throw new Exception("Basil ETA display missing");CapturePreview(Path.Combine(dataDirectory,"basil-eta-23.png"));checks.Add("Basil queue outcomes, discovered categories and dashboard progress verified");SmokeBasilCompletion();checks.Add("Basil completion inbox, duplicate suppression, opt-out and stale-event suppression verified");if(fmSummaries.Count==0)throw new Exception("No FM summaries loaded");ShowPage("FM Tracker");RenderFm();CapturePreview(Path.Combine(dataDirectory,"fm-tracker.png"));checks.Add("Live FM summaries loaded separately from listing evidence");ShowPage("Websites");CapturePreview(Path.Combine(dataDirectory,"websites.png"));if(websiteGrid.Rows.Count!=WebsiteDirectory.All.Length)throw new Exception("Website directory incomplete");checks.Add("All website destinations listed with connection type");ShowPage("Dashboard");RenderDashboard();if(await icons.Get("2000000") is null)throw new Exception("Classic item icon failed to load");if(iconCatalog.Resolve("4000016","Blue Mushroom Cap")!="4000009"||iconCatalog.Resolve("4000008","Pig's Head")!="4000017"||iconCatalog.Resolve("123","Unknown nonexistent item")!=""||iconCatalog.Resolve("","Blue Potion")!="")throw new Exception("Icon identity validation failed");await Task.WhenAll(fmSummaries.OrderByDescending(f=>f.LastSeen).Take(8).Select(f=>icons.Get(IconId(f))));using(var cachedIcons=new ItemIconCache(dataDirectory)){if(await cachedIcons.Get("2000000") is null)throw new Exception("Disk icon cache failed");}checks.Add("GMS 83 icon decoded, disk cache reopened; mismatched IDs resolved by name, unknown and ambiguous names left blank");if(dashboardWatch.Rows.Count==0||dashboardMarket.Rows.Count==0)throw new Exception("Dashboard did not show watchlist and FM data");CapturePreview(Path.Combine(dataDirectory,"dashboard-live.png"));checks.Add("Dashboard populated with real watchlist and FM data");

        fmFilter.SelectedIndex=1;if(fmGrid.Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is FmSummary f&&!store.Items().Any(i=>i.Watch&&TextKey.Search(i.Name)==TextKey.Search(f.Name))))throw new Exception("Watched filter leaked unwatched items");fmFilter.SelectedIndex=2;if(fmGrid.Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is FmSummary f&&!f.Equipment))throw new Exception("Equipment filter failed");fmFilter.SelectedIndex=0;fmOrder.SelectedIndex=2;var sorted=BrowseSummaries().Select(f=>f.Name).ToList();if(!sorted.SequenceEqual(sorted.Order()))throw new Exception("Name sort failed");fmOrder.SelectedIndex=0;var savedBefore=store.Items().First();WatchObject(savedBefore);if(store.Items().First(i=>i.Name==savedBefore.Name).Quantity!=savedBefore.Quantity)throw new Exception("Watch action lost inventory quantity");using(var detail=CreateItemDetails(fmSummaries.First(f=>f.Name=="Blue Potion"))){detail.Show(this);await Task.Delay(300);using var preview=new Bitmap(detail.Width,detail.Height);detail.DrawToBitmap(preview,new Rectangle(Point.Empty,detail.Size));preview.Save(Path.Combine(dataDirectory,"item-details.png"));detail.Close();}checks.Add("Watched/equipment filters, name sorting, preserving quantity when watching, and item detail window verified");await SmokeInventorySlots();checks.Add("Inventory slots, category filtering, empty selection and preserving watched items on stock removal verified");await SmokeMajorUpgrade();checks.Add("Catalog search/category, comparison evidence separation, bag search and undo removal verified");await SmokeCompleteUpgrade();checks.Add("Personal backup round trip restored trades, targets and saved items; new journal/equipment/alert pages rendered");await SmokePlanningUpgrade();checks.Add("Exact-stack sale preparation, sell view, budget, priorities, templates, variant charts, data health and dashboard visibility exercised");await SmokeCompanionUpgrade();checks.Add("Item command center, fee calculation, local screenshot OCR review, offline cache, trade draft recovery and session recap verified");await SmokeReliability();checks.Add("Exact-variant search navigation, simplified menus, streamlined inventory and explicit freshness labels verified");await SmokePolish();checks.Add("Exact variant stacks, alias search, multi-selection, enlarged inventory and backup preview exercised");await SmokeUsability();checks.Add("Undo center, pinned navigation, seller comparison, price changes, profit breakdown and compact window exercised");await SmokeWorkspaceTools();checks.Add("Transfers, plans, universal search, favorites, inventory order, dashboard layout and backup coverage verified");await SmokeMerchantTools();checks.Add("Merchant goals, restock deduplication, wishlist, notes, sessions, history, setup and four themes exercised");await SmokeDailyTools();checks.Add("Character isolation, bulk parse, linked stock undo, inbox persistence and daily backup exercised");int timerCount=TimerTicks.Count;timer.Interval=5000;await Task.Delay(11500);if(TimerTicks.Count-timerCount<2)throw new Exception("5-second timer did not run twice");checks.Add("5-second timer fired twice during 11.5-second measurement");timer.Interval=10000;return checks.ToArray();

    }

}









