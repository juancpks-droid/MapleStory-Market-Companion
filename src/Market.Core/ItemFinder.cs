namespace Market.Core;

public sealed record FinderIdentity(string Id,string Name);
public sealed record FinderResult(string Id,string Name,string Category,IReadOnlyList<Observation> Listings);
public static class ItemFinder
{
    public static string Category(string id)=>id.FirstOrDefault() switch{'1'=>"Equipment",'2'=>"Use",'3'=>"Set-up",'4'=>"Etc",'5'=>"Cash",_=>"Unknown"};
    public static IReadOnlyList<FinderResult> Search(IEnumerable<FinderIdentity> catalog,IEnumerable<Observation> observations,string query,string category="All",string provider="All",bool listedOnly=false)
    {
        var identities=catalog.Distinct().ToArray();
        var groups=identities.GroupBy(i=>TextKey.Search(i.Name)).ToDictionary(g=>g.Key,g=>g.ToArray());
        var listings=observations.Where(o=>o.Available&&o.Kind==EvidenceKind.Asking&&(provider=="All"||o.Provider==provider))
            .GroupBy(o=>TextKey.Search(o.Name)).ToDictionary(g=>g.Key,g=>g.OrderBy(o=>o.UnitPrice).ToArray());
        var all=identities.Concat(listings.Where(g=>!groups.ContainsKey(g.Key)).Select(g=>new FinderIdentity("",g.Value[0].Name)));
        var tokens=TextKey.Search(query).Split(' ',StringSplitOptions.RemoveEmptyEntries);
        return all.Where(i=>tokens.All(t=>TextKey.Search(i.Name).Contains(t)||i.Id.Contains(t))&&(category=="All"||Category(i.Id)==category))
            .Select(i=>new FinderResult(i.Id,i.Name,Category(i.Id),listings.GetValueOrDefault(TextKey.Search(i.Name),[])
                .Where(o=>!groups.TryGetValue(TextKey.Search(i.Name),out var matches)||matches.Length==1||o.ItemId==i.Id).ToArray()))
            .Where(i=>!listedOnly||i.Listings.Count>0)
            .OrderByDescending(i=>TextKey.Search(i.Name)==TextKey.Search(query)).ThenByDescending(i=>i.Listings.Count>0).ThenBy(i=>i.Name).ThenBy(i=>i.Id).ToArray();
    }
}
