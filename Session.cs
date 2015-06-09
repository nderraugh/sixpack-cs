using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;

namespace SixpackAB {
    public class Session {
        private const string validName = @"^[a-z0-9][a-z0-9\-_ ]*$";
        private readonly string clientId;
        private readonly string baseUrl = "http://localhost:5000";
        private readonly string ipAddress;
        private readonly string userAgent;
        private readonly int timeout = 500;

        /// <summary>
        /// Session constructor.
        /// </summary>
        /// <param name="clientId">The unique ID of the subject of your experiment (your user).</param>
        /// <param name="baseUrl">The base URL to your Sixpack server.</param>
        /// <param name="timeout">Timeout for the internal HTTP client used by the session.</param>
        /// <param name="ipAddress">The IP address of the subject of your experiment.</param>
        /// <param name="userAgent">The Useragent of the subject of your experiment.</param>
        public Session(string clientId = null, string baseUrl = null, int? timeout = null, string ipAddress = null, string userAgent = null) {
            this.clientId = clientId ?? Guid.NewGuid().ToString();
            this.baseUrl = baseUrl ?? this.baseUrl;
            this.ipAddress = ipAddress;
            this.userAgent = userAgent;
            this.timeout = timeout ?? this.timeout;
        }

        /// <summary>
        /// The client method to participate in an experiment.
        /// </summary>
        /// <param name="experimentName">The name of the experiment to participate in.</param>
        /// <param name="alternatives">The alternatives for the experiment.</param>
        /// <param name="force">Force an alternative, for testing purposes.</param>
        public dynamic Participate(string experimentName, string[] alternatives, string force)
        {
            if (!Regex.IsMatch(experimentName, validName))
            {
                throw new Exception("Bad experimentName");
            }
            if (alternatives.Length < 2)
            {
                throw new Exception("Must specify at least 2 alternatives");
            }
            foreach (var alt in alternatives)
            {
                if (!Regex.IsMatch(experimentName, validName))
                {
                    throw new Exception("Bad alternative name: " + alt);
                }
            }
            if (force != null)
            {
                return
                    new
                    {
                        Status = "ok",
                        Alternative = new
                        {
                            Name = force
                        },
                        Experiment = new
                        {
                            Version = 0,
                            Name = experimentName
                        },
                        ClientId = clientId
                    };
            }
            var parameters = GetParameters(experimentName);
            parameters.Add("alternatives", alternatives);
            return Request(baseUrl + "/participate", parameters);
        }

        /// <summary>
        /// The client method to participate in an experiment.
        /// </summary>
        /// <param name="experimentName">The name of the experiment to participate in.</param>
        /// <param name="alternatives">The alternatives for the experiment.</param>
        /// <param name="force">Force an alternative, for testing purposes.</param>
        /// <param name="callback">The callback that will handle the result of the method call or the exception that got thrown.</param>
        public void ParticipateAsync(string experimentName, string[] alternatives, string force,
            Action<Exception, dynamic> callback) {
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
                try
                {
                    callback(null, Request(baseUrl + "/participate", parameters));
                }
                catch(Exception e)
                {
                    callback(e, null);
                }
                
            }
        }

        /// <summary>
        /// The client method to register an conversion in an experiment.
        /// </summary>
        /// <param name="experimentName">The name of the experiment related to the conversion.</param>
        /// <param name="kpi">Any arbitrary KPI you want to associate with the conversion.</param>
        /// <param name="callback">The callback that will handle the result of the method call or the exception that got thrown.</param>
        public dynamic Convert(string experimentName, string kpi)
        {
            if (!Regex.IsMatch(experimentName, validName))
            {
                throw new Exception("Bad experimentName");
            }

            var parameters = GetParameters(experimentName);
            if (kpi != null)
            {
                if (!Regex.IsMatch(kpi, validName))
                {
                    throw new Exception("Bad kpi");
                }
                parameters.Add("kpi", kpi);
            }
            return Request(baseUrl + "/convert", parameters);
        }

        /// <summary>
        /// The client method to register an conversion in an experiment.
        /// </summary>
        /// <param name="experimentName">The name of the experiment related to the conversion.</param>
        /// <param name="kpi">Any arbitrary KPI you want to associate with the conversion.</param>
        /// <param name="callback">The callback that will handle the result of the method call or the exception that got thrown.</param>
        public void ConvertAsync(string experimentName, string kpi, Action<Exception, dynamic> callback) {
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
            try
            {
                callback(null, Request(baseUrl + "/convert", parameters));
            }
            catch (Exception e)
            {
                callback(e, null);
            }
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

        private dynamic Request(string baseUrl, Dictionary<string, object> parameters) {
            dynamic result = null;
            var client = new HttpClient {Timeout = new TimeSpan(0, 0, 0, 0, timeout)};
            var uri = RequestUri(baseUrl, parameters);
            try {
                var response = client.GetAsync(uri).Result;
                response.EnsureSuccessStatusCode();
                var responseJson = response.Content.ReadAsStringAsync().Result;
                result = new JavaScriptSerializer().Deserialize<object>(responseJson);
            }
            catch (HttpException e) {
                if (e.GetHttpCode() == (int) HttpStatusCode.InternalServerError) {
                    return new {Status = "failed", Response = e.GetHtmlErrorMessage()};
                }
            }
            catch (TimeoutException) {
                throw new Exception("request timed out");
            }
            return result;
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