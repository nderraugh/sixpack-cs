using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;

namespace SixPack {
    public class SixPack {
        private const string validName = @"^[a-z0-9][a-z0-9\-_ ]*$";
        private readonly Guid clientId;
        private readonly string baseUrl;
        private readonly string ipAddress;
        private readonly string userAgent;
        private readonly int timeout = 10000;

        public SixPack(Guid? clientId = null, string baseUrl = null, int? timeout = null, string ipAddress = null,
            string userAgent = null) {
            this.clientId = clientId ?? Guid.NewGuid();
            this.baseUrl = baseUrl ?? this.baseUrl;
            this.ipAddress = ipAddress ?? this.ipAddress;
            this.userAgent = userAgent ?? this.userAgent;
            this.timeout = timeout ?? this.timeout;
        }

        public void Participate(string experimentName, string[] alternatives, string force,
            Action<Exception, object> callback) {
                if (!Regex.IsMatch(experimentName, validName))
                {
                throw new Exception("Bad experimentName");
            }
            if (alternatives.Length < 2) {
                throw new Exception("Must specify at least 2 alternatives");
            }
            foreach (var alt in alternatives) {
                if (!Regex.IsMatch(experimentName, validName))
                {
                    throw new Exception("Bad alternative name: " + alt);
                }
            }
            if (force != null) {
                callback(null,
                    new {
                        Status = "ok",
                        Alternative = new {
                            Name = force
                        },
                        Experiment = new {
                            Version = 0,
                            Name = experimentName
                        },
                        ClientId = clientId
                    });
            }
            else {
                var parameters = GetParameters(experimentName);
                parameters.Add("alternatives", alternatives);
                Request(baseUrl + "/participate", parameters, 10000, callback);
            }
        }

        public void Convert(string experimentName, string kpi, Action<Exception, object> callback) {
            if (!Regex.IsMatch(experimentName, validName))
            {
                callback(new Exception("Bad experimentName"), null);
            }

            var parameters = GetParameters(experimentName);
            if (kpi != null) {
                if (!Regex.IsMatch(kpi, validName)) {
                    callback(new Exception("Bad kpi"), null);
                }
                parameters.Add("kpi", kpi);
            }
            Request(baseUrl + "/convert", parameters, timeout, callback);
        }

        private Uri RequestUri(string endpoint, Dictionary<string, object> parameters) {
            var queryString = string.Join("&", parameters.Keys.Select(x => {
                if ((parameters[x] as string[]) != null) {
                    var result = string.Join("&",
                        (parameters[x] as string[]).Select(
                            y => HttpUtility.UrlEncode(x) + "=" + HttpUtility.UrlEncode(y)));
                    return result;
                }
                return HttpUtility.UrlEncode(x) + "=" + HttpUtility.UrlEncode((string) parameters[x]);
            }));

            if (queryString.Length > 0) {
                endpoint += '?' + queryString;
            }
            return new Uri(endpoint);
        }

        private async void Request(string baseUrl, Dictionary<string, object> parameters, int timeout,
            Action<Exception, object> callback) {
            var client = new HttpClient {Timeout = new TimeSpan(0, 0, 0, 0, timeout)};
            var uri = RequestUri(baseUrl, parameters);
            try {
                var response = await client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                dynamic result = new JavaScriptSerializer().Deserialize<object>(responseJson);
                callback(null, result);
            }
            catch (HttpException e) {
                if (e.GetHttpCode() == (int) HttpStatusCode.InternalServerError) {
                    callback(null, new {Status = "failed", Response = e.GetHtmlErrorMessage()});
                }
            }
            catch (TimeoutException) {
                callback(new Exception("request timed out"), null);
            }
            catch (Exception e) {
                callback(e, null);
            }
        }

        private Dictionary<string, object> GetParameters(string experimentName) {
            var parameters = new Dictionary<string, object> {
                {"client_id", clientId.ToString()},
                {"experiment", experimentName}
            };

            if (ipAddress != null) {
                parameters.Add("ip_address", ipAddress);
            }
            if (userAgent != null) {
                parameters.Add("user_agent", userAgent);
            }

            return parameters;
        }
    }
}