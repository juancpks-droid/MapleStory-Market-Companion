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
  g.FillPath(fill,shape);using var border=new Pen(selected?GuidePalette.Gold:hover?GuidePalette.Muted:GuidePalette.Line);g.DrawPath(border,shape);
  if(selected){using var marker=new SolidBrush(GuidePalette.Gold);g.FillRectangle(marker,3,10,3,Height-20);}
  if(Badge is not null)NavigationBadges.Paint(g,Badge,new Rectangle(7,(Height-24)/2,24,24));
  TextRenderer.DrawText(g,Text,Font,new Rectangle(Badge is null?10:40,2,Width-(Badge is null?20:44),Height-4),!Enabled?GuidePalette.Muted:selected?GuidePalette.Gold:GuidePalette.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));
  if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-11,Height-11),GuidePalette.Gold,GuidePalette.Surface);
 }
}
public sealed class ClassicTitle : Label
{
 public Func<string>? PriceStatus;
 public ClassicTitle(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Opaque,true);Font=new Font("Segoe UI",21,FontStyle.Bold);ForeColor=GuidePalette.Text;}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.Clear(GuidePalette.Back);
  using var line=new Pen(GuidePalette.Line);e.Graphics.DrawLine(line,0,Height-1,Width,Height-1);
  TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(24,9,Math.Max(1,Width-220),35),GuidePalette.Text,TextFormatFlags.EndEllipsis);
  string description=Text switch{
   "Dashboard"=>"Your market at a glance • Explore listings, demand and watched items",
   "Price Checker"=>"Find an item • Compare player offers and NPC reference prices",
   "Monster Finder"=>"Explore monster stats, drop references and spawn locations",
   "Item Finder"=>"Discover classic items, monster drops and places to explore",
   "FM Tracker"=>"Explore Free Market sightings and recent source summaries",
   "Watchlist"=>"Your saved items • Follow prices and manage notifications",
   "Alert Inbox"=>"Review your market notifications and price alerts",
   "Settings"=>"Make it yours • Themes, data sources, updates and backups",
   "Seller Finder"=>"Find players offering an item for sale, with prices and location notes",
   _=>"Maple Market • Your classic world companion"};
  if(Text=="Price Checker"&&PriceStatus is not null)description=PriceStatus();
  using var small=new Font("Segoe UI",9);TextRenderer.DrawText(e.Graphics,description,small,new Rectangle(26,47,Math.Max(1,Width-220),22),GuidePalette.Muted,TextFormatFlags.EndEllipsis);
 }
 protected override void OnPaintBackground(PaintEventArgs e){
  // Background and title are drawn together in the buffered OnPaint pass.
 }
}
