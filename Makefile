export COMPOSE_PROJECT_NAME=advantage

.PHONY: build
build:
	podman build -t onbase-upload .

.PHONY: -down
down:
	echo "no implemented"

.PHONY: setup
setup: build

.PHONY: serve
serve:
	echo "booting to port 9090"
	podman run -p 9090:9090 --net advantage_advantage --network-alias onbase --rm onbase-upload dotnet Greystone.OnbaseUploadService.dll --urls=http://0.0.0.0:9090 --environment Development --no-launch-profile

.PHONY: test
test:
	echo "LMAO!"

.PHONY: shell
shell:
	podman run -p 9090:9090 --rm onbase-upload ash
