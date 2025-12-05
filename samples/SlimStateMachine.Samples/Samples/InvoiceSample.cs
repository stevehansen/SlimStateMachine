using SlimStateMachine.Samples.Models;
using Spectre.Console;

namespace SlimStateMachine.Samples.Samples;

public static class InvoiceSample
{
    private static bool _configured;

    public static void Configure()
    {
        if (_configured) return;
        _configured = true;

        StateMachine<Invoice, InvoiceStatus>.Configure(i => i.Status, builder =>
        {
            builder
                .SetInitialState(InvoiceStatus.Draft)
                // Draft transitions
                .AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent,
                    preCondition: i => i.TotalAmount > 0,
                    preConditionExpression: "TotalAmount > 0")
                .AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Cancelled)
                // Sent transitions
                .AllowTransition(InvoiceStatus.Sent, InvoiceStatus.PartiallyPaid,
                    preCondition: i => i.AmountPaid > 0 && !i.IsFullyPaid,
                    preConditionExpression: "AmountPaid > 0 && !IsFullyPaid")
                .AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Paid,
                    preCondition: i => i.IsFullyPaid,
                    preConditionExpression: "IsFullyPaid",
                    postAction: i => i.PaidDate = DateTime.Now)
                .AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Overdue,
                    preCondition: i => i.IsOverdue,
                    preConditionExpression: "IsOverdue")
                .AllowTransition(InvoiceStatus.Sent, InvoiceStatus.Cancelled)
                // PartiallyPaid transitions
                .AllowTransition(InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid,
                    preCondition: i => i.IsFullyPaid,
                    preConditionExpression: "IsFullyPaid",
                    postAction: i => i.PaidDate = DateTime.Now)
                .AllowTransition(InvoiceStatus.PartiallyPaid, InvoiceStatus.Overdue,
                    preCondition: i => i.IsOverdue,
                    preConditionExpression: "IsOverdue")
                // Overdue transitions
                .AllowTransition(InvoiceStatus.Overdue, InvoiceStatus.PartiallyPaid,
                    preCondition: i => i.AmountPaid > 0 && !i.IsFullyPaid,
                    preConditionExpression: "AmountPaid > 0 && !IsFullyPaid")
                .AllowTransition(InvoiceStatus.Overdue, InvoiceStatus.Paid,
                    preCondition: i => i.IsFullyPaid,
                    preConditionExpression: "IsFullyPaid",
                    postAction: i => i.PaidDate = DateTime.Now)
                .AllowTransition(InvoiceStatus.Overdue, InvoiceStatus.Cancelled)
                // OnEntry/OnExit actions
                .OnEntry(InvoiceStatus.Cancelled, i =>
                    AnsiConsole.MarkupLine("[red]Invoice cancelled![/]"))
                .OnEntry(InvoiceStatus.Paid, i =>
                    AnsiConsole.MarkupLine("[green]Invoice fully paid![/]"))
                .OnExit(InvoiceStatus.Draft, i =>
                    AnsiConsole.MarkupLine("[dim]Leaving draft status...[/]"));
        });

        // Subscribe to transition events for logging
        StateMachine<Invoice, InvoiceStatus>.OnTransition += context =>
        {
            var reasonText = string.IsNullOrEmpty(context.Reason) ? "" : $" (Reason: {context.Reason})";
            var forcedText = context.WasForced ? " [FORCED]" : "";
            AnsiConsole.MarkupLine($"[blue]>> Transition: {context.FromState} -> {context.ToState}{reasonText}{forcedText}[/]");
        };
    }

    public static void Run()
    {
        Configure();

        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[yellow]Invoice Workflow Sample[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]This sample demonstrates:[/]");
        AnsiConsole.MarkupLine("  - Pre-conditions with expressions");
        AnsiConsole.MarkupLine("  - Post-actions (setting PaidDate)");
        AnsiConsole.MarkupLine("  - OnEntry/OnExit state actions");
        AnsiConsole.MarkupLine("  - OnTransition event logging");
        AnsiConsole.MarkupLine("  - Transition with reason/metadata");
        AnsiConsole.WriteLine();

        // Create an invoice
        var invoice = new Invoice
        {
            Id = 1,
            CustomerName = "Acme Corp",
            TotalAmount = 1000m,
            DueDate = DateTime.Now.AddDays(30)
        };

        DisplayInvoice(invoice);

        while (true)
        {
            var possibleTransitions = StateMachine<Invoice, InvoiceStatus>.GetPossibleTransitions(invoice);
            var isFinalState = StateMachine<Invoice, InvoiceStatus>.IsInFinalState(invoice);

            if (isFinalState)
            {
                AnsiConsole.MarkupLine("[yellow]Invoice is in a final state. No more transitions possible.[/]");
                break;
            }

            var choices = new List<string>();
            foreach (var state in possibleTransitions)
            {
                choices.Add($"Transition to {state}");
            }
            choices.Add("Make a payment");
            choices.Add("Show diagram");
            choices.Add("Exit to menu");

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to do?")
                    .AddChoices(choices));

            if (action == "Exit to menu")
                break;

            if (action == "Make a payment")
            {
                var amount = AnsiConsole.Ask<decimal>("Enter payment amount:");
                invoice.AmountPaid += amount;
                AnsiConsole.MarkupLine($"[green]Payment of {amount:C} recorded. Total paid: {invoice.AmountPaid:C}[/]");
                DisplayInvoice(invoice);
                continue;
            }

            if (action == "Show diagram")
            {
                ShowDiagram(invoice);
                continue;
            }

            // Handle transition
            var targetState = Enum.Parse<InvoiceStatus>(action.Replace("Transition to ", ""));
            var reason = AnsiConsole.Ask("Enter reason (or press Enter to skip):", "");

            var metadata = new Dictionary<string, object>
            {
                ["userId"] = "user123",
                ["timestamp"] = DateTime.Now
            };

            var success = StateMachine<Invoice, InvoiceStatus>.TryTransition(
                invoice, targetState, reason, metadata);

            if (success)
            {
                AnsiConsole.MarkupLine("[green]Transition successful![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Transition failed - pre-condition not met.[/]");
            }

            DisplayInvoice(invoice);
        }
    }

    private static void DisplayInvoice(Invoice invoice)
    {
        AnsiConsole.WriteLine();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("ID", invoice.Id.ToString());
        table.AddRow("Customer", invoice.CustomerName);
        table.AddRow("Total Amount", invoice.TotalAmount.ToString("C"));
        table.AddRow("Amount Paid", invoice.AmountPaid.ToString("C"));
        table.AddRow("Amount Due", invoice.AmountDue.ToString("C"));
        table.AddRow("Status", $"[bold]{invoice.Status}[/]");
        table.AddRow("Due Date", invoice.DueDate?.ToString("d") ?? "N/A");
        table.AddRow("Paid Date", invoice.PaidDate?.ToString("d") ?? "N/A");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void ShowDiagram(Invoice invoice)
    {
        AnsiConsole.WriteLine();
        var format = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select diagram format:")
                .AddChoices("Mermaid", "D2"));

        string diagram;
        if (format == "Mermaid")
        {
            diagram = StateMachine<Invoice, InvoiceStatus>.GenerateMermaidGraph(invoice);
        }
        else
        {
            diagram = StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph(invoice);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(diagram)
            .Header($"[yellow]{format} Diagram[/]")
            .Border(BoxBorder.Rounded));
        AnsiConsole.WriteLine();
    }
}
