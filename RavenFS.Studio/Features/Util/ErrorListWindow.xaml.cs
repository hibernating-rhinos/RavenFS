using System.Windows.Input;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Features.Util
{
    public partial class ErrorListWindow : DialogView
    {
        public ErrorListWindow()
        {
            InitializeComponent();

            KeyUp += HandleKeyUp;
        }

        private void HandleKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static void ShowErrors(Notification selectedNotification = null)
        {
            var window = new ErrorListWindow();
            window.DataContext = new StudioErrorListModel() { SelectedItem = selectedNotification};

            window.Show();
        }
    }
}

