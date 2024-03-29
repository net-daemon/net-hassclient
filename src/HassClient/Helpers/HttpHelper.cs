﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Security;

namespace JoySoftware.HomeAssistant.Helpers
{
    internal static class HttpHelper
    {
        [SuppressMessage("", "CA2000")]
        public static HttpClient CreateHttpClient() => new(CreateHttpMessageHandler());
        public static HttpMessageHandler CreateHttpMessageHandler()
        {
            var bypassCertificateErrorsForHash = Environment.GetEnvironmentVariable("HASSCLIENT_BYPASS_CERT_ERR");
            if (string.IsNullOrEmpty(bypassCertificateErrorsForHash))
            {
                return new HttpClientHandler();
            }

            return CreateHttpMessageHandler(bypassCertificateErrorsForHash);
        }

        private static HttpMessageHandler CreateHttpMessageHandler(string certificate)
        {
            return new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, cert, _, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None)
                    {
                        return true; //Is valid
                    }

                    return cert?.GetCertHashString() == certificate.ToUpperInvariant();
                }
            };
        }
    }
}