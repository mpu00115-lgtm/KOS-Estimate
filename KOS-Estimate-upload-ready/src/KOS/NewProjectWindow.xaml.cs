using System.Windows;
namespace KOS;
public partial class NewProjectWindow : Window
{
    public string ProjectName => NameBox.Text;
    public string Address => AddressBox.Text;
    public string Client => ClientBox.Text;
    public string WorkType => WorkTypeBox.Text;
    public string Note => NoteBox.Text;
    public NewProjectWindow() => InitializeComponent();
    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName)) { MessageBox.Show("프로젝트명을 입력하세요."); return; }
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
