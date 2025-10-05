using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Npgsql;
using SecretStore.Models;
using SecretStore.Storage;

namespace SecretStore
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<Secret> _secrets = new ObservableCollection<Secret>();
        private SecretRepository _repo;
        private string _masterPassword;
        private string _storagePath;

        // Данные для подключения к PostgreSQL
        private string _host;
        private string _port;
        private string _db;
        private string _user;
        private string _pass;

        public MainWindow()
        {
            InitializeComponent();

            // Настройка окна
            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.SingleBorderWindow;
            this.Left = SystemParameters.WorkArea.Left;
            this.Top = SystemParameters.WorkArea.Top;
            this.Width = SystemParameters.WorkArea.Width;
            this.Height = SystemParameters.WorkArea.Height;

            SecretsList.ItemsSource = _secrets;

            // Путь для локального хранения
            _storagePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SecretStore",
                "secrets.dat"
            );

            // Ввод мастер-пароля
            var mpw = new MasterWindowPassword();
            while (true)
            {
                if (mpw.ShowDialog() != true)
                {
                    Application.Current.Shutdown();
                    return;
                }

                _masterPassword = mpw.EnteredPassword;
                _repo = new SecretRepository(_storagePath, _masterPassword);

                if (_repo.IsFirstRun())
                {
                    _repo.SaveAll(new List<Secret>());
                    break;
                }

                if (_repo.ValidateMasterPassword())
                    break;
                else
                {
                    MessageBox.Show("Неверный мастер-пароль! Попробуйте снова.", "Ошибка");
                    mpw = new MasterWindowPassword();
                }
            }

            // Загружаем локальные секреты
            try
            {
                var list = _repo.LoadAll();
                _secrets = new ObservableCollection<Secret>(list);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки секретов: {ex.Message}", "Ошибка");
                _secrets = new ObservableCollection<Secret>();
            }

            SecretsList.ItemsSource = _secrets;

            // Загружаем подключение к БД
            LoadDbAndSecretsAsync();
        }

        private async void LoadDbAndSecretsAsync()
        {
            try
            {
                // Получаем токен из переменной окружения
                var token = Environment.GetEnvironmentVariable("OPENBAO_TOKEN");

                if (string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("Ошибка: переменная окружения OPENBAO_TOKEN не найдена!", "Ошибка");
                    return;
                }

                var bao = new OpenBaoClient("http://127.0.0.1:8200", token);

                string name = "post";

                _host = (await bao.GetSecretValueAsync(name, "host"))?.Trim();
                _port = (await bao.GetSecretValueAsync(name, "port"))?.Trim();
                _db = (await bao.GetSecretValueAsync(name, "dbname"))?.Trim();
                _user = (await bao.GetSecretValueAsync(name, "username"))?.Trim();
                _pass = (await bao.GetSecretValueAsync(name, "password"))?.Trim();
                MessageBox.Show($"Host={_host};Port={_port};Database={_db};Username={_user};Password={_pass}");

                if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_port) ||
                    string.IsNullOrWhiteSpace(_db) || string.IsNullOrWhiteSpace(_user) || string.IsNullOrWhiteSpace(_pass))
                {
                    MessageBox.Show("Ошибка: одно из значений подключения пустое!");
                    return;
                }

                await LoadSecretsFromDbAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к Vault: {ex.Message}");
            }
        }

        private string GetConnectionString() =>
            $"Host={_host};Port={_port};Database={_db};Username={_user};Password={_pass}";

        private async Task LoadSecretsFromDbAsync()
        {
            try
            {
                var conn = new NpgsqlConnection(GetConnectionString());
                await conn.OpenAsync();

                string query = "SELECT name, username, password, description, created_at FROM secrets";
                var cmd = new NpgsqlCommand(query, conn);
                var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    _secrets.Add(new Secret
                    {
                        Name = reader.GetString(0),
                        Username = reader.IsDBNull(1) ? null : reader.GetString(1),
                        Password = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Notes = reader.IsDBNull(3) ? null : reader.GetString(3),
                        CreatedAt = reader.GetDateTime(4)
                    });
                }

                DbStatusText.Text = "/ Подключение к БД успешно!";
            }
            catch (Exception ex)
            {
                DbStatusText.Text = $"/ Ошибка БД: {ex.Message}";
            }
        }

        private  void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var addWin = new AddSecretWindow(GetConnectionString()) { Owner = this };

            if (addWin.ShowDialog() == true)
            {
                var secret = addWin.ResultSecret;

                // Проверяем, нет ли уже такого секрета по Name + CreatedAt
                if (!_secrets.Any(s => s.Name == secret.Name && s.CreatedAt == secret.CreatedAt))
                {
                    _secrets.Add(secret);
                    _repo.SaveAll(_secrets); // Сохраняем локально
                }
            }
        }


        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var query = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                SecretsList.ItemsSource = _secrets;
                return;
            }

            var filtered = _secrets.Where(s =>
                (s.Name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (s.Username ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            );

            SecretsList.ItemsSource = new ObservableCollection<Secret>(filtered);
        }

        private void ShowButton_Click(object sender, RoutedEventArgs e)
        {
            if (SecretsList.SelectedItem is Secret s)
            {
                MessageBox.Show($"Логин: {s.Username}\nПароль: {s.Password}", "Секрет");
            }
            else
            {
                MessageBox.Show("Выберите запись!", "Ошибка");
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (SecretsList.SelectedItem is Secret s)
            {
                try
                {
                    Clipboard.Clear();
                    Clipboard.SetText(s.Password ?? "");
                    MessageBox.Show("Пароль скопирован в буфер обмена", "Успех");
                }
                catch
                {
                    MessageBox.Show($"Пароль: {s.Password}", "Буфер занят");
                }
            }
            else
            {
                MessageBox.Show("Выберите запись!", "Ошибка");
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
                this.WindowState = WindowState.Maximized;
            else
            {
                this.WindowState = WindowState.Normal;
                this.Width = 1200;
                this.Height = 800;
                this.Left = (SystemParameters.WorkArea.Width - this.Width) / 2;
                this.Top = (SystemParameters.WorkArea.Height - this.Height) / 2;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
