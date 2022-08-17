using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using static QuickInfo.HtmlFactory;

namespace QuickInfo
{
    public class Program
    {
        private static readonly char[] multipleQuerySeparator = { '|' };

        private static Engine Instance { get; } = new(typeof(Engine).Assembly, typeof(Ip).Assembly);

        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            WebApplication app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapGet("/api/answers/", async httpContext =>
            {
                IHeaderDictionary headers = httpContext.Response.Headers;
                headers.Add("Content-Type", new[] { "text/html; charset=utf-8" });
                headers.Add("Cache-Control", new[] { "no-cache" });
                headers.Add("Pragma", new[] { "no-cache" });
                headers.Add("Expires", new[] { "-1" });
                headers.Add("Access-Control-Allow-Origin", new[] { "*" });
                headers.Add("Access-Control-Allow-Headers", new[] { "Content-Type" });

                string result;
                try
                {
                    result = GetResponse(Instance, httpContext.Request.Query["query"], httpContext.Request);
                }
                catch (Exception ex)
                {
                    var text = DivClass(Escape(ex.ToString()), "exceptionStack");
                    text += Div("<br/>Please open a new issue at " + A("https://github.com/KirillOsenkov/QuickInfo/issues/new") + " and paste the exception text above. Thanks and sorry for the inconvenience!");
                    result = DivClass(text, "exception");
                }

                await httpContext.Response.WriteAsync(result);
            });

            app.Run();
        }

        private static string GetResponse(Engine engine, string input, HttpRequest request = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                return Div("");
            }

            if (input.IndexOf('|') != -1)
            {
                var sb = new StringBuilder();
                sb.AppendLine("<div class=\"answersList\">");
                var multipleQueries = input.Split(multipleQuerySeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var singleQuery in multipleQueries)
                {
                    var result = GetSingleResponseWorker(engine, singleQuery, request);
                    if (result == null)
                    {
                        result = DivClass("No results.", "note");
                    }

                    sb.AppendLine("<div class=\"answerBlock\">");
                    sb.AppendLine(DivClass(singleQuery, "answerBlockHeader"));

                    result = DivClass(result, "singleAnswerSection");

                    sb.AppendLine(result);

                    sb.AppendLine("</div>");
                }

                if (multipleQueries.Length == 0)
                {
                    sb.AppendLine(DivClass("No results.", "note"));
                }

                sb.AppendLine("</div>");

                return sb.ToString();
            }
            else
            {
                var result = GetSingleResponseWorker(engine, input, request);
                if (result == null)
                {
                    result = DivClass($"No results. {SearchLink("Enter ? for help.", "?")}", "note");
                }

                result = DivClass(Environment.NewLine + result, "answersList");

                return result;
            }
        }

        private static string GetSingleResponseWorker(Engine engine, string input, HttpRequest request = null)
        {
            var query = new WebQuery(input);
            query.Request = request;

            var results = engine.GetResults(query);
            if (results == null || !results.Any())
            {
                return null;
            }

            var response = HtmlRenderer.RenderObject(results);

            if (query.IsHelp)
            {
                response = DivClass(response, "singleAnswerSection");
            }

            return response;
        }
    }
}
