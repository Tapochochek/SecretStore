using System;
using System.Windows;
using SecretStore.Models;

namespace SecretStore
{
    public partial class AddSecretWindow : Window
    {
        public Secret ResultSecret { get; private set; }

        public AddSecretWindow()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(NameBox.Text) || string.IsNullOrEmpty(PasswordBox.Password))
            {
                var warn = new InfoWindow("Ошибка", "Название и пароль обязательны!");
                warn.ShowDialog();
                return;
            }

            ResultSecret = new Secret
            {
                Name = NameBox.Text.Trim(),
                Username = LoginBox.Text.Trim(),
                Password = PasswordBox.Password,
                Notes = NoteBox.Text.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            DialogResult = true;
            Close();
        }


        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}