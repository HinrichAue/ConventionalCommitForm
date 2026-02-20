namespace ConventionalCommitForm
{
    /// <summary>
    /// Interaction logic for ConventionalCommitFormView.xaml
    /// </summary>
    public partial class ConventionalCommitFormView
    {
        public ConventionalCommitFormView()
        {
            InitializeComponent();

            DataContext = new ConventionalCommitFormViewModel();
        }
    }
}
