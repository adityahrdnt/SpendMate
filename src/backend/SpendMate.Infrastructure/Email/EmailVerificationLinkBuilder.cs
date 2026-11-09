using Microsoft.Extensions.Options;
using SpendMate.Application.Abstractions;

namespace SpendMate.Infrastructure.Email;

public sealed class EmailVerificationLinkOptions
{
    public const string SectionName = "EmailVerificationLink";

    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}

public sealed class EmailVerificationLinkBuilder(
    IOptions<EmailVerificationLinkOptions> options)
    : IEmailVerificationLinkBuilder
{
    public string Build(string token)
    {
        var baseUrl = options.Value.FrontendBaseUrl.TrimEnd('/');

        return $"{baseUrl}/verify-email?token={Uri.EscapeDataString(token)}";
    }
}
