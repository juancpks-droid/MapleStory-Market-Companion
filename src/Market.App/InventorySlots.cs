using Market.Core;
using System.Drawing.Drawing2D;

namespace Market.App;

public sealed class InventorySlot : Button
{
    public SavedItem? Item;
    public Image? ItemIcon;
    public bool Chosen;
    public bool Favorite;
    public string PlanTag="";
    bool hover;
    public InventorySlot(){Size=new Size(60,60);Margin=new Padding(3);FlatStyle=FlatStyle.Flat;DoubleBuffered=true;Cursor=Cursors.Hand;MouseEnter+=(_,_)=>{hover=true;Invalidate();};MouseLeave+=(_,_)=>{hover=false;Invalidate();};}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(Color.FromArgb(167,178,178));var r=new Rectangle(2,2,Width-5,Height-5);
        using var fill=new LinearGradientBrush(r,Color.FromArgb(242,244,228),Color.FromArgb(215,221,204),90);g.FillRectangle(fill,r);
        using var edge=new Pen(Chosen?Color.FromArgb(248,166,37):hover?Color.FromArgb(69,145,207):Color.FromArgb(129,145,150),Chosen?3:1);g.DrawRectangle(edge,r);g.DrawLine(Pens.White,4,4,Width-6,4);
        if(ItemIcon is {} icon){var state=g.Save();g.InterpolationMode=InterpolationMode.NearestNeighbor;int size=Math.Min(Width-18,(int)(32*Width/60f));g.DrawImage(icon,new Rectangle((Width-size)/2,(Height-size)/2-3,size,size));g.Restore(state);}
        else if(Item is not null)TextRenderer.DrawText(g,"?",Font,new Rectangle(4,8,Width-8,30),Color.SlateGray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        if(Item is not null)
        {
            var text=Item.Quantity.ToString("N0");using var font=new Font("Tahoma",8,FontStyle.Bold);var bounds=new Rectangle(2,Height-17,Width-7,14);
            TextRenderer.DrawText(g,text,font,new Rectangle(bounds.X+1,bounds.Y+1,bounds.Width,bounds.Height),Color.Black,TextFormatFlags.Right|TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g,text,font,bounds,Color.White,TextFormatFlags.Right|TextFormatFlags.NoPadding);
        }
        if(Favorite)TextRenderer.DrawText(g,"★",Font,new Rectangle(3,3,20,16),Color.DarkGoldenrod,TextFormatFlags.NoPadding);
        if(PlanTag.Length>0){using var tagBrush=new SolidBrush(PlanTag=="Sell"?Color.OrangeRed:PlanTag=="Keep"?Color.ForestGreen:Color.SteelBlue);g.FillRectangle(tagBrush,Width-10,4,6,10);}
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(6,6,Width-13,Height-13));
    }
}

