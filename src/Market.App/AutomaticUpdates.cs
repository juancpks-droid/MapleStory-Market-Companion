using Velopack;
using Velopack.Sources;

namespace Market.App;

public sealed partial class MainForm
{
    internal const string UpdateRepository="https://github.com/juancpks-droid/MapleStory-Market-Companion";
    readonly System.Windows.Forms.Timer updateTimer=new(){Interval=1000};
    readonly Label updateStatus=new(){Width=820,Height=48};
    readonly CheckBox automaticUpdates=new(){Text="Automatically check for updates (ask before downloading and restarting)",Width=800,Height=30};
    readonly Panel updateBanner=new(){Dock=DockStyle.Bottom,Height=82,Visible=false,Padding=new Padding(12),BackColor=Color.FromArgb(255,235,181)};
    readonly Label updateBannerText=new(){Dock=DockStyle.Fill,Padding=new Padding(0,8,8,0)};
    Button? restartUpdateButton;
    UpdateManager? updateManager;
    VelopackAsset? readyUpdate;
    bool checkingUpdate,updateDeferred;
    string declinedUpdateVersion="";
    int restartSeconds=30;
    DateTimeOffset nextUpdateCheck=DateTimeOffset.MinValue;

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
        form.Controls.Add(Button("Create desktop shortcut",()=>{try{DesktopShortcut.CreateOnDesktop();updateStatus.Text="Desktop shortcut created: MapleStory Market Companion.";}catch(Exception e){MessageBox.Show("Could not create the desktop shortcut: "+e.Message,"Desktop shortcut");}}));
        form.Controls.Add(new Label(){Text="A pop-up asks before downloading and restarting. Yes installs the update without an idle wait. No keeps this version running. Active refreshes, other app windows and unsaved Settings/Trading Sessions pages must finish first.",Width=820,Height=48});
    }

    void InitializeUpdates(Panel main)
    {
        updateBanner.Controls.Add(updateBannerText);
        var updateActions=new FlowLayoutPanel(){Dock=DockStyle.Right,Width=265,WrapContents=false,Padding=new Padding(0,5,0,0)};
        restartUpdateButton=Button("Restart now",()=>ApplyAppUpdate());restartUpdateButton.Enabled=false;restartUpdateButton.BackColor=Accent;updateActions.Controls.Add(restartUpdateButton);
        updateActions.Controls.Add(Button("Later",()=>{updateDeferred=true;updateBanner.Visible=false;updateStatus.Text="Update downloaded. It will be offered again next launch, or select Check for updates.";}));
        updateBanner.Controls.Add(updateActions);
        main.Controls.Add(updateBanner);content.BringToFront();
        try{
            updateManager=new UpdateManager(new GithubSource(UpdateRepository,null,false));
            updateStatus.Text=updateManager.IsInstalled?"Automatic updates are ready. Checking at startup and every hour.":"Install the Setup.exe version once to enable automatic updates.";
        }catch(Exception){updateStatus.Text="Updater could not start. You can reinstall from the GitHub releases page.";}


        FormClosed+=(_,_)=>{updateTimer.Stop();updateTimer.Dispose();};
        updateTimer.Tick+=async (_,_)=>{
            if(closing)return;
            if(readyUpdate is not null&&!updateDeferred&&CanRestartForUpdate())ApplyAppUpdate();
            else if(automaticUpdates.Checked&&!checkingUpdate&&readyUpdate is null&&DateTimeOffset.UtcNow>=nextUpdateCheck&&!Offline)await CheckAppUpdates(false);
        };
        if(!Environment.GetCommandLineArgs().Any(a=>a.Contains("smoke",StringComparison.OrdinalIgnoreCase)))updateTimer.Start();
    }

    bool CanRestartForUpdate()=>!closing&&!busy&&!checkingUpdate&&Application.OpenForms.Count==1&&selected is not ("Settings" or "Trading Sessions");
    static bool ContainsFocusedEditor(Control control)=>control.Controls.Cast<Control>().Any(c=>(c.Focused&&c is TextBoxBase or NumericUpDown or ComboBox)||ContainsFocusedEditor(c));
    bool ConfirmAppUpdate(string version)=>MessageBox.Show(this,$"A new update ({version}) is available.\n\nDo you want to download the update and restart now?\nYour saved inventory and settings will be kept.","New update available",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes;

    async Task CheckAppUpdates(bool manual)
    {
        if(checkingUpdate||closing)return;
        nextUpdateCheck=DateTimeOffset.UtcNow.AddHours(1);
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
                string version=update.TargetFullRelease.Version.ToString();
                if(!manual&&declinedUpdateVersion==version)return;
                if(!ConfirmAppUpdate(version)){declinedUpdateVersion=version;updateDeferred=true;updateStatus.Text="Update skipped. Use Check for updates whenever you're ready.";return;}
                declinedUpdateVersion="";updateDeferred=false;
                updateBanner.Visible=true;updateBannerText.Text="A new version is available. Downloading in the background…";
                var progress=new Progress<int>(p=>{if(!closing&&!IsDisposed){updateStatus.Text=$"Downloading update: {p}%";updateBannerText.Text=updateStatus.Text;}});
                await updateManager.DownloadUpdatesAsync(update,p=>((IProgress<int>)progress).Report(p),stop.Token);
                if(closing||IsDisposed)return;
                readyUpdate=update.TargetFullRelease;
            }
            else if(!ConfirmAppUpdate(readyUpdate.Version.ToString())){updateDeferred=true;return;}
            restartSeconds=30;
            restartUpdateButton!.Enabled=true;
            updateStatus.Text="Update ready to restart. Finish any refresh or edits and close other app windows, or choose Later.";
            updateBannerText.Text=updateStatus.Text;updateBanner.Visible=!updateDeferred;
        }catch(OperationCanceledException){}
        catch(Exception e){
            if(!closing&&!IsDisposed){updateStatus.Text="Update check or download failed. Your current version still works. Retry here later.";updateBanner.Visible=false;try{File.AppendAllText(Path.Combine(dataDirectory,"diagnostics.log"),$"{DateTimeOffset.UtcNow:O} Update: {e.GetType().Name}: {e.Message}\n");}catch{}}
        }finally{checkingUpdate=false;}
        if(readyUpdate is not null&&!updateDeferred)ApplyAppUpdate();
    }

    void ApplyAppUpdate()
    {
        if(readyUpdate is null||updateManager is null)return;
        // Explicit restart still preserves modal edits and in-flight database/network work.
        if(busy||checkingUpdate||Application.OpenForms.Count>1){updateBannerText.Text="Finish the current refresh and close other app windows before restarting.";return;}
        if(selected is "Settings" or "Trading Sessions"){
            updateBannerText.Text="Save your edits and leave this page before restarting.";return;
        }
        try{
            SaveViewSession();SaveFeatures();store.SaveSettings(settings);WriteRecoveryBackup("before-app-update");
            updateManager.WaitExitThenApplyUpdates(readyUpdate,silent:false,restart:true);
            ExitApplication();
        }catch(Exception e){updateDeferred=true;updateStatus.Text="Could not prepare the update. Current version kept; retry using Check for updates.";updateBannerText.Text=updateStatus.Text;try{File.AppendAllText(Path.Combine(dataDirectory,"diagnostics.log"),e.ToString());}catch{}}
    }

}


