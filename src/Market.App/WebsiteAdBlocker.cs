using Market.Core;
using Microsoft.Web.WebView2.Core;
namespace Market.App;

sealed class WebsiteAdBlocker : IDisposable
{
    readonly CoreWebView2 core;
    public bool Enabled {get;set;}=true;
    public int Blocked {get;private set;}
    public event Action? Changed;
    public WebsiteAdBlocker(CoreWebView2 core)
    {
        this.core=core;
        core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All,CoreWebView2WebResourceRequestSourceKinds.All);
        core.WebResourceRequested+=Requested;
    }
    void Requested(object? sender,CoreWebView2WebResourceRequestedEventArgs e)
    {
        if(!Enabled||!WebsiteAdPolicy.IsAdvertisingUrl(e.Request.Uri))return;
        e.Response=core.Environment.CreateWebResourceResponse(null,403,"Blocked by companion","Content-Type: text/plain\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *");
        Count();
    }
    public bool BlockPopup(string url){if(!Enabled||!WebsiteAdPolicy.IsAdvertisingUrl(url))return false;Count();return true;}
    void Count(){Blocked++;Changed?.Invoke();}
    public void Reset(){Blocked=0;Changed?.Invoke();}
    public void Dispose(){core.WebResourceRequested-=Requested;}
}
