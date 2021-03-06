﻿using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Boa.Constrictor.RestSharp
{
    /// <summary>
    /// Logs a request and its response to a JSON file.
    /// Shamelessly inspired by:
    /// https://stackoverflow.com/questions/15683858/restsharp-print-raw-request-and-response-headers
    /// </summary>
    public class RequestLogger
    {
        #region Serialization Classes

        /// <summary>
        /// Serialization class for duration.
        /// </summary>
        public class DurationData
        {
            /// <summary>
            /// The start time.
            /// </summary>
            public DateTime? StartTime { get; set; }

            /// <summary>
            /// The end time.
            /// </summary>
            public DateTime? EndTime { get; set; }

            /// <summary>
            /// The duration time, which is end time minus start time.
            /// </summary>
            public TimeSpan? Duration =>
                (StartTime == null || EndTime == null)
                ? null : EndTime - StartTime;
        }
        
        /// <summary>
        /// Serialization class for parameter.
        /// </summary>
        public class ParameterData
        {
            /// <summary>
            /// Name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Value.
            /// </summary>
            public object Value { get; set; }

            /// <summary>
            /// Type.
            /// </summary>
            public string Type { get; set; }
        }

        /// <summary>
        /// Serialization class for request.
        /// </summary>
        public class RequestData
        {
            /// <summary>
            /// Request method.
            /// </summary>
            public string Method { get; set; }

            /// <summary>
            /// Request URI.
            /// </summary>
            public Uri Uri { get; set; }

            /// <summary>
            /// Request resource path.
            /// </summary>
            public string Resource { get; set; }

            /// <summary>
            /// Request parameters.
            /// </summary>
            public IList<ParameterData> Parameters { get; set; }
        }

        /// <summary>
        /// Serialization class for response.
        /// </summary>
        public class ResponseData
        {
            /// <summary>
            /// Response URI.
            /// </summary>
            public Uri Uri { get; set; }

            /// <summary>
            /// Response HTTP status code.
            /// </summary>
            public HttpStatusCode StatusCode { get; set; }

            /// <summary>
            /// Response error message.
            /// </summary>
            public string ErrorMessage { get; set; }

            /// <summary>
            /// Response content.
            /// </summary>
            public string Content { get; set; }

            /// <summary>
            /// Response headers.
            /// </summary>
            public IList<ParameterData> Headers { get; set; }
        }

        /// <summary>
        /// Serialization class for all data.
        /// </summary>
        public class FullData
        {
            /// <summary>
            /// Duration time data.
            /// </summary>
            public DurationData Duration { get; set; }

            /// <summary>
            /// Request data.
            /// </summary>
            public RequestData Request { get; set; }

            /// <summary>
            /// Response data.
            /// </summary>
            public ResponseData Response { get; set; }

            /// <summary>
            /// Request cookies.
            /// </summary>
            public IList<Cookie> Cookies { get; set; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="path">Output filepath. Should be JSON.</param>
        /// <param name="client">RestSharp client.</param>
        /// <param name="request">Request object.</param>
        /// <param name="response">Response object.</param>
        /// <param name="start">Request's start time.</param>
        /// <param name="end">Request's end time.</param>
        public RequestLogger(
            string path,
            IRestClient client,
            IRestRequest request = null,
            IRestResponse response = null,
            DateTime? start = null,
            DateTime? end = null)
        {
            Path = path;
            Client = client;
            Request = request;
            Response = response;
            StartTime = start;
            EndTime = end;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Output filepath. Should be JSON.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// RestSharp client.
        /// </summary>
        public IRestClient Client { get; set; }

        /// <summary>
        /// Request object.
        /// </summary>
        public IRestRequest Request { get; set; }

        /// <summary>
        /// Response object.
        /// </summary>
        public IRestResponse Response { get; set; }

        /// <summary>
        /// Request's start time.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Request's end time.
        /// </summary>
        public DateTime? EndTime { get; set; }

        #endregion

        #region Private Methods

        /// <summary>
        /// Converts duration data to a serializable object.
        /// </summary>
        /// <returns></returns>
        private DurationData GetDurationToLog() => new DurationData
        {
            StartTime = StartTime,
            EndTime = EndTime,
        };

        /// <summary>
        /// Converts a list of parameters to a serializable object.
        /// </summary>
        /// <param name="parameters">List of parameters.</param>
        /// <returns></returns>
        private IList<ParameterData> GetParameterData(IList<Parameter> parameters) =>
            parameters.Select(p => new ParameterData
            {
                Name = p.Name,
                Value = p.Value,
                Type = p.Type.ToString()
            }).ToList();

        /// <summary>
        /// Converts the request data to a serializable object.
        /// </summary>
        /// <returns></returns>
        private RequestData GetRequestToLog() => (Request == null)
            ? null
            : new RequestData
            {
                Method = Request.Method.ToString(),
                Uri = Client.BuildUri(Request),
                Resource = Request.Resource,
                Parameters = GetParameterData(Request.Parameters),
            };

        /// <summary>
        /// Converts the response data to a serializable object.
        /// </summary>
        /// <returns></returns>
        private ResponseData GetResponseToLog() => (Response == null)
            ? null
            : new ResponseData
            {
                Uri = Response.ResponseUri,
                StatusCode = Response.StatusCode,
                ErrorMessage = Response.ErrorMessage,
                Content = Response.Content,
                Headers = GetParameterData(Response.Headers),
            };

        private IList<Cookie> GetCookiesToLog()
        {
            // Linq cannot be used because CookieCollection does not use generic typing.
            // Thus, we suffer with the foreach loop below.

            IList<Cookie> cookies = new List<Cookie>();

            foreach (var c in Client.CookieContainer.GetCookies(Client.BaseUrl))
                cookies.Add((Cookie)c);

            return cookies;
        }

        /// <summary>
        /// Converts all data to a single serializable object.
        /// </summary>
        /// <returns></returns>
        private FullData GetFullDataToLog() => new FullData
        {
            Duration = GetDurationToLog(),
            Request = GetRequestToLog(),
            Response = GetResponseToLog(),
            Cookies = GetCookiesToLog(),
        };

        #endregion

        #region Methods

        /// <summary>
        /// Logs the current data to a JSON file.
        /// </summary>
        public void Log()
        {
            var data = GetFullDataToLog();
            
            using (var file = new StreamWriter(Path))
                file.Write(JsonConvert.SerializeObject(data, Formatting.Indented));
        }
        
        #endregion
    }
}
