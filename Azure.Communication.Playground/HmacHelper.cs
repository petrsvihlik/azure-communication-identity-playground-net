using System;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Communication.Playground
{
    public static class HmacHelper
    {
        public static async Task AddAuthorization(this HttpRequestMessage request, string secret)
        {
            _ = request ?? throw new ArgumentNullException(nameof(request));
            var date = DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture);
            var pathAndQuery = request.RequestUri.IsAbsoluteUri
                ? request.RequestUri.PathAndQuery
                : $"/{request.RequestUri.OriginalString}";
            var contentHash = await ComputeContentHash(request.Content);


            var phrase = $"{request.Method.Method}\n{pathAndQuery}\n{date};{request.RequestUri.Authority};{contentHash}";
            var hash = ComputesSignature(phrase, secret);
            var hmacHeader = $"HMAC-SHA256 SignedHeaders=x-ms-date;host;x-ms-content-sha256&Signature={hash}";
            request.Headers.Add("x-ms-content-sha256", contentHash);
            request.Headers.Add("x-ms-date", date);
            request.Headers.Add("Authorization", hmacHeader);
        }

        private static string ComputesSignature(string phrase, string secret)
        {
            using var hmacsha256 = new HMACSHA256(Convert.FromBase64String(secret));
            var bytes = Encoding.ASCII.GetBytes(phrase);
            var hashedBytes = hmacsha256.ComputeHash(bytes);
            return Convert.ToBase64String(hashedBytes);
        }

        private static async Task<string> ComputeContentHash(HttpContent content)
        {
            var rawData = string.Empty;
            if (content != null)
            {
                await content.LoadIntoBufferAsync();
                rawData = await content.ReadAsStringAsync();
                (await content.ReadAsStreamAsync()).Position = 0;
            }
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(rawData);
            var hashedBytes = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
