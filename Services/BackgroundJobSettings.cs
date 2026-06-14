namespace Bebochka.Api.Services;

/// <summary>
/// Фоновая автоматика (очистка корзин, авто-отмена заказов по таймеру).
/// Пока выключено — логика будет вынесена в отдельный worker-сервис.
/// </summary>
public static class BackgroundJobSettings
{
    /// <summary>Выполнять ли CartRetentionService (очистка корзин / авто-отмена).</summary>
    public const bool ExecuteWork = false;
}
