#!/bin/sh
set -e
( \
	cat Dockerfile | grep 'grapecityus/wyn-enterprise' | sed 's/.*\://g'; \
	date '+%Y%m%d%H%M%S'; \
	#git describe --all --always --long --dirty | sed 's/.*-g//'; \
	[ -n "${AZURE_BUILDID}" ] && echo "ci${AZURE_BUILDID}"; \
) \
| tr \\n - \
| sed 's/-*$//' \
| tee version.txt
