using Bebochka.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bebochka.Api.Services;

/// <summary>
/// Idempotent schema fixes applied on startup (production DB may lag behind code).
/// </summary>
public static class DbSchemaBootstrap
{
    public static async Task ApplyAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSchemaBootstrap");

        try
        {
            await EnsurePersonalDataConsentLogsTableAsync(db, logger, ct);
            await EnsureMiscExpensesNullableShipmentAsync(db, logger, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Schema bootstrap failed");
            throw;
        }
    }

    private static async Task EnsurePersonalDataConsentLogsTableAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        var exists = await ScalarIntAsync(db,
            """
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'personaldataconsentlogs'
            """, ct);

        if (exists > 0)
        {
            logger.LogInformation("Table personaldataconsentlogs already exists");
            return;
        }

        var usersTable = await ScalarStringAsync(db,
            """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'users'
            LIMIT 1
            """, ct);

        if (string.IsNullOrEmpty(usersTable))
        {
            logger.LogWarning("Table users not found, skip personaldataconsentlogs creation");
            return;
        }

        logger.LogWarning("Creating table personaldataconsentlogs");

        await db.Database.ExecuteSqlRawAsync(
            $"""
             CREATE TABLE PersonalDataConsentLogs (
               Id INT AUTO_INCREMENT PRIMARY KEY,
               UserId INT NOT NULL,
               ConsentKind VARCHAR(80) NOT NULL,
               AcceptedAtUtc DATETIME NOT NULL,
               IpAddress VARCHAR(45) NULL,
               UserAgent TEXT NULL,
               DeviceType VARCHAR(32) NULL,
               ExtraJson TEXT NULL,
               INDEX IX_PersonalDataConsentLogs_UserId (UserId),
               INDEX IX_PersonalDataConsentLogs_AcceptedAtUtc (AcceptedAtUtc),
               CONSTRAINT FK_PersonalDataConsentLogs_Users FOREIGN KEY (UserId) REFERENCES `{usersTable}` (Id) ON DELETE RESTRICT
             ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
             """, ct);

        logger.LogInformation("Table personaldataconsentlogs created");
    }

    private static async Task EnsureMiscExpensesNullableShipmentAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        var tableName = await ScalarStringAsync(db,
            """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'incomingshipmentexpenses'
            LIMIT 1
            """, ct);

        if (string.IsNullOrEmpty(tableName))
        {
            logger.LogInformation("Table incomingshipmentexpenses not found, skip nullable migration");
            return;
        }

        var isNullable = await ScalarStringAsync(db,
            $"""
             SELECT IS_NULLABLE
             FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE()
               AND TABLE_NAME = '{tableName}'
               AND COLUMN_NAME = 'IncomingShipmentId'
             LIMIT 1
             """, ct);

        if (string.Equals(isNullable, "YES", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("IncomingShipmentId already nullable on {Table}", tableName);
            return;
        }

        logger.LogWarning("Applying migration: {Table}.IncomingShipmentId -> NULL", tableName);

        var fkName = await ScalarStringAsync(db,
            $"""
             SELECT CONSTRAINT_NAME
             FROM information_schema.TABLE_CONSTRAINTS
             WHERE CONSTRAINT_SCHEMA = DATABASE()
               AND TABLE_NAME = '{tableName}'
               AND CONSTRAINT_TYPE = 'FOREIGN KEY'
             LIMIT 1
             """, ct);

        if (!string.IsNullOrEmpty(fkName))
        {
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE `{tableName}` DROP FOREIGN KEY `{fkName}`", ct);
        }

        await db.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE `{tableName}` MODIFY COLUMN IncomingShipmentId INT NULL", ct);

        var shipmentTable = await ScalarStringAsync(db,
            """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'incomingshipments'
            LIMIT 1
            """, ct);

        if (string.IsNullOrEmpty(shipmentTable))
            return;

        var fkCount = await ScalarIntAsync(db,
            $"""
             SELECT COUNT(*)
             FROM information_schema.TABLE_CONSTRAINTS
             WHERE CONSTRAINT_SCHEMA = DATABASE()
               AND TABLE_NAME = '{tableName}'
               AND CONSTRAINT_TYPE = 'FOREIGN KEY'
             """, ct);

        if (fkCount == 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                $"""
                 ALTER TABLE `{tableName}`
                 ADD CONSTRAINT FK_IncomingShipmentExpenses_IncomingShipments
                 FOREIGN KEY (IncomingShipmentId) REFERENCES `{shipmentTable}` (Id) ON DELETE SET NULL
                 """, ct);
        }

        logger.LogInformation("Migration applied: misc expenses can exist without shipment");
    }

    private static async Task<string?> ScalarStringAsync(AppDbContext db, string sql, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return result == null || result == DBNull.Value ? null : Convert.ToString(result);
    }

    private static async Task<int> ScalarIntAsync(AppDbContext db, string sql, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }
}
