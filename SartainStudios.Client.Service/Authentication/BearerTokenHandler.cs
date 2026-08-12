using System.Net;
using System.Net.Http.Headers;

namespace SartainStudios.Client.Service.Authentication;

public sealed class BearerTokenHandler(TokenRefresher tokenRefresher) : DelegatingHandler
{
    public const string RefreshClientName = TokenRefresher.RefreshClientName;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var session = await tokenRefresher.GetValidSessionAsync(cancellationToken);
        if (session is not null && !string.IsNullOrWhiteSpace(session.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || session is null)
            return response;
        var refreshed = await tokenRefresher.RefreshAsync(session, cancellationToken);
        if (refreshed is null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
            return response;
        var retry = await CloneAsync(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        response.Dispose();
        return await base.SendAsync(retry, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var option in request.Options)
            clone.Options.TryAdd(option.Key, option.Value);
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}