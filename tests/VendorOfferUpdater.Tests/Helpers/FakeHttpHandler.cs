using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VendorOfferUpdater.Tests.Helpers
{
    /// <summary>
    /// Test HttpMessageHandler that matches requests by URL predicate or
    /// falls back to a queue of canned responses. Records all requested URLs.
    /// </summary>
    public class FakeHttpHandler : HttpMessageHandler
    {
        private readonly List<(Func<string, bool> Predicate, string Body, HttpStatusCode StatusCode)> _urlMap = new();
        private readonly Queue<(string Body, HttpStatusCode StatusCode)> _queue = new();

        public List<string> RequestedUrls { get; } = new();

        /// <summary>
        /// Register a canned response for URLs matching the predicate.
        /// Earlier registrations take priority.
        /// </summary>
        public void MapUrl(Func<string, bool> predicate, string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _urlMap.Add((predicate, responseBody, statusCode));
        }

        /// <summary>
        /// Enqueue a response that will be returned (FIFO) when no URL predicate matches.
        /// Useful for ordered pagination sequences.
        /// </summary>
        public void Enqueue(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _queue.Enqueue((responseBody, statusCode));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri?.ToString() ?? "";
            RequestedUrls.Add(url);

            foreach (var (predicate, body, statusCode) in _urlMap)
            {
                if (predicate(url))
                {
                    return Task.FromResult(new HttpResponseMessage(statusCode)
                    {
                        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
                    });
                }
            }

            if (_queue.Count > 0)
            {
                var (qBody, qStatus) = _queue.Dequeue();
                return Task.FromResult(new HttpResponseMessage(qStatus)
                {
                    Content = new StringContent(qBody, System.Text.Encoding.UTF8, "application/json")
                });
            }

            throw new InvalidOperationException(
                $"FakeHttpHandler: no matching predicate and queue is empty for URL: {url}");
        }
    }
}
