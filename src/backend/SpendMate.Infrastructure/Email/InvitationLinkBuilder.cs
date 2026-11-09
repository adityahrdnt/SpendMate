using Microsoft.Extensions.Options;
using SpendMate.Application.Abstractions;

namespace SpendMate.Infrastructure.Email;

public sealed class InvitationLinkBuilder(
    IOptions<InvitationLinkOptions> options)
    : IInvitationLinkBuilder
{
    public string Build(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var baseUrl = options.Value.FrontendBaseUrl.TrimEnd('/');

        return $"{baseUrl}/invitations/accept?token={Uri.EscapeDataString(token)}";
    }
}
