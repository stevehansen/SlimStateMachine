namespace SlimStateMachine.Samples.Models;

public enum DocumentStatus
{
    Draft,
    PendingReview,
    UnderReview,
    ChangesRequested,
    Approved,
    Rejected,
    Published,
    Archived
}

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public string? CurrentReviewer { get; set; }
    public List<string> ReviewComments { get; set; } = [];
    public int RevisionCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public bool HasContent => !string.IsNullOrWhiteSpace(Content);
    public bool HasReviewer => !string.IsNullOrWhiteSpace(CurrentReviewer);
}
