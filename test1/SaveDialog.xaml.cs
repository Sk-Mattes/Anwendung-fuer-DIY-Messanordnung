using System.Windows;

namespace test1
{
    public partial class SaveDialog : Window
    {
        public string FileName { get; private set; }

        public SaveDialog()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            FileName = FileNameTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
