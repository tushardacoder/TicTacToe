using TicTacToe.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddGameServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseRouting();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<TicTacToe.Web.Hubs.GameHub>("/gameHub");


app.Run();
