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
            await EnsureTelegramErrorsTableAsync(db, logger, ct);
            await EnsurePersonalDataConsentLogsTableAsync(db, logger, ct);
            await EnsureMiscExpensesNullableShipmentAsync(db, logger, ct);
            await EnsureUserChildrenAndReferralsTablesAsync(db, logger, ct);
            await EnsureUserChildrenClothingSizeWidthAsync(db, logger, ct);
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

    private static async Task EnsureTelegramErrorsTableAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        var exists = await ScalarIntAsync(db,
            """
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'telegramerrors'
            """, ct);

        if (exists > 0)
        {
            logger.LogInformation("Table telegramerrors already exists");
            return;
        }

        logger.LogWarning("Creating table telegramerrors");

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE TelegramErrors (
              Id INT AUTO_INCREMENT PRIMARY KEY,
              Message VARCHAR(2000) NOT NULL,
              Details TEXT NULL,
              ErrorType VARCHAR(100) NOT NULL,
              ProductInfo VARCHAR(1000) NULL,
              ChannelId VARCHAR(100) NULL,
              ErrorDate DATETIME NOT NULL,
              INDEX IX_TelegramErrors_ErrorDate (ErrorDate),
              INDEX IX_TelegramErrors_ErrorType (ErrorType)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, ct);

        logger.LogInformation("Table telegramerrors created");
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

    private static async Task EnsureUserChildrenAndReferralsTablesAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
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
            logger.LogWarning("Table users not found, skip userchildren/referrals creation");
            return;
        }

        await EnsureTableAsync(db, logger, "userchildren",
            $"""
             CREATE TABLE UserChildren (
               Id INT AUTO_INCREMENT PRIMARY KEY,
               UserId INT NOT NULL,
               Name VARCHAR(100) NOT NULL,
               DateOfBirth DATE NOT NULL,
               ClothingSize VARCHAR(100) NOT NULL,
               Gender VARCHAR(20) NOT NULL,
               CreatedAt DATETIME NOT NULL,
               UpdatedAt DATETIME NOT NULL,
               INDEX IX_UserChildren_UserId (UserId),
               CONSTRAINT FK_UserChildren_Users FOREIGN KEY (UserId) REFERENCES `{usersTable}` (Id) ON DELETE CASCADE
             ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
             """, ct);

        await EnsureTableAsync(db, logger, "referralcodes",
            $"""
             CREATE TABLE ReferralCodes (
               Id INT AUTO_INCREMENT PRIMARY KEY,
               UserId INT NOT NULL,
               Code VARCHAR(32) NOT NULL,
               IsActive TINYINT(1) NOT NULL DEFAULT 1,
               CreatedAt DATETIME NOT NULL,
               UNIQUE KEY uk_referralcodes_code (Code),
               UNIQUE KEY uk_referralcodes_user (UserId),
               CONSTRAINT FK_ReferralCodes_Users FOREIGN KEY (UserId) REFERENCES `{usersTable}` (Id) ON DELETE CASCADE
             ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
             """, ct);

        var ordersTable = await ScalarStringAsync(db,
            """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'orders'
            LIMIT 1
            """, ct);

        var firstOrderFk = string.IsNullOrEmpty(ordersTable)
            ? string.Empty
            : $", CONSTRAINT FK_Referrals_FirstOrders FOREIGN KEY (FirstOrderId) REFERENCES `{ordersTable}` (Id) ON DELETE SET NULL";

        await EnsureTableAsync(db, logger, "referrals",
            $"""
             CREATE TABLE Referrals (
               Id INT AUTO_INCREMENT PRIMARY KEY,
               ReferrerUserId INT NOT NULL,
               ReferredUserId INT NULL,
               ReferralCodeId INT NOT NULL,
               Status VARCHAR(30) NOT NULL DEFAULT 'Pending',
               CreatedAt DATETIME NOT NULL,
               RegisteredAt DATETIME NULL,
               FirstOrderId INT NULL,
               RewardGrantedAt DATETIME NULL,
               ReferrerRewardAmount DECIMAL(10,2) NULL,
               ReferredRewardAmount DECIMAL(10,2) NULL,
               INDEX IX_Referrals_ReferrerUserId (ReferrerUserId),
               INDEX IX_Referrals_ReferredUserId (ReferredUserId),
               INDEX IX_Referrals_ReferralCodeId (ReferralCodeId),
               INDEX IX_Referrals_Status (Status),
               CONSTRAINT FK_Referrals_ReferrerUsers FOREIGN KEY (ReferrerUserId) REFERENCES `{usersTable}` (Id) ON DELETE RESTRICT,
               CONSTRAINT FK_Referrals_ReferredUsers FOREIGN KEY (ReferredUserId) REFERENCES `{usersTable}` (Id) ON DELETE SET NULL,
               CONSTRAINT FK_Referrals_ReferralCodes FOREIGN KEY (ReferralCodeId) REFERENCES ReferralCodes (Id) ON DELETE RESTRICT
               {firstOrderFk}
             ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
             """, ct);
    }

    private static async Task EnsureUserChildrenClothingSizeWidthAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        var tableName = await ScalarStringAsync(db,
            """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'userchildren'
            LIMIT 1
            """, ct);

        if (string.IsNullOrEmpty(tableName))
            return;

        var maxLen = await ScalarIntAsync(db,
            $"""
             SELECT CHARACTER_MAXIMUM_LENGTH
             FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE()
               AND TABLE_NAME = '{tableName}'
               AND COLUMN_NAME = 'ClothingSize'
             LIMIT 1
             """, ct);

        if (maxLen >= 100)
        {
            logger.LogInformation("UserChildren.ClothingSize already wide enough ({Len})", maxLen);
            return;
        }

        logger.LogWarning("Widening {Table}.ClothingSize to VARCHAR(100)", tableName);
        await db.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE `{tableName}` MODIFY COLUMN ClothingSize VARCHAR(100) NOT NULL",
            ct);
        logger.LogInformation("UserChildren.ClothingSize widened");
    }

    private static async Task EnsureTableAsync(
        AppDbContext db,
        ILogger logger,
        string tableNameLower,
        string createSql,
        CancellationToken ct)
    {
        var exists = await ScalarIntAsync(db,
            $"""
             SELECT COUNT(*)
             FROM information_schema.TABLES
             WHERE TABLE_SCHEMA = DATABASE()
               AND LOWER(TABLE_NAME) = '{tableNameLower}'
             """, ct);

        if (exists > 0)
        {
            logger.LogInformation("Table {Table} already exists", tableNameLower);
            return;
        }

        logger.LogWarning("Creating table {Table}", tableNameLower);
        await db.Database.ExecuteSqlRawAsync(createSql, ct);
        logger.LogInformation("Table {Table} created", tableNameLower);
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
