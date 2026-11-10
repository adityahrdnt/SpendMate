namespace SpendMate.Application.Abstractions;

public enum FamilyMembershipOperationResult
{
    Success,
    FamilyNotFound,
    NotSharedFamily,
    RequesterNotOwner,
    TargetMembershipNotFound,
    LastOwner,
    CannotChangeOwnRole
}
