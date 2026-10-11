using Market.Core;
using System.Text.Json;

namespace Market.App;
public sealed partial class MainForm
{
    CompanionFeatures features=new();
    readonly CheckBox menuSoundCheck=new(){Text="Play optional menu sounds",Width=300,Height=40};
    string FeaturePath=>Path.Combine(dataDirectory,"companion-features.json");
    readonly DataGridView ruleGrid=Grid(),journalGrid=Grid(),activityGrid=Grid();
    readonly Label journalTotal=Info(65),activityNote=Info(76);
    readonly Panel equipmentPanel=new(){Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(20),BackColor=Surface};
    void SaveFeatures()=>features.Save(FeaturePath);
    void BuildCompleteUpgrade()
    {
        var p=pages["Price Alerts"];var bar=Toolbar();bar.Controls.Add(Button("New target",()=>EditRule(null)));bar.Controls.Add(Button("Edit target",()=>{if(ruleGrid.SelectedRows.Count>0&&ruleGrid.SelectedRows[0].Tag is PriceRule r)EditRule(r);}));bar.Controls.Add(Button("Delete target",()=>{if(ruleGrid.SelectedRows.Count>0&&ruleGrid.SelectedRows[0].Tag is PriceRule r&&MessageBox.Show("Delete this price target?","Price alerts",MessageBoxButtons.YesNo)==DialogResult.Yes){features.Rules.Remove(r);SaveFeatures();RenderFeatures();}}));
        var note=Info(76);note.Text="Targets notify when a recent available listing is at or below your price. Equipment requires an exact variant.\nOnly enabled sources qualify; the last retrieval must be within 30 minutes. Global notification, mute and snooze settings apply. Targets are thresholds, not estimated market values.";p.Controls.Add(ruleGrid);p.Controls.Add(note);p.Controls.Add(bar);
        p=pages["Trade Journal"];bar=Toolbar();bar.Controls.Add(Button("Record trade",()=>EditTrade(null)));bar.Controls.Add(Button("Edit selected",()=>{if(journalGrid.SelectedRows.Count>0&&journalGrid.SelectedRows[0].Tag is TradeEntry t)EditTrade(t);}));bar.Controls.Add(Button("Delete selected",()=>{if(journalGrid.SelectedRows.Count>0&&journalGrid.SelectedRows[0].Tag is TradeEntry t&&MessageBox.Show("Delete this recorded trade? Profit will be recalculated.","Trade journal",MessageBoxButtons.YesNo)==DialogResult.Yes){if(store.HasStockChange(t.Id)){MessageBox.Show("Undo the linked stock update before deleting this trade.");return;}features.Trades.Remove(t);SaveFeatures();RenderFeatures();}}));bar.Controls.Add(Button("Undo stock update",()=>{if(journalGrid.SelectedRows.Count>0&&journalGrid.SelectedRows[0].Tag is TradeEntry t){try{store.UndoStockTrade(t.Id);Render();}catch(Exception e){MessageBox.Show(e.Message);}}}));p.Controls.Add(journalGrid);p.Controls.Add(journalTotal);p.Controls.Add(bar);
        p=pages["Market Activity"];bar=Toolbar();bar.Controls.Add(Button("Update view",RenderActivity));activityNote.Text="Seven days of recorded asking-price history, grouped by exact item variant and UTC day. Each compared day needs at least 3 independent sellers.\nChanges compare sample medians, not completed-sale prices or guaranteed market-wide trends. Missing history is never filled in.";p.Controls.Add(activityGrid);p.Controls.Add(activityNote);p.Controls.Add(bar);
        p=pages["Equipment"];var equipmentNote=Info(65);equipmentNote.Text="Your planned equipment layout • choose from owned equipment. Click a slot to assign or clear it.\nThis layout does not change the game. Stat comparisons use recorded listing variants, not guessed base stats.";p.Controls.Add(equipmentPanel);p.Controls.Add(equipmentNote);
        p=pages["Backups"];var controls=new FlowLayoutPanel(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(15)};
        controls.Controls.Add(new Label(){Text="Protect your saved items",Font=new Font("Tahoma",16,FontStyle.Bold),AutoSize=true,Margin=new Padding(3,12,3,18)});
        controls.Controls.Add(new Label(){Text="Export all character inventories, balances, watchlists, trades, targets, inbox and interface preferences.\nRestore replaces that personal data after validation and saves a recovery copy first.\nMarket cache, price history and source/notification settings are kept separately and are not replaced.",Width=780,Height=94});
        controls.Controls.Add(Button("Export personal backup",ExportPersonal));controls.Controls.Add(Button("Restore personal backup",ImportPersonal));
        var automatic=new CheckBox(){Text="Automatic daily backups while the app runs (keep 7 copies)",Checked=features.AutoBackups,Width=680,Height=38};automatic.CheckedChanged+=(_,_)=>{features.AutoBackups=automatic.Checked;SaveFeatures();if(automatic.Checked)AutomaticBackup();};controls.Controls.Add(automatic);controls.Controls.Add(Button("Back up now",()=>AutomaticBackup(true)));controls.Controls.Add(Button("Open backup folder",OpenBackupFolder));backupStatus.Width=780;controls.Controls.Add(backupStatus);p.Controls.Add(controls);RenderFeatures();
    }
    static NumericUpDown Amount(decimal value,decimal min=0,decimal max=1_000_000_000_000m)=>new(){Minimum=min,Maximum=max,Value=Math.Clamp(value,min,max),Width=370,ThousandsSeparator=true,DecimalPlaces=0};
    Form EntryDialog(string title,out FlowLayoutPanel fields)
    {
        var d=new Form(){Text=title,Size=new Size(650,610),StartPosition=FormStartPosition.CenterParent,BackColor=Back,ForeColor=TextColor,Font=new Font("Tahoma",features.TextSize),MinimizeBox=false,MaximizeBox=false};fields=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(14)};d.Controls.Add(fields);return d;
    }
    static void Field(FlowLayoutPanel p,string label,Control input){p.Controls.Add(new Label(){Text=label,AutoSize=true,Margin=new Padding(3,8,3,2)});input.Width=540;p.Controls.Add(input);}
    void EditRule(PriceRule? existing,object? item=null)
    {
        using var d=EntryDialog("Custom price target",out var fields);var name=new TextBox(){Text=existing?.Name??ItemName(item)};var variant=new ComboBox(){Text=existing?.Variant??Listing(item)?.Variant??"",DropDownStyle=ComboBoxStyle.DropDown};
        void Variants(){var text=variant.Text;variant.Items.Clear();variant.Items.Add("");variant.Items.AddRange(observations.Where(o=>TextKey.Search(o.Name)==TextKey.Search(name.Text)&&o.Equipment&&o.VariantComplete).Select(o=>o.Variant).Distinct().Cast<object>().ToArray());variant.Text=text;}name.TextChanged+=(_,_)=>Variants();Variants();
        var price=Amount(existing?.Maximum??1,1);var enabled=new CheckBox(){Text="Target enabled",Checked=existing?.Enabled??true,Height=32};
        Field(fields,"Item name",name);Field(fields,"Exact equipment variant (leave empty for stackable items)",variant);Field(fields,"Notify at or below this unit price (mesos)",price);fields.Controls.Add(enabled);
        fields.Controls.Add(Button("Save target",()=>{try{var rule=new PriceRule(existing?.Id??Guid.NewGuid().ToString(),name.Text.Trim(),variant.Text.Trim(),price.Value,enabled.Checked){Profile=store.Profile};var candidate=new CompanionFeatures(){Rules=[rule]};candidate.Validate();if(existing is not null)features.Rules.Remove(existing);features.Rules.Add(rule);SaveFeatures();WatchObject(new SavedItem(rule.Name,0,true,false){Variant=rule.Variant});RenderFeatures();d.Close();}catch(Exception e){MessageBox.Show(e.Message,"Cannot save target");}}));d.ShowDialog(this);
    }
    void EditTrade(TradeEntry? existing,bool? buy=null)
    {
        if(existing is not null&&store.HasStockChange(existing.Id)){MessageBox.Show("Undo the linked stock update before editing this trade.");return;}
        if(File.Exists(DraftPath)){if(MessageBox.Show("An unfinished trade is saved. Resume it now? Choose No to keep it and cancel opening another trade.","Saved draft",MessageBoxButtons.YesNo)==DialogResult.Yes)ResumeTradeDraft();return;}
        using var d=CreateTradeEditor(existing,buy);d.ShowDialog(this);
    }
    Form CreateTradeEditor(TradeEntry? existing,bool? buy=null,TradeDraft? recovery=null)
    {
        var d=EntryDialog("Record your completed trade • draft saved as you type",out var fields);
        var initial=recovery?.Entry??existing;string id=initial?.Id??Guid.NewGuid().ToString(),profile=initial?.Profile??store.Profile;bool finished=false;
        var name=new TextBox(){Text=initial?.Name??search.Text,MaxLength=250};var variant=new TextBox(){Text=initial?.Variant??"",MaxLength=4000};var side=new ComboBox(){DropDownStyle=ComboBoxStyle.DropDownList};side.Items.AddRange(["Purchase","Sale"]);side.SelectedIndex=initial?.Buy==false||initial is null&&buy==false?1:0;
        var qty=Amount(initial?.Quantity??1,1,1_000_000_000);var price=Amount(initial?.UnitPrice??1,1);var fees=Amount(initial?.Fees??0);var date=new DateTimePicker(){Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd HH:mm:ss",Value=initial?.At.LocalDateTime??DateTime.Now};
        Field(fields,"Item name",name);Field(fields,"Exact variant (same text for matching purchases and sales)",variant);Field(fields,"Trade type",side);Field(fields,"Quantity",qty);Field(fields,"Price per unit (mesos)",price);Field(fields,"Total fees for this trade (mesos)",fees);Field(fields,"Completed at (local time)",date);
        var stock=new CheckBox(){Text="Also update this character’s inventory quantity (mesos unchanged)",Width=540,Height=42,Checked=recovery?.UpdateStock??false};fields.Controls.Add(stock);var status=new Label(){Width=540,Height=45,Text="Closing this form keeps your draft. Use Discard draft to remove it."};fields.Controls.Add(status);
        TradeEntry Entry()=>new(id,name.Text.Trim(),variant.Text.Trim(),side.SelectedIndex==0,(long)qty.Value,price.Value,fees.Value,new DateTimeOffset(date.Value)){Profile=profile};
        void Persist(){if(finished)return;try{new TradeDraft(Entry(),existing,stock.Checked,DateTimeOffset.UtcNow).Save(DraftPath);status.Text="Draft saved locally at "+DateTime.Now.ToString("T")+". Closing keeps it; no trade recorded yet.";}catch(Exception e){status.Text="Draft could not be saved: "+e.Message;}}
        foreach(var c in new Control[]{name,variant,side,qty,price,fees,date,stock})switch(c){case TextBox t:t.TextChanged+=(_,_)=>Persist();break;case ComboBox t:t.SelectedIndexChanged+=(_,_)=>Persist();break;case NumericUpDown t:t.ValueChanged+=(_,_)=>Persist();break;case DateTimePicker t:t.ValueChanged+=(_,_)=>Persist();break;case CheckBox t:t.CheckedChanged+=(_,_)=>Persist();break;}
        fields.Controls.Add(Button("Save completed trade",()=>{try{var entry=Entry();new CompanionFeatures(){Trades=[entry]}.Validate();var draft=new TradeDraft(entry,existing,stock.Checked,DateTimeOffset.UtcNow);if(draft.AlreadyRecorded(features.Trades)){finished=true;File.Delete(DraftPath);d.Close();return;}if(!draft.CanResume(features.Trades))throw new InvalidOperationException("The original trade has changed. It cannot be overwritten by this draft.");if(features.Trades.Any(t=>PolishTools.Duplicate(t,entry))&&MessageBox.Show("A very similar trade was recorded within five minutes. Record this as another trade?","Possible duplicate",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;if(!ReviewTrade(entry,stock.Checked))return;draft.Save(DraftPath);SaveTradeEntry(entry,existing,stock.Checked);finished=true;File.Delete(DraftPath);d.Close();}catch(Exception e){MessageBox.Show(e.Message,"Cannot save trade");}}));
        fields.Controls.Add(Button("Save as template",()=>SaveTemplate(Entry())));
        fields.Controls.Add(Button("Discard draft",()=>{if(MessageBox.Show("Discard this unfinished trade? Recorded trades stay unchanged.","Discard draft",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;try{File.Delete(DraftPath);finished=true;d.Close();}catch(Exception e){MessageBox.Show(e.Message);}}));
        d.FormClosing+=(_,_)=>{if(!finished)Persist();};return d;
    }
    void CheckPriceTargets()
    {
        if(DateTimeOffset.UtcNow<settings.SnoozedUntil)return;
        foreach(var rule in features.Rules.Where(r=>r.Enabled&&r.Profile==store.Profile))
        {
            if(store.Items().Any(i=>i.Muted&&TextKey.Search(i.Name)==TextKey.Search(rule.Name)))continue;
            var row=EligibleObservations().Where(o=>CompanionFeatures.Matches(rule,o,DateTimeOffset.UtcNow,settings.FreshHours)&&!features.TargetNotifications.Contains(CompanionFeatures.TargetKey(rule,o))).OrderBy(o=>o.UnitPrice).FirstOrDefault();if(row is null)continue;
            tray.BalloonTipTitle=rule.Name+" • price target reached";tray.BalloonTipText=$"Ask {TextKey.Mesos(row.UnitPrice)} ≤ target {TextKey.Mesos(rule.Maximum)} mesos\n{row.Provider} • asking listing, not a guaranteed trade";notificationUrl=row.Url;tray.Sound=settings.Sound;
            AddInbox("target:"+CompanionFeatures.TargetKey(rule,row),tray.BalloonTipTitle,tray.BalloonTipText,row.Url,row);features.TargetNotifications.Add(CompanionFeatures.TargetKey(rule,row));SaveFeatures();break;
        }
    }
    void RenderFeatures()
    {
        SetColumns(ruleGrid,["Item","Target ≤","Variant","State"]);foreach(var r in features.Rules.Where(r=>r.Profile==store.Profile)){int n=ruleGrid.Rows.Add(r.Name,TextKey.Mesos(r.Maximum),r.Variant.Length==0?"Stackable only":r.Variant,r.Enabled?"Enabled":"Paused");ruleGrid.Rows[n].Tag=r;}
        var outcomes=CompanionFeatures.Outcomes(features.Trades.Where(t=>t.Profile==store.Profile)).ToDictionary(r=>r.Id);SetColumns(journalGrid,["Date","Item / variant","Type","Quantity","Unit price","Fees","Matched profit","Unmatched sold"]);
        foreach(var t in features.Trades.Where(t=>t.Profile==store.Profile).OrderByDescending(t=>t.At)){var outcome=outcomes.GetValueOrDefault(t.Id);int n=journalGrid.Rows.Add(t.At.ToLocalTime().ToString("g"),t.Name+(t.Variant.Length>0?" • "+t.Variant:""),t.Buy?"Purchase":"Sale",t.Quantity,TextKey.Mesos(t.UnitPrice),TextKey.Mesos(t.Fees),t.Buy?"—":TextKey.Mesos(outcome?.MatchedProfit),outcome?.UnmatchedUnits??0);journalGrid.Rows[n].Tag=t;}
        journalGrid.Columns[0].FillWeight=155;journalGrid.Columns[1].FillWeight=180;journalGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;journalGrid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
        journalTotal.Text=$"Recorded matched profit: {TextKey.Mesos(outcomes.Values.Sum(o=>o.MatchedProfit))} mesos • {outcomes.Values.Sum(o=>o.UnmatchedUnits)} sold units without recorded purchase cost\nFIFO by exact name/variant, including allocated purchase and sale fees. Unmatched units excluded. Stock changes only when explicitly selected; prices are never added to market evidence.";
        RenderActivity();RenderEquipment();
    }
    void RenderActivity()
    {
        SetColumns(activityGrid,["Item / exact variant","Earlier UTC day","Earlier median ask","Latest UTC day","Latest median ask","Change","Samples"]);
        var active=store.History().Where(o=>settings.Sources.Any(s=>s.Id==o.Provider&&s.Authorized));foreach(var r in CompanionFeatures.Activity(active,DateTimeOffset.UtcNow))activityGrid.Rows.Add(r.Name+(r.Variant.Length>0?" • "+r.Variant:""),r.First.ToString("yyyy-MM-dd"),TextKey.Mesos(r.EarlierMedian),r.Last.ToString("yyyy-MM-dd"),TextKey.Mesos(r.LatestMedian),$"{(r.LatestMedian/r.EarlierMedian-1)*100:+0.0;-0.0;0}%",$"{r.EarlierCount} → {r.LatestCount}");
        if(activityGrid.Rows.Count==0)activityGrid.Rows.Add("Not enough comparable history yet","—","—","—","—","—","3 sellers on each of 2 days required");
    }
    void RenderEquipment()
    {
        equipmentPanel.SuspendLayout();while(equipmentPanel.Controls.Count>0)equipmentPanel.Controls[0].Dispose();
        foreach(var slotName in CompanionFeatures.EquipmentSlots)
        {
            var owned=ItemIdentity.Resolve(store.Items().Where(i=>i.Quantity>0),CurrentEquipment.GetValueOrDefault(slotName)??"");var tile=new Panel(){Width=140,Height=115,Margin=new Padding(10)};var label=new ClassicTitle(){Text=slotName,Dock=DockStyle.Top,Height=27,Font=new Font("Tahoma",9,FontStyle.Bold)};var slot=new InventorySlot(){Item=owned,ItemIcon=owned is null?null:icons.Peek(IconId(owned)),Left=38,Top=32,AccessibleName=slotName+": "+(owned?.Name??"Empty")};slot.Click+=(_,_)=>AssignEquipment(slotName);tile.Controls.Add(slot);tile.Controls.Add(label);tile.Controls.Add(new Label(){Text=owned?.Name??"Choose equipment",Top=94,Width=140,Height=20,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true});var positions=new Dictionary<string,Point>{{"Hat",new(2,0)},{"Face",new(2,1)},{"Eye",new(1,1)},{"Earrings",new(3,1)},{"Pendant",new(3,2)},{"Cape",new(0,2)},{"Top / Overall",new(2,2)},{"Gloves",new(4,2)},{"Weapon",new(0,3)},{"Shield",new(4,3)},{"Bottom",new(2,3)},{"Shoes",new(2,4)},{"Ring",new(3,3)}};var position=positions[slotName];tile.Location=new Point(20+position.X*150,10+position.Y*120);equipmentPanel.Controls.Add(tile);if(owned is not null)_=LoadSlotIcon(slot,owned);
        }
        equipmentPanel.ResumeLayout();
    }
    void AssignEquipment(string slot)
    {
        using var d=EntryDialog("Assign "+slot,out var fields);var choices=new ComboBox(){DropDownStyle=ComboBoxStyle.DropDownList};choices.Items.Add("Empty slot");foreach(var item in store.Items().Where(i=>i.Quantity>0&&InventoryCategory(i)=="EQUIP"))choices.Items.Add(item);choices.SelectedIndex=0;Field(fields,"Owned equipment (choose the appropriate item for this slot)",choices);fields.Controls.Add(Button("Save layout",()=>{if(choices.SelectedIndex==0)CurrentEquipment.Remove(slot);else CurrentEquipment[slot]=ItemIdentity.Key((SavedItem)choices.SelectedItem!);SaveFeatures();RenderEquipment();d.Close();}));d.ShowDialog(this);
    }
    PersonalBackup Snapshot()=>new(3,store.Items(),CompanionFeatures.Read(JsonSerializer.Serialize(features,Json.Options))){Characters=store.AllInventories(),ActiveCharacter=store.Profile,Transfers=store.Transfers()};
    void ExportPersonal()
    {
        using var dialog=new SaveFileDialog(){Filter="Personal backup (*.json)|*.json",FileName="MapleMarket-personal-backup.json"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(Snapshot(),Json.Options));footer.Text="Personal backup exported.";}catch(Exception e){MessageBox.Show(e.Message,"Backup failed");}
    }
    void RestorePersonal(PersonalBackup backup)
    {
        backup.Validate();var previous=Snapshot();File.WriteAllText(Path.Combine(dataDirectory,"personal-before-restore-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json"),JsonSerializer.Serialize(previous,Json.Options));
        try{store.ReplaceCharacters(backup.Version>=2?backup.Characters!:[new(new("main","Main character",0),backup.Items)],backup.Version>=2?backup.ActiveCharacter:"main",backup.Transfers);features=backup.Features;SaveFeatures();}catch{store.ReplaceCharacters(previous.Characters!,previous.ActiveCharacter,previous.Transfers);features=previous.Features;SaveFeatures();throw;}
        removedInventory=null;icons.Offline=Offline;offlineToggle.Checked=Offline;UpdateConnectionStatus();Render();RenderFeatures();ApplyTextSize();ApplyTownTheme();ApplyDashboardPanels();ShowPage(selected);
    }
    void ImportPersonal()
    {
        using var dialog=new OpenFileDialog(){Filter="Personal backup (*.json)|*.json",InitialDirectory=BackupDirectory};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{if(new FileInfo(dialog.FileName).Length>20*1024*1024)throw new FormatException("Backup exceeds 20 MB");var backup=JsonSerializer.Deserialize<PersonalBackup>(File.ReadAllText(dialog.FileName),Json.Options)??throw new FormatException("Empty backup");backup.Validate();using(var preview=CreateBackupPreview(backup)){if(preview.ShowDialog(this)!=DialogResult.OK)return;}RestorePersonal(backup);footer.Text="Personal backup restored. Recovery copy saved in the app data folder.";}catch(Exception e){MessageBox.Show(e.Message,"Restore failed");}
    }
    void AppendEquipmentComparison()
    {
        var a=Listing(compareLeft);var b=Listing(compareRight);if(a?.Equipment!=true&&b?.Equipment!=true)return;
        var left=CompanionFeatures.Stats(a?.Variant);var right=CompanionFeatures.Stats(b?.Variant);comparisonGrid.Rows.Add("Variant match",a is not null&&b is not null&&a.VariantComplete&&b.VariantComplete&&a.MatchKey==b.MatchKey?"Exact comparable variant":"Different or incomplete variants","Compare stats; do not pool these prices");
        foreach(var key in left.Keys.Union(right.Keys,StringComparer.OrdinalIgnoreCase).Order())comparisonGrid.Rows.Add(key,left.TryGetValue(key,out var l)?l.ToString():"Unknown",right.TryGetValue(key,out var r)?r.ToString():"Unknown");
    }
    async Task SmokeCompleteUpgrade()
    {
        var before=Snapshot();features.Rules.Add(new(Guid.NewGuid().ToString(),"Blue Potion","",25));features.Trades.Add(new(Guid.NewGuid().ToString(),"Blue Potion","",true,10,10,0,DateTimeOffset.UtcNow.AddDays(-1)));features.Trades.Add(new(Guid.NewGuid().ToString(),"Blue Potion","",false,5,20,5,DateTimeOffset.UtcNow));SaveFeatures();RenderFeatures();
        var backup=Snapshot();var read=JsonSerializer.Deserialize<PersonalBackup>(JsonSerializer.Serialize(backup,Json.Options),Json.Options)!;read.Validate();features.Trades.Clear();RestorePersonal(read);if(features.Trades.Count!=backup.Features.Trades.Count)throw new Exception("Personal backup restore lost journal");
        ShowPage("Trade Journal");CapturePreview(Path.Combine(dataDirectory,"journal.png"));ShowPage("Equipment");CapturePreview(Path.Combine(dataDirectory,"equipment.png"));ShowPage("Price Alerts");CapturePreview(Path.Combine(dataDirectory,"price-targets.png"));
        RestorePersonal(before);ShowPage("Dashboard");await Task.CompletedTask;
    }
}
