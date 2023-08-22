# note: this setup is for deploying to local microk8s w/ registry enabled.  Substitute your own registry and name as required.
REPO=localhost:32000/wyn-b2c-csp
IMG=$(REPO):`cat version.txt`

version:
	sh mkversion.sh

build: version
	if [ -d bin/Debug/netstandard2.0/publish ]; then rm -rf bin/Debug/netstandard2.0/publish; fi
	dotnet clean OAuthAPISecurityProvider.csproj
	dotnet publish --sc true OAuthAPISecurityProvider.csproj

push: build
	docker build . -t ${IMG}
	docker push ${IMG}
	lastversion

lastversion: 
	echo 'Repo and tag:' ${IMG}