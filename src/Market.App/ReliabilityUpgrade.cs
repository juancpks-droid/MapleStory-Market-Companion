using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
    readonly HashSet<string> essentialPages=["Dashboard","Item Finder","Accuracy","Price Checker","Seller Finder","FM Tracker","Inventory","Characters","Transfers","Watchlist","Trade Journal","Alert Inbox","Settings","Backups"];
    readonly HashSet<string> expandedTools=[];
    bool fullNavigation,reliabilityReady;
    void BuildReliabilityPolish()
    {
        var page=pages["Inventory"];inventorySearch.Parent?.Controls.Remove(inventorySearch);inventorySort.Parent?.Controls.Remove(inventorySort);bagPage.Parent?.Controls.Remove(bagPage);
        foreach(var panel in page.Controls.OfType<FlowLayoutPanel>().Where(p=>!p.Controls.OfType<Button>().Any(b=>inventoryTabs.Values.Contains(b))).ToArray()){page.Controls.Remove(panel);panel.Dispose();}
        var toolbar=new FlowLayoutPanel(){Dock=DockStyle.Top,Height=98,AutoScroll=true,WrapContents=true,Padding=new Padding(0)};
        toolbar.Controls.Add(Button("Add stack",()=>EditItem(null,false)));toolbar.Controls.Add(Button("Edit selected",()=>{if(CurrentInventory() is {} item)EditItem(item,false);}));toolbar.Controls.Add(Button("Remove",RemoveInventorySlot));
        var actions=new ContextMenuStrip();void Action(string name,Action callback)=>actions.Items.Add(name,null,(_,_)=>callback());Action("Import / reconcile",BulkImport);Action("Read item screenshot",ScreenshotInventory);Action("What should I sell?",()=>{using var d=CreateSellView();d.ShowDialog(this);});Action("Exact-variant charts",()=>ShowVariantChart(CurrentInventory()));Action("Undo last removal",UndoInventoryRemoval);actions.Items.Add(new ToolStripSeparator());Action("Select visible stacks",()=>{foreach(var slot in inventorySlots.Controls.OfType<InventorySlot>().Where(s=>s.Item is not null))bulkStacks.Add(ItemIdentity.Key(slot.Item!));RenderInventorySlots();});Action("Clear selection",()=>{bulkStacks.Clear();RenderInventorySlots();});Action("Bulk actions",BulkItemActions);actions.Items.Add(new ToolStripSeparator());Action("Favorite / unfavorite",ToggleFavorite);Action("Move earlier",()=>MoveCurrent(-1));Action("Move later",()=>MoveCurrent(1));Action("Notes / tag",()=>{if(CurrentInventory() is {} item)EditMerchant("memo",features.Memos.FirstOrDefault(m=>m.Profile==store.Profile&&TextKey.Search(m.Name)==TextKey.Search(item.Name))??new(Guid.NewGuid().ToString(),store.Profile,item.Name,"Keep",""));});Action("Accessibility & panel size",EditAccessibility);
        var actionButton=Button("More actions ▾",()=>{});actionButton.Click+=(_,_)=>actions.Show(actionButton,new Point(0,actionButton.Height));actionButton.Disposed+=(_,_)=>actions.Dispose();toolbar.Controls.Add(actionButton);inventorySearch.Width=180;inventorySearch.AccessibleName="Filter inventory by name or alias";inventorySearch.Margin=new Padding(4,10,4,4);toolbar.Controls.Add(inventorySearch);inventorySort.Width=155;inventorySort.Margin=new Padding(4,10,4,4);toolbar.Controls.Add(inventorySort);inventorySort.SelectedIndex=0;toolbar.SetFlowBreak(inventorySort,true);toolbar.Controls.Add(Button("Previous",()=>{inventoryPage=Math.Max(0,inventoryPage-1);RenderInventorySlots();}));toolbar.Controls.Add(Button("Next",()=>{inventoryPage++;RenderInventorySlots();}));toolbar.Controls.Add(bagPage);var saleDrop=Button("Drop stack here to sell",()=>{if(CurrentInventory() is {} i)PrepareSale(i);});ConfigureSaleDrop(saleDrop);toolbar.Controls.Add(saleDrop);page.Controls.Add(toolbar);

        priceGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;priceGrid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCells;
        priceGrid.CellFormatting+=(_,e)=>{if(e.RowIndex<0||priceGrid.Rows[e.RowIndex].Tag is not Observation o)return;var label=ReliabilityTools.EvidenceStatus(o,settings.Sources.Any(s=>s.Id==o.Provider&&s.Authorized),DateTimeOffset.UtcNow,settings.FreshHours);if(e.ColumnIndex==6&&(!label.StartsWith("Recently checked")||o.PriceApproximate))e.CellStyle!.ForeColor=Color.FromArgb(135,65,25);};sellerCompareGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&sellerCompareGrid.Rows[e.RowIndex].Tag is Observation o)ShowItemDetails(o);};
        reliabilityReady=true;RebuildNavigation();
    }
    void CompactGroups(string name)
    {
        if(!reliabilityReady)return;
        var beforeCollapsed=collapsedGroups.ToHashSet();var beforeExpanded=expandedTools.ToHashSet();
        var group=PageGroup(name);foreach(var other in new[]{"Market","Inventory","Trading","Settings"})if(other!=group)collapsedGroups.Add(other);
        collapsedGroups.Remove(group);expandedTools.Clear();if(!essentialPages.Contains(name))expandedTools.Add(group);
        if(!beforeCollapsed.SetEquals(collapsedGroups)||!beforeExpanded.SetEquals(expandedTools))RebuildNavigation();
    }
    async Task SmokeReliability()
    {
        var before=Snapshot();var a=new SavedItem("Reliability Sword",2,false,false){Variant="watk=42"};var b=a with{Variant="watk=48"};store.SaveItem(a);store.SaveItem(b);Render();universalQuery.Text="Reliability Sword";RenderUniversal();var row=universalGrid.Rows.Cast<DataGridViewRow>().Single(r=>r.Tag is SearchHit h&&h.Page=="Inventory"&&h.Variant==b.Variant);universalGrid.ClearSelection();row.Selected=true;OpenUniversal();if(CurrentInventory()?.Variant!=b.Variant)throw new Exception("Search navigation lost exact variant");using(var editor=CreateItemEditor(b,false)){editor.Show(this);var fields=editor.Controls.OfType<FlowLayoutPanel>().Single();if(fields.Controls.OfType<ComboBox>().Single().Text!=b.Variant)throw new Exception("Editing lost cached variant text");fields.Controls.OfType<Button>().Single(x=>x.Text=="Save stack").PerformClick();if(store.Items().Any(i=>i.Name==b.Name&&i.Variant==""))throw new Exception("Editing created a blank variant");}if(organizedNav!.Controls.OfType<Button>().Count()>18)throw new Exception("Simple navigation unexpectedly expanded");CapturePreview(Path.Combine(dataDirectory,"inventory-15.png"));ShowPage("Price Checker");CapturePreview(Path.Combine(dataDirectory,"freshness-15.png"));ShowPage("Dashboard");CapturePreview(Path.Combine(dataDirectory,"dashboard-15.png"));RestorePersonal(before);await Task.CompletedTask;
    }
}

