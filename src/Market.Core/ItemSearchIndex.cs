namespace Market.Core;
public sealed class ItemSearchIndex
{
 readonly (FinderIdentity Item,string Key)[] entries;
 public ItemSearchIndex(IEnumerable<FinderIdentity> items){entries=items.Where(i=>ItemFinder.HasEnglishDisplayName(i.Name)).DistinctBy(i=>i.Name).Select(i=>(i,TextKey.Search(i.Name))).ToArray();}
 public IReadOnlyList<FinderIdentity> Suggest(string query){
  var key=TextKey.Search(query);if(key.Length<2||key.Length>100)return [];
  var exact=entries.Where(e=>e.Key.Contains(key)||e.Item.Id==query).OrderBy(e=>e.Key==key?0:e.Key.StartsWith(key)?1:2).ThenBy(e=>e.Key.Length).Take(8).Select(e=>e.Item).ToArray();if(exact.Length>0)return exact;
  if(key.Length<4)return [];
  int Distance(string a,string b){if(Math.Abs(a.Length-b.Length)>2)return 3;var prev=Enumerable.Range(0,b.Length+1).ToArray();for(int i=1;i<=a.Length;i++){var next=new int[b.Length+1];next[0]=i;for(int j=1;j<=b.Length;j++)next[j]=Math.Min(Math.Min(next[j-1]+1,prev[j]+1),prev[j-1]+(a[i-1]==b[j-1]?0:1));if(next.Min()>2)return 3;prev=next;}return prev[b.Length];}
  return entries.Where(e=>Math.Abs(e.Key.Length-key.Length)<=2).Select(e=>(e.Item,Distance:Distance(key,e.Key))).Where(e=>e.Distance<=2).OrderBy(e=>e.Distance).ThenBy(e=>e.Item.Name).Take(8).Select(e=>e.Item).ToArray();
 }
}
