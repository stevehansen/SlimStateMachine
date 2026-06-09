namespace SlimStateMachine;

public static partial class StateMachine<TEntity, TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Generates a D2 diagram definition string representing the configured state machine.
    /// </summary>
    /// <param name="includeStyles">When true, includes styling for the diagram. Default is true.</param>
    /// <returns>A string suitable for rendering with D2.</returns>
    public static string GenerateD2Graph(bool includeStyles = true) => Current.GenerateD2Graph(includeStyles);

    /// <summary>
    /// Generates a D2 diagram definition string representing the configured state machine
    /// with the current state highlighted.
    /// </summary>
    /// <param name="entity">The entity instance whose current state should be highlighted.</param>
    /// <param name="includeStyles">When true, includes styling for the diagram. Default is true.</param>
    /// <returns>A string suitable for rendering with D2.</returns>
    public static string GenerateD2Graph(TEntity entity, bool includeStyles = true) => Current.GenerateD2Graph(entity, includeStyles);

    /// <summary>
    /// Generates a D2 diagram definition string representing the configured state machine
    /// with the specified state highlighted.
    /// </summary>
    /// <param name="currentState">The state to highlight.</param>
    /// <param name="includeStyles">When true, includes styling for the diagram. Default is true.</param>
    /// <returns>A string suitable for rendering with D2.</returns>
    public static string GenerateD2Graph(TEnum currentState, bool includeStyles = true) => Current.GenerateD2Graph(currentState, includeStyles);

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
    public static string GenerateDiagram(DiagramType diagramType) => Current.GenerateDiagram(diagramType);

    /// <summary>
    /// Generates a diagram definition string representing the configured state machine
    /// in the specified format, with the specified entity's current state highlighted.
    /// </summary>
    /// <param name="diagramType">The type of diagram to generate.</param>
    /// <param name="entity">The entity whose current state should be highlighted.</param>
    /// <returns>A string suitable for rendering with the specified diagramming tool.</returns>
    public static string GenerateDiagram(DiagramType diagramType, TEntity entity) => Current.GenerateDiagram(diagramType, entity);

    /// <summary>
    /// Generates a diagram definition string representing the configured state machine
    /// in the specified format, with the specified state highlighted.
    /// </summary>
    /// <param name="diagramType">The type of diagram to generate.</param>
    /// <param name="currentState">The state to highlight.</param>
    /// <returns>A string suitable for rendering with the specified diagramming tool.</returns>
    public static string GenerateDiagram(DiagramType diagramType, TEnum currentState) => Current.GenerateDiagram(diagramType, currentState);
}
