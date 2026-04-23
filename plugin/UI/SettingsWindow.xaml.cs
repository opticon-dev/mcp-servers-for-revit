using System.Windows;
using System.Windows.Controls;

namespace revit_mcp_plugin.UI
{
    /// <summary>
    /// Settings.xaml의 상호작용 로직
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private CommandSetSettingsPage commandSetPage;
        private bool isInitialized = false;

        public SettingsWindow()
        {
            InitializeComponent();

            // 페이지 초기화
            commandSetPage = new CommandSetSettingsPage();

            // 기본 페이지 로드
            ContentFrame.Navigate(commandSetPage);

            isInitialized = true;
        }

        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInitialized) return;

            if (NavListBox.SelectedItem == CommandSetItem)
            {
                ContentFrame.Navigate(commandSetPage);
            }
        }
    }
}
