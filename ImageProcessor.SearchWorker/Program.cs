using ImageProcessor.SearchWorker;

var builder = Host.CreateApplicationBuilder(args);

// Configuration is automatically loaded from appsettings.json and environment variables

// Register WorkerRole as a hosted service
builder.Services.AddSingleton<WorkerRole>();
builder.Services.AddHostedService<SearchWorkerService>();

var host = builder.Build();
await host.RunAsync();
