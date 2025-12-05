using SlimStateMachine.Samples.Models;
using Spectre.Console;

namespace SlimStateMachine.Samples.Samples;

public static class DocumentSample
{
    private static bool _configured;

    public static void Configure()
    {
        if (_configured) return;
        _configured = true;

        StateMachine<Document, DocumentStatus>.Configure(d => d.Status, builder =>
        {
            builder
                .SetInitialState(DocumentStatus.Draft)
                // Draft transitions
                .AllowTransition(DocumentStatus.Draft, DocumentStatus.PendingReview,
                    preCondition: d => d.HasContent,
                    preConditionExpression: "HasContent")
                // PendingReview transitions
                .AllowTransition(DocumentStatus.PendingReview, DocumentStatus.UnderReview,
                    preCondition: d => d.HasReviewer,
                    preConditionExpression: "HasReviewer")
                .AllowTransition(DocumentStatus.PendingReview, DocumentStatus.Draft)
                // UnderReview transitions
                .AllowTransition(DocumentStatus.UnderReview, DocumentStatus.Approved,
                    postAction: d => d.ApprovedAt = DateTime.Now)
                .AllowTransition(DocumentStatus.UnderReview, DocumentStatus.Rejected)
                .AllowTransition(DocumentStatus.UnderReview, DocumentStatus.ChangesRequested)
                // ChangesRequested transitions
                .AllowTransition(DocumentStatus.ChangesRequested, DocumentStatus.Draft,
                    postAction: d => d.RevisionCount++)
                // Approved transitions
                .AllowTransition(DocumentStatus.Approved, DocumentStatus.Published,
                    postAction: d => d.PublishedAt = DateTime.Now)
                .AllowTransition(DocumentStatus.Approved, DocumentStatus.Draft,
                    postAction: d => d.RevisionCount++)
                // Published transitions
                .AllowTransition(DocumentStatus.Published, DocumentStatus.Archived)
                .AllowTransition(DocumentStatus.Published, DocumentStatus.Draft,
                    postAction: d => d.RevisionCount++)
                // Rejected transitions
                .AllowTransition(DocumentStatus.Rejected, DocumentStatus.Draft,
                    postAction: d => d.RevisionCount++)
                // OnEntry actions for audit trail
                .OnEntry(DocumentStatus.Approved, d =>
                    AnsiConsole.MarkupLine($"[green]Document approved at {d.ApprovedAt:g}[/]"))
                .OnEntry(DocumentStatus.Published, d =>
                    AnsiConsole.MarkupLine($"[green]Document published at {d.PublishedAt:g}[/]"))
                .OnEntry(DocumentStatus.Rejected, d =>
                    AnsiConsole.MarkupLine("[red]Document rejected[/]"))
                .OnExit(DocumentStatus.Draft, d =>
                    AnsiConsole.MarkupLine($"[dim]Document revision {d.RevisionCount} submitted[/]"));
        });

        StateMachine<Document, DocumentStatus>.OnTransition += context =>
        {
            var doc = (Document)context.Entity;
            AnsiConsole.MarkupLine($"[blue]>> Document '{doc.Title}': {context.FromState} -> {context.ToState}[/]");
        };
    }

