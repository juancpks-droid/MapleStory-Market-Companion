using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
    async Task SmokePlanningUpgrade()
    {
        var before=Snapshot();
        try
        {
            var item=new SavedItem("Planning test sword",3,true,false){Variant="watk=42"};store.SaveItem(item);var bought=new TradeEntry(Guid.NewGuid().ToString(),item.Name,item.Variant,true,3,100,3,DateTimeOffset.UtcNow.AddDays(-3)){Profile=store.Profile};features.Trades.Add(bought);Workspace.Shopping.Add(new(Guid.NewGuid().ToString(),store.Profile,"Unknown planning item","",2,0,500));Planning.Budgets[store.Profile]=1000;Planning.Priorities.Add(new(store.Profile,item.Name,item.Variant,"Buy soon",50));Planning.Templates.Add(new(Guid.NewGuid().ToString(),store.Profile,"Test template",item.Name,item.Variant,false,2,200,5));SaveFeatures();
            var provider=settings.Sources.First(s=>s.Authorized).Id;var listing=new Observation{Provider=provider,ListingId="priority-smoke-17",Name=item.Name,Variant=item.Variant,Equipment=true,VariantComplete=true,UnitPrice=40,Quantity=1,Kind=EvidenceKind.Asking,ObservedAt=DateTimeOffset.UtcNow,RetrievedAt=DateTimeOffset.UtcNow,Available=true,Url="https://example.org/test-only"};observations.Add(listing);try{CheckPriorityAlerts();var entry=features.Inbox.Single(e=>e.ListingKey==listing.Key);if(!PriorityStillEligible(entry))throw new Exception("Priority alert missing");var priority=Planning.Priorities.Single(p=>p.Name==item.Name);Planning.Priorities[Planning.Priorities.IndexOf(priority)]=priority with{Group="Just tracking"};if(PriorityStillEligible(entry))throw new Exception("Changed priority still delivers");}finally{observations.Remove(listing);}
            var draft=SaleDraft(item);if(draft.Entry.Variant!=item.Variant||draft.Entry.Quantity!=3||draft.Entry.Buy||draft.UpdateStock)throw new Exception("Sale preparation lost exact stack");
            using(var form=CreateTradeEditor(null,false,draft)){form.Show(this);var fields=form.Controls.OfType<FlowLayoutPanel>().Single();if(fields.Controls.OfType<TextBox>().ElementAt(1).Text!=item.Variant)throw new Exception("Sale editor variant missing");form.Close();}File.Delete(DraftPath);
            foreach(var (name,form) in new[]{("sell-view",CreateSellView()),("budget",CreateBudgetPlanner()),("priorities",CreatePriorities()),("templates",CreateTemplates()),("variant-chart",CreateVariantChart(item)),("data-health",CreateDataHealth())})
            {using(form){form.Show(this);await Task.Delay(80);if(form.Controls.OfType<DataGridView>().Single().Columns.Count==0)throw new Exception("Empty planning layout: "+name);using var b=new Bitmap(form.Width,form.Height);form.DrawToBitmap(b,new Rectangle(Point.Empty,form.Size));b.Save(Path.Combine(dataDirectory,name+"-17.png"));form.Close();}}
            Planning.DashboardPanels=["FM"];ShowPage("Dashboard");ApplyDashboardPanels();if(dashboardSections["Watchlist"].Visible||!dashboardSections["FM"].Visible)throw new Exception("Dashboard panel choice not applied");CapturePreview(Path.Combine(dataDirectory,"dashboard-17.png"));
            var restored=CompanionFeatures.Read(System.Text.Json.JsonSerializer.Serialize(features,Json.Options));if(restored.Planning.Templates.Count!=Planning.Templates.Count||restored.Planning.Budgets[store.Profile]!=1000)throw new Exception("Planner persistence failed");
        }
        finally{if(File.Exists(DraftPath))File.Delete(DraftPath);RestorePersonal(before);}
    }
}
