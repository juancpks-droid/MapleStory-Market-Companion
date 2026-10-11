using System.Drawing.Drawing2D;

namespace Market.App;

// Original painted UI decoration; no game files or external artwork required.
public sealed class ClassicButton : Button
{
    public string? Badge;
    bool hover;
    public ClassicButton(){DoubleBuffered=true;MouseEnter+=(_,_)=>{hover=true;Invalidate();};MouseLeave+=(_,_)=>{hover=false;Invalidate();};}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;var r=new Rectangle(0,0,Width-1,Height-1);
        bool selected=BackColor==Color.FromArgb(185,99,35);
        using var fill=new LinearGradientBrush(r,selected?Color.FromArgb(255,209,99):hover?Color.FromArgb(224,248,255):ControlPaint.Light(ClassicPalette.Town),selected?Color.FromArgb(231,157,51):ClassicPalette.Town,90);
        g.FillRectangle(fill,r);using var border=new Pen(selected?Color.FromArgb(132,77,29):Color.FromArgb(68,111,142));g.DrawRectangle(border,r);
        using var shine=new Pen(Color.FromArgb(235,255,255));g.DrawLine(shine,2,2,Width-3,2);
        using var inner=new Pen(Color.FromArgb(225,243,248));g.DrawRectangle(inner,2,2,Width-5,Height-5);
        if(Badge is not null)NavigationBadges.Paint(g,Badge,new Rectangle(7,(Height-26)/2,26,26));
        TextRenderer.DrawText(g,Text,Font,new Rectangle(Badge is null?10:40,2,Width-(Badge is null?20:44),Height-4),Color.FromArgb(40,62,76),TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-9,Height-9));
    }
}

public sealed class ClassicTitle : Label
{
    public ClassicTitle(){DoubleBuffered=true;Font=new Font("Tahoma",12,FontStyle.Bold);ForeColor=Color.White;}
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if(Width<2||Height<2)return;
        using var fill=new LinearGradientBrush(ClientRectangle,ControlPaint.Light(ClassicPalette.Town),ControlPaint.Dark(ClassicPalette.Town),90);
        e.Graphics.FillRectangle(fill,ClientRectangle);
        using var border=new Pen(Color.FromArgb(40,72,104));e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);
        e.Graphics.DrawLine(Pens.LightBlue,2,2,Width-3,2);
    }
}



