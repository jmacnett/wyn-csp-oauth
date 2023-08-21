# Wyn AAD B2C OAuth Custom Security Provider
The intent of this is to allow a loose passthrough of the de facto Wyn custom CSP implementation tailored towards AzureAD B2C OAuth access tokens, allowing users created in Wyn via an AAD SSO provider to request Wyn api tokens.

## Caveats
As of Wyn Enterprise 7.0.00189.0, the k8s container is running dotnet 6.0.2; as a result, the highest installable level of several packages is lower than the latest available LTS from nuget.

Packages of note that are required to be installed at a version outside of the latest available:
 - Microsoft.IdentityModel.Protocols.OpenIdConnect: 6.15.0
 - System.IdentityModel.Tokens.Jwt: 6.15.0
 - NpgSql: 6.0.9
 - System.Runtime.Caching: 6.0.0

Also of note: if you're building a harness to test this locally and/or by including it in the Dockerfile, you will want to target "net6.0" while the above remains accurate.

## Testing

### build image for local repository (using microk8s for testing)
`rm -rf bin/Debug/netstandard2.0/ && dotnet clean OAuthAPISecurityProvider.csproj && dotnet publish --sc true OAuthAPISecurityProvider.csproj && docker build . -t localhost:32000/wyn-b2c-csp:mytag && docker push localhost:32000/wyn-b2c-csp:mytag`

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



## Known Issues
There is more detail in the files, but currently, we're not able to validate Jwt tokens via the official .net method, as there is some sort of downstream library mismatch when the netstandard2.0 dlls of this CSP are loaded into Wyn for execution.  I've not been able to fully run this down as yet, but it's related to how it's referencing Microsoft.Identity.Json and the fully-internal JsonConvert deserializer.  So, while I've included a sample of what the proper implementation SHOULD look like, it's not operable today.

Instead, we're validating the tokens via tagging the optional B2C 'userinfo' endpoint, which is sufficent to make sure that the token is valid and not expired.   We are also explicitly verifying the issuer against the well-known endpoint value, and optionally providing a mechanism to allow one to provide a "valid audiences" list (comma-delimited set of AAD registration guids).  This appears to work well enough.

## Future State
In upcoming updates, I may provide sample microk8s patching and spinup/teardown scripts, if there is any interest.  

In a similar vein, I also may provide sample terraform scripts to configure the AAD B2C app registration, although that would be partially incomplete, insofar as spinning a fresh B2C instance with terraform today is not 100% scriptable if you're using the IdentityExperienceFramework Custom Policies, and if you're using Azure B2C at all, you probably are.

Obviously, if a day comes wherein we can mount the proper Jwt validator instead, I will make my best efforts to shift that over in a timely manner.

## Contributing
I'm happy to review any pull requests and discuss them, although I retain final say as to what goes into this repository.

## License Addendum/Clarifications
I do not have any influence, permissions, or other access to the upstream Wyn sample CSP repository, nor do I work for GrapeCity.  This plugin sample is presented as-is for those who may run into similar authentication problems as I did. No warranty is expressed or implied on any of the code or suggestions in this repository.