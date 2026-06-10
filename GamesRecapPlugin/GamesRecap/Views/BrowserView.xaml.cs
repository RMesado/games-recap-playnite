using System.Windows.Controls;
using System.Windows.Input;

namespace GamesRecap.Views
{
    public partial class BrowserView : UserControl
    {
        public BrowserView()
        {
            InitializeComponent();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ViewModels.BrowserViewModel vm)
                vm.SearchCommand.Execute(null);
        }
    }
}
