FROM grapecityus/wyn-enterprise-k8s:latest

COPY --chmod=666 bin/Debug/netstandard2.0/publish/* /app/Server/SecurityProviders/

#COPY --chmod=666 clitest/publish/* /app/Server/SecurityProviders/
