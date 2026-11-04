namespace JobTracker.Application.Abstractions;

/// <summary>
/// Пользователь, от имени которого выполняется запрос. Пока приложение локальное — всегда
/// <see cref="LocalUser"/>; с авторизацией реализация будет брать id из токена/куки, остальной код не изменится.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}

/// <summary>Единственный пользователь локальной версии. Ему же при миграции достались записи, созданные до появления пользователей.</summary>
public static class LocalUser
{
    public static readonly Guid Id = new("00000000-0000-0000-0000-000000000001");
}
