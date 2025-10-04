using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SecretStore
{
    /// <summary>
    /// Логика взаимодействия для MasterWindowPassword.xaml
    /// </summary>
    public partial class MasterWindowPassword : Window
    {
        public string EnteredPassword { get; private set; }
        public bool IsFirstRun { get; set; }

        public MasterWindowPassword()
        {
            InitializeComponent();

            if (IsFirstRun)
            {
                Title = "Создание мастер-пароля";
                var textBlock = (TextBlock)FindName("InstructionText");
                if (textBlock != null)
                    textBlock.Text = "Создайте мастер-пароль:";
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MasterPasswordBox.Password))
            {
                MessageBox.Show("Введите мастер-пароль");
                return;
            }

            // Для первого запуска проверяем длину пароля
            if (IsFirstRun && MasterPasswordBox.Password.Length < 6)
            {
                MessageBox.Show("Мастер-пароль должен быть не менее 6 символов");
                return;
            }

            EnteredPassword = MasterPasswordBox.Password;
            this.DialogResult = true;
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

    }
}
