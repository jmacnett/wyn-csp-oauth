# note: this setup is for deploying to local microk8s w/ registry enabled.  Substitute your own registry and name as required.
REPO=localhost:32000/wyn-b2c-csp
IMG=$(REPO):`cat version.txt`

version:
	sh mkversion.sh

# only for local testing w/ microk8s or other local k8s
dotnet-build:
	if [ -d bin/Debug/netstandard2.0/publish ]; then rm -rf bin/Debug/netstandard2.0/publish; fi
	dotnet clean OAuthAPISecurityProvider.csproj
	dotnet publish --sc true OAuthAPISecurityProvider.csproj

build: version
	env DOCKER_BUILDKIT=1 docker build . -t ${IMG}

push: build
	docker push ${IMG}
	echo 'Repo and tag:' ${IMG}

lastversion: 
	echo 'Repo and tag:' ${IMG}
