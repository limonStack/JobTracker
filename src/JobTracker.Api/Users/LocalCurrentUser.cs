using JobTracker.Application.Abstractions;

namespace JobTracker.Api.Users;

/// <summary>
/// Пока авторизации нет: все запросы — от единственного локального пользователя.
/// С авторизацией здесь появится реализация, читающая id из HttpContext.User.
/// </summary>
internal sealed class LocalCurrentUser : ICurrentUser
{
    public Guid UserId => LocalUser.Id;
}
