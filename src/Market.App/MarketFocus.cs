namespace Market.App;
public sealed partial class MainForm
{
 static readonly HashSet<string> retiredPages=["Getting Started","Inventory","Characters","Transfers","Equipment","Restock","Duplicate Review","Undo Center","Selling Planner","Trade Journal","Trading Sessions","Merchant Goals","Profit Breakdown"];
 static bool MarketPage(string name)=>!retiredPages.Contains(name);
 void ApplyMarketFocus(){
  void Clean(Control root){
   foreach(Control c in root.Controls){
    if(c is Button button&&(button.Text.Contains("inventory",StringComparison.OrdinalIgnoreCase)||button.Text.Contains("Switch character",StringComparison.OrdinalIgnoreCase)||button.Text=="Check duplicates"||button.Text.Contains("Record trade",StringComparison.OrdinalIgnoreCase)||button.Text.Contains("Trading session",StringComparison.OrdinalIgnoreCase)||button.Text=="My goals"||button.Text=="Setup guide"))button.Visible=false;
    Clean(c);
   }
  }
  Clean(this);
  inventoryCard.Visible=false;
 }
}

