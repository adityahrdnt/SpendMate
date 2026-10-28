namespace SpendMate.Application.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
}
