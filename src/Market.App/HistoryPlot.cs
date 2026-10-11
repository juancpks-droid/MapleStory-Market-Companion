using Market.Core;
using System.Drawing.Drawing2D;
namespace Market.App;

public sealed class HistoryPlot:Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public List<Observation> Points {get;set;}=[];
    readonly ToolTip tip=new(){AutoPopDelay=15000};
    readonly List<(PointF position,Observation observation)> hits=[];
    Observation? hovered;
    public HistoryPlot(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Color.FromArgb(255,249,229);ForeColor=Color.FromArgb(52,62,65);AccessibleName="Item price history. Hover a point for source, price and date.";MouseMove+=HoverPoint;MouseLeave+=(_,_)=>{tip.Hide(this);hovered=null;};}
    protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
    static Color EvidenceColor(EvidenceKind kind)=>kind switch{EvidenceKind.Asking=>Color.SteelBlue,EvidenceKind.ConfirmedSale=>Color.SeaGreen,EvidenceKind.ReportedSale=>Color.DarkOrange,_=>Color.SlateGray};
    void HoverPoint(object? sender,MouseEventArgs e){
        var near=hits.OrderBy(h=>Math.Abs(h.position.X-e.X)+Math.Abs(h.position.Y-e.Y)).FirstOrDefault();
        if(near.observation is null||Math.Abs(near.position.X-e.X)>10||Math.Abs(near.position.Y-e.Y)>10){tip.Hide(this);hovered=null;return;}
        if(hovered==near.observation)return;hovered=near.observation;var o=hovered;
        tip.Show($"{o.Name}\n{(o.PriceApproximate?"Rounded: ~ ":"")}{o.UnitPrice:N0} mesos / unit • {o.Kind}\n{o.Provider} • {o.ObservedAt?.ToLocalTime():g}\nSeller: {o.Seller}\n{o.Variant}",this,e.X+12,e.Y+12,12000);
    }
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);hits.Clear();var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        var points=Points.Where(p=>p.ObservedAt.HasValue&&p.UnitPrice>0).OrderBy(p=>p.ObservedAt).ToArray();
        using var ink=new SolidBrush(ForeColor);using var muted=new SolidBrush(Color.FromArgb(93,109,118));
        if(points.Length==0){g.DrawString("No dated price history yet",Font,ink,18,18);g.DrawString("Choose an item listing and a date range.\nHistory builds as dated observations are collected; missing prices are never invented.",Font,muted,new RectangleF(18,45,Math.Max(1,Width-36),Math.Max(1,Height-50)));return;}
        if(Width<250||Height<125){g.DrawString($"{points.Length} observations • enlarge the chart to explore",Font,ink,new RectangleF(8,8,Width-16,Height-16));return;}
        decimal min=points.Min(p=>p.UnitPrice),max=points.Max(p=>p.UnitPrice),median=ValuationEngine.Median(points.Select(p=>p.UnitPrice));
        decimal pad=Math.Max(1,(max-min)*.08m),low=Math.Max(0,min-pad),high=max+pad;
        var start=points[0].ObservedAt!.Value;var end=points[^1].ObservedAt!.Value;
        var plot=new Rectangle(90,48,Math.Max(10,Width-112),Math.Max(10,Height-102));
        TextRenderer.DrawText(g,$"PRICE HISTORY  •  {points.Length:N0} observations  •  Sample median {TextKey.Mesos(median)} mesos",Font,new Rectangle(12,9,Width-24,28),ForeColor,TextFormatFlags.EndEllipsis);
        using var grid=new Pen(Color.FromArgb(222,222,208));using var axis=new Pen(Color.FromArgb(148,162,163));
        for(int i=0;i<=4;i++){float y=plot.Top+plot.Height*i/4f;g.DrawLine(grid,plot.Left,y,plot.Right,y);var value=high-(high-low)*i/4;TextRenderer.DrawText(g,TextKey.Mesos(value),Font,new Rectangle(1,(int)y-9,82,20),ForeColor,TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
        g.DrawLine(axis,plot.Left,plot.Top,plot.Left,plot.Bottom);
        float Y(decimal p)=>plot.Bottom-(float)((p-low)/(high-low))*plot.Height;
        using(var medianPen=new Pen(Color.FromArgb(173,127,71)){DashStyle=DashStyle.Dash})g.DrawLine(medianPen,plot.Left,Y(median),plot.Right,Y(median));
        foreach(var p in points){float x=plot.Left+(end==start?.5f:(float)((p.ObservedAt!.Value-start).TotalSeconds/(end-start).TotalSeconds))*plot.Width;float y=Y(p.UnitPrice);using var dot=new SolidBrush(EvidenceColor(p.Kind));if(p.PriceApproximate){using var outline=new Pen(EvidenceColor(p.Kind),2);g.DrawEllipse(outline,x-4,y-4,8,8);}else g.FillEllipse(dot,x-3,y-3,6,6);hits.Add((new(x,y),p));}
        string Stamp(DateTimeOffset t)=>t.ToLocalTime().ToString(end-start<TimeSpan.FromDays(1)?"MMM d HH:mm":"MMM d");
        for(int i=0;i<3;i++){if(end==start&&i!=1)continue;var t=start+TimeSpan.FromTicks((end-start).Ticks*i/2);int x=plot.Left+plot.Width*i/2;var text=Stamp(t);int width=TextRenderer.MeasureText(text,Font).Width;g.DrawString(text,Font,muted,Math.Clamp(x-width/2,plot.Left,Math.Max(plot.Left,plot.Right-width)),plot.Bottom+5);}
        TextRenderer.DrawText(g,"Blue: asks   Orange: reported sales   Green: confirmed   Gray: other   ○ rounded   - - sample median",Font,new Rectangle(12,Height-25,Width-24,22),ForeColor,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
    }
}
