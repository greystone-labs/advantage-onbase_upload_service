FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
WORKDIR /App

# Copy everything
COPY Greystone.OnbaseUploadService ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/sdk:7.0.101-alpine3.17-amd64

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
RUN apk add --no-cache \
        icu-data-full \
        icu-libs \
        nano

ENV ASPNETCORE_ENVIRONMENT=Development

WORKDIR /App
COPY --from=build-env /App/out .
ENV DOTNET_EnableDiagnostics=0
ENV ASPNETCORE_URLS=http://+:9090 \
    # Enable detection of running in a container
    DOTNET_RUNNING_IN_CONTAINER=true 

CMD ["dotnet", "Greystone.OnbaseUploadService.dll", "--no-launch-profile"]

