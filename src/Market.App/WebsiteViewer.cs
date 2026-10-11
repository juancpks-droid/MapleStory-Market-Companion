using Market.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Text.Json;

namespace Market.App;

// A user-opened website window. No host objects, price extraction, or background scans.
sealed class WebsiteViewer : Form
{
    readonly WebView2 web=new(){Dock=DockStyle.Fill};
    readonly Label status=new(){Dock=DockStyle.Bottom,Height=48,Padding=new Padding(8),AutoEllipsis=true};
    readonly TextBox address=new(){Dock=DockStyle.Top,ReadOnly=true,Height=28,AccessibleName="Website address"};
    readonly WebsiteLookup lookup;
    readonly string dataDirectory;
    readonly Button back=new(){Text="Back",AutoSize=true,Enabled=false};
    readonly Button forward=new(){Text="Forward",AutoSize=true,Enabled=false};
    readonly CheckBox blockAds=new(){Text="Block ads",AutoSize=true,Checked=true};
    readonly Label blockedCount=new(){AutoSize=true,Padding=new Padding(8,4,0,0)};
    WebsiteAdBlocker? adBlocker;
    string AdPreference=>Path.Combine(dataDirectory,"website-ad-blocking.txt");
    bool searched;
    public TaskCompletionSource<bool> Ready {get;}=new();
    public string CurrentUrl=>web.CoreWebView2?.Source??lookup.Url;
    public string ViewerStatus=>status.Text;
    public WebsiteViewer(WebsiteLookup lookup,string dataDirectory)
    {
        this.lookup=lookup;this.dataDirectory=dataDirectory;
        Text=lookup.Name+" • "+(lookup.ItemName.Length>0?lookup.ItemName:"Website");Size=new(1120,800);MinimumSize=new(800,600);StartPosition=FormStartPosition.CenterParent;
        var bar=new FlowLayoutPanel(){Dock=DockStyle.Top,Height=42,AutoScroll=true,WrapContents=false,Padding=new Padding(5)};
        void Add(string label,Action action){var b=new Button(){Text=label,AutoSize=true};b.Click+=(_,_)=>action();bar.Controls.Add(b);}
        bar.Controls.Add(back);bar.Controls.Add(forward);back.Click+=(_,_)=>{if(web.CanGoBack)web.GoBack();};forward.Click+=(_,_)=>{if(web.CanGoForward)web.GoForward();};
        Add("Reload",()=>web.Reload());Add("Open in browser",()=>OpenBrowser(CurrentUrl));Add("Copy item name",()=>{if(lookup.ItemName.Length>0){try{Clipboard.SetText(lookup.ItemName);}catch{status.Text="Clipboard busy. Try Copy item name again.";}}});
        if(lookup.SourceUrl is {} original)Add("Selected listing",()=>web.CoreWebView2?.Navigate(original));
        Add("WebView2 help",()=>OpenBrowser("https://developer.microsoft.com/en-us/microsoft-edge/webview2/"));
        try{blockAds.Checked=!File.Exists(AdPreference)||File.ReadAllText(AdPreference).Trim()!="off";}catch{}
        var protection=new FlowLayoutPanel(){Dock=DockStyle.Top,Height=34,Padding=new Padding(8,2,0,0),WrapContents=false};
        protection.Controls.Add(blockAds);protection.Controls.Add(blockedCount);
        protection.Controls.Add(new Label(){Text="Common ad domains • switch off if a site breaks",AutoSize=true,Padding=new Padding(12,4,0,0)});
        blockedCount.Text=blockAds.Checked?"0 blocked this page":"Off";
        blockAds.CheckedChanged+=(_,_)=>
        {
            try{Directory.CreateDirectory(dataDirectory);File.WriteAllText(AdPreference+".tmp",blockAds.Checked?"on":"off");File.Move(AdPreference+".tmp",AdPreference,true);}catch{status.Text="Could not save ad-block preference. It still applies to this window.";}
            if(adBlocker is not null){adBlocker.Enabled=blockAds.Checked;adBlocker.Reset();web.Reload();}
        };
        var note=new Label(){Dock=DockStyle.Top,Height=64,Padding=new Padding(8),Text=$"Item: {lookup.ItemName}\nWebsite view only • These prices do not enter companion estimates. {lookup.Hint}"};
        Controls.Add(web);Controls.Add(status);Controls.Add(address);Controls.Add(note);Controls.Add(protection);Controls.Add(bar);address.Text=lookup.Url;status.Text="Opening website…";
        Shown+=async(_,_)=>await Initialize();
        FormClosed+=(_,_)=>{Ready.TrySetResult(false);adBlocker?.Dispose();web.Dispose();};
    }
    static void OpenBrowser(string url){if(WebsiteLookup.SafeUrl(url))try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch{MessageBox.Show("Windows could not open the browser. Copy the website address and open it manually.");}}
    async Task Initialize()
    {
        try
        {
            var environment=await CoreWebView2Environment.CreateAsync(userDataFolder:Path.Combine(dataDirectory,"website-browser"));
            if(IsDisposed)return;
            await web.EnsureCoreWebView2Async(environment);if(IsDisposed)return;
            var core=web.CoreWebView2;core.Settings.AreHostObjectsAllowed=false;core.Settings.IsWebMessageEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
            adBlocker=new WebsiteAdBlocker(core){Enabled=blockAds.Checked};
            adBlocker.Changed+=()=>blockedCount.Text=adBlocker.Enabled?$"{adBlocker.Blocked} blocked this page":"Off";
            core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            core.DownloadStarting+=(_,e)=>{e.Cancel=true;status.Text="Use Open in browser for downloads.";};
            core.NavigationStarting+=(_,e)=>{if(!WebsiteLookup.SafeUrl(e.Uri)){e.Cancel=true;status.Text="This link cannot open here. Use the website in your browser.";}else{adBlocker.Reset();address.Text=e.Uri;status.Text="Loading…";}};
            core.NewWindowRequested+=(_,e)=>{e.Handled=true;if(!adBlocker.BlockPopup(e.Uri)&&e.IsUserInitiated&&WebsiteLookup.SafeUrl(e.Uri))core.Navigate(e.Uri);};
            core.HistoryChanged+=(_,_)=>{back.Enabled=core.CanGoBack;forward.Enabled=core.CanGoForward;};
            core.SourceChanged+=(_,_)=>address.Text=core.Source;
            core.ProcessFailed+=(_,_)=>status.Text="The website viewer stopped. Close this window and reopen it, or use Open in browser.";
            core.NavigationCompleted+=async(_,e)=>
            {
                if(IsDisposed)return;
                status.Text=e.IsSuccess?"Website loaded. Prices, sign-in requirements and availability are controlled by this website.":"Page could not load. Try Reload or Open in browser. "+e.WebErrorStatus;
                if(e.IsSuccess&&lookup.Mode!=WebsiteSearchMode.None&&!searched&&lookup.AtSearchPage(core.Source)){searched=true;await LookupItem();}
                Ready.TrySetResult(e.IsSuccess);
            };
            core.Navigate(lookup.Url);
        }
        catch(Exception e)
        {
            if(IsDisposed)return;
            web.Visible=false;status.Height=100;status.Text="In-app browsing is unavailable. Install or update Microsoft Edge WebView2 Runtime using WebView2 help, then reopen this window. You can also use Open in browser. ("+e.GetType().Name+")";Ready.TrySetResult(false);
        }
    }
    public string LookupResult {get;private set;}="not-started";
    async Task LookupItem()
    {
        try
        {
            status.Text="Finding "+lookup.ItemName+" on "+lookup.Name+"…";bool filled=false;
            // Pre-filtered URLs on these sites need only an exact result selection.
            bool urlSearch=lookup.Mode is WebsiteSearchMode.MapleBuddy or WebsiteSearchMode.GameMargins;
            for(int i=0;i<60&&!IsDisposed&&lookup.AtSearchPage(CurrentUrl);i++)
            {
                if(!urlSearch&&(!filled||i==8||i==20))
                {
                    var fill=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync(WebsiteSearchScripts.Fill(lookup.Mode,lookup.ItemName,filled)));
                    if(fill=="user-changed"){LookupResult="user-changed";status.Text="You changed the search. Automatic item selection stopped.";return;}
                    filled=fill=="filled";
                    if(filled&&!WebsiteLookup.ExactLookup(lookup.Mode)){LookupResult="search-only";status.Text="Item name filled. Choose a result on the website if one is available; no exact item view was verified.";return;}
                }
                else
                {
                    var result=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync(WebsiteSearchScripts.Match(lookup.Mode,lookup.ItemName)));
                    if(result=="opened"){LookupResult="opened";status.Text="Opened the exact-name match. Check equipment stats and the website's price evidence.";return;}
                    if(result=="ambiguous"){LookupResult="ambiguous";status.Text="More than one exact-name result. Choose the correct item on the website.";return;}
                }
                await Task.Delay(250);
            }
            if(!IsDisposed){LookupResult="not-found";status.Text="No unique exact item was opened. Use the website search, Copy item name, or Open in browser.";}
        }
        catch{if(!IsDisposed){LookupResult="unavailable";status.Text="Use the website search or Open in browser. No price was imported.";}}
    }
    internal Task<string> ReadHeadings()=>web.ExecuteScriptAsync("[...document.querySelectorAll('h1,h2,h3,[role=dialog]')].filter(e=>e.getClientRects().length).map(e=>e.getAttribute('aria-label')||e.textContent)");
    internal Task<string> InspectPage()=>web.ExecuteScriptAsync("JSON.stringify({text:document.body.innerText,inputs:[...document.querySelectorAll('input')].map(x=>({label:x.getAttribute('aria-label'),value:x.value})),links:[...document.querySelectorAll('a[href]')].filter(a=>a.textContent.includes('Blue Mushroom')).map(a=>({text:a.textContent,href:a.getAttribute('href')}))})");
    internal async Task CapturePage(string path)
    {
        if(web.CoreWebView2 is null)throw new Exception("WebView2 did not initialize");
        using var stream=File.Create(path);await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
    }
}

