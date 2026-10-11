using Market.Core;

namespace Market.App;
public sealed partial class MainForm
{
    void SmokeBasilCompletion()
    {
        // Isolated desktop smoke data only; never called by a production UI action.
        var oldSources=settings.Sources;var oldRows=observations;var oldInbox=features.Inbox.ToList();var oldToggle=settings.BasilCompletionNotifications;var oldBusy=busy;
        try
        {
            var now=DateTimeOffset.UtcNow;var source=new SourceConfig(){Id="BasilMarket",Enabled=true,PermissionReference="Synthetic smoke only",BasilPages=["/market/sell?type=etc"]};source.BasilVisited[source.BasilPages[0]]=now;
            var row=new Observation(){Provider="BasilMarket",ListingId="smoke",Name="Synthetic smoke only",Kind=EvidenceKind.Asking,Available=true,UnitPrice=10,Quantity=1,PriceApproximate=false,Url="https://basilmarket.net/market/listing/1"};
            settings.Sources=[source];observations=[row];busy=false;settings.BasilCompletionNotifications=true;BasilCompletion.Observe(observations,source,now);
            CheckBasilCompletionInbox();CheckBasilCompletionInbox();var entry=features.Inbox.Single(i=>i.Key==source.BasilCompletionKey);
            if(!BasilCompletionStillEligible(entry))throw new Exception("Completion alert not eligible");
            settings.BasilCompletionNotifications=false;if(BasilCompletionStillEligible(entry))throw new Exception("Completion opt-out ignored");
            settings.BasilCompletionNotifications=true;observations=[row with{PriceApproximate=true}];if(BasilCompletionStillEligible(entry))throw new Exception("Old completion alert remained eligible");
            features.Inbox.RemoveAll(i=>i.Key==source.BasilCompletionKey);observations=[row];CheckBasilCompletionInbox();if(features.Inbox.Any(i=>i.Key==source.BasilCompletionKey))throw new Exception("Dismissed completion repeated");
        }
        finally{settings.Sources=oldSources;observations=oldRows;features.Inbox=oldInbox;settings.BasilCompletionNotifications=oldToggle;busy=oldBusy;store.SaveSettings(settings);SaveFeatures();}
    }
    bool BasilCompletionStillEligible(InboxEntry entry)
    {
        if(!entry.Key.StartsWith("basil-complete:"))return true;
        var source=settings.Sources.FirstOrDefault(s=>s.Id=="BasilMarket");
        return !busy&&!Offline&&source is not null&&BasilCompletion.CanDeliver(entry.Key,observations,source,DateTimeOffset.UtcNow,settings.BasilCompletionNotifications);
    }
    void CheckBasilCompletionInbox()
    {
        if(busy||!settings.BasilCompletionNotifications)return;
        var source=settings.Sources.FirstOrDefault(s=>s.Id=="BasilMarket");
        if(source is null||!BasilCompletion.CanDeliver(source.BasilCompletionKey,observations,source,DateTimeOffset.UtcNow,true)||source.BasilAnnouncedKey==source.BasilCompletionKey)return;
        if(!features.Inbox.Any(i=>i.Key==source.BasilCompletionKey))AddInbox(source.BasilCompletionKey,"BasilMarket scan finished","The discovered sale listings and category pass are checked. Some prices may still be rounded. New listings can start another queue.","");
        source.BasilAnnouncedKey=source.BasilCompletionKey;store.SaveSettings(settings);
    }
}
