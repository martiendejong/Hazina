namespace Common.Infrastructure.AspNetCore.Authentication
{
    /// <summary>
    /// Configuration options for authentication and user seeding.
    /// </summary>
    public class AuthOptions
    {
        /// <summary>
        /// Secret key used for signing JWT tokens.
        /// </summary>
        public string JwtKey { get; set; } = "dev-secret-key";

        /// <summary>
        /// Username for the admin user to be seeded at startup.
        /// </summary>
        public string SeedAdminUserName { get; set; } = "admin";

        /// <summary>
        /// Password for the admin user to be seeded at startup.
        /// </summary>
        public string SeedAdminUserPassword { get; set; } = "Admin#12345";

        /// <summary>
        /// Username for the regular user to be seeded at startup.
        /// </summary>
        public string SeedRegularUserName { get; set; } = "REGULAR";

        /// <summary>
        /// Password for the regular user to be seeded at startup.
        /// </summary>
        public string SeedRegularUserPassword { get; set; } = "User#12345";
    }
}
