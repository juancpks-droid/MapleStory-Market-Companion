using System.Text.Json;
using System.Globalization;

namespace Market.Core;

public sealed record PriceRule(string Id,string Name,string Variant,decimal Maximum,bool Enabled=true){public string Profile {get;init;}="main";}
public sealed record TradeEntry(string Id,string Name,string Variant,bool Buy,long Quantity,decimal UnitPrice,decimal Fees,DateTimeOffset At){public string Profile {get;init;}="main";}
public sealed record TradeOutcome(string Id,decimal MatchedProfit,long UnmatchedUnits);
public sealed record ActivityRow(string Name,string Variant,DateTimeOffset First,DateTimeOffset Last,decimal EarlierMedian,decimal LatestMedian,int EarlierCount,int LatestCount);
public sealed class CompanionFeatures
{
    public PlanningOptions Planning {get;set;}=new();
    public PolishOptions Polish {get;set;}=new();
    public WorkspaceOptions Workspace {get;set;}=new();
    public List<MerchantGoal> Goals {get;set;}=[];
    public List<RestockRule> Restocks {get;set;}=[];
    public List<GearWish> Wishes {get;set;}=[];
    public List<ItemMemo> Memos {get;set;}=[];
    public List<MerchantSession> Sessions {get;set;}=[];
    public HashSet<string> LowStock {get;set;}=[];
    public string Theme {get;set;}="Henesys";
    public bool SetupComplete {get;set;}

