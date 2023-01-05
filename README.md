
podman build -t onbase-upload .

podman run -p 9090:9090 --rm onbase-upload dotnet Greystone.OnbaseUploadService.dll --urls=http://0.0.0.0:9090 --environment Development

http://localhost:9090


curl -H "x-api-key: 1234" http://localhost:9090/v1/DocumentTypes  --head
curl -X POST -H "x-api-key: 1234" http://localhost:9090/v1/upload