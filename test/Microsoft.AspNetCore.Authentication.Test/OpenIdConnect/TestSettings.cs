// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace Microsoft.AspNetCore.Authentication.Test.OpenIdConnect
{
    /// <summary>
    /// This helper class is used to check that query string parameters are as expected.
    /// </summary>
    internal class TestSettings
    {
        private readonly Action<OpenIdConnectOptions> _configureOptions;

        public TestSettings() : this(configure: null)
        {
        }

        public TestSettings(Action<OpenIdConnectOptions> configure)
        {
            _configureOptions = o =>
            {
                configure?.Invoke(o);
                _options = o;
            };
        }

        public UrlEncoder Encoder => UrlEncoder.Default;

        public string ExpectedState { get; set; }

        public TestServer CreateTestServer(AuthenticationProperties properties = null) => TestServerBuilder.CreateServer(_configureOptions, handler: null, properties: properties);

        public IDictionary<string, string> ValidateChallengeFormPost(string responseBody, params string[] parametersToValidate)
        {
            IDictionary<string, string> formInputs = null;
            var errors = new List<string>();
            var xdoc = XDocument.Parse(responseBody.Replace("doctype", "DOCTYPE"));
            var forms = xdoc.Descendants("form");
            if (forms.Count() != 1)
            {
                errors.Add("Only one form element is expected in response body.");
            }
            else
            {
                formInputs = forms.Single()
                                  .Elements("input")
                                  .ToDictionary(elem => elem.Attribute("name").Value,
                                                elem => elem.Attribute("value").Value);

                ValidateParameters(formInputs, parametersToValidate, errors, htmlEncoded: false);
            }

            if (errors.Any())
            {
                var buf = new StringBuilder();
                buf.AppendLine($"The challenge form post is not valid.");
                // buf.AppendLine();

                foreach (var error in errors)
                {
                    buf.AppendLine(error);
                }

                Debug.WriteLine(buf.ToString());
                Assert.True(false, buf.ToString());
            }

            return formInputs;
        }

        public IDictionary<string, string> ValidateSignoutFormPost(TestTransaction transaction, params string[] parametersToValidate)
        {
            IDictionary<string, string> formInputs = null;
            var errors = new List<string>();
            var xdoc = XDocument.Parse(transaction.ResponseText.Replace("doctype", "DOCTYPE"));
            var forms = xdoc.Descendants("form");
            if (forms.Count() != 1)
            {
                errors.Add("Only one form element is expected in response body.");
            }
            else
            {
                formInputs = forms.Single()
                                  .Elements("input")
                                  .ToDictionary(elem => elem.Attribute("name").Value,
                                                elem => elem.Attribute("value").Value);

                ValidateParameters(formInputs, parametersToValidate, errors, htmlEncoded: false);
            }

            if (errors.Any())
            {
                var buf = new StringBuilder();
                buf.AppendLine($"The signout form post is not valid.");
                // buf.AppendLine();

                foreach (var error in errors)
                {
                    buf.AppendLine(error);
                }

                Debug.WriteLine(buf.ToString());
                Assert.True(false, buf.ToString());
            }

            return formInputs;
        }

        public IDictionary<string, string> ValidateChallengeRedirect(Uri redirectUri, params string[] parametersToValidate) =>
            ValidateRedirectCore(redirectUri, OpenIdConnectRequestType.Authentication, parametersToValidate);

        public IDictionary<string, string> ValidateSignoutRedirect(Uri redirectUri, params string[] parametersToValidate) =>
            ValidateRedirectCore(redirectUri, OpenIdConnectRequestType.Logout, parametersToValidate);

        private IDictionary<string, string> ValidateRedirectCore(Uri redirectUri, OpenIdConnectRequestType requestType, string[] parametersToValidate)
        {
            var errors = new List<string>();

            // Validate the authority
            ValidateExpectedAuthority(redirectUri.AbsoluteUri, errors, requestType);

            // Convert query to dictionary
            var queryDict = string.IsNullOrEmpty(redirectUri.Query) ?
                new Dictionary<string, string>() :
                redirectUri.Query.TrimStart('?').Split('&').Select(part => part.Split('=')).ToDictionary(parts => parts[0], parts => parts[1]);

            // Validate the query string parameters
            ValidateParameters(queryDict, parametersToValidate, errors, htmlEncoded: true);

            if (errors.Any())
            {
                var buf = new StringBuilder();
                buf.AppendLine($"The redirect uri is not valid.");
                buf.AppendLine(redirectUri.AbsoluteUri);

                foreach (var error in errors)
                {
                    buf.AppendLine(error);
                }

                Debug.WriteLine(buf.ToString());
                Assert.True(false, buf.ToString());
            }

            return queryDict;
        }

        private void ValidateParameters(
            IDictionary<string, string> actualValues,
            IEnumerable<string> parametersToValidate,
            ICollection<string> errors,
            bool htmlEncoded)
        {
            foreach (var paramToValidate in parametersToValidate)
            {
                switch (paramToValidate)
                {
                    case OpenIdConnectParameterNames.ClientId:
                        ValidateClientId(actualValues, errors, htmlEncoded);
                        break;
                    case OpenIdConnectParameterNames.ResponseType:
                        ValidateResponseType(actualValues, errors, htmlEncoded);
                        break;
                    case OpenIdConnectParameterNames.ResponseMode:
                        ValidateResponseMode(actualValues, errors, htmlEncoded);
                        break;
                    case OpenIdConnectParameterNames.Scope:
                        ValidateScope(actualValues, errors, htmlEncoded);
                        break;
                    case OpenIdConnectParameterNames.RedirectUri:
                        ValidateRedirectUri(actualValues, errors, htmlEncoded);
                        break;
                    case OpenIdConnectParameterNames.Resource:
                        ValidateResource(actualValues, errors, htmlEncoded);
                        break;
                    case OpenIdConnectParameterNames.State:
                        ValidateState(actualValues, errors, htmlEncoded);
                        break;
                    case OpenIdConnectParameterNames.SkuTelemetry:
                        ValidateSkuTelemetry(actualValues, errors, htmlEncoded);
                        break;
                    case OpenIdConnectParameterNames.VersionTelemetry:
                        ValidateVersionTelemetry(actualValues, errors, htmlEncoded);
                        break;
                    case OpenIdConnectParameterNames.PostLogoutRedirectUri:
                        ValidatePostLogoutRedirectUri(actualValues, errors, htmlEncoded);
                        break;
                    case OpenIdConnectParameterNames.MaxAge:
                        ValidateMaxAge(actualValues, errors, htmlEncoded);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown parameter \"{paramToValidate}\".");
                }
            }
        }

        OpenIdConnectOptions _options = null;

        private void ValidateExpectedAuthority(string absoluteUri, ICollection<string> errors, OpenIdConnectRequestType requestType)
        {
            string expectedAuthority;
            switch (requestType)
            {
                case OpenIdConnectRequestType.Token:
                    expectedAuthority = _options.Configuration?.TokenEndpoint ?? _options.Authority + @"/oauth2/token";
                    break;
                case OpenIdConnectRequestType.Logout:
                    expectedAuthority = _options.Configuration?.EndSessionEndpoint ?? _options.Authority + @"/oauth2/logout";
                    break;
                default:
                    expectedAuthority = _options.Configuration?.AuthorizationEndpoint ?? _options.Authority + @"/oauth2/authorize";
                    break;
            }

            if (!absoluteUri.StartsWith(expectedAuthority))
            {
                errors.Add($"ExpectedAuthority: {expectedAuthority}");
            }
        }

        private void ValidateClientId(IDictionary<string, string> actualParams, ICollection<string> errors, bool htmlEncoded) =>
            ValidateParameter(OpenIdConnectParameterNames.ClientId, _options.ClientId, actualParams, errors, htmlEncoded);

        private void ValidateResponseType(IDictionary<string, string> actualParams, ICollection<string> errors, bool htmlEncoded) =>
            ValidateParameter(OpenIdConnectParameterNames.ResponseType, _options.ResponseType, actualParams, errors, htmlEncoded);

        private void ValidateResponseMode(IDictionary<string, string> actualParams, ICollection<string> errors, bool htmlEncoded) =>
            ValidateParameter(OpenIdConnectParameterNames.ResponseMode, _options.ResponseMode, actualParams, errors, htmlEncoded);

        private void ValidateScope(IDictionary<string, string> actualParams, ICollection<string> errors, bool htmlEncoded) =>
            ValidateParameter(OpenIdConnectParameterNames.Scope, string.Join(" ", _options.Scope), actualParams, errors, htmlEncoded);

        private void ValidateRedirectUri(IDictionary<string, string> actualParams, ICollection<string> errors, bool htmlEncoded) =>
            ValidateParameter(OpenIdConnectParameterNames.RedirectUri, TestServerBuilder.TestHost + _options.CallbackPath, actualParams, errors, htmlEncoded);

        private void ValidateResource(IDictionary<string, string> actualParams, ICollection<string> errors, bool htmlEncoded) =>
            ValidateParameter(OpenIdConnectParameterNames.RedirectUri, _options.Resource, actualParams, errors, htmlEncoded);

        private void ValidateState(IDictionary<string, string> actualParams, ICollection<string> errors, bool htmlEncoded) =>
            ValidateParameter(OpenIdConnectParameterNames.State, ExpectedState, actualParams, errors, htmlEncoded);

        private void ValidateSkuTelemetry(IDictionary<string, string> actualParams, ICollection<string> errors, bool htmlEncoded) =>
#if NETCOREAPP2_0 || NETCOREAPP2_1
            ValidateParameter(OpenIdConnectParameterNames.SkuTelemetry, "ID_NETSTANDARD1_4", actualParams, errors, htmlEncoded);
#elif NET461
            ValidateParameter(OpenIdConnectParameterNames.SkuTelemetry, "ID_NET451", actualParams, errors, htmlEncoded);
#else
#error Invalid target framework.
#endif

        private void ValidateVersionTelemetry(IDictionary<string, string> actualParams, ICollection<string> errors, bool htmlEncoded) =>
            ValidateParameter(OpenIdConnectParameterNames.VersionTelemetry, typeof(OpenIdConnectMessage).GetTypeInfo().Assembly.GetName().Version.ToString(), actualParams, errors, htmlEncoded);

        private void ValidatePostLogoutRedirectUri(IDictionary<string, string> actualParams, ICollection<string> errors, bool htmlEncoded) =>
            ValidateParameter(OpenIdConnectParameterNames.PostLogoutRedirectUri, "https://example.com/signout-callback-oidc", actualParams, errors, htmlEncoded);

        private void ValidateMaxAge(IDictionary<string, string> actualQuery, ICollection<string> errors, bool htmlEncoded)
        {
            if(_options.MaxAge.HasValue)
            {
                Assert.Equal(TimeSpan.FromMinutes(20), _options.MaxAge.Value);
                string expectedMaxAge = "1200";
                ValidateParameter(OpenIdConnectParameterNames.MaxAge, expectedMaxAge, actualQuery, errors, htmlEncoded);
            }
            else if(actualQuery.ContainsKey(OpenIdConnectParameterNames.MaxAge))
            {
                errors.Add($"Parameter {OpenIdConnectParameterNames.MaxAge} is present but it should be absent");
            }
        }

        private void ValidateParameter(
            string parameterName,
            string expectedValue,
            IDictionary<string, string> actualParams,
            ICollection<string> errors,
            bool htmlEncoded)
        {
            string actualValue;
            if (actualParams.TryGetValue(parameterName, out actualValue))
            {
                if (htmlEncoded)
                {
                    expectedValue = Encoder.Encode(expectedValue);
                }

                if (actualValue != expectedValue)
                {
                    errors.Add($"Parameter {parameterName}'s expected value is '{expectedValue}' but its actual value is '{actualValue}'");
                }
            }
            else
            {
                errors.Add($"Parameter {parameterName} is missing");
            }
        }
    }
}