    public static void Run()
    {
        Configure();

        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[yellow]Document Approval Workflow[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]This sample demonstrates:[/]");
        AnsiConsole.MarkupLine("  - Complex multi-stage workflow");
        AnsiConsole.MarkupLine("  - Loops back to draft for revisions");
        AnsiConsole.MarkupLine("  - Revision counting via post-actions");
        AnsiConsole.MarkupLine("  - Multiple paths to final state");
        AnsiConsole.WriteLine();

        var doc = new Document
        {
            Id = 1,
            Title = AnsiConsole.Ask<string>("Enter document title:"),
            Author = AnsiConsole.Ask<string>("Enter author name:")
        };

        DisplayDocument(doc);

        while (true)
        {
            var isFinal = StateMachine<Document, DocumentStatus>.IsInFinalState(doc);
            var possibleTransitions = StateMachine<Document, DocumentStatus>.GetPossibleTransitions(doc);

            if (isFinal)
            {
                AnsiConsole.MarkupLine("[yellow]Document is in a final state (Archived).[/]");
                break;
            }

            var choices = new List<string>();

            // Add action choices based on current state
            switch (doc.Status)
            {
                case DocumentStatus.Draft:
                    choices.Add("Edit content");
                    if (possibleTransitions.Contains(DocumentStatus.PendingReview))
                        choices.Add("Submit for review");
                    break;

                case DocumentStatus.PendingReview:
                    choices.Add("Assign reviewer");
                    if (possibleTransitions.Contains(DocumentStatus.UnderReview))
                        choices.Add("Start review");
                    choices.Add("Return to draft");
                    break;

                case DocumentStatus.UnderReview:
                    choices.Add("Approve");
                    choices.Add("Reject");
                    choices.Add("Request changes");
                    break;

                case DocumentStatus.ChangesRequested:
                    choices.Add("Return to draft for revision");
                    break;

                case DocumentStatus.Approved:
                    choices.Add("Publish");
                    choices.Add("Return to draft for revision");
                    break;

                case DocumentStatus.Published:
                    choices.Add("Archive");
                    choices.Add("Unpublish (return to draft)");
                    break;

                case DocumentStatus.Rejected:
                    choices.Add("Return to draft for revision");
                    break;
            }

            choices.Add("Show workflow diagram");
            choices.Add("Exit to menu");

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[cyan]Current status: {doc.Status}[/] - What would you like to do?")
                    .AddChoices(choices));

            AnsiConsole.WriteLine();

            switch (action)
            {
                case "Edit content":
                    doc.Content = AnsiConsole.Ask<string>("Enter document content:");
                    AnsiConsole.MarkupLine("[green]Content updated.[/]");
                    break;

                case "Submit for review":
                    StateMachine<Document, DocumentStatus>.TryTransition(doc, DocumentStatus.PendingReview);
                    break;

                case "Assign reviewer":
                    doc.CurrentReviewer = AnsiConsole.Ask<string>("Enter reviewer name:");
                    AnsiConsole.MarkupLine($"[green]Reviewer '{doc.CurrentReviewer}' assigned.[/]");
                    break;

                case "Start review":
                    StateMachine<Document, DocumentStatus>.TryTransition(doc, DocumentStatus.UnderReview);
                    break;

                case "Return to draft":
                case "Return to draft for revision":
                case "Unpublish (return to draft)":
                    StateMachine<Document, DocumentStatus>.TryTransition(doc, DocumentStatus.Draft);
                    break;

                case "Approve":
                    var comment = AnsiConsole.Ask("Enter approval comment:", "Looks good!");
                    doc.ReviewComments.Add($"[Approved] {comment}");
                    StateMachine<Document, DocumentStatus>.TryTransition(doc, DocumentStatus.Approved);
                    break;

                case "Reject":
                    var rejectReason = AnsiConsole.Ask<string>("Enter rejection reason:");
                    doc.ReviewComments.Add($"[Rejected] {rejectReason}");
                    StateMachine<Document, DocumentStatus>.TryTransition(doc, DocumentStatus.Rejected);
                    break;

                case "Request changes":
                    var changeRequest = AnsiConsole.Ask<string>("Enter change request:");
                    doc.ReviewComments.Add($"[Changes Requested] {changeRequest}");
                    StateMachine<Document, DocumentStatus>.TryTransition(doc, DocumentStatus.ChangesRequested);
                    break;

                case "Publish":
                    StateMachine<Document, DocumentStatus>.TryTransition(doc, DocumentStatus.Published);
                    break;

                case "Archive":
                    StateMachine<Document, DocumentStatus>.TryTransition(doc, DocumentStatus.Archived);
                    break;

                case "Show workflow diagram":
                    ShowDiagram(doc);
                    continue;

                case "Exit to menu":
                    return;
            }

            DisplayDocument(doc);
        }
    }

    private static void DisplayDocument(Document doc)
    {
        AnsiConsole.WriteLine();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("ID", doc.Id.ToString());
        table.AddRow("Title", doc.Title);
        table.AddRow("Author", doc.Author);
        table.AddRow("Content", string.IsNullOrWhiteSpace(doc.Content) ? "[dim]<empty>[/]" : doc.Content.Length > 50 ? doc.Content[..50] + "..." : doc.Content);
        table.AddRow("Status", $"[bold]{doc.Status}[/]");
        table.AddRow("Reviewer", doc.CurrentReviewer ?? "[dim]Not assigned[/]");
        table.AddRow("Revisions", doc.RevisionCount.ToString());
        table.AddRow("Created", doc.CreatedAt.ToString("g"));
        table.AddRow("Approved", doc.ApprovedAt?.ToString("g") ?? "N/A");
        table.AddRow("Published", doc.PublishedAt?.ToString("g") ?? "N/A");

        AnsiConsole.Write(table);

        if (doc.ReviewComments.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Review Comments:[/]");
            foreach (var reviewComment in doc.ReviewComments)
            {
                AnsiConsole.MarkupLine($"  - {reviewComment}");
            }
        }

        AnsiConsole.WriteLine();
    }

    private static void ShowDiagram(Document doc)
    {
        var diagram = StateMachine<Document, DocumentStatus>.GenerateMermaidGraph(doc);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(diagram)
            .Header("[yellow]Document Workflow (Mermaid)[/]")
            .Border(BoxBorder.Rounded));
        AnsiConsole.WriteLine();
    }
}
