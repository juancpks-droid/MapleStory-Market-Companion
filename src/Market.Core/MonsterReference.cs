namespace Market.Core;
public static class MonsterReference
{
    public static WebsiteLookup ForItem(string name)
    {
        name=name.Trim();if(name.Length is 0 or >250)throw new ArgumentException("Select an item name first.");
        return new("NiaMeowDB • Monster drops & maps","https://meowdb.com/msclassic/monsters?q="+Uri.EscapeDataString(name),name,false,
            "Monster results match names or dropped items. Open a monster and its Locations / maps links to find it. Check the exact item in its drop list. Community reports and Classic World coverage can differ from GMS 83 or your server.");
    }
}
