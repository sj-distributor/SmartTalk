#!/usr/bin/env bash
#
# Local verification gate for RealtimeAiV2 hardening work.
#
# CI runs on TeamCity; this script is the developer-machine equivalent and the
# gate every hardening PR must pass before review. It runs three levels:
#
#   L1  build the whole solution + the RealtimeAiV2 test subset
#   L2  the full unit suite, tolerating exactly the known-quarantined failures
#   L3  the golden/characterization contract: those files must be untouched
#
# L3 is the non-breaking proof. A change that needs a golden edited is a
# deliberate behaviour change and must be signed off, not slipped through — so
# this script fails on it and the PR has to say so out loud.
#
# Usage:
#   scripts/verify-realtime-v2.sh            # all three levels
#   scripts/verify-realtime-v2.sh l1         # fast loop while editing (~15s)
#
# Raising the expected RealtimeAiV2 count as you add tests:
#   EXPECTED_V2_MIN=440 scripts/verify-realtime-v2.sh
# then update the default below in the same commit.
#
# Baseline on main @ 9a7535eec (macOS, .NET 8): build 0 errors ~11s,
# RealtimeAiV2 411 passing ~4s, full unit suite 691 passing / 2 failing.
# The ~15s build+subset loop is the intended edit-run cycle.

set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

# Forces English tooling output so the summary line parses on any machine.
export DOTNET_CLI_UI_LANGUAGE=en

SOLUTION="SmartTalk.sln"
UNIT_TESTS="src/SmartTalk.UnitTests/SmartTalk.UnitTests.csproj"
GOLDEN_DIR="src/SmartTalk.UnitTests/Services/RealtimeAiV2/Characterization"

# Minimum RealtimeAiV2 cases that must pass. Never lower this — the suite only grows.
EXPECTED_V2_MIN="${EXPECTED_V2_MIN:-477}"

# Pre-existing failures outside RealtimeAiV2, quarantined rather than fixed:
# they are not this effort's scope (CLAUDE.md Rule 3). L2 fails if the set of
# failing tests differs from this list in either direction — a quarantined test
# that starts passing is also a signal worth seeing.
QUARANTINED=(
  "SmartTalk.UnitTests.Services.Http.Clients.DaovikaClientTests.GetSalesGroupByPhoneNumberAsync_ShouldQueryDaovikaTableWithPhone"
  "SmartTalk.UnitTests.Services.PhoneOrder.PhoneOrderProcessJobServiceSummaryFlowTests.HandleReleasedDiarizedTranscribeAsync_ShortAiGreetingOnlyCall_ShouldCompleteWithFixedSummary"
)

RED=$'\033[31m'; GREEN=$'\033[32m'; YELLOW=$'\033[33m'; BOLD=$'\033[1m'; OFF=$'\033[0m'
FAILURES=0

step()  { printf '\n%s▸ %s%s\n' "$BOLD" "$1" "$OFF"; }
pass()  { printf '  %s✓%s %s\n' "$GREEN" "$OFF" "$1"; }
fail()  { printf '  %s✗%s %s\n' "$RED" "$OFF" "$1"; FAILURES=$((FAILURES + 1)); }
note()  { printf '  %s·%s %s\n' "$YELLOW" "$OFF" "$1"; }

# Extracts "Failed: N, Passed: N" from the dotnet test summary line.
counts_from() { sed -n 's/.*Failed:[[:space:]]*\([0-9]*\), Passed:[[:space:]]*\([0-9]*\).*/\1 \2/p' "$1" | tail -1; }

# xunit prints "<fully.qualified.name> [FAIL]"; the marker is not localized.
failed_names_from() { grep -o '[A-Za-z0-9_.]*\ \[FAIL\]' "$1" | sed 's/ \[FAIL\]//' | sort -u; }

run_build() {
  step "L1a  build $SOLUTION"
  local out; out=$(mktemp)
  if dotnet build "$SOLUTION" -v q --nologo >"$out" 2>&1; then
    pass "0 errors"
  else
    fail "build failed"
    grep -E ': error ' "$out" | head -20
  fi
  rm -f "$out"
}

run_l1() {
  step "L1b  RealtimeAiV2 subset (expect >= $EXPECTED_V2_MIN passing, 0 failing)"
  local out; out=$(mktemp)
  dotnet test "$UNIT_TESTS" --no-build -v q --nologo --filter "FullyQualifiedName~RealtimeAiV2" >"$out" 2>&1
  read -r failed passed <<<"$(counts_from "$out")"

  if [[ -z "${failed:-}" ]]; then
    fail "could not parse test summary"; tail -20 "$out"; rm -f "$out"; return
  fi
  if [[ "$failed" -ne 0 ]]; then
    fail "$failed failing"
    failed_names_from "$out" | sed 's/^/      /'
  elif [[ "$passed" -lt "$EXPECTED_V2_MIN" ]]; then
    fail "$passed passing, below baseline $EXPECTED_V2_MIN — tests were removed or silently skipped"
  else
    pass "$passed passing"
    [[ "$passed" -gt "$EXPECTED_V2_MIN" ]] && \
      note "$((passed - EXPECTED_V2_MIN)) above baseline — raise EXPECTED_V2_MIN in this commit"
  fi
  rm -f "$out"
}

run_l2() {
  step "L2   full unit suite (expect only the ${#QUARANTINED[@]} quarantined failures)"
  local out; out=$(mktemp)
  dotnet test "$UNIT_TESTS" --no-build -v q --nologo >"$out" 2>&1

  local expected actual
  expected=$(printf '%s\n' "${QUARANTINED[@]}" | sort -u)
  actual=$(failed_names_from "$out")

  if [[ "$actual" == "$expected" ]]; then
    pass "only the ${#QUARANTINED[@]} quarantined failures"
  else
    local new gone
    new=$(comm -13 <(printf '%s' "$expected") <(printf '%s' "$actual"))
    gone=$(comm -23 <(printf '%s' "$expected") <(printf '%s' "$actual"))
    [[ -n "$new" ]]  && { fail "NEW failures — this is a regression:"; printf '      %s\n' $new; }
    [[ -n "$gone" ]] && { fail "quarantined test now passes — remove it from QUARANTINED:"; printf '      %s\n' $gone; }
  fi
  rm -f "$out"
}

run_l3() {
  step "L3   golden contract ($(basename "$GOLDEN_DIR")/ must be untouched)"
  local changed; changed=$(git status --porcelain -- "$GOLDEN_DIR")

  if [[ -z "$changed" ]]; then
    pass "no golden files modified"
  else
    fail "golden files modified — this PR is a deliberate behaviour change"
    printf '      %s\n' "$changed"
    note "if intended: get sign-off and list every behaviour delta in the commit message"
  fi
}

case "${1:-all}" in
  l1)  run_build; run_l1 ;;
  l2)  run_l2 ;;
  l3)  run_l3 ;;
  all) run_build; run_l1; run_l2; run_l3 ;;
  *)   echo "usage: $0 [all|l1|l2|l3]" >&2; exit 2 ;;
esac

printf '\n'
if [[ "$FAILURES" -eq 0 ]]; then
  printf '%s%sGATE PASSED%s\n' "$BOLD" "$GREEN" "$OFF"; exit 0
else
  printf '%s%sGATE FAILED (%d)%s\n' "$BOLD" "$RED" "$FAILURES" "$OFF"; exit 1
fi
