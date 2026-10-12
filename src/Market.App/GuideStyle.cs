using System.Drawing.Drawing2D;
namespace Market.App;
internal static class GuidePalette
{
 internal static Color Back=Color.FromArgb(8,8,10),Surface=Color.FromArgb(19,19,23),Raised=Color.FromArgb(29,29,34),Line=Color.FromArgb(49,49,55),Text=Color.FromArgb(240,239,235),Muted=Color.FromArgb(166,166,175),Gold=Color.FromArgb(241,203,100);
}
public sealed partial class MainForm
{
 void ApplyGuideStyle(){
  var tint=features.Theme switch{
   "Ellinia"=>Color.FromArgb(151,211,158),
   "Kerning City"=>Color.FromArgb(166,185,247),
   "Perion"=>Color.FromArgb(240,164,113),
   _=>Color.FromArgb(241,203,100)};
  Color Shade(double strength)=>Color.FromArgb((int)(18+tint.R*strength),(int)(22+tint.G*strength),(int)(30+tint.B*strength));
  GuidePalette.Gold=tint;GuidePalette.Back=Shade(.015);GuidePalette.Surface=Shade(.04);GuidePalette.Raised=Shade(.085);GuidePalette.Line=Shade(.19);
  void Paint(Control c){
   c.ForeColor=GuidePalette.Text;
   if(c is DataGridView grid){
    grid.BackgroundColor=GuidePalette.Back;grid.GridColor=GuidePalette.Line;
    grid.DefaultCellStyle.BackColor=GuidePalette.Surface;grid.DefaultCellStyle.ForeColor=GuidePalette.Text;
    grid.DefaultCellStyle.SelectionBackColor=ControlPaint.Light(GuidePalette.Raised,.12f);grid.DefaultCellStyle.SelectionForeColor=GuidePalette.Text;grid.RowsDefaultCellStyle.SelectionBackColor=grid.DefaultCellStyle.SelectionBackColor;foreach(DataGridViewColumn column in grid.Columns)column.DefaultCellStyle.SelectionBackColor=grid.DefaultCellStyle.SelectionBackColor;
    grid.AlternatingRowsDefaultCellStyle.BackColor=GuidePalette.Back;grid.DefaultCellStyle.Padding=new Padding(10,8,10,8);grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(10,8,10,8);grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",9,FontStyle.Bold);
    grid.ColumnHeadersDefaultCellStyle.BackColor=GuidePalette.Raised;grid.ColumnHeadersDefaultCellStyle.ForeColor=GuidePalette.Gold;
    grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=GuidePalette.Raised;
    grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
    return;
   }
   if(c is ClassicButton){c.Invalidate();return;}
   if(c is ClassicBrand)return;
   if(c is ComboBox combo){combo.FlatStyle=FlatStyle.Flat;if(combo is not GuideComboBox){combo.BackColor=Color.White;combo.ForeColor=Color.Black;return;}}
   if(c is TextBoxBase or ComboBox or NumericUpDown or ListBox){c.BackColor=GuidePalette.Raised;}
   else if(c is HistoryPlot){c.BackColor=GuidePalette.Surface;}
   else if(c is Label){c.BackColor=Color.Transparent;c.ForeColor=c is ClassicTitle?GuidePalette.Text:GuidePalette.Muted;}
   else if(c is TabPage)c.BackColor=GuidePalette.Surface;
   else if(c is Panel or TabControl or GroupBox or CheckBox or RadioButton or Form)c.BackColor=GuidePalette.Back;
   foreach(Control child in c.Controls)Paint(child);
  }
  Paint(this);
  foreach(var page in pages.Values)Paint(page);
  updateBanner.BackColor=GuidePalette.Raised;updateBannerText.ForeColor=GuidePalette.Gold;
  sidebarVersion.ForeColor=GuidePalette.Gold;
  foreach(var card in new[]{marketCard,dealCard,watchCard,inventoryCard}){card.BackColor=GuidePalette.Surface;card.ForeColor=GuidePalette.Gold;}
  Invalidate(true);
 }
}
