namespace ShipExtract.UI.Helpers;

/// <summary>Utility methods for producing user-friendly exception messages.</summary>
public static class ExceptionHelper
{
    /// <summary>
    /// Produces a concise, user-friendly one-line summary of an exception,
    /// unwrapping <see cref="AggregateException"/> and <see cref="System.Reflection.TargetInvocationException"/>.
    /// </summary>
    public static string GetUserMessage(Exception ex)
    {
        var inner = ex;
        while (inner is AggregateException or System.Reflection.TargetInvocationException
               && inner.InnerException is not null)
            inner = inner.InnerException;
        return inner.Message;
    }
}
