using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 readonly FlowLayoutPanel priceSuggestions=new(){AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,MaximumSize=new Size(520,430),AutoScroll=true,Visible=false,Padding=new Padding(6),BorderStyle=BorderStyle.FixedSingle};
 readonly System.Windows.Forms.Timer suggestionTimer=new(){Interval=200};
 ItemSearchIndex? priceSearchIndex;int suggestionGeneration;
 void BuildPriceSuggestions(Panel page,Control bar){
  priceSearchIndex=new(iconCatalog.Entries.Select(e=>new FinderIdentity(e.Id,e.Name)).Concat(ScrollNames.References.Select(e=>new FinderIdentity("",ScrollNames.Display(e.Name)))));
  page.Controls.Add(priceSuggestions);priceSuggestions.BringToFront();
  ConfigurePriceAutocomplete();
  search.TextChanged+=(_,_)=>{suggestionGeneration++;suggestionTimer.Stop();priceSuggestions.Visible=false;if(search.Focused)suggestionTimer.Start();};
  search.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){suggestionTimer.Stop();priceSuggestions.Visible=false;e.SuppressKeyPress=true;}};
  suggestionTimer.Tick+=(_,_)=>{suggestionTimer.Stop();ShowPriceSuggestions();};
  FormClosed+=(_,_)=>suggestionTimer.Dispose();
 }
 void ConfigurePriceAutocomplete(){
  var names=new AutoCompleteStringCollection();
  names.AddRange(iconCatalog.Names.Concat(ScrollNames.References.Select(e=>ScrollNames.Display(e.Name))).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray());
  search.AutoCompleteCustomSource=names;
  search.AutoCompleteSource=AutoCompleteSource.CustomSource;
  search.AutoCompleteMode=AutoCompleteMode.Suggest;
 }
 async void ShowPriceSuggestions(bool requireFocus=true){
  if(closing||IsDisposed||(requireFocus&&!search.Focused)||priceSearchIndex is null)return;
  // Native suggestions stay anchored beneath the input. Keep related/typo
  // buttons only when there is no prefix match, avoiding two competing lists.
  if(string.IsNullOrWhiteSpace(search.Text)||search.AutoCompleteCustomSource.Cast<string>().Any(n=>n.StartsWith(search.Text.Trim(),StringComparison.OrdinalIgnoreCase))){priceSuggestions.Visible=false;return;}
  int generation=suggestionGeneration;var items=priceSearchIndex.Suggest(search.Text);
  if(items.Count==1&&TextKey.Search(items[0].Name)==TextKey.Search(search.Text)){priceSuggestions.Visible=false;return;}
  foreach(Control old in priceSuggestions.Controls.Cast<Control>().ToArray())old.Dispose();priceSuggestions.Controls.Clear();
  if(items.Count==0){priceSuggestions.Visible=false;return;}
  priceSuggestions.Controls.Add(new Label{Text="Choose an item:",AutoSize=true,Padding=new Padding(4,10,4,4)});
  var buttons=new List<(Button Button,FinderIdentity Item)>();
  foreach(var item in items){var button=Button(item.Name,async()=>{priceSuggestions.Visible=false;search.Text=item.Name;await LookupPricesNow();});button.ImageAlign=ContentAlignment.MiddleLeft;button.TextImageRelation=TextImageRelation.ImageBeforeText;priceSuggestions.Controls.Add(button);buttons.Add((button,item));}
  priceSuggestions.BackColor=GuidePalette.Surface;priceSuggestions.Location=pages["Price Checker"].PointToClient(search.PointToScreen(new Point(0,search.Height+2)));priceSuggestions.BringToFront();priceSuggestions.Visible=true;
  foreach(var pair in buttons){var icon=await icons.Get(pair.Item.Id);if(closing||IsDisposed||generation!=suggestionGeneration)return;if(!pair.Button.IsDisposed&&icon is not null)pair.Button.Image=icon;}
 }
}
