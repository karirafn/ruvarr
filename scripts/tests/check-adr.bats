#!/usr/bin/env bats

setup() {
  script="$BATS_TEST_DIRNAME/../check-adr.sh"
  adr_dir="$BATS_TEST_TMPDIR/adr"
  mkdir -p "$adr_dir"
}

write_adr() {
  local name="$1" reaches="${2:-structure}"
  printf -- '---\nreaches: %s\n---\n\n# Title\n' "$reaches" > "$adr_dir/$name"
}

@test "passes a directory of valid ADRs" {
  write_adr 0001-first-decision.md structure
  write_adr 0003-second-decision.md hidden-invariant

  run "$script" "$adr_dir"

  [ "$status" -eq 0 ]
}

@test "fails when two ADRs share a prefix, naming both files" {
  write_adr 0010-first-decision.md
  write_adr 0010-second-decision.md

  run "$script" "$adr_dir"

  [ "$status" -ne 0 ]
  [[ "$output" == *"0010-first-decision.md"* ]]
  [[ "$output" == *"0010-second-decision.md"* ]]
}

@test "fails on a file name that is not NNNN-kebab-slug.md, naming the file" {
  write_adr 0001-valid-decision.md
  write_adr 12-Bad_Name.md

  run "$script" "$adr_dir"

  [ "$status" -ne 0 ]
  [[ "$output" == *"12-Bad_Name.md"* ]]
  [[ "$output" != *"0001-valid-decision.md"* ]]
}

@test "fails on an ADR without front matter, naming the file" {
  printf '# Title\n\nreaches: structure\n' > "$adr_dir/0001-no-front-matter.md"

  run "$script" "$adr_dir"

  [ "$status" -ne 0 ]
  [[ "$output" == *"0001-no-front-matter.md"* ]]
}

@test "fails on front matter without a reaches key, naming the file" {
  printf -- '---\nstatus: accepted\n---\n\n# Title\n' > "$adr_dir/0001-missing-key.md"

  run "$script" "$adr_dir"

  [ "$status" -ne 0 ]
  [[ "$output" == *"0001-missing-key.md"* ]]
}

@test "fails on a reaches value outside the allowed list, naming the file" {
  write_adr 0001-invalid-value.md performance

  run "$script" "$adr_dir"

  [ "$status" -ne 0 ]
  [[ "$output" == *"0001-invalid-value.md"* ]]
}

@test "fails on front matter holding a key besides reaches, naming the file" {
  printf -- '---\nreaches: structure\nstatus: accepted\n---\n\n# Title\n' > "$adr_dir/0001-extra-key.md"

  run "$script" "$adr_dir"

  [ "$status" -ne 0 ]
  [[ "$output" == *"0001-extra-key.md"* ]]
}

@test "fails on front matter that is never closed, naming the file" {
  printf -- '---\nreaches: structure\n\n# Title\n' > "$adr_dir/0001-unclosed.md"

  run "$script" "$adr_dir"

  [ "$status" -ne 0 ]
  [[ "$output" == *"0001-unclosed.md"* ]]
}
