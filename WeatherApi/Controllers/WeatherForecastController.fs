namespace WeatherApi.Controllers

open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open WeatherApi

[<ApiController>]
[<Route("[controller]")>]
type WeatherForecastController (io: IWeatherForecastIO) =
    inherit ControllerBase()

    [<HttpGet>]
    member this.GetAsync(location:string) : Task<IActionResult> =
        task{
            let cts = new CancellationTokenSource(5000)
            let! weatherForecastResult = io.CallWeatherServiceAsync location cts.Token
            match weatherForecastResult with
            | Ok res ->
                return ObjectResult(res) :> IActionResult
            | Error err ->
                return this.BadRequest(err) :> IActionResult
        }
        
        