using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
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

        public MainWindow()
        {
            InitializeComponent();

            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.SingleBorderWindow;

            this.Left = SystemParameters.WorkArea.Left;
            this.Top = SystemParameters.WorkArea.Top;
            this.Width = SystemParameters.WorkArea.Width;
            this.Height = SystemParameters.WorkArea.Height;

            _storagePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SecretStore", "secrets.dat"
            );

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
                {
                    break;
                }
                else
                {
                    // ❌ Неверный пароль
                    var info = new InfoWindow("Ошибка", "Неверный мастер-пароль! Попробуйте снова.");
                    info.ShowDialog();

                    // 🔄 создаём новое окно ввода
                    mpw = new MasterWindowPassword();
                }
            }

            try
            {
                var list = _repo.LoadAll();
                _secrets = new ObservableCollection<Secret>(list);
            }
            catch (Exception ex)
            {
                // ❌ Ошибка загрузки
                var info = new InfoWindow("Ошибка загрузки", ex.Message);
                info.ShowDialog();

                _secrets = new ObservableCollection<Secret>();
            }

            SecretsList.ItemsSource = _secrets;
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
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var addWin = new AddSecretWindow { Owner = this };
            if (addWin.ShowDialog() == true)
            {
                var secret = addWin.ResultSecret;
                _secrets.Add(secret);
                _repo.SaveAll(_secrets);
            }
        }
        private void ShowButton_Click(object sender, RoutedEventArgs e)
        {
            if (SecretsList.SelectedItem is Secret s)
            {
                var info = new InfoWindow("Секрет", $"Логин: {s.Username}\nПароль: {s.Password}");
                info.ShowDialog();
            }
            else
            {
                var warn = new InfoWindow("Ошибка", "Выберите запись!");
                warn.ShowDialog();
            }
        }
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (SecretsList.SelectedItem is Secret s)
            {
                try
                {
                    Clipboard.Clear();
                    Clipboard.SetText(s.Password);

                    var copy = new InfoWindow("Успех", "Пароль скопирован в буфер обмена");
                    copy.ShowDialog();
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    var info = new InfoWindow("Буфер занят", $"Пароль: {s.Password}");
                    info.ShowDialog();
                }
                catch (Exception ex)
                {
                    var error = new InfoWindow("Ошибка", ex.Message);
                    error.ShowDialog();
                }
            }
            else
            {
                var warn = new InfoWindow("Ошибка", "Выберите запись!");
                warn.ShowDialog();
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }


        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                this.WindowState = WindowState.Normal;

                // Когда возвращаемся из развёрнутого — делаем окно нормального размера
                this.Width = 1200;  // задай удобную ширину
                this.Height = 800;  // и удобную высоту
                this.Left = (SystemParameters.WorkArea.Width - this.Width) / 2;
                this.Top = (SystemParameters.WorkArea.Height - this.Height) / 2;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void ConnectDbButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bao = new OpenBaoClient("http://127.0.0.1:8200", "s.yHwkjeAuEtelkED8C4lbKemy");


                string name = "post";
                var host = (await bao.GetSecretValueAsync(name, "host"))?.Trim();
                var port = (await bao.GetSecretValueAsync(name, "port"))?.Trim();
                var db = (await bao.GetSecretValueAsync(name, "dbname"))?.Trim();
                var user = (await bao.GetSecretValueAsync(name, "username"))?.Trim();
                var pass = (await bao.GetSecretValueAsync(name, "password"))?.Trim();

                var connStr = $"Host={host};Port={port};Database={db};Username={user};Password={pass}";

                MessageBox.Show($"host={host}\nport={port}\ndb={db}\nuser={user}\npass={pass}");

                MessageBox.Show(connStr);


                using (var conn = new Npgsql.NpgsqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    using (var cmd = new Npgsql.NpgsqlCommand("SELECT version()", conn))
                    {
                        var result = await cmd.ExecuteScalarAsync();
                        DbStatusText.Text = $"/ подключение успешно!\n{result}";
                    }
                }
            }
            catch (Exception ex)
            {
                DbStatusText.Text = $"/ oшибка: {ex.Message}";
            }
        }
    }
}
