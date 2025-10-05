using System;
using System.Windows;
using SecretStore.Models;
using Npgsql;
using System.Threading.Tasks;

namespace SecretStore
{
    public partial class AddSecretWindow : Window
    {
        private readonly string _connStr;

        public Secret ResultSecret { get; private set; } // секрет для возврата в MainWindow

        public AddSecretWindow(string connStr)
        {
            InitializeComponent();
            _connStr = connStr;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(NameBox.Text) || string.IsNullOrEmpty(PasswordBox.Password))
            {
                MessageBox.Show("Название и пароль обязательны!", "Ошибка");
                return;
            }

            var secret = new Secret
            {
                Name = NameBox.Text.Trim(),
                Username = LoginBox.Text.Trim(),
                Password = PasswordBox.Password,
                Notes = NoteBox.Text.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                // Добавляем в БД
                await AddSecretToDbAsync(secret);

                // Возвращаем результат в MainWindow
                ResultSecret = secret;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления в базу: {ex.Message}", "Ошибка");
            }
        }

        private async Task AddSecretToDbAsync(Secret secret)
        {
            var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync();

            string query = @"
                INSERT INTO public.secrets (name, username, password, description, created_at)
                VALUES (@name, @username, @password, @description, @created_at)";

            var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("name", secret.Name);
            cmd.Parameters.AddWithValue("username", (object)secret.Username ?? DBNull.Value);
            cmd.Parameters.AddWithValue("password", (object)secret.Password ?? DBNull.Value);
            cmd.Parameters.AddWithValue("description", (object)secret.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("created_at", secret.CreatedAt);

            await cmd.ExecuteNonQueryAsync();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
