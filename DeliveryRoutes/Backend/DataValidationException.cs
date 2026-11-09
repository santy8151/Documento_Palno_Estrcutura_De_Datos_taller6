namespace Backend;

/// <summary>Raised when an input file or argument is invalid. Carries every problem found.</summary>
public class DataValidationException : Exception
{
    public DataValidationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public DataValidationException(string error)
        : this(new[] { error })
    {
    }

    public IReadOnlyList<string> Errors { get; }
}
