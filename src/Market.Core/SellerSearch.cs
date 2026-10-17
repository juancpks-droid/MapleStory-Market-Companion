namespace Market.Core;
public static class SellerSearch
{
 public static List<Observation> Find(IEnumerable<Observation> observations,string query,bool recentOnly,bool lowestFirst,DateTimeOffset now,int freshHours)
 {
  query=query.Trim();if(query.Length<3)return [];
  var tokens=TextKey.Search(query).Split(' ',StringSplitOptions.RemoveEmptyEntries);if(tokens.Length==0)return [];
  var rows=observations.GroupBy(o=>o.Key).Select(g=>g.OrderByDescending(o=>o.RetrievedAt).First())
   .Where(o=>o.Kind==EvidenceKind.Asking&&o.Available&&o.UnitPrice>0&&o.Quantity is not <=0)
   .Where(o=>ScrollNames.IsScroll(query)?ScrollNames.Matches(o.Name,query):tokens.All(t=>TextKey.Search(o.Name).Contains(t)))
   .Where(o=>!recentOnly||(o.Fresh(now,TimeSpan.FromHours(freshHours))&&o.RetrievedAt>=now.AddHours(-freshHours)));
  return (lowestFirst?rows.OrderBy(o=>o.UnitPrice).ThenByDescending(o=>o.ObservedAt):rows.OrderByDescending(o=>o.ObservedAt).ThenBy(o=>o.UnitPrice)).ToList();
 }
}
