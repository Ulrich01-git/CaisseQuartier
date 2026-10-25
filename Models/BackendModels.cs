namespace CaisseQuartier.Models;

public sealed record ApiErrorResponse(string Message);

public sealed record AssociationInfo(
    Guid Id,
    string Name,
    string Purpose,
    decimal MonthlyGoal,
    string JoinCode);

public sealed record MemberIdentity(
    Guid Id,
    Guid AssociationId,
    string FullName,
    string Contact,
    string? Phone,
    string? Email,
    string? Neighborhood,
    string? Occupation,
    DateOnly JoinedOn,
    decimal MonthlyContribution,
    string? PhotoUrl,
    string Role);

public sealed record AuthResponse(string Token, MemberIdentity Member, AssociationInfo Association);

public sealed record RegisterRequest(
    string FullName,
    string Contact,
    string Password,
    string? JoinCode,
    string? AssociationName,
    string? Purpose,
    decimal MonthlyGoal);

public sealed record LoginRequest(string Contact, string Password);

public sealed record AssociationUpdateRequest(string Name, string Purpose, decimal MonthlyGoal);

public sealed record MemberProfile(
    Guid Id,
    string FullName,
    string Contact,
    string? Phone,
    string? Email,
    string? Neighborhood,
    string? Occupation,
    DateOnly JoinedOn,
    decimal MonthlyContribution,
    decimal PaidThisMonth,
    decimal DueThisMonth,
    string? PhotoUrl,
    string Role,
    bool CanEdit)
{
    public string Name => FullName;
    public decimal Expected => MonthlyContribution;
    public decimal Paid => PaidThisMonth;
    public decimal Due => DueThisMonth;
    public bool IsLate => DueThisMonth > 0;
    public string AmountLabel => IsLate
        ? $"{Helpers.Money.Fcfa(DueThisMonth)} à régler"
        : $"{Helpers.Money.Fcfa(PaidThisMonth)} versés";
}

public sealed record MemberCreateRequest(
    string FullName,
    string? Contact,
    string? Phone,
    string? Email,
    string? Neighborhood,
    string? Occupation,
    DateOnly JoinedOn,
    decimal MonthlyContribution);

public sealed record MemberUpdateRequest(
    string FullName,
    string? Phone,
    string? Email,
    string? Neighborhood,
    string? Occupation,
    decimal MonthlyContribution);

public sealed record TransactionCreateRequest(
    Guid MemberId,
    string? Title,
    string? Description,
    decimal Amount,
    DateOnly Date);

public sealed record TransactionInfo(
    Guid Id,
    string Type,
    Guid MemberId,
    string MemberName,
    string Title,
    string? Description,
    decimal Amount,
    DateOnly Date,
    string RecordedBy,
    DateTime CreatedAt)
{
    public string DateLabel => Date.ToString("dd/MM/yyyy");
    public string AmountLabel => Helpers.Money.Fcfa(Amount);
}

public sealed record AuditEventInfo(
    Guid Id,
    string Type,
    string Summary,
    string ActorName,
    DateTime CreatedAt);

public sealed record DashboardInfo(
    AssociationInfo Association,
    DateOnly From,
    DateOnly To,
    decimal Contributions,
    decimal Expenses,
    decimal Balance,
    decimal Goal,
    IReadOnlyList<TransactionInfo> RecentTransactions);
