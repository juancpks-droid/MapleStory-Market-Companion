namespace Market.App;
public sealed partial class MainForm {
 // Keep the title, navigation and destination page in the same native paint pass.
 // Website viewers are separate forms and do not inherit this style.
 protected override CreateParams CreateParams {
  get{var cp=base.CreateParams;cp.ExStyle|=0x02000000;return cp;}
 }
}
