namespace SpendMate.Application.Abstractions;

public interface IEmailVerificationLinkBuilder
{
    string Build(string token);
}
