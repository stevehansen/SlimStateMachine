using SlimStateMachine.Samples.Models;
using Spectre.Console;

namespace SlimStateMachine.Samples.Samples;

public static class OrderSample
{
    private static bool _configured;

    public static void Configure()
    {
        if (_configured) return;
        _configured = true;

        StateMachine<Order, OrderStatus>.Configure(o => o.Status, builder =>
        {
            builder
                .SetInitialState(OrderStatus.Created)
                // Created transitions
                .AllowTransition(OrderStatus.Created, OrderStatus.Confirmed,
                    preCondition: o => o.PaymentReceived,
                    preConditionExpression: "PaymentReceived")
                .AllowTransition(OrderStatus.Created, OrderStatus.Cancelled)
                // Confirmed transitions
                .AllowTransition(OrderStatus.Confirmed, OrderStatus.Processing,
                    preCondition: o => o.InStock,
                    preConditionExpression: "InStock")
                .AllowTransition(OrderStatus.Confirmed, OrderStatus.Cancelled)
                // Processing transitions
                .AllowTransition(OrderStatus.Processing, OrderStatus.Shipped,
                    postAction: o =>
                    {
                        o.TrackingNumber = $"TRK-{Guid.NewGuid().ToString()[..8].ToUpper()}";
                        o.ShippedDate = DateTime.Now;
                    })
                .AllowTransition(OrderStatus.Processing, OrderStatus.Cancelled)
                // Shipped transitions
                .AllowTransition(OrderStatus.Shipped, OrderStatus.Delivered,
                    postAction: o => o.DeliveredDate = DateTime.Now)
                // Delivered transitions
                .AllowTransition(OrderStatus.Delivered, OrderStatus.Returned)
                // OnEntry actions
                .OnEntry(OrderStatus.Shipped, o =>
                    AnsiConsole.MarkupLine($"[green]Order shipped! Tracking: {o.TrackingNumber}[/]"))
                .OnEntry(OrderStatus.Delivered, o =>
                    AnsiConsole.MarkupLine("[green]Order delivered successfully![/]"))
                .OnEntry(OrderStatus.Cancelled, o =>
                    AnsiConsole.MarkupLine("[red]Order has been cancelled.[/]"));
        });

        StateMachine<Order, OrderStatus>.OnTransition += context =>
        {
            AnsiConsole.MarkupLine($"[blue]>> Order {((Order)context.Entity).Id}: {context.FromState} -> {context.ToState}[/]");
        };
    }

