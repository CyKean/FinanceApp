namespace FinanceApp.Infrastructure.Persistence;

using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class LocalAccountStore : ILocalAccountStore
{
    private readonly FinanceAppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<LocalAccountStore> _logger;

    public LocalAccountStore(
        FinanceAppDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<LocalAccountStore> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<LocalAccountDto?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(email);
        if (normalized.Length == 0)
            return null;

        var user = await _context.LocalUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);

        return user is null ? null : ToDto(user);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        await _context.LocalUsers.CountAsync(cancellationToken);

    public async Task<LocalAccountDto> CreateAsync(string email, string password, CancellationToken cancellationToken = default) =>
        await CreateCoreAsync(email, password, Guid.NewGuid(), cancellationToken);

    public Task<LocalAccountDto> CreateAsync(string email, string password, Guid id, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(email, password, id, cancellationToken);

    private async Task<LocalAccountDto> CreateCoreAsync(string email, string password, Guid id, CancellationToken cancellationToken)
    {
        var normalized = Normalize(email);
        if (normalized.Length == 0)
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password is required.", nameof(password));
        if (id == Guid.Empty)
            throw new ArgumentException("Id is required.", nameof(id));

        if (await _context.LocalUsers.AnyAsync(u => u.Email == normalized, cancellationToken))
            throw new InvalidOperationException($"An account already exists for {normalized}.");

        var user = new LocalUser
        {
            Id = id,
            Email = normalized,
            PasswordHash = _passwordHasher.Hash(password),
            CreatedAt = DateTime.UtcNow
        };

        _context.LocalUsers.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created local account {UserId} for {Email}", user.Id, user.Email);
        return ToDto(user);
    }

    public async Task ReassignIdAsync(Guid currentId, Guid newId, CancellationToken cancellationToken = default)
    {
        if (currentId == newId)
            return;

        var user = await _context.LocalUsers.FirstOrDefaultAsync(u => u.Id == currentId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("No local account with id {CurrentId} to reassign to {NewId}", currentId, newId);
            return;
        }

        user.Id = newId;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Reassigned local account {CurrentId} to cloud identity {NewId}", currentId, newId);
    }

    public async Task RecordSignInAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.LocalUsers.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return;

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static LocalAccountDto ToDto(LocalUser user) => new(user.Id, user.Email, user.PasswordHash, user.CreatedAt, user.LastLoginAt);
}