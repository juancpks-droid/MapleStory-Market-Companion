namespace Market.App;
sealed class MarketGrid:DataGridView
{
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);if(Rows.Count!=0||Width<180||Height<110)return;
  int y=Math.Max(ColumnHeadersHeight+22,Height/2-24);
  using var title=new Font("Segoe UI",11,FontStyle.Bold);
  TextRenderer.DrawText(e.Graphics,"No results to display",title,new Rectangle(12,y,Width-24,26),GuidePalette.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);
  TextRenderer.DrawText(e.Graphics,"Search, refresh, or adjust your filters.",Font,new Rectangle(12,y+29,Width-24,24),GuidePalette.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);
 }
}
sealed class MarketListingCard:FlowLayoutPanel
{
 public MarketListingCard(){DoubleBuffered=true;}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(GuidePalette.Line);e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);}
}
public sealed class MarketTabs:TabControl
{
 public MarketTabs(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Padding=new Point(16,9);}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.Clear(GuidePalette.Back);
  using var surface=new SolidBrush(GuidePalette.Surface);using var line=new Pen(GuidePalette.Line);
  var body=DisplayRectangle;body.Inflate(2,2);e.Graphics.FillRectangle(surface,body);e.Graphics.DrawRectangle(line,body);
  for(int i=0;i<TabCount;i++){var rect=GetTabRect(i);bool active=i==SelectedIndex;using var fill=new SolidBrush(active?GuidePalette.Surface:GuidePalette.Back);e.Graphics.FillRectangle(fill,rect);TextRenderer.DrawText(e.Graphics,TabPages[i].Text,Font,rect,active?GuidePalette.Gold:GuidePalette.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);if(active){using var accent=new Pen(GuidePalette.Gold,3);e.Graphics.DrawLine(accent,rect.Left+12,rect.Bottom-2,rect.Right-12,rect.Bottom-2);}}
 }
 protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
}
sealed class MarketMetric:Control
{
 public string Value="—",Caption="",Detail=""; public bool Detailed;
 public MarketMetric(){DoubleBuffered=true;Dock=DockStyle.Fill;Margin=new Padding(0,4,10,10);}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.Clear(GuidePalette.Surface);using var border=new Pen(GuidePalette.Line);e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);
  using var reference=new Font("Segoe UI",23,FontStyle.Bold);float scale=Math.Min(1f,(Width-24f)/Math.Max(1,TextRenderer.MeasureText(Value,reference).Width));using var number=new Font("Segoe UI",Math.Max(12,23*scale),FontStyle.Bold);using var caption=new Font("Segoe UI",Math.Max(9,Font.Size),FontStyle.Bold);using var detail=new Font("Segoe UI",Math.Max(8.5f,Font.Size-.5f));
  TextRenderer.DrawText(e.Graphics,Caption,caption,new Rectangle(14,12,Width-28,20),GuidePalette.Muted,TextFormatFlags.EndEllipsis);
  TextRenderer.DrawText(e.Graphics,Value,number,new Rectangle(12,33,Width-24,40),GuidePalette.Gold,TextFormatFlags.EndEllipsis);
  TextRenderer.DrawText(e.Graphics,Detail,detail,new Rectangle(14,78,Width-28,Detailed?Math.Max(22,Height-84):22),GuidePalette.Muted,Detailed?TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis:TextFormatFlags.EndEllipsis);
 }
}
