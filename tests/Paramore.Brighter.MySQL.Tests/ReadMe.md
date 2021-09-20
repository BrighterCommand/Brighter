# !Docker Command used
docker run -p 127.0.0.1:3306:3306  --name BrighterTests -e MARIADB_ROOT_PASSWORD=root -d mariadb:latest