    public List<PriceRule> Rules {get;set;}=[];
    public List<TradeEntry> Trades {get;set;}=[];
    public HashSet<string> TargetNotifications {get;set;}=[];
    public Dictionary<string,string> Equipment {get;set;}=[];
    public bool Offline {get;set;}
    public bool MenuSounds {get;set;}
    public Dictionary<string,Dictionary<string,string>> CharacterEquipment {get;set;}=[];
    public List<InboxEntry> Inbox {get;set;}=[];
    public bool AutoBackups {get;set;}=true;
    public int TextSize {get;set;}=9;
    public static readonly string[] EquipmentSlots=["Hat","Face","Eye","Earrings","Pendant","Cape","Top / Overall","Gloves","Weapon","Shield","Bottom","Shoes","Ring"];
    public static CompanionFeatures Load(string path)=>File.Exists(path)?Read(File.ReadAllText(path)):new();
    public static CompanionFeatures Read(string text){var data=JsonSerializer.Deserialize<CompanionFeatures>(text,Json.Options)??throw new FormatException("Missing companion data");data.Validate();return data;}
    public void Validate()
    {
        if(Planning is null)throw new FormatException("Missing planning preferences");Planning.Validate();
        if(Polish is null)throw new FormatException("Missing polish settings");Polish.Validate();if(Workspace is null)throw new FormatException("Missing workspace options");Workspace.Validate();ValidateMerchant();
        if(CharacterEquipment is null||Inbox is null||Inbox.Count>2000||TextSize is <9 or >13)throw new FormatException("Invalid interface preferences");
        bool ProfileValid(string? p)=>p=="main"||Guid.TryParse(p,out _);
        if(CharacterEquipment.Any(p=>!ProfileValid(p.Key)||p.Value is null||p.Value.Any(v=>!EquipmentSlots.Contains(v.Key)||string.IsNullOrWhiteSpace(v.Value)||v.Value.Length>5000)))throw new FormatException("Invalid character equipment");
        if(Inbox.Any(i=>i is null||string.IsNullOrWhiteSpace(i.Key)||!ProfileValid(i.Profile)||i.ItemName is null||i.Variant is null||i.ListingKey is null||i.ItemName.Length>250||i.Variant.Length>4000||i.ListingKey.Length>4000||i.Price<0||i.Title is null||i.Message is null||i.Url is null||i.Title.Length>1000||i.Message.Length>10000||i.Url.Length>4000))throw new FormatException("Invalid alert inbox");
        if(Rules is null||Trades is null||TargetNotifications is null||Equipment is null||Rules.Count>10000||Trades.Count>100000||TargetNotifications.Count>100000)throw new FormatException("Invalid companion data");
        foreach(var r in Rules)if(r is null||!Guid.TryParse(r.Id,out _)||string.IsNullOrWhiteSpace(r.Name)||r.Name.Length>250||r.Variant is null||r.Variant.Length>4000||r.Maximum<=0||r.Maximum>1_000_000_000_000m)throw new FormatException("Invalid price alert");
        foreach(var t in Trades)if(t is null||!Guid.TryParse(t.Id,out _)||string.IsNullOrWhiteSpace(t.Name)||t.Name.Length>250||t.Variant is null||t.Variant.Length>4000||t.Quantity is <=0 or >1_000_000_000||t.UnitPrice is <=0 or >1_000_000_000_000m||t.Fees is <0 or >1_000_000_000_000m||t.At> DateTimeOffset.UtcNow.AddMinutes(2))throw new FormatException("Invalid trade entry");
        if(Rules.Select(r=>r.Id).Distinct().Count()!=Rules.Count||Trades.Select(t=>t.Id).Distinct().Count()!=Trades.Count)throw new FormatException("Duplicate entry ID");
        if(Rules.Any(r=>!ProfileValid(r.Profile))||Trades.Any(t=>!ProfileValid(t.Profile)))throw new FormatException("Invalid character reference");
        if(Equipment.Any(p=>!EquipmentSlots.Contains(p.Key)||string.IsNullOrWhiteSpace(p.Value)||p.Value.Length>5000))throw new FormatException("Invalid equipment layout");
    }
    void ValidateMerchant()
    {
        bool Identity(string id,string profile,string name)=>Guid.TryParse(id,out _)&&(profile=="main"||Guid.TryParse(profile,out _))&&!string.IsNullOrWhiteSpace(name)&&name.Length<=250;
        if(Goals is null||Restocks is null||Wishes is null||Memos is null||Sessions is null||LowStock is null||!MerchantTools.Themes.Contains(Theme))throw new FormatException("Invalid merchant preferences");
        if(Goals.Count+Restocks.Count+Wishes.Count+Memos.Count+Sessions.Count>50000||LowStock.Count>50000)throw new FormatException("Too many merchant entries");
        if(Goals.Any(x=>x is null||!Identity(x.Id,x.Profile,x.Name)||x.Target is <=0 or >1_000_000_000_000m)||Restocks.Any(x=>x is null||!Identity(x.Id,x.Profile,x.Name)||x.Minimum is <=0 or >1_000_000_000)||Wishes.Any(x=>x is null||!Identity(x.Id,x.Profile,x.Name)||string.IsNullOrWhiteSpace(x.Variant)||x.Variant.Length>4000||x.Maximum is <0 or >1_000_000_000_000m)||Memos.Any(x=>x is null||!Identity(x.Id,x.Profile,x.Name)||!MerchantTools.Tags.Contains(x.Tag)||x.Notes is null||x.Notes.Length>4000)||Sessions.Any(x=>x is null||!Identity(x.Id,x.Profile,x.Name)||x.Start>DateTimeOffset.UtcNow.AddMinutes(2)||x.End<x.Start||x.End>DateTimeOffset.UtcNow.AddMinutes(2)||x.StartMesos is <0 or >1_000_000_000_000m||x.EndMesos is <0 or >1_000_000_000_000m))throw new FormatException("Invalid merchant entry");
        var ids=Goals.Select(x=>x.Id).Concat(Restocks.Select(x=>x.Id)).Concat(Wishes.Select(x=>x.Id)).Concat(Memos.Select(x=>x.Id)).Concat(Sessions.Select(x=>x.Id)).ToList();if(ids.Distinct().Count()!=ids.Count)throw new FormatException("Duplicate merchant entry");
        if(Sessions.Where(s=>s.End is null).GroupBy(s=>s.Profile).Any(g=>g.Count()>1))throw new FormatException("Only one active session per character");
    }
    public void Save(string path){Validate();var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this,Json.Options));File.Move(temp,path,true);}
    public static bool Matches(PriceRule rule,Observation row,DateTimeOffset now,int freshHours)=>rule.Enabled&&TextKey.Search(rule.Name)==TextKey.Search(row.Name)&&row.Kind==EvidenceKind.Asking&&row.Available&&row.UnitPrice>0&&row.UnitPrice<=rule.Maximum&&row.Fresh(now,TimeSpan.FromHours(freshHours))&&row.RetrievedAt>=now.AddMinutes(-30)&&(!row.Equipment||(row.VariantComplete&&rule.Variant.Length>0&&rule.Variant==row.Variant));
    public static string TargetKey(PriceRule rule,Observation row)=>rule.Id+":"+row.Key+":"+row.UnitPrice.ToString(CultureInfo.InvariantCulture);
    public static Dictionary<string,decimal> Stats(string? variant)
    {
        var result=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);foreach(var part in (variant??"").Split(';')){var pair=part.Split('=',2);if(pair.Length==2&&decimal.TryParse(pair[1],NumberStyles.Number,CultureInfo.InvariantCulture,out var n))result[pair[0].Trim()]=n;}return result;
    }
    public static List<TradeOutcome> Outcomes(IEnumerable<TradeEntry> trades)
    {
        var lots=new Dictionary<string,Queue<(long quantity,decimal cost)>>();var result=new List<TradeOutcome>();
        foreach(var t in trades.OrderBy(t=>t.At).ThenBy(t=>t.Buy?0:1).ThenBy(t=>t.Id,StringComparer.Ordinal))
        {
            string key=t.Profile+"|"+TextKey.Search(t.Name)+"|"+t.Variant;if(!lots.TryGetValue(key,out var queue))lots[key]=queue=new();
            if(t.Buy){queue.Enqueue((t.Quantity,t.UnitPrice+t.Fees/t.Quantity));continue;}
            long remaining=t.Quantity;decimal profit=0;
            while(remaining>0&&queue.Count>0)
            {
                var lot=queue.Peek();long used=Math.Min(remaining,lot.quantity);profit+=used*(t.UnitPrice-t.Fees/t.Quantity-lot.cost);remaining-=used;
                queue.Dequeue();if(used<lot.quantity){var rest=queue.ToArray();queue.Clear();queue.Enqueue((lot.quantity-used,lot.cost));foreach(var r in rest)queue.Enqueue(r);}
            }
            result.Add(new(t.Id,profit,remaining));
        }
        return result;
    }
    public static List<ActivityRow> Activity(IEnumerable<Observation> history,DateTimeOffset now,int days=7)
    {
        var rows=history.Where(o=>o.Kind==EvidenceKind.Asking&&o.ObservedAt is {} at&&at<=now&&at>=now.AddDays(-days)&&!string.IsNullOrWhiteSpace(o.Seller)&&(!o.Equipment||o.VariantComplete));var result=new List<ActivityRow>();
        foreach(var group in rows.GroupBy(o=>o.MatchKey))
        {
            var daily=group.GroupBy(o=>o.ObservedAt!.Value.UtcDateTime.Date).Select(g=>new{Date=g.Key,Rows=ValuationEngine.Deduplicate(g)}).Where(g=>g.Rows.Select(o=>TextKey.Normalize(o.Seller)).Distinct().Count()>=3).OrderBy(g=>g.Date).ToList();
            if(daily.Count<2)continue;var first=daily.First();var last=daily.Last();var item=group.First();result.Add(new(item.Name,item.Variant,new DateTimeOffset(first.Date,TimeSpan.Zero),new DateTimeOffset(last.Date,TimeSpan.Zero),ValuationEngine.Median(first.Rows.Select(o=>o.UnitPrice)),ValuationEngine.Median(last.Rows.Select(o=>o.UnitPrice)),first.Rows.Count,last.Rows.Count));
        }
        return result.OrderByDescending(r=>Math.Abs(r.LatestMedian/r.EarlierMedian-1)).ToList();
    }
}

