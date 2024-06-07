ENV_FILE ?= .env
image := rechtlogisch/csotto

docker-build:
	docker build \
		--platform linux/amd64 \
		-t $(image) \
		.

docker-cmd:
	@docker run \
		-it --rm \
		--env-file=$(ENV_FILE) \
		--platform=linux/amd64 \
		-v $(PATH_DOWNLOAD):/app/download/ \
		--entrypoint="bash" \
		$(image)

docker-csotto:
	@docker run \
		-it --rm \
		--env-file=$(ENV_FILE) \
		--platform=linux/amd64 \
		--name=csotto \
		-v $(PATH_DOWNLOAD):/app/download/ \
		$(image) \
		$(input)
