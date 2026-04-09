namespace Bebochka.Api.Exceptions;

/// <summary>Вызвано при регистрации без обязательного согласия на обработку ПДн.</summary>
public sealed class ConsentRequiredException : Exception
{
    public ConsentRequiredException()
        : base("Требуется согласие на обработку персональных данных.")
    {
    }
}
