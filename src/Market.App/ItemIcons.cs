using Market.Core;
using System.Drawing.Drawing2D;
using System.Text.Json;

namespace Market.App;

public sealed class IconCatalog
{
    public sealed record Entry(string Id,string Name);
    readonly Dictionary<string,Entry[]> names;
    public IEnumerable<string> Names=>Entries.Select(e=>e.Name).Distinct();
    public IEnumerable<Entry> Entries=>names.Values.SelectMany(v=>v).Where(e=>ItemFinder.HasEnglishDisplayName(e.Name));
    public IconCatalog(){var entries=JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"icon-catalog-gms83.json")))??[];entries=entries.Select(e=>e with{Name=ScrollNames.LegacyName(e.Id,e.Name)}).ToArray();names=entries.GroupBy(e=>TextKey.Search(e.Name)).ToDictionary(g=>g.Key,g=>g.ToArray());}
    public string Resolve(string id,string name){if(!names.TryGetValue(TextKey.Search(name),out var matches))return "";return matches.FirstOrDefault(e=>e.Id==id)?.Id??(matches.Length==1?matches[0].Id:"");}
}

public sealed class ItemIconCache : IDisposable
{
    public bool Offline {get;set;}
    readonly HttpClient client=new(){Timeout=TimeSpan.FromSeconds(10)};
    readonly SemaphoreSlim gate=new(2);
    readonly Dictionary<string,Image?> images=[];
    readonly Dictionary<string,Task<Image?>> pending=[];
    readonly CancellationTokenSource stop=new();
    readonly string directory; bool disposed;
    public ItemIconCache(string directory){this.directory=Path.Combine(directory,"icons-gms83");Directory.CreateDirectory(this.directory);client.DefaultRequestHeaders.UserAgent.ParseAdd("MapleMarketCompanion/0.5");}
    public Image? Peek(string id)=>images.GetValueOrDefault(id);
    public Task<Image?> Get(string id)
    {
        if(disposed||id.Length is <1 or >8||!id.All(char.IsAsciiDigit))return Task.FromResult<Image?>(null);
        if(images.TryGetValue(id,out var image))return Task.FromResult(image);
        if(pending.TryGetValue(id,out var task)&&!task.IsCompleted)return task;
        return pending[id]=Load(id);
    }
    async Task<Image?> Load(string id)
    {
        Image? result=null;bool acquired=false;
        try
        {
            await gate.WaitAsync(stop.Token);acquired=true;
            var path=Path.Combine(directory,id+".png");byte[] bytes;
            if(File.Exists(path))bytes=await File.ReadAllBytesAsync(path,stop.Token);
            else
            {
                if(Offline)return null;
                using var response=await client.GetAsync($"https://maplestory.io/api/gms/83/item/{id}/icon",HttpCompletionOption.ResponseHeadersRead,stop.Token);
                response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>262144)throw new IOException("Icon too large");
                using var input=await response.Content.ReadAsStreamAsync(stop.Token);using var output=new MemoryStream();var buffer=new byte[8192];int count;
                while((count=await input.ReadAsync(buffer,stop.Token))>0){if(output.Length+count>262144)throw new IOException("Icon too large");output.Write(buffer,0,count);}bytes=output.ToArray();
            }
            using var stream=new MemoryStream(bytes);using var source=Image.FromStream(stream);
            if(source.Width>256||source.Height>256)throw new IOException("Invalid icon dimensions");
            result=new Bitmap(source);if(!File.Exists(path))await File.WriteAllBytesAsync(path,bytes,stop.Token);
        }
        catch(Exception ex) when(ex is HttpRequestException or IOException or OperationCanceledException or ArgumentException or System.Runtime.InteropServices.ExternalException){result?.Dispose();result=null;}
        finally{if(acquired)gate.Release();}
        if(disposed){result?.Dispose();return null;}images[id]=result;return result;
    }
    public void Dispose(){disposed=true;stop.Cancel();client.Dispose();foreach(var icon in images.Values)icon?.Dispose();images.Clear();}
}

public sealed partial class MainForm
{
    ItemIconCache icons=null!;
    readonly IconCatalog iconCatalog=new();
    string IconId(object? row)=>row switch
    {
        IconCatalog.Entry e=>e.Id,
        Observation o=>iconCatalog.Resolve(o.ItemId,o.Name),FmSummary f=>iconCatalog.Resolve(f.ItemId,f.Name),Deal d=>iconCatalog.Resolve(d.Listing.ItemId,d.Listing.Name),
        SavedItem s=>iconCatalog.Resolve(observations.FirstOrDefault(o=>TextKey.Search(o.Name)==TextKey.Search(s.Name)&&o.ItemId.Length>0)?.ItemId??fmSummaries.FirstOrDefault(f=>TextKey.Search(f.Name)==TextKey.Search(s.Name))?.ItemId??"",s.Name),
        _=>""
    };
    void ConfigureIcons()
    {
        icons=new ItemIconCache(dataDirectory){Offline=Offline};FormClosed+=(_,_)=>icons.Dispose();
        foreach(var grid in new[]{priceGrid,dealsGrid,watchGrid,inventoryGrid,fmGrid,dashboardWatch,dashboardDeals,dashboardMarket})
        {
            grid.RowTemplate.Height=44;
            grid.CellPainting+=(_,e)=>
            {
                if(e.RowIndex<0||e.ColumnIndex!=0)return;
                e.PaintBackground(e.ClipBounds,true);var g=e.Graphics!;
                var slot=new Rectangle(e.CellBounds.Left+5,e.CellBounds.Top+4,35,35);
                using(var b=new SolidBrush(Color.FromArgb(245,243,223)))g.FillRectangle(b,slot);
                using(var p=new Pen(Color.FromArgb(142,151,153)))g.DrawRectangle(p,slot);
                var id=IconId(grid.Rows[e.RowIndex].Tag);var icon=icons.Peek(id);
                if(icon is not null){var saved=g.Save();g.InterpolationMode=InterpolationMode.NearestNeighbor;float scale=Math.Min(1f,Math.Min(32f/icon.Width,32f/icon.Height));int w=(int)(icon.Width*scale),h=(int)(icon.Height*scale);g.DrawImage(icon,new Rectangle(slot.Left+(35-w)/2,slot.Top+(35-h)/2,w,h));g.Restore(saved);}
                else if(id.Length>0)_=LoadIcon(id,grid);
                var color=(e.State&DataGridViewElementStates.Selected)!=0?e.CellStyle!.SelectionForeColor:e.CellStyle!.ForeColor;
                TextRenderer.DrawText(g,e.FormattedValue?.ToString(),grid.Font,new Rectangle(e.CellBounds.Left+46,e.CellBounds.Top,Math.Max(1,e.CellBounds.Width-49),e.CellBounds.Height),color,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
                e.Handled=true;
            };
            grid.ColumnAdded+=(_,e)=>{if(e.Column.Index==0){e.Column.MinimumWidth=170;e.Column.FillWeight=180;}};
        }
    }
    async Task LoadIcon(string id,DataGridView grid){var image=await icons.Get(id);if(image is not null&&!grid.IsDisposed&&!IsDisposed)grid.InvalidateColumn(0);}
}

