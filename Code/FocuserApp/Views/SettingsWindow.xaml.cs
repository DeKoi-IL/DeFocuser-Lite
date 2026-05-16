/*
 * SettingsWindow.xaml.cs
 * Modal settings dialog. Currently houses the Updates section; new sections
 * will be appended to the XAML as features grow.
 */

using ASCOM.DeKoi.DeFocuserApp.ViewModels;

using System.Windows;
using System.Windows.Input;

namespace ASCOM.DeKoi.DeFocuserApp.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
