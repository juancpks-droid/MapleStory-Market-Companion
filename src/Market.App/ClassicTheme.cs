using System.Drawing.Drawing2D;

namespace Market.App;

// Original painted UI decoration; no game files or external artwork required.
public sealed class ClassicButton : Button
{
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
        TextRenderer.DrawText(g,Text,Font,new Rectangle(10,2,Width-20,Height-4),Color.FromArgb(40,62,76),TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));
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

public sealed class ClassicBrand : Control
{
    public ClassicBrand(){DoubleBuffered=true;Height=145;Dock=DockStyle.Top;}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using var sky=new LinearGradientBrush(ClientRectangle,Color.FromArgb(165,221,241),Color.FromArgb(242,245,203),90);g.FillRectangle(sky,ClientRectangle);
        using var hill=new SolidBrush(Color.FromArgb(125,164,94));g.FillEllipse(hill,-60,90,240,100);g.FillEllipse(hill,60,100,190,95);
        using var stem=new SolidBrush(Color.FromArgb(255,237,189));g.FillEllipse(stem,74,85,32,36);
        using var cap=new SolidBrush(Color.FromArgb(222,111,45));g.FillPie(cap,55,62,70,50,180,180);g.FillEllipse(cap,55,82,70,14);
        g.FillEllipse(Brushes.Wheat,68,73,14,9);g.FillEllipse(Brushes.Wheat,96,78,12,8);g.FillEllipse(Brushes.SaddleBrown,83,99,3,5);g.FillEllipse(Brushes.SaddleBrown,96,99,3,5);
        using var font=new Font("Georgia",15,FontStyle.Bold);using var small=new Font("Segoe UI",9,FontStyle.Bold);
        TextRenderer.DrawText(g,"MAPLE MARKET",font,new Rectangle(0,9,Width,29),Color.FromArgb(95,54,28),TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g,"CLASSIC WORLD",small,new Rectangle(0,38,Width,20),Color.FromArgb(57,92,71),TextFormatFlags.HorizontalCenter);
        using var frame=new Pen(Color.FromArgb(142,103,54),2);g.DrawRectangle(frame,1,1,Width-2,Height-2);
    }
}
