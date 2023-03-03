FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim 

WORKDIR /app
COPY out/ .

#run the site
ENTRYPOINT ["dotnet", "GreetingsWatcher.dll"]
