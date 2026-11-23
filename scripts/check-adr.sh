#!/usr/bin/env bash
# Checks the ADRs under docs/adr/ (or the directory given as $1) and names every offending file.
set -euo pipefail

# Source: the gate under "## Decision records" in CLAUDE.md. Change both together.
readonly ALLOWED_REACHES=(structure module-contract external-contract non-functional dependency hidden-invariant)

readonly NAME_PATTERN='^[0-9]{4}-[a-z0-9]+(-[a-z0-9]+)*\.md$'

adr_dir="${1:-$(dirname "${BASH_SOURCE[0]}")/../docs/adr}"
if [[ ! -d "$adr_dir" ]]; then
  echo "ADR directory not found: $adr_dir" >&2
  exit 2
fi

failures=0
fail() {
  echo "$1: $2" >&2
  failures=$((failures + 1))
}

is_allowed_reaches() {
  local value
  for value in "${ALLOWED_REACHES[@]}"; do
    [[ "$1" == "$value" ]] && return 0
  done
  return 1
}

check_front_matter() {
  local file="$1" name="$2"
  local line line_number=0 closed=false reaches=""

  while IFS= read -r line || [[ -n "$line" ]]; do
    line_number=$((line_number + 1))
    line="${line%$'\r'}"
    if (( line_number == 1 )); then
      line="${line#$'\xEF\xBB\xBF'}"
      if [[ "$line" != "---" ]]; then
        fail "$name" "missing front matter (first line must be ---)"
        return
      fi
      continue
    fi
    if [[ "$line" == "---" ]]; then
      closed=true
      break
    fi
    [[ -z "$line" ]] && continue
    if [[ "$line" =~ ^reaches:[[:space:]]*(.*[^[:space:]])[[:space:]]*$ ]]; then
      if [[ -n "$reaches" ]]; then
        fail "$name" "front matter holds reaches: more than once"
        return
      fi
      reaches="${BASH_REMATCH[1]}"
    else
      fail "$name" "front matter may hold only reaches:, found: $line"
      return
    fi
  done < "$file"

  if (( line_number == 0 )); then
    fail "$name" "missing front matter (file is empty)"
  elif [[ "$closed" != true ]]; then
    fail "$name" "front matter is not closed with ---"
  elif [[ -z "$reaches" ]]; then
    fail "$name" "front matter lacks a reaches: value"
  elif ! is_allowed_reaches "$reaches"; then
    fail "$name" "reaches: '$reaches' is not one of: ${ALLOWED_REACHES[*]}"
  fi
}

declare -A files_by_prefix=()

shopt -s nullglob dotglob
for file in "$adr_dir"/*; do
  [[ -f "$file" ]] || continue
  name="$(basename "$file")"

  if [[ ! "$name" =~ $NAME_PATTERN ]]; then
    fail "$name" "file name does not match NNNN-kebab-slug.md"
    continue
  fi

  prefix="${name:0:4}"
  files_by_prefix[$prefix]+="${files_by_prefix[$prefix]:+ }$name"

  check_front_matter "$file" "$name"
done

for prefix in "${!files_by_prefix[@]}"; do
  read -r -a names <<< "${files_by_prefix[$prefix]}"
  if (( ${#names[@]} > 1 )); then
    fail "${names[*]}" "ADRs share the prefix $prefix"
  fi
done

if (( failures > 0 )); then
  echo "check-adr: $failures problem(s) found in $adr_dir" >&2
  exit 1
fi
echo "check-adr: all ADRs in $adr_dir pass"
