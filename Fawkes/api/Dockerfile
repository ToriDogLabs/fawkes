#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
#RUN apt-get update -qq && apt-get install -qqy curl ca-certificates
#RUN	install -d /usr/share/postgresql-common/pgdg
#RUN	curl -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc --fail https://www.postgresql.org/media/keys/ACCC4CF8.asc
#RUN	echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt jammy-pgdg main" >> /etc/apt/sources.list.d/pgdg.list

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    curl ca-certificates gnupg lsb-release && \
    curl -fSsL https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor -o /usr/share/keyrings/postgresql.gpg && \
    echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] http://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
        python3 python3-pip python3-venv libpq-dev python3-dev build-essential postgresql-client-17 file gzip bzip2 xz-utils pv lsb-release && \
    pip install --no-cache-dir --no-build-isolation 'barman[cloud]==3.16.0' --break-system-packages && \
    apt-get purge -y build-essential python3-dev && apt-get autoremove -y && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

USER root
#RUN id -u barman &>/dev/null || useradd -m barman
#RUN mkdir -p /var/lib/barman
#RUN chown barman /var/lib/barman
RUN groupadd --system barman
RUN useradd --system --gid barman --create-home --home-dir /var/lib/barman --shell /bin/bash barman
RUN mkdir -p /etc/barman /var/lib/barman /var/log/barman /app/data /var/lib/barmanweb
RUN chown -R barman:barman /var/lib/barman /var/log/barman /app/data /var/lib/barmanweb

RUN touch /etc/barman.conf
RUN chown barman /etc/barman.conf

USER barman
RUN touch /var/lib/barman/.pgpass
RUN chmod 0600 /var/lib/barman/.pgpass

ENV AWS_CONFIG_FILE=/app/data/.aws/config
ENV AWS_SHARED_CREDENTIALS_FILE=/app/data/.aws/credentials

WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Fawkes/Fawkes.csproj", "Fawkes/"]
RUN dotnet restore "./Fawkes/Fawkes.csproj"
COPY . .
WORKDIR "/src/Fawkes"
RUN dotnet build "./Fawkes.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Fawkes.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Fawkes.dll"]