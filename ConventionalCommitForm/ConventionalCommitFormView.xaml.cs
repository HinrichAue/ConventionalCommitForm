using System.Windows;

namespace ConventionalCommitForm
{
    /// <summary>
    /// Interaction logic for ConventionalCommitFormView.xaml
    /// </summary>
    public partial class ConventionalCommitFormView : Window
    {
        public ConventionalCommitFormView()
        {
            InitializeComponent();

            DataContext = new ConventionalCommitFormViewModel();
        }
    }
}
