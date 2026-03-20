namespace Kinnect.Infrastructure;

internal sealed class KeycloakBackchannelHandler : DelegatingHandler
{
    private readonly string _publicHost;
    private readonly string _internalScheme;
    private readonly string _internalHost;
    private readonly int _internalPort;

    public KeycloakBackchannelHandler(string publicAuthority, string backchannelBaseUrl)
        : base(new HttpClientHandler())
    {
        var pub = new Uri(publicAuthority.TrimEnd('/'));
        var internal_ = new Uri(backchannelBaseUrl.TrimEnd('/'));
        _publicHost = pub.Host;
        _internalScheme = internal_.Scheme;
        _internalHost = internal_.Host;
        _internalPort = internal_.Port;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri &&
            string.Equals(uri.Host, _publicHost, StringComparison.OrdinalIgnoreCase))
        {
            request.RequestUri = new UriBuilder(uri)
            {
                Scheme = _internalScheme,
                Host = _internalHost,
                Port = _internalPort
            }.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}