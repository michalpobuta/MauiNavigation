namespace MauiNavigation.Core.Navigation;

/// <summary>
/// Represents the result of a modal navigation that returns a value.
/// Used with PresentModalForResultAsync / DismissModalWithResultAsync.
/// </summary>
public sealed class NavigationResult<T>
{
    private NavigationResult(bool succeeded, T? value)
    {
        Succeeded = succeeded;
        Value = value;
    }

    /// <summary>
    /// True if the modal returned a result (user tapped Apply/Save/etc).
    /// False if the modal was dismissed without a result (user tapped Cancel/back).
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// The result value. Only valid when Succeeded is true.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static NavigationResult<T> Success(T value) => new(true, value);

    /// <summary>
    /// Creates a cancelled/dismissed result with no value.
    /// </summary>
    public static NavigationResult<T> Cancelled() => new(false, default);

    /// <summary>
    /// Pattern matching helper. Example:
    /// <code>
    /// var result = await Navigation.PresentModalForResultAsync&lt;FilterResult&gt;(Routes.Filter);
    /// if (result.TryGetValue(out var filter))
    ///     ApplyFilter(filter);
    /// </code>
    /// </summary>
    public bool TryGetValue(out T? value)
    {
        value = Value;
        return Succeeded;
    }
}
