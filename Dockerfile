ARG RELEASE_VERSION
#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# --- STAGE 1: Python Builder ---
# We install Barman here so we don't need build-tools in the final image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS python-builder
USER root
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 python3-pip python3-venv libpq-dev python3-dev build-essential
    
RUN python3 -m venv /opt/barman-venv
ENV PATH="/opt/barman-venv/bin:$PATH"
RUN pip install --no-cache-dir 'barman[cloud]==3.16.0'

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root

# Install only RUNTIME dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl ca-certificates gnupg lsb-release && \
    curl -fSsL https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor -o /usr/share/keyrings/postgresql.gpg && \
    echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] http://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
    python3 postgresql-client-17 file gzip bzip2 xz-utils pv lsb-release libpq5 && \
    apt-get purge -y curl gnupg lsb-release && \
    apt-get autoremove -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy the Python virtual environment from the builder
COPY --from=python-builder /opt/barman-venv /opt/barman-venv
ENV PATH="/opt/barman-venv/bin:$PATH"

# Setup Users & Directories
RUN groupadd --system barman && \
    useradd --system --gid barman --create-home --home-dir /var/lib/barman --shell /bin/bash barman && \
    mkdir -p /etc/barman /var/lib/barman /var/log/barman /app/data /var/lib/barmanweb && \
    touch /etc/barman.conf

USER barman
RUN touch /var/lib/barman/.pgpass && chmod 0600 /var/lib/barman/.pgpass

ENV AWS_CONFIG_FILE=/app/data/.aws/config
ENV AWS_SHARED_CREDENTIALS_FILE=/app/data/.aws/credentials

WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Fawkes/api/Fawkes.Api.csproj", "api/"]
RUN dotnet restore "./api/Fawkes.Api.csproj"
COPY ./Fawkes .
WORKDIR "/src/api"
RUN dotnet build -c Release -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Build the frontend
FROM node:22.14.0-alpine AS frontend-builder
WORKDIR /app

RUN corepack enable

# Copy only lockfiles first to leverage Docker cache
COPY ./web/package.json ./web/yarn.lock ./web/.yarnrc.yml ./
COPY ./web/.yarn ./.yarn

# Install dependencies
RUN yarn install --immutable

# Copy source and build
COPY ./web/. .
RUN yarn build

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
ARG RELEASE_VERSION
WORKDIR /app

# Get the executable and copy it to /healthchecks
COPY --from=ghcr.io/alexaka1/distroless-dotnet-healthchecks:1 / /healthchecks
# Setup the healthcheck using the EXEC array syntax
HEALTHCHECK CMD ["/healthchecks/Distroless.HealthChecks", "--uri", "http://localhost:8080/health"]

COPY --link --from=publish /app/publish .
COPY --link --from=frontend-builder /app/dist ./wwwroot

USER root
RUN chown -R barman:barman /var/lib/barman /var/log/barman /app/data /var/lib/barmanweb /etc/barman.conf
USER barman

ENTRYPOINT ["dotnet", "Fawkes.Api.dll"]