public sealed partial class MainForm
{
    readonly FlowLayoutPanel inventorySlots=new(){Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.FromArgb(167,178,178),Padding=new Padding(8)};
    readonly Dictionary<string,Button> inventoryTabs=[];
    readonly RichTextBox selectedInventoryText=new(){Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,ScrollBars=RichTextBoxScrollBars.Vertical,BackColor=Surface};
    readonly Label inventoryCount=new(){Dock=DockStyle.Bottom,Height=30,Padding=new Padding(10,7,0,0),BackColor=Surface};
    readonly ToolTip inventoryTip=new(){AutoPopDelay=15000};
    string inventoryCategory="ALL",selectedInventoryName="";
    SavedItem? CurrentInventory()=>ItemIdentity.Resolve(store.Items().Where(s=>s.Quantity>0),selectedInventoryName);
    string InventoryCategory(SavedItem item)
    {
        string id=IconId(item);return id.FirstOrDefault() switch{'1'=>"EQUIP",'2'=>"USE",'3'=>"SET-UP",'4'=>"ETC",'5'=>"CASH",_=>"OTHER"};
    }
    void BuildSlotInventory()
    {
        var page=pages["Inventory"];var bar=Toolbar();bar.Height=65;bar.Controls.Add(Button("Notes / tag",()=>{if(CurrentInventory() is {} item)EditMerchant("memo",features.Memos.FirstOrDefault(m=>m.Profile==store.Profile&&TextKey.Search(m.Name)==TextKey.Search(item.Name))??new ItemMemo(Guid.NewGuid().ToString(),store.Profile,item.Name,"Keep",""));}));bar.Controls.Add(Button("Bulk import",BulkImport));bar.Controls.Add(Button("Add item",()=>EditItem(null,false)));bar.Controls.Add(Button("Edit quantity",()=>{if(CurrentInventory() is {} item)EditItem(item,false);}));bar.Controls.Add(Button("Remove item",RemoveInventorySlot));
        bar.Controls.Add(Button("Undo remove",UndoInventoryRemoval));bar.Controls.Add(inventorySearch);inventorySort.Items.AddRange(["Name A–Z","Quantity high–low","Category","Custom order","Favorites first"]);inventorySort.SelectedIndex=0;bar.Controls.Add(inventorySort);inventorySearch.TextChanged+=(_,_)=>RenderInventorySlots();inventorySort.SelectedIndexChanged+=(_,_)=>RenderInventorySlots();
        var tabs=new FlowLayoutPanel(){Dock=DockStyle.Top,Height=48,WrapContents=false,AutoScroll=true};
        foreach(var category in new[]{"ALL","EQUIP","USE","SET-UP","ETC","CASH","OTHER"}){var tab=Button(category,()=>{inventoryCategory=category;RenderInventorySlots();});tab.AccessibleName=category+" inventory category";inventoryTabs[category]=tab;tabs.Controls.Add(tab);}
        var details=new Panel(){Dock=DockStyle.Right,Width=280,Padding=new Padding(10,0,0,0)};var heading=new ClassicTitle(){Text="ITEM INFORMATION",Dock=DockStyle.Top,Height=36,Padding=new Padding(10,8,0,0)};
        var actions=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=135,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        actions.Controls.Add(Button("Item details",()=>{if(CurrentInventory() is {} item)ShowItemDetails(item);}));
        actions.Controls.Add(Button("Watch item",()=>{if(CurrentInventory() is {} item)WatchObject(item);}));
        actions.Controls.Add(Button("Find listings",()=>{if(CurrentInventory() is {} item){search.Text=item.Name;ShowPage("Price Checker");}}));
        details.Controls.Add(selectedInventoryText);details.Controls.Add(actions);details.Controls.Add(heading);
        var body=new Panel(){Dock=DockStyle.Fill};body.Controls.Add(inventorySlots);body.Controls.Add(details);body.Controls.Add(inventoryCount);
        page.Controls.Add(body);page.Controls.Add(tabs);page.Controls.Add(inventorySummary);page.Controls.Add(bar);
        FormClosed+=(_,_)=>inventoryTip.Dispose();
    }
    void RemoveInventorySlot()
    {
        if(CurrentInventory() is not {} item)return;removedInventory=item;if(item.Watch)store.SaveItem(item with{Quantity=0});else store.RemoveItem(item.Name,item.Variant);selectedInventoryName="";Render();
    }
    void RenderInventorySlots()
    {
        if(inventorySlots.IsDisposed)return;
        var all=store.Items().Where(s=>s.Quantity>0).ToList();var visible=OrderedInventory(all.Where(s=>(inventoryCategory=="ALL"||InventoryCategory(s)==inventoryCategory)&&PolishTools.Similar(features.Polish.Resolve(inventorySearch.Text),s.Name))).ToList();
        int matches=visible.Count;inventoryPage=Math.Clamp(inventoryPage,0,Math.Max(0,(matches-1)/96));visible=visible.Skip(inventoryPage*96).Take(96).ToList();bagPage.Text=$"Page {inventoryPage+1}/{Math.Max(1,(matches+95)/96)} • {bulkStacks.Count} selected";if(ItemIdentity.Resolve(visible,selectedInventoryName) is not {} selectedStack)selectedInventoryName=visible.Count>0?ItemIdentity.Key(visible[0]):"";else selectedInventoryName=ItemIdentity.Key(selectedStack);
        inventorySlots.SuspendLayout();inventoryTip.RemoveAll();while(inventorySlots.Controls.Count>0)inventorySlots.Controls[0].Dispose();
        foreach(var item in visible)
        {
            var slot=new InventorySlot(){Item=item,Size=new Size(features.Polish.SlotSize,features.Polish.SlotSize),ItemIcon=icons.Peek(IconId(item)),Chosen=ItemIdentity.Key(item)==selectedInventoryName||bulkStacks.Contains(ItemIdentity.Key(item)),AccessibleName=$"{item.Name}, quantity {item.Quantity:N0}",AccessibleDescription="Select to inspect. Press Enter for item details."};
            var commands=ItemCommandMenu(()=>item);slot.ContextMenuStrip=commands;slot.Disposed+=(_,_)=>commands.Dispose();ConfigureSlotOrganization(slot,item);slot.Click+=(_,_)=>{selectedInventoryName=ItemIdentity.Key(item);if((ModifierKeys&Keys.Control)!=0){if(!bulkStacks.Add(ItemIdentity.Key(item)))bulkStacks.Remove(ItemIdentity.Key(item));}else bulkStacks.Clear();bagPage.Text=$"Page {inventoryPage+1} • {bulkStacks.Count} selected";foreach(var other in inventorySlots.Controls.OfType<InventorySlot>()){other.Chosen=other.Item is {} otherItem&&(ItemIdentity.Key(otherItem)==ItemIdentity.Key(item)||bulkStacks.Contains(ItemIdentity.Key(otherItem)));other.Invalidate();}RenderSelectedInventory();};
            slot.KeyDown+=(_,e)=>{if(e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down){int index=inventorySlots.Controls.IndexOf(slot);int columns=Math.Max(1,inventorySlots.ClientSize.Width/(features.Polish.SlotSize+6));int next=index+(e.KeyCode==Keys.Left?-1:e.KeyCode==Keys.Right?1:e.KeyCode==Keys.Up?-columns:columns);if(next>=0&&next<inventorySlots.Controls.Count&&inventorySlots.Controls[next] is InventorySlot nextSlot&&nextSlot.Item is not null){nextSlot.Focus();nextSlot.PerformClick();}e.SuppressKeyPress=true;}if(e.KeyCode==Keys.Enter){ShowItemDetails(item);e.SuppressKeyPress=true;}};
            inventoryTip.SetToolTip(slot,$"{item.Name}\nQuantity: {item.Quantity:N0}\n{InventoryCategory(item)} • Click to inspect\n{(item.Watch?"Watching":"Not watched")}\n{(item.Variant.Length>0?string.Join("\n",CompanionFeatures.Stats(item.Variant).Select(p=>p.Key.ToUpperInvariant()+": "+p.Value)):"Stackable / no saved variant")}");inventorySlots.Controls.Add(slot);_=LoadSlotIcon(slot,item);
        }
        int empty=Math.Max(0,48-visible.Count);for(int i=0;i<empty;i++){var slot=new InventorySlot(){Size=new Size(features.Polish.SlotSize,features.Polish.SlotSize),AccessibleName="Empty item slot",TabStop=false};slot.Click+=(_,_)=>EditItem(null,false);inventoryTip.SetToolTip(slot,"Empty slot — click to add an item");inventorySlots.Controls.Add(slot);}
        inventorySlots.ResumeLayout();foreach(var (name,tab) in inventoryTabs){tab.BackColor=name==inventoryCategory?Accent:Surface;tab.Invalidate();}
        inventoryCount.Text=$"★ Favorite • Red: sell / Green: keep / Blue: other tag • {visible.Count} stacks • {all.Count} total • Empty slots can be clicked to add items";RenderSelectedInventory();
    }
    async Task LoadSlotIcon(InventorySlot slot,SavedItem item){var icon=await icons.Get(IconId(item));if(!slot.IsDisposed){slot.ItemIcon=icon;slot.Invalidate();}}
    void RenderSelectedInventory()
    {
        var item=CurrentInventory();if(item is null){selectedInventoryText.Text="Select an item slot to see its quantity and estimated value.\n\nClick an empty slot to add an item.\n\nOTHER contains items whose classic category could not be identified.";return;}
        var row=EligibleObservations().FirstOrDefault(o=>TextKey.Search(o.Name)==TextKey.Search(item.Name)&&o.Kind==EvidenceKind.Asking&&o.Available&&(!o.Equipment||o.Variant==item.Variant));
        var estimate=row is null?null:ValuationEngine.Estimate(EligibleObservations(),row,DateTimeOffset.UtcNow,TimeSpan.FromHours(settings.FreshHours),settings.Discount);
        selectedInventoryText.Text=$"{item.Name}\n{item.Variant}\n{(FavoriteItems().Contains(TextKey.Search(item.Name))?"★ Favorite":"")} {features.Memos.FirstOrDefault(m=>m.Profile==store.Profile&&TextKey.Search(m.Name)==TextKey.Search(item.Name))?.Tag}\n\n{InventoryCategory(item)}\nQuantity: {item.Quantity:N0}\n\nEstimated unit value\n{TextKey.Mesos(estimate?.Value)}\n\nEstimated stack value\n{TextKey.Mesos(estimate?.Value*item.Quantity)}\n\n"+(estimate is null?"Not enough comparable market evidence.":$"Confidence: {estimate.Confidence}/100\n{estimate.Basis}")+"\n\n"+(item.Watch?(item.Muted?"Watchlist • alerts muted":"Watchlist • alerts enabled"):"Not on watchlist");
    }
    async Task SmokeInventorySlots()
    {
        store.SaveItem(new SavedItem("Blue Potion",50,false,false));store.SaveItem(new SavedItem("Leather",12,false,false));
        Render();ShowPage("Inventory");inventoryCategory="USE";RenderInventorySlots();
        if(!inventorySlots.Controls.OfType<InventorySlot>().Any(s=>s.Item?.Name=="Blue Potion")||inventorySlots.Controls.OfType<InventorySlot>().Any(s=>s.Item is {} i&&InventoryCategory(i)!="USE"))throw new Exception("Inventory category filtering failed");
        inventoryCategory="ALL";RenderInventorySlots();selectedInventoryName="Blue Mushroom Cap";var before=CurrentInventory()??throw new Exception("Inventory selection failed");RemoveInventorySlot();
        var after=store.Items().FirstOrDefault(s=>s.Name==before.Name);if(after is null||!after.Watch||after.Quantity!=0)throw new Exception("Removing inventory lost watched item");store.SaveItem(before);RenderItems();
        RenderInventorySlots();if(inventorySlots.Controls.Count<48)throw new Exception("Empty inventory slots missing");
        await Task.WhenAll(store.Items().Where(s=>s.Quantity>0).Select(s=>icons.Get(IconId(s))));RenderInventorySlots();
        footer.Text="Inventory preview • isolated test quantities";CapturePreview(Path.Combine(dataDirectory,"inventory-slots.png"));
        inventoryCategory="CASH";RenderInventorySlots();if(inventorySlots.Controls.OfType<InventorySlot>().Any(s=>s.Item is not null)||CurrentInventory() is not null)throw new Exception("Empty category kept stale selection");
        inventoryCategory="ALL";RenderInventorySlots();ShowPage("Dashboard");
    }
}

