namespace SliceR.Tests.Assertions;

public static class AssertionExtensions
{
    public static IAssertion<T> Should<T>(this T actual) => new Assertion<T>(actual);
}
