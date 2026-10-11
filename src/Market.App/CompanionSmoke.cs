using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
    async Task SmokeCompanionUpgrade()
    {
        var before=Snapshot();var draftPath=DraftPath;
        try
        {
            var owned=new SavedItem("Red Potion",70,true,false);store.SaveItem(owned);features.Memos.Add(new(Guid.NewGuid().ToString(),store.Profile,owned.Name,"Keep","Screenshot review test"));
            using(var details=CreateItemDetails(owned)){details.Show(this);var tabs=details.Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<TabControl>()).Single();if(tabs.TabPages.Count!=2||!CommandCenterText(owned).Contains("70 owned"))throw new Exception("Command center lost stock");await Task.Delay(150);using var b=new Bitmap(details.Width,details.Height);details.DrawToBitmap(b,new Rectangle(Point.Empty,details.Size));b.Save(Path.Combine(dataDirectory,"command-center-16.png"));details.Close();}
            using(var editor=CreateTradeEditor(null)){editor.Show(this);var f=editor.Controls.OfType<FlowLayoutPanel>().Single();f.Controls.OfType<TextBox>().First().Text="Draft test potion";f.Controls.OfType<NumericUpDown>().First().Value=7;editor.Close();}
            var draft=TradeDraft.Load(draftPath)??throw new Exception("Draft missing after close");if(draft.Entry.Name!="Draft test potion"||draft.Entry.Quantity!=7||features.Trades.Any(t=>t.Id==draft.Entry.Id))throw new Exception("Draft changed journal or lost fields");
            using(var editor=CreateTradeEditor(draft.Original,null,draft)){editor.Show(this);if(editor.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<TextBox>().First().Text!=draft.Entry.Name)throw new Exception("Draft recovery field lost");editor.Close();}File.Delete(draftPath);
            bool old=offlineToggle.Checked;offlineToggle.Checked=true;var checkedAt=settings.Sources.Select(s=>s.LastChecked).ToArray();await RefreshSources(true);if(!checkedAt.SequenceEqual(settings.Sources.Select(s=>s.LastChecked))||!connectionStatus.Text.StartsWith("OFFLINE"))throw new Exception("Offline mode fetched");ShowPage("Inventory");if(!offlineToggle.Visible||offlineToggle.Parent!.Parent!=footer)throw new Exception("Offline control not visible");CapturePreview(Path.Combine(dataDirectory,"offline-16.png"));offlineToggle.Checked=old;
            var screenshot=Path.Combine(dataDirectory,"ocr-input-16.png");using(var b=new Bitmap(850,300)){using var g=Graphics.FromImage(b);g.Clear(Color.White);using var font=new Font("Arial",30);g.DrawString("Red Potion\nQuantity: 70",font,Brushes.Black,new PointF(30,30));b.Save(screenshot);}
            string text=await ReadScreenshot(screenshot);var suggestion=CompanionUpgrade.Suggest(text,iconCatalog.Names);if(suggestion.Name!="Red Potion"||suggestion.Quantity!=70)throw new Exception("Windows OCR did not recognize controlled fixture: "+text);
            using(var review=CreateScreenshotReview(screenshot,text)){review.Show(this);var f=review.Controls.OfType<FlowLayoutPanel>().Single();if(f.Controls.OfType<Button>().Last().Enabled)throw new Exception("Unreviewed screenshot may save");f.Controls.OfType<CheckBox>().Single().Checked=true;if(!f.Controls.OfType<Button>().Last().Enabled)throw new Exception("Review confirmation failed");using var b=new Bitmap(review.Width,review.Height);review.DrawToBitmap(b,new Rectangle(Point.Empty,review.Size));b.Save(Path.Combine(dataDirectory,"screenshot-review-16.png"));review.Close();}
            var session=new MerchantSession(Guid.NewGuid().ToString(),store.Profile,"Release verification",DateTimeOffset.UtcNow.AddHours(-1),null){StartMesos=store.Characters().Single(c=>c.Id==store.Profile).Mesos};if(!SessionRecapText(session).Contains("Journal net cash flow"))throw new Exception("Session recap missing cash flow");
            var listing=observations.First();var estimate=new Valuation(100,90,100,110,75,60,5,"Test only");using(var d=CreateItemDetails(new Deal(listing,estimate,50,50,100,"Test only"))){d.Show(this);var tabs=d.Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<TabControl>()).Single();if(tabs.TabPages.Count!=3)throw new Exception("Deal explanation missing");tabs.SelectedIndex=2;using var b=new Bitmap(d.Width,d.Height);d.DrawToBitmap(b,new Rectangle(Point.Empty,d.Size));b.Save(Path.Combine(dataDirectory,"deal-explanation-16.png"));d.Close();}
        }
        finally{if(File.Exists(draftPath))File.Delete(draftPath);RestorePersonal(before);offlineToggle.Checked=features.Offline;icons.Offline=Offline;}
    }
}
