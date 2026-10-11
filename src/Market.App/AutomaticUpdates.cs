using Velopack;
using Velopack.Sources;

namespace Market.App;

public sealed partial class MainForm
{
    internal const string UpdateRepository="https://github.com/juancpks-droid/MapleStory-Market-Companion";
    readonly System.Windows.Forms.Timer updateTimer=new(){Interval=1000};
    readonly Label updateStatus=new(){Width=820,Height=48};
    readonly CheckBox automaticUpdates=new(){Text="Automatically download updates and restart when idle",Width=800,Height=30};
    readonly FlowLayoutPanel updateBanner=new(){Dock=DockStyle.Bottom,Height=92,Visible=false,BackColor=Color.FromArgb(255,235,181)};
    readonly Label updateBannerText=new(){Width=800,Height=30};
    UpdateManager? updateManager;
    VelopackAsset? readyUpdate;
    bool checkingUpdate,updateDeferred;
    int restartSeconds=30;
    DateTimeOffset nextUpdateCheck=DateTimeOffset.MinValue;
    DateTimeOffset lastUpdateActivity=DateTimeOffset.UtcNow;
    string UpdatePreferencePath=>Path.Combine(dataDirectory,"automatic-updates.txt");

    void BuildUpdateSettings(FlowLayoutPanel form)
    {
        form.Controls.Add(new Label(){Text="App updates • Version "+Application.ProductVersion.Split('+')[0],Width=800,Height=30});
        automaticUpdates.Checked=!File.Exists(UpdatePreferencePath)||File.ReadAllText(UpdatePreferencePath).Trim()!="off";
        automaticUpdates.CheckedChanged+=(_,_)=>{
            try{File.WriteAllText(UpdatePreferencePath,automaticUpdates.Checked?"on":"off");}
            catch(Exception){updateStatus.Text="Could not save the update preference. Check data-folder access.";}
            restartSeconds=30;
        };
        form.Controls.Add(automaticUpdates);form.Controls.Add(updateStatus);
        form.Controls.Add(Button("Check for updates",async ()=>await CheckAppUpdates(true)));
        form.Controls.Add(new Label(){Text="Automatic restart waits until Dashboard is open, you stop typing or clicking, and other app windows are closed. Choose Later to keep using this version until the next launch. Internet access is required.",Width=820,Height=48});
    }

    void InitializeUpdates(Panel main)
    {
        updateBanner.Controls.Add(updateBannerText);
        updateBanner.Controls.Add(Button("Restart now",()=>ApplyAppUpdate()));
        updateBanner.Controls.Add(Button("Later",()=>{updateDeferred=true;updateBanner.Visible=false;updateStatus.Text="Update downloaded. It will be offered again next launch, or select Check for updates.";}));
        main.Controls.Add(updateBanner);updateBanner.BringToFront();
        try{
            updateManager=new UpdateManager(new GithubSource(UpdateRepository,null,false));
            updateStatus.Text=updateManager.IsInstalled?"Automatic updates are ready. Checking at startup and every 4 hours.":"Install the Setup.exe version once to enable automatic updates.";
        }catch(Exception){updateStatus.Text="Updater could not start. You can reinstall from the GitHub releases page.";}
        var activity=new UpdateActivityFilter(()=>lastUpdateActivity=DateTimeOffset.UtcNow);
        Application.AddMessageFilter(activity);
        FormClosed+=(_,_)=>{updateTimer.Stop();updateTimer.Dispose();Application.RemoveMessageFilter(activity);};
        updateTimer.Tick+=async (_,_)=>{
            if(closing)return;
            if(readyUpdate is not null&&!updateDeferred&&automaticUpdates.Checked){
                if(!CanRestartForUpdate()||DateTimeOffset.UtcNow-lastUpdateActivity<TimeSpan.FromSeconds(30)){restartSeconds=30;updateBannerText.Text="Update ready. Waiting until you're idle to restart (or choose Later).";return;}
                updateBannerText.Text=$"Update ready. Restarting automatically in {restartSeconds} seconds…";
                if(--restartSeconds<0)ApplyAppUpdate();
            }else if(automaticUpdates.Checked&&!checkingUpdate&&readyUpdate is null&&DateTimeOffset.UtcNow>=nextUpdateCheck&&!Offline)await CheckAppUpdates(false);
        };
        if(!Environment.GetCommandLineArgs().Any(a=>a.Contains("smoke",StringComparison.OrdinalIgnoreCase)))updateTimer.Start();
    }

