namespace MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

/// <summary>
/// To implement ArgumentNullException.ThrowIfNull for .NET Standard.
/// </summary>
internal sealed class ArgumentNullException : System.ArgumentNullException
{
    public ArgumentNullException() : base()
    {
    }
    
    public ArgumentNullException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ArgumentNullException(string paramName) : base(paramName)
    {
    }

    public ArgumentNullException(string message, string paramName) : base(message, paramName)
    {
    }

    public static void ThrowIfNull(object argument, string paramName = null)
    {
        if (argument is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}