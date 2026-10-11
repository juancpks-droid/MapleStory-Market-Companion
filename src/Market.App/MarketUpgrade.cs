using Market.Core;

namespace Market.App;
public sealed partial class MainForm
{
    readonly TextBox catalogSearch=new(){Width=245,PlaceholderText="Find an item in the classic catalog"};
    readonly ComboBox catalogCategory=new(){Width=120,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly FlowLayoutPanel catalogTiles=new(){Dock=DockStyle.Fill,AutoScroll=true,BackColor=Surface,Padding=new Padding(8)};
    readonly Label catalogStatus=Info(42);
    readonly DataGridView comparisonGrid=Grid();
    readonly Label comparisonNote=Info(65);
    readonly TextBox inventorySearch=new(){Width=160,PlaceholderText="Search your bag"};
    readonly ComboBox inventorySort=new(){Width=145,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Button catalogPrevious=Button("Previous",()=>{}),catalogNext=Button("Next",()=>{});
    IconCatalog.Entry[] catalogEntries=[];int catalogPage;
    object? compareLeft,compareRight;SavedItem? removedInventory;
    static string CatalogCategory(string id)=>id.FirstOrDefault() switch{'1'=>"EQUIP",'2'=>"USE",'3'=>"SET-UP",'4'=>"ETC",'5'=>"CASH",_=>"OTHER"};
    void BuildCatalog()
    {
        catalogEntries=iconCatalog.Entries.OrderBy(e=>e.Name).ThenBy(e=>e.Id).ToArray();
        var page=pages["Item Catalog"];var bar=Toolbar();catalogCategory.Items.AddRange(["ALL","EQUIP","USE","SET-UP","ETC","CASH"]);catalogCategory.SelectedIndex=0;
        bar.Controls.Add(catalogSearch);bar.Controls.Add(catalogCategory);bar.Controls.Add(catalogPrevious);bar.Controls.Add(catalogNext);
        catalogSearch.TextChanged+=(_,_)=>{catalogPage=0;DebounceSearch(RenderCatalog);};catalogCategory.SelectedIndexChanged+=(_,_)=>{catalogPage=0;RenderCatalog();};
        catalogPrevious.Click+=(_,_)=>{catalogPage=Math.Max(0,catalogPage-1);RenderCatalog();};catalogNext.Click+=(_,_)=>{catalogPage++;RenderCatalog();};
        page.Controls.Add(catalogTiles);page.Controls.Add(catalogStatus);page.Controls.Add(bar);RenderCatalog();
    }
    IconCatalog.Entry[] CatalogResults()=>catalogEntries.Where(e=>PolishTools.Similar(features.Polish.Resolve(catalogSearch.Text),e.Name)&&(catalogCategory.SelectedIndex<=0||CatalogCategory(e.Id)==catalogCategory.Text)).ToArray();
    void RenderCatalog()
    {
        var entries=CatalogResults();int pageCount=Math.Max(1,(entries.Length+35)/36);catalogPage=Math.Clamp(catalogPage,0,pageCount-1);
        catalogStatus.Text=$"{entries.Length:N0} classic items • Page {catalogPage+1} / {pageCount}\nCatalog entries are item identities, not proof of live market coverage. Click a tile for details; right-click for actions.";
        catalogPrevious.Enabled=catalogPage>0;catalogNext.Enabled=catalogPage<pageCount-1;
        catalogTiles.SuspendLayout();while(catalogTiles.Controls.Count>0)catalogTiles.Controls[0].Dispose();
        foreach(var entry in entries.Skip(catalogPage*36).Take(36))
        {
            var tile=new Button(){Width=142,Height=92,Text=entry.Name,TextAlign=ContentAlignment.BottomCenter,ImageAlign=ContentAlignment.TopCenter,FlatStyle=FlatStyle.Flat,BackColor=Surface,ForeColor=TextColor,Padding=new Padding(3),Margin=new Padding(4),AccessibleName=entry.Name};
            tile.Click+=(_,_)=>ShowItemDetails(entry);var menu=new ContextMenuStrip();menu.Items.Add("Watch item",null,(_,_)=>WatchObject(entry));menu.Items.Add("Add to inventory",null,(_,_)=>{search.Text=entry.Name;EditItem(ItemIdentity.Resolve(store.Items(),entry.Name),false);});menu.Items.Add("Compare",null,(_,_)=>PinComparison(entry));menu.Items.Add("Search listings",null,(_,_)=>{search.Text=entry.Name;ShowPage("Price Checker");});ExtendItemCommands(menu,()=>entry);menu.Items.Add("Add item note",null,(_,_)=>EditMerchant("memo",new ItemMemo(Guid.NewGuid().ToString(),store.Profile,entry.Name,"Keep","")));tile.ContextMenuStrip=menu;tile.Disposed+=(_,_)=>menu.Dispose();catalogTiles.Controls.Add(tile);
        }
        catalogTiles.ResumeLayout();if(selected=="Item Catalog")LoadCatalogImages();
    }
    async void LoadCatalogImages()
    {
        var entries=CatalogResults().Skip(catalogPage*36).Take(36).ToArray();var tiles=catalogTiles.Controls.OfType<Button>().ToArray();
        for(int i=0;i<Math.Min(entries.Length,tiles.Length);i++){if(tiles[i].IsDisposed||selected!="Item Catalog")return;var icon=await icons.Get(entries[i].Id);if(!tiles[i].IsDisposed)tiles[i].Image=icon;}
    }
    void PinComparison(object item){if(compareLeft is null)compareLeft=item;else compareRight=item;ShowPage("Compare");RenderComparison();}
    void BuildComparison()
    {
        var page=pages["Compare"];var bar=Toolbar();bar.Controls.Add(Button("Choose items",()=>ShowPage("Item Catalog")));bar.Controls.Add(Button("Clear comparison",()=>{compareLeft=null;compareRight=null;RenderComparison();}));bar.Controls.Add(Button("Swap sides",()=>{(compareLeft,compareRight)=(compareRight,compareLeft);RenderComparison();}));
        comparisonNote.Text="Right-click two items and choose Compare. The first fills the left side; the next fills the right.\nFM reference prices may mix variants and ages. They never become inventory values or bargain evidence.";
        page.Controls.Add(comparisonGrid);page.Controls.Add(comparisonNote);page.Controls.Add(bar);RenderComparison();
    }
    string[] CompareValues(object? item)
    {
        if(item is null)return ["Choose an item","—","—","—","—","—","—","—","—"];
        string name=ItemName(item);var listing=Listing(item);var fm=fmSummaries.FirstOrDefault(f=>TextKey.Search(f.Name)==TextKey.Search(name));var saved=FindSaved(item);
        var value=listing is null||!settings.Sources.Any(s=>s.Id==listing.Provider&&s.Authorized)?null:ValuationEngine.Estimate(EligibleObservations(),listing,DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours),settings.Discount);
        return [name,CatalogCategory(IconId(item)),saved?.Quantity.ToString("N0")??"0",TextKey.Mesos(listing?.UnitPrice),TextKey.Mesos(value?.Value),value is null?"No comparable estimate":$"{value.Confidence}/100 • {value.Count} observations",TextKey.Mesos(fm?.Median7),fm is null?"No FM summary":$"{fm.LastSeen.ToLocalTime():g} • {(settings.Sources.Any(s=>s.Id=="Kerning Exchange"&&s.Authorized)?fm.Quality(DateTimeOffset.UtcNow,settings.FreshHours):"Source disabled")}",listing?.Equipment==true?listing.Variant:"No specific equipment variant selected"];
    }
    void RenderComparison()
    {
        SetColumns(comparisonGrid,["Field","Left item","Right item"]);var left=CompareValues(compareLeft);var right=CompareValues(compareRight);string[] fields=["Item","Category","Owned quantity","Selected listing / unit","Comparable estimate","Evidence confidence","FM 7-day reference","FM last sighting / quality","Equipment variant"];
        for(int i=0;i<fields.Length;i++)comparisonGrid.Rows.Add(fields[i],left[i],right[i]);comparisonGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;comparisonGrid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;comparisonGrid.Columns[0].FillWeight=75;AppendEquipmentComparison();
    }
    void UndoInventoryRemoval()
    {
        if(removedInventory is not {} item)return;var current=store.Items().FirstOrDefault(s=>ItemIdentity.Key(s)==ItemIdentity.Key(item));
        store.SaveItem(current is null?item:current with{Quantity=current.Quantity==0?item.Quantity:current.Quantity});removedInventory=null;Render();footer.Text="Removed inventory stack restored; current saved preferences kept.";
    }
    async Task SmokeMajorUpgrade()
    {
        catalogSearch.Text="Blue Potion";if(CatalogResults().Length==0||CatalogResults().Any(e=>!e.Name.Contains("Blue Potion")))throw new Exception("Catalog search failed");
        catalogCategory.SelectedIndex=1;if(CatalogResults().Length!=0)throw new Exception("Catalog category filter failed");catalogCategory.SelectedIndex=0;
        var entry=CatalogResults().First();compareLeft=entry;compareRight=new SavedItem("Leather",12,false,false);RenderComparison();if(comparisonGrid.Rows.Count!=9||CompareValues(entry)[4]!="Price unavailable")throw new Exception("Comparison invented a market estimate");
        ShowPage("Compare");CapturePreview(Path.Combine(dataDirectory,"comparison.png"));
        inventorySearch.Text="Leather";RenderInventorySlots();if(inventorySlots.Controls.OfType<InventorySlot>().Any(s=>s.Item is {} i&&i.Name!="Leather"))throw new Exception("Bag search failed");inventorySearch.Clear();
        selectedInventoryName="Leather";var quantity=CurrentInventory()?.Quantity;RemoveInventorySlot();UndoInventoryRemoval();if(CurrentInventory()?.Quantity!=quantity){selectedInventoryName="Leather";if(CurrentInventory()?.Quantity!=quantity)throw new Exception("Undo stock removal failed");}
        ShowPage("Item Catalog");await Task.WhenAll(CatalogResults().Select(e=>icons.Get(e.Id)));RenderCatalog();await Task.Delay(200);CapturePreview(Path.Combine(dataDirectory,"catalog.png"));catalogSearch.Clear();ShowPage("Dashboard");
    }
}
