var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // Add JSON options for API integration
    options.ModelBinderProviders.Insert(0, new Microsoft.AspNetCore.Mvc.ModelBinding.Binders.SimpleTypeModelBinderProvider(typeof(decimal), new Microsoft.AspNetCore.Mvc.ModelBinding.Binders.DecimalModelBinder()));
});

// Add anti-forgery token protection
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.SuppressXFrameOptionsHeader = false;
});

// Configure JSON serialization for API compatibility
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

// Add HttpClient for API calls
builder.Services.AddHttpClient("DriftRideApi", client =>
{
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7000";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Add CORS for local development and API communication
builder.Services.AddCors(options =>
{
    options.AddPolicy("DriftRidePolicy", policy =>
    {
        policy.WithOrigins(
                "https://localhost:7000",
                "https://localhost:5001",
                "http://localhost:5000"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Location");
    });
});

// Add session state for customer workflow tracking
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Add memory cache for payment method configuration
builder.Services.AddMemoryCache();

// Register API service
builder.Services.AddScoped<DriftRide.Web.Services.IDriftRideApiService, DriftRide.Web.Services.DriftRideApiService>();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Enable CORS
app.UseCors("DriftRidePolicy");

app.UseRouting();

// Enable session before authorization
app.UseSession();

app.UseAuthorization();

// Map routes with customer as default for mobile users
app.MapControllerRoute(
    name: "customer",
    pattern: "customer/{action=Index}",
    defaults: new { controller = "Customer" });

// Sales dashboard routes
app.MapControllerRoute(
    name: "sales",
    pattern: "sales/{action=Login}",
    defaults: new { controller = "Sales" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Customer}/{action=Index}/{id?}");

app.Run();
