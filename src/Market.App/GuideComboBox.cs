namespace Market.App;
// Paint the closed selector too, keeping the native keyboard and drop-down behavior.
sealed class GuideComboBox : ComboBox
{
 public GuideComboBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=22;}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.Clear(GuidePalette.Raised);
  using var border=new Pen(Focused?GuidePalette.Gold:GuidePalette.Line);
  e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);
  TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(7,0,Math.Max(1,Width-28),Height),GuidePalette.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
  TextRenderer.DrawText(e.Graphics,"⌄",Font,new Rectangle(Width-23,0,20,Height),GuidePalette.Gold,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
 }
 protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
 protected override void OnDrawItem(DrawItemEventArgs e){
  using var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(68,59,35):GuidePalette.Raised);e.Graphics.FillRectangle(fill,e.Bounds);
  if(e.Index>=0)TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,e.Bounds,GuidePalette.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
 }
}
