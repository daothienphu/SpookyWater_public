namespace SpookyWater.Editor
{
    public enum SetupScope
    {
        Project,
        Scene
    }

    public enum SetupState
    {
        Ok,
        Info,
        Warning,
        Error
    }

    public readonly struct SetupStatus
    {
        public readonly SetupState State;
        public readonly string Message;

        public SetupStatus(SetupState state, string message)
        {
            State = state;
            Message = message;
        }

        public bool IsOk => State == SetupState.Ok || State == SetupState.Info;

        public static SetupStatus Ok(string message) => new SetupStatus(SetupState.Ok, message);

        public static SetupStatus Info(string message) => new SetupStatus(SetupState.Info, message);

        public static SetupStatus Warning(string message) => new SetupStatus(SetupState.Warning, message);

        public static SetupStatus Error(string message) => new SetupStatus(SetupState.Error, message);
    }

    public sealed class SetupIssue
    {
        public string Id;
        public string Title;
        public string Message;
        public SetupScope Scope;
        public SetupState State;
        public bool CanFix;
    }

    public abstract class ProjectRequirement
    {
        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string What { get; }
        public abstract string Why { get; }
        public abstract SetupScope Scope { get; }

        public virtual bool CanFix => true;

        public virtual string ManualHelp => null;

        public virtual string SecondaryLabel => null;

        public abstract SetupStatus Check();

        public virtual bool Fix() => false;

        public virtual void Secondary()
        {
        }
    }
}
