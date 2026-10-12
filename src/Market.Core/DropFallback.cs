namespace Market.Core;

public sealed record DropLookupResult(ItemDrops Drops,string Source,string Notice);
public static class DropFallback
{
 public static async Task<DropLookupResult> Lookup(string name,Func<string,CancellationToken,Task<ItemDrops>> primary,Func<string,CancellationToken,Task<ItemDrops>> fallback,CancellationToken ct,Action<string>? progress=null,TimeSpan? primaryTimeout=null){
  ItemDrops? first=null;string reason;
  using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct)){
   deadline.CancelAfter(primaryTimeout??TimeSpan.FromSeconds(55));
   try{first=await primary(name,deadline.Token);ct.ThrowIfCancellationRequested();if(first.Monsters.Count>0)return new(first,"NiaMeowDB","");reason="NiaMeowDB returned no monster results.";}
   catch(OperationCanceledException) when(!ct.IsCancellationRequested){reason="NiaMeowDB timed out.";}
   catch(Exception e) when(e is HttpRequestException or IOException or FormatException){ct.ThrowIfCancellationRequested();reason="NiaMeowDB could not load.";}
  }
  ct.ThrowIfCancellationRequested();progress?.Invoke(reason+" Trying CCPLZ automatically…");
  try{var second=await fallback(name,ct);ct.ThrowIfCancellationRequested();return new(second,"CCPLZ",reason+" Automatic fallback. ");}
  catch(Exception e) when(!ct.IsCancellationRequested && e is HttpRequestException or IOException or FormatException or OperationCanceledException){
   return new(first??new ItemDrops(name,"","No completed drop result was received.",[],DateTimeOffset.UtcNow),first is null?"NiaMeowDB / CCPLZ":"NiaMeowDB",reason+" CCPLZ also could not load. Select Load / retry. ");
  }
 }
}
