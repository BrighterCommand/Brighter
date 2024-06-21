How to build a multi-platform image using a cloud builder and push to docker hub.
https://docs.docker.com/build/cloud/setup/

This builds images that work on linux/amd64 and linux/arm64 which is really nice for us Mac M1, M2, M3 and other arm users.  

You need an account which is connected the the brightercommand organisation https://hub.docker.com/u/brightercommand

```shell
docker login
```

The cloud builder is setup here https://build.docker.com/accounts/brightercommand/builders


Create a local instance of the cloud builder on your local machine.
```shell
docker buildx create --driver cloud brightercommand/jeffthebuilder
```

Use the cloud builder.
```shell
docker buildx build --builder cloud-brightercommand-jeffthebuilder \
  --platform linux/amd64,linux/arm64 \
  --tag brightercommand/rabbitmq:3.13-management-delay \
  --push .
```
And don't forget to do latest
```shell
docker buildx build --builder cloud-brightercommand-jeffthebuilder \
  --platform linux/amd64,linux/arm64 \
  --tag brightercommand/rabbitmq:latest \
  --push .
```


