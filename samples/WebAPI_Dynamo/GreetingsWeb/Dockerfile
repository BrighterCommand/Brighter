FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim 

WORKDIR /app
COPY out/ .

# Expose the port
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
#run the site
ENTRYPOINT ["dotnet", "GreetingsWeb.dll"]
