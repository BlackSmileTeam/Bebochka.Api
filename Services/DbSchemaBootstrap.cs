using Bebochka.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bebochka.Api.Services;

/// <summary>
/// Idempotent schema fixes applied on startup (production DB may lag behind code).
/// </summary>
public static class DbSchemaBootstrap
{
    private static volatile bool _userChildrenSchemaReady;
    private static volatile bool _referralSchemaReady;
    private static readonly SemaphoreSlim UserChildrenSchemaLock = new(1, 1);
    private static readonly SemaphoreSlim ReferralSchemaLock = new(1, 1);

    public static async Task ApplyAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSchemaBootstrap");

        var dbName = await ScalarStringAsync(db, "SELECT DATABASE()", ct);
        logger.LogInformation("DbSchemaBootstrap starting (database: {Database})", dbName ?? "?");

        try
        {
            await EnsureUserChildrenTableAsync(db, logger, ct);
            await EnsureUserAutoFilterColumnAsync(db, logger, ct);
            await EnsureUserDateOfBirthColumnAsync(db, logger, ct);
            await EnsureTelegramErrorsTableAsync(db, logger, ct);
            await EnsurePersonalDataConsentLogsTableAsync(db, logger, ct);
            await EnsureMiscExpensesNullableShipmentAsync(db, logger, ct);
            try
            {
                await EnsureReferralTablesCoreAsync(db, logger, ct);
                _referralSchemaReady = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Referral tables bootstrap failed on startup; will retry on first referral request");
            }
            await EnsureUserChildrenClothingSizeWidthAsync(db, logger, ct);
            _userChildrenSchemaReady = true;
            logger.LogInformation("DbSchemaBootstrap completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Schema bootstrap failed");
            throw;
        }
    }

    /// <summary>
    /// Idempotent guard for referral endpoints (covers prod DB lag after deploy).
    /// </summary>
    public static async Task EnsureReferralsReadyAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (_referralSchemaReady)
            return;

        await ReferralSchemaLock.WaitAsync(ct);
        try
        {
            if (_referralSchemaReady)
                return;

            await EnsureReferralTablesCoreAsync(db, logger, ct);
            _referralSchemaReady = true;
        }
        finally
        {
            ReferralSchemaLock.Release();
        }
    }

    /// <summary>
    /// Idempotent guard for profile children endpoints (covers prod DB lag after deploy).
    /// </summary>
    public static async Task EnsureUserChildrenReadyAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (_userChildrenSchemaReady)
            return;

        await UserChildrenSchemaLock.WaitAsync(ct);
        try
        {
            if (_userChildrenSchemaReady)
                return;

            await EnsureUserChildrenTableAsync(db, logger, ct);
            await EnsureUserChildrenClothingSizeWidthAsync(db, logger, ct);
            _userChildrenSchemaReady = true;
        }
        finally
        {
            UserChildrenSchemaLock.Release();
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

    private static async Task EnsureUserAutoFilterColumnAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        var tableName = await ScalarStringAsync(db,
            """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'users'
            LIMIT 1
            """, ct);

        if (string.IsNullOrEmpty(tableName))
        {
            logger.LogWarning("Table users not found, skip AutoFilterByChildren column");
            return;
        }

        var exists = await ScalarIntAsync(db,
            $"""
             SELECT COUNT(*)
             FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE()
               AND TABLE_NAME = '{tableName}'
               AND COLUMN_NAME = 'AutoFilterByChildren'
             """, ct);

        if (exists > 0)
        {
            logger.LogInformation("Column AutoFilterByChildren already exists on {Table}", tableName);
            return;
        }

        logger.LogWarning("Adding {Table}.AutoFilterByChildren", tableName);
        await db.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE `{tableName}` ADD COLUMN AutoFilterByChildren TINYINT(1) NOT NULL DEFAULT 0",
            ct);
        logger.LogInformation("Column AutoFilterByChildren added");
    }

    private static async Task EnsureUserDateOfBirthColumnAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        var tableName = await ScalarStringAsync(db,
            """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'users'
            LIMIT 1
            """, ct);

        if (string.IsNullOrEmpty(tableName))
            return;

        var exists = await ScalarIntAsync(db,
            $"""
             SELECT COUNT(*)
             FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE()
               AND TABLE_NAME = '{tableName}'
               AND COLUMN_NAME = 'DateOfBirth'
             """, ct);

        if (exists > 0)
            return;

        logger.LogWarning("Adding {Table}.DateOfBirth", tableName);
        await db.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE `{tableName}` ADD COLUMN DateOfBirth DATE NULL",
            ct);
        logger.LogInformation("Column DateOfBirth added");
    }

    private static async Task EnsureUserChildrenTableAsync(
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
            logger.LogWarning("Table users not found, skip userchildren creation");
            return;
        }

        var exists = await ScalarIntAsync(db,
            """
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'userchildren'
            """, ct);

        if (exists > 0)
        {
            logger.LogInformation("Table userchildren already exists");
            return;
        }

        logger.LogWarning("Creating table userchildren");
        await db.Database.ExecuteSqlRawAsync(
            $"""
             CREATE TABLE IF NOT EXISTS userchildren (
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

        var created = await ScalarIntAsync(db,
            """
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'userchildren'
            """, ct);

        if (created == 0)
            throw new InvalidOperationException("userchildren table was not created by schema bootstrap");

        logger.LogInformation("Table userchildren created");
    }

    private static async Task EnsureReferralTablesCoreAsync(
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
            throw new InvalidOperationException("Table users not found, cannot create referral tables");

        await EnsureTableAsync(db, logger, "referralcodes",
            $"""
             CREATE TABLE IF NOT EXISTS ReferralCodes (
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

        var referralCodesTable = await ScalarStringAsync(db,
            """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = 'referralcodes'
            LIMIT 1
            """, ct);

        if (string.IsNullOrEmpty(referralCodesTable))
            throw new InvalidOperationException("ReferralCodes table was not created by schema bootstrap");

        if (await TableExistsAsync(db, "referrals", ct))
        {
            logger.LogInformation("Table Referrals already exists");
            return;
        }

        logger.LogWarning("Creating table Referrals");
        var referralsSql = $"""
            CREATE TABLE IF NOT EXISTS Referrals (
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
              CONSTRAINT FK_Referrals_ReferralCodes FOREIGN KEY (ReferralCodeId) REFERENCES `{referralCodesTable}` (Id) ON DELETE RESTRICT
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(referralsSql, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Referrals table create with FK failed, retrying without foreign keys");
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS Referrals (
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
                  INDEX IX_Referrals_Status (Status)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
                """, ct);
        }

        if (!await TableExistsAsync(db, "referrals", ct))
            throw new InvalidOperationException("Referrals table was not created by schema bootstrap");

        logger.LogInformation("Table Referrals created");
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string tableNameLower, CancellationToken ct)
    {
        return await ScalarIntAsync(db,
            $"""
             SELECT COUNT(*)
             FROM information_schema.TABLES
             WHERE TABLE_SCHEMA = DATABASE()
               AND LOWER(TABLE_NAME) = '{tableNameLower}'
             """, ct) > 0;
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
