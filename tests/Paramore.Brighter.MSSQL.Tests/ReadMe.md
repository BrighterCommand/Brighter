﻿# !Docker Command used 
docker run -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=Password123!' -p 11433:1433 -d mcr.microsoft.com/mssql/server:latest