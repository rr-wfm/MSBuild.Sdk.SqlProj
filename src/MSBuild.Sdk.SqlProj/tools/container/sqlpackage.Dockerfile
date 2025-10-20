# Base image: ships with the SDK
ARG BASE_IMAGE=mcr.microsoft.com/dotnet/runtime:8.0
FROM ${BASE_IMAGE}
ARG SQLPACKAGE_VERSION
ARG DACPAC_NAME
ENV DACPAC_NAME=${DACPAC_NAME}

# Install sqlpackage dependencies and pull from NuGet
# - Supports native (platform zip) and dotnet tool layouts
# - Installs a stable wrapper in /usr/local/bin/sqlpackage
RUN set -eux; \
    apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl unzip \
 && curl -L -o /tmp/sqlpackage.nupkg "https://globalcdn.nuget.org/packages/microsoft.sqlpackage.${SQLPACKAGE_VERSION}.nupkg" \
 && mkdir -p /opt/sqlpackage-src /opt/sqlpackage \
 && unzip -q /tmp/sqlpackage.nupkg -d /opt/sqlpackage-src \
 && inner="$(find /opt/sqlpackage-src -type f -iname '*linux-x64*.zip' | head -n1 || true)" \
 && if [ -n "$inner" ]; then \
        unzip -q "$inner" -d /opt/sqlpackage; \
    else \
        cp -R /opt/sqlpackage-src/tools/*/any/* /opt/sqlpackage/; \
    fi \
 && printf '#!/usr/bin/env bash\nset -euo pipefail\nif [ -x /opt/sqlpackage/sqlpackage ]; then exec /opt/sqlpackage/sqlpackage "$@"; elif [ -f /opt/sqlpackage/sqlpackage.dll ]; then exec dotnet /opt/sqlpackage/sqlpackage.dll "$@"; else echo "sqlpackage not found"; exit 127; fi\n' > /usr/local/bin/sqlpackage \
 && chmod +x /usr/local/bin/sqlpackage \
 && rm -rf /var/lib/apt/lists/* /tmp/* /opt/sqlpackage-src \
 # Create entrypoint wrapper as root:
 # - Bakes in `-Action:Publish` and `-SourceFile:/work/$DACPAC_NAME`
 # - Forwards user-supplied args to sqlpackage
 && install -d /usr/local/bin \
 && printf '%s\n' \
      '#!/usr/bin/env bash' \
      'set -euo pipefail' \
      'exec sqlpackage -Action:Publish -SourceFile:/work/"${DACPAC_NAME}" "$@"' \
   | tee /usr/local/bin/docker-entrypoint >/dev/null \
 && chmod +x /usr/local/bin/docker-entrypoint

# Staging: the SDK copies all dacpacs into /work at build time
WORKDIR /work
COPY ./.container/*.dacpac /work/

# Drop privileges to a non-root user
RUN useradd -m runner
USER runner

# Exec-form ENTRYPOINT for proper signal handling and linter compliance
ENTRYPOINT ["/usr/local/bin/docker-entrypoint"]
