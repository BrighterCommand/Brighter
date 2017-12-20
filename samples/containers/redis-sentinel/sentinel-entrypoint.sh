#!/bin/sh
# Drawn from https://blog.alexseifert.com/2016/11/14/using-redis-sentinel-with-docker-compose/
 
sed -i "s/\$SENTINEL_QUORUM/$SENTINEL_QUORUM/g" /redis/sentinel.conf
sed -i "s/\$SENTINEL_DOWN_AFTER/$SENTINEL_DOWN_AFTER/g" /redis/sentinel.conf
sed -i "s/\$SENTINEL_FAILOVER/$SENTINEL_FAILOVER/g" /redis/sentinel.conf
 
redis-server /redis/sentinel.conf --sentinel