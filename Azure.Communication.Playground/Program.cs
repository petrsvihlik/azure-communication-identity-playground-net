using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Communication.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using static Azure.Communication.Identity.CommunicationIdentityClientOptions;

namespace Azure.Communication.Playground
{
    internal partial class Program
    {
        static IConfigurationRoot _config;
        static async Task Main(string[] args)
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddUserSecrets<Program>()
                .Build();

            var op = GetEnumFromCLI<Operation>();
            var env = GetEnumFromCLI<Environment>();
            var api = GetEnumFromCLI<ApiType>();
            var version = GetEnumFromCLI(ServiceVersion.V2021_10_31_preview);
            string versionString = version.ToString().ToLower().Replace("_", "-")["v".Length..];

            Console.WriteLine("Custom Teams Endpoint Playground");

            string host = GetHost(env);
            string secret = _config.GetSection($"{env}:Secret").Value;
            CommunicationIdentityClient communicationClient = null;
            HttpClient httpClient = null;

            switch (api)
            {
                case ApiType.REST:
                    httpClient = GetHttpClient(new Uri($"https://{host}"));
                    break;

                case ApiType.SDK:
                    communicationClient = new($"endpoint=https://{host}/;accesskey={secret}", new CommunicationIdentityClientOptions(version));
                    break;

            }

            switch (op)
            {
                case Operation.ExchangeToken:
                    var aadToken = await GetAADAccessToken();

                    switch (api)
                    {
                        case ApiType.REST:
                            var request = new { Token = aadToken };
                            var json = JsonConvert.SerializeObject(request);
                            var data = new StringContent(json, Encoding.UTF8, "application/json");
                            var message = new HttpRequestMessage(HttpMethod.Post, $"/teamsUser/:exchangeAccessToken?api-version={versionString}")
                            {
                                Content = data
                            };
                            string responseContent = await SendMessage(httpClient, message, secret);
                            Console.WriteLine(responseContent);
                            break;

                        case ApiType.SDK:
                            var acsToken = await communicationClient.GetTokenForTeamsUserAsync(aadToken);
                            Console.WriteLine($"ACS token: {acsToken.Value.Token}");
                            break;
                    }
                    break;

                case Operation.IssueToken:
                    switch (api)
                    {
                        case ApiType.REST:
                            var message = new HttpRequestMessage(HttpMethod.Post, $"/identities?api-version={versionString}")
                            {
                                Content = new StringContent(@"{""createTokenWithScopes"": [""chat""]}", Encoding.UTF8, "application/json")
                            };
                            string responseContent = await SendMessage(httpClient, message, secret);
                            Console.WriteLine(responseContent);
                            break;

                        case ApiType.SDK:
                            var userTokenResponse = communicationClient.CreateUserAndToken(new List<CommunicationTokenScope> { "chat" });
                            Console.WriteLine($"User: {userTokenResponse.Value.User}\nToken: {userTokenResponse.Value.AccessToken.Token}");
                            break;
                    }
                    break;
            }

            Console.ReadLine();
        }

        private static async Task<string> SendMessage(HttpClient httpClient, HttpRequestMessage message, string secret)
        {
            await message.AddAuthorization(secret);
            var response = await httpClient.SendAsync(message);
            return await response.Content.ReadAsStringAsync();
        }

        private static string GetHost(Environment env)
        {
            string host = null;
            switch (env)
            {
                case Environment.PPE:
                    host = $"{_config.GetSection("PPE:ResourceName").Value}.ppe.communication.azure.net";
                    break;

                case Environment.PROD:
                    host = $"{_config.GetSection("PROD:ResourceName").Value}.communication.azure.com";
                    break;
            }

            return host;
        }

        private static HttpClient GetHttpClient(Uri baseUri)
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

        private static async Task<string> GetAADAccessToken()
        {
            var clientId = _config.GetSection("AAD:ClientID").Value;
            var tenant = _config.GetSection("AAD:TenantID").Value;
            var redirectUri = "http://localhost";

            var client = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, tenant)
                //.WithAuthority("https://login.microsoftonline.com/common") // For multi-tenant scenarios
                //.WithAuthority($"M365AadAuthority/tenantId") // For multi-tenant scenarios
                .WithRedirectUri(redirectUri)
                .Build();

            Console.WriteLine("Acquiring AAD Access Token...");

            // Interactive flow
            var authResult = await client.AcquireTokenInteractive(new[] { /*"https://auth.msft.communication.azure.com/.default"*/ "https://auth.msft.communication.azure.com/VoIP" }).ExecuteAsync();
            
            // Non-interactive flow
            //var tokenResult = client.AcquireTokenByUsernamePassword("M365Scope", "username", new System.Security.SecureString()).ExecuteAsync();

            Console.WriteLine($"AAD Access token: {authResult.AccessToken}");

            Console.WriteLine("Acquiring ACS Token...");
            return authResult.AccessToken;
        }

        private static T GetEnumFromCLI<T>(T defVal = default) where T : struct, Enum
        {
            T value = defVal;
            Console.WriteLine($"Specify the {value.GetType().Name}: ");
            foreach (var item in Enum.GetValues(typeof(T)))
            {
                string defString = ((int)item) == Convert.ToInt32(defVal) ? " (default)" : "";
                Console.WriteLine($"\t- {item}: {(int)item}{defString}");
            }
            var succ = Enum.TryParse(Console.ReadLine(), out value);
            return succ ? value : defVal;
        }
    }
}
