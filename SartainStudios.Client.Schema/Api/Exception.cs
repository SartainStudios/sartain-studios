using System.Net;

namespace SartainStudios.Client.Schema.Api;

public sealed class Exception(Problem problem, HttpStatusCode statusCode)
    : System.Exception(problem.ToMessage())
{
    public Problem Problem { get; } = problem;
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? Code => Problem.Code;

    public IReadOnlyDictionary<string, string[]> Errors =>
        Problem.Errors ?? new Dictionary<string, string[]>();
}