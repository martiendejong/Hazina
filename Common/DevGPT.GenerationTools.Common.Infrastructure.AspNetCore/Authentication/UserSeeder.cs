using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructure.AspNetCore.Authentication
{
    /// <summary>
    /// Service for seeding default users at application startup.
    /// </summary>
    public class UserSeeder
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AuthOptions _authOptions;
        private readonly ILogger<UserSeeder>? _logger;

        public UserSeeder(
            UserManager<IdentityUser> userManager,
            AuthOptions authOptions,
            ILogger<UserSeeder>? logger = null)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
            _logger = logger;
        }

        /// <summary>
        /// Seeds admin and regular users based on AuthOptions configuration.
        /// </summary>
        /// <param name="onAdminCreated">Callback invoked when admin user is created. Parameters: (IdentityUser adminUser, UserManager)</param>
        /// <param name="onRegularUserCreated">Callback invoked when regular user is created. Parameters: (IdentityUser regularUser, UserManager)</param>
        /// <param name="onUsersSeeded">Callback invoked after both users are seeded. Parameters: (IdentityUser adminUser, IdentityUser regularUser)</param>
        public async Task SeedUsersAsync(
            Func<IdentityUser, UserManager<IdentityUser>, Task>? onAdminCreated = null,
            Func<IdentityUser, UserManager<IdentityUser>, Task>? onRegularUserCreated = null,
            Func<IdentityUser, IdentityUser, Task>? onUsersSeeded = null)
        {
            _logger?.LogInformation("Starting user seeding...");
            _logger?.LogInformation("Admin username: {AdminUserName}", _authOptions.SeedAdminUserName);
            _logger?.LogInformation("Regular username: {RegularUserName}", _authOptions.SeedRegularUserName);

            // Seed admin user
            var adminUser = await SeedUserAsync(
                _authOptions.SeedAdminUserName,
                _authOptions.SeedAdminUserPassword,
                "admin@example.com",
                "Admin User"
            );

            if (adminUser != null && onAdminCreated != null)
            {
                await onAdminCreated(adminUser, _userManager);
            }

            // Seed regular user
            var regularUser = await SeedUserAsync(
                _authOptions.SeedRegularUserName,
                _authOptions.SeedRegularUserPassword,
                "user@example.com",
                "Regular User"
            );

            if (regularUser != null && onRegularUserCreated != null)
            {
                await onRegularUserCreated(regularUser, _userManager);
            }

            // Invoke post-seeding callback
            if (adminUser != null && regularUser != null && onUsersSeeded != null)
            {
                await onUsersSeeded(adminUser, regularUser);
            }

            _logger?.LogInformation("User seeding completed");
        }

        /// <summary>
        /// Seeds a single user with the specified credentials.
        /// </summary>
        private async Task<IdentityUser?> SeedUserAsync(
            string userName,
            string password,
            string email,
            string displayName)
        {
            var user = await _userManager.FindByNameAsync(userName);

            if (user == null)
            {
                _logger?.LogInformation("Creating new user: {UserName}", userName);
                user = new IdentityUser
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    _logger?.LogInformation("User created successfully: {UserName}", userName);
                }
                else
                {
                    _logger?.LogError("Failed to create user {UserName}: {Errors}",
                        userName,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                    return null;
                }
            }
            else
            {
                _logger?.LogInformation("User exists, updating password: {UserName}", userName);

                // Update password if user exists
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, password);

                if (resetResult.Succeeded)
                {
                    _logger?.LogInformation("Password updated successfully for: {UserName}", userName);
                }
                else
                {
                    _logger?.LogError("Failed to update password for {UserName}: {Errors}",
                        userName,
                        string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                }
            }

            return user;
        }
    }
}
