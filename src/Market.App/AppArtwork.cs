namespace Market.App;
internal static class AppArtwork
{
    internal static readonly Icon Icon=Load();
    static Icon Load(){using var stream=typeof(AppArtwork).Assembly.GetManifestResourceStream("market-app-icon")??throw new InvalidOperationException("App icon missing");using var icon=new Icon(stream);return (Icon)icon.Clone();}
}
