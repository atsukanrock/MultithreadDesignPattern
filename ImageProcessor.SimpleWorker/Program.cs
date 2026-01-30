using ImageProcessor.SimpleWorker;

var builder = Host.CreateApplicationBuilder(args);

// Configuration is automatically loaded from appsettings.json and environment variables

// Register WorkerRole as a hosted service
builder.Services.AddSingleton<WorkerRole>();
builder.Services.AddHostedService<SimpleWorkerService>();

var host = builder.Build();
await host.RunAsync();
