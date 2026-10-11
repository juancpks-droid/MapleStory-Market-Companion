using System.Drawing.Drawing2D;
namespace Market.App;
public sealed class ClassicBrand : Control
{
 public ClassicBrand(){DoubleBuffered=true;Height=180;Dock=DockStyle.Top;AccessibleName="Maple Market, Classic World";}
 protected override void OnPaint(PaintEventArgs e){
  if(Width<2||Height<2)return;
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  var saved=g.Save();g.ScaleTransform(Width/191f,Height/180f);
  using var bg=new LinearGradientBrush(new Rectangle(0,0,191,180),GuidePalette.Raised,GuidePalette.Surface,90);g.FillRectangle(bg,0,0,191,180);
  using var glow=new SolidBrush(Color.FromArgb(22,255,220,140));g.FillEllipse(glow,25,14,145,145);g.FillEllipse(glow,46,33,103,106);
  using var gold=new Pen(GuidePalette.Gold,1.3f);g.DrawRectangle(gold,3,3,185,173);
  using var inner=new Pen(Color.FromArgb(76,121,123));g.DrawRectangle(inner,7,7,177,165);
  foreach(float x in new[]{3f,188f})foreach(float y in new[]{3f,176f}){using var stud=new SolidBrush(Color.FromArgb(255,223,151));g.FillEllipse(stud,x-2,y-2,4,4);}
  void Leaf(float x,float y,float scale,float angle){var s=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);g.ScaleTransform(scale,scale);PointF[] p=[new(0,-15),new(5,-6),new(10,-9),new(9,-2),new(16,0),new(9,6),new(10,11),new(2,9),new(0,17),new(-2,9),new(-10,11),new(-9,6),new(-16,0),new(-9,-2),new(-10,-9),new(-5,-6)];using var b=new SolidBrush(Color.FromArgb(233,152,62));g.FillPolygon(b,p);g.DrawLine(gold,0,-7,0,17);g.Restore(s);}
  Leaf(25,36,.65f,-28);Leaf(165,43,.48f,35);Leaf(154,111,.38f,-25);
  using var shadow=new SolidBrush(Color.FromArgb(90,0,0,0));g.FillEllipse(shadow,57,105,86,13);
  // Original illustrated mushroom mascot with a shaded cap and tiny market coins.
  using var stem=new LinearGradientBrush(new Rectangle(72,64,49,46),Color.FromArgb(255,248,204),Color.FromArgb(225,184,108),90);g.FillEllipse(stem,75,66,44,46);
  using var rim=new Pen(Color.FromArgb(87,44,28),2);g.DrawArc(rim,75,66,44,46,0,180);
  using var cap=new LinearGradientBrush(new Rectangle(52,25,91,61),Color.FromArgb(255,186,80),Color.FromArgb(211,71,31),90);
  using var shape=new GraphicsPath();shape.AddBezier(52,76,54,9,130,9,143,76);shape.AddBezier(143,76,126,92,69,92,52,76);shape.CloseFigure();g.FillPath(cap,shape);g.DrawPath(rim,shape);
  using var spot=new SolidBrush(Color.FromArgb(255,233,169));g.FillEllipse(spot,70,44,23,13);g.FillEllipse(spot,111,55,17,11);g.FillEllipse(spot,97,32,10,6);
  using var face=new SolidBrush(Color.FromArgb(75,52,39));g.FillEllipse(face,85,88,4,7);g.FillEllipse(face,104,88,4,7);g.DrawArc(rim,92,94,9,6,5,165);
  using var cheek=new SolidBrush(Color.FromArgb(217,137,92));g.FillEllipse(cheek,80,97,7,3);g.FillEllipse(cheek,109,97,7,3);
  for(int i=0;i<3;i++){using var coin=new SolidBrush(Color.FromArgb(239+i*5,175+i*12,56));g.FillEllipse(coin,128+i*4,106-i*5,20,8);g.DrawEllipse(gold,128+i*4,106-i*5,20,8);}
  void Title(string text,float size,float y,Color color){using var path=new GraphicsPath();using var f=new FontFamily("Segoe UI");using var format=new StringFormat(){Alignment=StringAlignment.Center};path.AddString(text,f,(int)FontStyle.Bold,size,new RectangleF(8,y,175,35),format);using var outline=new Pen(Color.FromArgb(7,25,31),3){LineJoin=LineJoin.Round};g.DrawPath(outline,path);using var brush=new SolidBrush(color);g.FillPath(brush,path);}
  Title("MAPLE MARKET",15,124,Color.FromArgb(255,225,164));
  using var small=new Font("Tahoma",7,FontStyle.Bold,GraphicsUnit.Pixel);using var ink=new SolidBrush(Color.FromArgb(175,211,201));using var center=new StringFormat(){Alignment=StringAlignment.Center};g.DrawString("C L A S S I C   W O R L D",small,ink,new RectangleF(15,151,161,14),center);
  g.DrawLine(gold,20,157,33,157);g.DrawLine(gold,158,157,171,157);g.Restore(saved);
 }
}
public static class NavigationBadges
{
 static readonly string[] Pages=["Dashboard","Item Finder","Price Checker","Deal Finder","Watchlist","Inventory","FM Tracker","Item Catalog","Compare","Equipment","Price Alerts","Trade Journal","Market Activity","Backups","Characters","Alert Inbox","Market Health","Websites","Settings","Merchant Goals","Restock","Gear Wishlist","Item Notes","Trading Sessions","Price History","Getting Started","Transfers","Shopping List","Selling Planner","Search Everything","Price Changes","Seller Comparison","Undo Center","Profit Breakdown","Duplicate Review"];
 static readonly string[] Symbols=["⌂","⌕","$","%","★","▣","FM","▤","⇄","⚔","!","TJ","↗","BK","CH","✉","♥","WWW","⚙","MG","+","GW","IN","TS","PH","GO","⇆","SL","SP","SE","PC","SC","↶","PB","DR"];
 public static void Paint(Graphics g,string page,Rectangle r){
  int i=Array.IndexOf(Pages,page);if(i<0)return;
  Color[] colors=[Color.FromArgb(42,111,134),Color.FromArgb(155,82,39),Color.FromArgb(72,125,79),Color.FromArgb(125,84,146),Color.FromArgb(169,110,28)];var color=colors[i%colors.Length];
  var state=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;
  using var fill=new LinearGradientBrush(r,ControlPaint.Light(color),color,90);g.FillEllipse(fill,r);
  using var border=new Pen(Color.FromArgb(106,74,40),1);g.DrawEllipse(border,r);using var shine=new Pen(Color.FromArgb(245,222,164),1);g.DrawArc(shine,r.X+2,r.Y+2,r.Width-4,r.Height-4,195,150);
  using var font=new Font("Segoe UI Symbol",Symbols[i].Length>1?7:12,FontStyle.Bold);TextRenderer.DrawText(g,Symbols[i],font,r,Color.FromArgb(255,245,215),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);g.Restore(state);
 }
}

