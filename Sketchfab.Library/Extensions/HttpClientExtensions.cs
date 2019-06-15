using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Sketchfab.Extensions
{
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Sends a get request to the Url provided using this session cookies.
        /// </summary>
        /// <returns> Response object containing the result of the response </returns>
        public static Response GetRequest(this HttpClient client, string url)
        {
            var response = client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            return new Response()
            {
                Result = response,
                Content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(),
                StatusCode = response.StatusCode
            };
        }

        /// <summary>
        /// Sends a post request to the Url provided using this session cookies and returns the string result.
        /// </summary>
        public static Response PostRequest(this HttpClient client, string url, string data)
        {
            var response = client.PostAsync(url, new StringContent(data)).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            return new Response()
            {
                Result = response,
                Content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(),
                StatusCode = response.StatusCode
            };
        }


        #region GetString

        /// <summary>
        ///     Send a GET request to the specified Uri and return the response body as a string in a synchronous operation.
        /// </summary>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="ArgumentNullException">
        ///     The <paramref name="requestUri"/> was null.
        /// </exception>
        public static string GetString(this HttpClient client, string requestUri)
        {
            return Task.Run(() => client.GetStringAsync(requestUri)).Result;
        }

        /// <summary>
        ///     Send a GET request to the specified Uri and return the response body as a string in a synchronous operation.
        /// </summary>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="ArgumentNullException">
        ///     The <paramref name="requestUri"/> was null.
        /// </exception>
        public static string GetString(this HttpClient client, Uri requestUri)
        {
            return Task.Run(() => client.GetStringAsync(requestUri)).Result;
        }

        #endregion

        #region GetStream

        /// <summary>
        ///     Send a GET request to the specified Uri and return the response body as a stream in a synchronous operation.
        /// </summary>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="ArgumentNullException">
        ///     The <paramref name="requestUri"/> was null.
        /// </exception>
        public static Stream GetStream(this HttpClient client, string requestUri)
        {
            return Task.Run(() => client.GetStreamAsync(requestUri)).Result;
        }

        /// <summary>
        ///     Send a GET request to the specified Uri and return the response body as a stream in a synchronous operation.
        /// </summary>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <exception cref="ArgumentNullException">
        ///     The <paramref name="requestUri"/> was null.
        /// </exception>
        public static Stream GetStream(this HttpClient client, Uri requestUri)
        {
            return Task.Run(() => client.GetStreamAsync(requestUri)).Result;
        }

        #endregion


    }

    public class Response
    {
        public HttpResponseMessage Result { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public string Content { get; set; }
    }

}
