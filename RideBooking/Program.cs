using RideBooking.Data;
using RideBooking.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure DbContext with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<RideBookingDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddMemoryCache();
builder.Services.Configure<GoogleMapsSettings>(builder.Configuration.GetSection("GoogleMapsSettings"));
builder.Services.AddHttpClient<GoogleMapsLocationService>(client =>
{
    // GetQuoteAsync (and CreateBookingAsync's transaction) waits on this call;
    // bound how long a slow/hung Directions API response can block it.
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Register booking services
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ILocationService>(sp => sp.GetRequiredService<GoogleMapsLocationService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
