# note: this setup is for deploying to local microk8s w/ registry enabled.  Substitute your own registry and name as required.
REPO=mcisemi001.azurecr.io/wyn-enterprise-b2c-csp
IMG=$(REPO):`cat version.txt`

version:
	sh mkversion.sh

acrlogin:
	grep -q mcisemi001 ~/.docker/.token_seed || az acr login -n mcisemi001

build: version
	if [ -d bin/Debug/netstandard2.0/publish ]; then rm -rf bin/Debug/netstandard2.0/publish; fi
	dotnet clean OAuthAPISecurityProvider.csproj
	dotnet publish --sc true OAuthAPISecurityProvider.csproj

push: build acrlogin
	docker build . -t ${IMG}
	docker push ${IMG}
	echo 'Repo and tag:' ${IMG}

lastversion: 
	echo 'Repo and tag:' ${IMG}