namespace SlimStateMachine
{
    /// <summary>
    /// Represents errors that occur during state machine operations.
    /// </summary>
    public class StateMachineException : Exception
    {
        public StateMachineException(string message) : base(message) { }
        public StateMachineException(string message, Exception innerException) : base(message, innerException) { }
    }
}