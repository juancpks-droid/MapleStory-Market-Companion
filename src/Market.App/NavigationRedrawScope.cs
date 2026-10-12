using System.Runtime.InteropServices;
namespace Market.App;
// Only freeze the visible navigation container during synchronous reparenting.
// Never use on hidden controls: WM_SETREDRAW(true) would make them visible.
internal sealed class NavigationRedrawScope : IDisposable {
 readonly Control control;readonly bool frozen;
 internal NavigationRedrawScope(Control control){this.control=control;frozen=control.IsHandleCreated&&control.Visible;if(frozen)SendMessage(control.Handle,0x000B,0,0);}
 public void Dispose(){if(!frozen||control.IsDisposed||!control.IsHandleCreated)return;SendMessage(control.Handle,0x000B,1,0);RedrawWindow(control.Handle,0,0,0x0001|0x0080|0x0400);}
 [DllImport("user32.dll")]static extern nint SendMessage(nint window,uint message,nint wParam,nint lParam);
 [DllImport("user32.dll")]static extern bool RedrawWindow(nint window,nint rect,nint region,uint flags);
}
