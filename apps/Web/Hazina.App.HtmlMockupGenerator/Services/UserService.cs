using HtmlMockupGenerator.Data;
using HtmlMockupGenerator.Models;
using Microsoft.EntityFrameworkCore;

namespace HtmlMockupGenerator.Services;

public class UserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> GetOrCreateUserAsync(string id, string email, string name, string? picture = null)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        
        if (user == null)
        {
            user = new User
            {
                Id = id,
                Email = email,
                Name = name,
                Picture = picture,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.Name = name;
            user.Picture = picture;
        }

        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> CanUserGenerateAsync(string userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        return user?.CanGenerateToday() ?? false;
    }

    public async Task<bool> IncrementUserGenerationAsync(string userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return false;

        if (!user.CanGenerateToday()) return false;

        user.IncrementGenerationCount();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetRemainingGenerationsAsync(string userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        return user?.RemainingGenerationsToday() ?? 0;
    }

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
    }
} 