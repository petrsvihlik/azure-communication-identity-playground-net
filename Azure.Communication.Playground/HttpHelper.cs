using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Azure.Communication.Playground
{
    internal class HttpHelper
    {
        public static async Task<string> SendMessage(HttpClient httpClient, HttpRequestMessage message, string secret)
        {
            await message.AddAuthorization(secret, httpClient.BaseAddress.Authority);
            var response = await httpClient.SendAsync(message);
            return await response.Content.ReadAsStringAsync();
        }

        public static HttpClient GetHttpClient(Uri baseUri)
        {
            var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (a, b, c, d) => { return true; } // Don't check certificates
            };
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = baseUri
            };
            return httpClient;
        }
    }
}
