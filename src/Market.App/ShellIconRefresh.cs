using System.Runtime.InteropServices;
namespace Market.App;
internal static class ShellIconRefresh
{
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
 static extern void SHChangeNotify(uint eventId,uint flags,string item,IntPtr other);
 internal static int RefreshFolder(string folder,string[] targets,string icon){
  if(!Directory.Exists(folder)||!File.Exists(icon))return 0;
  object? shell=null;int changed=0;
  try{
   shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);dynamic host=shell!;
   foreach(var file in Directory.EnumerateFiles(folder,"*.lnk")){
    object? shortcut=null;
    try{
     shortcut=host.CreateShortcut(file);dynamic link=shortcut;
     string target=link.TargetPath;
     if(!targets.Any(t=>string.Equals(t,target,StringComparison.OrdinalIgnoreCase)))continue;
     string location=icon+",0";
     if(string.Equals((string)link.IconLocation,location,StringComparison.OrdinalIgnoreCase))continue;
     link.IconLocation=location;link.Save();changed++;
     SHChangeNotify(0x2000,0x1005,file,IntPtr.Zero); // Update this shortcut only; Unicode path, flush notification.
    }catch(COMException){}catch(IOException){}catch(UnauthorizedAccessException){}
    finally{if(shortcut is not null)Marshal.FinalReleaseComObject(shortcut);}
   }
  }finally{if(shell is not null)Marshal.FinalReleaseComObject(shell);}
  return changed;
 }
 internal static void Run(){
  // Installed app only. Preserve shortcut targets, arguments, names and pinning.
  if(!File.Exists(Path.Combine(AppContext.BaseDirectory,"sq.version")))return;
  try{
   var root=Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.FullName;
   string[] targets=[DesktopShortcut.Target,Path.Combine(root,"MapleStory Market Companion.exe")];
   var icon=Path.Combine(AppContext.BaseDirectory,"Assets","maple-crest-v3.ico");
   string[] folders=[Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.Programs),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","Internet Explorer","Quick Launch","User Pinned","TaskBar")];
   foreach(var folder in folders)RefreshFolder(folder,targets,icon);
  }catch(Exception e) when(e is IOException or UnauthorizedAccessException or COMException){System.Diagnostics.Debug.WriteLine(e.Message);}
 }
 internal static void Smoke(){
  var folder=Path.Combine(AppContext.BaseDirectory,"shell-icon-smoke");Directory.CreateDirectory(folder);
  dynamic host=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
  try{
   foreach(var name in new[]{"app","unrelated"}){
    dynamic l=host.CreateShortcut(Path.Combine(folder,name+".lnk"));l.TargetPath=name=="app"?DesktopShortcut.Target:Path.Combine(Environment.SystemDirectory,"notepad.exe");l.Arguments="--preserve-test";l.IconLocation=DesktopShortcut.Target+",0";l.Save();Marshal.FinalReleaseComObject(l);
   }
   var icon=Path.Combine(AppContext.BaseDirectory,"Assets","maple-crest-v3.ico");
   if(RefreshFolder(folder,[DesktopShortcut.Target],icon)!=1||RefreshFolder(folder,[DesktopShortcut.Target],icon)!=0)throw new Exception("Refresh was not targeted and idempotent");
   dynamic link=host.CreateShortcut(Path.Combine(folder,"app.lnk"));
   if((string)link.IconLocation!=icon+",0"||(string)link.Arguments!="--preserve-test"||(string)link.TargetPath!=DesktopShortcut.Target)throw new Exception("Shortcut metadata changed");Marshal.FinalReleaseComObject(link);
   File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: stale icon reference replaced; target and arguments preserved; unrelated shortcut untouched; repeat migration makes no changes.");
  }finally{Marshal.FinalReleaseComObject(host);}
 }
}
