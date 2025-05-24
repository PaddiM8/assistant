namespace Assistant.Services.Planera;

public record PlaneraTicket(
    int Id,
    string Title,
    string Description,
    PlaneraTicketPriority Priority,
    PlaneraTicketStatus Status
);

public enum PlaneraTicketStatus
{
    None,
    Done,
    Inactive,
    Closed,
}

public enum PlaneraTicketPriority
{
    None,
    Low,
    Normal,
    High,
    Severe,
}

public enum PlaneraTicketFilter
{
    All,
    Open,
    Closed,
    Inactive,
    Done,
    AssignedToMe,
}
