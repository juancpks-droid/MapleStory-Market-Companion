using System.Runtime.InteropServices;

namespace Market.App;
// A small native tray icon allows NIIF_NOSOUND, which NotifyIcon does not expose.
sealed class DesktopTray : NativeWindow, IDisposable
{
    const uint Callback=0x8001;
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct Data
    {
        public uint cbSize;public IntPtr hWnd;public uint uID,uFlags,uCallbackMessage;public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string szTip;
        public uint dwState,dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string szInfoTitle;
        public uint dwInfoFlags;public Guid guidItem;public IntPtr hBalloonIcon;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,EntryPoint="Shell_NotifyIconW")]
    static extern bool Notify(uint message,ref Data data);
    Data data;bool visible;
    public event EventHandler? DoubleClick;
    public event EventHandler? NotificationClick;
    public ContextMenuStrip? ContextMenuStrip{get;set;}
    public string BalloonTipTitle{get;set;}="";
    public string BalloonTipText{get;set;}="";
    public bool Sound{get;set;}
    public bool Visible{get=>visible;set{if(value==visible)return;if(value){Notify(0,ref data);}else{Notify(2,ref data);}visible=value;}}
    public DesktopTray()
    {
        CreateHandle(new CreateParams(){Caption="Maple Market notifications"});
        data=new(){cbSize=(uint)Marshal.SizeOf<Data>(),hWnd=Handle,uID=1,uFlags=1|2|4,uCallbackMessage=Callback,hIcon=AppArtwork.Icon.Handle,szTip="MapleStory Classic Market Companion",szInfo="",szInfoTitle=""};Visible=true;
    }
    public bool ShowBalloonTip(int milliseconds)
    {
        data.uFlags=0x10;data.szInfoTitle=BalloonTipTitle.Length>63?BalloonTipTitle[..63]:BalloonTipTitle;data.szInfo=BalloonTipText.Length>255?BalloonTipText[..255]:BalloonTipText;data.uTimeoutOrVersion=(uint)milliseconds;data.dwInfoFlags=1u|(Sound?0u:0x10u);bool result=Notify(1,ref data);data.uFlags=1|2|4;return result;
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==Callback){int action=(int)(m.LParam.ToInt64()&0xffff);if(action==0x203)DoubleClick?.Invoke(this,EventArgs.Empty);if(action==0x405)NotificationClick?.Invoke(this,EventArgs.Empty);if(action==0x205)ContextMenuStrip?.Show(Cursor.Position);}
        base.WndProc(ref m);
    }
    public void Dispose(){Visible=false;ContextMenuStrip?.Dispose();DestroyHandle();}
}

