using System.Net;
namespace Market.Core;
// One bounded, serialized queue per source. No credentials, redirects or background crawl.
public sealed class PublicReferenceClient : IDisposable
{
 readonly string host; readonly bool checkRobots; readonly HttpClient client;
 readonly SemaphoreSlim gate=new(1,1); readonly Dictionary<string,(string Text,DateTimeOffset At)> cache=[];
 readonly Queue<DateTimeOffset> requests=[]; DateTimeOffset next; string? robots; DateTimeOffset robotsAt;
 public PublicReferenceClient(string host,bool checkRobots=true,HttpMessageHandler? handler=null){this.host=host;this.checkRobots=checkRobots;client=new(handler??new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(20)};client.DefaultRequestHeaders.UserAgent.ParseAdd("MapleMarketCompanion/1.25.5 (on-demand reference)");}
 public async Task<string> Get(string url,CancellationToken ct){
  var uri=new Uri(url);if(uri.Scheme!="https"||uri.GetLeftPart(UriPartial.Authority)!=host||uri.UserInfo.Length>0)throw new FormatException("Invalid reference address");
  await gate.WaitAsync(ct);try{
   if(checkRobots&&(robots is null||robotsAt<DateTimeOffset.UtcNow.AddHours(-1))){robots=await Fetch(host+"/robots.txt",ct);robotsAt=DateTimeOffset.UtcNow;}
   if(checkRobots&&!KerningPages.RobotsAllows(robots!,uri.PathAndQuery))throw new HttpRequestException("Reference access disallowed");
   return await Fetch(url,ct);
  }finally{gate.Release();}
 }
 async Task<string> Fetch(string url,CancellationToken ct){
  ct.ThrowIfCancellationRequested();if(cache.TryGetValue(url,out var page)&&page.At>DateTimeOffset.UtcNow.AddHours(-1))return page.Text;
  var now=DateTimeOffset.UtcNow;while(requests.TryPeek(out var at)&&at<now.AddHours(-1))requests.Dequeue();
  if(requests.Count>=120||next>now.AddMinutes(1))throw new HttpRequestException("Reference busy; try later");
  if(next>now)await Task.Delay(next-now,ct);requests.Enqueue(DateTimeOffset.UtcNow);next=DateTimeOffset.UtcNow.AddSeconds(3);
  using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct);
  if(response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)next=DateTimeOffset.UtcNow.AddMinutes(15);
  response.EnsureSuccessStatusCode();if((int)response.StatusCode>=300)throw new HttpRequestException("Reference address changed");
  using var stream=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[8192];int count;
  while((count=await stream.ReadAsync(buffer,ct))>0){if(output.Length+count>2*1024*1024)throw new IOException("Reference too large");await output.WriteAsync(buffer.AsMemory(0,count),ct);}
  string text=System.Text.Encoding.UTF8.GetString(output.ToArray());if(cache.Count>=100)cache.Clear();cache[url]=(text,DateTimeOffset.UtcNow);return text;
 }
 public void Dispose()=>client.Dispose();
}
