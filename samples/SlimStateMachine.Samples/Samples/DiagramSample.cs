using SlimStateMachine.Samples.Models;
using Spectre.Console;

namespace SlimStateMachine.Samples.Samples;

public static class DiagramSample
{
    public static void Run()
    {
        // Ensure configurations are loaded
        InvoiceSample.Configure();
        OrderSample.Configure();
        DocumentSample.Configure();

        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[yellow]Diagram Generation Sample[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]This sample demonstrates:[/]");
        AnsiConsole.MarkupLine("  - Mermaid diagram generation");
        AnsiConsole.MarkupLine("  - D2 diagram generation");
        AnsiConsole.MarkupLine("  - State highlighting");
        AnsiConsole.MarkupLine("  - Pre-condition labels in diagrams");
        AnsiConsole.MarkupLine("  - DiagramType enum for format selection");
        AnsiConsole.WriteLine();

        while (true)
        {
            var stateMachine = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a state machine to visualize:")
                    .AddChoices(
                        "Invoice State Machine",
                        "Order State Machine",
                        "Document State Machine",
                        "Exit to menu"));

            if (stateMachine == "Exit to menu")
                return;

            AnsiConsole.WriteLine();

            var format = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select diagram format:")
                    .AddChoices(
                        "Mermaid",
                        "D2",
                        "D2 (without styling)",
                        "Both formats"));

            var highlightState = AnsiConsole.Confirm("Highlight a specific state?", false);

            AnsiConsole.WriteLine();

            switch (stateMachine)
            {
                case "Invoice State Machine":
                    ShowInvoiceDiagrams(format, highlightState);
                    break;

                case "Order State Machine":
                    ShowOrderDiagrams(format, highlightState);
                    break;

                case "Document State Machine":
                    ShowDocumentDiagrams(format, highlightState);
                    break;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
    }

    private static void ShowInvoiceDiagrams(string format, bool highlightState)
    {
        InvoiceStatus? highlight = null;
        if (highlightState)
        {
            highlight = AnsiConsole.Prompt(
                new SelectionPrompt<InvoiceStatus>()
                    .Title("Select state to highlight:")
                    .AddChoices(Enum.GetValues<InvoiceStatus>()));
        }

        if (format is "Mermaid" or "Both formats")
        {
            var mermaid = highlight.HasValue
                ? StateMachine<Invoice, InvoiceStatus>.GenerateMermaidGraph(highlight.Value)
                : StateMachine<Invoice, InvoiceStatus>.GenerateMermaidGraph();

            ShowDiagramPanel("Mermaid - Invoice State Machine", mermaid);
        }

        if (format is "D2" or "Both formats")
        {
            var d2 = highlight.HasValue
                ? StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph(highlight.Value)
                : StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph();

            ShowDiagramPanel("D2 - Invoice State Machine", d2);
        }

        if (format == "D2 (without styling)")
        {
            var d2NoStyle = highlight.HasValue
                ? StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph(highlight.Value, includeStyles: false)
                : StateMachine<Invoice, InvoiceStatus>.GenerateD2Graph(includeStyles: false);

            ShowDiagramPanel("D2 (no styling) - Invoice State Machine", d2NoStyle);
        }

        // Show using DiagramType enum
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Using DiagramType enum:[/]");
        var diagramByType = StateMachine<Invoice, InvoiceStatus>.GenerateDiagram(
            StateMachine<Invoice, InvoiceStatus>.DiagramType.Mermaid);
        AnsiConsole.MarkupLine($"[dim]GenerateDiagram(DiagramType.Mermaid) returns same as GenerateMermaidGraph()[/]");
    }

    private static void ShowOrderDiagrams(string format, bool highlightState)
    {
        OrderStatus? highlight = null;
        if (highlightState)
        {
            highlight = AnsiConsole.Prompt(
                new SelectionPrompt<OrderStatus>()
                    .Title("Select state to highlight:")
                    .AddChoices(Enum.GetValues<OrderStatus>()));
        }

        if (format is "Mermaid" or "Both formats")
        {
            var mermaid = highlight.HasValue
                ? StateMachine<Order, OrderStatus>.GenerateMermaidGraph(highlight.Value)
                : StateMachine<Order, OrderStatus>.GenerateMermaidGraph();

            ShowDiagramPanel("Mermaid - Order State Machine", mermaid);
        }

        if (format is "D2" or "Both formats")
        {
            var d2 = highlight.HasValue
                ? StateMachine<Order, OrderStatus>.GenerateD2Graph(highlight.Value)
                : StateMachine<Order, OrderStatus>.GenerateD2Graph();

            ShowDiagramPanel("D2 - Order State Machine", d2);
        }

        if (format == "D2 (without styling)")
        {
            var d2NoStyle = highlight.HasValue
                ? StateMachine<Order, OrderStatus>.GenerateD2Graph(highlight.Value, includeStyles: false)
                : StateMachine<Order, OrderStatus>.GenerateD2Graph(includeStyles: false);

            ShowDiagramPanel("D2 (no styling) - Order State Machine", d2NoStyle);
        }
    }

    private static void ShowDocumentDiagrams(string format, bool highlightState)
    {
        DocumentStatus? highlight = null;
        if (highlightState)
        {
            highlight = AnsiConsole.Prompt(
                new SelectionPrompt<DocumentStatus>()
                    .Title("Select state to highlight:")
                    .AddChoices(Enum.GetValues<DocumentStatus>()));
        }

        if (format is "Mermaid" or "Both formats")
        {
            var mermaid = highlight.HasValue
                ? StateMachine<Document, DocumentStatus>.GenerateMermaidGraph(highlight.Value)
                : StateMachine<Document, DocumentStatus>.GenerateMermaidGraph();

            ShowDiagramPanel("Mermaid - Document State Machine", mermaid);
        }

        if (format is "D2" or "Both formats")
        {
            var d2 = highlight.HasValue
                ? StateMachine<Document, DocumentStatus>.GenerateD2Graph(highlight.Value)
                : StateMachine<Document, DocumentStatus>.GenerateD2Graph();

            ShowDiagramPanel("D2 - Document State Machine", d2);
        }

        if (format == "D2 (without styling)")
        {
            var d2NoStyle = highlight.HasValue
                ? StateMachine<Document, DocumentStatus>.GenerateD2Graph(highlight.Value, includeStyles: false)
                : StateMachine<Document, DocumentStatus>.GenerateD2Graph(includeStyles: false);

            ShowDiagramPanel("D2 (no styling) - Document State Machine", d2NoStyle);
        }
    }

    private static void ShowDiagramPanel(string title, string content)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(content)
            .Header($"[yellow]{title}[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
    }
}