public sealed record PersonalBackup(int Version,List<SavedItem> Items,CompanionFeatures Features)
{
    public List<CharacterInventory>? Characters {get;init;}
    public string ActiveCharacter {get;init;}="main";
    public List<TransferRecord> Transfers {get;init;}=[];
    public void Validate()
    {
        if(Version is not (1 or 2 or 3)||Items is null||Features is null||Items.Count>100000)throw new FormatException("Unsupported backup");Features.Validate();ValidateItems(Items);
        if(Transfers is null||Transfers.Count>100000||Transfers.Any(t=>t is null||!Guid.TryParse(t.Id,out _)||t.From==t.To||(t.From!="main"&&!Guid.TryParse(t.From,out _))||(t.To!="main"&&!Guid.TryParse(t.To,out _))||t.Quantity is <0 or >1_000_000_000||t.Mesos is <0 or >1_000_000_000_000m||t.Item is null||t.Variant is null||t.FromName is null||t.ToName is null)||Transfers.Select(t=>t.Id).Distinct().Count()!=Transfers.Count)throw new FormatException("Invalid transfer history");
        if(Version>=2){if(Characters is null||Characters.Count is <1 or >100||Characters.Any(c=>c is null||c.Character is null||c.Items is null)||!Characters.Any(c=>c.Character.Id=="main")||!Characters.Any(c=>c.Character.Id==ActiveCharacter)||Characters.Select(c=>c.Character.Id).Distinct().Count()!=Characters.Count)throw new FormatException("Invalid character backup");foreach(var c in Characters){if((c.Character.Id!="main"&&!Guid.TryParse(c.Character.Id,out _))||string.IsNullOrWhiteSpace(c.Character.Name)||c.Character.Name.Length>80||c.Character.Mesos is <0 or >1_000_000_000_000m)throw new FormatException("Invalid character");ValidateItems(c.Items);}}
    }
    static void ValidateItems(List<SavedItem> items){if(items.Count>100000)throw new FormatException("Too many saved items");foreach(var i in items)if(i is null||string.IsNullOrWhiteSpace(i.Name)||i.Name.Length>250||i.Quantity is <0 or >1_000_000_000||i.Variant is null||i.Variant.Length>4000)throw new FormatException("Invalid saved item");if(items.Select(ItemIdentity.Key).Distinct().Count()!=items.Count)throw new FormatException("Duplicate saved item");}
}
