using Microsoft.Data.SqlClient;

namespace FeatureFlags.Api.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Creates the SQL Server database named in the connection string when it does not exist.
    /// </summary>
    public static async Task EnsureSqlServerDatabaseExistsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return;
        }

        ValidateDatabaseName(databaseName);

        builder.InitialCatalog = "master";

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             IF DB_ID(N'{EscapeLiteral(databaseName)}') IS NULL
             BEGIN
                 CREATE DATABASE [{EscapeIdentifier(databaseName)}];
             END
             """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateDatabaseName(string databaseName)
    {
        foreach (var ch in databaseName)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Database name '{databaseName}' contains invalid characters. Use letters, digits, '_' or '-'.");
        }
    }

    private static string EscapeLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string EscapeIdentifier(string value) => value.Replace("]", "]]", StringComparison.Ordinal);
}
