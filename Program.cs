using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Library.Models;
using Library.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<EmailService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "default-key"))
    };
});

builder.Services.AddControllers();

builder.Services.AddDbContext<LibraryContext>(options =>
    options.UseSqlServer("Server=tcp:freshworks1.database.windows.net,1433;Initial Catalog=freshworks;User ID=ajay_rajan97;Password=Helloworld@123;Encrypt=True;"));

var app = builder.Build();

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryContext>();

    if (!db.Users.Any(u => u.Role == "LibraryDirector"))
    {
        var libraryExists = db.LibraryBranches.FirstOrDefault();  // Corrected variable name
        if (libraryExists == null)
        {
            Console.WriteLine(" No libraries found in the database. Cannot seed LibraryDirector user.");
        }
        else
        {
            var adminUser = new User
            {
                Email = "director@library.com",
                Role = "LibraryDirector",
                IsApproved = true,
                PasswordHash = HashPassword("SuperSecurePassword123"),
                LibraryId = libraryExists.Id
            };

            db.Users.Add(adminUser);
            db.SaveChanges();

            Console.WriteLine("Default LibraryDirector created: director@library.com / SuperSecurePassword123");
        }
    }
}

string HashPassword(string password)
{
    using var sha256 = System.Security.Cryptography.SHA256.Create();
    var bytes = Encoding.UTF8.GetBytes(password);
    var hash = sha256.ComputeHash(bytes);
    return Convert.ToBase64String(hash);
}

app.Run();
