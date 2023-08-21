FROM grapecityus/wyn-enterprise-k8s:7.0.00189.0

COPY --chmod=666 bin/Debug/netstandard2.0/publish/* /app/Server/SecurityProviders/
