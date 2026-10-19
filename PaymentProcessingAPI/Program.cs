using Microsoft.EntityFrameworkCore;
using PaymentProcessingAPI.Data;
using PaymentProcessingAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Keys.ToDictionary(
            key => key,
            _ => new[] { "Значение отсутствует или имеет неверный формат." });
        var details = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Запрос содержит некорректные данные."
        };
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(details);
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddSingleton<IBankPaymentClient, DemoBankPaymentClient>();
builder.Services.AddScoped<PaymentProcessor>();
builder.Services.AddHostedService<PaymentProcessingWorker>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    logger.LogInformation("Применение миграций EF Core к базе PaymentProcessing");
    await dbContext.Database.MigrateAsync();
    logger.LogInformation("База PaymentProcessing готова");
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Не удалось создать базу PaymentProcessing или применить миграции");
    throw new InvalidOperationException(
        "Не удалось инициализировать базу данных. Проверьте, что служба PostgreSQL запущена и у пользователя есть права на создание базы и таблиц.", ex);
}

app.Use(async (context, next) =>
{
    var started = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Необработанная ошибка при обработке запроса {Method} {Path}", context.Request.Method, context.Request.Path);
        if (context.Response.HasStarted)
            throw;

        var (statusCode, title) = ex switch
        {
            InvalidPaymentException => (StatusCodes.Status400BadRequest, ex.Message),
            AccountNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            IdempotencyConflictException => (StatusCodes.Status409Conflict, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "При обработке запроса произошла внутренняя ошибка.")
        };

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = title
        });
    }
    finally
    {
        started.Stop();
        app.Logger.LogInformation("Запрос {Method} {Path} завершен с кодом {StatusCode} за {ElapsedMs} мс",
            context.Request.Method, context.Request.Path, context.Response.StatusCode, started.ElapsedMilliseconds);
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.Logger.LogInformation("Запуск PaymentProcessingAPI в окружении {Environment}", app.Environment.EnvironmentName);
app.Run();
