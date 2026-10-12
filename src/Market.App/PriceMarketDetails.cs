using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly TabControl priceDetails=new MarketTabs(){Dock=DockStyle.Fill};
 readonly FlowLayoutPanel sellCards=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
 readonly FlowLayoutPanel buyCards=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
 readonly Label sellCount=Info(32),buyCount=Info(32),marketHeadline=Info(30);
 readonly ComboBox listingSort=new GuideComboBox(){Width=145,DropDownStyle=ComboBoxStyle.DropDownList};
 readonly NumericUpDown listingMin=Amount(0),listingMax=Amount(0);
 readonly ComboBox listingSource=new GuideComboBox(){Width=150,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Listing source"};
 readonly CheckBox listingRecent=new(){Text="Recent only",AutoSize=true},listingCompact=new(){Text="Compact cards",AutoSize=true};
 void ResetMarketFilters(){listingMin.Value=0;listingMax.Value=0;listingSort.SelectedIndex=3;listingSource.SelectedIndex=0;listingRecent.Checked=false;}
 Observation? marketTarget; bool fillingPriceChoices; readonly ComboBox priceItemChoice=new GuideComboBox(){Width=195,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Matching item"};
 void BuildPriceDetails(Panel page,Control chart){
  page.Controls.Remove(priceGrid);page.Controls.Remove(chart);
  var listings=new TabPage("For sale & looking to buy");
  var filters=Toolbar();filters.Height=42;filters.WrapContents=true;filters.AutoScroll=false;filters.Controls.Add(priceItemChoice);priceItemChoice.SelectedIndexChanged+=(_,_)=>{if(!fillingPriceChoices){RenderValuation();RenderHistory();QueueItemLookup(priceItemChoice.Text);}};
  listingSort.Items.AddRange(["Newest first","Price: low to high","Price: high to low","Kerning first"]);listingSort.SelectedIndex=3;
  listingMin.Width=75;listingMax.Width=75;
  filters.Controls.Add(listingSort);filters.Controls.Add(new Label(){Text="Min mesos",AutoSize=true,Padding=new Padding(0,8,0,0)});filters.Controls.Add(listingMin);
  filters.Controls.Add(new Label(){Text="Max (0 = any)",AutoSize=true,Padding=new Padding(0,8,0,0)});filters.Controls.Add(listingMax);
  listingSource.Items.Add("All sources");listingSource.Items.AddRange(settings.Sources.Select(s=>s.Id).Distinct().Cast<object>().ToArray());listingSource.SelectedIndex=0;
  filters.Controls.Add(listingSource);filters.Controls.Add(listingRecent);filters.Controls.Add(listingCompact);filters.Controls.Add(Button("Reset filters",ResetMarketFilters));
  listingSource.SelectedIndexChanged+=(_,_)=>RenderMarketCards(marketTarget);listingRecent.CheckedChanged+=(_,_)=>RenderMarketCards(marketTarget);listingCompact.CheckedChanged+=(_,_)=>RenderMarketCards(marketTarget);
  var columns=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};columns.ColumnStyles.Add(new(SizeType.Percent,50));columns.ColumnStyles.Add(new(SizeType.Percent,50));
  Panel Side(FlowLayoutPanel cards,Label count){var panel=new Panel(){Dock=DockStyle.Fill,Padding=new Padding(3)};panel.Controls.Add(cards);panel.Controls.Add(count);cards.SizeChanged+=(_,_)=>{foreach(Control card in cards.Controls){int width=Math.Max(150,cards.ClientSize.Width-24);card.MaximumSize=new Size(width,0);card.Width=width;foreach(Control child in card.Controls){child.MaximumSize=new Size(Math.Max(130,width-24),0);if(child is FlowLayoutPanel)child.Width=Math.Max(130,width-24);}}};return panel;}
  columns.Controls.Add(Side(sellCards,sellCount));columns.Controls.Add(Side(buyCards,buyCount));listings.Controls.Add(columns);listings.Controls.Add(filters);
  priceDetails.TabPages.Add(listings);
  var timeline=new TabPage("Price over time");chart.Dock=DockStyle.Fill;timeline.Controls.Add(chart);priceDetails.TabPages.Add(timeline);
  var evidence=new TabPage("All price evidence");evidence.Controls.Add(priceGrid);priceDetails.TabPages.Add(evidence);BuildListingWorkbench();
  priceDetails.SelectedIndexChanged+=(_,_)=>{RenderValuation();RenderHistory();ApplyWorkbenchFocus();};page.Controls.Add(priceDetails);priceDetails.BringToFront();
  page.Controls.Add(marketHeadline);marketHeadline.BringToFront();priceDetails.BringToFront();marketHeadline.Font=new Font("Segoe UI",10,FontStyle.Bold);
  FitPriceText(fmContext);FitPriceText(priceSummary);FitPriceText(marketHeadline);BuildNpcReference(page);BuildPriceOverview(page);
  listingSort.SelectedIndexChanged+=(_,_)=>RenderMarketCards(marketTarget);listingMin.ValueChanged+=(_,_)=>RenderMarketCards(marketTarget);listingMax.ValueChanged+=(_,_)=>RenderMarketCards(marketTarget);
 }
 Observation? SelectedPriceItem()=>priceDetails.SelectedIndex==2&&priceGrid.SelectedRows.Count>0?priceGrid.SelectedRows[0].Tag as Observation:observations.FirstOrDefault(o=>o.Name==priceItemChoice.Text);
 void SetPriceChoices(List<Observation> rows){
  var previous=priceItemChoice.Text;var query=features.Polish.Resolve(search.Text).Trim();fillingPriceChoices=true;
  priceItemChoice.Items.Clear();var names=rows.Select(o=>o.Name).Distinct().Order().ToArray();priceItemChoice.Items.AddRange(names);
  var exact=names.FirstOrDefault(n=>TextKey.Search(n)==TextKey.Search(query));
  priceItemChoice.SelectedItem=exact??(names.Contains(previous)?previous:names.FirstOrDefault());
  fillingPriceChoices=false;
 }
 void RenderMarketCards(Observation? target){
  marketTarget=target;var now=DateTimeOffset.UtcNow;
  var name=target?.Name??search.Text.Trim();
  var all=observations.Where(o=>TextKey.Normalize(o.Name)==TextKey.Normalize(name)&&(!o.Equipment||(target is not null&&o.MatchKey==target.MatchKey))&&o.Available&&o.Kind is EvidenceKind.Asking or EvidenceKind.Buying).DistinctBy(o=>o.Key).ToList();
  var eligible=all.Where(o=>settings.Sources.Any(s=>s.Id==o.Provider&&s.Authorized)&&o.Fresh(now,TimeSpan.FromHours(settings.FreshHours))&&o.RetrievedAt>=now.AddMinutes(-30)).ToList();
  RenderListingWorkbench(all.Where(o=>o.UnitPrice>=listingMin.Value&&(listingMax.Value==0||o.UnitPrice<=listingMax.Value)&&(listingSource.SelectedIndex<=0||o.Provider==listingSource.Text)&&(!listingRecent.Checked||eligible.Contains(o))));
  var asks=eligible.Where(o=>o.Kind==EvidenceKind.Asking).ToList();var buys=eligible.Where(o=>o.Kind==EvidenceKind.Buying).ToList();
  marketHeadline.Text=(name.Length==0?"Select or search for an item":name)+"  •  Lowest recent ask: "+TextKey.Mesos(asks.Count==0?null:asks.Min(o=>o.UnitPrice))+"  •  Best recent buy offer: "+TextKey.Mesos(buys.Count==0?null:buys.Max(o=>o.UnitPrice));
  void Fill(FlowLayoutPanel flow,Label label,EvidenceKind kind,string heading){
   flow.SuspendLayout();foreach(Control c in flow.Controls.Cast<Control>().ToArray())c.Dispose();flow.Controls.Clear();
   var candidates=all.Where(o=>o.Kind==kind).ToList();
   var matches=candidates.Where(o=>o.UnitPrice>=listingMin.Value&&(listingMax.Value==0||o.UnitPrice<=listingMax.Value)&&(listingSource.SelectedIndex<=0||o.Provider==listingSource.Text)&&(!listingRecent.Checked||eligible.Contains(o)));
   matches=listingSort.SelectedIndex switch{1=>matches.OrderBy(o=>o.UnitPrice),2=>matches.OrderByDescending(o=>o.UnitPrice),3=>matches.OrderBy(o=>o.Provider=="Kerning Exchange"?0:1).ThenByDescending(o=>o.ObservedAt??o.RetrievedAt),_=>matches.OrderByDescending(o=>o.ObservedAt??o.RetrievedAt)};
   var rows=matches.Take(100).ToList();label.Text=$"{heading} ({candidates.Count}) • showing {rows.Count}";
   if(rows.Count==0)flow.Controls.Add(new Label(){Text=candidates.Count>0?"No listings match these filters. Try Reset filters to see cached listings.":"No matching cached listings. Choose Get prices to retrieve available public data here. Kerning is the primary listing source; other enabled sources add coverage. No imported offers does not mean nobody is trading.",AutoSize=true,MaximumSize=new Size(Math.Max(150,flow.ClientSize.Width-24),0),Padding=new Padding(10)});
   foreach(var o in rows){
    var card=new MarketListingCard(){Width=Math.Max(150,flow.ClientSize.Width-24),MaximumSize=new Size(Math.Max(150,flow.ClientSize.Width-24),0),AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(12),Margin=new Padding(2,3,2,10),BackColor=GuidePalette.Surface,Tag=o};
    void Line(string text,bool strong=false){card.Controls.Add(new Label(){Text=text,AutoSize=true,MaximumSize=new Size(Math.Max(130,card.Width-22),0),ForeColor=strong?GuidePalette.Gold:GuidePalette.Muted,Font=new Font("Segoe UI",strong?12:9,strong?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,2,0,3)});}
    Line(o.Name,true);Line((o.PriceApproximate?"~ ":"")+TextKey.Mesos(o.UnitPrice)+" mesos / unit",true);
    Line("Qty: "+(o.Quantity?.ToString("N0")??"Unknown")+" • "+o.Provider);
    Line((kind==EvidenceKind.Buying?"Buyer: ":"Seller: ")+(string.IsNullOrWhiteSpace(o.Seller)?"Not provided":o.Seller));
    if(o.Equipment)Line("Variant: "+(o.VariantComplete?o.Variant:"Incomplete details — check source"));
    if(!listingCompact.Checked){Line(string.IsNullOrWhiteSpace(o.Note)?"Location / seller notes: not provided":o.Note);
    Line("Posted: "+(o.ObservedAt?.ToLocalTime().ToString("g")??"Unknown")+" • Checked: "+o.RetrievedAt.ToLocalTime().ToString("g"));}
    Line(ReliabilityTools.EvidenceStatus(o,settings.Sources.Any(s=>s.Id==o.Provider&&s.Authorized),now,settings.FreshHours));
    var actions=new FlowLayoutPanel(){AutoSize=true,MaximumSize=new Size(Math.Max(130,card.Width-22),0),Width=Math.Max(130,card.Width-22),WrapContents=true};
    actions.Controls.Add(Button("Details",()=>MessageBox.Show(this,ListingDetails(o),o.Name,MessageBoxButtons.OK,MessageBoxIcon.Information)));actions.Controls.Add(Button("Website fallback",()=>Open(o.Url)));
    void Copy(string value){try{Clipboard.SetText(value);}catch{footer.Text="Clipboard busy. Try again.";}}
    if(!string.IsNullOrWhiteSpace(o.Seller))actions.Controls.Add(Button("Copy name",()=>Copy(o.Seller)));
    actions.Controls.Add(Button("Copy link",()=>Copy(o.Url)));actions.Controls.Add(Button("Copy details",()=>Copy(ListingDetails(o))));card.Controls.Add(actions);flow.Controls.Add(card);
   }
   flow.ResumeLayout();
  }
  Fill(sellCards,sellCount,EvidenceKind.Asking,"FOR SALE");Fill(buyCards,buyCount,EvidenceKind.Buying,"LOOKING TO BUY");
 }
 static string ListingDetails(Observation o)=>string.Join(Environment.NewLine,o.Name,(o.Kind==EvidenceKind.Buying?"BUY":"SELL")+" • "+(o.PriceApproximate?"approximately ":"")+TextKey.Mesos(o.UnitPrice)+" mesos / unit","Quantity: "+(o.Quantity?.ToString("N0")??"Unknown"),"Player: "+(string.IsNullOrWhiteSpace(o.Seller)?"Not provided":o.Seller),"Source: "+o.Provider,"Details: "+o.Note,"Checked: "+o.RetrievedAt.ToLocalTime().ToString("g"),o.Url);
}


