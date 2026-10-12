using System.Globalization;

using System.Text;

using System.Text.Json;

using System.Text.Json.Serialization;

using System.Text.RegularExpressions;



namespace Market.Core;



public enum EvidenceKind { Asking, ReportedSale, ConfirmedSale, CommunityEstimate, Buying, HistoricalAsk }



public sealed record Observation

{

    public string Provider { get; init; } = "";

    public string ListingId { get; init; } = "";

    public string ItemId { get; init; } = "";

    public string Name { get; init; } = "";

    public string Variant { get; init; } = "";

    public bool Equipment { get; init; }

    public bool VariantComplete { get; init; }

    public decimal UnitPrice { get; init; }

    public long? Quantity { get; init; }

    public EvidenceKind Kind { get; init; }

    public DateTimeOffset? ObservedAt { get; init; }

    public DateTimeOffset RetrievedAt { get; init; }

    public string SourceAge { get; init; } = "Unknown";

    public string Seller { get; init; } = "";
    public string Note { get; init; } = "";

    public string Url { get; init; } = "";

    public string OriginalReference { get; init; } = "";

    public bool Available { get; init; }

    public bool PriceApproximate { get; init; }

    public string Verification { get; init; } = "";

    [JsonIgnore] public string Key => Provider + ":" + ListingId;

    [JsonIgnore] public string ItemKey => TextKey.Normalize(Name);

    [JsonIgnore] public string MatchKey => ItemKey + "|" + (Equipment ? Variant : "stackable");

    public bool Fresh(DateTimeOffset now, TimeSpan age) => ObservedAt is { } t && t <= now.AddMinutes(2) && t >= now - age && RetrievedAt <= now.AddMinutes(2);

    public string Freshness(DateTimeOffset now, TimeSpan age) => ObservedAt is null ? "Source time unknown" : Fresh(now, age) ? "Within age limit" : "Stale data";

    public void Validate()

    {

        if (string.IsNullOrWhiteSpace(Provider) || string.IsNullOrWhiteSpace(ListingId) || string.IsNullOrWhiteSpace(Name) || UnitPrice <= 0 || Quantity <= 0 || !Enum.IsDefined(Kind)) throw new FormatException("Observation has missing identity, invalid type, price or quantity.");

        if (!Uri.TryCreate(Url, UriKind.Absolute, out var u) || u.Scheme != "https") throw new FormatException("Source links must use HTTPS.");

        if (Equipment && VariantComplete && string.IsNullOrWhiteSpace(Variant)) throw new FormatException("Equipment requires a complete variant signature.");

    }

}



public static class TextKey
{
    public static string Search(string value)=>Regex.Replace(Normalize(value).Replace("\'",""),"[^a-z0-9]+"," ").Trim();

    public static string Normalize(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormKC).Replace('’', '\'').Trim().ToLowerInvariant(), @"\s+", " ");

    public static decimal ParsePrice(string value)

    {

        var m = Regex.Match(value.Trim(), @"^(\d[\d,]*(?:\.\d+)?)\s*([kKmM]?)$");

        if (!m.Success) throw new FormatException("Unrecognized price format");

        var n = decimal.Parse(m.Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture);

        n *= m.Groups[2].Value.ToLowerInvariant() switch { "k" => 1000, "m" => 1000000, _ => 1 };

        if (n <= 0) throw new FormatException("Price must be positive");

        return n;

    }

    public static decimal PerUnit(decimal total, long count) => count > 0 && total > 0 ? total / count : throw new FormatException("Invalid stack");

    public static string Mesos(decimal? n) => n?.ToString("N0", CultureInfo.CurrentCulture) ?? "Price unavailable";

}



public sealed class SourceConfig

{

    public string Id { get; set; } = "";

    public bool Enabled { get; set; }

    public string PermissionReference { get; set; } = "";

    public int IntervalMinutes { get; set; } = 15;

    public string FeedUrl { get; set; } = "";

    public DateTimeOffset NextAllowed { get; set; }

    public DateTimeOffset LastChecked { get; set; }

    public DateTimeOffset BlockedUntil { get; set; }

    public Dictionary<string,DateTimeOffset> PageChecked { get; set; }=[];

    public List<DateTimeOffset> RequestTimes { get; set; }=[];
    public Dictionary<string,BasilCheck> BasilChecks {get;set;}=[];
    public List<string> BasilPages {get;set;}=[];
    public Dictionary<string,DateTimeOffset> BasilVisited {get;set;}=[];

    public bool BasilWasComplete {get;set;}
    public string BasilCompletionKey {get;set;}="";
    public string BasilAnnouncedKey {get;set;}="";
    public bool Authorized => Enabled && !string.IsNullOrWhiteSpace(PermissionReference);

}

public sealed class Settings

{

    public int AccessVersion { get; set; }

    public int LocalSeconds { get; set; } = 10;

    public int FreshHours { get; set; } = 72;

    public decimal Discount { get; set; } = 25;

    public decimal MinimumProfit { get; set; } = 1000;

    public int Confidence { get; set; } = 50;

    public bool Notifications { get; set; } = true;

    public bool BasilCompletionNotifications {get;set;}=true;

    public bool Sound { get; set; }

    public bool WatchOnly { get; set; } = true;

    public bool CloseToTray { get; set; }

    public DateTimeOffset SnoozedUntil { get; set; }

    public List<SourceConfig> Sources { get; set; } = [new() { Id = "BasilMarket", Enabled=true, PermissionReference=BasilPages.AccessReference }, new() { Id = "Kerning Exchange", Enabled=true, PermissionReference="https://kerning.gg/robots.txt — public pages allowed; policy checked before each batch" }, new() { Id = "Approved JSON feed" }];

    public void Sanitize()

    {

        LocalSeconds = LocalSeconds == 5 ? 5 : 10; FreshHours = Math.Clamp(FreshHours, 1, 168);

        Discount = Math.Clamp(Discount, 1, 95); Confidence = Math.Clamp(Confidence, 1, 100); MinimumProfit = Math.Max(0, MinimumProfit);

        foreach (var s in Sources) s.IntervalMinutes = Math.Clamp(s.IntervalMinutes, 15, 1440);

    }

}

public sealed record SavedItem(string Name, long Quantity, bool Watch, bool Muted) { public string Variant {get;init;}=""; public override string ToString()=>Name+(Variant.Length>0?" • "+Variant:""); }

public static class Json

{

    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

}