public sealed partial class MainForm
{
    void ShowWebsite(string siteName,string name,object? item=null)
    {
        if(Offline){MessageBox.Show("Turn off Work offline before opening a website.","Offline");return;}
        var site=WebsiteDirectory.All.Single(s=>s.Name==siteName);
        using var dialog=new WebsiteViewer(WebsiteLookup.Create(site,name,Listing(item)?.Url),dataDirectory);dialog.ShowDialog(this);
    }
    void ShowItemWebsites(string name,object? item=null)
    {
        // A modal picker supports keyboard and mouse navigation.
        using var picker=new Form(){Text="View item on a website",Size=new(630,530),StartPosition=FormStartPosition.CenterParent};
        var list=new ListBox(){Dock=DockStyle.Fill};list.Items.AddRange(WebsiteDirectory.All.Select(s=>s.Name+" — "+WebsiteLookup.Capability(s.Name)).ToArray());list.SelectedIndex=3;
        var open=Button("Open item / website",()=>{if(list.SelectedIndex>=0)ShowWebsite(WebsiteDirectory.All[list.SelectedIndex].Name,name,item);});open.Dock=DockStyle.Bottom;
        var note=new Label(){Dock=DockStyle.Top,Height=64,Padding=new Padding(8),Text=name+"\nExact lookups open a unique matching item. Other sites offer search fallbacks. Equipment variants and price coverage depend on the website."};
        picker.Controls.Add(list);picker.Controls.Add(open);picker.Controls.Add(note);list.DoubleClick+=(_,_)=>open.PerformClick();picker.ShowDialog(this);
    }
    void BuildWebsiteLookup()
    {
        var bar=pages["Price Checker"].Controls.OfType<FlowLayoutPanel>().First();bar.Height=110;bar.WrapContents=true;
        object? Current()=>priceGrid.SelectedRows.Count>0?priceGrid.SelectedRows[0].Tag:null;
        string Name()=>Current() is {} item?ItemName(item):search.Text.Trim();
        void Lookup(bool nia){var name=Name();if(name.Length==0){footer.Text="Search for or select an item first.";return;}if(nia)ShowWebsite("NiaMeowDB",name,Current());else ShowItemWebsites(name,Current());}
        bar.Controls.Add(Button("View on NiaMeowDB",()=>Lookup(true)));bar.Controls.Add(Button("Other websites",()=>Lookup(false)));
    }
}
