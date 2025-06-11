using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace SimplifyQuoter.Services.ServiceLayer
{
    public static class ODataBatchHelper
    {
        /// <summary>
        /// Builds a multipart/mixed batch request for GET paths.
        /// </summary>
        public static HttpContent CreateBatchContent(
            IEnumerable<string> paths,
            string boundary)
        {
            var sb = new StringBuilder();
            foreach (var path in paths)
            {
                sb.AppendLine("--" + boundary);
                sb.AppendLine("Content-Type: application/http");
                sb.AppendLine("Content-Transfer-Encoding: binary");
                sb.AppendLine();
                sb.AppendLine("GET " + path + " HTTP/1.1");
                sb.AppendLine();
            }
            sb.AppendLine("--" + boundary + "--");

            var content = new StringContent(sb.ToString());
            content.Headers.ContentType = new MediaTypeHeaderValue("multipart/mixed")
            {
                Parameters = { new NameValueHeaderValue("boundary", boundary) }
            };
            return content;
        }

        /// <summary>
        /// Extracts JSON bodies from a multipart/mixed batch response.
        /// </summary>
        public static List<string> ParseBatchResponse(string raw, string boundary)
        {
            var bodies = new List<string>();
            var parts = raw.Split(new[] { "--" + boundary }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.TrimStart().StartsWith("--")) continue;
                int start = part.IndexOf('{');
                int end = part.LastIndexOf('}');
                if (start >= 0 && end > start)
                    bodies.Add(part.Substring(start, end - start + 1).Trim());
            }
            return bodies;
        }
    }
}
