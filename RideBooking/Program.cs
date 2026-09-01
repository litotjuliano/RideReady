using RideBooking.Data;
using RideBooking.Jobs;
using RideBooking.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;

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
builder.Services.AddScoped<IDriverAssignmentService, DriverAssignmentService>();
builder.Services.AddScoped<IDriverPortalService, DriverPortalService>();

// Register notification services (Email, WhatsApp, Google Calendar)
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<WhatsAppSettings>(builder.Configuration.GetSection("WhatsAppSettings"));
builder.Services.Configure<GoogleCalendarSettings>(builder.Configuration.GetSection("GoogleCalendarSettings"));

builder.Services.AddHttpClient<WhatsAppCloudApiSender>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IWhatsAppSender>(sp => sp.GetRequiredService<WhatsAppCloudApiSender>());
builder.Services.AddScoped<ICalendarSyncService, GoogleCalendarSyncService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Register Quartz background jobs (notification retries, unassigned-booking reminders, no-show detection)
builder.Services.AddQuartz(q =>
{
    var retryKey = new JobKey("NotificationRetryJob");
    q.AddJob<NotificationRetryJob>(opts => opts.WithIdentity(retryKey));
    q.AddTrigger(opts => opts
        .ForJob(retryKey)
        .WithIdentity("NotificationRetryJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

    var reminderKey = new JobKey("ReminderEscalationJob");
    q.AddJob<ReminderEscalationJob>(opts => opts.WithIdentity(reminderKey));
    q.AddTrigger(opts => opts
        .ForJob(reminderKey)
        .WithIdentity("ReminderEscalationJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

    var noShowKey = new JobKey("NoShowDetectionJob");
    q.AddJob<NoShowDetectionJob>(opts => opts.WithIdentity(noShowKey));
    q.AddTrigger(opts => opts
        .ForJob(noShowKey)
        .WithIdentity("NoShowDetectionJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));
});
builder.Services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

builder.Services.Configure<AdminCredentialsSettings>(builder.Configuration.GetSection("AdminCredentials"));

builder.Services.AddAuthentication()
    .AddCookie("AdminAuth", options =>
    {
        options.Cookie.Name = "RideBooking.AdminAuth";
        options.LoginPath = "/AdminAuth/Login";
        options.AccessDeniedPath = "/AdminAuth/Login";
    })
    .AddCookie("DriverAuth", options =>
    {
        options.Cookie.Name = "RideBooking.DriverAuth";
        options.LoginPath = "/DriverAuth/Login";
        options.AccessDeniedPath = "/DriverAuth/Login";
    });
builder.Services.AddAuthorization();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<RideBookingDbContext>();

var app = builder.Build();

// Apply pending EF Core migrations automatically on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RideBookingDbContext>();
    await db.Database.MigrateAsync();
}

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

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
