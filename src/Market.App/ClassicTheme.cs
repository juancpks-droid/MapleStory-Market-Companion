using System.Drawing.Drawing2D;
namespace Market.App;
public sealed class ClassicButton : Button
{
 public string? Badge;
 bool hover;
 public ClassicButton(){DoubleBuffered=true;MouseEnter+=(_,_)=>{hover=true;Invalidate();};MouseLeave+=(_,_)=>{hover=false;Invalidate();};}
 protected override void OnPaint(PaintEventArgs e){
  if(Width<3||Height<3)return;
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  bool selected=BackColor==Color.FromArgb(185,99,35);
  var r=new Rectangle(1,1,Width-3,Height-3);
  using var shape=new GraphicsPath();const int radius=14;
  shape.AddArc(r.Left,r.Top,radius,radius,180,90);shape.AddArc(r.Right-radius,r.Top,radius,radius,270,90);
  shape.AddArc(r.Right-radius,r.Bottom-radius,radius,radius,0,90);shape.AddArc(r.Left,r.Bottom-radius,radius,radius,90,90);shape.CloseFigure();
  g.Clear(Parent?.BackColor??GuidePalette.Back);
  using var fill=new SolidBrush(selected?ControlPaint.Light(GuidePalette.Raised,.12f):hover?GuidePalette.Raised:GuidePalette.Surface);
  g.FillPath(fill,shape);using var border=new Pen(selected?GuidePalette.Gold:GuidePalette.Line);g.DrawPath(border,shape);
  if(Badge is not null)NavigationBadges.Paint(g,Badge,new Rectangle(7,(Height-24)/2,24,24));
  TextRenderer.DrawText(g,Text,Font,new Rectangle(Badge is null?10:40,2,Width-(Badge is null?20:44),Height-4),!Enabled?GuidePalette.Muted:selected?GuidePalette.Gold:GuidePalette.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));
  if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-11,Height-11),GuidePalette.Gold,GuidePalette.Surface);
 }
}
public sealed class ClassicTitle : Label
{
 public ClassicTitle(){DoubleBuffered=true;Font=new Font("Segoe UI",16,FontStyle.Bold);ForeColor=GuidePalette.Text;}
 protected override void OnPaintBackground(PaintEventArgs e){
  e.Graphics.Clear(GuidePalette.Back);
  using var line=new Pen(GuidePalette.Line);e.Graphics.DrawLine(line,0,Height-1,Width,Height-1);
 }
}
