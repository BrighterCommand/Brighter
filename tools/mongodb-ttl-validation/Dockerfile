FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS runtime9
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble

COPY --from=runtime9 /usr/share/dotnet/shared/ /usr/share/dotnet/shared/
ARG BRIGHTER_TTL_UID=1000
ARG BRIGHTER_TTL_GID=1000
RUN if ! getent group "${BRIGHTER_TTL_GID}" > /dev/null; then \
        groupadd --gid "${BRIGHTER_TTL_GID}" brighter-test; \
    fi \
    && if ! getent passwd "${BRIGHTER_TTL_UID}" > /dev/null; then \
        useradd --uid "${BRIGHTER_TTL_UID}" --gid "${BRIGHTER_TTL_GID}" \
            --create-home --no-log-init brighter-test; \
    fi \
    && mkdir /work && chmod 1777 /work \
    && rm -rf /tmp/.dotnet

ENV DOTNET_CLI_HOME=/tmp/dotnet-cli \
    NUGET_PACKAGES=/tmp/nuget-packages \
    XDG_DATA_HOME=/tmp/xdg-data \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
COPY run-tests.sh /usr/local/bin/brighter-mongodb-tests
WORKDIR /work
USER ${BRIGHTER_TTL_UID}:${BRIGHTER_TTL_GID}
RUN dotnet nuget locals all --list \
    && rm -rf /tmp/.dotnet
ENTRYPOINT ["bash", "/usr/local/bin/brighter-mongodb-tests"]
