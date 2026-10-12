using Market.Core;

using System.Diagnostics;

using System.Text.Json;



namespace Market.App;

static class Program

{

    [STAThread]

    static void Main(string[] args)

    {

        Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        ApplicationConfiguration.Initialize();
        if(args.Contains("--status-smoke")){StatusBarSmoke.Run();return;}
        if(args.Contains("--reference-sources-smoke")){ReferenceSourcesSmoke.Run();return;}
        if(args.Contains("--lookup-smoke")){PriceLookupSmoke.Run();return;}
        if(args.Contains("--session-smoke")){SessionSmoke.Run();return;}
        if(args.Contains("--shortcut-smoke")){DesktopShortcut.Smoke();ShellIconRefresh.Smoke();return;}
        if(args.Contains("--inline-drops-smoke")){InlineDropsSmoke.Run();return;}
        if(args.Contains("--quest-drops-smoke")){QuestDropsSmoke.Run();return;}
        if(args.Contains("--npc-fallback-smoke")){QuestDropsSmoke.Run(true);return;}
        if(args.Contains("--drops-smoke")){DropReferenceSmoke.Run();return;}
        if(args.Contains("--finder-smoke")){FinderSmoke.Run();return;}
        if(args.Length==3&&args[0]=="--updater-smoke"){UpdaterSmoke.Run(args[1],args[2]).GetAwaiter().GetResult();return;}
        if(args.Contains("--adblock-smoke")){AdBlockSmoke.Run();return;}

        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MapleStoryClassicMarketCompanion");

        bool smoke=args.Contains("--smoke")||args.Contains("--viewer-smoke");
        if(args.Contains("--viewer-smoke")){WebsiteViewerSmoke.Run();return;}

        if(smoke) directory=Path.Combine(AppContext.BaseDirectory,"smoke-data");

        Directory.CreateDirectory(directory);

        Application.ThreadException += (_,e) => { File.AppendAllText(Path.Combine(directory,"diagnostics.log"),$"{DateTimeOffset.UtcNow:O} {e.Exception.GetType().Name}: {e.Exception.Message}\n"); MessageBox.Show("The operation could not finish. Your saved data has been kept. See diagnostics.log in the data folder.","Market Companion"); };

        using var single=new Mutex(true,smoke?"Local\\MapleClassicMarketCompanionSmoke":"Local\\MapleClassicMarketCompanion",out bool first);

        if(!first) { MessageBox.Show("Market Companion is already running. Check the system tray."); return; }
        if(!smoke){DesktopShortcut.EnsureOnce(directory);ShellIconRefresh.Run();}

        try

        {

            string databasePath=Path.Combine(directory,"market.db");bool firstRun=!File.Exists(databasePath);

            using var store=new Store(databasePath);

            var defaultsPath=Path.Combine(AppContext.BaseDirectory,"settings-defaults.json");

            if(firstRun && File.Exists(defaultsPath)){var defaults=JsonSerializer.Deserialize<Settings>(File.ReadAllText(defaultsPath),Json.Options)??new Settings();defaults.Sanitize();store.SaveSettings(defaults);}

            using var form=new MainForm(store,directory);

            if(smoke) form.Shown += async (_,_) =>

            {

                try

                {

                    await Task.Delay(700);

                    form.CapturePreview(Path.Combine(directory,"dashboard.png"));

                    var result=await form.Smoke();

                    form.ShowPage("Price Checker"); form.CapturePreview(Path.Combine(directory,"price-checker.png"));

                    form.ShowPage("Settings"); form.CapturePreview(Path.Combine(directory,"settings.png"));

                    form.ShowPage("Dashboard");

                    using var proc=Process.GetCurrentProcess(); var cpu=proc.TotalProcessorTime; var started=DateTime.UtcNow;

                    var maximumWorking=proc.WorkingSet64; var maximumPrivate=proc.PrivateMemorySize64;

                    for(int i=0;i<(args.Contains("--soak")?60:12);i++) { await Task.Delay(5000); proc.Refresh(); maximumWorking=Math.Max(maximumWorking,proc.WorkingSet64); maximumPrivate=Math.Max(maximumPrivate,proc.PrivateMemorySize64); }

                    proc.Refresh(); var elapsed=(DateTime.UtcNow-started).TotalSeconds;

                    File.WriteAllText(Path.Combine(directory,"smoke-results.json"),JsonSerializer.Serialize(new {status="passed",checks=result,seconds=elapsed,localAnalysisTicks=form.AnalysisTicks,timerTicks=form.TimerTicks.Select(t=>t.ToString("O")),cpuSeconds=(proc.TotalProcessorTime-cpu).TotalSeconds,averageCpuPercentOfOneCore=(proc.TotalProcessorTime-cpu).TotalSeconds/elapsed*100,workingSetMb=proc.WorkingSet64/1048576d,peakSampledWorkingSetMb=maximumWorking/1048576d,privateMb=proc.PrivateMemorySize64/1048576d,peakSampledPrivateMb=maximumPrivate/1048576d,limitations="Kerning public source enabled. Windows accepted the notification request; visual OS delivery not asserted. This is a bounded smoke/soak run, not long-term production validation."},Json.Options));

                }

                catch(Exception e) { File.WriteAllText(Path.Combine(directory,"smoke-results.json"),JsonSerializer.Serialize(new {status="failed",error=e.ToString()},Json.Options)); }

                finally { form.ExitApplication(); }

            };

            Application.Run(form);

        }

        catch(Exception e) {File.AppendAllText(Path.Combine(directory,"diagnostics.log"),e.ToString()); if(smoke){File.WriteAllText(Path.Combine(directory,"smoke-results.json"),JsonSerializer.Serialize(new{status="failed",error=e.ToString()},Json.Options));Environment.ExitCode=1;}else MessageBox.Show("Could not start the application. Details are in " + directory + "\\diagnostics.log","Startup error");}

    }

}
