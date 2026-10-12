namespace Market.App;
// Composite the page and its native child controls together, including nested tabs.
// Scope this to the content/sidebar rather than all windows or embedded browsers.
internal sealed class StablePageHost : Panel
{
 public StablePageHost(){DoubleBuffered=true;}
 protected override CreateParams CreateParams {
  get {var cp=base.CreateParams;cp.ExStyle|=0x02000000;return cp;} // WS_EX_COMPOSITED
 }
}
