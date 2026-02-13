namespace Microsoft.DotNet.ImageBuilder;

internal static class StandaloneLoggerFactory
{
    private static readonly ILoggerFactory s_loggerFactory =
        LoggerFactory.Create(builder => builder.AddSimpleConsole());

    public static ILogger CreateLogger<T>() => s_loggerFactory.CreateLogger<T>();

    public static ILogger CreateLogger(string categoryName) => s_loggerFactory.CreateLogger(categoryName);
}
