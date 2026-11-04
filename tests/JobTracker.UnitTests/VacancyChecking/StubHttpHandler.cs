using System.Net;
using System.Text;

namespace JobTracker.UnitTests.VacancyChecking;

/// <summary>Подменяет сеть: HttpClient получает заранее заданный ответ или исключение.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }

    public static StubHttpHandler Html(string html, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(html, Encoding.UTF8, "text/html") });

    public static StubHttpHandler Fixture(string name, HttpStatusCode status = HttpStatusCode.OK) =>
        Html(ReadFixture(name), status);

    /// <summary>Как будто HttpClient прошёл по редиректу: в ответе конечный адрес, а не запрошенный.</summary>
    public static StubHttpHandler RedirectedTo(Uri finalUrl, string html = "") =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUrl),
        });

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "VacancyChecking", "Fixtures", name));

    public static StubHttpHandler Throws(Exception exception) => new(_ => throw exception);
}
