namespace Market.App;

public sealed partial class MainForm
{
    Button sidebarUpdateButton=null!;
    readonly Label sidebarVersion=new(){Dock=DockStyle.Top,Height=26,TextAlign=ContentAlignment.MiddleCenter};
    readonly Label sidebarUpdateStatus=new(){Dock=DockStyle.Fill,AutoEllipsis=true,Padding=new Padding(0,5,0,0)};

    Control BuildSidebarUpdates()
    {
        var panel=new Panel(){Dock=DockStyle.Bottom,Height=155,Padding=new Padding(0,8,0,0)};
        sidebarVersion.Text="Installed: v"+Application.ProductVersion.Split('+')[0];
        sidebarVersion.AccessibleName="Installed app version";
        sidebarUpdateButton=Button("Check for updates",async ()=>{
            sidebarUpdateButton.Enabled=false;
            try{await CheckAppUpdates(true);}
            finally{if(!IsDisposed&&!closing)sidebarUpdateButton.Enabled=true;}
        });
        sidebarUpdateButton.AutoSize=false;sidebarUpdateButton.Dock=DockStyle.Top;sidebarUpdateButton.Height=38;
        sidebarUpdateButton.AccessibleName="Check for app updates";
        sidebarUpdateStatus.AccessibleName="App update status";
        updateStatus.TextChanged+=(_,_)=>sidebarUpdateStatus.Text=updateStatus.Text;
        panel.Controls.Add(sidebarUpdateStatus);panel.Controls.Add(sidebarUpdateButton);panel.Controls.Add(sidebarVersion);
        sidebarUpdateStatus.BringToFront();
        return panel;
    }
}
