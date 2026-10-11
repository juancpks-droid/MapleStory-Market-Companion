using System.Runtime.InteropServices;
namespace Market.App;
internal static class DesktopShortcut
{
    internal static string Target=>Environment.ProcessPath??Path.Combine(AppContext.BaseDirectory,"MapleStoryClassicMarketCompanion.exe");
    internal static string Create(string folder,string target)
    {
        if(!File.Exists(target))throw new FileNotFoundException("The app executable could not be found.");
        Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"MapleStory Market Companion.lnk");
        object? shell=null,shortcut=null;
        try{
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")??throw new InvalidOperationException("Windows shortcut support is unavailable."));
            dynamic host=shell!;shortcut=host.CreateShortcut(path);dynamic link=shortcut;
            link.TargetPath=Path.GetFullPath(target);link.WorkingDirectory=Path.GetDirectoryName(target);link.IconLocation=target+",0";
            link.Description="MapleStory Market Companion";link.Arguments="";link.Save();return path;
        }finally{if(shortcut is not null)Marshal.FinalReleaseComObject(shortcut);if(shell is not null)Marshal.FinalReleaseComObject(shell);}
    }
    internal static string CreateOnDesktop()=>Create(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Target);
    internal static void EnsureOnce(string dataDirectory)
    {
        var marker=Path.Combine(dataDirectory,"desktop-shortcut-v1.txt");
        if(File.Exists(marker)||!File.Exists(Path.Combine(AppContext.BaseDirectory,"sq.version")))return;
        try{CreateOnDesktop();File.WriteAllText(marker,"created");}catch(Exception e){try{File.AppendAllText(Path.Combine(dataDirectory,"diagnostics.log"),"Desktop shortcut: "+e.Message+Environment.NewLine);}catch{}}
    }
    internal static void Smoke()
    {
        var folder=Path.Combine(AppContext.BaseDirectory,"shortcut-smoke-data");Directory.CreateDirectory(folder);
        try{
            var path=Create(folder,Target);Create(folder,Target);
            dynamic host=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;dynamic link=host.CreateShortcut(path);
            try{if(!File.Exists(path)||!string.Equals((string)link.TargetPath,Target,StringComparison.OrdinalIgnoreCase)||(string)link.Arguments!=""||Directory.GetFiles(folder,"*.lnk").Length!=1)throw new Exception("Shortcut target or duplicate check failed");}
            finally{Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(host);}
            File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: real Windows shortcut created, points to the app with no test arguments, and repeat creation does not duplicate it.");
        }catch(Exception e){Environment.ExitCode=1;File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL: "+e);}
    }
}
