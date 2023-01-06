
podman build -t onbase-upload .

podman run -p 9090:9090 --rm onbase-upload dotnet Greystone.OnbaseUploadService.dll --urls=http://0.0.0.0:9090 --environment Development --no-launch-profile

http://localhost:9090


curl -H "x-api-key: 1234" http://localhost:9090/v1/DocumentTypes  --head
curl -X POST -H "x-api-key: 1234" http://localhost:9090/v1/upload

$ curl -X 'POST'    -H "x-api-key: 1234"   'http://localhost:9090/v1/Upload'   -H 'accept: text/plain'   -H 'Content-Type: application/json'   -d '{
  "documentTypeName": "Amenities {FHAMAP}",
  "keywords": {
    "additionalProp1": "string",
    "additionalProp2": "string",
    "additionalProp3": "string"
  },
  "fileCount": 1
}'

curl -v -X 'DELETE' \
  -H "x-api-key: 1234" \
  'http://localhost:9090/v1/Upload/<uploadId>' \
  -H 'accept: */*'

WARN: will always return a 200, even if previously deleted, however, the file upload will fail with a 404

curl -v -X 'PUT' \
  -H "x-api-key: 1234" \
  'http://localhost:9090/v1/Upload/0ecb05b2-3eea-4b87-bd11-8cd9e2af44f5/files/0' \
  -H 'accept: */*' \
  -H 'Content-Type: multipart/form-data' \
  -F 'ContentType=text/plain' \
  -F 'ContentDisposition=' \
  -F 'Headers={
  "additionalProp1": [
    "string"
  ],
  "additionalProp2": [
    "string"
  ],
  "additionalProp3": [
    "string"
  ]
}' \
  -F 'Length=3400' \
  -F 'Name=trace.txt' \
  -F 'FileName=trace.txt' \
  -F file=@/home/mac/Downloads/trace.txt


curl -v -X 'POST' \
  -H "x-api-key: 1234" \
  'http://localhost:9090/v1/Upload/0ecb05b2-3eea-4b87-bd11-8cd9e2af44f5/commit' \
  -H 'accept: */*' 

WARN: multiple commits will return diff document id

## testing 
dotnet new console --framework net7.0

using System.Globalization;
Console.WriteLine(CultureInfo.CurrentCulture);

podman  run -it --rm --entrypoint "sh" onbase-upload
dotnet run --no-launch-profile

## examples

https://github.com/dotnet/dotnet-docker/blob/main/samples/aspnetapp/Dockerfile.alpine-x64
