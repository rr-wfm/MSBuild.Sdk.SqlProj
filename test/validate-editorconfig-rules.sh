#!/usr/bin/env bash

set -euo pipefail

# Regression check for .editorconfig-backed SQL analyzer severity overrides.
# This script builds the SDK first so the packaged DacpacTool under
# src/MSBuild.Sdk.SqlProj/tools/* is up to date before the fixture projects run.
#
# Run locally from the repo root:
#   bash ./test/validate-editorconfig-rules.sh

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
matrix_project="${repo_root}/test/TestProjectWithEditorConfigMatrix/TestProjectWithEditorConfigMatrix.csproj"

matrix_log="$(mktemp)"

cleanup() {
  rm -f "${matrix_log}"
  rm -rf \
    "${repo_root}/test/TestProjectWithEditorConfigMatrix/bin" \
    "${repo_root}/test/TestProjectWithEditorConfigMatrix/obj"
}
trap cleanup EXIT

require_log_contains() {
  local needle="$1"
  local file="$2"

  if ! grep -F "${needle}" "${file}" >/dev/null; then
    echo "Expected to find: ${needle}"
    echo
    cat "${file}"
    exit 1
  fi
}

require_log_not_contains() {
  local needle="$1"
  local file="$2"

  if grep -F "${needle}" "${file}" >/dev/null; then
    echo "Did not expect to find: ${needle}"
    echo
    cat "${file}"
    exit 1
  fi
}

run_and_capture() {
  local log_file="$1"
  shift

  set +e
  "$@" 2>&1 | tee "${log_file}"
  local cmd_exit=${PIPESTATUS[0]}
  set -e

  return "${cmd_exit}"
}

cd "${repo_root}"

echo "Building SDK package project so the packaged DacpacTool is current..."
# Keep the packaged SDK tool current before building the test fixtures.
dotnet build ./src/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj.csproj -c Release -nologo

# Qualified rule names in .editorconfig should:
# - promote SRD0068 to an error
# - suppress SRP0005 entirely
echo "Building editorconfig matrix fixture..."
if run_and_capture "${matrix_log}" dotnet build "${matrix_project}" -nologo; then
  matrix_exit=0
else
  matrix_exit=$?
fi

if [[ "${matrix_exit}" -eq 0 ]]; then
  echo "Expected the editorconfig matrix fixture to fail the build with SRD0068 as an error."
  echo
  cat "${matrix_log}"
  exit 1
fi

echo "Checking qualified editorconfig entries..."
require_log_contains "error SRD0068: SqlServer.Rules : Query statements should finish with a semicolon - ';'." "${matrix_log}"
require_log_not_contains "SRP0005: SqlServer.Rules : SET NOCOUNT ON is recommended to be enabled in stored procedures and triggers." "${matrix_log}"

echo "Checking invalid short rule ids were warned and ignored..."
require_log_contains "SQLPROJ0002" "${matrix_log}"
require_log_contains ".editorconfig rule id 'SRD0002' must use a fully qualified rule name" "${matrix_log}"
require_log_contains ".editorconfig rule id 'SRP0020' must use a fully qualified rule name" "${matrix_log}"
require_log_contains "warning SRD0002: SqlServer.Rules : Table does not have a primary key." "${matrix_log}"
require_log_contains "warning SRP0020: SqlServer.Rules : Table does not have a clustered index." "${matrix_log}"

echo "Editorconfig validation passed."
