namespace SpendMate.Application.Abstractions;

public interface IInvitationLinkBuilder
{
    string Build(string token);
}
