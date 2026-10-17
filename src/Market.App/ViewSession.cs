using Market.Core;
using System.Text.Json;
namespace Market.App;
public sealed partial class MainForm
{
    sealed class ViewSession
    {
        public string Page {get;set;}="Dashboard";
        public string GuidePath {get;set;}=""; public string GuideItem {get;set;}="";
        public Dictionary<string,string> Values {get;set;}=[];
        public int Days {get;set;}=7;
        public string? DisplayedPriceQuery {get;set;}
        public int PriceDetailTab {get;set;} public string PriceItem {get;set;}=""; public int FinderDetailTab {get;set;} public int SettingsSection {get;set;}
        public string Bag {get;set;}="ALL";
        public int Width {get;set;}=1220;
        public int Height {get;set;}=840;
        public bool Maximized {get;set;}
    }
    readonly System.Windows.Forms.Timer sessionTimer=new(){Interval=5000};
    string SessionPath=>Path.Combine(dataDirectory,"view-session.json");
    string lastSessionJson="";
    Dictionary<string,Control> SessionControls()=>new(){
        ["accuracyLevel"]=accuracyLevel,["accuracyAcc"]=accuracyAcc,["accuracyPotion"]=accuracyPotion,["accuracyMobLevel"]=accuracyMobLevel,["accuracyAvoid"]=accuracyAvoid,["accuracyMonster"]=accuracyMonster,["sellerSearch"]=sellerSearch,["sellerRecent"]=sellerRecent,["sellerSort"]=sellerSort,["watchSearch"]=watchSearch,["watchFilter"]=watchFilter,["listingFocus"]=listingFocus,["listingText"]=listingText,["listingSide"]=listingSide,["listingQuantity"]=listingQuantity,["listingExact"]=listingExact,["listingSource"]=listingSource,["listingRecent"]=listingRecent,["listingCompact"]=listingCompact,["listingSort"]=listingSort,["listingMin"]=listingMin,["listingMax"]=listingMax,["dashboardSearch"]=quickSearch,["priceSearch"]=search,["fmSearch"]=fmSearch,
        ["finderDropSource"]=finderDropSource,["finderSearch"]=finderSearch,["finderCategory"]=finderCategory,["finderSource"]=finderSource,["finderListed"]=finderListed,
        ["inventorySearch"]=inventorySearch,["inventorySort"]=inventorySort,["catalogSearch"]=catalogSearch,["catalogCategory"]=catalogCategory
    };
    void SaveViewSession()
    {
        var bounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
        var state=new ViewSession{GuidePath=guidePath,GuideItem=guideSelection,Page=selected,PriceDetailTab=priceDetails.SelectedIndex,PriceItem=priceItemChoice.SelectedItem as string??"",DisplayedPriceQuery=displayedPriceQuery,SettingsSection=settingsSections.SelectedIndex,FinderDetailTab=finderDetailTabs.SelectedIndex,Days=historyDays,Bag=inventoryCategory,Width=bounds.Width,Height=bounds.Height,Maximized=WindowState==FormWindowState.Maximized};
        foreach(var (key,control) in SessionControls())state.Values[key]=control is CheckBox check?check.Checked.ToString():control.Text;
        var json=JsonSerializer.Serialize(state);
        if(json==lastSessionJson)return;
        File.WriteAllText(SessionPath+".tmp",json);
        File.Move(SessionPath+".tmp",SessionPath,true);
        lastSessionJson=json;
    }
    void InitializeViewSession()
    {
        try{
            if(File.Exists(SessionPath)){
                var state=JsonSerializer.Deserialize<ViewSession>(File.ReadAllText(SessionPath))??new();
                foreach(var (key,control) in SessionControls()){
                    if(state.Values is null||!state.Values.TryGetValue(key,out var value)||value is null)continue;
                    if(control is CheckBox check){if(bool.TryParse(value,out bool enabled))check.Checked=enabled;}
                    else if(control is ComboBox combo){int index=combo.FindStringExact(value);if(index>=0)combo.SelectedIndex=index;}
                    else if(control is NumericUpDown number){if(decimal.TryParse(value,out var parsed))number.Value=Math.Clamp(parsed,number.Minimum,number.Maximum);}
                    else control.Text=value[..Math.Min(value.Length,1000)];
                }
                displayedPriceQuery=state.DisplayedPriceQuery??ResolvePriceQuery(search.Text);priceInputPending=false;priceLookupLoading=false;
                historyDays=new[]{1,7,14,30}.Contains(state.Days)?state.Days:7;
                foreach(var (days,button) in historyButtons)button.ForeColor=days==historyDays?Accent:TextColor;
                if(inventoryTabs.ContainsKey(state.Bag??""))inventoryCategory=state.Bag!;
                var area=Screen.FromControl(this).WorkingArea;
                Size=new Size(Math.Clamp(state.Width,MinimumSize.Width,Math.Max(MinimumSize.Width,area.Width)),Math.Clamp(state.Height,MinimumSize.Height,Math.Max(MinimumSize.Height,area.Height)));
                if(state.Maximized)WindowState=FormWindowState.Maximized;
                if(GameGuideReader.ValidPath(state.GuidePath)){guidePath=state.GuidePath;guideSelection=state.GuideItem??"";}
                if(state.Page=="Deal Finder")state.Page="Seller Finder";
                if(state.Page is not null&&pages.ContainsKey(state.Page))ShowPage(state.Page);
                if(priceItemChoice.Items.Contains(state.PriceItem))priceItemChoice.SelectedItem=state.PriceItem;priceDetails.SelectedIndex=Math.Clamp(state.PriceDetailTab,0,3);
                finderDetailTabs.SelectedIndex=Math.Clamp(state.FinderDetailTab,0,finderDetailTabs.TabPages.Count-1);settingsSections.SelectedIndex=Math.Clamp(state.SettingsSection,0,1);
            }
        }catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){footer.Text="Previous view could not be restored. Your saved player data is still available.";}
        sessionTimer.Tick+=(_,_)=>{try{SaveViewSession();}catch(Exception){footer.Text="Could not save your current view. Check data-folder access before restarting.";}};
        sessionTimer.Start();
        FormClosed+=(_,_)=>{sessionTimer.Stop();sessionTimer.Dispose();};
    }
}
