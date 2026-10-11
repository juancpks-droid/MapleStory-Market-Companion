using System.Drawing.Drawing2D;
namespace Market.App;
internal static class GuidePalette
{
 internal static readonly Color Back=Color.FromArgb(8,8,10),Surface=Color.FromArgb(19,19,23),Raised=Color.FromArgb(29,29,34),Line=Color.FromArgb(49,49,55),Text=Color.FromArgb(240,239,235),Muted=Color.FromArgb(166,166,175),Gold=Color.FromArgb(241,203,100);
}
public sealed partial class MainForm
{
 void ApplyGuideStyle(){
  void Paint(Control c){
   c.ForeColor=GuidePalette.Text;
   if(c is DataGridView grid){
    grid.BackgroundColor=GuidePalette.Back;grid.GridColor=GuidePalette.Line;
    grid.DefaultCellStyle.BackColor=GuidePalette.Surface;grid.DefaultCellStyle.ForeColor=GuidePalette.Text;
    grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(68,59,35);grid.DefaultCellStyle.SelectionForeColor=GuidePalette.Text;
    grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(23,23,27);
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
   else if(c is Panel or TabPage or TabControl or GroupBox or CheckBox or RadioButton or Form)c.BackColor=GuidePalette.Back;
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