    bool CanRestartForUpdate()=>!closing&&!busy&&!checkingUpdate&&Application.OpenForms.Count==1&&selected=="Dashboard"&&!ContainsFocusedEditor(this);
    static bool ContainsFocusedEditor(Control control)=>control.Controls.Cast<Control>().Any(c=>(c.Focused&&c is TextBoxBase or NumericUpDown or ComboBox)||ContainsFocusedEditor(c));

    async Task CheckAppUpdates(bool manual)
    {
        if(checkingUpdate||closing)return;
        nextUpdateCheck=DateTimeOffset.UtcNow.AddHours(4);
        if(Offline){updateStatus.Text="Offline mode is on. Turn it off to check for updates.";return;}
        if(updateManager is null||!updateManager.IsInstalled){updateStatus.Text="Install Setup.exe from GitHub Releases once to enable automatic updates.";return;}
        checkingUpdate=true;
        if(manual)updateDeferred=false;
        try{
            if(readyUpdate is null){
                updateStatus.Text="Checking GitHub for a newer version…";
                var update=await updateManager.CheckForUpdatesAsync();
                if(closing||IsDisposed)return;
                if(update is null){updateStatus.Text="You're up to date. Last checked "+DateTime.Now.ToString("g");return;}
                updateBanner.Visible=true;updateBannerText.Text="A new version is available. Downloading in the background…";
                var progress=new Progress<int>(p=>{if(!closing&&!IsDisposed){updateStatus.Text=$"Downloading update: {p}%";updateBannerText.Text=updateStatus.Text;}});
                await updateManager.DownloadUpdatesAsync(update,p=>((IProgress<int>)progress).Report(p),stop.Token);
                if(closing||IsDisposed)return;
                readyUpdate=update.TargetFullRelease;
            }
            restartSeconds=30;
            updateStatus.Text="Update downloaded and verified. Restart now, or leave the app idle for automatic installation.";
            updateBannerText.Text=updateStatus.Text;updateBanner.Visible=!updateDeferred;
        }catch(OperationCanceledException){}
        catch(Exception e){
            if(!closing&&!IsDisposed){updateStatus.Text="Update check or download failed. Your current version still works. Retry here later.";updateBanner.Visible=false;try{File.AppendAllText(Path.Combine(dataDirectory,"diagnostics.log"),$"{DateTimeOffset.UtcNow:O} Update: {e.GetType().Name}: {e.Message}\n");}catch{}}
        }finally{checkingUpdate=false;}
    }

    void ApplyAppUpdate()
    {
        if(readyUpdate is null||updateManager is null)return;
        // Explicit restart still preserves modal edits and in-flight database/network work.
        if(busy||checkingUpdate||Application.OpenForms.Count>1){updateBannerText.Text="Finish the current refresh and close other app windows before restarting.";return;}
        if(selected!="Dashboard"||ContainsFocusedEditor(content)){
            updateBannerText.Text="Save your edits and open Dashboard before restarting.";return;
        }
        try{
            SaveFeatures();store.SaveSettings(settings);WriteRecoveryBackup("before-app-update");
            updateManager.WaitExitThenApplyUpdates(readyUpdate,silent:false,restart:true);
            ExitApplication();
        }catch(Exception e){updateDeferred=true;updateStatus.Text="Could not prepare the update. Current version kept; retry using Check for updates.";updateBannerText.Text=updateStatus.Text;try{File.AppendAllText(Path.Combine(dataDirectory,"diagnostics.log"),e.ToString());}catch{}}
    }

    sealed class UpdateActivityFilter(Action activity):IMessageFilter
    {
        public bool PreFilterMessage(ref Message message){if(message.Msg is 0x100 or 0x104 or 0x201 or 0x204 or 0x207 or 0x20A)activity();return false;}
    }
}
