namespace WeatherApi
#nowarn "20"
open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.HttpsPolicy
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open WeatherApi.Controllers

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =

        let builder = WebApplication.CreateBuilder(args)
        builder.Logging.AddConsole()
        builder.Services.AddControllers()
        builder.Services.AddHttpClient()
        
        builder.Services.AddSingleton<IWeatherForecastIO>(fun sp -> 
            let httpClientFactory = sp.GetRequiredService<IHttpClientFactory>()
            let logger = sp.GetRequiredService<ILogger<WeatherForecastController>>() 
            { new IWeatherForecastIO with
                member _.CallWeatherServiceAsync(latitude, longitude, token) =
                    OpenMeteoWeatherService.getForecastAsync httpClientFactory latitude longitude token
                member _.LogError(error, args) = logger.LogError(error, args)
                member _.LogInformation(error, args) = logger.LogInformation(error, args) }
        )

        let app = builder.Build()
        
        app.UseHttpsRedirection()
        app.UseAuthorization()
        app.MapControllers()

        app.Run()

        exitCode