    public static void Run()
    {
        Configure();

        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[yellow]Order Management Sample[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]This sample demonstrates:[/]");
        AnsiConsole.MarkupLine("  - TryTransitionAny (first valid transition)");
        AnsiConsole.MarkupLine("  - ForceTransition (bypassing pre-conditions)");
        AnsiConsole.MarkupLine("  - GetDefinedTransitions vs GetPossibleTransitions");
        AnsiConsole.MarkupLine("  - CanTransition checks");
        AnsiConsole.MarkupLine("  - Query methods (IsFinalState, GetAllStates)");
        AnsiConsole.WriteLine();

        var order = new Order
        {
            Id = 1001,
            CustomerName = "John Doe",
            Items = ["Widget A", "Widget B", "Gadget X"]
        };

        DisplayOrder(order);

        while (true)
        {
            var isFinal = StateMachine<Order, OrderStatus>.IsInFinalState(order);

            if (isFinal)
            {
                AnsiConsole.MarkupLine("[yellow]Order is in a final state.[/]");
                AnsiConsole.WriteLine();
            }

            var choices = new List<string>
            {
                "Show possible transitions",
                "Show defined transitions",
                "Attempt specific transition",
                "Try any transition",
                "Force transition",
                "Toggle payment received",
                "Toggle in stock",
                "Show all states",
                "Show diagram",
                "Exit to menu"
            };

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select an action:")
                    .AddChoices(choices));

            AnsiConsole.WriteLine();

            switch (action)
            {
                case "Show possible transitions":
                    ShowPossibleTransitions(order);
                    break;

                case "Show defined transitions":
                    ShowDefinedTransitions(order);
                    break;

                case "Attempt specific transition":
                    AttemptTransition(order);
                    break;

                case "Try any transition":
                    TryAnyTransition(order);
                    break;

                case "Force transition":
                    ForceTransition(order);
                    break;

                case "Toggle payment received":
                    order.PaymentReceived = !order.PaymentReceived;
                    AnsiConsole.MarkupLine($"[cyan]Payment received: {order.PaymentReceived}[/]");
                    DisplayOrder(order);
                    break;

                case "Toggle in stock":
                    order.InStock = !order.InStock;
                    AnsiConsole.MarkupLine($"[cyan]In stock: {order.InStock}[/]");
                    DisplayOrder(order);
                    break;

                case "Show all states":
                    ShowAllStates();
                    break;

                case "Show diagram":
                    ShowDiagram(order);
                    break;

                case "Exit to menu":
                    return;
            }
        }
    }

    private static void ShowPossibleTransitions(Order order)
    {
        var possible = StateMachine<Order, OrderStatus>.GetPossibleTransitions(order);
        AnsiConsole.MarkupLine("[bold]Possible transitions (pre-conditions met):[/]");
        if (possible.Any())
        {
            foreach (var state in possible)
            {
                AnsiConsole.MarkupLine($"  [green]-> {state}[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("  [dim]None available[/]");
        }
        AnsiConsole.WriteLine();
    }

    private static void ShowDefinedTransitions(Order order)
    {
        var defined = StateMachine<Order, OrderStatus>.GetDefinedTransitions(order.Status);
        AnsiConsole.MarkupLine("[bold]Defined transitions (ignoring pre-conditions):[/]");
        if (defined.Any())
        {
            foreach (var state in defined)
            {
                var canTransition = StateMachine<Order, OrderStatus>.CanTransition(order, state);
                var status = canTransition ? "[green]available[/]" : "[red]blocked[/]";
                AnsiConsole.MarkupLine($"  -> {state} ({status})");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("  [dim]None defined[/]");
        }
        AnsiConsole.WriteLine();
    }

    private static void AttemptTransition(Order order)
    {
        var defined = StateMachine<Order, OrderStatus>.GetDefinedTransitions(order.Status).ToList();
        if (!defined.Any())
        {
            AnsiConsole.MarkupLine("[red]No transitions defined from current state.[/]");
            return;
        }

        var target = AnsiConsole.Prompt(
            new SelectionPrompt<OrderStatus>()
                .Title("Select target state:")
                .AddChoices(defined));

        var canTransition = StateMachine<Order, OrderStatus>.CanTransition(order, target);
        AnsiConsole.MarkupLine($"[dim]CanTransition check: {canTransition}[/]");

        var success = StateMachine<Order, OrderStatus>.TryTransition(order, target);
        if (success)
        {
            AnsiConsole.MarkupLine("[green]Transition successful![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Transition failed - pre-condition not met.[/]");
        }

        DisplayOrder(order);
    }

    private static void TryAnyTransition(Order order)
    {
        AnsiConsole.MarkupLine("[dim]Attempting TryTransitionAny...[/]");

        var previousState = order.Status;
        var success = StateMachine<Order, OrderStatus>.TryTransitionAny(order);
        if (success)
        {
            AnsiConsole.MarkupLine($"[green]Transitioned from {previousState} to: {order.Status}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]No valid transition found.[/]");
        }

        DisplayOrder(order);
    }

    private static void ForceTransition(Order order)
    {
        var allStates = StateMachine<Order, OrderStatus>.GetAllStates()
            .Where(s => !s.Equals(order.Status))
            .ToList();

        var target = AnsiConsole.Prompt(
            new SelectionPrompt<OrderStatus>()
                .Title("Select state to force transition to:")
                .AddChoices(allStates));

        var reason = AnsiConsole.Ask("Enter reason for force transition:", "Admin override");

        var success = StateMachine<Order, OrderStatus>.ForceTransition(order, target, reason);
        if (success)
        {
            AnsiConsole.MarkupLine("[yellow]Force transition successful![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Force transition failed - transition not defined.[/]");
        }

        DisplayOrder(order);
    }

    private static void ShowAllStates()
    {
        var allStates = StateMachine<Order, OrderStatus>.GetAllStates();
        var allTransitions = StateMachine<Order, OrderStatus>.GetAllTransitions();

        var tree = new Tree("[bold]Order State Machine[/]");

        foreach (var state in allStates)
        {
            var isFinal = StateMachine<Order, OrderStatus>.IsFinalState(state);
            var stateLabel = isFinal ? $"[red]{state} (final)[/]" : $"[green]{state}[/]";
            var stateNode = tree.AddNode(stateLabel);

            if (allTransitions.TryGetValue(state, out var targets))
            {
                foreach (var target in targets)
                {
                    stateNode.AddNode($"[dim]-> {target}[/]");
                }
            }
        }

        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
    }

    private static void DisplayOrder(Order order)
    {
        AnsiConsole.WriteLine();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("ID", order.Id.ToString());
        table.AddRow("Customer", order.CustomerName);
        table.AddRow("Items", string.Join(", ", order.Items));
        table.AddRow("Status", $"[bold]{order.Status}[/]");
        table.AddRow("Payment Received", order.PaymentReceived ? "[green]Yes[/]" : "[red]No[/]");
        table.AddRow("In Stock", order.InStock ? "[green]Yes[/]" : "[red]No[/]");
        table.AddRow("Tracking #", order.TrackingNumber ?? "N/A");
        table.AddRow("Shipped", order.ShippedDate?.ToString("g") ?? "N/A");
        table.AddRow("Delivered", order.DeliveredDate?.ToString("g") ?? "N/A");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void ShowDiagram(Order order)
    {
        var diagram = StateMachine<Order, OrderStatus>.GenerateMermaidGraph(order);
        AnsiConsole.Write(new Panel(diagram)
            .Header("[yellow]Mermaid Diagram[/]")
            .Border(BoxBorder.Rounded));
        AnsiConsole.WriteLine();
    }
}
