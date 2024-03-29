﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates; //Only import this if you are using certificate
using System.Threading;
using System.Threading.Tasks;
using TimersTimer = System.Timers.Timer;

namespace daemon_console
{
    /// <summary>
    /// This sample shows how to query the Microsoft Graph from a daemon application
    /// which uses application permissions.
    /// For more information see https://aka.ms/msal-net-client-credentials
    /// </summary>
    class Program
    {
        // Even if this is a console application here, a daemon application is a confidential client application
        private static IConfidentialClientApplication _app;
        private static AuthenticationConfig _config;

        static void Main(string[] args)
        {
            _config = AuthenticationConfig.ReadFromJsonFile("appsettings.json");

            PrepareConfidentialClient();

            var timeout = 5000;
            Console.WriteLine($"The Graph Api will be called every {timeout / 1000} seconds unless any key was pressed.");

            var timer = new TimersTimer(timeout);
            timer.Elapsed += CallGraphApi;

            timer.AutoReset = true;
            timer.Enabled = true;

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        private static void CallGraphApi(Object source, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }

        private static async Task RunAsync()
        {

            // With client credentials flows the scopes is ALWAYS of the shape "resource/.default", as the 
            // application permissions need to be set statically (in the portal or by PowerShell), and then granted by
            // a tenant administrator. 
            string[] scopes = new string[] { $"{_config.ApiUrl}.default" };

            AuthenticationResult result = null;
            try
            {
                // Acquire the token for MS Graph
                result = await _app.AcquireTokenForClient(scopes)
                    .ExecuteAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Token acquired from MSAL");
                Console.ResetColor();
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                // Invalid scope. The scope has to be of the form "https://resourceurl/.default"
                // Mitigation: change the scope to be as expected
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Scope provided is not supported");
                Console.ResetColor();
            }

            // Call MS Graph API
            if (result != null)
            {
                var httpClient = new HttpClient();
                var apiCaller = new ProtectedApiCallHelper(httpClient);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{DateTime.Now.ToString("o")}-Calling MS Graph ");
                Console.ResetColor();
                await apiCaller.CallWebApiAndProcessResultASync($"{_config.ApiUrl}v1.0/users", result.AccessToken, Display);
            }
        }

        /// <summary>
        /// Prepares the MSAL's confidential client.
        /// </summary>
        /// <returns></returns>
        private static void PrepareConfidentialClient()
        {            

            // You can run this sample using ClientSecret or Certificate. The code will differ only when instantiating the IConfidentialClientApplication
            bool isUsingClientSecret = AppUsesClientSecret(_config);

            if (isUsingClientSecret)
            {
                _app = ConfidentialClientApplicationBuilder.Create(_config.ClientId)
                    .WithClientSecret(_config.ClientSecret)
                    .WithAuthority(new Uri(_config.Authority))
                    .WithClientCapabilities(new[] { "cp1" }) // Declare this app to be able to receive CAE events
                    .Build();
            }

            else
            {
                X509Certificate2 certificate = ReadCertificate(_config.CertificateName);
                _app = ConfidentialClientApplicationBuilder.Create(_config.ClientId)
                    .WithCertificate(certificate)
                    .WithAuthority(new Uri(_config.Authority))
                    .WithClientCapabilities(new[] { "cp1" }) // Declare this app to be able to receive CAE events
                    .Build();
            }
        }

        /// <summary>
        /// Display the result of the Web API call
        /// </summary>
        /// <param name="result">Object to display</param>
        private static void Display(JObject result)
        {
            foreach (JProperty child in result.Properties().Where(p => !p.Name.StartsWith("@")))
            {
                Console.WriteLine($"{child.Name} = {child.Value}");
            }
        }

        /// <summary>
        /// Checks if the sample is configured for using ClientSecret or Certificate. This method is just for the sake of this sample.
        /// You won't need this verification in your production application since you will be authenticating in AAD using one mechanism only.
        /// </summary>
        /// <param name="config">Configuration from appsettings.json</param>
        /// <returns></returns>
        private static bool AppUsesClientSecret(AuthenticationConfig config)
        {
            string clientSecretPlaceholderValue = "[Enter here a client secret for your application]";
            string certificatePlaceholderValue = "[Or instead of client secret: Enter here the name of a certificate (from the user cert store) as registered with your application]";

            if (!String.IsNullOrWhiteSpace(config.ClientSecret) && config.ClientSecret != clientSecretPlaceholderValue)
            {
                return true;
            }

            else if (!String.IsNullOrWhiteSpace(config.CertificateName) && config.CertificateName != certificatePlaceholderValue)
            {
                return false;
            }

            else
                throw new Exception("You must choose between using client secret or certificate. Please update appsettings.json file.");
        }

        private static X509Certificate2 ReadCertificate(string certificateName)
        {
            if (string.IsNullOrWhiteSpace(certificateName))
            {
                throw new ArgumentException("certificateName should not be empty. Please set the CertificateName setting in the appsettings.json", "certificateName");
            }
            CertificateDescription certificateDescription = CertificateDescription.FromStoreWithDistinguishedName(certificateName);
            DefaultCertificateLoader defaultCertificateLoader = new DefaultCertificateLoader();
            defaultCertificateLoader.LoadIfNeeded(certificateDescription);
            return certificateDescription.Certificate;
        }
    }
}
