using Market.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace Market.App;
static class WebsiteViewerSmoke
{
    public static void Run()
    {
        string directory=Path.Combine(AppContext.BaseDirectory,"viewer-smoke-data");Directory.CreateDirectory(directory);
        using var host=new Form(){Text="Website viewer checks",Size=new(1000,740)};
        host.Shown+=async(_,_)=>
        {
            var checks=new List<string>();var failures=new List<string>();
            void Save()=>File.WriteAllText(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(new{status=failures.Count==0?"passed":"failed",checks,failures},Json.Options));
            try
            {
                using(var fixture=new WebView2(){Dock=DockStyle.Fill})
                {
                    host.Controls.Add(fixture);await fixture.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(userDataFolder:Path.Combine(directory,"fixture-browser")));
                    var loaded=new TaskCompletionSource();fixture.CoreWebView2.NavigationCompleted+=(_,e)=>loaded.TrySetResult();
                    fixture.NavigateToString("<html><body><input aria-label='Search priced items'></body></html>");await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
                    var filled=await fixture.ExecuteScriptAsync(WebsiteSearchScripts.Fill(WebsiteSearchMode.Nia,"Pig's Head & 雪"));if(filled!="\"filled\"")throw new Exception("Search prefill failed");
                    var value=JsonSerializer.Deserialize<string>(await fixture.ExecuteScriptAsync("document.querySelector('input').value"));if(value!="Pig's Head & 雪")throw new Exception("Search escaping failed");
                    if(await fixture.ExecuteScriptAsync(WebsiteSearchScripts.Fill(WebsiteSearchMode.Nia,"Changed item",true))!="\"user-changed\"")throw new Exception("User search was overwritten");
                    var fixtures=new Dictionary<WebsiteSearchMode,string>{
                        [WebsiteSearchMode.Nia]="<a href='/msclassic/item-db/363'>Blue Mushroom Cap open for the price</a>",
                        [WebsiteSearchMode.Kerning]="<ul role='listbox'><li role='option'><span class='sugg-name'>Blue Mushroom Cap</span><span>Etc</span></li></ul>",
                        [WebsiteSearchMode.Basil]="<ul role='listbox'><li role='option'><button><span class='font-semibold'>Blue Mushroom Cap</span><span>50</span></button></li></ul>",
                        [WebsiteSearchMode.Mesolane]="<a role='option' href='/item/blue-mushroom-cap'><strong class='uiItemName'>Blue Mushroom Cap</strong></a>",
                        [WebsiteSearchMode.MapleBuddy]="<table><tr role='button' aria-label='Blue Mushroom Cap: open details'><td data-col='name'>Blue Mushroom Cap</td></tr></table>",
                        [WebsiteSearchMode.Metaroad]="<ul role='listbox'><li role='option'><button><span class='min-w-0'><span>Blue Mushroom Cap</span><span>Etc</span></span></button></li></ul>",
                        [WebsiteSearchMode.GameMargins]="<a href='/maplestory-classic/item/blue-mushroom-cap'>Blue Mushroom Cap</a>",
                        [WebsiteSearchMode.MesoWatch]="<button class='item-select'>Blue Mushroom Cap</button>"};
                    foreach(var (mode,html) in fixtures)
                    {
                        await fixture.ExecuteScriptAsync("document.body.innerHTML="+JsonSerializer.Serialize(html));
                        if(await fixture.ExecuteScriptAsync(WebsiteSearchScripts.Match(mode,"Blue Mushroom Cap",false))!="\"opened\"")throw new Exception(mode+" exact match failed");
                        if(await fixture.ExecuteScriptAsync(WebsiteSearchScripts.Match(mode,"Blue Mushroom",false))!="\"waiting\"")throw new Exception(mode+" partial-name match accepted");
                        var other=html.Replace("363","444").Replace("/item/blue-mushroom-cap","/item/second-blue-mushroom-cap");
                        await fixture.ExecuteScriptAsync("document.body.insertAdjacentHTML('beforeend',"+JsonSerializer.Serialize(other)+")");
                        if(await fixture.ExecuteScriptAsync(WebsiteSearchScripts.Match(mode,"Blue Mushroom Cap",false))!="\"ambiguous\"")throw new Exception(mode+" ambiguous name accepted");
                    }
                    checks.Add("All eight website selectors reject partial and ambiguous names; search escaping and user-edited search preservation verified");host.Controls.Remove(fixture);
                }
                foreach(var siteName in new[]{"NiaMeowDB","Kerning Exchange","BasilMarket","MESOLANE","MapleBuddy","Metaroad","GameMargins","MesoWatch"})
                {
                    var site=WebsiteDirectory.All.Single(s=>s.Name==siteName);
                    using var viewer=new WebsiteViewer(WebsiteLookup.Create(site,"Blue Mushroom Cap"),directory);
                    try
                    {
                        File.WriteAllText(Path.Combine(directory,"progress.txt"),siteName);viewer.Show(host);
                        if(!await viewer.Ready.Task.WaitAsync(TimeSpan.FromSeconds(50)))throw new Exception(viewer.ViewerStatus);
                        var until=DateTime.UtcNow.AddSeconds(20);bool verified=false;
                        while(DateTime.UtcNow<until)
                        {
                            var headings=JsonSerializer.Deserialize<string[]>(await viewer.ReadHeadings())??[];
                            if(viewer.LookupResult=="opened"&&headings.Any(h=>h.Trim()=="Blue Mushroom Cap")){verified=true;break;}
                            await Task.Delay(250);
                        }
                        await viewer.CapturePage(Path.Combine(directory,siteName.Replace(" ","-")+".png"));
                        File.WriteAllText(Path.Combine(directory,siteName.Replace(" ","-")+"-page.json"),await viewer.InspectPage());
                        if(!verified)throw new Exception(viewer.LookupResult+" at "+viewer.CurrentUrl+"; "+viewer.ViewerStatus);
                        checks.Add(siteName+": live exact Blue Mushroom Cap view opened and item heading verified at "+viewer.CurrentUrl);
                    }
                    catch(Exception e){failures.Add(siteName+": "+e.Message);}
                    finally{viewer.Close();File.WriteAllText(Path.Combine(directory,"progress-results.json"),JsonSerializer.Serialize(new{checks,failures},Json.Options));}
                }
                Save();
            }
            catch(Exception e){failures.Add(e.ToString());Save();}
            finally{host.Close();}
        };
        Application.Run(host);
    }
}
