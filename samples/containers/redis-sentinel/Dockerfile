# Drawn from https://blog.alexseifert.com/2016/11/14/using-redis-sentinel-with-docker-compose/
FROM redis:3.2.5-alpine
 
ENV SENTINEL_QUORUM 2
ENV SENTINEL_DOWN_AFTER 1000
ENV SENTINEL_FAILOVER 1000
 
WORKDIR /redis
 
COPY sentinel.conf .
COPY sentinel-entrypoint.sh /usr/local/bin/
 
RUN chown redis:redis /redis/* && \
    chmod +x /usr/local/bin/sentinel-entrypoint.sh
 
EXPOSE 26379
 
ENTRYPOINT ["sentinel-entrypoint.sh"]
