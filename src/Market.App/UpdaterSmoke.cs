using System.Text.Json;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Market.App;

static class UpdaterSmoke
{
    public static async Task Run(string feedPath,string reportPath)
    {
        var checks=new List<string>();
        var root=Path.Combine(Path.GetDirectoryName(reportPath)!,"updater-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try{
            var source=new SimpleFileSource(new DirectoryInfo(feedPath));
            UpdateManager Manager(string version,string folder)=>new(source,null,new TestVelopackLocator("MapleStoryMarketCompanion",version,Path.Combine(root,folder)));
            var old=Manager("1.17.0","old");
            var update=await old.CheckForUpdatesAsync()??throw new Exception("New version not found");
            if(update.TargetFullRelease.Version.ToString()!="1.18.0")throw new Exception("Unexpected version");
            checks.Add("An older installation discovers 1.18.0");
            await old.DownloadUpdatesAsync(update);
            if(old.UpdatePendingRestart is null)throw new Exception("Verified download was not staged");
            checks.Add("Download is verified and staged for restart");
            if(await Manager("1.18.0","same").CheckForUpdatesAsync() is not null)throw new Exception("Same version offered");
            checks.Add("Same version is not reinstalled");
            if(await Manager("9.0.0","newer").CheckForUpdatesAsync() is not null)throw new Exception("Downgrade offered");
            checks.Add("Older releases do not downgrade an installation");
            var badFeed=Path.Combine(root,"bad-feed");Directory.CreateDirectory(badFeed);
            foreach(var path in Directory.GetFiles(feedPath,"*.json"))File.Copy(path,Path.Combine(badFeed,Path.GetFileName(path)));
            var asset=update.TargetFullRelease;
            File.WriteAllText(Path.Combine(badFeed,asset.FileName),"corrupt download");
            var bad=new UpdateManager(new SimpleFileSource(new DirectoryInfo(badFeed)),null,new TestVelopackLocator("MapleStoryMarketCompanion","1.17.0",Path.Combine(root,"bad-packages")));
            bool rejected=false;
            try{await bad.DownloadUpdatesAsync((await bad.CheckForUpdatesAsync())!);}catch(Exception){rejected=true;}
            if(!rejected||bad.UpdatePendingRestart is not null)throw new Exception("Corrupt package not rejected");
            checks.Add("Corrupt package is rejected and never staged");
            Directory.CreateDirectory(Path.Combine(root,"missing-feed"));
            var missing=new UpdateManager(new SimpleFileSource(new DirectoryInfo(Path.Combine(root,"missing-feed"))),null,new TestVelopackLocator("MapleStoryMarketCompanion","1.17.0",Path.Combine(root,"missing-packages")));
            try{await missing.CheckForUpdatesAsync();}catch{checks.Add("Unavailable release feed fails without changing installed files");}
            File.WriteAllText(reportPath,JsonSerializer.Serialize(new{status="passed",checks,limitation="Local release package tests; live GitHub and installed restart tested separately."},new JsonSerializerOptions{WriteIndented=true}));
        }catch(Exception e){File.WriteAllText(reportPath,JsonSerializer.Serialize(new{status="failed",checks,error=e.ToString()},new JsonSerializerOptions{WriteIndented=true}));Environment.ExitCode=1;}
    }
}
