#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$repo_root"
target=Sql170

# Always use a fresh artifact directory; stale packages must not satisfy the tests.
mkdir -p "$repo_root/artifacts"
artifact_dir=$(mktemp -d "$repo_root/artifacts/packaged-XXXXXX")
work_dir=$(mktemp -d)
trap 'rm -rf "$work_dir"' EXIT
mkdir -p "$artifact_dir/packages" "$artifact_dir/dacpac" "$work_dir/project"

dotnet pack src/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj.csproj -c Release -o "$artifact_dir/packages"
dotnet build test/TestProject/TestProject.csproj -c Release -t:Rebuild -p:SqlServerVersion="$target"
dotnet pack test/TestProject/TestProject.csproj -c Release --no-build -p:SqlServerVersion="$target" -o "$artifact_dir/packages"
sdk_version=$(dotnet msbuild src/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj.csproj -target:GetBuildVersion -getProperty:PackageVersion)
dependency_version=$(dotnet msbuild test/TestProject/TestProject.csproj -target:GetBuildVersion -getProperty:PackageVersion)

cp -R test/TestProjectWithSDKRef/Tables test/TestProjectWithSDKRef/'Stored Procedures' \
  test/TestProjectWithSDKRef/Post-Deployment "$work_dir/project/"
sed "s/#{NBGV_NuGetPackageVersion}#/$sdk_version/g" \
  test/TestProjectWithSDKRef/TestProjectWithSDKRef.csproj > "$work_dir/project/TestProjectWithSDKRef.csproj"
cat > "$work_dir/project/nuget.config" <<'EOF'
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="artifacts">
      <package pattern="MSBuild.Sdk.SqlProj" />
      <package pattern="TestProject" />
    </packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
EOF
dotnet nuget add source "$artifact_dir/packages" --name artifacts --configfile "$work_dir/project/nuget.config"
NUGET_PACKAGES="$work_dir/packages" dotnet build "$work_dir/project/TestProjectWithSDKRef.csproj" -c Release \
  -p:DependencyVersion="$dependency_version" -p:SqlServerVersion="$target" /warnaserror:SQL71502 \
  "-bl:$artifact_dir/prepare.binlog;ProjectImports=None"
cp "$work_dir/project/bin/Release/net10.0/"*.dacpac "$artifact_dir/dacpac/"
printf '\nArtifacts ready. Run:\nSQLPROJ_TEST_ARTIFACTS=%q dotnet test test/PackagedDeploymentIntegrationTests/PackagedDeploymentIntegrationTests.csproj -c Release --logger trx --results-directory artifacts/packaged-results\n' \
  "$artifact_dir"
