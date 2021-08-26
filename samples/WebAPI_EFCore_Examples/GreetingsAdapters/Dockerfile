FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base

FROM mcr.microsoft.com/dotnet/core/sdk:5.0 AS build
WORKDIR /src
COPY GreetingsAdapters.csproj GreetingsAdapters/
COPY ../GreetingsPorts/GreetingsPorts.csproj GreetingsPorts/
COPY ../GreetingsEntities/GreetingsEntities.csproj GreetingsEntities/

RUN dotnet restore "GreetingsAdapters/GreetingsAdapters.csproj"

COPY . GreetingsAdapters/
COPY ../GreetingsPorts/* GreetingsPorts/
COPY ../GreetingsEntities/* GreetingsEntities/

WORKDIR "/src/GreetingsAdapters"
RUN dotnet build "GreetingsAdapters.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GreetingsAdapters.csproj" -c Release -o app/publish 

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose the port
EXPOSE 5000

#run the site
ENTRYPOINT ["dotnet", "GreetingsAdapters.dll"]
