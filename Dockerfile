FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
WORKDIR /App

# Copy everything
COPY Greystone.OnbaseUploadService ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0
# FROM mcr.microsoft.com/dotnet/nightly/aspnet:7.0.1-alpine3.17-amd64

WORKDIR /App
COPY --from=build-env /App/out .
ENV DOTNET_EnableDiagnostics=0
CMD ["dotnet", "Greystone.OnbaseUploadService.dll"]