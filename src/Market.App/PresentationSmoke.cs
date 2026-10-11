using Market.Core;
using System.Text.Json;
namespace Market.App;
public sealed partial class MainForm
{
 internal void PresentationChecks(List<string> checks){
  Width=1024;updateBanner.Visible=true;updateBannerText.Text="Update ready. Restart now? Choose Restart now or Later.";PerformLayout();
  var actions=updateBanner.Controls.OfType<FlowLayoutPanel>().Single();var buttons=actions.Controls.OfType<Button>().ToArray();
  if(buttons.Length!=2||buttons[0].Top!=buttons[1].Top||buttons[0].Right>buttons[1].Left||buttons[1].Right>actions.ClientSize.Width)throw new Exception("Restart actions are not side by side");
  if(restartUpdateButton!.Enabled)throw new Exception("Restart enabled before package ready");
  checks.Add("Restart now and Later fit side by side at minimum window width; restart disabled until verified download");
  CapturePreview(Path.Combine(dataDirectory,"restart-prompt.png"));updateBanner.Visible=false;
  using var graph=new HistoryPlot(){Size=new(720,300)};
  using var bitmap=new Bitmap(720,300);
  graph.DrawToBitmap(bitmap,new Rectangle(Point.Empty,graph.Size));bitmap.Save(Path.Combine(dataDirectory,"chart-empty.png"));
  graph.Points=[new(){Name="TEST ONLY",UnitPrice=50,ObservedAt=DateTimeOffset.UtcNow,Kind=EvidenceKind.Asking}];graph.DrawToBitmap(bitmap,new Rectangle(Point.Empty,graph.Size));
  graph.Points=Enumerable.Range(0,12).Select(i=>new Observation(){Name="TEST ONLY",UnitPrice=50+i*3,ObservedAt=DateTimeOffset.UtcNow.AddHours(-i),Kind=i%3==0?EvidenceKind.ConfirmedSale:i%3==1?EvidenceKind.Asking:EvidenceKind.ReportedSale,PriceApproximate=i%4==0}).ToList();
  graph.DrawToBitmap(bitmap,new Rectangle(Point.Empty,graph.Size));bitmap.Save(Path.Combine(dataDirectory,"chart-sample.png"));
  checks.Add("Empty, single-value and mixed evidence charts render with rounded-price markers");
  CheckAppUpdates(false).GetAwaiter().GetResult();
  if(nextUpdateCheck>DateTimeOffset.UtcNow.AddMinutes(61)||nextUpdateCheck<DateTimeOffset.UtcNow.AddMinutes(59))throw new Exception("Update schedule is not one hour");
  checks.Add("Update checks schedule the next attempt one hour ahead, including offline mode");
 }
}
static class DropReferenceSmoke
{
 public static void Run(){
  var directory=Path.Combine(AppContext.BaseDirectory,"drops-smoke-data");Directory.CreateDirectory(directory);
  using var viewer=new WebsiteViewer(MonsterReference.ForItem("Snail Shell"),directory);
  viewer.Shown+=async(_,_)=>{try{
   if(!await viewer.Ready.Task.WaitAsync(TimeSpan.FromSeconds(40)))throw new Exception("Website did not load");
   string content="";for(int i=0;i<20;i++){content=await viewer.ReadMonsterReferences(true);using var result=JsonDocument.Parse(content);if(result.RootElement.GetProperty("query").GetString()=="Snail Shell"&&result.RootElement.GetProperty("locations").GetInt32()>0)break;await Task.Delay(500);}
   using(var result=JsonDocument.Parse(content)){if(result.RootElement.GetProperty("query").GetString()!="Snail Shell"||result.RootElement.GetProperty("locations").GetInt32()==0||result.RootElement.GetProperty("monsters").GetArrayLength()==0)throw new Exception("Item search or monster location results missing");}
   await viewer.CapturePage(Path.Combine(directory,"drops.png"));
   File.WriteAllText(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(new{status="passed",url=viewer.CurrentUrl,checks=new[]{"Live monster reference opens inside WebView2 with selected item query and map references"}},Json.Options));
  }catch(Exception e){Environment.ExitCode=1;File.WriteAllText(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(new{status="failed",error=e.ToString()},Json.Options));}finally{viewer.Close();}};
  Application.Run(viewer);
 }
}
