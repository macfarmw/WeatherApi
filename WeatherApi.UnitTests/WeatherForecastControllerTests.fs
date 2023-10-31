namespace WeatherApi.UnitTests

open System
open System.Threading
open System.Threading.Tasks
open Castle.Core.Logging
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open NSubstitute
open WeatherApi
open Xunit

open WeatherApi.Controllers
open Xunit.Abstractions

type WeatherForecastControllerTests(output:ITestOutputHelper) =
    
    [<Fact>]
    member this.``Get should return a list of weather forecasts`` () =
        task{
            // Arrange
            let (location:Location) = "123"
            let today = DateTime.Today
            let (expectedForecastResult:Result<WeatherForecast,string>) =
                Ok
                    { TemperatureCurrentC = 7
                      TemperatureCurrentF = 45
                      DailyForecast =
                        [|
                            DailyForecast.Create today 7 45 12 55 "Overcast"
                            DailyForecast.Create (today.AddDays(1)) 3 38 17 62 "Drizzle"
                            DailyForecast.Create (today.AddDays(2)) 3 38 17 62 "Drizzle"
                            DailyForecast.Create (today.AddDays(3)) 9 48 17 62 "Clear"
                            DailyForecast.Create (today.AddDays(4)) 9 48 20 68 "Overcast"
                            DailyForecast.Create (today.AddDays(5)) 14 57 23 74 "Overcast"
                            DailyForecast.Create (today.AddDays(6)) 19 67 29 85 "Partly cloudy"
                        |] }

            let mockIo =
                { new IWeatherForecastIO with
                    member _.CallWeatherServiceAsync = (fun _ _ -> Task.FromResult(expectedForecastResult)) }
            
            let ctrl = WeatherForecastController(mockIo)
            
            // Act
            let! result = ctrl.GetAsync location
            
            // Assert
            Assert.NotNull(result)
            Assert.IsAssignableFrom<ObjectResult>(result) |> ignore
            let objectValue = (result :?> ObjectResult).Value :?> WeatherForecast
            Assert.Equal(7, objectValue.TemperatureCurrentC)
        }
        
    [<Fact>]
    member this.``Get should return an error`` () =
        task{
            // Arrange
            let (location:Location) = "123"
            let errorMsg = "Invalid or missing location" 
            let today = DateTime.Today
            let (expectedForecastResult:Result<WeatherForecast,string>) =
                Error errorMsg
                
            let mockIo =
                { new IWeatherForecastIO with
                    member _.CallWeatherServiceAsync = (fun _ _ -> Task.FromResult(expectedForecastResult)) }
            
            let ctrl = WeatherForecastController(mockIo)
            
            // Act
            let! result = ctrl.GetAsync location
            
            // Assert
            Assert.IsAssignableFrom<BadRequestObjectResult>(result) |> ignore
            let badRequest = result :?> BadRequestObjectResult
            Assert.Equal(Nullable(400), badRequest.StatusCode)
            Assert.Equal(errorMsg, badRequest.Value.ToString())
            
        }
