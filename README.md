
podman build -t onbase-upload .

podman run -p 9090:9090 --rm onbase-upload dotnet Greystone.OnbaseUploadService.dll --urls=http://0.0.0.0:9090 --environment Development --no-launch-profile

http://localhost:9090


curl -H "x-api-key: 1234" http://localhost:9090/v1/DocumentTypes  --head
curl -X POST -H "x-api-key: 1234" http://localhost:9090/v1/upload

podman run --rm -it -p 8000:80 -v $(pwd):/app/ -w /app -e ASPNETCORE_URLS=http://+:80 -e ASPNETCORE_ENVIRONMENT=Development mcr.microsoft.com/dotnet/sdk:7.0 dotnet run --no-launch-profile




## testing 
dotnet new console --framework net7.0

using System.Globalization;
Console.WriteLine(CultureInfo.CurrentCulture);

podman  run -it --rm --entrypoint "bash" onbase-upload
dotnet run --no-launch-profile

