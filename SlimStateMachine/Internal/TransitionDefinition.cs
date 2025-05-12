namespace SlimStateMachine.Internal;

/// <summary>
/// Defines a single transition between states. (Internal)
/// </summary>
/// <typeparam name="TEntity">The type of the entity being managed.</typeparam>
/// <typeparam name="TEnum">The enum type representing the state.</typeparam>
internal class TransitionDefinition<TEntity, TEnum>
    where TEnum : struct, Enum
{
    public TEnum FromState { get; }
    public TEnum ToState { get; }
    public Func<TEntity, bool>? PreCondition { get; }
    public string? PreConditionExpression { get; } // For graph generation
    public Action<TEntity>? PostAction { get; }

    public TransitionDefinition(
        TEnum fromState,
        TEnum toState,
        Func<TEntity, bool>? preCondition,
        string? preConditionExpression,
        Action<TEntity>? postAction)
    {
        FromState = fromState;
        ToState = toState;
        PreCondition = preCondition;
        PreConditionExpression = preConditionExpression;
        PostAction = postAction;
    }

    /// <summary>
    /// Checks if the pre-condition (if any) is met for the given entity.
    /// </summary>
    public bool IsPreConditionMet(TEntity entity)
    {
        return PreCondition?.Invoke(entity) ?? true; // No condition means it's met
    }

    /// <summary>
    /// Executes the post-action (if any) on the given entity.
    /// </summary>
    public void ExecutePostAction(TEntity entity)
    {
        PostAction?.Invoke(entity);
    }
}