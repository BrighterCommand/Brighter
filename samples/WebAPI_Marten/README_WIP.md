# marten setup WIP
you'll need the postgresql instance so use docker-compose located in the root directory  

docker-compose -f docker-compose.yaml up -d

user_name: root
password: password  

Npgsql.PostgresException (0x80004005): 3D000: database "marten_db" does not exist
(make sure that you have created an actual db and not the server)  :)
