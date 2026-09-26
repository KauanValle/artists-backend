namespace ArtistPlatform.Domain.Enums;

public enum UserRole
{
    Artist = 1,
    Contractor = 2,
    Admin = 3
}

public enum ArtistType
{
    Solo = 1,
    Band = 2
}

/// <summary>AVAILABLE / PRE_RESERVED / UNAVAILABLE (PRD §9).</summary>
public enum AvailabilityStatus
{
    Available = 1,
    PreReserved = 2,
    Unavailable = 3
}

/// <summary>show, ensaio, reunião, viagem, gravação e outro (PRD §8).</summary>
public enum EventType
{
    Show = 1,
    Rehearsal = 2,
    Meeting = 3,
    Travel = 4,
    Recording = 5,
    Other = 6
}

public enum EventStatus
{
    Scheduled = 1,
    Confirmed = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>Ciclo da contratação (PRD §11).</summary>
public enum BookingStatus
{
    Requested = 1,
    Negotiating = 2,
    Proposed = 3,
    Accepted = 4,
    Confirmed = 5,
    Completed = 6,
    Cancelled = 7,
    Rejected = 8,
    Expired = 9
}

public enum ProposalStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3,
    Expired = 4,
    Cancelled = 5
}

public enum TransactionType
{
    Income = 1,
    Expense = 2
}

/// <summary>Receitas: PENDING/RECEIVED/OVERDUE/CANCELLED. Despesas: PENDING/PAID/OVERDUE/CANCELLED (PRD §12).</summary>
public enum TransactionStatus
{
    Pending = 1,
    Received = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}

public enum EquipmentStatus
{
    Available = 1,
    InUse = 2,
    Maintenance = 3,
    Unavailable = 4
}

public enum NotificationType
{
    NewBookingRequest = 1,
    NewMessage = 2,
    NewProposal = 3,
    ProposalAccepted = 4,
    ProposalRejected = 5,
    ShowConfirmed = 6,
    UpcomingShow = 7,
    PaymentOverdue = 8,
    NewReview = 9
}

/// <summary>Divisão de cachê percentual ou fixa (PRD §13).</summary>
public enum ShareType
{
    Percentage = 1,
    Fixed = 2
}

public enum ContractorType
{
    Person = 1,
    Company = 2
}
