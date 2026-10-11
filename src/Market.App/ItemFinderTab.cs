using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
    readonly TextBox finderSearch=new(){Width=250,PlaceholderText="Item name or classic item ID",AccessibleName="Find an item"};
    readonly ComboBox finderCategory=new(){Width=125,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox finderSource=new(){Width=150,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly CheckBox finderListed=new(){Text="With listings",Width=115,Height=32};
    readonly DataGridView finderItems=Grid(),finderListings=Grid();
    readonly Label finderStatus=Info(55),finderEvidence=Info(65);
    bool finderReady;int finderGeneration;
    FinderResult? FinderSelection=>finderItems.SelectedRows.Count>0?finderItems.SelectedRows[0].Tag as FinderResult:null;
    object? FinderActionItem=>finderListings.SelectedRows.Count>0?finderListings.SelectedRows[0].Tag:FinderSelection is {} item?new IconCatalog.Entry(item.Id,item.Name):null;
    void BuildItemFinder()
    {
        finderCategory.AccessibleName="Item category";finderSource.AccessibleName="Listing source";
        foreach(var box in new[]{finderCategory,finderSource}){
            box.BackColor=Surface;box.ForeColor=TextColor;box.DrawMode=DrawMode.OwnerDrawFixed;box.ItemHeight=20;
            box.DrawItem+=(_,e)=>{e.DrawBackground();int index=e.Index>=0?e.Index:box.SelectedIndex;if(index>=0)TextRenderer.DrawText(e.Graphics,box.Items[index]?.ToString(),box.Font,e.Bounds,e.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);e.DrawFocusRectangle();};
        }
        finderCategory.Items.AddRange(["All","Equipment","Use","Set-up","Etc","Cash","Unknown"]);finderCategory.SelectedIndex=0;
        finderSource.Items.Add("All");finderSource.Items.AddRange(settings.Sources.Select(s=>s.Id).Concat(observations.Select(o=>o.Provider)).Distinct().Cast<object>().ToArray());finderSource.SelectedIndex=0;
        var bar=Toolbar();bar.Height=48;bar.Controls.Add(finderSearch);bar.Controls.Add(finderCategory);bar.Controls.Add(finderSource);bar.Controls.Add(finderListed);
        var actions=Toolbar();actions.Height=92;actions.WrapContents=true;actions.AutoScroll=false;
        actions.Controls.Add(Button("Monster drops & maps",()=>{if(FinderSelection is {} item){if(Offline){MessageBox.Show("Turn off Work offline to look up monster drops and maps.","Offline");return;}var viewer=new WebsiteViewer(MonsterReference.ForItem(item.Name),dataDirectory);viewer.Show(this);}}));
        actions.Controls.Add(Button("Check prices",()=>{if(FinderSelection is {} item){search.Text=item.Name;ShowPage("Price Checker");}}));
        actions.Controls.Add(Button("Watch item",()=>{if(FinderActionItem is {} item)WatchObject(item);}));
        actions.Controls.Add(Button("Price alert",()=>{if(FinderActionItem is {} item)EditRule(null,item);}));
        actions.Controls.Add(Button("Compare",()=>{if(FinderActionItem is {} item)PinComparison(item);}));
        actions.Controls.Add(Button("Open listing",()=>{if(finderListings.SelectedRows.Count>0&&finderListings.SelectedRows[0].Tag is Observation o)Open(o.Url);}));
        actions.Controls.Add(Button("Item websites",()=>{if(FinderActionItem is {} item)ShowItemWebsites(ItemName(item),item);}));
        actions.Controls.Add(Button("Refresh sources",async ()=>{await RefreshSources();RenderItemFinder();}));
        var body=new TableLayoutPanel(){Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};body.RowStyles.Add(new(SizeType.Percent,45));body.RowStyles.Add(new(SizeType.Percent,55));
        body.Controls.Add(finderItems,0,0);var lower=new Panel(){Dock=DockStyle.Fill};lower.Controls.Add(finderListings);lower.Controls.Add(finderEvidence);body.Controls.Add(lower,0,1);
        var page=pages["Item Finder"];page.Controls.Add(body);page.Controls.Add(actions);page.Controls.Add(finderStatus);page.Controls.Add(bar);
        finderSearch.TextChanged+=(_,_)=>DebounceSearch(RenderItemFinder);finderCategory.SelectedIndexChanged+=(_,_)=>RenderItemFinder();finderSource.SelectedIndexChanged+=(_,_)=>RenderItemFinder();finderListed.CheckedChanged+=(_,_)=>RenderItemFinder();
        finderItems.SelectionChanged+=(_,_)=>RenderFinderListings();finderListings.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&finderListings.Rows[e.RowIndex].Tag is Observation o)ShowItemDetails(o);};
        finderItems.Scroll+=(_,_)=>{if(finderReady)_=LoadFinderIcons(finderGeneration);};
        finderReady=true;
    }
    void RenderItemFinder()
    {
        if(!finderReady||selected!="Item Finder")return;
        var previous=FinderSelection;
        var results=ItemFinder.Search(iconCatalog.Entries.Select(e=>new FinderIdentity(e.Id,e.Name)),observations,finderSearch.Text,finderCategory.Text,finderSource.Text,finderListed.Checked);
        finderItems.Columns.Clear();finderItems.Columns.Add(new DataGridViewImageColumn(){Name="Icon",HeaderText="",ImageLayout=DataGridViewImageCellLayout.Normal,Width=44,AutoSizeMode=DataGridViewAutoSizeColumnMode.None,DefaultCellStyle=new(){NullValue=null}});
        foreach(var name in new[]{"Item","Category","Classic ID","Known asks"})finderItems.Columns.Add(name,name);
        foreach(var item in results.Take(200)){int row=finderItems.Rows.Add(icons.Peek(item.Id)!,item.Name,item.Category,item.Id,item.Listings.Count);finderItems.Rows[row].Tag=item;}
        if(previous is not null){var row=finderItems.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>r.Tag is FinderResult item&&item.Id==previous.Id&&item.Name==previous.Name);if(row is not null){finderItems.ClearSelection();row.Selected=true;}}
        finderStatus.Text=$"{results.Count:N0} matching items • showing {Math.Min(200,results.Count):N0}; refine your search for more.\nSelect an item, then Monster drops & maps for NiaMeowDB’s in-app reference. English names only. Catalog: GMS 83; reference: Classic World. No known listings does not mean unavailable everywhere.";
        RenderFinderListings();_ = LoadFinderIcons(++finderGeneration);
    }
    async Task LoadFinderIcons(int generation)
    {
        foreach(var row in finderItems.Rows.Cast<DataGridViewRow>().Where(r=>r.Displayed).Take(32).ToArray()){
            if(closing||generation!=finderGeneration||selected!="Item Finder")return;
            if(row.Tag is not FinderResult item)continue;var icon=await icons.Get(item.Id);
            if(closing||generation!=finderGeneration||row.DataGridView is null)return;row.Cells[0].Value=icon;
        }
    }
    void RenderFinderListings()
    {
        SetColumns(finderListings,["Unit mesos","Quantity","Seller","Source","Last checked","Evidence / age","Variant"]);
        var item=FinderSelection;if(item is null){finderEvidence.Text="Select an item above to see known asking listings.";return;}
        finderEvidence.Text=item.Listings.Count==0?$"{item.Name}: no known available asking listings in the selected source. Try Check prices or Item websites.":$"{item.Name} • {item.Listings.Count} known asks (first 500 shown), cheapest first. Variants may differ.\nAvailability was recorded at the last check; asking prices are not completed sales. ~ means a rounded price.";
        foreach(var o in item.Listings.Take(500)){
            bool enabled=settings.Sources.Any(s=>s.Id==o.Provider&&s.Authorized);
            int row=finderListings.Rows.Add((o.PriceApproximate?"~ ":"")+TextKey.Mesos(o.UnitPrice),o.Quantity?.ToString("N0")??"Unknown",string.IsNullOrWhiteSpace(o.Seller)?"Unknown":o.Seller,o.Provider,o.RetrievedAt.ToLocalTime().ToString("g"),ReliabilityTools.EvidenceStatus(o,enabled,DateTimeOffset.UtcNow,settings.FreshHours),o.Equipment?(o.VariantComplete?o.Variant:"Incomplete: "+o.Variant):"Stackable");finderListings.Rows[row].Tag=o;
        }
    }
}

