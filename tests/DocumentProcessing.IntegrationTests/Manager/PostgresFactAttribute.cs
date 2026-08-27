namespace DocumentProcessing.IntegrationTests.Manager;

[AttributeUsage(
    AttributeTargets.Method)]
internal sealed class PostgresFactAttribute
    : FactAttribute
{
    #region ctor

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    PostgresManagerPersistenceTests.ConnectionStringEnvironmentVariable)))
        {
            Skip =
                $"Set {PostgresManagerPersistenceTests.ConnectionStringEnvironmentVariable} to run PostgreSQL Manager integration tests.";
        }
    }

    #endregion
}
