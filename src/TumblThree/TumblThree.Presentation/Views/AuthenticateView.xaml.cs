﻿using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Waf.Applications;
using System.Windows;
using System.Windows.Controls;
using TumblThree.Applications.Services;
using TumblThree.Applications.ViewModels;
using TumblThree.Applications.Views;

namespace TumblThree.Presentation.Views
{
    /// <summary>
    ///     Interaction logic for SettingsView.xaml.
    /// </summary>
    [Export(typeof(IAuthenticateView))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public partial class AuthenticateView : IAuthenticateView
    {
        private readonly Lazy<AuthenticateViewModel> viewModel;
        private readonly string _appSettingsPath;
        private string _url;
        private string _domain;

        [ImportingConstructor]
        public AuthenticateView(IEnvironmentService environmentService)
        {
            InitializeComponent();
            _appSettingsPath = Path.GetFullPath(Path.Combine(environmentService.AppSettingsPath, ".."));
            viewModel = new Lazy<AuthenticateViewModel>(() => ViewHelper.GetViewModel<AuthenticateViewModel>(this));
            browser.Loaded += Browser_Navigated;
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, _appSettingsPath);
            await browser.EnsureCoreWebView2Async(env);
        }

        private AuthenticateViewModel ViewModel
        {
            get { return viewModel.Value; }
        }

        public void ShowDialog(object owner, string url, string cookieDomain)
        {
            browser.Initialized += OnLoad;
            Owner = owner as Window;
            _url = url;
            _domain = cookieDomain;
            ShowDialog();
        }

        private void OnLoad(object sender, EventArgs e)
        {
            if (browser.IsInitialized)
                browser.CoreWebView2.Navigate(_url);
        }

        public string GetUrl()
        {
            return browser.Source.ToString();
        }

        public async Task<CookieCollection> GetCookies(string url)
        {
            //var cookieManager = Cef.GetGlobalCookieManager();
            //var cookies = await cookieManager.VisitUrlCookiesAsync(url, true);

            var cookieManager = browser.CoreWebView2.CookieManager;
            var cookies = await cookieManager.GetCookiesAsync(url);

            CookieCollection cookieCollection;
            if (url.Contains("tumblr.com"))
            {
                // don't ask why, but one cookieCollection works and the other not
                var cookieHeader = GetCookieHeader(cookies);
                CookieContainer cookieCon = new CookieContainer();
                cookieCon.SetCookies(new Uri("https://" + _domain + "/"), cookieHeader);
                cookieCollection = FixCookieDates(cookieCon.GetCookies(new Uri("https://" + _domain + "/")));
            }
            else
            {
                cookieCollection = AuthenticateView.GetCookies(cookies);
            }
      
            return cookieCollection;
        }

        private static CookieCollection GetCookies(List<CoreWebView2Cookie> cookies)
        {
            CookieCollection cookieCollection = new CookieCollection();
            foreach (var cookie in cookies)
            {
                var transferCookie = new System.Net.Cookie(cookie.Name, WebUtility.UrlEncode(cookie.Value), cookie.Path, cookie.Domain);
                transferCookie.Expires = cookie.Expires;
                transferCookie.HttpOnly = cookie.IsHttpOnly;
                transferCookie.Secure = cookie.IsSecure;
                cookieCollection.Add(transferCookie);
            }
            return cookieCollection;
        }

        private static string GetCookieHeader(List<CoreWebView2Cookie> cookies)
        {
            StringBuilder cookieString = new StringBuilder();
            string delimiter = string.Empty;

            foreach (var cookie in cookies)
            {
                cookieString.Append(delimiter)
                    .Append(cookie.Name)
                    .Append('=')
                    .Append(WebUtility.UrlEncode(cookie.Value));
                delimiter = ",";
            }

            return cookieString.ToString();
        }

        private static CookieCollection FixCookieDates(CookieCollection cookieCol)
        {
            foreach (System.Net.Cookie cookie in cookieCol)
            {
                if (cookie.Expires.Equals(DateTime.MinValue) && cookie.Expires.Kind == DateTimeKind.Unspecified)
                    cookie.Expires = new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            return cookieCol;
        }

        private void Browser_Navigated(object sender, RoutedEventArgs e)
        {
            if (browser.IsInitialized)
                browser.Source = new Uri(_url);
            //try
            //{
            //    var cwb = (ChromiumWebBrowser)sender;
            //    if (cwb.Address.Equals(ViewModel.OAuthCallbackUrl))
            //    {
            //        Close();
            //    }
            //}
            //catch
            //{
            //}
        }

        public static void SetSilent(WebBrowser browser, bool silent)
        {
            if (browser == null)
            {
                throw new ArgumentNullException("browser");
            }

            // get an IWebBrowser2 from the document
            var sp = browser.Document as IOleServiceProvider;
            if (sp != null)
            {
                var IID_IWebBrowserApp = new Guid("0002DF05-0000-0000-C000-000000000046");
                var IID_IWebBrowser2 = new Guid("D30C1661-CDAF-11d0-8A3E-00C04FC9E26E");

                object webBrowser;
                sp.QueryService(ref IID_IWebBrowserApp, ref IID_IWebBrowser2, out webBrowser);
                if (webBrowser != null)
                {
                    webBrowser.GetType()
                              .InvokeMember("Silent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.PutDispProperty,
                                  null, webBrowser, new object[] { silent });
                }
            }
        }

        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IOleServiceProvider
        {
            [PreserveSig]
            int QueryService([In] ref Guid guidService, [In] ref Guid riid,
                [MarshalAs(UnmanagedType.IDispatch)] out object ppvObject);
        }
    }
}
