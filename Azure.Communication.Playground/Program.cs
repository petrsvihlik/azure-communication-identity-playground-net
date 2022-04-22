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
            Console.WriteLine("Azure Communication Services - Identity Playground");
            Console.WriteLine("--------------------------------------------------\n");

            _config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddUserSecrets<Program>()
                .Build();

            var op = CliHelper.GetEnumFromCLI<Operation>();
            var env = CliHelper.GetEnumFromCLI<Environment>();
            var api = CliHelper.GetEnumFromCLI<ApiType>();
            var version = CliHelper.GetEnumFromCLI(ServiceVersion.V2021_10_31_preview);
            string versionString = version.ToString().ToLower().Replace("_", "-")["v".Length..];
            string userId = null;
            var (host, secret) = GetEnvSettings(env);

            CommunicationIdentityClient communicationClient = null;
            HttpClient httpClient = null;

            switch (api)
            {
                case ApiType.REST:
                    httpClient = HttpHelper.GetHttpClient(new Uri($"https://{host}"));
                    break;

                case ApiType.SDK:
                    communicationClient = new($"endpoint=https://{host}/;accesskey={secret}", new CommunicationIdentityClientOptions(version));
                    break;
            }

            switch (op)
            {
                case Operation.ExchangeToken:
                    var accountType = CliHelper.GetEnumFromCLI<AccountType>();
                    var aadToken = await GetAADAccessToken(accountType);
                    if (!string.IsNullOrEmpty(aadToken))
                    {
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
                                string responseContent = await HttpHelper.SendMessage(httpClient, message, secret);
                                Console.WriteLine(responseContent);
                                break;

                            case ApiType.SDK:
                                var acsToken = await communicationClient.GetTokenForTeamsUserAsync(aadToken);
                                Console.WriteLine($"ACS token: {acsToken.Value.Token}");
                                break;
                        }
                    }
                    break;

                case Operation.CreateUserAndToken:
                    switch (api)
                    {
                        case ApiType.REST:
                            var message = new HttpRequestMessage(HttpMethod.Post, $"/identities?api-version={versionString}")
                            {
                                Content = new StringContent(@"{""createTokenWithScopes"": [""chat""]}", Encoding.UTF8, "application/json")
                            };
                            string responseContent = await HttpHelper.SendMessage(httpClient, message, secret);
                            Console.WriteLine(responseContent);
                            break;

                        case ApiType.SDK:
                            var userTokenResponse = communicationClient.CreateUserAndToken(new List<CommunicationTokenScope> { "chat" });
                            Console.WriteLine($"User: {userTokenResponse.Value.User}\nToken: {userTokenResponse.Value.AccessToken.Token}");
                            break;
                    }
                    break;

                case Operation.IssueToken:
                    Console.Write("Enter user id: ");
                    userId = Console.ReadLine();
                    switch (api)
                    {
                        case ApiType.REST:
                            var message = new HttpRequestMessage(HttpMethod.Post, $"/identities/{userId}/:issueAccessToken?api-version={versionString}")
                            {
                                Content = new StringContent(@"{""createTokenWithScopes"": [""chat""]}", Encoding.UTF8, "application/json")
                            };
                            string responseContent = await HttpHelper.SendMessage(httpClient, message, secret);
                            Console.WriteLine(responseContent);
                            break;

                        case ApiType.SDK:
                            var userTokenResponse = communicationClient.GetToken(new CommunicationUserIdentifier(userId),new List<CommunicationTokenScope> { "chat" });
                            Console.WriteLine($"Token: {userTokenResponse.Value.Token}");
                            break;
                    }
                    break;

                case Operation.RevokeToken:
                    Console.Write("Enter user id: ");
                    userId = Console.ReadLine();
                    switch(api)
                    {
                        case ApiType.REST:
                            var message = new HttpRequestMessage(HttpMethod.Post, $"/identities/{userId}/:revokeAccessTokens?api-version={versionString}");
                            string responseContent = await HttpHelper.SendMessage(httpClient, message, secret);
                            Console.WriteLine(responseContent);
                            break;

                        case ApiType.SDK:
                            var user = new CommunicationUserIdentifier(userId);
                            var response = communicationClient.RevokeTokens(user);
                            Console.WriteLine(response.ReasonPhrase);
                            break;
                    }
                    break;                
            }

            Console.ReadLine();
        }

        private static (string, string) GetEnvSettings(Environment env)
        {
            string host = null;
            string secret = _config.GetSection($"{env}:Secret").Value;
            switch (env)
            {
                case Environment.PPE:
                    host = $"{_config.GetSection("PPE:ResourceName").Value}.ppe.communication.azure.net";
                    break;

                case Environment.PROD:
                    host = $"{_config.GetSection("PROD:ResourceName").Value}.communication.azure.com";
                    break;
            }

            return (host, secret);
        }

        private static async Task<string> GetAADAccessToken(AccountType accountType)
        {
            var clientId = _config.GetSection($"AAD:{accountType}:ClientID").Value;
            var tenantId = _config.GetSection($"AAD:{accountType}:TenantID").Value;
            var redirectUri = "http://localhost";

            IPublicClientApplication client = null;

            // See all mutli-tenant authority configurations at
            // https://docs.microsoft.com/en-us/azure/active-directory/develop/msal-client-application-configuration#authority
            switch (accountType)
            {
                case AccountType.SingleTenant:
                    client = PublicClientApplicationBuilder
                        .Create(clientId)
                        .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                        .WithRedirectUri(redirectUri)
                        .Build();
                    break;

                case AccountType.MultiTenant:
                    client = PublicClientApplicationBuilder
                        .Create(clientId)
                        .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                        .WithRedirectUri(redirectUri)
                        .Build();
                    break;
            }

            Console.WriteLine("Acquiring AAD Access Token...");

            // Interactive flow
            var authResult = await client.AcquireTokenInteractive(new[] { "https://auth.msft.communication.azure.com/Teams.ManageCalls" }).ExecuteAsync();

            // Non-interactive flow
            //var tokenResult = client.AcquireTokenByUsernamePassword("M365Scope", "username", new System.Security.SecureString()).ExecuteAsync();

            Console.WriteLine($"AAD Access token: {authResult.AccessToken}");

            Console.WriteLine("Acquiring ACS Token...");
            return authResult.AccessToken;
        }
    }
}
