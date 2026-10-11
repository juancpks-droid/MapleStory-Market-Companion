using Market.Core;
using System.Text.Json;

namespace Market.App;
public sealed partial class MainForm
{
    readonly DataGridView characterGrid=Grid(),inboxGrid=Grid(),healthGrid=Grid();
    readonly Label characterNote=Info(65),healthNote=Info(65),backupStatus=Info(55);
    ListingFilter listingFilter=new();
    DateTimeOffset backupAttempt;
    void RecoverLinkedJournal()
    {
        bool changed=false;foreach(var trade in store.LinkedTrades()){var existing=features.Trades.FirstOrDefault(t=>t.Id==trade.Id);if(existing==trade)continue;if(existing is not null)features.Trades.Remove(existing);features.Trades.Add(trade);changed=true;}if(changed)SaveFeatures();
    }
    Dictionary<string,string> CurrentEquipment
    {
        get{if(store.Profile=="main")return features.Equipment;if(!features.CharacterEquipment.TryGetValue(store.Profile,out var layout))features.CharacterEquipment[store.Profile]=layout=new();return layout;}
    }
    void BuildDailyTools()
    {
        KeyDown+=async(_,e)=>{string? page=e.Control?e.KeyCode switch{Keys.I=>"Inventory",Keys.J=>"Trade Journal",Keys.B=>"Backups",Keys.L=>"Item Catalog",_=>null}:null;if(page is not null){ShowPage(page);e.SuppressKeyPress=true;}if(e.KeyCode==Keys.F5){e.SuppressKeyPress=true;await RefreshSources();}};
        var p=pages["Characters"];var bar=Toolbar();bar.Controls.Add(Button("Add character",()=>EditCharacter(null)));bar.Controls.Add(Button("Add storage",()=>EditCharacter(null,"Shared storage")));bar.Controls.Add(Button("Switch to selected",SwitchSelectedCharacter));bar.Controls.Add(Button("Name / mesos",()=>{if(characterGrid.SelectedRows.Count>0&&characterGrid.SelectedRows[0].Tag is CharacterProfile c)EditCharacter(c);}));characterGrid.CellDoubleClick+=(_,_)=>SwitchSelectedCharacter();
        characterNote.Text="Each character or storage profile has its own bag, watchlist, equipment layout, trade journal and targets.\nMesos balances are recorded manually. Trade-linked updates change stock only, not mesos balances.";p.Controls.Add(characterGrid);p.Controls.Add(characterNote);p.Controls.Add(bar);
        p=pages["Alert Inbox"];bar=Toolbar();bar.Controls.Add(Button("Mark all read",()=>{features.Inbox=features.Inbox.Select(i=>i.Profile==store.Profile?i with{Read=true}:i).ToList();SaveFeatures();RenderInbox();}));bar.Controls.Add(Button("Open selected source",()=>{if(inboxGrid.SelectedRows.Count>0&&inboxGrid.SelectedRows[0].Tag is InboxEntry i)Open(i.Url);}));var note=Info(65);note.Text="Alerts are retained here even when desktop notifications are disabled or missed. Latest 1,000 messages across profiles are kept.\nRead marks persist. Muted items and snoozed alerts stay quiet; each listing/price is recorded once.";p.Controls.Add(inboxGrid);p.Controls.Add(note);p.Controls.Add(bar);
        p=pages["Market Health"];bar=Toolbar();bar.Controls.Add(Button("Check status",RenderHealth));bar.Controls.Add(Button("Refresh eligible sources",async()=>await RefreshSources()));bar.Controls.Add(Button("Source settings",()=>ShowPage("Settings")));healthNote.Text="Source status and timing explain missing coverage. Cached prices are not proof of current availability.\nRefresh respects provider pacing and backoff; a browser shortcut is not a price feed.";p.Controls.Add(healthGrid);p.Controls.Add(healthNote);p.Controls.Add(bar);
        RenderDailyTools();
    }
    void RenderDailyTools()
    {
        SetColumns(characterGrid,["Character / storage","Mesos balance","Item stacks","Watchlist","Active"]);foreach(var c in store.Characters()){var items=store.ItemsFor(c.Id);int n=characterGrid.Rows.Add(c.Name,TextKey.Mesos(c.Mesos),items.Count(i=>i.Quantity>0),items.Count(i=>i.Watch),c.Id==store.Profile?"Current":"");characterGrid.Rows[n].Tag=c;}
        RenderInbox();RenderHealth();
    }
    void EditCharacter(CharacterProfile? existing,string defaultName="")
    {
        using var d=EntryDialog("Character / storage profile",out var fields);var name=new TextBox(){Text=existing?.Name??defaultName};var balance=Amount(existing?.Mesos??0);Field(fields,"Name",name);Field(fields,"Recorded mesos balance",balance);fields.Controls.Add(Button("Save profile",()=>{try{if(existing is null&&store.Characters().Count>=100)throw new InvalidOperationException("Maximum 100 profiles");store.SaveCharacter(new(existing?.Id??Guid.NewGuid().ToString(),name.Text,balance.Value));RenderDailyTools();RenderMerchantTools();d.Close();}catch(Exception e){MessageBox.Show(e.Message);}}));d.ShowDialog(this);
    }
    void SwitchSelectedCharacter(){if(characterGrid.SelectedRows.Count>0&&characterGrid.SelectedRows[0].Tag is CharacterProfile c)SwitchCharacter(c.Id);}
    void SwitchCharacter(string id)
    {
        store.SelectCharacter(id);bulkStacks.Clear();inventoryPage=0;removedInventory=null;selectedInventoryName="";compareLeft=null;compareRight=null;Render();RenderDailyTools();RenderComparison();title.Text=selected+" • "+store.Characters().Single(c=>c.Id==id).Name;footer.Text="Active profile changed. Saved bags and watchlists remain separate.";
    }
    void BulkImport(){using var dialog=CreateInventoryImport();dialog.ShowDialog(this);}
    Form CreateInventoryImport()
    {
        var d=new Form(){Text="Import into "+store.Characters().Single(c=>c.Id==store.Profile).Name,Size=new Size(820,680),StartPosition=FormStartPosition.CenterParent,BackColor=Back,Font=new Font("Segoe UI",features.TextSize)};var text=new TextBox(){Dock=DockStyle.Top,Height=175,Multiline=true,ScrollBars=ScrollBars.Both,PlaceholderText="Name,Quantity,Variant (optional)\r\nBlue Potion,50\r\nLeather,12"};var preview=Grid();var info=Info(70);info.Text="Paste CSV or tab-separated spreadsheet rows, or load a CSV/TSV file.\nCompare saved and imported quantities before applying. Unlisted items and watch preferences are kept.\nPrices are not imported. Each exact equipment variant is a separate stack.";var actions=Toolbar();var importBottom=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=48};List<SavedItem>? parsed=null;string? previewState=null;var apply=Button("Apply preview",()=>{});apply.Enabled=false;
        text.TextChanged+=(_,_)=>{parsed=null;apply.Enabled=false;};actions.Controls.Add(Button("Load CSV / TSV",()=>{using var f=new OpenFileDialog(){Filter="CSV or TSV|*.csv;*.tsv|Text|*.txt"};if(f.ShowDialog(d)!=DialogResult.OK)return;try{if(new FileInfo(f.FileName).Length>2_000_000)throw new FormatException("File exceeds 2 MB");text.Text=File.ReadAllText(f.FileName);}catch(Exception e){MessageBox.Show(e.Message);}}));
        actions.Controls.Add(Button("Preview",()=>{try{parsed=InventoryImport.Parse(text.Text);InventoryImport.Merge(store.Items(),parsed);previewState=JsonSerializer.Serialize(store.Items(),Json.Options);SetColumns(preview,["Item","Saved quantity","Imported quantity","Difference","Exact variant"]);var prior=store.Items().ToDictionary(ItemIdentity.Key);foreach(var i in parsed){long old=prior.GetValueOrDefault(ItemIdentity.Key(i))?.Quantity??0;preview.Rows.Add(i.Name,old,i.Quantity,$"{i.Quantity-old:+0;-0;0}",i.Variant);}var importedKeys=parsed.Select(ItemIdentity.Key).ToHashSet();foreach(var kept in prior.Values.Where(i=>!importedKeys.Contains(ItemIdentity.Key(i))))preview.Rows.Add(kept.Name,kept.Quantity,"Not listed", "Kept unchanged",kept.Variant);apply.Enabled=true;}catch(Exception e){parsed=null;apply.Enabled=false;MessageBox.Show(e.Message,"Import needs correction");}}));
        apply.Click+=(_,_)=>{if(parsed is null)return;try{if(previewState!=JsonSerializer.Serialize(store.Items(),Json.Options))throw new InvalidOperationException("Inventory changed since preview. Preview again before applying.");var merged=InventoryImport.Merge(store.Items(),parsed);new PersonalBackup(1,merged,new()).Validate();WriteRecoveryBackup("before-import");store.ReplaceItems(merged);Render();d.Close();}catch(Exception e){MessageBox.Show(e.Message,"Import failed");}};importBottom.Controls.Add(apply);d.Controls.Add(preview);d.Controls.Add(text);d.Controls.Add(info);d.Controls.Add(actions);d.Controls.Add(importBottom);return d;
    }
    void EditListingFilters()
    {
        using var d=EntryDialog("Filter listing results",out var fields);var equipment=new CheckBox(){Text="Equipment only",Checked=listingFilter.EquipmentOnly,Height=30};var fresh=new CheckBox(){Text="Recent available asking listings only",Checked=listingFilter.RecentOnly,Height=30};fields.Controls.Add(equipment);fields.Controls.Add(fresh);var maximum=Amount(listingFilter.MaxPrice);var stat=new ComboBox(){DropDownStyle=ComboBoxStyle.DropDown};stat.Items.Add("");stat.Items.AddRange(new[]{"watk","matk","str","dex","int","luk"}.Concat(observations.SelectMany(o=>CompanionFeatures.Stats(o.Variant).Keys)).Distinct().Cast<object>().ToArray());stat.Text=listingFilter.Stat;var minimum=Amount(listingFilter.MinStat,0,1_000_000);var slots=Amount(listingFilter.MinSlots,0,100);Field(fields,"Maximum unit price (0 = no limit)",maximum);Field(fields,"Stat key (empty = no stat filter; use keys shown in Compare)",stat);Field(fields,"Minimum selected stat",minimum);Field(fields,"Minimum remaining upgrade slots (0 = no filter)",slots);fields.Controls.Add(Button("Apply filters",()=>{listingFilter=new(equipment.Checked,fresh.Checked,maximum.Value,stat.Text.Trim(),minimum.Value,(int)slots.Value);RenderPrices();d.Close();}));fields.Controls.Add(Button("Clear filters",()=>{listingFilter=new();RenderPrices();d.Close();}));d.ShowDialog(this);
    }
    void AddInbox(string key,string titleText,string message,string url,Observation? listing=null,string itemName="")
    {
        if(features.Inbox.Any(i=>i.Key==key&&i.Profile==store.Profile))return;features.Inbox.Add(new(key,store.Profile,titleText,message,url,DateTimeOffset.UtcNow){ItemName=listing?.Name??itemName,Variant=listing?.Variant??"",ListingKey=listing?.Key??"",Price=listing?.UnitPrice??0});features.Inbox=features.Inbox.OrderByDescending(i=>i.At).Take(1000).ToList();SaveFeatures();RenderInbox();
    }
    void RecordDealInbox()
    {
        if(DateTimeOffset.UtcNow<settings.SnoozedUntil)return;var items=store.Items();
        foreach(var deal in deals.Take(20)){var item=items.FirstOrDefault(i=>TextKey.Search(i.Name)==TextKey.Search(deal.Listing.Name));if(item?.Muted==true||(settings.WatchOnly&&item?.Watch!=true))continue;var key="inbox-deal:"+AlertKey(deal);if(features.TargetNotifications.Contains(key))continue;AddInbox(key,deal.Listing.Name+" • potential deal",$"Ask {TextKey.Mesos(deal.Listing.UnitPrice)} • estimated value {TextKey.Mesos(deal.Estimate.Value)} • confidence {deal.Estimate.Confidence}/100",deal.Listing.Url,deal.Listing);features.TargetNotifications.Add(key);SaveFeatures();}
    }
    void RenderInbox(){SetColumns(inboxGrid,["When","Alert","Details","Read"]);foreach(var i in features.Inbox.Where(i=>i.Profile==store.Profile).OrderByDescending(i=>i.At)){int n=inboxGrid.Rows.Add(i.At.ToLocalTime().ToString("g"),i.Title,i.Message,i.Read?"Yes":"New");inboxGrid.Rows[n].Tag=i;}inboxGrid.Columns[2].FillWeight=230;inboxGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;inboxGrid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;}
    void RenderHealth()
    {
        SetColumns(healthGrid,["Source","Status","Last check","Next eligible","Cached / recent"]);var now=DateTimeOffset.UtcNow;
        foreach(var s in settings.Sources){var result=sourceResults.FirstOrDefault(r=>r.Provider==s.Id);var rows=observations.Where(o=>o.Provider==s.Id).ToList();var next=s.BlockedUntil>s.NextAllowed?s.BlockedUntil:s.NextAllowed;healthGrid.Rows.Add(s.Id,!s.Authorized?"Disabled / access not configured":s.BlockedUntil>now?"Backoff until "+s.BlockedUntil.ToLocalTime().ToString("g"):result?.Status??"Awaiting first check",s.LastChecked==default?"Not checked":s.LastChecked.ToLocalTime().ToString("g"),!s.Authorized?"Not scheduled":next<=now?"Due now":next.ToLocalTime().ToString("g"),$"{rows.Count} cached / {rows.Count(o=>o.Fresh(now,TimeSpan.FromHours(settings.FreshHours)))} recent");}healthGrid.Columns[1].FillWeight=220;
    }
    string BackupDirectory=>Path.Combine(dataDirectory,"automatic-backups");
    void WriteRecoveryBackup(string reason){File.WriteAllText(Path.Combine(dataDirectory,reason+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json"),JsonSerializer.Serialize(Snapshot(),Json.Options));}
    void AutomaticBackup(bool force=false)
    {
        if(!features.AutoBackups&&!force)return;if(!force&&DateTimeOffset.UtcNow-backupAttempt<TimeSpan.FromMinutes(10))return;backupAttempt=DateTimeOffset.UtcNow;
        try{Directory.CreateDirectory(BackupDirectory);var path=Path.Combine(BackupDirectory,"daily-"+DateTime.Now.ToString("yyyy-MM-dd")+".json");if(force||!File.Exists(path)){var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(Snapshot(),Json.Options));File.Move(temp,path,true);}foreach(var file in Directory.GetFiles(BackupDirectory,"daily-*.json").OrderByDescending(Path.GetFileName).Skip(7))File.Delete(file);backupStatus.Text="Daily backups enabled while the app runs • latest "+File.GetLastWriteTime(path).ToString("g")+"\nLast 7 daily copies kept. Use Restore personal backup to recover one.";}
        catch(Exception e){backupStatus.Text="Automatic backup failed: "+e.Message;}
    }
    void OpenBackupFolder(){Directory.CreateDirectory(BackupDirectory);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(BackupDirectory){UseShellExecute=true});}
    void ApplyTextSize()
    {
        void Apply(Control c){if(c is Label branding&&branding.Text.StartsWith("YOUR MARKET COMPANION"))return;if(c is DataGridView grid){grid.DefaultCellStyle.Font=new Font("Segoe UI",features.TextSize);grid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.True;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize;grid.RowTemplate.Height=Math.Max(44,features.TextSize*4);}else if(c is not ClassicTitle&&c is not ClassicBrand)c.Font=new Font("Segoe UI",features.TextSize,c.Font.Style);foreach(Control child in c.Controls)Apply(child);}
        foreach(Control child in Controls)Apply(child);foreach(var grid in new[]{priceGrid,dealsGrid,watchGrid,fmGrid,comparisonGrid,journalGrid,inboxGrid,healthGrid})grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
        foreach(var grid in new[]{priceGrid,dealsGrid,watchGrid,fmGrid,dashboardMarket,dashboardWatch,dashboardDeals}){grid.RowTemplate.MinimumHeight=44;foreach(DataGridViewRow row in grid.Rows)row.MinimumHeight=44;}
    }
    void SaveTradeEntry(TradeEntry entry,TradeEntry? previous,bool stock)
    {
        if(previous is not null&&store.HasStockChange(previous.Id))throw new InvalidOperationException("Undo the linked stock update before editing this trade");var original=features.Trades.ToList();
        if(stock)store.ApplyStockTrade(entry);
        try{if(previous is not null)features.Trades.Remove(previous);features.Trades.Add(entry);SaveFeatures();}catch{features.Trades=original;if(stock)store.UndoStockTrade(entry.Id);throw;}Render();
    }
    async Task SmokeDailyTools()
    {
        var snapshot=Snapshot();var profile=new CharacterProfile(Guid.NewGuid().ToString(),"Test storage",1234);store.SaveCharacter(profile);SwitchCharacter(profile.Id);if(store.Items().Count!=0)throw new Exception("Character inventory leaked");var imported=InventoryImport.Merge(store.Items(),InventoryImport.Parse("Name,Quantity\nBlue Potion,20"));store.ReplaceItems(imported);
        var trade=new TradeEntry(Guid.NewGuid().ToString(),"Blue Potion","",false,2,20,0,DateTimeOffset.UtcNow){Profile=profile.Id};SaveTradeEntry(trade,null,true);if(store.Items().Single().Quantity!=18)throw new Exception("Trade stock update failed");store.UndoStockTrade(trade.Id);if(store.Items().Single().Quantity!=20)throw new Exception("Trade undo failed");AddInbox("test-inbox","Isolated notification test","Not a market opportunity","https://kerning.gg/");AutomaticBackup(true);
        for(int i=1;i<=9;i++)File.WriteAllText(Path.Combine(BackupDirectory,"daily-"+DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd")+".json"),JsonSerializer.Serialize(Snapshot(),Json.Options));AutomaticBackup(true);if(Directory.GetFiles(BackupDirectory,"daily-*.json").Length!=7)throw new Exception("Daily backup rotation failed");
        RenderDailyTools();ShowPage("Characters");CapturePreview(Path.Combine(dataDirectory,"characters.png"));ShowPage("Alert Inbox");CapturePreview(Path.Combine(dataDirectory,"inbox.png"));ShowPage("Market Health");CapturePreview(Path.Combine(dataDirectory,"market-health.png"));
        features.TextSize=13;ApplyTextSize();ShowPage("Price Checker");CapturePreview(Path.Combine(dataDirectory,"large-text.png"));features.TextSize=9;ApplyTextSize();
        RestorePersonal(snapshot);ShowPage("Dashboard");await Task.CompletedTask;
    }
}
