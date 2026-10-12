namespace Market.App;
public sealed partial class MainForm
{
 static void ResponsiveSettings(Panel scroller,FlowLayoutPanel form){
  form.AutoSize=false;form.Dock=DockStyle.Top;bool sizing=false;
  void Fit(){
   if(sizing||scroller.ClientSize.Width<100)return;sizing=true;
   try{
    int width=Math.Max(300,scroller.ClientSize.Width-26);form.MaximumSize=new Size(width,0);form.Width=width;
    foreach(Control child in form.Controls){
     if(child is Button)continue;child.Width=width-14;
     if(child is FlowLayoutPanel row){
      if(row.Controls.Count==2&&row.Controls[0] is Label label){label.Width=Math.Min(310,(width-20)*42/100);label.Height=label.GetPreferredSize(new Size(label.Width,0)).Height;var field=row.Controls[1];if(field is TextBoxBase)field.Width=Math.Max(100,width-label.Width-42);row.Height=Math.Max(label.Height,field.Height)+14;}
     }else if(child is Label note){note.Height=note.GetPreferredSize(new Size(note.Width,0)).Height+8;}
     else if(child is CheckBox check){check.Height=Math.Max(32,check.GetPreferredSize(new Size(check.Width,0)).Height+8);}
    }
    form.Height=form.Padding.Vertical+form.Controls.Cast<Control>().Where(c=>c.Visible).Sum(c=>c.Height+c.Margin.Vertical)+12;
   }finally{sizing=false;}
  }
  scroller.SizeChanged+=(_,_)=>Fit();form.Layout+=(_,_)=>Fit();Fit();
 }
}
