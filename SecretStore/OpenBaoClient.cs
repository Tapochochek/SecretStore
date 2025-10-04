using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SecretStore
{
    class OpenBaoClient
    {
        private readonly HttpClient _http;

        public OpenBaoClient(string baseAddress, string token)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseAddress) };
            _http.DefaultRequestHeaders.Add("X-Vault-Token", token);
        }

        public async Task<string> GetSecretValueAsync(string secretPath, string key)
        {
            var resp = await _http.GetAsync($"/v1/secret/data/{secretPath}");
            if (!resp.IsSuccessStatusCode) return null;

            var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
            return json["data"]?["data"][key]?.ToString();
        }
    }
}
