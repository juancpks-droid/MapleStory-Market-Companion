namespace Market.Core;
public static class ReferenceFallback
{
 public static async Task<DropLookupResult> Drops(string name,IReadOnlyList<(string Source,Func<CancellationToken,Task<ItemDrops>> Read)> sources,CancellationToken ct,Action<string>? progress=null){
  DropLookupResult? last=null;
  foreach(var source in sources){ct.ThrowIfCancellationRequested();progress?.Invoke(source.Source);using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(35));
   try{var result=await source.Read(deadline.Token);ct.ThrowIfCancellationRequested();last=new(result,source.Source,"");if(result.Monsters.Count>0)return last;}
   catch(Exception e) when(!ct.IsCancellationRequested&&e is HttpRequestException or IOException or FormatException or System.Text.Json.JsonException or OperationCanceledException){}
  }
  ct.ThrowIfCancellationRequested();return last??new(new(name,"","Reference sources could not be reached. This does not mean the item has no drops.",[],DateTimeOffset.UtcNow),"Automatic","");
 }
}
