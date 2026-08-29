using System;

namespace Framework.Foundation.Initialization
{
    public class LifecyclePhaseException : Exception
    {
        public string PhaseName { get; }
        public Type EntityType { get; }

        public LifecyclePhaseException(string phaseName, Type entityType, Exception innerException)
            : base($"[{phaseName}] {entityType.Name}: {innerException.Message}", innerException)
        {
            PhaseName = phaseName;
            EntityType = entityType;
        }
    }
}
