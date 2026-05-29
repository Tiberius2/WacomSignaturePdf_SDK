using System.Drawing;
using System.Windows.Forms;

namespace WacomSignaturePdf.Forms
{
    // Base class for borderless forms that can be dragged by clicking anywhere.
    // Eliminates duplicated drag logic across ErrorDialog and ResetOrUnloadDialog.
    public partial class DraggableForm : Form
    {
        private Point _dragStart;
        private bool _dragging;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = e.Location; }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
                Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }
    }
}
