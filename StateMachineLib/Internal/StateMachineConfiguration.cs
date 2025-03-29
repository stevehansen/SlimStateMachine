using System.Linq.Expressions;
using System.Reflection;

namespace StateMachineLib.Internal
{
    /// <summary>
    /// Holds the complete configuration for a specific state machine type. (Internal)
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity being managed.</typeparam>
    /// <typeparam name="TEnum">The enum type representing the state.</typeparam>
    internal class StateMachineConfiguration<TEntity, TEnum>
        where TEnum : struct, Enum
    {
        public TEnum InitialState { get; }
        // Key: FromState, Value: List of transitions originating from FromState
        public IReadOnlyDictionary<TEnum, List<TransitionDefinition<TEntity, TEnum>>> Transitions { get; }

        private readonly Func<TEntity, TEnum> _stateGetter;
        private readonly Action<TEntity, TEnum> _stateSetter;
        private readonly string _statusPropertyName;

        internal StateMachineConfiguration(
            TEnum initialState,
            Dictionary<TEnum, List<TransitionDefinition<TEntity, TEnum>>> transitions,
            Expression<Func<TEntity, TEnum>> statusPropertyAccessor)
        {
            InitialState = initialState;
            Transitions = transitions; // Consider making a ReadOnlyDict copy if builder mutates

            // Compile getter
            _stateGetter = statusPropertyAccessor.Compile();

            // Compile setter
            if (!(statusPropertyAccessor.Body is MemberExpression memberExpression))
            {
                throw new ArgumentException("The status property accessor must be a simple member expression (e.g., entity => entity.Status).", nameof(statusPropertyAccessor));
            }

            _statusPropertyName = memberExpression.Member.Name;
            var propertyInfo = memberExpression.Member as PropertyInfo;
            if (propertyInfo == null || !propertyInfo.CanWrite)
            {
                throw new ArgumentException($"The property '{_statusPropertyName}' must be writable.", nameof(statusPropertyAccessor));
            }

            var entityParam = statusPropertyAccessor.Parameters[0]; // entity
            var valueParam = Expression.Parameter(typeof(TEnum), "value"); // value
            var assign = Expression.Assign(memberExpression, valueParam);
            var setterLambda = Expression.Lambda<Action<TEntity, TEnum>>(assign, entityParam, valueParam);
            _stateSetter = setterLambda.Compile();
        }

        public TEnum GetCurrentState(TEntity entity) => _stateGetter(entity);

        public void SetState(TEntity entity, TEnum newState) => _stateSetter(entity, newState);

        public string GetStatusPropertyName() => _statusPropertyName;

        public TransitionDefinition<TEntity, TEnum>? FindTransition(TEnum from, TEnum to)
        {
            if (Transitions.TryGetValue(from, out var possibleTransitions))
            {
                // Usually only one transition between two specific states, but check just in case
                return possibleTransitions.FirstOrDefault(t => t.ToState.Equals(to));
            }
            return null;
        }

        public IEnumerable<TransitionDefinition<TEntity, TEnum>> GetTransitionsFrom(TEnum from)
        {
            if (Transitions.TryGetValue(from, out var possibleTransitions))
            {
                return possibleTransitions;
            }
            return Enumerable.Empty<TransitionDefinition<TEntity, TEnum>>();
        }
    }
}