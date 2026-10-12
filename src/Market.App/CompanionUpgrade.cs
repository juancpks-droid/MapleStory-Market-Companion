using Market.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace Market.App;

public sealed partial class MainForm
{
    CancellationTokenSource? refreshStop;
    readonly Label connectionStatus=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
    readonly CheckBox offlineToggle=new(){Text="Work offline",Dock=DockStyle.Right,Width=115};
    string DraftPath=>Path.Combine(dataDirectory,"trade-draft.json");
    bool Offline=>features.Offline;
    void BuildCompanionUpgrade()
    {
        var strip=new Panel(){Dock=DockStyle.Bottom,Height=30,Padding=new Padding(18,0,8,0),BackColor=Surface};strip.Controls.Add(connectionStatus);strip.Controls.Add(offlineToggle);footer.Height=66;footer.Padding=new Padding(18,36,0,0);strip.Dock=DockStyle.Top;footer.Controls.Add(strip);
        offlineToggle.Checked=Offline;icons.Offline=Offline;offlineToggle.CheckedChanged+=(_,_)=>{features.Offline=offlineToggle.Checked;icons.Offline=Offline;if(Offline){pendingSearch=false;refreshStop?.Cancel();}SaveFeatures();UpdateConnectionStatus();RenderDashboard();};
        var journalBar=pages["Trade Journal"].Controls.OfType<FlowLayoutPanel>().First();journalBar.Controls.Add(Button("Resume draft",ResumeTradeDraft));journalBar.Height=92;journalBar.WrapContents=true;journalBar.Controls.Add(Button("Discard saved draft",()=>{if(!File.Exists(DraftPath)){MessageBox.Show("No unfinished draft is saved.");return;}if(MessageBox.Show("Discard the unfinished draft? Recorded trades are unchanged. A recovery copy will be kept.","Discard draft",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;try{File.Move(DraftPath,Path.Combine(dataDirectory,"discarded-draft-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json"));}catch(Exception e){MessageBox.Show(e.Message);}}));
        var sessionBar=pages["Trading Sessions"].Controls.OfType<FlowLayoutPanel>().First();sessionBar.Controls.Add(Button("Session recap",()=>{if(SelectedMerchant(sessionGrid) is MerchantSession s)ShowSessionRecap(s);else footer.Text="Select a session to see its recap.";}));
        sessionGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&sessionGrid.Rows[e.RowIndex].Tag is MerchantSession s)ShowSessionRecap(s);};
        var dealBar=pages["Deal Finder"].Controls.OfType<FlowLayoutPanel>().First();dealBar.Controls.Add(Button("Explain deal",()=>{if(dealsGrid.SelectedRows.Count>0&&dealsGrid.SelectedRows[0].Tag is Deal d)ShowItemDetails(d);}));
        KeyDown+=(_,e)=>{if(e.Control&&e.Shift&&e.KeyCode==Keys.S){ScreenshotInventory();e.SuppressKeyPress=true;}if(e.Control&&e.KeyCode==Keys.R){ResumeTradeDraft();e.SuppressKeyPress=true;}if(e.Control&&e.KeyCode==Keys.O){offlineToggle.Checked=!offlineToggle.Checked;e.SuppressKeyPress=true;}};
        inventoryTip.OwnerDraw=true;inventoryTip.Popup+=(_,e)=>{var text=inventoryTip.GetToolTip(e.AssociatedControl);e.ToolTipSize=new Size(330,Math.Min(430,35+TextRenderer.MeasureText(text,Font,new Size(305,0),TextFormatFlags.WordBreak).Height));};
        inventoryTip.Draw+=(_,e)=>{using var b=new SolidBrush(Color.FromArgb(32,42,61));e.Graphics.FillRectangle(b,e.Bounds);using var p=new Pen(Color.Goldenrod,2);e.Graphics.DrawRectangle(p,1,1,e.Bounds.Width-3,e.Bounds.Height-3);TextRenderer.DrawText(e.Graphics,e.ToolTipText,Font,new Rectangle(12,12,e.Bounds.Width-24,e.Bounds.Height-24),Color.FromArgb(255,240,190),TextFormatFlags.WordBreak);};
        UpdateConnectionStatus();
    }
    void UpdateConnectionStatus()
    {
        var latest=observations.Select(o=>o.RetrievedAt).DefaultIfEmpty().Max();var age=latest==default?"no saved listings":$"newest cached listing: {Math.Max(0,(int)(DateTimeOffset.UtcNow-latest).TotalMinutes):N0} min ago";
        bool failed=sourceResults.Any(r=>r.Status.StartsWith("Unavailable",StringComparison.OrdinalIgnoreCase));
        connectionStatus.Text=(Offline?"OFFLINE • Fetches and desktop market alerts paused":failed?"SOURCE UNAVAILABLE • Working from cache":"Online checks enabled • prices can still be cached")+" • "+age;
        connectionStatus.AccessibleName=connectionStatus.Text;
    }
    string CommandCenterText(object item)
    {
        string name=ItemName(item),variant=(item as SavedItem)?.Variant??Listing(item)?.Variant??"";
        bool exact=item is SavedItem||Listing(item) is not null;
        var stocks=store.Items().Where(i=>TextKey.Search(i.Name)==TextKey.Search(name)&&(!exact||i.Variant==variant)).ToList();
        var trades=features.Trades.Where(t=>t.Profile==store.Profile&&TextKey.Search(t.Name)==TextKey.Search(name)&&(!exact||t.Variant==variant)).OrderByDescending(t=>t.At).Take(8);
        var notes=features.Memos.Where(m=>m.Profile==store.Profile&&TextKey.Search(m.Name)==TextKey.Search(name));
        return "PERSONAL NOTES\n"+(notes.Any()?string.Join("\n",notes.Select(m=>m.Tag+": "+m.Notes)):"No notes saved.");
    }
    TabPage TextTab(string title,string text)
    {var p=new TabPage(title){BackColor=Surface};p.Controls.Add(new RichTextBox(){Dock=DockStyle.Fill,Text=text,ReadOnly=true,BackColor=Surface,ForeColor=TextColor,BorderStyle=BorderStyle.None,Font=new Font("Tahoma",10),Padding=new Padding(12)});return p;}
    void AddCommandCenter(Form dialog,Control frame,Control evidence,object item)
    {
        dialog.Text=ItemName(item)+" • Item command center";dialog.Size=new Size(840,700);
        var tabs=new MarketTabs(){Dock=DockStyle.Fill};frame.Controls.Remove(evidence);var summary=TextTab("Item notes",CommandCenterText(item));tabs.TabPages.Add(summary);var prices=new TabPage("Price evidence");prices.Controls.Add(evidence);tabs.TabPages.Add(prices);
        if(item is Deal deal)tabs.TabPages.Add(DealTab(deal));frame.Controls.Add(tabs);
        dialog.Activated+=(_,_)=>{if(!summary.IsDisposed)summary.Controls.OfType<RichTextBox>().Single().Text=CommandCenterText(item);};
    }
    TabPage DealTab(Deal deal)
    {
        var p=new TabPage("Why this deal?"){BackColor=Surface};var row=deal.Listing;var rows=ValuationEngine.Deduplicate(EligibleObservations().Where(o=>o.Key!=row.Key&&o.MatchKey==row.MatchKey&&o.Fresh(DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours))&&!o.PriceApproximate&&(!o.Equipment||o.VariantComplete)&&((o.Kind==EvidenceKind.Asking&&o.Available)||o.Kind is EvidenceKind.ReportedSale or EvidenceKind.ConfirmedSale))).ToList();
        var text=new RichTextBox(){Dock=DockStyle.Fill,ReadOnly=true,BackColor=Surface,BorderStyle=BorderStyle.None,Text=$"WHY IT WAS FLAGGED\nAsk {TextKey.Mesos(row.UnitPrice)} • estimate {TextKey.Mesos(deal.Estimate.Value)} • {deal.Discount:N1}% below estimate\nConfidence {deal.Estimate.Confidence}/100 (required {settings.Confidence}) • minimum profit {TextKey.Mesos(settings.MinimumProfit)}\n{deal.Estimate.Basis} • {deal.Estimate.Count} observations in estimate\n\nCHECK BEFORE TRADING\n{ReliabilityTools.EvidenceStatus(row,settings.Sources.Any(s=>s.Id==row.Provider&&s.Authorized),DateTimeOffset.UtcNow,settings.FreshHours)}\nSource posted: {row.ObservedAt?.LocalDateTime.ToString("g")??"unknown"}\nRetrieved: {row.RetrievedAt.LocalDateTime:g}\nExact variant: {row.Variant}\n\nCOMPARABLE EVIDENCE • target excluded; estimate may filter outliers\n"+string.Join("\n",rows.OrderBy(o=>o.UnitPrice).Take(20).Select(o=>$"{o.Seller} • {TextKey.Mesos(o.UnitPrice)} • {o.Kind} • posted {o.ObservedAt?.LocalDateTime:g}"))+"\n\nAsking prices are not completed sales. Availability and resale price can change. Fees below are your assumptions."};
        var fields=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=185,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};var qty=Amount(1,1,1_000_000_000);var sell=Amount(deal.Estimate.Normal);var buyFee=Amount(0);var sellFee=Amount(0);var result=new Label(){AutoSize=true};
        void Row(string label,Control c){var line=new FlowLayoutPanel(){Width=720,Height=29};line.Controls.Add(new Label(){Text=label,Width=195,Padding=new Padding(0,5,0,0)});c.Width=190;line.Controls.Add(c);fields.Controls.Add(line);}Row("Quantity",qty);Row("Expected resale / unit",sell);Row("Total purchase fees",buyFee);Row("Total sale fees",sellFee);fields.Controls.Add(result);
        void Update()=>result.Text=$"Estimated net: {TextKey.Mesos(CompanionUpgrade.NetProfit(row.UnitPrice,sell.Value,(long)qty.Value,buyFee.Value,sellFee.Value))} mesos • assumption, not a guaranteed return";
        foreach(var n in new[]{qty,sell,buyFee,sellFee})n.ValueChanged+=(_,_)=>Update();Update();p.Controls.Add(text);p.Controls.Add(fields);return p;
    }
    string SessionRecapText(MerchantSession session)
    {
        var t=CompanionUpgrade.SessionRecap(session,features.Trades,DateTimeOffset.UtcNow);var character=store.Characters().Single(c=>c.Id==session.Profile);var stock=store.AllInventories().Single(c=>c.Character.Id==session.Profile).Items.Where(i=>i.Quantity>0);
        return $"{session.Name}\n{session.Start.LocalDateTime:g} — {session.End?.LocalDateTime.ToString("g")??"still running"}\n\n{t.TradeCount} recorded trades\nPurchases: {TextKey.Mesos(t.Purchases)}\nSales: {TextKey.Mesos(t.Sales)}\nFees: {TextKey.Mesos(t.Fees)}\nJournal net cash flow: {TextKey.Mesos(t.NetCashFlow)} mesos\nRealized matched FIFO profit: {TextKey.Mesos(t.MatchedProfit)} mesos\nUnmatched sold units excluded from profit: {t.Unmatched}\n\nRECORDED MESOS BALANCE\nCurrent: {TextKey.Mesos(character.Mesos)}\n"+(session.StartMesos is null?"Start balance unavailable for this older session.":$"Start: {TextKey.Mesos(session.StartMesos)}\n{(session.EndMesos is null?"Current":"End")} change: {TextKey.Mesos((session.EndMesos??character.Mesos)-session.StartMesos)}")+"\nJournal cash flow and manually recorded mesos balance are separate.\n\nREMAINING STOCK NOW • not a historical end-of-session snapshot\n"+string.Join("\n",stock.Select(i=>$"{i.Name} • {i.Quantity:N0} • {i.Variant}"));
    }
    void ShowSessionRecap(MerchantSession session)
    {
        using var d=new Form(){Text="Trading session recap",Size=new Size(780,650),StartPosition=FormStartPosition.CenterParent,Font=Font};var text=SessionRecapText(session);var body=new RichTextBox(){Text=text,Dock=DockStyle.Fill,ReadOnly=true,BackColor=Surface};var bar=Toolbar();bar.Controls.Add(Button("Export recap",()=>{using var save=new SaveFileDialog(){Filter="Text recap (*.txt)|*.txt",FileName="trading-session-recap.txt"};if(save.ShowDialog(d)==DialogResult.OK)try{File.WriteAllText(save.FileName,text);}catch(Exception e){MessageBox.Show(e.Message,"Could not export");}}));d.Controls.Add(body);d.Controls.Add(bar);d.ShowDialog(this);
    }
    async Task<string> ReadScreenshot(string path)
    {
        if(new FileInfo(path).Length>20*1024*1024)throw new IOException("Choose an image smaller than 20 MB.");
        var engine=OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"))??OcrEngine.TryCreateFromUserProfileLanguages();if(engine is null)throw new InvalidOperationException("Windows text recognition needs an installed OCR language. Add English under Windows language options, or enter the item manually.");
        var file=await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));using var stream=await file.OpenAsync(FileAccessMode.Read);var decoder=await BitmapDecoder.CreateAsync(stream);if(decoder.PixelWidth>OcrEngine.MaxImageDimension||decoder.PixelHeight>OcrEngine.MaxImageDimension)throw new IOException("Crop the screenshot to one item tooltip before importing.");using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied);var result=await engine.RecognizeAsync(bitmap);return string.Join(Environment.NewLine,result.Lines.Select(l=>l.Text));
    }
    async void ScreenshotInventory()
    {
        using var file=new OpenFileDialog(){Filter="Screenshot (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",Title="Choose a screenshot cropped to one item tooltip"};if(file.ShowDialog(this)!=DialogResult.OK)return;
        var profile=store.Profile;try{footer.Text="Reading screenshot locally…";var text=await ReadScreenshot(file.FileName);if(IsDisposed)return;if(store.Profile!=profile){MessageBox.Show("Character changed while reading. Open the screenshot again for the intended character.");return;}using var d=CreateScreenshotReview(file.FileName,text);d.ShowDialog(this);}catch(Exception e){MessageBox.Show(e.Message,"Screenshot entry");}finally{if(!IsDisposed)footer.Text="Screenshot entry finished. Only reviewed entries are saved.";}
    }
    Form CreateScreenshotReview(string path,string recognized)
    {
        var d=EntryDialog("Review screenshot • nothing saved yet",out var f);d.Size=new Size(760,850);var suggestion=CompanionUpgrade.Suggest(recognized,iconCatalog.Names);var picture=new PictureBox(){Width=540,Height=150,SizeMode=PictureBoxSizeMode.Zoom};using(var image=Image.FromFile(path))picture.Image=new Bitmap(image);d.Disposed+=(_,_)=>picture.Image?.Dispose();f.Controls.Add(picture);
        var raw=new TextBox(){Multiline=true,ReadOnly=true,Height=110,ScrollBars=ScrollBars.Vertical,Text=recognized};Field(f,"Recognized text (local OCR; mistakes are possible)",raw);var name=new TextBox(){Text=suggestion.Name,MaxLength=250};var qty=Amount(suggestion.Quantity??0,0,1_000_000_000);var variant=new TextBox(){Text=suggestion.Variant,MaxLength=4000};Field(f,"Exact item name • blank if the catalog match is ambiguous",name);Field(f,"Quantity • unknown stays 0; enter the visible amount",qty);Field(f,"Equipment stats • verify every field; partial OCR is not a complete variant",variant);
        var confirm=new CheckBox(){Text="I checked the name, quantity and exact stats against the image",Width=540,Height=40};f.Controls.Add(confirm);var save=Button("Review stack replacement",()=>{try{if(!confirm.Checked||qty.Value<=0||string.IsNullOrWhiteSpace(name.Text))throw new FormatException("Review every field and enter a positive quantity first.");var existing=store.Items().FirstOrDefault(i=>ItemIdentity.Key(i)==ItemIdentity.Key(name.Text.Trim(),variant.Text.Trim()));var item=new SavedItem(name.Text.Trim(),(long)qty.Value,existing?.Watch??false,existing?.Muted??false){Variant=variant.Text.Trim()};if(MessageBox.Show($"Set {item.Name}\nVariant: {item.Variant}\nQuantity: {existing?.Quantity??0} → {item.Quantity}\n\nThis replaces the matching stack quantity.","Confirm reviewed inventory",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;store.SaveItem(item);Render();d.Close();}catch(Exception e){MessageBox.Show(e.Message,"Cannot save screenshot entry");}});save.Enabled=false;confirm.CheckedChanged+=(_,_)=>save.Enabled=confirm.Checked;foreach(var c in new Control[]{name,qty,variant}){if(c is NumericUpDown n)n.ValueChanged+=(_,_)=>confirm.Checked=false;else c.TextChanged+=(_,_)=>confirm.Checked=false;}f.Controls.Add(save);return d;
    }
    void ResumeTradeDraft()
    {
        try{var draft=TradeDraft.Load(DraftPath);if(draft is null){MessageBox.Show("No unfinished trade is saved.");return;}if(draft.AlreadyRecorded(features.Trades)){File.Delete(DraftPath);MessageBox.Show("This recovered trade was already recorded. No duplicate was added.");return;}if(draft.Original is not null&&store.HasStockChange(draft.Original.Id))throw new InvalidOperationException("Undo the linked stock update before resuming this edit.");if(!draft.CanResume(features.Trades))throw new InvalidOperationException("The original trade changed or was removed. The draft cannot overwrite newer data. Use Discard saved draft in Trade Journal to archive it and start again.");if(!store.Characters().Any(c=>c.Id==draft.Entry.Profile))throw new InvalidOperationException("The draft's character no longer exists.");SwitchCharacter(draft.Entry.Profile);using var d=CreateTradeEditor(draft.Original,null,draft);d.ShowDialog(this);}catch(Exception e){MessageBox.Show(e.Message,"Draft recovery");}
    }
}
