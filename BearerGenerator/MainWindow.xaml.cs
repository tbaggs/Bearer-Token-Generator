﻿/*
 The MIT License (MIT)

Copyright (c) 2018 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Configuration;
// The following using statements were added for this sample.
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BearerGenerator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Redirect URI is the URI where Azure AD will return OAuth responses.
        // The Authority is the sign-in URL of the tenant.
        //
        private static readonly string AadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private static readonly string Tenant = ConfigurationManager.AppSettings["ida:Tenant"];
        private static readonly string ClientId = ConfigurationManager.AppSettings["ida:ClientId"];

        private static readonly string Authority = string.Format(CultureInfo.InvariantCulture, AadInstance, Tenant);

        //
        // To authenticate to the To Do list service, the client needs to know the service's App ID URI.
        // To contact the To Do list service we need it's URL as well.
        //
        private static readonly string ServiceScope = ConfigurationManager.AppSettings["service:Scope"];
        private static readonly string ServiceBaseAddress = ConfigurationManager.AppSettings["service:BaseAddress"];
        private static readonly string ServicePath = ConfigurationManager.AppSettings["service:Path"];
        private static readonly string[] Scopes = { ServiceScope };

        private readonly HttpClient _httpClient = new HttpClient();
        private readonly PublicClientApplication _app;

        // Button strings
        private const string SignInString = "Sign In";
        private const string ClearCacheString = "Clear Cache";

        public MainWindow()
        {
            InitializeComponent();
            _app = new PublicClientApplication(ClientId, Authority, TokenCacheHelper.GetUserCache());
            GetBearerToken();
        }

        private void GetBearerToken()
        {
            GetBearerToken(SignInButton.Content.ToString() != ClearCacheString);
        }

        private async Task GetBearerToken(bool isAppStarting)
        {
            var accounts = (await _app.GetAccountsAsync()).ToList();
            if (!accounts.Any())
            {
                SignInButton.Content = SignInString;
                return;
            }
            //
            // Get an access token to call the To Do service.
            //
            AuthenticationResult result = null;
            try
            {
                result = await _app.AcquireTokenSilentAsync(Scopes, accounts.FirstOrDefault());
                SignInButton.Content = ClearCacheString;
                SetUserName(result.Account);
            }
            // There is no access token in the cache, so prompt the user to sign-in.
            catch (MsalUiRequiredException)
            {
                if (!isAppStarting)
                {
                    MessageBox.Show("Please sign in to view your Bearer Token");
                    SignInButton.Content = SignInString;
                }
            }
            catch (MsalException ex)
            {
                // An unexpected error occurred.
                string message = ex.Message;
                if (ex.InnerException != null)
                {
                    message += "Error Code: " + ex.ErrorCode + "Inner Exception : " + ex.InnerException.Message;
                }
                MessageBox.Show(message);

                UserName.Content = Properties.Resources.UserNotSignedIn;
                return;
            }

            // Once the token has been returned by ADAL, add it to the http authorization header, before making the call to access the To Do list service.
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            // Call the To Do list service.
            HttpResponseMessage response = await _httpClient.GetAsync(ServiceBaseAddress + ServicePath);

            if (response.IsSuccessStatusCode)
            {
                BearerToken.Text = result.AccessToken;

                MessageBox.Show("API called successfully", "Success", MessageBoxButton.OK);

                // Read the response and data-bind to the GridView to display To Do items.
                //string s = await response.Content.ReadAsStringAsync();
                //List<TodoItem> toDoArray = JsonConvert.DeserializeObject<List<TodoItem>>(s);

                //TodoList.ItemsSource = toDoArray.Select(t => new { t.Title });
            }
            else
            {
                string failureDescription = await response.Content.ReadAsStringAsync();
                MessageBox.Show($"{response.ReasonPhrase}\n {failureDescription}", "An error occurred while calling API", MessageBoxButton.OK);
            }
        }

        private async void Refresh(object sender = null, RoutedEventArgs args = null)
        {
            await GetBearerToken(SignInButton.Content.ToString() != ClearCacheString);
        }
        private async void SignIn(object sender = null, RoutedEventArgs args = null)
        {
            var accounts = (await _app.GetAccountsAsync()).ToList();

            // If there is already a token in the cache, clear the cache and update the label on the button.
            if (SignInButton.Content.ToString() == ClearCacheString)
            {
                // clear the cache
                while (accounts.Any())
                {
                    await _app.RemoveAsync(accounts.First());
                    accounts = (await _app.GetAccountsAsync()).ToList();
                }
                // Also clear cookies from the browser control.
                SignInButton.Content = SignInString;
                UserName.Content = Properties.Resources.UserNotSignedIn;
                return;
            }

            //
            // Get an access token to call the To Do list service.
            //
            AuthenticationResult result = null;
            try
            {
                // Force a sign-in (PromptBehavior.Always), as the ADAL web browser might contain cookies for the current user, and using .Auto
                // would re-sign-in the same user
                result = await _app.AcquireTokenAsync(Scopes, accounts.FirstOrDefault(), UIBehavior.SelectAccount, string.Empty);
                SignInButton.Content = ClearCacheString;
                SetUserName(result.Account);
                GetBearerToken();
            }
            catch (MsalException ex)
            {
                if (ex.ErrorCode == "access_denied")
                {
                    // The user canceled sign in, take no action.
                }
                else
                {
                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Error Code: " + ex.ErrorCode + "Inner Exception : " + ex.InnerException.Message;
                    }

                    MessageBox.Show(message);
                }

                UserName.Content = Properties.Resources.UserNotSignedIn;
            }
        }

        // Set user name to text box
        private void SetUserName(IAccount userInfo)
        {
            string userName = null;

            if (userInfo != null)
            {
                userName = userInfo.Username;
            }

            if (userName == null)
                userName = Properties.Resources.UserNotIdentified;

            UserName.Content = userName;
        }
    }
}
