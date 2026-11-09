namespace SpendMate.Infrastructure.Email;

public sealed class InvitationLinkOptions
{
    public const string SectionName = "InvitationLink";

    public string FrontendBaseUrl { get; set; } =
        "http://localhost:5173";
}
