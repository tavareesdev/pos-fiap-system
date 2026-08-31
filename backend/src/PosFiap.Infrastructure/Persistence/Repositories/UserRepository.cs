using Microsoft.EntityFrameworkCore;
using PosFiap.Domain.Entities;
using PosFiap.Domain.Interfaces;
using PosFiap.Domain.ValueObjects;

namespace PosFiap.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PosFiapDbContext _context;

    public UserRepository(PosFiapDbContext context) => _context = context;

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _context.Users.AddAsync(user, cancellationToken);

    public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(u => u.Email == email, cancellationToken);
}
