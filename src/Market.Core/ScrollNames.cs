using System.Text.Json;
using System.Text.RegularExpressions;

namespace Market.Core;

// Source IDs belong to their own game version. Never join GMS artwork IDs to
// Classic market records by number. Rates below come from source descriptions.
public static class ScrollNames
{
    public sealed record Reference(string Name,string ClassicId,int Rate,string Source);
    public sealed record Legacy(string Id,string Name,int Rate);
    static T[] Read<T>(string resource) {
        using var stream=typeof(ScrollNames).Assembly.GetManifestResourceStream(resource)!;
        return JsonSerializer.Deserialize<T[]>(stream)??[];
    }
    public static readonly Reference[] References=Read<Reference>("scroll-reference");
    public static readonly Legacy[] LegacyItems=Read<Legacy>("scroll-legacy");
    static readonly Dictionary<string,Reference> byName=References.ToDictionary(x=>TextKey.Search(x.Name));
    static readonly Dictionary<string,Reference[]> byFamily=References.GroupBy(x=>Family(x.Name)).ToDictionary(g=>g.Key,g=>g.ToArray());
    public static bool IsScroll(string name)=>TextKey.Search(name).Split(' ').Contains("scroll");
    public static string LegacyName(string id,string name) {
        var item=LegacyItems.FirstOrDefault(x=>x.Id==id);
        return item is null?name:item.Name+" "+item.Rate+"%";
    }
    public static bool HasVerifiedLegacyRate(string id,string name)=>LegacyItems.Any(x=>x.Id==id)||Regex.IsMatch(name,@"\b\d+\s*%");
    static string Family(string name) {
        var s=TextKey.Search(name);
        s=Regex.Replace(s,@"\b(?:for|scroll|percent|lesser|intermediate|greater|chaos|\d+)\b"," ");
        s=Regex.Replace(s,@"\b(?:att|watk|atk)\b","attack");
        s=Regex.Replace(s,@"\b(?:matt|matk)\b","magic attack");
        s=Regex.Replace(s,@"\b(?:intelligence)\b","int");
        s=Regex.Replace(s,@"\b(?:dexterity)\b","dex");
        s=Regex.Replace(s,@"\b(?:defense|defence)\b","def");
        s=s.Replace("pole arm","polearm").Replace("1h ","one handed ").Replace("2h ","two handed ");
        return string.Join(' ',s.Split(' ',StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
    }
    static Reference? Exact(string name)=>byName.GetValueOrDefault(TextKey.Search(name));
    static int? Rate(string name) {
        var match=Regex.Match(name,@"\b(\d+)\s*(?:%|percent\b|$)",RegexOptions.IgnoreCase);
        return match.Success?(int.TryParse(match.Groups[1].Value,out var rate)?rate:-1):Exact(name)?.Rate;
    }
    public static Reference? Resolve(string name) {
        var exact=Exact(name);if(exact is not null)return exact;
        if(!IsScroll(name)||Rate(name) is not int rate)return null;
        var family=Family(name);bool chaos=TextKey.Search(name).Split(' ').Contains("chaos");
        var tier=Regex.Match(TextKey.Search(name),@"\b(?:lesser|intermediate|greater|chaos)\b").Value;
        var matches=(byFamily.GetValueOrDefault(family)??[]).Where(x=>x.Rate==rate&&x.Name.Contains("Chaos",StringComparison.OrdinalIgnoreCase)==chaos&&(tier.Length==0||TextKey.Search(x.Name).Split(' ').Contains(tier))).ToArray();
        return matches.Length==1?matches[0]:null;
    }
    public static string Query(string name)=>Resolve(name)?.Name??name;
    public static string Display(string name)=>Exact(name) is {} item?name+" ("+item.Rate+"%)":name;
    public static bool Matches(string candidate,string query) {
        if(!IsScroll(query))return TextKey.Search(candidate).Contains(TextKey.Search(query));
        if(!IsScroll(candidate))return false;
        var resolved=Resolve(query);
        if(resolved is not null)return TextKey.Search(Query(candidate))==TextKey.Search(resolved.Name);
        // Unknown rates and named special variants must never fall back to another tier.
        if(Rate(query) is not null||Regex.IsMatch(query,@"\b(?:lesser|intermediate|greater|chaos|unverified)\b",RegexOptions.IgnoreCase))return TextKey.Search(candidate)==TextKey.Search(query);
        var tokens=Family(query).Split(' ',StringSplitOptions.RemoveEmptyEntries);
        var candidateTokens=Family(candidate).Split(' ',StringSplitOptions.RemoveEmptyEntries);
        return tokens.All(candidateTokens.Contains);
    }
}
