namespace SlimStateMachine;

/// <summary>
/// Provides context information about a state transition.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being managed.</typeparam>
/// <typeparam name="TEnum">The enum type representing the state.</typeparam>
public sealed class TransitionContext<TEntity, TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// The entity that is transitioning.
    /// </summary>
    public TEntity Entity { get; }

    /// <summary>
    /// The state the entity is transitioning from.
    /// </summary>
    public TEnum FromState { get; }

    /// <summary>
    /// The state the entity is transitioning to.
    /// </summary>
    public TEnum ToState { get; }

    /// <summary>
    /// Optional reason or description for the transition.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Optional metadata associated with the transition.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; }

    /// <summary>
    /// Whether this transition was forced (bypassing pre-conditions).
    /// </summary>
    public bool WasForced { get; }

    internal TransitionContext(
        TEntity entity,
        TEnum fromState,
        TEnum toState,
        string? reason = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        bool wasForced = false)
    {
        Entity = entity;
        FromState = fromState;
        ToState = toState;
        Reason = reason;
        Metadata = metadata;
        WasForced = wasForced;
    }
}
