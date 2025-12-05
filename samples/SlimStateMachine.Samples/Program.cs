using SlimStateMachine.Samples.Samples;
using Spectre.Console;

AnsiConsole.Clear();

while (true)
{
    AnsiConsole.Write(
        new FigletText("SlimStateMachine")
            .LeftJustified()
            .Color(Color.Green));

    AnsiConsole.MarkupLine("[dim]A lightweight C# state machine library[/]");
    AnsiConsole.WriteLine();

    var selection = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold]Select a sample to run:[/]")
            .PageSize(10)
            .AddChoices(
                "[green]1.[/] Invoice Workflow - Pre-conditions, Post-actions, OnEntry/OnExit",
                "[green]2.[/] Order Management - TryTransitionAny, ForceTransition, Query Methods",
                "[green]3.[/] Document Approval - Complex Multi-stage Workflow",
                "[green]4.[/] Diagram Generation - Mermaid & D2 Diagrams",
                "[red]Exit[/]"));

    if (selection.StartsWith("[red]Exit"))
    {
        AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
        break;
    }

    try
    {
        if (selection.Contains("Invoice"))
        {
            InvoiceSample.Run();
        }
        else if (selection.Contains("Order"))
        {
            OrderSample.Run();
        }
        else if (selection.Contains("Document"))
        {
            DocumentSample.Run();
        }
        else if (selection.Contains("Diagram"))
        {
            DiagramSample.Run();
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.WriteException(ex);
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Press any key to return to menu...[/]");
    Console.ReadKey(true);
    AnsiConsole.Clear();
}
