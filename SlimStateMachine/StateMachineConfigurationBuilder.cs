using System.Linq.Expressions;
using SlimStateMachine.Internal;

namespace SlimStateMachine;

/// <summary>
/// Fluent builder for configuring a state machine.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being managed.</typeparam>
/// <typeparam name="TEnum">The enum type representing the state.</typeparam>
public sealed class StateMachineConfigurationBuilder<TEntity, TEnum>
    where TEnum : struct, Enum
{
    private TEnum? _initialState;
    private readonly Dictionary<TEnum, List<TransitionDefinition<TEntity, TEnum>>> _transitions = new();
    private readonly Expression<Func<TEntity, TEnum>> _statusPropertyAccessor;

    // Internal constructor - creation managed by StateMachine.Configure
    internal StateMachineConfigurationBuilder(Expression<Func<TEntity, TEnum>> statusPropertyAccessor)
    {
        _statusPropertyAccessor = statusPropertyAccessor
                                  ?? throw new ArgumentNullException(nameof(statusPropertyAccessor));

        // Basic validation of the expression
        if (statusPropertyAccessor.Body is not MemberExpression { Member: System.Reflection.PropertyInfo })
            throw new ArgumentException("Expression must be a simple property accessor (e.g., x => x.Status).", nameof(statusPropertyAccessor));
    }

    /// <summary>
    /// Sets the initial state for the state machine. Required.
    /// </summary>
    /// <param name="initialState">The initial state.</param>
    public StateMachineConfigurationBuilder<TEntity, TEnum> SetInitialState(TEnum initialState)
    {
        _initialState = initialState;
        return this;
    }

    /// <summary>
    /// Defines an allowed transition between two states.
    /// </summary>
    /// <param name="fromState">The state to transition from.</param>
    /// <param name="toState">The state to transition to.</param>
    /// <param name="preCondition">Optional: A function that must return true for the transition to be allowed.</param>
    /// <param name="preConditionExpression">Optional: A string representation of the pre-condition for visualization (e.g., "Amount > 0").</param>
    /// <param name="postAction">Optional: An action to execute after the transition is successfully completed (before state change).</param>
    public StateMachineConfigurationBuilder<TEntity, TEnum> AllowTransition(
        TEnum fromState,
        TEnum toState,
        Func<TEntity, bool>? preCondition = null,
        string? preConditionExpression = null,
        Action<TEntity>? postAction = null)
    {
        var transition = new TransitionDefinition<TEntity, TEnum>(
            fromState,
            toState,
            preCondition,
            preConditionExpression,
            postAction);

        if (!_transitions.TryGetValue(fromState, out var list))
        {
            list = [];
            _transitions[fromState] = list;
        }

        // Prevent duplicate definitions for the exact same from/to pair? Or allow overriding? Let's prevent.
        if (list.Any(t => t.ToState.Equals(toState)))
            throw new InvalidOperationException($"A transition from {fromState} to {toState} is already defined.");

        list.Add(transition);
        return this;
    }

    /// <summary>
    /// Builds the configuration. Called internally by StateMachine.Configure.
    /// </summary>
    internal StateMachineConfiguration<TEntity, TEnum> Build()
    {
        if (_initialState == null)
            throw new StateMachineException("Initial state must be configured using SetInitialState.");

        // Validate that all states used in transitions are valid enum values
        var allDefinedStates = _transitions.Keys
            .Concat(_transitions.Values.SelectMany(list => list.Select(t => t.ToState)))
            .Distinct();
#if NET9_0_OR_GREATER
        var enumValues = Enum.GetValues<TEnum>();
#else
            var enumValues = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToArray();
#endif

        foreach (var state in allDefinedStates)
        {
            if (!enumValues.Contains(state))
                throw new StateMachineException($"State '{state}' used in a transition is not a valid value of enum {typeof(TEnum).Name}.");
        }
        if (!enumValues.Contains(_initialState.Value))
            throw new StateMachineException($"Initial state '{_initialState.Value}' is not a valid value of enum {typeof(TEnum).Name}.");

        // --- Final states calculation ---
        // A final state is any state that is never a 'fromState' in any transition (i.e., has no outgoing transitions)
        var statesWithOutgoing = new HashSet<TEnum>(_transitions.Keys);

        return new(
            _initialState.Value,
            _transitions,
            _statusPropertyAccessor,
            enumValues.Except(statesWithOutgoing)
        );
    }
}