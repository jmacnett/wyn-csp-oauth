# Wyn AAD B2C OAuth Custom Security Provider
The intent of this is to allow a loose passthrough of the de facto Wyn custom CSP implementation tailored towards AzureAD B2C OAuth access tokens, allowing users created in Wyn via an AAD SSO provider to request Wyn api tokens.

*__NOTE: At this time, the code sample as-is is not complete! Do not use!  This README will be updated once the contained code is completely verified.__*

## Caveats
As of Wyn Enterprise 6.1.00328.0, the k8s container is running dotnet 6.0.2; as a result, the highest installable level of several packages is lower than the latest available LTS from nuget.

Packages of note that are required to be installed at a version outside of the latest available:
 - Microsoft.IdentityModel.Protocols.OpenIdConnect: 6.15.0
 - System.IdentityModel.Tokens.Jwt: 6.15.0
 - NpgSql: 6.0.9
 - System.Runtime.Caching: 6.0.0

Also of note: if you're building a harness to test this locally and/or by including it in the Dockerfile, you will want to target "net6.0", as of this writing.

## Testing

### build image for local repository (using microk8s for testing)
`rm -rf bin/Debug/netstandard2.0/ && dotnet clean && dotnet publish --sc true && docker build . -t localhost:32000/wyn-b2c-csp:mytag && docker push localhost:32000/wyn-b2c-csp:mytag`

### direct run to look at deployed files
`microk8s kubectl run wyn-oauth-csp-test -i --rm --attach --image=localhost:32000/wyn-b2c-csp:mytag --command /bin/bash`

## Deployment
Wyn typically provides k8s manifests and AKS manifests seperately; I'd suggest you use kustomize to overlay your custom image value in the services/server.yaml, with something similar to 

```
- op: replace
  path: /spec/template/spec/containers/0/image
  value: "localhost:32000/wyn-b2c-csp:mytag"
```

and deploy as you normally would for kubernetes manifests, beyond any other kustomize changes you may have (probably have) made.

This implementation also is making the assumptions that:
- you've created a kubernetes secret for the purposes of holding the wynis database connection information, and
- said database server where wynis is hosted is postgresql.