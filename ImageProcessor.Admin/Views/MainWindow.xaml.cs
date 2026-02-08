using System.Windows.Input;

namespace ImageProcessor.Admin.Views
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.GetPosition(this).Y <= TitleBarHeight)
            {
                DragMove();
            }
        }
    }
}