# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

ADD . .
RUN dotnet restore

# Copy everything else and build
RUN dotnet publish -c Release -o /out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build-env /out .

ENV Audible__Environment__TempPath=/data/tmp
ENV Audible__Environment__OutputPath=/data/out
ENV Audible__Environment__SettingsBasePath=/data/settings
ENV Audible__Environment__OutputPattern=%Author%/%Series%/%title%/%title%.%ext%

ENTRYPOINT ["dotnet", "AudibleSyncService.dll"]