// See https://aka.ms/new-console-template for more information

using FellowOakDicom;
using Microsoft.Extensions.Hosting;

Console.WriteLine("Init DicomServer.SCP!");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddFellowOakDicom();
    })
    .Build();

// This is still necessary for now until fo-dicom has first-class AspNetCore integration
DicomSetupBuilder.UseServiceProvider(host.Services);

await host.RunAsync();