# Managing I/O in F# web API applications

*Source code for this article is available on [github](https://github.com/macfarmw/WeatherApi/tree/main).*

In this article, we'll consider a method for handling I/O operations in an F# application. There 
are several methods available for separating I/O in F# apps. These range from using the built-in 
dependency injection and interfaces to more abstract functional styles. We'll look at a simple
example using an interface based on the weather API template.

## Dedicated interface for each controller
In .NET applications, it's common to inject several service interfaces into each controller. These 
interfaces may each have any number of methods and the controllers often don't use them all. In 
order to be more explicit about what I/O operations the controller needs to perform, we can 
create an dedicated interface for each controller with only the methods it needs defined.

An alternative to interfaces that can be used with F#, is a record of functions. This works well 
and is mostly a difference in style. The use of interfaces is the recommended approach found in 
the F# guidelines for passing a group of functions and it also leaves open the possibility of 
using standard mocking libraries like NSubstitute.

Here's an example of our interface with 3 I/O operations defined.
```fsharp
type ErrorMessage = string
type MessageTemplate = string
type Latitude = float
type Longitude = float

// I/O operations interface
type IWeatherForecastIO =
    abstract CallWeatherServiceAsync: Latitude * Longitude * CancellationToken -> Task<Result<WeatherForecast, ErrorMessage>>
    abstract LogError: MessageTemplate * obj array -> unit
    abstract LogInformation: MessageTemplate * obj array -> unit
```

## Using tuple style arguments for interface members
There is a gotcha to watch out for with interfaces in F#. Even if we define an interface 
member with curried style parameters, the compiler will convert these methods to tuple style 
automatically. So, when defining and calling interface members in F#, using tuple style 
throughout may reduce any confusion.

> Note: Curried style parameters are defined in function type signatures as  
> `FunctionType: Arg1 -> Arg2 -> ReturnType` whereas tuple style parameters are defined as shown
> in the example `FunctionType: Arg1 * Arg2 -> ReturnType`

*See Brian's detailed explanation in the following Stackoverflow Q&A*    
[How are F# interface members implemented with object expressions?](https://stackoverflow.com/questions/76690204/how-are-f-interface-members-implemented-with-object-expressions)

## Module functions to implement each I/O method
While the interface members are written using an object oriented style, the functions that 
implement each of these members can use a functional style and curried parameters. The implementing 
function will be connected up to each of the members in the interface when we create the object 
instances that implement the interface.

Here is how the object that implements the interface is defined and added to the service 
collection in Program.fs
```fsharp
    ...
    builder.Services.AddSingleton<IWeatherForecastIO>(fun sp -> 
        let httpClientFactory = sp.GetRequiredService<IHttpClientFactory>()
        let logger = sp.GetRequiredService<ILogger<WeatherForecastController>>() 
        { new IWeatherForecastIO with
            member _.CallWeatherServiceAsync(latitude, longitude, token) =
                OpenMeteoWeatherService.getForecastAsync httpClientFactory latitude longitude token
            member _.LogError(error, args) = logger.LogError(error, args)
            member _.LogInformation(error, args) = logger.LogInformation(error, args) }
    )
    ...
```

The concrete dependencies `httpClientFactory` and `logger` were defined previously and are 
pulled in here for use in new object. The function we've created to implement the 
`CallWeatherServiceAsync` interface member calls out to the free Open Meteo service to get the 
weather data. The `getForecastAsync` function needs access to the `httpClientFactory` to make 
the API call so we pass that in as one of the parameters. Our production code that uses the 
interface won't need to interact with the HTTP client at all using this setup. Creating a mock 
for the `CallWeatherServiceAsync` interface member is now much simpler because only the simple data 
criteria and return types need to be considered.

It is very helpful to keep low level dependencies like database, cloud, and HTTP API concealed 
this way. It also reduces the number of places that changes need to be made when switching I/O 
sources between different databases and cloud services. As long as the simple public API of the 
interface remains unchanged such infrastructure changes won't spill over beyond this setup point.

> Note: Keep in mind the the dependency libraries often define types for input and 
> return data as well. To maintain full isolation, create custom types within the application 
> and map inputs and outputs to these within the implementing module functions. For example, the 
> client for AWS queues define `SendMessageResponse` and `ReceiveMessageResponse` types. If these 
> types are used at the interface level then all the consuming code downstream will have to take 
> a reference on the AWS client. If 3rd party types remain isolated only with the module that 
> interacts within that resource it can lead to much easier maintenance.

## Using the DI container
The standard DI container that comes with ASP.NET is an easy default choice for many applications.
In our example above the finished implementation of `IWeatherForecastIO` is added to the container
as a singleton. So now it's simple to use this within the controller.

```fsharp
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
```

## Testing with standard mocking tools
Because we selected an interface for the I/O operations, we can make use of any of the standard
.NET mocking tool libraries. In our example we'll use NSubstitute. It's also very common to 
create custom mock functions in F#, and that option remains available. In fact, if the 
mock will only be used to return specific a canned response (it's really stub in that case) then 
there it's really just as simple to use a custom mock function.
If we want to test interactions with a mock function like counting how many times it was called, 
then the mocking library can be more helpful. Let's look at it both ways.

In the first example test we'll use NSubstitute.
```fsharp
[<Fact>]
member this.``GetAsync should return a list of weather forecasts``() =
    task {
        // Arrange
        let (location: Location) = "41.8755616,-87.624421"
        let today = DateTime.Today

        let (expectedForecastResult: Result<WeatherForecast, string>) =
            Ok
                { TemperatureCurrentC = 7
                  TemperatureCurrentF = 45
                  DailyForecast =
                    [| DailyForecast.Create today 7 45 12 55 "Overcast"
                       DailyForecast.Create (today.AddDays(1)) 3 38 17 62 "Drizzle"
                       DailyForecast.Create (today.AddDays(2)) 3 38 17 62 "Drizzle"
                       DailyForecast.Create (today.AddDays(3)) 9 48 17 62 "Clear"
                       DailyForecast.Create (today.AddDays(4)) 9 48 20 68 "Overcast"
                       DailyForecast.Create (today.AddDays(5)) 14 57 23 74 "Overcast"
                       DailyForecast.Create (today.AddDays(6)) 19 67 29 85 "Partly cloudy" |] }
                
        let mockWeatherForecastIO = Substitute.For<IWeatherForecastIO>()
        mockWeatherForecastIO
            .CallWeatherServiceAsync(Arg.Any(), Arg.Any(), Arg.Any())
            .Returns(expectedForecastResult)
        |> ignore

        let ctrl = WeatherForecastController(mockWeatherForecastIO)

        // Act
        let! result = ctrl.GetAsync location

        // Assert
        Assert.NotNull(result)

        Assert.IsAssignableFrom<ObjectResult>(result)
        |> ignore

        let objectValue = (result :?> ObjectResult).Value :?> WeatherForecast
        Assert.Equal(7, objectValue.TemperatureCurrentC)
    }
```
In this test we setup some expected data to be returned by the API call. `Create` here is a 
simple static member on the `DailyForecast` record type to make this creation operation more 
concise. Review the full code sample on github if you'd like to see the details.  

The `mockWeatherForecastIO` instance is quite simple to setup. Since we're not trying to assert 
anything about what the log functions are doing in this test, we just skip setting them up and 
go with whatever the mocking library does as default. The hardest part of writing this test by 
far was figuring out how to get at the value inside the `ObjectResult` type.

```fsharp
let objectValue = (result :?> ObjectResult).Value :?> WeatherForecast
```

Here is an alternative implementation of the mock that doesn't use NSubstitute.
```fsharp
let mockWeatherForecastIOCustom =
    { new IWeatherForecastIO with
        member _.CallWeatherServiceAsync(_, _, _) = task{ return expectedForecastResult }
        member _.LogError(error, args) = ()
        member _.LogInformation(error, args) = () }
```

This implementation uses a very nice F# feature called an [object expression](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/object-expressions) to create an object that implements 
the interface inline. If you want to track how many times the custom `CallWeatherServiceAsync` gets 
called by the controller, a mutable value can be used like this.

```fsharp
let mutable timesCalled = 0 
let mockWeatherForecastIOCustom =
    { new IWeatherForecastIO with
        member _.CallWeatherServiceAsync(_, _, _) =
            task{
                timesCalled <- timesCalled + 1
                return expectedForecastResult
            }
        member _.LogError(error, args) = ()
        member _.LogInformation(error, args) = () }
```
Then, simply inspect the value with an assertion.

## Alternative approaches
I found a lot of help and inspiration when looking into this topic from these articles.  
- [Dealing with complex dependency injection in F#](https://www.bartoszsypytkowski.com/dealing-with-complex-dependency-injection-in-f/)
- [Dependency Injection in F# Web APIs](https://dev.to/jhewlett/dependency-injection-in-f-web-apis-4h2o)
- [Six approaches to dependency injection](https://fsharpforfunandprofit.com/posts/dependencies/)
