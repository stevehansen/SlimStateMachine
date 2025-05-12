using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using SlimStateMachine.Internal;

[assembly: InternalsVisibleTo("SlimStateMachine.Tests")]

namespace SlimStateMachine;

/// <summary>
/// Provides static methods for defining and interacting with state machines
/// associated with an entity type and its status enum property.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being managed.</typeparam>
/// <typeparam name="TEnum">The enum type representing the state (must be struct, Enum).</typeparam>
public static partial class StateMachine<TEntity, TEnum>
    where TEnum : struct, Enum // Ensure TEnum is an enum
{
    private static StateMachineConfiguration<TEntity, TEnum>? _config;

    // ReSharper disable StaticMemberInGenericType
#if NET9_0_OR_GREATER
    private static readonly Lock _configureLock = new();
#else
    private static readonly object _configureLock = new();
#endif
    // ReSharper restore StaticMemberInGenericType

    /// <summary>
    /// Configures the state machine for the given TEntity and TEnum types.
    /// This method should only be called once per TEntity/TEnum combination, typically at application startup.
    /// </summary>
    /// <param name="statusPropertyAccessor">An expression identifying the status property (e.g., `invoice => invoice.Status`).</param>
    /// <param name="configureAction">An action that uses the builder to define states and transitions.</param>
    /// <exception cref="ArgumentNullException">Thrown if parameters are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the state machine for this TEntity/TEnum is already configured.</exception>
    /// <exception cref="StateMachineException">Thrown if configuration is invalid (e.g., missing initial state).</exception>
    public static void Configure(
        Expression<Func<TEntity, TEnum>> statusPropertyAccessor,
        Action<StateMachineConfigurationBuilder<TEntity, TEnum>> configureAction)
    {
        if (statusPropertyAccessor == null) throw new ArgumentNullException(nameof(statusPropertyAccessor));
        if (configureAction == null) throw new ArgumentNullException(nameof(configureAction));

        // Double-checked locking pattern for thread-safe initialization
        if (_config is null)
        {
            lock (_configureLock)
            {
                if (_config is null)
                {
                    var builder = new StateMachineConfigurationBuilder<TEntity, TEnum>(statusPropertyAccessor);
                    configureAction(builder);
                    _config = builder.Build(); // Build validates required settings like initial state

                    return; // Exit after successful configuration
                }
            }
        }

        // If we reach here, it means the key was already present before or after acquiring the lock.
        throw new InvalidOperationException($"State machine for {typeof(TEntity).Name} with status {typeof(TEnum).Name} is already configured.");
    }

    /// <summary>
    /// Gets the configured state machine details. Internal use primarily.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the state machine is not configured.</exception>
    private static StateMachineConfiguration<TEntity, TEnum> GetConfiguration()
    {
        if (_config is null)
            throw new InvalidOperationException($"State machine for {typeof(TEntity).Name} with status {typeof(TEnum).Name} has not been configured. Call Configure() first.");

        return _config;
    }

    /// <summary>
    /// Gets the initial state defined for this state machine.
    /// </summary>
    public static TEnum InitialState => GetConfiguration().InitialState;

    /// <summary>
    /// Checks if a transition from the entity's current state to the specified target state is possible,
    /// considering any defined pre-conditions.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="toState">The target state.</param>
    /// <returns>True if the transition is allowed, false otherwise.</returns>
    public static bool CanTransition(TEntity entity, TEnum toState)
    {
        var config = GetConfiguration();
        var currentState = config.GetCurrentState(entity);
        return CanTransition(entity, currentState, toState);
    }

    /// <summary>
    /// Checks if a transition from the specified source state to the target state is possible
    /// for the given entity, considering any defined pre-conditions.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="fromState">The source state.</param>
    /// <param name="toState">The target state.</param>
    /// <returns>True if the transition is allowed, false otherwise.</returns>
    public static bool CanTransition(TEntity entity, TEnum fromState, TEnum toState)
    {
        var config = GetConfiguration();
        var transition = config.FindTransition(fromState, toState);

        if (transition == null)
        {
            return false; // No transition defined
        }

        return transition.IsPreConditionMet(entity); // Check pre-condition
    }

    /// <summary>
    /// Attempts to transition the entity to the specified target state from its current state.
    /// If the transition is allowed (passes pre-conditions), the entity's state property is updated,
    /// and any post-transition action is executed.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="toState">The target state.</param>
    /// <returns>True if the transition was successful, false otherwise.</returns>
    /// <exception cref="StateMachineException">Can be thrown by PostActions if they encounter errors.</exception>
    public static bool TryTransition(TEntity entity, TEnum toState)
    {
        var config = GetConfiguration();
        var currentState = config.GetCurrentState(entity);
        return TryTransition(config, entity, currentState, toState);
    }

    private static bool TryTransition(StateMachineConfiguration<TEntity, TEnum> config, TEntity entity, TEnum currentState, TEnum toState)
    {
        var transition = config.FindTransition(currentState, toState);

        if (transition == null || !transition.IsPreConditionMet(entity))
        {
            return false; // Transition not defined or pre-condition failed
        }

        // Pre-conditions met, execute post-action FIRST, then change state
        try
        {
            transition.ExecutePostAction(entity);
            config.SetState(entity, toState); // Update the entity's state property
            return true;
        }
        catch (Exception ex)
        {
            // Wrap exceptions from PostAction for clarity
            throw new StateMachineException($"Error executing post-action for transition from {currentState} to {toState}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attempts to transition the entity to the first valid target state from its current state,
    /// trying each of the provided possible target states in order. Returns true if any transition succeeds.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="toStates">The ordered list of possible target states to try.</param>
    /// <param name="successfulState">The state to which the transition was made, or default if none succeeded.</param>
    /// <returns>True if a transition was successful, false otherwise.</returns>
    public static bool TryTransitionAny(TEntity entity, IReadOnlyCollection<TEnum> toStates, out TEnum successfulState)
    {
        var config = GetConfiguration();
        var currentState = config.GetCurrentState(entity);

        foreach (var toState in toStates)
        {
            if (TryTransition(config, entity, currentState, toState))
            {
                successfulState = toState;
                return true;
            }
        }

        successfulState = default;
        return false;
    }

    /// <summary>
    /// Attempts to transition the entity to the first valid target state from its current state.
    /// Returns true if any transition succeeds.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <returns>True if a transition was successful, false otherwise.</returns>
    public static bool TryTransitionAny(TEntity entity)
    {
        var config = GetConfiguration();
        var currentState = config.GetCurrentState(entity);

        return config.GetTransitionsFrom(currentState).Any(transition => TryTransition(config, entity, currentState, transition.ToState));
    }

    /// <summary>
    /// Gets a list of states that the entity can transition to from its current state,
    /// considering pre-conditions.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <returns>An enumerable of possible target states.</returns>
    public static IEnumerable<TEnum> GetPossibleTransitions(TEntity entity)
    {
        var config = GetConfiguration();
        var currentState = config.GetCurrentState(entity);

        return config.GetTransitionsFrom(currentState)
            .Where(transition => transition.IsPreConditionMet(entity))
            .Select(transition => transition.ToState);
    }

    /// <summary>
    /// Gets a list of all defined target states reachable from a given state,
    /// *without* considering pre-conditions based on a specific entity instance.
    /// </summary>
    /// <param name="fromState">The source state.</param>
    /// <returns>An enumerable of defined target states.</returns>
    public static IEnumerable<TEnum> GetDefinedTransitions(TEnum fromState)
    {
        var config = GetConfiguration();
        return config.GetTransitionsFrom(fromState)
            .Select(transition => transition.ToState);
    }

    /// <summary>
    /// Returns true if the given state is a final state (i.e., has no outgoing transitions).
    /// </summary>
    public static bool IsFinalState(TEnum state)
    {
        var config = GetConfiguration();
        return config.FinalStates.Contains(state);
    }

    /// <summary>
    /// Returns true if the entity is currently in a final state (i.e., has no outgoing transitions).
    /// </summary>
    public static bool IsInFinalState(TEntity entity)
    {
        var config = GetConfiguration();
        var currentState = config.GetCurrentState(entity);
        return config.FinalStates.Contains(currentState);
    }

    /// <summary>
    /// Generates a Mermaid graph definition string representing the configured state machine.
    /// </summary>
    /// <returns>A string suitable for rendering with Mermaid.js (e.g., in Markdown).</returns>
    public static string GenerateMermaidGraph()
    {
        return GenerateMermaidGraph(entity: default, currentState: null);
    }

    /// <summary>
    /// Generates a Mermaid graph definition string representing the configured state machine
    /// with the current state of the provided entity highlighted.
    /// </summary>
    /// <param name="entity">The entity instance whose current state should be highlighted.</param>
    /// <returns>A string suitable for rendering with Mermaid.js.</returns>
    public static string GenerateMermaidGraph(TEntity entity)
    {
        return GenerateMermaidGraph(entity: entity, null);
    }

    /// <summary>
    /// Generates a Mermaid graph definition string representing the configured state machine
    /// with the specified state highlighted.
    /// </summary>
    /// <param name="currentState">The state to highlight.</param>
    /// <returns>A string suitable for rendering with Mermaid.js.</returns>
    public static string GenerateMermaidGraph(TEnum currentState)
    {
        return GenerateMermaidGraph(entity: default, currentState);
    }

    private static string GenerateMermaidGraph(TEntity? entity, TEnum? currentState)
    {
        var config = GetConfiguration();
        var sb = new StringBuilder();

        // If entity is provided but currentState is default, get the current state from the entity
        if (entity is not null && currentState is null)
            currentState = config.GetCurrentState(entity);

        sb.AppendLine("graph TD"); // Top-Down graph

        // Add style for current state if specified
        if (currentState is not null)
        {
            sb.AppendLine("    %% Styling for current state");
            sb.AppendLine($"    style {currentState} fill:#ffffaa,stroke:#ffaa00,stroke-width:3px");
        }

        // Add initial state marker
        sb.AppendLine($"    Start((\u26aa)) --> {config.InitialState}");

        // Add all defined transitions
        var allTransitions = config.Transitions.Values.SelectMany(list => list).ToArray();

        if (!allTransitions.Any())
        {
            // If only initial state defined, ensure it shows up
            sb.AppendLine($"    {config.InitialState}");
        }
        else
        {
            foreach (var transition in allTransitions)
            {
                if (!string.IsNullOrWhiteSpace(transition.PreConditionExpression))
                {
                    // Escape quotes in the condition expression for the label
                    var safeCondition = transition.PreConditionExpression.Replace("\"", "#quot;");
                    sb.AppendLine($"    {transition.FromState} -- \"{safeCondition}\" --> {transition.ToState}");
                }
                else
                {
                    sb.AppendLine($"    {transition.FromState} --> {transition.ToState}");
                }
            }
        }

        // Optional: Add states that might only be initial or final states and have no explicit transitions listed
        //var allStatesMentioned = new HashSet<TEnum>(config.Transitions.Keys);
        //allStatesMentioned.UnionWith(allTransitions.Select(t => t.ToState));
        //allStatesMentioned.Add(config.InitialState);

        // This part is usually implicitly handled by Mermaid, but can be explicit if needed
        // foreach(var state in allStatesMentioned) {
        //     if (!allTransitions.Any(t => t.FromState.Equals(state) || t.ToState.Equals(state))) {
        //         sb.AppendLine($"    {state}"); // Ensure lone states are declared
        //     }
        // }

        return sb.ToString();
    }

    /// <summary>
    /// Clears the configuration for this specific StateMachine&lt;TEntity, TEnum>.
    /// Primarily useful for testing purposes to allow reconfiguration.
    /// Use with caution in production environments.
    /// </summary>
    internal static void ClearConfiguration_TestOnly()
    {
        _config = null; // Reset the configuration
    }
}