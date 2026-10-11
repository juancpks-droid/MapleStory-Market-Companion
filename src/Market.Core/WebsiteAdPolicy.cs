namespace Market.Core;

// A small, bundled network blocklist, not a full browser-extension filter engine.
public static class WebsiteAdPolicy
{
    static readonly HashSet<string> Hosts=new(StringComparer.OrdinalIgnoreCase)
    {
        "doubleclick.net","googlesyndication.com","googleadservices.com","adservice.google.com",
        "adnxs.com","adsrvr.org","amazon-adsystem.com","criteo.com","criteo.net",
        "rubiconproject.com","pubmatic.com","openx.net","casalemedia.com","indexww.com",
        "smartadserver.com","advertising.com","adform.net","taboola.com","outbrain.com",
        "revcontent.com","mgid.com","media.net","yieldmo.com","sharethrough.com",
        "33across.com","triplelift.com","sovrn.com","lijit.com","undertone.com",
        "monumetric.com","adthrive.com","mediavine.com","ezoic.net"
    };
    public static bool IsAdvertisingUrl(string? url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not ("https" or "http"))return false;
        string host=uri.IdnHost.TrimEnd('.');
        while(host.Length>0){if(Hosts.Contains(host))return true;int dot=host.IndexOf('.');if(dot<0)break;host=host[(dot+1)..];}
        return false;
    }
}
