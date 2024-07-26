using System.Windows;

namespace CleckList
{
    public partial class InputDialog : Window
    {
        public InputDialog(string question, string defaultAnswer = "")
        {
            InitializeComponent();
            textBox.Text = defaultAnswer;
        }

        public string Answer { get; private set; }
        public bool IsRemoved { get; private set; }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Answer = textBox.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            Answer = "null";
            IsRemoved = true;
            DialogResult = true;
        }
    }
}