using System.Text;
using System.Globalization;

namespace Market.Core;
public sealed record CharacterProfile(string Id,string Name,decimal Mesos);
public sealed record CharacterInventory(CharacterProfile Character,List<SavedItem> Items);
public sealed record InboxEntry(string Key,string Profile,string Title,string Message,string Url,DateTimeOffset At,bool Read=false){public string ItemName {get;init;}="";public string Variant {get;init;}="";public string ListingKey {get;init;}="";public decimal Price {get;init;}}
public sealed record ListingFilter(bool EquipmentOnly=false,bool RecentOnly=false,decimal MaxPrice=0,string Stat="",decimal MinStat=0,int MinSlots=0)
{
    public bool Matches(Observation o,DateTimeOffset now,int freshHours)
    {
        if(EquipmentOnly&&!o.Equipment||RecentOnly&&(!o.Available||o.Kind!=EvidenceKind.Asking||!o.Fresh(now,TimeSpan.FromHours(freshHours)))||MaxPrice>0&&o.UnitPrice>MaxPrice)return false;
        var stats=CompanionFeatures.Stats(o.Variant);
        if(Stat.Length>0&&(!o.Equipment||!o.VariantComplete||!stats.TryGetValue(Stat,out var value)||value<MinStat))return false;
        return MinSlots==0||(o.Equipment&&o.VariantComplete&&stats.TryGetValue("remaining",out var slots)&&slots>=MinSlots);
    }
}
public static class InventoryImport
{
    public static List<SavedItem> Parse(string input)
    {
        if(input.Length>2_000_000)throw new FormatException("Import exceeds 2 MB");
        var result=new List<SavedItem>();var lines=input.Replace("\r","").Split('\n');
        foreach(var (line,index) in lines.Select((s,i)=>(s,i)))
        {
            if(string.IsNullOrWhiteSpace(line))continue;var fields=Fields(line,line.Contains('\t')?'\t':',');
            if(result.Count==0&&fields.Count>=2&&fields[0].Trim().Equals("name",StringComparison.OrdinalIgnoreCase)&&fields[1].Trim().Equals("quantity",StringComparison.OrdinalIgnoreCase))continue;
            if(fields.Count is <2 or >3||string.IsNullOrWhiteSpace(fields[0])||!long.TryParse(fields[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out var q)||q is <1 or >1_000_000_000)throw new FormatException($"Line {index+1}: expected name, positive quantity, optional exact variant");
            var item=new SavedItem(fields[0].Trim(),q,false,false){Variant=fields.Count==3?fields[2].Trim():""};if(item.Name.Length>250||item.Variant.Length>4000)throw new FormatException($"Line {index+1}: value too long");result.Add(item);
        }
        if(result.Count==0||result.Count>10000)throw new FormatException("Import must contain 1–10,000 items");
        if(result.Select(ItemIdentity.Key).Distinct().Count()!=result.Count)throw new FormatException("Duplicate name and variant: combine quantities before importing");return result;
    }
    static List<string> Fields(string line,char separator)
    {
        var fields=new List<string>();var current=new StringBuilder();bool quoted=false;
        for(int i=0;i<line.Length;i++){char c=line[i];if(c=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){current.Append('"');i++;}else quoted=!quoted;}else if(c==separator&&!quoted){fields.Add(current.ToString());current.Clear();}else current.Append(c);}
        if(quoted)throw new FormatException("Unclosed quote in import");fields.Add(current.ToString());return fields;
    }
    public static List<SavedItem> Merge(List<SavedItem> existing,List<SavedItem> incoming)
    {
        var map=existing.ToDictionary(ItemIdentity.Key);foreach(var item in incoming){if(map.TryGetValue(ItemIdentity.Key(item),out var before)){if(before.Quantity>0&&before.Variant!=item.Variant)throw new FormatException("Variant conflicts with existing stock: "+item.Name);map[ItemIdentity.Key(item)]=before with{Quantity=item.Quantity,Variant=item.Variant};}else map[ItemIdentity.Key(item)]=item;}return map.Values.ToList();
    }
}
