using System.Diagnostics.CodeAnalysis;
namespace Market.App;
// Status text is a sibling of connection controls, never their painted parent.
internal sealed class StableStatusBar : Panel
{
 internal readonly StableStatusLabel Message=new(){Dock=DockStyle.Fill,Padding=new Padding(18,0,8,0)};
 public StableStatusBar(){DoubleBuffered=true;Height=66;Controls.Add(Message);}
 [AllowNull] public override string Text{get=>Message?.Text??"";set{if(Message!=null&&Message.Text!=(value??""))Message.Text=value??"";}}
 internal void ShowAnalysis(string text){if(Text.Length==0||Text.StartsWith("Ready")||Text.StartsWith("Local analysis:"))Text=text;}
}
internal sealed class StableStatusLabel : Label
{
 public StableStatusLabel(){AutoSize=false;AutoEllipsis=true;TextAlign=ContentAlignment.MiddleLeft;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Opaque,true);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);base.OnPaint(e);}
}
