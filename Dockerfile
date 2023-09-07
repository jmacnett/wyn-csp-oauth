FROM bitnami/dotnet-sdk:6

COPY . /build
WORKDIR /build
RUN dotnet build OAuthAPISecurityProvider.csproj

FROM grapecityus/wyn-enterprise-k8s:7.0.00221.0

COPY --from=0 /build/bin/Debug/netstandard2.0/OAuthAPISecurityProvider.dll /app/Server/SecurityProviders/OAuthAPISecurityProvider.dll
