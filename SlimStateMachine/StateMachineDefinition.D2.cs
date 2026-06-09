using System.Text;

namespace SlimStateMachine;

public sealed partial class StateMachineDefinition<TEntity, TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Generates a D2 diagram definition string representing the configured state machine.
    /// </summary>
    /// <param name="includeStyles">When true, includes styling for the diagram. Default is true.</param>
    /// <returns>A string suitable for rendering with D2.</returns>
    public string GenerateD2Graph(bool includeStyles = true)
    {
        return GenerateD2Graph(entity: default, currentState: null, includeStyles);
    }

    /// <summary>
    /// Generates a D2 diagram definition string representing the configured state machine
    /// with the current state highlighted.
    /// </summary>
    /// <param name="entity">The entity instance whose current state should be highlighted.</param>
    /// <param name="includeStyles">When true, includes styling for the diagram. Default is true.</param>
    /// <returns>A string suitable for rendering with D2.</returns>
    public string GenerateD2Graph(TEntity entity, bool includeStyles = true)
    {
        return GenerateD2Graph(entity: entity, null, includeStyles);
    }

    /// <summary>
    /// Generates a D2 diagram definition string representing the configured state machine
    /// with the specified state highlighted.
    /// </summary>
    /// <param name="currentState">The state to highlight.</param>
    /// <param name="includeStyles">When true, includes styling for the diagram. Default is true.</param>
    /// <returns>A string suitable for rendering with D2.</returns>
    public string GenerateD2Graph(TEnum currentState, bool includeStyles = true)
    {
        return GenerateD2Graph(entity: default, currentState, includeStyles);
    }

    private string GenerateD2Graph(TEntity? entity, TEnum? currentState, bool includeStyles)
    {
        var config = _config;
        var sb = new StringBuilder();

        // If entity is provided but currentState is default, get the current state from the entity
        if (entity is not null && currentState is null)
            currentState = config.GetCurrentState(entity);

        // Track if we're highlighting a specific state
        var isHighlightingState = currentState is not null;

        // Add title and direction
        sb.AppendLine($"# State Machine: {typeof(TEntity).Name} - {typeof(TEnum).Name}");
        if (isHighlightingState)
            sb.AppendLine($"# Current State: {currentState}");
        sb.AppendLine("direction: down");
        sb.AppendLine();

        // Add styling if requested
        if (includeStyles)
        {
            sb.AppendLine("# Styles");
            sb.AppendLine("style {");
            sb.AppendLine("  fill: honeydew");
            sb.AppendLine("  stroke: limegreen");
            sb.AppendLine("  stroke-width: 2");
            sb.AppendLine("  font-size: 14");
            sb.AppendLine("  shadow: true");
            sb.AppendLine("}");
            sb.AppendLine();

            // Style for the Start node
            sb.AppendLine("Start: {");
            sb.AppendLine("  shape: circle");
            sb.AppendLine("  style.fill: lightgreen");
            sb.AppendLine("  style.stroke: green");
            sb.AppendLine("  width: 40");
            sb.AppendLine("  height: 40");
            sb.AppendLine("}");
            sb.AppendLine();

            // Add highlighting style for current state if specified
            if (isHighlightingState)
            {
                sb.AppendLine($"{currentState}: {{");
                sb.AppendLine("  style.fill: lightyellow");
                sb.AppendLine("  style.stroke: orange");
                sb.AppendLine("  style.stroke-width: 3");
                sb.AppendLine("  style.shadow: true");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        // Add initial state marker
        sb.AppendLine($"Start -> {config.InitialState}");
        sb.AppendLine();

        // Add all defined transitions
        var allTransitions = config.Transitions.Values.SelectMany(list => list).ToArray();

        if (!allTransitions.Any())
        {
            // If only initial state defined, ensure it shows up
            sb.AppendLine($"{config.InitialState}");
        }
        else
        {
            sb.AppendLine("# Transitions");
            foreach (var transition in allTransitions)
            {
                if (!string.IsNullOrWhiteSpace(transition.PreConditionExpression))
                {
                    // D2 uses a different label syntax than Mermaid
                    sb.AppendLine($"{transition.FromState} -> {transition.ToState}: {transition.PreConditionExpression}");
                }
                else
                {
                    sb.AppendLine($"{transition.FromState} -> {transition.ToState}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a diagram definition string representing the configured state machine
    /// in the specified format.
    /// </summary>
    /// <param name="diagramType">The type of diagram to generate.</param>
    /// <returns>A string suitable for rendering with the specified diagramming tool.</returns>
    public string GenerateDiagram(StateMachine<TEntity, TEnum>.DiagramType diagramType)
    {
        return diagramType switch
        {
            StateMachine<TEntity, TEnum>.DiagramType.Mermaid => GenerateMermaidGraph(),
            StateMachine<TEntity, TEnum>.DiagramType.D2 => GenerateD2Graph(),
            _ => throw new ArgumentException($"Unsupported diagram type: {diagramType}")
        };
    }

    /// <summary>
    /// Generates a diagram definition string representing the configured state machine
    /// in the specified format, with the specified entity's current state highlighted.
    /// </summary>
    /// <param name="diagramType">The type of diagram to generate.</param>
    /// <param name="entity">The entity whose current state should be highlighted.</param>
    /// <returns>A string suitable for rendering with the specified diagramming tool.</returns>
    public string GenerateDiagram(StateMachine<TEntity, TEnum>.DiagramType diagramType, TEntity entity)
    {
        return diagramType switch
        {
            StateMachine<TEntity, TEnum>.DiagramType.Mermaid => GenerateMermaidGraph(entity),
            StateMachine<TEntity, TEnum>.DiagramType.D2 => GenerateD2Graph(entity),
            _ => throw new ArgumentException($"Unsupported diagram type: {diagramType}")
        };
    }

    /// <summary>
    /// Generates a diagram definition string representing the configured state machine
    /// in the specified format, with the specified state highlighted.
    /// </summary>
    /// <param name="diagramType">The type of diagram to generate.</param>
    /// <param name="currentState">The state to highlight.</param>
    /// <returns>A string suitable for rendering with the specified diagramming tool.</returns>
    public string GenerateDiagram(StateMachine<TEntity, TEnum>.DiagramType diagramType, TEnum currentState)
    {
        return diagramType switch
        {
            StateMachine<TEntity, TEnum>.DiagramType.Mermaid => GenerateMermaidGraph(currentState),
            StateMachine<TEntity, TEnum>.DiagramType.D2 => GenerateD2Graph(currentState),
            _ => throw new ArgumentException($"Unsupported diagram type: {diagramType}")
        };
    }
}
