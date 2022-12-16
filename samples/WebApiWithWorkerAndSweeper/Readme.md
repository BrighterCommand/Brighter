To use this

  - docker run -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=Password1!' -p 11433:1433 -d mcr.microsoft.com/mssql/server:latest
  - Create a Database called BrighterOrderTests
  - Run Scheme script in Data