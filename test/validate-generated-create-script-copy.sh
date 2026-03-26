#!/usr/bin/env bash

set -euo pipefail

# Regression check for issue #788:
# https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/issues/788
# A generated create script must be copied to the referencing project's output
# when the referenced SQL project sets GenerateCreateScript=True.
# Run locally from the repo root:
#   dotnet build ./src/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj.csproj -c Release && bash ./test/validate-generated-create-script-copy.sh

rm -rf \
  test/TestIncludeFromVanillaProjWithGeneratedScript/bin \
  test/TestIncludeFromVanillaProjWithGeneratedScript/obj \
  test/TestProjectWithGenerateScript/bin \
  test/TestProjectWithGenerateScript/obj

dotnet build ./test/TestIncludeFromVanillaProjWithGeneratedScript/TestIncludeFromVanillaProjWithGeneratedScript.csproj -nologo

compgen -G 'test/TestIncludeFromVanillaProjWithGeneratedScript/bin/Debug/*/TestProjectWithGenerateScript.dacpac' > /dev/null
compgen -G 'test/TestIncludeFromVanillaProjWithGeneratedScript/bin/Debug/*/TestProjectWithGenerateScript_Create.sql' > /dev/null
