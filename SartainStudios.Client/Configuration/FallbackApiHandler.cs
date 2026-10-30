namespace SartainStudios.Client.Configuration;

public sealed class FallbackApiHandler(ApiSettings settings) : DelegatingHandler
{
    private static volatile bool _useFallback;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.FallbackBaseUrl) || request.RequestUri is null)
            return await base.SendAsync(request, cancellationToken);

        var primary = new Uri(settings.BaseUrl);
        var fallback = new Uri(settings.FallbackBaseUrl);

        if (_useFallback)
            return await SendToAsync(request, fallback, cancellationToken);

        try
        {
            var response = await SendToAsync(request, primary, cancellationToken);
            if ((int)response.StatusCode < 502 || (int)response.StatusCode > 504) return response;
            response.Dispose();
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        _useFallback = true;
        return await SendToAsync(request, fallback, cancellationToken);
    }

    private Task<HttpResponseMessage> SendToAsync(
        HttpRequestMessage request,
        Uri target,
        CancellationToken cancellationToken)
    {
        var current = request.RequestUri!;
        request.RequestUri = new UriBuilder(current)
        {
            Scheme = target.Scheme,
            Host = target.Host,
            Port = target.Port
        }.Uri;

        return base.SendAsync(request, cancellationToken);
    }
}