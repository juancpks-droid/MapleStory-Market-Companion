namespace Market.Core;

public enum WebsiteSearchMode { None, Nia, Kerning, Basil, Mesolane, MapleBuddy, Metaroad, GameMargins, MesoWatch, Sleepywood, MesoScout }
public sealed record WebsiteLookup(string Name,string Url,string ItemName,bool SearchNia,string Hint)
{
    public string? SourceUrl {get;init;}
    public WebsiteSearchMode Mode=>ItemName.Length==0?WebsiteSearchMode.None:ModeFor(Name);
    public static WebsiteSearchMode ModeFor(string name)=>name switch
    {
        "NiaMeowDB"=>WebsiteSearchMode.Nia,"Kerning Exchange" or "Kerning FM Tracker"=>WebsiteSearchMode.Kerning,
        "BasilMarket"=>WebsiteSearchMode.Basil,"MESOLANE"=>WebsiteSearchMode.Mesolane,"MapleBuddy"=>WebsiteSearchMode.MapleBuddy,
        "Metaroad"=>WebsiteSearchMode.Metaroad,"GameMargins"=>WebsiteSearchMode.GameMargins,"MesoWatch"=>WebsiteSearchMode.MesoWatch,
        "Sleepywood"=>WebsiteSearchMode.Sleepywood,"Meso Scout"=>WebsiteSearchMode.MesoScout,_=>WebsiteSearchMode.None
    };
    public static bool ExactLookup(WebsiteSearchMode mode)=>mode is not (WebsiteSearchMode.None or WebsiteSearchMode.Sleepywood or WebsiteSearchMode.MesoScout);
    public static string Capability(string name)=>ExactLookup(ModeFor(name))?"Exact item lookup":ModeFor(name)==WebsiteSearchMode.None?"Manual website search":"Prefilled search; exact item unavailable";
    public static bool SafeUrl(string? url)=>Uri.TryCreate(url,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.UserInfo.Length==0&&!u.IsLoopback;
    public bool AtSearchPage(string url)=>SafeUrl(url)&&new Uri(url).Host==new Uri(Url).Host&&new Uri(url).AbsolutePath==new Uri(Url).AbsolutePath;
    public static WebsiteLookup Create(Website site,string name,string? sourceUrl=null)
    {
        name=name.Trim();var mode=ModeFor(site.Name);var query=Uri.EscapeDataString(name);
        var source=SafeUrl(sourceUrl)&&new Uri(sourceUrl!).Host==new Uri(site.Url).Host?sourceUrl:null;
        var url=name.Length==0?site.Url:mode switch
        {
            WebsiteSearchMode.Nia=>"https://meowdb.com/msclassic/free-market",
            WebsiteSearchMode.Kerning=>"https://kerning.gg/",
            WebsiteSearchMode.Basil=>"https://basilmarket.net/market/sell?q="+query,
            WebsiteSearchMode.MapleBuddy=>"https://maplebuddy.co/library/?set=all&q="+query,
            WebsiteSearchMode.GameMargins=>"https://gamemargins.com/maplestory-classic/items?q="+query,
            _=>site.Url
        };
        var hint=ExactLookup(mode)?"Opens a unique exact-name item match. Missing or ambiguous matches stay in search; equipment stats still need checking.":mode==WebsiteSearchMode.None?"Use the website's search. An exact item route has not been verified.":"Searches the item name. This site's exact item view is not currently verified; results may be unavailable.";
        return new(site.Name,url,name,mode==WebsiteSearchMode.Nia&&name.Length>0,hint){SourceUrl=source};
    }
}
