namespace WeatherApi.Controllers

open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open WeatherApi

[<ApiController>]
[<Route("[controller]")>]
type WeatherForecastController (io: IWeatherForecastIO) =
    inherit ControllerBase()
    
    let _io = io

    [<HttpGet>]
    member this.GetAsync(location:string) : Task<IActionResult> =
        task{
            let cts = new CancellationTokenSource(5000)
            
            let coord = Coordinates.ofString location

            let! weatherForecast =
                match coord with
                | Some { Latitude = lat; Longitude = long } ->
                    _io.CallWeatherServiceAsync(lat, long, cts.Token)
                | None ->
                    Task.FromResult(Result.Error "Invalid location.")
                        
            match weatherForecast with
            | Ok res ->
                _io.LogInformation("Successfully retrieved forecast for {location}", [| location |] )
                return ObjectResult(res) :> IActionResult
            | Error err ->
                _io.LogError("Error {error} retrieving forecast for {location}", [| err; location |] )
                return this.BadRequest(err) :> IActionResult
        }
