using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using UGroLifeAPI.Data; // Assuming UGroLifeApi.Data is where your DbContext is located
using Microsoft.AspNetCore.Authentication.JwtBearer; // Required for JWT Bearer authentication
using Microsoft.IdentityModel.Tokens; // Required for SecurityKey
using System.Text; // Required for Encoding

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure PostgreSQL with EF Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// --- START: JWT Authentication Configuration ---
// Get the JWT Secret Key from configuration
var jwtSecret = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtSecret))
{
    throw new InvalidOperationException("JWT:Key is not configured in appsettings.json or environment variables.");
}
var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Set to true in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true, // Validate the issuer of the token
        ValidIssuer = builder.Configuration["Jwt:Issuer"], // Your API's issuer (e.g., "UGroLifeAPI")
        ValidateAudience = true, // Validate the audience of the token
        ValidAudience = builder.Configuration["Jwt:Audience"], // Your frontend's audience (e.g., "UGroLifeApp")
        ValidateLifetime = true, // Validate the token's expiration
        ClockSkew = TimeSpan.Zero // No clock skew, token must be valid exactly within its lifetime
    };
});
// --- END: JWT Authentication Configuration ---


// Add CORS policy - important for your frontend to talk to your API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        policy =>
        {
            // For development, you can allow any origin.
            // For production, replace "*" with specific allowed origins (e.g., "https://yourfrontenddomain.com")
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); 
app.UseRouting(); // Important for routing to work

app.UseCors("AllowSpecificOrigin"); // Apply the CORS policy

// --- START: Authentication Middleware ---
// IMPORTANT: UseAuthentication MUST come BEFORE UseAuthorization
app.UseAuthentication();
// --- END: Authentication Middleware ---

app.UseAuthorization(); // This middleware checks if the user is authorized based on policies/attributes

app.MapControllers(); // Maps your API controller routes

app.Run();
