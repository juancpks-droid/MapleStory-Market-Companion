using Market.Core;

namespace Market.App;
public sealed partial class MainForm
{
    readonly ComboBox fmFilter=new(){Width=150,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Surface};
    readonly ComboBox fmOrder=new(){Width=140,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Surface};
    static string ItemName(object? item)=>item switch{IconCatalog.Entry e=>e.Name,Observation o=>o.Name,FmSummary f=>f.Name,Deal d=>d.Listing.Name,SavedItem s=>s.Name,_=>""};
    static Observation? Listing(object? item)=>item switch{Observation o=>o,Deal d=>d.Listing,_=>null};
    SavedItem? FindSaved(object item){var variant=(item as SavedItem)?.Variant??Listing(item)?.Variant;return variant is null?ItemIdentity.Resolve(store.Items(),ItemName(item)):store.Items().FirstOrDefault(i=>ItemIdentity.Key(i)==ItemIdentity.Key(ItemName(item),variant));}
    void WatchObject(object item)
    {
        var name=ItemName(item);if(name.Length==0)return;
        var wantedVariant=(item as SavedItem)?.Variant??Listing(item)?.Variant??"";var saved=store.Items().FirstOrDefault(s=>ItemIdentity.Key(s)==ItemIdentity.Key(name,wantedVariant));
        store.SaveItem(saved is null?new SavedItem(name,0,true,false){Variant=item is SavedItem supplied?supplied.Variant:Listing(item)?.Variant??""}:saved with{Watch=true});
        Render();footer.Text="Watching "+name+". Existing quantities and alert preferences were kept.";
    }
    IEnumerable<FmSummary> BrowseSummaries()
    {
        var names=store.Items().Where(s=>s.Watch).Select(s=>TextKey.Search(s.Name)).ToHashSet();
        var rows=fmSummaries.Where(s=>TextKey.Search(s.Name).Contains(TextKey.Search(fmSearch.Text)));
        rows=fmFilter.SelectedIndex switch{1=>rows.Where(s=>names.Contains(TextKey.Search(s.Name))),2=>rows.Where(s=>s.Equipment),3=>rows.Where(s=>!s.Equipment),4=>rows.Where(s=>s.LastSeen<=DateTimeOffset.UtcNow&&s.LastSeen>=DateTimeOffset.UtcNow.AddHours(-settings.FreshHours)),_=>rows};
        return fmOrder.SelectedIndex switch{1=>rows.OrderByDescending(s=>s.LastSeen),2=>rows.OrderBy(s=>s.Name),3=>rows.OrderBy(s=>s.LowestAsk??decimal.MaxValue),_=>rows.OrderByDescending(s=>s.Sightings)};
    }
    void ConfigureTradingTools()
    {
        fmFilter.AccessibleName="Filter market items";fmOrder.AccessibleName="Sort market items";
        foreach(var box in new[]{fmFilter,fmOrder})
        {
            box.DrawMode=DrawMode.OwnerDrawFixed;box.ItemHeight=20;
            box.DrawItem+=(_,e)=>{e.DrawBackground();int index=e.Index>=0?e.Index:box.SelectedIndex;if(index>=0)TextRenderer.DrawText(e.Graphics,box.Items[index]?.ToString(),box.Font,e.Bounds,e.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);e.DrawFocusRectangle();};
        }
        var suggestions=new AutoCompleteStringCollection();suggestions.AddRange(iconCatalog.Names.Concat(fmSummaries.Select(s=>s.Name)).Concat(observations.Select(s=>s.Name)).Distinct().Order().ToArray());
        foreach(var field in new[]{search,quickSearch,fmSearch}){field.AutoCompleteMode=AutoCompleteMode.SuggestAppend;field.AutoCompleteSource=AutoCompleteSource.CustomSource;field.AutoCompleteCustomSource=suggestions;}
        KeyPreview=true;KeyDown+=(_,e)=>{if(e.Control&&e.KeyCode==Keys.F){ShowPage("Price Checker");search.Focus();search.SelectAll();e.SuppressKeyPress=true;}};
        foreach(var grid in new[]{priceGrid,dealsGrid,watchGrid,inventoryGrid,fmGrid,dashboardWatch,dashboardDeals,dashboardMarket})
        {
            var menu=new ContextMenuStrip();object? Current()=>grid.SelectedRows.Count>0?grid.SelectedRows[0].Tag:null;
            menu.Items.Add("Add item note",null,(_,_)=>{if(Current() is {} item)EditMerchant("memo",new ItemMemo(Guid.NewGuid().ToString(),store.Profile,ItemName(item),"Keep",""));});
            menu.Items.Add("Wishlist exact equipment",null,(_,_)=>{if(Current() is Observation o&&o.Equipment&&o.VariantComplete)EditMerchant("wish",new GearWish(Guid.NewGuid().ToString(),store.Profile,o.Name,o.Variant,0));else MessageBox.Show("Select an equipment listing with complete stats first.");});
            menu.Items.Add("Set price alert",null,(_,_)=>{if(Current() is {} item)EditRule(null,item);});
            menu.Items.Add("Compare",null,(_,_)=>{if(Current() is {} item)PinComparison(item);});
            menu.Items.Add("Item details",null,(_,_)=>{if(Current() is {} item)ShowItemDetails(item);});
            menu.Items.Add("Watch this item",null,(_,_)=>{if(Current() is {} item)WatchObject(item);});
            menu.Items.Add("Search listings",null,(_,_)=>{if(Current() is {} item){search.Text=ItemName(item);ShowPage("Price Checker");}});

            ExtendItemCommands(menu,Current);menu.Opening+=(_,e)=>e.Cancel=Current() is null;
            grid.ContextMenuStrip=menu;grid.Disposed+=(_,_)=>menu.Dispose();
            grid.CellMouseDown+=(_,e)=>{if(e.Button==MouseButtons.Right){grid.ClearSelection();if(e.RowIndex>=0){grid.Rows[e.RowIndex].Selected=true;grid.CurrentCell=grid.Rows[e.RowIndex].Cells[Math.Max(0,e.ColumnIndex)];}}};
        }
        foreach(var grid in new[]{watchGrid,inventoryGrid})grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&grid.Rows[e.RowIndex].Tag is {} item)ShowItemDetails(item);};
    }
    void ShowItemDetails(object item){using var dialog=CreateItemDetails(item);dialog.ShowDialog(this);}
    Form CreateItemDetails(object item)
    {
        var name=ItemName(item);var listing=Listing(item)??(item is SavedItem savedItem?EligibleObservations().Where(o=>TextKey.Search(o.Name)==TextKey.Search(name)&&(!o.Equipment||o.Variant==savedItem.Variant)).OrderByDescending(o=>o.RetrievedAt).FirstOrDefault():null);var fm=item as FmSummary??fmSummaries.FirstOrDefault(f=>TextKey.Search(f.Name)==TextKey.Search(name));
        var dialog=new Form(){Text=name+" • Item details",Size=new Size(680,590),MinimumSize=new Size(600,510),StartPosition=FormStartPosition.CenterParent,BackColor=Back,ForeColor=TextColor,Font=Font,MinimizeBox=false,MaximizeBox=false};
        var head=new ClassicTitle(){Text=name,Dock=DockStyle.Top,Height=54,Padding=new Padding(65,16,6,6)};
        var picture=new PictureBox(){Left=12,Top=8,Size=new Size(40,38),BackColor=Surface,SizeMode=PictureBoxSizeMode.CenterImage,Image=icons.Peek(IconId(item))};head.Controls.Add(picture);
        var actions=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=92,Padding=new Padding(8),WrapContents=true};
        var watch=Button("Watch item",()=>{WatchObject(item);});actions.Controls.Add(watch);var commandMenu=ItemCommandMenu(()=>item);var commands=Button("Item actions",()=>commandMenu.Show(Cursor.Position));actions.Controls.Add(commands);dialog.Disposed+=(_,_)=>commandMenu.Dispose();
        actions.Controls.Add(Button("View listings",()=>{search.Text=name;ShowPage("Price Checker");dialog.Close();}));

        var url=listing?.Url??fm?.Url;if(url is not null)actions.Controls.Add(Button("Open source",()=>Open(url)));
        actions.Height=132;actions.Controls.Add(Button("View on NiaMeowDB",()=>ShowWebsite("NiaMeowDB",name,item)));actions.Controls.Add(Button("Other websites",()=>ShowItemWebsites(name,item)));
        actions.Controls.Add(Button("Price alert",()=>{EditRule(null,item);}));
        actions.Controls.Add(Button("Compare",()=>{PinComparison(item);dialog.Close();}));
        actions.Controls.Add(Button("Close",()=>dialog.Close()));
        string details;
        if(listing is not null)
        {
            bool enabled=settings.Sources.Any(s=>s.Id==listing.Provider&&s.Authorized);var estimate=enabled?ValuationEngine.Estimate(EligibleObservations(),listing,DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours),settings.Discount):null;
            details=$"{(Listing(item) is null?"LATEST CACHED MATCHING LISTING":"SELECTED LISTING")}\n{TextKey.Mesos(listing.UnitPrice)} mesos per unit • {listing.Kind}\nSeller: {listing.Seller}\nProvider: {listing.Provider}\nObserved: {listing.ObservedAt?.ToLocalTime().ToString("g")??"Unknown"}\nRetrieved: {listing.RetrievedAt.LocalDateTime:g}\n{ReliabilityTools.EvidenceStatus(listing,enabled,DateTimeOffset.UtcNow,settings.FreshHours)} • {(listing.Available?"Listed available at last retrieval":"No longer listed available")}\n\nCOMPARABLE VALUE\n"+(estimate is null?"Not enough eligible comparable evidence.":$"{TextKey.Mesos(estimate.Value)} mesos • confidence {estimate.Confidence}/100\n{estimate.Count} observations • {estimate.Basis}\nQuick sale {TextKey.Mesos(estimate.Quick)} • Maximum buy {TextKey.Mesos(estimate.MaxBuy)}")+"\n\nVARIANT\n"+(listing.Equipment?listing.Variant:"Stackable item");
        }
        else details="ITEM OVERVIEW\nOpen View listings to inspect individual offers.\nUse Search online there to request current coverage.\n"+(item is SavedItem saved?$"\nWatch alerts: {(saved.Watch?(saved.Muted?"Muted":"Enabled"):"Not watched")}\n":"");
        if(fm is not null)details+=$"\n\nFM REFERENCE • NOT A VERIFIED SALE PRICE\n7-day median: {TextKey.Mesos(fm.Median7)} mesos\nLowest observed ask: {TextKey.Mesos(fm.LowestAsk)} mesos\n{fm.Sightings:N0} sightings • {fm.Sellers:N0} players\nLast seen: {fm.LastSeen.ToLocalTime():g}\n{(settings.Sources.Any(s=>s.Id=="Kerning Exchange"&&s.Authorized)?fm.Quality(DateTimeOffset.UtcNow,settings.FreshHours):"Source disabled — cached only")}\nAggregates may mix ages and equipment variants.";
        var body=new RichTextBox(){Text=details,Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Surface,ForeColor=TextColor,Font=new Font("Tahoma",10),ScrollBars=RichTextBoxScrollBars.Vertical};
        var frame=new Panel(){Dock=DockStyle.Fill,Padding=new Padding(16),BackColor=Surface};frame.Controls.Add(body);dialog.Controls.Add(frame);dialog.Controls.Add(actions);dialog.Controls.Add(head);AddCommandCenter(dialog,frame,body,item);
        dialog.Shown+=async(_,_)=>{var icon=await icons.Get(IconId(item));if(!dialog.IsDisposed)picture.Image=icon;};
        return dialog;
    }
}
