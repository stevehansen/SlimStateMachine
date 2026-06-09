using System.Linq.Expressions;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SlimStateMachine.Tests")]

namespace SlimStateMachine;

/// <summary>
/// Provides static methods for defining and interacting with state machines
/// associated with an entity type and its status enum property. This is a thin,
/// resettable façade over a single registered <see cref="StateMachineDefinition{TEntity, TEnum}"/> instance.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being managed.</typeparam>
/// <typeparam name="TEnum">The enum type representing the state (must be struct, Enum).</typeparam>
public static partial class StateMachine<TEntity, TEnum>
    where TEnum : struct, Enum // Ensure TEnum is an enum
{
    // ReSharper disable StaticMemberInGenericType
    private static volatile StateMachineDefinition<TEntity, TEnum>? _current;

#if NET9_0_OR_GREATER
    private static readonly Lock _configureLock = new();
#else
    private static readonly object _configureLock = new();
#endif
    // ReSharper restore StaticMemberInGenericType

    /// <summary>
    /// Event raised after a successful state transition. Forwards subscriptions to the
    /// registered instance, so the state machine must be configured first.
    /// </summary>
    public static event Action<TransitionContext<TEntity, TEnum>>? OnTransition
    {
        add => Current.OnTransition += value;
        remove
        {
            // Tolerate unsubscribe after Reset() (e.g. during disposal): nothing to remove.
            var current = _current;
            if (current is not null)
                current.OnTransition -= value;
        }
    }

    /// <summary>
    /// Gets the registered state machine instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the state machine is not configured.</exception>
    public static StateMachineDefinition<TEntity, TEnum> Current
    {
        get
        {
            if (_current is null)
                throw new InvalidOperationException($"State machine for {typeof(TEntity).Name} with status {typeof(TEnum).Name} has not been configured. Call Configure() first.");

            return _current;
        }
    }

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
        if (_current is null)
        {
            lock (_configureLock)
            {
                if (_current is null)
                {
                    _current = StateMachineDefinition<TEntity, TEnum>.Build(statusPropertyAccessor, configureAction);

                    return; // Exit after successful configuration
                }
            }
        }

        // If we reach here, it means the key was already present before or after acquiring the lock.
        throw new InvalidOperationException($"State machine for {typeof(TEntity).Name} with status {typeof(TEnum).Name} is already configured.");
    }

    /// <summary>
    /// Registers a pre-built state machine instance as the singleton for this TEntity/TEnum combination.
    /// Like <see cref="Configure"/>, this is write-once.
    /// </summary>
    /// <param name="definition">The instance to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="definition"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the state machine for this TEntity/TEnum is already configured.</exception>
    public static void Use(StateMachineDefinition<TEntity, TEnum> definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        if (_current is null)
        {
            lock (_configureLock)
            {
                if (_current is null)
                {
                    _current = definition;

                    return;
                }
            }
        }

        throw new InvalidOperationException($"State machine for {typeof(TEntity).Name} with status {typeof(TEnum).Name} is already configured.");
    }

    /// <summary>
    /// Clears the registered instance for this TEntity/TEnum combination, allowing reconfiguration.
    /// Idempotent.
    /// </summary>
    /// <returns>True if an instance was registered and has now been cleared; false if nothing was registered.</returns>
    public static bool Reset()
    {
        lock (_configureLock)
        {
            var wasConfigured = _current is not null;
            _current = null;
            return wasConfigured;
        }
    }

    /// <summary>
    /// Gets the initial state defined for this state machine.
    /// </summary>
    public static TEnum InitialState => Current.InitialState;

    /// <summary>
    /// Checks if a transition from the entity's current state to the specified target state is possible,
    /// considering any defined pre-conditions.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="toState">The target state.</param>
    /// <returns>True if the transition is allowed, false otherwise.</returns>
    public static bool CanTransition(TEntity entity, TEnum toState) => Current.CanTransition(entity, toState);

    /// <summary>
    /// Checks if a transition from the specified source state to the target state is possible
    /// for the given entity, considering any defined pre-conditions.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="fromState">The source state.</param>
    /// <param name="toState">The target state.</param>
    /// <returns>True if the transition is allowed, false otherwise.</returns>
    public static bool CanTransition(TEntity entity, TEnum fromState, TEnum toState) => Current.CanTransition(entity, fromState, toState);

    /// <summary>
    /// Attempts to transition the entity to the specified target state from its current state.
    /// If the transition is allowed (passes pre-conditions), the entity's state property is updated,
    /// and any post-transition action is executed.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="toState">The target state.</param>
    /// <returns>True if the transition was successful, false otherwise.</returns>
    /// <exception cref="StateMachineException">Can be thrown by PostActions if they encounter errors.</exception>
    public static bool TryTransition(TEntity entity, TEnum toState) => Current.TryTransition(entity, toState);

    /// <summary>
    /// Attempts to transition the entity to the specified target state from its current state,
    /// with an optional reason for the transition.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="toState">The target state.</param>
    /// <param name="reason">Optional reason or description for the transition.</param>
    /// <param name="metadata">Optional metadata associated with the transition.</param>
    /// <returns>True if the transition was successful, false otherwise.</returns>
    public static bool TryTransition(TEntity entity, TEnum toState, string? reason, IReadOnlyDictionary<string, object>? metadata = null) => Current.TryTransition(entity, toState, reason, metadata);

    /// <summary>
    /// Attempts to transition the entity to the first valid target state from its current state,
    /// trying each of the provided possible target states in order. Returns true if any transition succeeds.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="toStates">The ordered list of possible target states to try.</param>
    /// <param name="successfulState">The state to which the transition was made, or default if none succeeded.</param>
    /// <returns>True if a transition was successful, false otherwise.</returns>
    public static bool TryTransitionAny(TEntity entity, IReadOnlyCollection<TEnum> toStates, out TEnum successfulState) => Current.TryTransitionAny(entity, toStates, out successfulState);

    /// <summary>
    /// Attempts to transition the entity to the first valid target state from its current state.
    /// Returns true if any transition succeeds.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <returns>True if a transition was successful, false otherwise.</returns>
    public static bool TryTransitionAny(TEntity entity) => Current.TryTransitionAny(entity);

    /// <summary>
    /// Gets a list of states that the entity can transition to from its current state,
    /// considering pre-conditions.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <returns>An enumerable of possible target states.</returns>
    public static IEnumerable<TEnum> GetPossibleTransitions(TEntity entity) => Current.GetPossibleTransitions(entity);

    /// <summary>
    /// Gets a list of all defined target states reachable from a given state,
    /// *without* considering pre-conditions based on a specific entity instance.
    /// </summary>
    /// <param name="fromState">The source state.</param>
    /// <returns>An enumerable of defined target states.</returns>
    public static IEnumerable<TEnum> GetDefinedTransitions(TEnum fromState) => Current.GetDefinedTransitions(fromState);

    /// <summary>
    /// Returns true if the given state is a final state (i.e., has no outgoing transitions).
    /// </summary>
    public static bool IsFinalState(TEnum state) => Current.IsFinalState(state);

    /// <summary>
    /// Returns true if the entity is currently in a final state (i.e., has no outgoing transitions).
    /// </summary>
    public static bool IsInFinalState(TEntity entity) => Current.IsInFinalState(entity);

    /// <summary>
    /// Forces a transition to the specified state, bypassing pre-conditions.
    /// The transition must still be defined in the state machine configuration.
    /// Use with caution - this is intended for administrative or recovery scenarios.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="toState">The target state.</param>
    /// <param name="reason">Optional reason for the forced transition.</param>
    /// <param name="metadata">Optional metadata associated with the transition.</param>
    /// <returns>True if the transition was successful (transition was defined), false otherwise.</returns>
    public static bool ForceTransition(TEntity entity, TEnum toState, string? reason = null, IReadOnlyDictionary<string, object>? metadata = null) => Current.ForceTransition(entity, toState, reason, metadata);

    /// <summary>
    /// Gets all states defined in the enum type.
    /// </summary>
    /// <returns>An array of all enum values.</returns>
    public static TEnum[] GetAllStates() => Current.GetAllStates();

    /// <summary>
    /// Gets all defined transitions in the state machine.
    /// </summary>
    /// <returns>A dictionary where keys are source states and values are arrays of target states.</returns>
    public static IReadOnlyDictionary<TEnum, TEnum[]> GetAllTransitions() => Current.GetAllTransitions();

    /// <summary>
    /// Generates a Mermaid graph definition string representing the configured state machine.
    /// </summary>
    /// <returns>A string suitable for rendering with Mermaid.js (e.g., in Markdown).</returns>
    public static string GenerateMermaidGraph() => Current.GenerateMermaidGraph();

    /// <summary>
    /// Generates a Mermaid graph definition string representing the configured state machine
    /// with the current state of the provided entity highlighted.
    /// </summary>
    /// <param name="entity">The entity instance whose current state should be highlighted.</param>
    /// <returns>A string suitable for rendering with Mermaid.js.</returns>
    public static string GenerateMermaidGraph(TEntity entity) => Current.GenerateMermaidGraph(entity);

    /// <summary>
    /// Generates a Mermaid graph definition string representing the configured state machine
    /// with the specified state highlighted.
    /// </summary>
    /// <param name="currentState">The state to highlight.</param>
    /// <returns>A string suitable for rendering with Mermaid.js.</returns>
    public static string GenerateMermaidGraph(TEnum currentState) => Current.GenerateMermaidGraph(currentState);
}
