using SecretStore.Models;
using SecretStore.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SecretStore.Storage
{
    class SecretRepository
    {
        private readonly string _filePath;
        private readonly string _masterPassword;

        public SecretRepository(string filePath, string masterPassword)
        {
            _filePath = filePath;
            _masterPassword = masterPassword;
        }

        public List<Secret> LoadAll()
        {
            if (!File.Exists(_filePath))
                return new List<Secret>();

            var encrypted = File.ReadAllText(_filePath);

            try
            {
                var json = CryptoUtils.DecryptString(encrypted, _masterPassword);
                return JsonSerializer.Deserialize<List<Secret>>(json) ?? new List<Secret>();
            }
            catch
            {
                throw new Exception("Не удалось расшифровать файл. Возможно, неверный мастер-пароль.");
            }
        }

        public void SaveAll(IEnumerable<Secret> secrets)
        {
            var json = JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true });
            var encrypted = CryptoUtils.EncryptString(json, _masterPassword);

            var dir = Path.GetDirectoryName(_filePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_filePath, encrypted);
        }
        public bool IsFirstRun()
        {
            return !File.Exists(_filePath);
        }

        public bool ValidateMasterPassword()
        {
            if (!File.Exists(_filePath))
                return true; // Первый запуск - любой пароль подходит

            try
            {
                var encrypted = File.ReadAllText(_filePath);
                CryptoUtils.DecryptString(encrypted, _masterPassword);
                return true; // Успешно расшифровали - пароль верный
            }
            catch
            {
                return false; // Не смогли расшифровать - неверный пароль
            }
        }
    }
}
