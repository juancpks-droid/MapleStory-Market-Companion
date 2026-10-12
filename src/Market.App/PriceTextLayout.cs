namespace Market.App;
public sealed partial class MainForm
{
 static void FitPriceText(Label label,Control? container=null){
  void Fit(){
   if(label.IsDisposed||label.Width<20)return;
   int height=label.GetPreferredSize(new Size(label.Width,0)).Height;
   height=Math.Max(height,label.Font.Height+label.Padding.Vertical+4);
   if(container is not null){if(container.Height!=height)container.Height=height;}
   else if(label.Height!=height)label.Height=height;
  }
  label.TextChanged+=(_,_)=>Fit();label.FontChanged+=(_,_)=>Fit();label.SizeChanged+=(_,_)=>Fit();Fit();
 }
}
