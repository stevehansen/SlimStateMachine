using System.Text;

namespace SlimStateMachine;

public static partial class StateMachine<TEntity, TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Generates a D2 diagram definition string representing the configured state machine.
    /// </summary>
    /// <param name="includeStyles">When true, includes styling for the diagram. Default is true.</param>
    /// <returns>A string suitable for rendering with D2.</returns>
    public static string GenerateD2Graph(bool includeStyles = true)
    {
        var config = GetConfiguration();
        var sb = new StringBuilder();

        // Add title and direction
        sb.AppendLine($"# State Machine: {typeof(TEntity).Name} - {typeof(TEnum).Name}");
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
    /// Enum to specify the diagram type to generate.
    /// </summary>
    public enum DiagramType
    {
        Mermaid,
        D2,
    }

    /// <summary>
    /// Generates a diagram definition string representing the configured state machine
    /// in the specified format.
    /// </summary>
    /// <param name="diagramType">The type of diagram to generate.</param>
    /// <returns>A string suitable for rendering with the specified diagramming tool.</returns>
    public static string GenerateDiagram(DiagramType diagramType)
    {
        return diagramType switch
        {
            DiagramType.Mermaid => GenerateMermaidGraph(),
            DiagramType.D2 => GenerateD2Graph(),
            _ => throw new ArgumentException($"Unsupported diagram type: {diagramType}")
        };
    }
}