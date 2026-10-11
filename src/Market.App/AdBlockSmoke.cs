using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text;
using System.Text.Json;
namespace Market.App;
static class AdBlockSmoke
{
    public static void Run()
    {
        string folder=Path.Combine(AppContext.BaseDirectory,"adblock-smoke-data");Directory.CreateDirectory(folder);
        using var host=new Form{Text="Ad-block checks",Width=900,Height=650,ShowInTaskbar=false};
        var web=new WebView2{Dock=DockStyle.Fill};host.Controls.Add(web);
        host.Shown+=async(_,_)=>
        {
            var checks=new List<string>();string? error=null;
            try
            {
                await web.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(userDataFolder:Path.Combine(folder,"browser")));
                var core=web.CoreWebView2;using var blocker=new WebsiteAdBlocker(core);
                // All fixture traffic is intercepted locally, including ads; no remote requests.
                core.WebResourceRequested+=(_,e)=>{if(e.Response is null)e.Response=core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("fixture")),200,"OK","Content-Type: text/plain\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-store");};
                var loaded=new TaskCompletionSource();core.NavigationCompleted+=(_,_)=>loaded.TrySetResult();web.NavigateToString("<html><body><h1>Item search fixture</h1><input aria-label='Search priced items'></body></html>");await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
                async Task Request(string url,int expected)
                {
                    await web.ExecuteScriptAsync("window.result=null;fetch("+JsonSerializer.Serialize(url)+").then(r=>window.result=r.status).catch(()=>window.result=-1)");
                    for(int i=0;i<100;i++){var result=await web.ExecuteScriptAsync("window.result");if(result!="null"){if(result!=expected.ToString())throw new Exception(url+": "+result+" expected "+expected);return;}await Task.Delay(50);}throw new Exception("Request timed out");
                }
                await Request("https://pagead2.googlesyndication.com/ad-test",403);if(blocker.Blocked!=1)throw new Exception("Counter failed");checks.Add("Advertising request blocked before fixture transport; counter increments");
                await Request("https://fixture.example/item-search",200);await Request("https://challenges.cloudflare.com/fixture",200);checks.Add("Ordinary item and challenge requests allowed");
                blocker.Enabled=false;await Request("https://pagead2.googlesyndication.com/ad-test-off",200);if(blocker.Blocked!=1)throw new Exception("Disabled blocker counted requests");checks.Add("Switching protection off allows same advertising host");
                blocker.Enabled=true;blocker.Reset();await Request("https://ads.doubleclick.net/new-page",403);if(blocker.Blocked!=1)throw new Exception("Reset/re-enable failed");checks.Add("Re-enable and page counter reset work");
                if(!blocker.BlockPopup("https://doubleclick.net/popup")||blocker.BlockPopup("https://fixture.example/item"))throw new Exception("Popup policy failed");checks.Add("Ad popups blocked; normal item popups allowed by policy");
                var fill=await web.ExecuteScriptAsync(WebsiteSearchScripts.Fill(Market.Core.WebsiteSearchMode.Nia,"Blue Mushroom Cap"));if(fill!="\"filled\"")throw new Exception("Item search failed");checks.Add("Existing item search still fills while protection is enabled");
                CheckBox Toggle(WebsiteViewer viewer)=>viewer.Controls.OfType<FlowLayoutPanel>().SelectMany(p=>p.Controls.OfType<CheckBox>()).Single();
                var lookup=new Market.Core.WebsiteLookup("Fixture","https://fixture.example/","",false,"");
                string preferences=Path.Combine(folder,"preferences");
                using(var viewer=new WebsiteViewer(lookup,preferences)){Toggle(viewer).Checked=true;Toggle(viewer).Checked=false;}
                using(var viewer=new WebsiteViewer(lookup,preferences)){if(Toggle(viewer).Checked)throw new Exception("Off preference not restored");Toggle(viewer).Checked=true;}
                using(var viewer=new WebsiteViewer(lookup,preferences)){if(!Toggle(viewer).Checked)throw new Exception("On preference not restored");}
                checks.Add("Actual website-window switch persists on/off across reopened windows");
            }
            catch(Exception e){error=e.ToString();Environment.ExitCode=1;}
            finally{File.WriteAllText(Path.Combine(folder,"results.json"),JsonSerializer.Serialize(new{status=error is null?"passed":"failed",checks,error}));host.Close();}
        };
        Application.Run(host);
    }
}
