using Market.Core;
namespace Market.App;
public sealed partial class MainForm
{
 void CheckRelatedDropChoices(List<string> checks){
  ShowPage("Item Finder");finderDetailTabs.SelectedIndex=1;
  ShowDropSuggestions([new DropItemSuggestion("Blue Snail Shell","https://meowdb.com/msclassic/item-db/2")]);
  if(!relatedDropPanel.Visible||relatedDropChoice.Items.Count!=1)throw new Exception("Related source choice missing");
  ShowDropSuggestions([]);if(relatedDropPanel.Visible||relatedDropChoice.Items.Count!=0)throw new Exception("Related choices not cleared");
  checks.Add("Unconfirmed related item choices display separately and clear between lookups");
  bool offline=features.Offline;features.Offline=false;dropLookupStop?.Cancel();int generation=++dropGeneration;dropItemKey="timeout-fixture";ShowDropTimeout(generation-1);if(dropItemKey!="timeout-fixture")throw new Exception("Old cancellation changed current request");ShowDropTimeout(generation);if(dropItemKey!=""||!finderDropNote.Text.Contains("timed out"))throw new Exception("Timeout stayed stuck");features.Offline=offline;checks.Add("Current timeout clears lookup lock and shows retry; superseded cancellation cannot overwrite current lookup");
 }
}
